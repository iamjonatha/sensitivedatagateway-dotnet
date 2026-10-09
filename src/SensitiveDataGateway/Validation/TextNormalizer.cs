using System.Globalization;
using System.Text;

namespace SensitiveDataGateway.Validation;

/// <summary>
/// Reduces text to the form two values share when they are the same value written twice.
/// </summary>
/// <remarks>
/// Normalization exists because an anonymizer bug rarely reproduces a value byte for byte.
/// It reproduces it with a different case, a non-breaking space, a dotted tax code or a
/// decomposed accent. Comparing the reduced forms catches the near miss, which is the one
/// that would otherwise travel.
/// </remarks>
internal static class TextNormalizer
{
    /// <summary>
    /// Reduces text to lowercase letters and digits, with diacritics removed.
    /// </summary>
    /// <param name="text">The text to reduce.</param>
    /// <returns>The reduced form, which may be empty.</returns>
    public static string Reduce(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            Append(builder, character);
        }

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, char character)
    {
        if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
        {
            return;
        }

        if (char.IsLetterOrDigit(character))
        {
            builder.Append(char.ToLowerInvariant(character));
        }
    }
}
