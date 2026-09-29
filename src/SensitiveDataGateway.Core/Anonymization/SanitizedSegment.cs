using System;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// A segment after the plan has been applied to it.
/// </summary>
/// <remarks>
/// <para>
/// A sanitized segment is a different type from a <see cref="TextSegment"/>, not a copy of
/// one with different text. The distinction is load-bearing: it makes "the payload contains
/// only sanitized text" something the compiler checks, rather than something a reviewer has
/// to notice.
/// </para>
/// <para>
/// The segment keeps its identity and its provenance so that an audit record can point at it,
/// and carries no reference to the text it replaced.
/// </para>
/// </remarks>
public sealed class SanitizedSegment
{
    private SanitizedSegment(SegmentId id, PartId partId, string text, SegmentOrigin origin, int order)
    {
        Id = id;
        PartId = partId;
        Text = text;
        Origin = origin;
        Order = order;
    }

    /// <summary>
    /// Gets the identifier of the segment this was produced from.
    /// </summary>
    public SegmentId Id { get; }

    /// <summary>
    /// Gets the part the text was extracted from.
    /// </summary>
    public PartId PartId { get; }

    /// <summary>
    /// Gets the sanitized text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets where in the part the text came from.
    /// </summary>
    public SegmentOrigin Origin { get; }

    /// <summary>
    /// Gets the position of the segment in reading order within its part.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Creates a sanitized segment.
    /// </summary>
    /// <param name="id">The identifier of the segment this was produced from.</param>
    /// <param name="partId">The part the text was extracted from.</param>
    /// <param name="text">The sanitized text.</param>
    /// <param name="origin">Where in the part the text came from.</param>
    /// <param name="order">The position of the segment in reading order.</param>
    /// <returns>The sanitized segment.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An identifier is the default value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> is negative.</exception>
    public static SanitizedSegment Create(
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

        return new SanitizedSegment(id, partId, text, origin, order);
    }

    /// <summary>
    /// Returns a description of the segment.
    /// </summary>
    /// <returns>
    /// The identifier, the origin and the length of the text. Sanitized text is still not
    /// logged: a mask keeps the shape of what it replaced, and a pseudonym is a stable
    /// identifier for a person.
    /// </returns>
    public override string ToString() => $"{Id} ({Origin}, {Text.Length} characters)";
}
