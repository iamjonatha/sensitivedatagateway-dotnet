using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SensitiveDataGateway.Documents.Pdf.Tests;

/// <summary>
/// Writes a PDF file object by object, with a correct cross-reference table.
/// </summary>
/// <remarks>
/// <para>
/// The extractor parses strictly, so a test document with a wrong offset fails for the wrong
/// reason. This writer keeps the offsets right while leaving every dictionary under the
/// test's control, which is what makes it possible to produce the constructs the document
/// builder shipped with the parser cannot produce: a form, an embedded file, an XFA stream
/// and an encryption dictionary.
/// </para>
/// <para>
/// Everything written here is ASCII, so a character offset is a byte offset and the table
/// can be computed from the text.
/// </para>
/// </remarks>
internal static class RawPdf
{
    private const string Header = "%PDF-1.7\n";

    /// <summary>
    /// Assembles a file from the bodies of its indirect objects.
    /// </summary>
    /// <param name="objects">The bodies, in order, numbered from one.</param>
    /// <param name="trailerEntries">The trailer entries other than <c>/Size</c> and <c>/Root</c>.</param>
    /// <returns>The bytes of the file.</returns>
    public static byte[] Build(IReadOnlyList<string> objects, string trailerEntries = "")
    {
        var builder = new StringBuilder(Header);
        var offsets = new List<int>(objects.Count);

        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(builder.Length);
            builder.Append(index + 1).Append(" 0 obj\n").Append(objects[index]).Append("\nendobj\n");
        }

        var tableOffset = builder.Length;

        AppendTable(builder, offsets);
        AppendTrailer(builder, objects.Count + 1, trailerEntries, tableOffset);

        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    /// <summary>
    /// Assembles the usual three objects of a one-page document.
    /// </summary>
    /// <param name="catalogEntries">The catalog entries other than <c>/Type</c> and <c>/Pages</c>.</param>
    /// <param name="pageEntries">The page entries other than <c>/Type</c>, <c>/Parent</c> and <c>/MediaBox</c>.</param>
    /// <param name="extraObjects">The objects the entries refer to, numbered from four.</param>
    /// <param name="trailerEntries">The trailer entries other than <c>/Size</c> and <c>/Root</c>.</param>
    /// <returns>The bytes of the file.</returns>
    public static byte[] OnePage(
        string catalogEntries = "",
        string pageEntries = "",
        IReadOnlyList<string>? extraObjects = null,
        string trailerEntries = "")
    {
        var objects = new List<string>
        {
            $"<< /Type /Catalog /Pages 2 0 R {catalogEntries} >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] {pageEntries} >>",
        };

        objects.AddRange(extraObjects ?? []);

        return Build(objects, trailerEntries);
    }

    /// <summary>
    /// Wraps a payload in a stream object of the right declared length.
    /// </summary>
    /// <param name="entries">The stream dictionary entries other than <c>/Length</c>.</param>
    /// <param name="payload">The uncompressed payload.</param>
    /// <returns>The body of the object.</returns>
    public static string Stream(string entries, string payload) =>
        $"<< {entries} /Length {payload.Length} >>\nstream\n{payload}\nendstream";

    private static void AppendTable(StringBuilder builder, List<int> offsets)
    {
        builder.Append("xref\n0 ").Append(offsets.Count + 1).Append('\n');
        builder.Append("0000000000 65535 f \n");

        foreach (var offset in offsets)
        {
            builder.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }
    }

    private static void AppendTrailer(StringBuilder builder, int size, string entries, int tableOffset) =>
        builder
            .Append("trailer\n<< /Size ")
            .Append(size)
            .Append(" /Root 1 0 R ")
            .Append(entries)
            .Append(" >>\nstartxref\n")
            .Append(tableOffset)
            .Append("\n%%EOF\n");
}
