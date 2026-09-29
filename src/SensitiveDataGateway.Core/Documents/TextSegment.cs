using System;

namespace SensitiveDataGateway.Documents;

/// <summary>
/// A run of text an extractor produced from one part, with the provenance it came with.
/// </summary>
/// <remarks>
/// Segments are the unit everything downstream works on. A detector reports a
/// <see cref="SourceSpan"/> into a segment, the policy decides about that span, and the
/// anonymizer rewrites the segment's text. Keeping segments small and provenanced is what
/// lets the audit record say where a change was made without quoting what was there.
/// </remarks>
public sealed class TextSegment
{
    private TextSegment(SegmentId id, PartId partId, string text, SegmentOrigin origin, int order)
    {
        Id = id;
        PartId = partId;
        Text = text;
        Origin = origin;
        Order = order;
    }

    /// <summary>
    /// Gets the identifier of the segment.
    /// </summary>
    public SegmentId Id { get; }

    /// <summary>
    /// Gets the part the text was extracted from.
    /// </summary>
    public PartId PartId { get; }

    /// <summary>
    /// Gets the extracted text.
    /// </summary>
    /// <remarks>
    /// This is document content. It is never logged, never put in an exception message and
    /// never copied into a detection record.
    /// </remarks>
    public string Text { get; }

    /// <summary>
    /// Gets where in the part the text came from.
    /// </summary>
    public SegmentOrigin Origin { get; }

    /// <summary>
    /// Gets the position of the segment in reading order within its part.
    /// </summary>
    /// <remarks>
    /// Order is what lets the anonymizer reassemble a document that reads the way the
    /// original did, and what lets a reviewer follow a redaction back to its context.
    /// </remarks>
    public int Order { get; }

    /// <summary>
    /// Gets the span that covers the whole segment.
    /// </summary>
    public SourceSpan Span => SourceSpan.Create(Id, 0, Text.Length);

    /// <summary>
    /// Creates a segment.
    /// </summary>
    /// <param name="id">The identifier of the segment.</param>
    /// <param name="partId">The part the text was extracted from.</param>
    /// <param name="text">The extracted text. It may be empty.</param>
    /// <param name="origin">Where in the part the text came from.</param>
    /// <param name="order">The position of the segment in reading order.</param>
    /// <returns>The segment.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An identifier is the default value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> is negative.</exception>
    public static TextSegment Create(
        SegmentId id,
        PartId partId,
        string text,
        SegmentOrigin origin,
        int order)
    {
        if (id.IsDefault)
        {
            throw new ArgumentException("The segment identifier carries no value.", nameof(id));
        }

        if (partId.IsDefault)
        {
            throw new ArgumentException("The part identifier carries no value.", nameof(partId));
        }

        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(order);

        return new TextSegment(id, partId, text, origin, order);
    }

    /// <summary>
    /// Returns a description of the segment.
    /// </summary>
    /// <returns>
    /// The identifier, the origin and the length of the text. It never includes the text.
    /// </returns>
    public override string ToString() => $"{Id} ({Origin}, {Text.Length} characters)";
}
