using System;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// The structure and check character of an Italian tax code.
/// </summary>
/// <remarks>
/// <para>
/// The code encodes a surname, a given name, a date of birth, a sex and a municipality, and
/// closes with a check character over the other fifteen. Sixteen letters and digits that
/// satisfy all of it are a tax code; a near miss is not one, and the detector drops it.
/// </para>
/// <para>
/// When two people would receive the same code the authority replaces digits with letters,
/// from the back. Those substitutions are part of the code and are checked as written, so
/// the check character is computed over the characters that are there rather than over a
/// normalized form.
/// </para>
/// </remarks>
internal static class ItalianTaxCode
{
    /// <summary>
    /// The number of characters in a tax code.
    /// </summary>
    public const int Length = 16;

    private const string MonthLetters = "ABCDEHLMPRST";

    private const string OmocodeLetters = "LMNPQRSTUV";

    private static ReadOnlySpan<byte> OddValues =>
    [
        1, 0, 5, 7, 9, 13, 15, 17, 19, 21, 2, 4, 18, 20,
        11, 3, 6, 8, 12, 14, 16, 10, 22, 25, 24, 23,
    ];

    /// <summary>
    /// Determines whether a candidate is a well-formed tax code.
    /// </summary>
    /// <param name="value">The candidate, in upper case and without separators.</param>
    /// <returns><see langword="true"/> when the structure and the check character both hold.</returns>
    public static bool IsValid(ReadOnlySpan<char> value) =>
        value.Length == Length
        && HasValidStructure(value)
        && HasValidDayOfBirth(value)
        && value[15] == CheckCharacter(value);

    private static bool HasValidStructure(ReadOnlySpan<char> value)
    {
        for (var index = 0; index < 6; index++)
        {
            if (!char.IsAsciiLetterUpper(value[index]))
            {
                return false;
            }
        }

        return IsDigitOrOmocode(value[6])
            && IsDigitOrOmocode(value[7])
            && MonthLetters.Contains(value[8], StringComparison.Ordinal)
            && IsDigitOrOmocode(value[9])
            && IsDigitOrOmocode(value[10])
            && char.IsAsciiLetterUpper(value[11])
            && IsDigitOrOmocode(value[12])
            && IsDigitOrOmocode(value[13])
            && IsDigitOrOmocode(value[14])
            && char.IsAsciiLetterUpper(value[15]);
    }

    /// <summary>
    /// Determines whether the two day characters name a day that exists.
    /// </summary>
    /// <param name="value">The candidate.</param>
    /// <returns><see langword="true"/> when the day is in range.</returns>
    /// <remarks>
    /// Forty is added to the day for a woman, so both ranges are legal and anything outside
    /// them is not a tax code. The check costs nothing and removes a class of false positives
    /// that the check character alone lets through once in twenty-six.
    /// </remarks>
    private static bool HasValidDayOfBirth(ReadOnlySpan<char> value)
    {
        var day = (Digit(value[9]) * 10) + Digit(value[10]);

        return day is (>= 1 and <= 31) or (>= 41 and <= 71);
    }

    private static bool IsDigitOrOmocode(char character) =>
        char.IsAsciiDigit(character) || OmocodeLetters.Contains(character, StringComparison.Ordinal);

    private static int Digit(char character)
    {
        if (char.IsAsciiDigit(character))
        {
            return character - '0';
        }

        return OmocodeLetters.IndexOf(character, StringComparison.Ordinal);
    }

    /// <summary>
    /// Computes the check character of the first fifteen characters.
    /// </summary>
    /// <param name="value">The candidate.</param>
    /// <returns>The character the code must end with.</returns>
    private static char CheckCharacter(ReadOnlySpan<char> value)
    {
        var sum = 0;

        for (var index = 0; index < 15; index++)
        {
            var character = value[index];
            var position = char.IsAsciiDigit(character) ? character - '0' : character - 'A';

            // Positions are counted from one, so the even table applies at odd indices. On an
            // even position the value is the position itself; on an odd one it is substituted.
            sum += index % 2 == 0 ? OddValues[position] : position;
        }

        return (char)('A' + (sum % 26));
    }
}
