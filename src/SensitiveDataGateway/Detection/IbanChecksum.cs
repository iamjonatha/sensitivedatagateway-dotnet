using System;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// The mod-97 check an international bank account number carries.
/// </summary>
/// <remarks>
/// Unlike Luhn, this one is strong: a wrong character fails it with probability 96 in 97. A
/// string that passes is an account number or a deliberate forgery, which is why a detector
/// may report it as certain rather than as likely.
/// </remarks>
internal static class IbanChecksum
{
    private const int MinimumLength = 15;
    private const int MaximumLength = 34;

    /// <summary>
    /// Determines whether a candidate satisfies the mod-97 check.
    /// </summary>
    /// <param name="value">The candidate, which may contain spaces.</param>
    /// <returns><see langword="true"/> when the remainder is one.</returns>
    public static bool IsValid(ReadOnlySpan<char> value)
    {
        Span<char> compact = stackalloc char[MaximumLength];

        return TryCompact(value, compact, out var length) && Remainder(compact[..length]) == 1;
    }

    private static bool TryCompact(ReadOnlySpan<char> value, Span<char> compact, out int length)
    {
        length = 0;

        foreach (var character in value)
        {
            if (character == ' ')
            {
                continue;
            }

            if (!char.IsAsciiLetterOrDigit(character) || length == MaximumLength)
            {
                return false;
            }

            compact[length++] = char.ToUpperInvariant(character);
        }

        return length >= MinimumLength
            && char.IsAsciiLetterUpper(compact[0])
            && char.IsAsciiLetterUpper(compact[1])
            && char.IsAsciiDigit(compact[2])
            && char.IsAsciiDigit(compact[3]);
    }

    /// <summary>
    /// Computes the remainder of the rearranged account number modulo 97.
    /// </summary>
    /// <param name="compact">The account number, without spaces and in upper case.</param>
    /// <returns>The remainder.</returns>
    /// <remarks>
    /// The first four characters move to the end, each letter becomes its position in the
    /// alphabet plus nine, and the resulting number is reduced as it is consumed because it
    /// is far too long to hold in any integer type.
    /// </remarks>
    private static int Remainder(ReadOnlySpan<char> compact)
    {
        var remainder = 0;

        for (var index = 0; index < compact.Length; index++)
        {
            var character = compact[(index + 4) % compact.Length];

            remainder = char.IsAsciiDigit(character)
                ? ((remainder * 10) + (character - '0')) % 97
                : ((remainder * 100) + (character - 'A' + 10)) % 97;
        }

        return remainder;
    }
}
