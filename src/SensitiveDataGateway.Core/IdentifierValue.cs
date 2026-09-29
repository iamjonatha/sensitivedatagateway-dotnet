using System;

namespace SensitiveDataGateway;

/// <summary>
/// The validation rule shared by every identifier value object in the domain.
/// </summary>
/// <remarks>
/// Identifiers are the only strings the gateway lets out of the trust boundary unchanged, so
/// the rule that decides what an identifier may contain is written once and applied to all of
/// them. It is deliberately strict: bounded, free of control characters, and trimmed, so that
/// an identifier cannot be used to smuggle content into a log line or to forge the structure
/// of one.
/// </remarks>
internal static class IdentifierValue
{
    /// <summary>
    /// The greatest number of characters any identifier may contain once trimmed.
    /// </summary>
    internal const int MaxLength = 128;

    /// <summary>
    /// Validates and normalizes a candidate identifier.
    /// </summary>
    /// <param name="value">The candidate, or <see langword="null"/>.</param>
    /// <param name="normalized">
    /// The trimmed value when the candidate is valid; otherwise an empty string.
    /// </param>
    /// <returns><see langword="true"/> when the candidate is a valid identifier.</returns>
    internal static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;

        if (value is null)
        {
            return false;
        }

        var trimmed = value.Trim();

        if (trimmed.Length is 0 or > MaxLength)
        {
            return false;
        }

        foreach (var character in trimmed)
        {
            if (char.IsControl(character))
            {
                return false;
            }
        }

        normalized = trimmed;
        return true;
    }
}
