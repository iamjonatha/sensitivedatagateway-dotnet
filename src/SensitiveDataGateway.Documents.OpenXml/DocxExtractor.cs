using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;

namespace SensitiveDataGateway.Documents.OpenXml;

/// <summary>
/// Reads the text out of a Word document.
/// </summary>
/// <remarks>
/// <para>
/// The extractor produces one segment per block of the body, one per row of each table, one
/// per header and footer, one per footnote and endnote, one per comment together with the
/// name of whoever left it, one per external link target, and one per package property a
/// person can type into.
/// </para>
/// <para>
/// It reads what a reader of the document cannot see as well as what they can: the text of a
/// tracked deletion is still in the file, and a name an author removed before sending is
/// exactly the kind of value this library exists to keep out of a request.
/// </para>
/// <para>
/// It refuses rather than reads around anything it cannot account for: a document encrypted
/// with a password, a document that imports a chunk in another format, a document assembled
/// from separate files, and a package whose markup the reader cannot parse. A part of the
/// document that nothing scanned must not be transmitted.
/// </para>
/// </remarks>
public sealed class DocxExtractor : IDocumentExtractor
{
    private readonly OpenXmlExtractorOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocxExtractor"/> class with the default
    /// bounds.
    /// </summary>
    public DocxExtractor()
        : this(OpenXmlExtractorOptions.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DocxExtractor"/> class.
    /// </summary>
    /// <param name="options">The bounds the extractor stays inside.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public DocxExtractor(OpenXmlExtractorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        Version = ExtractorVersion.Create(ExtractorId.Create("docx"), "1.0.0");
    }

    /// <inheritdoc/>
    public ExtractorVersion Version { get; }

    /// <inheritdoc/>
    public bool CanExtract(string mediaType)
    {
        ArgumentNullException.ThrowIfNull(mediaType);

        return OpenXmlMediaTypes.Essence(mediaType) == OpenXmlMediaTypes.Wordprocessing;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<TextSegment>> ExtractAsync(
        DocumentPart part,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(part);

        var buffer = await OpenXmlContent.ReadAsync(part, _options, cancellationToken).ConfigureAwait(false);

        await using (buffer.ConfigureAwait(false))
        {
            return Parse(part.Id, buffer, cancellationToken);
        }
    }

    private IReadOnlyList<TextSegment> Parse(
        PartId partId,
        System.IO.Stream buffer,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = WordprocessingDocument.Open(buffer, isEditable: false, OpenXmlSettings.For(_options));

            return new DocxCollector(partId, _options).Collect(document, cancellationToken);
        }
        catch (Exception exception)
            when (exception is not SensitiveDataGatewayException && OpenXmlFailures.IsParseFailure(exception))
        {
            throw new ExtractionException(OpenXmlErrors.Malformed(partId), exception);
        }
    }
}
