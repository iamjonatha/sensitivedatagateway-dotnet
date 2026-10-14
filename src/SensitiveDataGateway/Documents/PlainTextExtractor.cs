using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SensitiveDataGateway.Documents;

/// <summary>
/// Reads parts that declare a textual media type.
/// </summary>
/// <remarks>
/// <para>
/// This extractor accepts <c>text/*</c> and nothing else. It is deliberately not a universal
/// fallback: handing a PDF or a ZIP to a text reader produces mojibake, and mojibake is
/// scanned, found clean and transmitted. A part no extractor claims is recorded as a failure,
/// which blocks, and blocking is the safe outcome.
/// </para>
/// <para>
/// Bytes are decoded as strict UTF-8. An invalid byte raises
/// <see cref="ExtractionException"/> rather than being replaced, because a replacement
/// character in the middle of a tax code is a match the scan will not make.
/// </para>
/// <para>
/// Text is split into paragraphs on blank lines, not into lines. A line split would cut an
/// IBAN or a card number written across two lines into two halves, neither of which matches,
/// and a detector that cannot see a value cannot report it.
/// </para>
/// </remarks>
public sealed class PlainTextExtractor : IDocumentExtractor
{
    /// <summary>
    /// The media type prefix this extractor accepts.
    /// </summary>
    public const string AcceptedMediaTypePrefix = "text/";

    private static readonly UTF8Encoding s_strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Initializes a new instance of the <see cref="PlainTextExtractor"/> class.
    /// </summary>
    public PlainTextExtractor() => Version = ExtractorVersion.Create(ExtractorId.Create("builtin.text"), "1.0.0");

    /// <inheritdoc/>
    public ExtractorVersion Version { get; }

    /// <inheritdoc/>
    public bool CanExtract(string mediaType)
    {
        ArgumentNullException.ThrowIfNull(mediaType);

        return mediaType.StartsWith(AcceptedMediaTypePrefix, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<TextSegment>> ExtractAsync(
        DocumentPart part,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(part);

        var text = await ReadAsync(part, cancellationToken).ConfigureAwait(false);

        return SplitIntoParagraphs(part.Id, text);
    }

    private static async ValueTask<string> ReadAsync(DocumentPart part, CancellationToken cancellationToken)
    {
        if (part.Content.Kind == DocumentContentKind.Text)
        {
            return part.Content.AsText();
        }

        if (part.Content.Kind == DocumentContentKind.Bytes)
        {
            return Decode(part.Id, part.Content.AsBytes().Span);
        }

        return await ReadStreamAsync(part, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<string> ReadStreamAsync(DocumentPart part, CancellationToken cancellationToken)
    {
        var stream = await part.Content.OpenStreamAsync(cancellationToken).ConfigureAwait(false);

        await using (stream.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

            return Decode(part.Id, buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        }
    }

    private static string Decode(PartId partId, ReadOnlySpan<byte> bytes)
    {
        try
        {
            return s_strictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new ExtractionException(ExtractionErrors.Undecodable(partId), exception);
        }
    }

    private static List<TextSegment> SplitIntoParagraphs(PartId partId, string text)
    {
        var segments = new List<TextSegment>();
        var paragraph = new StringBuilder();

        foreach (var line in text.Split('\n'))
        {
            var content = line.TrimEnd('\r');

            if (string.IsNullOrWhiteSpace(content))
            {
                Flush(segments, partId, paragraph);
                continue;
            }

            if (paragraph.Length > 0)
            {
                paragraph.Append('\n');
            }

            paragraph.Append(content);
        }

        Flush(segments, partId, paragraph);

        return segments;
    }

    private static void Flush(List<TextSegment> segments, PartId partId, StringBuilder paragraph)
    {
        if (paragraph.Length == 0)
        {
            return;
        }

        var order = segments.Count;
        var ordinal = order + 1;

        segments.Add(TextSegment.Create(
            SegmentIds.Paragraph(partId, ordinal),
            partId,
            paragraph.ToString(),
            SegmentOrigin.At(SegmentOriginKind.Paragraph, ordinal),
            order));

        paragraph.Clear();
    }
}
