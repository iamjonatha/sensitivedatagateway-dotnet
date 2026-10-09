using System;
using System.Collections.Generic;

namespace SensitiveDataGateway.Validation;

/// <summary>
/// Measures how unpredictable a string is, in bits per character.
/// </summary>
/// <remarks>
/// A key, a token and a password look alike from here: short, dense, and drawn from a wide
/// alphabet with no structure. Prose does not. The measure is crude on purpose — it is the
/// last net, and it is cheap.
/// </remarks>
internal static class ShannonEntropy
{
    /// <summary>
    /// Measures the entropy of a string.
    /// </summary>
    /// <param name="value">The string to measure.</param>
    /// <returns>Bits per character, or zero for an empty string.</returns>
    public static double Measure(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return 0;
        }

        var counts = new Dictionary<char, int>();

        foreach (var character in value)
        {
            counts[character] = counts.TryGetValue(character, out var seen) ? seen + 1 : 1;
        }

        var entropy = 0d;

        foreach (var count in counts.Values)
        {
            var probability = (double)count / value.Length;
            entropy -= probability * Math.Log2(probability);
        }

        return entropy;
    }
}
