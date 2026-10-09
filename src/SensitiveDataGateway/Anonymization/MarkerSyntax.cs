using System;
using System.Globalization;
using System.Text;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// The syntax of every token the gateway writes into a document, and the escape that keeps a
/// document from forging one.
/// </summary>
/// <remarks>
/// <para>
/// A token has to be unmistakable. If document content can look like gateway output, then a
/// document can claim that a value was already sanitized, and the validator — which is
/// looking for exactly these shapes — would agree. The syntax is therefore closed, and any
/// occurrence of it in the source is escaped before the source is kept.
/// </para>
/// <para>
/// The grammar is:
/// </para>
/// <code>
/// marker        := '[' body ']'
/// body          := one or more of A-Z, 0-9 and '_'
/// pseudonym     := '[' type '_' number ']'        [PERSON_001]
/// hash          := '[' type '_H' hex ']'          [IBAN_H3F9A2C71]
/// redaction     := '[REDACTED_' type ']'          [REDACTED_EMAIL]
/// escaped       := '[[' body ']'                  the source said [PERSON_001]
/// </code>
/// <para>
/// A mask produces no marker: it keeps the shape of what it replaced, so it is recognizable
/// as a mask and not as a token.
/// </para>
/// </remarks>
public static class MarkerSyntax
{
    /// <summary>
    /// The character a marker opens with.
    /// </summary>
    public const char Open = '[';

    /// <summary>
    /// The character a marker closes with.
    /// </summary>
    public const char Close = ']';

    /// <summary>
    /// The type name a redaction marker carries in place of an entity type.
    /// </summary>
    public const string RedactionType = "REDACTED";

    /// <summary>
    /// The character that introduces the digest in a hash marker.
    /// </summary>
    public const char HashPrefix = 'H';

    /// <summary>
    /// The smallest number of digits in the sequence number of a pseudonym marker.
    /// </summary>
    public const int SequenceDigits = 3;

    /// <summary>
    /// Builds the marker that replaces a pseudonymized value.
    /// </summary>
    /// <param name="type">The kind of thing that was replaced.</param>
    /// <param name="sequence">The one-based position of the value within its scope and type.</param>
    /// <returns>The marker, for example <c>[PERSON_001]</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="type"/> is the default value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sequence"/> is not positive.</exception>
    public static string Pseudonym(EntityType type, int sequence)
    {
        RequireType(type);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);

        var number = sequence.ToString(CultureInfo.InvariantCulture).PadLeft(SequenceDigits, '0');

        return $"{Open}{type}_{number}{Close}";
    }

    /// <summary>
    /// Builds the marker that replaces a hashed value.
    /// </summary>
    /// <param name="type">The kind of thing that was replaced.</param>
    /// <param name="digest">The upper-case hexadecimal digest.</param>
    /// <returns>The marker, for example <c>[IBAN_H3F9A2C71]</c>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="type"/> is the default value, or <paramref name="digest"/> is not a
    /// non-empty run of upper-case hexadecimal characters.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="digest"/> is <see langword="null"/>.</exception>
    public static string Hash(EntityType type, string digest)
    {
        RequireType(type);
        ArgumentNullException.ThrowIfNull(digest);

        if (digest.Length == 0 || !IsHexadecimal(digest))
        {
            throw new ArgumentException(
                "A hash marker carries a non-empty upper-case hexadecimal digest.",
                nameof(digest));
        }

        return $"{Open}{type}_{HashPrefix}{digest}{Close}";
    }

    /// <summary>
    /// Builds the marker that replaces a redacted value.
    /// </summary>
    /// <param name="type">The kind of thing that was removed.</param>
    /// <returns>The marker, for example <c>[REDACTED_EMAIL]</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="type"/> is the default value.</exception>
    /// <remarks>
    /// A redaction marker names the type and nothing else. It is deliberately not unique: two
    /// redacted e-mail addresses produce the same marker, because a redaction that could be
    /// told apart from another redaction is a correlatable token under a different name.
    /// </remarks>
    public static string Redaction(EntityType type)
    {
        RequireType(type);

        return $"{Open}{RedactionType}_{type}{Close}";
    }

    /// <summary>
    /// Determines whether a text contains something shaped like a marker.
    /// </summary>
    /// <param name="text">The text to examine.</param>
    /// <returns><see langword="true"/> when the text contains marker syntax.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The test is on the shape alone, so it also reports a bracketed word that was never a
    /// token. That is the safe direction to be wrong in: escaping text that did not need it
    /// changes a document cosmetically, while missing text that did lets a document forge a
    /// token.
    /// </remarks>
    public static bool ContainsMarker(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return IndexOfMarker(text, 0) >= 0;
    }

    /// <summary>
    /// Escapes every marker-shaped run in a text by doubling the opening bracket.
    /// </summary>
    /// <param name="text">The text to escape.</param>
    /// <returns>The escaped text, or the same instance when there was nothing to escape.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var found = IndexOfMarker(text, 0);

        if (found < 0)
        {
            return text;
        }

        var escaped = new StringBuilder(text.Length + 8);
        var copied = 0;

        while (found >= 0)
        {
            escaped.Append(text, copied, found - copied).Append(Open);
            copied = found;
            found = IndexOfMarker(text, found + 1);
        }

        escaped.Append(text, copied, text.Length - copied);

        return escaped.ToString();
    }

    /// <summary>
    /// Removes the escape from every escaped marker-shaped run in a text.
    /// </summary>
    /// <param name="text">The text to unescape.</param>
    /// <returns>The unescaped text, or the same instance when there was nothing to unescape.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// This undoes <see cref="Escape(string)"/>, and belongs to re-identification: it is how
    /// text that was kept gets its original brackets back once the tokens around it have been
    /// resolved.
    /// </remarks>
    public static string Unescape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var found = IndexOfEscape(text, 0);

        if (found < 0)
        {
            return text;
        }

        var unescaped = new StringBuilder(text.Length);
        var copied = 0;

        while (found >= 0)
        {
            unescaped.Append(text, copied, found - copied);
            copied = found + 1;
            found = IndexOfEscape(text, copied + 1);
        }

        unescaped.Append(text, copied, text.Length - copied);

        return unescaped.ToString();
    }

    /// <summary>
    /// Returns the index just past the closing bracket of the marker that starts at a
    /// position, or <c>-1</c> when no marker starts there.
    /// </summary>
    /// <param name="text">The text to examine.</param>
    /// <param name="open">The position of the opening bracket.</param>
    /// <returns>The index just past the marker, or <c>-1</c>.</returns>
    internal static int MarkerEnd(string text, int open)
    {
        if (open >= text.Length || text[open] != Open)
        {
            return -1;
        }

        var index = open + 1;

        while (index < text.Length && IsBodyCharacter(text[index]))
        {
            index++;
        }

        var closed = index > open + 1 && index < text.Length && text[index] == Close;

        return closed ? index + 1 : -1;
    }

    private static int IndexOfMarker(string text, int start)
    {
        for (var index = start; index < text.Length; index++)
        {
            if (MarkerEnd(text, index) > 0)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Counts the markers in a text that are not escaped.
    /// </summary>
    /// <param name="text">The text to examine.</param>
    /// <returns>The number of markers the gateway would have had to write.</returns>
    /// <remarks>
    /// Validation compares this count with the number of transformations that produce a
    /// marker. A text with more markers than the gateway emitted carries one the document
    /// supplied, which is the forgery the escape exists to prevent.
    /// </remarks>
    internal static int CountMarkers(string text)
    {
        var count = 0;

        for (var index = 0; index < text.Length; index++)
        {
            var end = SkipEscape(text, index);

            if (end > 0)
            {
                index = end - 1;
                continue;
            }

            end = MarkerEnd(text, index);

            if (end > 0)
            {
                count++;
                index = end - 1;
            }
        }

        return count;
    }

    /// <summary>
    /// Removes every marker from a text, leaving escaped markers in place.
    /// </summary>
    /// <param name="text">The text to examine.</param>
    /// <returns>The text without the tokens the gateway wrote.</returns>
    /// <remarks>
    /// The anti-leak scan runs over this form. A token is high-entropy by construction and
    /// bracketed by construction, so scanning it would report the gateway's own output as a
    /// secret and train the operator to disregard the check that matters.
    /// </remarks>
    internal static string WithoutMarkers(string text)
    {
        var stripped = new StringBuilder(text.Length);

        for (var index = 0; index < text.Length; index++)
        {
            var end = SkipEscape(text, index);

            if (end < 0)
            {
                end = MarkerEnd(text, index);
            }
            else
            {
                stripped.Append(text, index, end - index);
            }

            if (end > 0)
            {
                index = end - 1;
            }
            else
            {
                stripped.Append(text[index]);
            }
        }

        return stripped.ToString();
    }

    /// <summary>
    /// Returns the markers in a text, concatenated, with everything else dropped.
    /// </summary>
    /// <param name="text">The text to examine.</param>
    /// <returns>The tokens the gateway wrote, separated by a space.</returns>
    /// <remarks>
    /// Validation reads this to check that a token does not spell out the value it stands
    /// for. The marker body accepts letters, digits and underscores, so a defective
    /// pseudonym generator could write <c>[PERSON_DANA_OAKFIELD]</c> and produce a token that
    /// is well formed and useless.
    /// </remarks>
    internal static string MarkersOnly(string text)
    {
        var markers = new StringBuilder();

        for (var index = 0; index < text.Length; index++)
        {
            var escape = SkipEscape(text, index);

            if (escape > 0)
            {
                index = escape - 1;
                continue;
            }

            var end = MarkerEnd(text, index);

            if (end > 0)
            {
                markers.Append(text, index, end - index).Append(' ');
                index = end - 1;
            }
        }

        return markers.ToString();
    }

    private static int SkipEscape(string text, int index) =>
        index < text.Length - 1 && text[index] == Open && MarkerEnd(text, index + 1) > 0
            ? MarkerEnd(text, index + 1)
            : -1;

    private static int IndexOfEscape(string text, int start)
    {
        for (var index = start; index < text.Length - 1; index++)
        {
            if (text[index] == Open && MarkerEnd(text, index + 1) > 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsBodyCharacter(char value) =>
        char.IsAsciiDigit(value) || char.IsAsciiLetterUpper(value) || value == '_';

    private static bool IsHexadecimal(string value)
    {
        foreach (var character in value)
        {
            if (!char.IsAsciiDigit(character) && character is < 'A' or > 'F')
            {
                return false;
            }
        }

        return true;
    }

    private static void RequireType(EntityType type)
    {
        if (type.IsDefault)
        {
            throw new ArgumentException("The entity type carries no value.", nameof(type));
        }
    }
}
