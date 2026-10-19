using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SensitiveDataGateway.Documents.Mime;

/// <summary>
/// Reads the parts of a message that are themselves MIME structures.
/// </summary>
/// <remarks>
/// <para>
/// Two kinds of part reach this extractor. A forwarded message — <c>message/rfc822</c> and
/// its relatives — yields nothing, because <see cref="MimeBundleReader"/> has already
/// flattened its headers and its bodies into parts of their own; the part exists so that
/// those parts have a parent to point at. A delivery or disposition report yields one
/// segment per field, which is how the recipient addresses inside a bounce come to be
/// scanned instead of travelling unexamined.
/// </para>
/// <para>
/// Nothing else is claimed. <c>message/external-body</c> and <c>message/partial</c> are
/// pointers at content this library is not allowed to fetch or complete, so they are left
/// for no extractor to take and the operation blocks.
/// </para>
/// </remarks>
public sealed class MimeStructureExtractor : IDocumentExtractor
{
    private static readonly UTF8Encoding s_strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Initializes a new instance of the <see cref="MimeStructureExtractor"/> class.
    /// </summary>
    public MimeStructureExtractor() =>
        Version = ExtractorVersion.Create(ExtractorId.Create("mime.structure"), "1.0.0");

    /// <inheritdoc/>
    public ExtractorVersion Version { get; }

    /// <inheritdoc/>
    public bool CanExtract(string mediaType)
    {
        ArgumentNullException.ThrowIfNull(mediaType);

        return MimeMediaTypes.IsStructure(MimeMediaTypes.Essence(mediaType));
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<TextSegment>> ExtractAsync(
        DocumentPart part,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(part);

        if (MimeMediaTypes.IsEnvelope(MimeMediaTypes.Essence(part.MediaType)))
        {
            return [];
        }

        var text = await ReadAsync(part, cancellationToken).ConfigureAwait(false);

        return SplitIntoFields(part.Id, text);
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

        var stream = await part.Content.OpenStreamAsync(cancellationToken).ConfigureAwait(false);

        await using (stream.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

            return Decode(part.Id, buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        }
    }

    /// <summary>
    /// Decodes the report as strict UTF-8.
    /// </summary>
    /// <param name="partId">The part.</param>
    /// <param name="bytes">The decoded content of the part.</param>
    /// <returns>The text.</returns>
    /// <remarks>
    /// A report is US-ASCII by specification, which strict UTF-8 reads unchanged. A byte
    /// that is neither raises rather than being replaced: a replacement character in the
    /// middle of an address is a match the scan will not make.
    /// </remarks>
    private static string Decode(PartId partId, ReadOnlySpan<byte> bytes)
    {
        try
        {
            return s_strictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new ExtractionException(MimeErrors.Undecodable(partId), exception);
        }
    }

    /// <summary>
    /// Splits a report into one segment per field.
    /// </summary>
    /// <param name="partId">The part the fields came from.</param>
    /// <param name="text">The text of the report.</param>
    /// <returns>The segments.</returns>
    /// <remarks>
    /// A report is a sequence of short <c>name: value</c> lines, so a line is the unit here
    /// where a paragraph is the unit in flowing text. Folded continuation lines are joined
    /// onto the field they belong to, because a value cut in half matches nothing.
    /// </remarks>
    private static List<TextSegment> SplitIntoFields(PartId partId, string text)
    {
        var segments = new List<TextSegment>();
        var field = new StringBuilder();

        foreach (var line in text.Split('\n'))
        {
            var content = line.TrimEnd('\r');

            if (string.IsNullOrWhiteSpace(content))
            {
                Flush(segments, partId, field);
                continue;
            }

            if (IsContinuation(content))
            {
                field.Append(' ').Append(content.TrimStart());
                continue;
            }

            Flush(segments, partId, field);
            field.Append(content);
        }

        Flush(segments, partId, field);

        return segments;
    }

    private static bool IsContinuation(string line) =>
        line.Length > 0 && (line[0] == ' ' || line[0] == '\t');

    private static void Flush(List<TextSegment> segments, PartId partId, StringBuilder field)
    {
        if (field.Length == 0)
        {
            return;
        }

        var order = segments.Count;
        var ordinal = order + 1;

        segments.Add(TextSegment.Create(
            SegmentIds.Header(partId, ordinal),
            partId,
            field.ToString(),
            SegmentOrigin.At(SegmentOriginKind.Header, ordinal),
            order));

        field.Clear();
    }
}
