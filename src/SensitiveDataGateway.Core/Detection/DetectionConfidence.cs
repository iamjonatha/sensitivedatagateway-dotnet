using System;
using System.Globalization;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// How sure a detector is about what it found, from 0.0 to 1.0.
/// </summary>
/// <remarks>
/// <para>
/// Confidence is a claim, not a decision. A detector states how sure it is and stops; the
/// policy decides what that is worth, and the same score can lead to a redaction for one
/// entity type and to a block for another.
/// </para>
/// <para>
/// The default value is <see cref="None"/>, which is the safe reading of an absent score:
/// something that has not been argued for has not been established.
/// </para>
/// </remarks>
public readonly struct DetectionConfidence : IEquatable<DetectionConfidence>, IComparable<DetectionConfidence>
{
    private DetectionConfidence(double value) => Value = value;

    /// <summary>
    /// Gets a confidence of 0.0.
    /// </summary>
    public static DetectionConfidence None => default;

    /// <summary>
    /// Gets a confidence of 1.0, used by detectors whose match is arithmetic rather than
    /// probabilistic, such as a checksum that validated.
    /// </summary>
    public static DetectionConfidence Certain { get; } = new(1.0);

    /// <summary>
    /// Gets the score, between 0.0 and 1.0 inclusive.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Creates a confidence.
    /// </summary>
    /// <param name="value">The score, between 0.0 and 1.0 inclusive.</param>
    /// <returns>The confidence.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is outside the range, or is not a number.
    /// </exception>
    /// <remarks>
    /// An out-of-range score is rejected rather than clamped. Clamping turns a detector's
    /// arithmetic mistake into a plausible-looking number that the policy will then trust.
    /// </remarks>
    public static DetectionConfidence Create(double value)
    {
        if (double.IsNaN(value) || value < 0.0 || value > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "A confidence is a number between 0.0 and 1.0 inclusive.");
        }

        return new DetectionConfidence(value);
    }

    /// <inheritdoc />
    public int CompareTo(DetectionConfidence other) => Value.CompareTo(other.Value);

    /// <inheritdoc />
    public bool Equals(DetectionConfidence other) => Value.Equals(other.Value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DetectionConfidence other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <summary>
    /// Returns the score.
    /// </summary>
    /// <returns>The score, formatted invariantly. It carries no document content.</returns>
    public override string ToString() => Value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    /// Determines whether two confidences are the same.
    /// </summary>
    /// <param name="left">The first confidence.</param>
    /// <param name="right">The second confidence.</param>
    /// <returns><see langword="true"/> when the scores are equal.</returns>
    public static bool operator ==(DetectionConfidence left, DetectionConfidence right) => left.Equals(right);

    /// <summary>
    /// Determines whether two confidences differ.
    /// </summary>
    /// <param name="left">The first confidence.</param>
    /// <param name="right">The second confidence.</param>
    /// <returns><see langword="true"/> when the scores differ.</returns>
    public static bool operator !=(DetectionConfidence left, DetectionConfidence right) => !left.Equals(right);

    /// <summary>
    /// Determines whether one confidence is lower than another.
    /// </summary>
    /// <param name="left">The first confidence.</param>
    /// <param name="right">The second confidence.</param>
    /// <returns><see langword="true"/> when the first score is lower.</returns>
    public static bool operator <(DetectionConfidence left, DetectionConfidence right) => left.Value < right.Value;

    /// <summary>
    /// Determines whether one confidence is higher than another.
    /// </summary>
    /// <param name="left">The first confidence.</param>
    /// <param name="right">The second confidence.</param>
    /// <returns><see langword="true"/> when the first score is higher.</returns>
    public static bool operator >(DetectionConfidence left, DetectionConfidence right) => left.Value > right.Value;

    /// <summary>
    /// Determines whether one confidence is not higher than another.
    /// </summary>
    /// <param name="left">The first confidence.</param>
    /// <param name="right">The second confidence.</param>
    /// <returns><see langword="true"/> when the first score is lower or equal.</returns>
    public static bool operator <=(DetectionConfidence left, DetectionConfidence right) => left.Value <= right.Value;

    /// <summary>
    /// Determines whether one confidence is not lower than another.
    /// </summary>
    /// <param name="left">The first confidence.</param>
    /// <param name="right">The second confidence.</param>
    /// <returns><see langword="true"/> when the first score is higher or equal.</returns>
    public static bool operator >=(DetectionConfidence left, DetectionConfidence right) => left.Value >= right.Value;
}
