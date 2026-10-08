using System;

namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// Replaces the inside of a value, keeping its length and its ends.
/// </summary>
/// <remarks>
/// <para>
/// A mask is the weakest transformation the gateway applies, and the only one that lets part
/// of the original through. It exists because some decisions need the shape — the last digits
/// of a card, the country of an IBAN — and it is bounded so that the part that gets through
/// is never most of the value.
/// </para>
/// <para>
/// The rule is:
/// </para>
/// <code>
/// length &lt; 8          every character is masked
/// length &gt;= 8         min(4, length / 4) characters survive at each end
/// </code>
/// <para>
/// At least half of a masked value is therefore always masked, and the length is preserved so
/// that the result still reads as a value of that kind. A mask produces no marker: it is not
/// a token, and nothing correlates on it.
/// </para>
/// </remarks>
public static class Masker
{
    /// <summary>
    /// The character that replaces a masked character.
    /// </summary>
    public const char MaskCharacter = '*';

    /// <summary>
    /// The shortest value that keeps any of its characters.
    /// </summary>
    public const int ShortestPartialMask = 8;

    /// <summary>
    /// The greatest number of characters that survive at each end.
    /// </summary>
    public const int MaxVisiblePerEnd = 4;

    /// <summary>
    /// Returns the masked form of a value.
    /// </summary>
    /// <param name="value">The value, which is document content.</param>
    /// <returns>The masked value, of the same length.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static string Apply(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length == 0)
        {
            return value;
        }

        var visible = value.Length < ShortestPartialMask
            ? 0
            : Math.Min(MaxVisiblePerEnd, value.Length / 4);

        return string.Create(value.Length, (value, visible), static (destination, state) =>
        {
            var (source, keep) = state;

            for (var index = 0; index < destination.Length; index++)
            {
                var atStart = index < keep;
                var atEnd = index >= destination.Length - keep;

                destination[index] = atStart || atEnd ? source[index] : MaskCharacter;
            }
        });
    }
}
