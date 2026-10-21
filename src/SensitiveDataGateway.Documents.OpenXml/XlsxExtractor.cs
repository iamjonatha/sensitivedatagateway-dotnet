using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;

namespace SensitiveDataGateway.Documents.OpenXml;

/// <summary>
/// Reads the text out of an Excel workbook.
/// </summary>
/// <remarks>
/// <para>
/// The extractor produces one segment per row of each sheet, one per sheet name, one per
/// defined name, one per comment, and one per package property a person can type into. A row
/// carries the text of its cells resolved through the shared string table, together with the
/// formula that produced each value, because a formula is where a name assembled from
/// fragments lives.
/// </para>
/// <para>
/// It refuses rather than reads around anything it cannot account for: a workbook encrypted
/// with a password, a workbook whose formulas read cells of another file, and a package whose
/// markup the reader cannot parse. A part of the workbook that nothing scanned must not be
/// transmitted.
/// </para>
/// </remarks>
public sealed class XlsxExtractor : IDocumentExtractor
{
    private readonly OpenXmlExtractorOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="XlsxExtractor"/> class with the default
    /// bounds.
    /// </summary>
    public XlsxExtractor()
        : this(OpenXmlExtractorOptions.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XlsxExtractor"/> class.
    /// </summary>
    /// <param name="options">The bounds the extractor stays inside.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public XlsxExtractor(OpenXmlExtractorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        Version = ExtractorVersion.Create(ExtractorId.Create("xlsx"), "1.0.0");
    }

    /// <inheritdoc/>
    public ExtractorVersion Version { get; }

    /// <inheritdoc/>
    public bool CanExtract(string mediaType)
    {
        ArgumentNullException.ThrowIfNull(mediaType);

        return OpenXmlMediaTypes.Essence(mediaType) == OpenXmlMediaTypes.Spreadsheet;
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
            using var document = SpreadsheetDocument.Open(buffer, isEditable: false, OpenXmlSettings.For(_options));

            return new XlsxCollector(partId, _options).Collect(document, cancellationToken);
        }
        catch (Exception exception)
            when (exception is not SensitiveDataGatewayException && OpenXmlFailures.IsParseFailure(exception))
        {
            throw new ExtractionException(OpenXmlErrors.Malformed(partId), exception);
        }
    }
}
