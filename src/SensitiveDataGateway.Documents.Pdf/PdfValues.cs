using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UglyToad.PdfPig.AcroForms.Fields;
using UglyToad.PdfPig.Actions;
using UglyToad.PdfPig.Annotations;
using UglyToad.PdfPig.Content;

namespace SensitiveDataGateway.Documents.Pdf;

/// <summary>
/// Reads the values a person typed, out of the places a PDF keeps them.
/// </summary>
internal static class PdfValues
{
    private static readonly UTF8Encoding s_strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// The entries of the document information dictionary that are read.
    /// </summary>
    /// <remarks>
    /// These six are free text a person wrote. The dates are deliberately left out: they are
    /// machine-written timestamps in a fixed syntax, and feeding them to a detector buys
    /// nothing but dates that look like a date of birth.
    /// </remarks>
    public static IReadOnlyList<(string Name, Func<DocumentInformation, string?> Read)> Information { get; } =
    [
        ("title", information => information.Title),
        ("author", information => information.Author),
        ("subject", information => information.Subject),
        ("keywords", information => information.Keywords),
        ("creator", information => information.Creator),
        ("producer", information => information.Producer),
    ];

    /// <summary>
    /// Returns the text of a page, in reading order.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <returns>The text.</returns>
    /// <remarks>
    /// Reading order matters here more than it does in a viewer. Two columns read across
    /// instead of down interleave the lines of one with the lines of the other, and an IBAN
    /// cut in half by a line from the neighbouring column is an IBAN no detector matches.
    /// </remarks>
    public static string TextOf(Page page) =>
        UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(
            page,
            addDoubleNewline: true);

    /// <summary>
    /// Returns the text an annotation carries, including whatever its action points at.
    /// </summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The text, which is empty when the annotation carries none.</returns>
    /// <remarks>
    /// The target of an action is read because <c>mailto:</c> in a link is an address like
    /// any other, and a link is exactly where an address survives a review of the visible
    /// page.
    /// </remarks>
    public static string TextOf(Annotation annotation)
    {
        var target = TargetOf(annotation.Action);
        var content = annotation.Content;

        if (string.IsNullOrWhiteSpace(target))
        {
            return content ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(content) ? target : string.Concat(content, "\n", target);
    }

    /// <summary>
    /// Returns the value held in a form field.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <returns>The value, or <see langword="null"/> when the field holds no free text.</returns>
    /// <remarks>
    /// Only the fields a person types into or chooses from are read. A checkbox and a radio
    /// button hold a state name written by whoever built the form, which is structure rather
    /// than something anybody disclosed.
    /// </remarks>
    public static string? ValueOf(AcroFieldBase field) => field switch
    {
        AcroTextField text => text.Value,
        AcroComboBoxField combo => Join(combo.SelectedOptions),
        AcroListBoxField list => Join(list.SelectedOptions),
        _ => null,
    };

    /// <summary>
    /// Decodes an XMP packet as text, without parsing it as XML.
    /// </summary>
    /// <param name="bytes">The bytes of the packet.</param>
    /// <returns>The text.</returns>
    /// <exception cref="DecoderFallbackException">The packet is not valid in its encoding.</exception>
    /// <remarks>
    /// <para>
    /// The packet holds the title, the author and the keywords a second time, so it has to be
    /// scanned. It is handled as a string and never handed to an XML reader: the packet is
    /// attacker-supplied XML, and an XML reader is the component that resolves a document type
    /// definition, an external entity and an entity that expands a thousandfold.
    /// </para>
    /// <para>
    /// The markup stays in the text. A detector matching an address inside an element is
    /// right to match it; what matters is that nothing in this library ever acts on a tag.
    /// </para>
    /// </remarks>
    public static string DecodeXmp(ReadOnlySpan<byte> bytes)
    {
        using var stream = new MemoryStream(bytes.ToArray(), writable: false);
        using var reader = new StreamReader(stream, s_strictUtf8, detectEncodingFromByteOrderMarks: true);

        return reader.ReadToEnd();
    }

    private static string? TargetOf(PdfAction? action) => action switch
    {
        UriAction uri => uri.Uri,
        GoToRAction remote => remote.Filename,
        GoToEAction embedded => embedded.FileSpecification,
        LaunchAction launch => launch.FileName,
        SubmitFormAction submit => submit.FileName,
        ImportDataAction import => import.FileName,
        JavaScriptAction script => script.JavaScript,
        _ => null,
    };

    private static string? Join(IReadOnlyList<string> options)
    {
        if (options.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();

        foreach (var option in options)
        {
            builder.Append(option).Append('\n');
        }

        return builder.ToString();
    }
}
