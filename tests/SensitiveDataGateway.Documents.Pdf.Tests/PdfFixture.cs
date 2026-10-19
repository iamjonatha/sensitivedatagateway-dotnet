using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Documents;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace SensitiveDataGateway.Documents.Pdf.Tests;

/// <summary>
/// Builds the documents the tests read.
/// </summary>
/// <remarks>
/// Every value in every document here is invented. The names, the addresses and the account
/// numbers belong to the <c>.invalid</c> reserved domain or to the synthetic corpus, and
/// none of them identifies anybody.
/// </remarks>
internal static class PdfFixture
{
    /// <summary>
    /// Builds a document whose pages carry the given lines of text.
    /// </summary>
    /// <param name="pages">One entry per page.</param>
    /// <returns>The bytes of the document.</returns>
    public static byte[] WithPages(params string[] pages)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        foreach (var text in pages)
        {
            var page = builder.AddPage(612, 792);
            page.AddText(text, 12, new PdfPoint(50, 700), font);
        }

        return builder.Build();
    }

    /// <summary>
    /// Builds a one-page document with entries in its information dictionary.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="author">The author.</param>
    /// <returns>The bytes of the document.</returns>
    public static byte[] WithInformation(string title, string author)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        builder.DocumentInformation.Title = title;
        builder.DocumentInformation.Author = author;

        var page = builder.AddPage(612, 792);
        page.AddText("Body", 12, new PdfPoint(50, 700), font);

        return builder.Build();
    }

    /// <summary>
    /// Builds a one-page document with a link anchored to the page.
    /// </summary>
    /// <param name="uri">The target of the link.</param>
    /// <returns>The bytes of the document.</returns>
    public static byte[] WithLink(string uri)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        var page = builder.AddPage(612, 792);
        page.AddText("Contact", 12, new PdfPoint(50, 700), font);
        page.AddLink(uri, new PdfRectangle(50, 700, 150, 720));

        return builder.Build();
    }

    /// <summary>
    /// Builds a document whose single page draws an image and writes nothing.
    /// </summary>
    /// <returns>The bytes of the document.</returns>
    public static byte[] WithImageOnlyPage()
    {
        using var builder = new PdfDocumentBuilder();

        var page = builder.AddPage(612, 792);
        page.AddPng(Png(), new PdfRectangle(50, 600, 150, 700));

        return builder.Build();
    }

    /// <summary>
    /// Wraps bytes in a part the extractor accepts.
    /// </summary>
    /// <param name="bytes">The document.</param>
    /// <returns>The part.</returns>
    public static DocumentPart Part(byte[] bytes) =>
        DocumentPart.Create(
            PartId.Create("p1"),
            PartKind.Body,
            "application/pdf",
            DocumentContent.FromBytes(bytes));

    /// <summary>
    /// Reads a document with the default options.
    /// </summary>
    /// <param name="bytes">The document.</param>
    /// <returns>The segments.</returns>
    public static ValueTask<IReadOnlyList<TextSegment>> ReadAsync(byte[] bytes) =>
        new PdfExtractor().ExtractAsync(Part(bytes), CancellationToken.None);

    /// <summary>
    /// Returns the bytes of a one-pixel opaque PNG.
    /// </summary>
    /// <returns>The image.</returns>
    private static byte[] Png() =>
        Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
}
