using System;
using System.Globalization;

namespace SensitiveDataGateway;

/// <summary>
/// A range of characters inside one extracted text segment.
/// </summary>
/// <remarks>
/// <para>
/// A span is how every stage after extraction names a piece of text without carrying it: a
/// detector reports where it found a candidate, the policy decides about that location, and
/// the anonymizer rewrites it. Nothing in the chain has to repeat the value, which is what
/// keeps a detection record safe to log.
/// </para>
/// <para>
/// A span is anchored to a segment. Two spans in different segments never contain or overlap
/// each other, however their offsets compare.
/// </para>
/// </remarks>
public readonly struct SourceSpan : IEquatable<SourceSpan>
{
    private SourceSpan(SegmentId segmentId, int start, int length)
    {
        SegmentId = segmentId;
        Start = start;
        Length = length;
    }

    /// <summary>
    /// Gets the segment this span points into.
    /// </summary>
    public SegmentId SegmentId { get; }

    /// <summary>
    /// Gets the zero-based index of the first character of the span.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// Gets the number of characters in the span.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the index one past the last character of the span.
    /// </summary>
    public int End => Start + Length;

    /// <summary>
    /// Gets a value indicating whether the span covers no characters.
    /// </summary>
    /// <remarks>
    /// An empty span is legal: it marks a position without selecting text, which is what a
    /// detector reports when it recognizes a structure rather than a value.
    /// </remarks>
    public bool IsEmpty => Length == 0;

    /// <summary>
    /// Creates a span.
    /// </summary>
    /// <param name="segmentId">The segment the span points into.</param>
    /// <param name="start">The zero-based index of the first character.</param>
    /// <param name="length">The number of characters.</param>
    /// <returns>The span.</returns>
    /// <exception cref="ArgumentException"><paramref name="segmentId"/> is the default value.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="start"/> or <paramref name="length"/> is negative, or the end of the
    /// span would overflow.
    /// </exception>
    public static SourceSpan Create(SegmentId segmentId, int start, int length)
    {
        if (segmentId.IsDefault)
        {
            throw new ArgumentException("A span must be anchored to a segment.", nameof(segmentId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (length > int.MaxValue - start)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                "The end of the span would exceed the largest representable offset.");
        }

        return new SourceSpan(segmentId, start, length);
    }

    /// <summary>
    /// Determines whether this span lies entirely within a text of the given length.
    /// </summary>
    /// <param name="textLength">The length of the text the span points into.</param>
    /// <returns><see langword="true"/> when the span is in range.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="textLength"/> is negative.</exception>
    /// <remarks>
    /// A span that fails this test is a defect somewhere upstream, and the stage that finds
    /// it blocks rather than clamping. A clamped span rewrites the wrong characters, which is
    /// the failure mode the whole pipeline exists to prevent.
    /// </remarks>
    public bool IsWithin(int textLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(textLength);

        return End <= textLength;
    }

    /// <summary>
    /// Determines whether a position falls inside this span.
    /// </summary>
    /// <param name="position">The zero-based character index.</param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="position"/> is at or after
    /// <see cref="Start"/> and before <see cref="End"/>. An empty span contains nothing.
    /// </returns>
    public bool Contains(int position) => position >= Start && position < End;

    /// <summary>
    /// Determines whether this span completely covers another.
    /// </summary>
    /// <param name="other">The span to test.</param>
    /// <returns>
    /// <see langword="true"/> when both spans are in the same segment and
    /// <paramref name="other"/> lies within this one.
    /// </returns>
    public bool Contains(SourceSpan other) =>
        SegmentId == other.SegmentId && other.Start >= Start && other.End <= End;

    /// <summary>
    /// Determines whether this span shares at least one character with another.
    /// </summary>
    /// <param name="other">The span to test.</param>
    /// <returns>
    /// <see langword="true"/> when both spans are in the same segment and their ranges
    /// intersect. An empty span overlaps nothing.
    /// </returns>
    /// <remarks>
    /// This is the test the detection stage uses to reconcile two detectors that claim the
    /// same text, and the test the anonymizer uses to refuse to rewrite the same characters
    /// twice.
    /// </remarks>
    public bool Overlaps(SourceSpan other) =>
        !IsEmpty && !other.IsEmpty && SegmentId == other.SegmentId && Start < other.End && other.Start < End;

    /// <summary>
    /// Returns the range shared by this span and another.
    /// </summary>
    /// <param name="other">The span to intersect with.</param>
    /// <param name="intersection">
    /// When this method returns <see langword="true"/>, the shared range; otherwise the
    /// default value of the struct.
    /// </param>
    /// <returns><see langword="true"/> when the spans overlap.</returns>
    public bool TryIntersect(SourceSpan other, out SourceSpan intersection)
    {
        if (!Overlaps(other))
        {
            intersection = default;
            return false;
        }

        var start = Math.Max(Start, other.Start);
        var end = Math.Min(End, other.End);

        intersection = new SourceSpan(SegmentId, start, end - start);
        return true;
    }

    /// <inheritdoc />
    public bool Equals(SourceSpan other) =>
        SegmentId == other.SegmentId && Start == other.Start && Length == other.Length;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SourceSpan other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(SegmentId, Start, Length);

    /// <summary>
    /// Returns a diagnostic description of the span.
    /// </summary>
    /// <returns>
    /// The segment identifier and the range, in a form safe to log: it names a location and
    /// never the text at it.
    /// </returns>
    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{SegmentId}[{Start}..{End})");

    /// <summary>
    /// Determines whether two spans are the same.
    /// </summary>
    /// <param name="left">The first span.</param>
    /// <param name="right">The second span.</param>
    /// <returns><see langword="true"/> when they denote the same range of the same segment.</returns>
    public static bool operator ==(SourceSpan left, SourceSpan right) => left.Equals(right);

    /// <summary>
    /// Determines whether two spans differ.
    /// </summary>
    /// <param name="left">The first span.</param>
    /// <param name="right">The second span.</param>
    /// <returns><see langword="true"/> when they denote different ranges or different segments.</returns>
    public static bool operator !=(SourceSpan left, SourceSpan right) => !left.Equals(right);
}
