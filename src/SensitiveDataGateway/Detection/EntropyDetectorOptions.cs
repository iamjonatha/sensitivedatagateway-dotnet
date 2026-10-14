using System;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// What the entropy detector considers dense enough to be a secret.
/// </summary>
/// <remarks>
/// <para>
/// The detector is the net under the patterns: it finds the key whose vendor nobody wrote a
/// pattern for. It works on shape alone, so it is the one deterministic detector that reports
/// candidates rather than values, and the thresholds decide how much prose it mistakes for a
/// key.
/// </para>
/// <para>
/// Both bounds refuse values that would turn the detector off or make it report everything.
/// A detector that reports everything is the same as one that reports nothing, because an
/// operator stops reading either.
/// </para>
/// </remarks>
public sealed class EntropyDetectorOptions
{
    /// <summary>
    /// The shortest run of characters the detector will consider.
    /// </summary>
    public const int MinimumAllowedLength = 16;

    /// <summary>
    /// The longest run of characters the detector may be told to require.
    /// </summary>
    public const int MaximumAllowedLength = 256;

    /// <summary>
    /// The lowest density the detector may be told to accept, in bits per character.
    /// </summary>
    public const double MinimumAllowedBits = 3.0;

    /// <summary>
    /// The highest density the detector may be told to require, in bits per character.
    /// </summary>
    public const double MaximumAllowedBits = 6.5;

    private EntropyDetectorOptions(int minimumLength, double minimumBitsPerCharacter)
    {
        MinimumLength = minimumLength;
        MinimumBitsPerCharacter = minimumBitsPerCharacter;
    }

    /// <summary>
    /// Gets the options the library uses when none are given.
    /// </summary>
    /// <value>
    /// Runs of twenty characters or more, at four and a half bits each. The pair was chosen
    /// against the synthetic corpus: shorter runs match identifiers, and lower densities match
    /// ordinary words.
    /// </value>
    public static EntropyDetectorOptions Default { get; } = new(minimumLength: 20, minimumBitsPerCharacter: 4.5);

    /// <summary>
    /// Gets the shortest run of characters the detector considers.
    /// </summary>
    public int MinimumLength { get; }

    /// <summary>
    /// Gets the density a run must reach to be reported, in bits per character.
    /// </summary>
    public double MinimumBitsPerCharacter { get; }

    /// <summary>
    /// Returns a copy of these options that considers runs of a different length.
    /// </summary>
    /// <param name="minimumLength">The shortest run to consider.</param>
    /// <returns>The new options.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="minimumLength"/> is outside the allowed range.
    /// </exception>
    public EntropyDetectorOptions WithMinimumLength(int minimumLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumLength, MinimumAllowedLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minimumLength, MaximumAllowedLength);

        return new EntropyDetectorOptions(minimumLength, MinimumBitsPerCharacter);
    }

    /// <summary>
    /// Returns a copy of these options that requires a different density.
    /// </summary>
    /// <param name="minimumBitsPerCharacter">The density a run must reach.</param>
    /// <returns>The new options.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="minimumBitsPerCharacter"/> is outside the allowed range.
    /// </exception>
    public EntropyDetectorOptions WithMinimumBitsPerCharacter(double minimumBitsPerCharacter)
    {
        if (double.IsNaN(minimumBitsPerCharacter))
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumBitsPerCharacter),
                "The density is not a number.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(minimumBitsPerCharacter, MinimumAllowedBits);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minimumBitsPerCharacter, MaximumAllowedBits);

        return new EntropyDetectorOptions(MinimumLength, minimumBitsPerCharacter);
    }
}
