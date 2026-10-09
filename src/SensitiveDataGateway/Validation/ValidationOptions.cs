using System;
using System.Globalization;

namespace SensitiveDataGateway.Validation;

/// <summary>
/// The thresholds the final privacy validation uses.
/// </summary>
/// <remarks>
/// <para>
/// Every value here makes the scan stricter as it goes down, and every default is chosen to
/// be the stricter of two defensible numbers. None of them can be set to a value that turns a
/// check off: there is no threshold that means "do not look".
/// </para>
/// <para>
/// The type is immutable. Each method returns a new instance, so an options value cannot be
/// changed after a validator was built from it.
/// </para>
/// </remarks>
public sealed class ValidationOptions
{
    /// <summary>
    /// The shortest fragment length the scan will accept.
    /// </summary>
    /// <remarks>
    /// Below three characters a fragment matches ordinary prose, and a check that always
    /// fails is a check nobody keeps.
    /// </remarks>
    public const int MinimumAllowedFragmentLength = 3;

    /// <summary>
    /// The shortest string the entropy check will measure.
    /// </summary>
    public const int MinimumAllowedEntropyLength = 8;

    private ValidationOptions(int fragmentLength, int entropyLength, double entropyThreshold)
    {
        MinimumFragmentLength = fragmentLength;
        MinimumEntropyLength = entropyLength;
        EntropyThreshold = entropyThreshold;
    }

    /// <summary>
    /// Gets the options the validator uses when the caller states none.
    /// </summary>
    public static ValidationOptions Default { get; } = new(fragmentLength: 5, entropyLength: 20, entropyThreshold: 4.0);

    /// <summary>
    /// Gets the shortest part of a detected value that may not reappear in the output.
    /// </summary>
    /// <remarks>
    /// This is what stops a multi-word value from being smuggled through one word at a time.
    /// </remarks>
    public int MinimumFragmentLength { get; }

    /// <summary>
    /// Gets the shortest string the entropy check measures.
    /// </summary>
    public int MinimumEntropyLength { get; }

    /// <summary>
    /// Gets the entropy, in bits per character, at or above which a string is treated as key
    /// material rather than as text.
    /// </summary>
    public double EntropyThreshold { get; }

    /// <summary>
    /// Returns options with a different fragment length.
    /// </summary>
    /// <param name="length">The shortest fragment that may not reappear in the output.</param>
    /// <returns>The new options.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="length"/> is below <see cref="MinimumAllowedFragmentLength"/>.
    /// </exception>
    public ValidationOptions WithMinimumFragmentLength(int length)
    {
        if (length < MinimumAllowedFragmentLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                length,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "A fragment shorter than {0} characters matches ordinary text.",
                    MinimumAllowedFragmentLength));
        }

        return new ValidationOptions(length, MinimumEntropyLength, EntropyThreshold);
    }

    /// <summary>
    /// Returns options with a different entropy candidate length.
    /// </summary>
    /// <param name="length">The shortest string the entropy check measures.</param>
    /// <returns>The new options.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="length"/> is below <see cref="MinimumAllowedEntropyLength"/>.
    /// </exception>
    public ValidationOptions WithMinimumEntropyLength(int length)
    {
        if (length < MinimumAllowedEntropyLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                length,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Entropy is not meaningful below {0} characters.",
                    MinimumAllowedEntropyLength));
        }

        return new ValidationOptions(MinimumFragmentLength, length, EntropyThreshold);
    }

    /// <summary>
    /// Returns options with a different entropy threshold.
    /// </summary>
    /// <param name="threshold">Bits per character at or above which a string is key material.</param>
    /// <returns>The new options.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="threshold"/> is not above zero, or is above six bits per character,
    /// which no printable alphabet reaches.
    /// </exception>
    public ValidationOptions WithEntropyThreshold(double threshold)
    {
        if (threshold is <= 0 or > 6)
        {
            throw new ArgumentOutOfRangeException(
                nameof(threshold),
                threshold,
                "The threshold must be above zero and at most six bits per character.");
        }

        return new ValidationOptions(MinimumFragmentLength, MinimumEntropyLength, threshold);
    }

    /// <summary>
    /// Returns a description of the options.
    /// </summary>
    /// <returns>The three thresholds.</returns>
    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "fragment {0}, entropy {1} over {2} characters",
        MinimumFragmentLength,
        EntropyThreshold,
        MinimumEntropyLength);
}
