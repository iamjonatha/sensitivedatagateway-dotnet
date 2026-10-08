using System;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// Replaces a value with a keyed digest of it.
/// </summary>
/// <remarks>
/// A hash token is correlatable and not reversible: the same value yields the same token
/// wherever the key is the same, and no amount of the token gets the value back. That is the
/// difference from a pseudonym, which is reversible through a map that stays behind — and the
/// difference matters, because a hash cannot be undone by the application either.
/// </remarks>
public sealed class Hasher
{
    private readonly HashingKey _key;

    /// <summary>
    /// Creates a hasher.
    /// </summary>
    /// <param name="key">The secret that keys every digest.</param>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    public Hasher(HashingKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        _key = key;
    }

    /// <summary>
    /// Returns the replacement for a hashed value.
    /// </summary>
    /// <param name="type">What kind of thing is being replaced.</param>
    /// <param name="value">The value, which is document content.</param>
    /// <returns>The marker, for example <c>[IBAN_H3F9A2C71B4D5E6F7]</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="type"/> is the default value, or <paramref name="value"/> is empty.
    /// </exception>
    public string Apply(EntityType type, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value.Length == 0)
        {
            throw new ArgumentException("A hash cannot stand for an empty value.", nameof(value));
        }

        return MarkerSyntax.Hash(type, _key.Digest(value));
    }
}
