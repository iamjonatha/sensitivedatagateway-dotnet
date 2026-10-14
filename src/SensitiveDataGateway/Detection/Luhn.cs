using System;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// The check digit scheme payment card numbers use.
/// </summary>
/// <remarks>
/// Luhn is what separates a card number from sixteen digits that merely look like one. It is
/// weak as a checksum — it catches a single wrong digit and most transpositions, and nothing
/// else — but a sequence that fails it is not a card number, and that is the question a
/// detector needs answered.
/// </remarks>
internal static class Luhn
{
    private const int MinimumDigits = 12;
    private const int MaximumDigits = 19;

    /// <summary>
    /// Determines whether a sequence of digits satisfies the Luhn check.
    /// </summary>
    /// <param name="value">The candidate, which may contain spaces and hyphens.</param>
    /// <returns><see langword="true"/> when the check digit is consistent with the rest.</returns>
    public static bool IsValid(ReadOnlySpan<char> value)
    {
        var sum = 0;
        var digits = 0;
        var doubling = false;

        for (var index = value.Length - 1; index >= 0; index--)
        {
            var character = value[index];

            if (character is ' ' or '-')
            {
                continue;
            }

            if (!char.IsAsciiDigit(character))
            {
                return false;
            }

            var digit = character - '0';
            digits++;

            if (doubling)
            {
                digit *= 2;

                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            doubling = !doubling;
        }

        return digits >= MinimumDigits && digits <= MaximumDigits && sum % 10 == 0;
    }
}
