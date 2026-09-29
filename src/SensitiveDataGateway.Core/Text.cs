using System;
using System.Globalization;

namespace SensitiveDataGateway;

/// <summary>
/// Validation shared by the short, structural strings the domain accepts.
/// </summary>
/// <remarks>
/// These are strings that describe the document rather than reproduce it: error codes, media
/// types, structural labels and version markers. They are bounded and free of control
/// characters so that they cannot be used to forge the shape of a log line or to carry a
/// payload through a field nobody inspects.
/// </remarks>
internal static class Text
{
    /// <summary>
    /// Validates and trims a required structural string.
    /// </summary>
    /// <param name="value">The candidate.</param>
    /// <param name="maxLength">The greatest number of characters allowed once trimmed.</param>
    /// <param name="parameterName">The name of the argument being validated.</param>
    /// <returns>The trimmed value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The value is empty, too long, or has a control character.</exception>
    internal static string Require(string? value, int maxLength, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        if (!TryNormalize(value, maxLength, out var normalized))
        {
            throw new ArgumentException(
                $"The value must be non-empty, free of control characters and at most {maxLength.ToString(CultureInfo.InvariantCulture)} characters once trimmed.",
                parameterName);
        }

        return normalized;
    }

    /// <summary>
    /// Validates and trims an optional structural string.
    /// </summary>
    /// <param name="value">The candidate, or <see langword="null"/>.</param>
    /// <param name="maxLength">The greatest number of characters allowed once trimmed.</param>
    /// <param name="parameterName">The name of the argument being validated.</param>
    /// <returns>The trimmed value, or <see langword="null"/> when none was supplied.</returns>
    /// <exception cref="ArgumentException">A value was supplied and is empty, too long, or has a control character.</exception>
    internal static string? Allow(string? value, int maxLength, string parameterName) =>
        value is null ? null : Require(value, maxLength, parameterName);

    private static bool TryNormalize(string value, int maxLength, out string normalized)
    {
        normalized = string.Empty;

        var trimmed = value.Trim();

        if (trimmed.Length == 0 || trimmed.Length > maxLength)
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
