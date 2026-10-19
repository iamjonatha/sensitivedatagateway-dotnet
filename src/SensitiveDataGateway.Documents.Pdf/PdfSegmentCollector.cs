using System.Collections.Generic;
using System.Text;
using System.Threading;
using UglyToad.PdfPig;
using UglyToad.PdfPig.AcroForms;
using UglyToad.PdfPig.AcroForms.Fields;
using UglyToad.PdfPig.Content;

namespace SensitiveDataGateway.Documents.Pdf;

/// <summary>
/// Turns one open document into segments, and refuses the document when part of it cannot be
/// read.
/// </summary>
/// <remarks>
/// <para>
/// The order the segments come out in is the order a reader meets them: the document
/// information, then the XMP packet, then each page with the annotations anchored to it,
/// then the form. The order is an index into the document for a person reading a report; it
/// is not an ordering anything downstream depends on.
/// </para>
/// <para>
/// Every identifier is derived from an ordinal. None of them is derived from a title, a
/// field name or a bookmark, because a segment identifier reaches a provider and document
/// content does not.
/// </para>
/// </remarks>
internal sealed class PdfSegmentCollector
{
    /// <summary>
    /// The deepest a form field tree may nest.
    /// </summary>
    /// <remarks>
    /// A form nested deeper than this is not a form somebody built; it is a tree assembled to
    /// exhaust the stack of whatever reads it.
    /// </remarks>
    private const int MaxFieldDepth = 32;

    private readonly PartId _partId;
    private readonly PdfExtractorOptions _options;
    private readonly List<TextSegment> _segments = [];
    private int _annotations;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSegmentCollector"/> class.
    /// </summary>
    /// <param name="partId">The part being read.</param>
    /// <param name="options">The bounds the read stays inside.</param>
    public PdfSegmentCollector(PartId partId, PdfExtractorOptions options)
    {
        _partId = partId;
        _options = options;
    }

    /// <summary>
    /// Reads the whole document.
    /// </summary>
    /// <param name="document">The open document.</param>
    /// <param name="cancellationToken">The token to cancel the read with.</param>
    /// <returns>The segments.</returns>
    /// <exception cref="ExtractionException">Part of the document could not be read.</exception>
    public IReadOnlyList<TextSegment> Collect(PdfDocument document, CancellationToken cancellationToken)
    {
        var form = document.TryGetForm(out var acroForm) ? acroForm : null;

        Refuse(document, form);
        AddInformation(document.Information);
        AddXmp(document);
        AddPages(document, cancellationToken);
        AddForm(form);

        return _segments;
    }

    /// <summary>
    /// Refuses a document whose content cannot all be accounted for.
    /// </summary>
    /// <param name="document">The open document.</param>
    /// <param name="form">The form, when the document has one.</param>
    /// <exception cref="ExtractionException">The document is refused.</exception>
    /// <remarks>
    /// These checks run before a single segment is produced. A document that carries an
    /// embedded file or an XFA form would otherwise come back looking like a successful
    /// read, and a successful read of part of a document is the failure this library exists
    /// to prevent.
    /// </remarks>
    private void Refuse(PdfDocument document, AcroForm? form)
    {
        if (document.NumberOfPages > _options.MaxPageCount)
        {
            throw new ExtractionException(PdfErrors.TooManyPages(_partId, _options.MaxPageCount));
        }

        if (document.Advanced.TryGetEmbeddedFiles(out var files) && files.Count > 0)
        {
            throw new ExtractionException(PdfErrors.EmbeddedFile(_partId));
        }

        if (form is not null && form.Dictionary.Data.ContainsKey("XFA"))
        {
            throw new ExtractionException(PdfErrors.XfaForm(_partId));
        }
    }

    private void AddInformation(DocumentInformation information)
    {
        foreach (var (name, read) in PdfValues.Information)
        {
            AddMetadata(name, read(information));
        }
    }

    /// <summary>
    /// Adds the XMP packet, decoded as text.
    /// </summary>
    /// <param name="document">The open document.</param>
    /// <exception cref="ExtractionException">The packet is not valid in its encoding.</exception>
    private void AddXmp(PdfDocument document)
    {
        if (!document.TryGetXmpMetadata(out var xmp))
        {
            return;
        }

        try
        {
            AddMetadata("xmp", PdfValues.DecodeXmp(xmp.GetXmlBytes()));
        }
        catch (DecoderFallbackException exception)
        {
            throw new ExtractionException(PdfErrors.Malformed(_partId), exception);
        }
    }

    private void AddPages(PdfDocument document, CancellationToken cancellationToken)
    {
        for (var number = 1; number <= document.NumberOfPages; number++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddPage(document.GetPage(number), number);
        }
    }

    /// <summary>
    /// Adds one page, and the annotations anchored to it.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="number">The one-based page number.</param>
    /// <exception cref="ExtractionException">The page is an image of text.</exception>
    /// <remarks>
    /// A page that draws something and yields no text is a scan. Accepting it would report a
    /// clean document whose every word went unread, so it is refused until an optical reader
    /// is configured to turn it into text.
    /// </remarks>
    private void AddPage(Page page, int number)
    {
        var text = PdfValues.TextOf(page);
        var empty = string.IsNullOrWhiteSpace(text);

        if (empty && page.NumberOfImages > 0)
        {
            throw new ExtractionException(PdfErrors.ImageOnlyPage(_partId, number));
        }

        if (!empty)
        {
            Add(SegmentIds.Page(_partId, number), text, SegmentOrigin.At(SegmentOriginKind.Page, number));
        }

        foreach (var annotation in page.GetAnnotations())
        {
            AddAnnotation(PdfValues.TextOf(annotation));
        }
    }

    /// <summary>
    /// Adds the values held in the form.
    /// </summary>
    /// <param name="form">The form, when the document has one.</param>
    /// <remarks>
    /// A field value is recorded as an annotation because that is what it is: a field is
    /// drawn by a widget annotation, and the value is reached through it. Treating it as one
    /// also means the value of a field spanning no page still gets an ordinal.
    /// </remarks>
    private void AddForm(AcroForm? form)
    {
        if (form is null)
        {
            return;
        }

        AddFields(form.Fields, depth: 0);
    }

    private void AddFields(IReadOnlyList<AcroFieldBase> fields, int depth)
    {
        if (depth > MaxFieldDepth)
        {
            throw new ExtractionException(PdfErrors.Malformed(_partId));
        }

        foreach (var field in fields)
        {
            if (field is AcroNonTerminalField parent)
            {
                AddFields(parent.Children, depth + 1);
                continue;
            }

            AddAnnotation(PdfValues.ValueOf(field));
        }
    }

    private void AddAnnotation(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var ordinal = ++_annotations;

        Add(
            SegmentIds.Annotation(_partId, ordinal),
            text,
            SegmentOrigin.At(SegmentOriginKind.Annotation, ordinal));
    }

    private void AddMetadata(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        Add(SegmentIds.Metadata(_partId, name), value, SegmentOrigin.Named(SegmentOriginKind.Metadata, name));
    }

    private void Add(SegmentId id, string text, SegmentOrigin origin) =>
        _segments.Add(TextSegment.Create(id, _partId, text, origin, _segments.Count));
}
