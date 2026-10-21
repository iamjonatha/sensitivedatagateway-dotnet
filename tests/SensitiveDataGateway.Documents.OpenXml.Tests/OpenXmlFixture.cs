using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Documents.OpenXml.Tests;

/// <summary>
/// The scaffolding the Open XML tests share.
/// </summary>
/// <remarks>
/// Every value in every document built for these tests is invented. The names, the addresses
/// and the account numbers belong to the <c>.invalid</c> reserved domain or to the synthetic
/// corpus, and none of them identifies anybody.
/// </remarks>
internal static class OpenXmlFixture
{
    /// <summary>
    /// The media type of a Word document.
    /// </summary>
    public const string WordMediaType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    /// <summary>
    /// The media type of an Excel workbook.
    /// </summary>
    public const string ExcelMediaType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>
    /// Wraps bytes in a part the given extractor accepts.
    /// </summary>
    /// <param name="bytes">The document.</param>
    /// <param name="mediaType">The media type.</param>
    /// <returns>The part.</returns>
    public static DocumentPart Part(byte[] bytes, string mediaType) =>
        DocumentPart.Create(
            PartId.Create("p1"),
            PartKind.Body,
            mediaType,
            DocumentContent.FromBytes(bytes));

    /// <summary>
    /// Reads a Word document with the default options.
    /// </summary>
    /// <param name="bytes">The document.</param>
    /// <returns>The segments.</returns>
    public static ValueTask<IReadOnlyList<TextSegment>> ReadDocxAsync(byte[] bytes) =>
        new DocxExtractor().ExtractAsync(Part(bytes, WordMediaType), CancellationToken.None);

    /// <summary>
    /// Reads an Excel workbook with the default options.
    /// </summary>
    /// <param name="bytes">The workbook.</param>
    /// <returns>The segments.</returns>
    public static ValueTask<IReadOnlyList<TextSegment>> ReadXlsxAsync(byte[] bytes) =>
        new XlsxExtractor().ExtractAsync(Part(bytes, ExcelMediaType), CancellationToken.None);

    /// <summary>
    /// Reads a Word document and returns the error it was refused with.
    /// </summary>
    /// <param name="bytes">The document.</param>
    /// <returns>The error.</returns>
    public static async Task<GatewayError> RefusedDocxAsync(byte[] bytes)
    {
        var failure = await Assert.ThrowsAsync<ExtractionException>(
            async () => await ReadDocxAsync(bytes));

        return failure.Error;
    }

    /// <summary>
    /// Reads an Excel workbook and returns the error it was refused with.
    /// </summary>
    /// <param name="bytes">The workbook.</param>
    /// <returns>The error.</returns>
    public static async Task<GatewayError> RefusedXlsxAsync(byte[] bytes)
    {
        var failure = await Assert.ThrowsAsync<ExtractionException>(
            async () => await ReadXlsxAsync(bytes));

        return failure.Error;
    }

    /// <summary>
    /// Joins the text of every segment, one per line.
    /// </summary>
    /// <param name="segments">The segments.</param>
    /// <returns>The text.</returns>
    public static string TextOf(IEnumerable<TextSegment> segments) =>
        string.Join(Environment.NewLine, segments.Select(segment => segment.Text));
}
