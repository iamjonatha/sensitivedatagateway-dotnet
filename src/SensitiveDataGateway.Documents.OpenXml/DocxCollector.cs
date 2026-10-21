using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace SensitiveDataGateway.Documents.OpenXml;

/// <summary>
/// Walks a Word document and records everything it holds as text.
/// </summary>
internal sealed class DocxCollector(PartId partId, OpenXmlExtractorOptions options)
{
    private const string WordprocessingNamespace =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private readonly OpenXmlSegments _segments = new(partId, options);

    private int _paragraphs;
    private int _tables;
    private int _headers;
    private int _annotations;

    /// <summary>
    /// Collects every segment of the document.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">The token to cancel the walk with.</param>
    /// <returns>The segments.</returns>
    /// <exception cref="ExtractionException">
    /// The document has no main part, or holds content this extractor will not read around.
    /// </exception>
    public IReadOnlyList<TextSegment> Collect(
        WordprocessingDocument document,
        CancellationToken cancellationToken)
    {
        var main = document.MainDocumentPart
            ?? throw new ExtractionException(OpenXmlErrors.Headless(partId));

        var root = main.Document
            ?? throw new ExtractionException(OpenXmlErrors.Headless(partId));

        Refuse(root);

        _segments.AddProperties(document);
        AddBlocks(root.Body, cancellationToken);
        AddHeadersAndFooters(main, cancellationToken);
        AddNotes(main, cancellationToken);
        AddComments(main, cancellationToken);
        AddHyperlinks(main);

        return _segments.ToList();
    }

    /// <summary>
    /// Refuses a document whose text is not all in its own markup.
    /// </summary>
    /// <param name="root">The root element of the document.</param>
    /// <exception cref="ExtractionException">The document embeds or links other content.</exception>
    /// <remarks>
    /// <para>
    /// An <c>altChunk</c> carries a chunk of HTML, RTF or another document format that the
    /// word processor imports on open; a <c>subDoc</c> names a separate file whose text
    /// appears in the master document. Either way part of what a reader sees is not in the
    /// markup this extractor reads, and the right answer to "I can only read some of this" is
    /// to stop.
    /// </para>
    /// <para>
    /// The elements are matched by name rather than by the type the SDK gave them. An element
    /// the reader did not expect where it found it is handed back untyped, and a refusal that
    /// depends on the reader's typing is a refusal a misplaced element walks straight past.
    /// </para>
    /// </remarks>
    private void Refuse(Document root)
    {
        if (Holds(root, "altChunk"))
        {
            throw new ExtractionException(OpenXmlErrors.EmbeddedChunk(partId));
        }

        if (Holds(root, "subDoc"))
        {
            throw new ExtractionException(OpenXmlErrors.ExternalContent(partId));
        }
    }

    private static bool Holds(Document root, string localName) =>
        root.Descendants().Any(element =>
            element.LocalName == localName
            && element.NamespaceUri == WordprocessingNamespace);

    /// <summary>
    /// Records the blocks of a container, one segment each.
    /// </summary>
    /// <param name="container">The body, a header, a footer or a note.</param>
    /// <param name="cancellationToken">The token to cancel the walk with.</param>
    private void AddBlocks(OpenXmlElement? container, CancellationToken cancellationToken)
    {
        if (container is null)
        {
            return;
        }

        foreach (var element in container.ChildElements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (element is Table table)
            {
                AddTable(table);
            }
            else
            {
                AddParagraph(element);
            }
        }
    }

    private void AddParagraph(OpenXmlElement element)
    {
        _paragraphs++;

        _segments.Add(
            SegmentIds.Paragraph(partId, _paragraphs),
            OpenXmlText.Flatten(element),
            SegmentOrigin.At(SegmentOriginKind.Paragraph, _paragraphs));
    }

    /// <summary>
    /// Records a table, one segment per row.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <remarks>
    /// A row rather than a cell, because an author who puts a bank account in one column and
    /// its holder in the next has written one fact across two cells, and a detector handed
    /// the cells separately sees neither.
    /// </remarks>
    private void AddTable(Table table)
    {
        _tables++;
        var ordinal = 0;

        foreach (var row in table.Elements<TableRow>())
        {
            ordinal++;

            _segments.Add(
                SegmentIds.Row(partId, _tables, ordinal),
                OpenXmlText.FlattenRow(row),
                SegmentOrigin.At(SegmentOriginKind.Row, ordinal));
        }
    }

    private void AddHeadersAndFooters(MainDocumentPart main, CancellationToken cancellationToken)
    {
        foreach (var header in main.HeaderParts)
        {
            AddRunningBlock(header.Header, cancellationToken);
        }

        foreach (var footer in main.FooterParts)
        {
            AddRunningBlock(footer.Footer, cancellationToken);
        }
    }

    private void AddRunningBlock(OpenXmlElement? container, CancellationToken cancellationToken)
    {
        if (container is null)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        _headers++;

        _segments.Add(
            SegmentIds.Header(partId, _headers),
            OpenXmlText.Flatten(container),
            SegmentOrigin.At(SegmentOriginKind.Header, _headers));
    }

    /// <summary>
    /// Records footnotes and endnotes as further blocks of the document.
    /// </summary>
    /// <param name="main">The main document part.</param>
    /// <param name="cancellationToken">The token to cancel the walk with.</param>
    private void AddNotes(MainDocumentPart main, CancellationToken cancellationToken)
    {
        foreach (var note in main.FootnotesPart?.Footnotes?.Elements<Footnote>() ?? [])
        {
            AddBlocks(note, cancellationToken);
        }

        foreach (var note in main.EndnotesPart?.Endnotes?.Elements<Endnote>() ?? [])
        {
            AddBlocks(note, cancellationToken);
        }
    }

    /// <summary>
    /// Records every comment, together with the name of whoever left it.
    /// </summary>
    /// <param name="main">The main document part.</param>
    /// <param name="cancellationToken">The token to cancel the walk with.</param>
    private void AddComments(MainDocumentPart main, CancellationToken cancellationToken)
    {
        foreach (var comment in main.WordprocessingCommentsPart?.Comments?.Elements<Comment>() ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            _annotations++;

            var author = comment.Author?.Value;
            var text = string.IsNullOrWhiteSpace(author)
                ? OpenXmlText.Flatten(comment)
                : $"{author}\n{OpenXmlText.Flatten(comment)}";

            _segments.Add(
                SegmentIds.Annotation(partId, _annotations),
                text,
                SegmentOrigin.At(SegmentOriginKind.Annotation, _annotations));
        }
    }

    /// <summary>
    /// Records the target of every link, as text.
    /// </summary>
    /// <param name="main">The main document part.</param>
    /// <remarks>
    /// The target is read, never opened. A link is frequently where an address or an account
    /// reference is written down — <c>mailto:</c> most obviously — so it is a string the
    /// detectors have to see, and it is nothing more than a string here.
    /// </remarks>
    private void AddHyperlinks(MainDocumentPart main)
    {
        foreach (var relationship in main.HyperlinkRelationships)
        {
            if (!relationship.IsExternal)
            {
                continue;
            }

            _annotations++;

            _segments.Add(
                SegmentIds.Annotation(partId, _annotations),
                relationship.Uri.ToString(),
                SegmentOrigin.At(SegmentOriginKind.Annotation, _annotations));
        }
    }
}
