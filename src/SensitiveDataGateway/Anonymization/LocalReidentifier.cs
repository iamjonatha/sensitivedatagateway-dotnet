using System;
using System.Text;

namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// Puts the original values back into a text, inside the boundary.
/// </summary>
/// <remarks>
/// <para>
/// Re-identification is deliberately awkward. It is a separate type with an explicit name, it
/// takes the scope key of the map it needs, and it exists only on the side of the boundary
/// where the values never left. Nothing in a provider package, a sanitized document or a
/// validated payload can reach it.
/// </para>
/// <para>
/// Only pseudonyms come back. A hash token has no inverse and a redaction marker stands for a
/// value that was not kept, so both survive the round trip unchanged — which is the point of
/// choosing them over a pseudonym in the first place.
/// </para>
/// </remarks>
public sealed class LocalReidentifier
{
    private readonly IPseudonymStore _store;

    /// <summary>
    /// Creates a re-identifier.
    /// </summary>
    /// <param name="store">Where the pseudonym maps are kept.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is <see langword="null"/>.</exception>
    public LocalReidentifier(IPseudonymStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
    }

    /// <summary>
    /// Replaces every pseudonym token in a text with the value it stands for.
    /// </summary>
    /// <param name="text">The text to restore, typically an answer that came back from a provider.</param>
    /// <param name="key">The scope whose map allocated the tokens.</param>
    /// <returns>The restored text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> carries no value.</exception>
    /// <remarks>
    /// A token this map never allocated is left as it stands. An unknown token is not an
    /// error: it is either a token of another scope, or text that merely looks like one.
    /// </remarks>
    public string Restore(string text, PseudonymScopeKey key)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (key.IsDefault)
        {
            throw new ArgumentException("The scope key carries no value.", nameof(key));
        }

        return _store.TryGet(key, out var map) && map is not null
            ? Restore(text, map)
            : MarkerSyntax.Unescape(text);
    }

    private static string Restore(string text, PseudonymMap map)
    {
        var builder = new StringBuilder(text.Length);
        var index = 0;

        while (index < text.Length)
        {
            index = AppendFrom(builder, text, index, map);
        }

        return builder.ToString();
    }

    private static int AppendFrom(StringBuilder builder, string text, int index, PseudonymMap map)
    {
        if (text[index] != MarkerSyntax.Open)
        {
            builder.Append(text[index]);

            return index + 1;
        }

        var escapedEnd = MarkerSyntax.MarkerEnd(text, index + 1);

        if (escapedEnd > 0)
        {
            builder.Append(text, index + 1, escapedEnd - index - 1);

            return escapedEnd;
        }

        var end = MarkerSyntax.MarkerEnd(text, index);

        if (end < 0)
        {
            builder.Append(text[index]);

            return index + 1;
        }

        AppendToken(builder, text[index..end], map);

        return end;
    }

    private static void AppendToken(StringBuilder builder, string token, PseudonymMap map) =>
        builder.Append(map.TryReveal(token, out var value) && value is not null ? value : token);
}
