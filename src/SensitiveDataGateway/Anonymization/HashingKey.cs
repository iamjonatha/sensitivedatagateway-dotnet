using System;
using System.Security.Cryptography;

namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// The secret that makes a hash token something other than a reversible encoding.
/// </summary>
/// <remarks>
/// <para>
/// A digest of a short value is not anonymization. A phone number, a tax code or a card
/// number has a small enough space that an unkeyed digest is reversed by enumerating it, so
/// the gateway never produces one: every hash token is a keyed MAC, and the key is this.
/// </para>
/// <para>
/// The key is as sensitive as a pseudonym map, and for the same reason — it is what turns a
/// token back into evidence about a value. It never leaves the process, never appears in a
/// log and has no accessor that returns its material.
/// </para>
/// </remarks>
public sealed class HashingKey
{
    /// <summary>
    /// The smallest number of bytes in a hashing key.
    /// </summary>
    public const int MinimumLength = 32;

    /// <summary>
    /// The number of bytes of the digest a token carries.
    /// </summary>
    public const int DigestBytes = 8;

    private readonly byte[] _material;

    private HashingKey(byte[] material) => _material = material;

    /// <summary>
    /// Creates a key from material the application supplies.
    /// </summary>
    /// <param name="material">At least <see cref="MinimumLength"/> bytes. The array is copied.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentException"><paramref name="material"/> is too short.</exception>
    /// <remarks>
    /// A supplied key makes hash tokens stable across operations and across processes, which
    /// is the whole reason to supply one. That stability is also cross-document
    /// correlatability, so the key belongs in the same place as any other long-lived secret.
    /// </remarks>
    public static HashingKey Create(ReadOnlySpan<byte> material)
    {
        if (material.Length < MinimumLength)
        {
            throw new ArgumentException(
                $"A hashing key carries at least {MinimumLength} bytes.",
                nameof(material));
        }

        return new HashingKey(material.ToArray());
    }

    /// <summary>
    /// Creates a random key that lives as long as the object does.
    /// </summary>
    /// <returns>The key.</returns>
    /// <remarks>
    /// This is the safe default. Tokens are correlatable for as long as the key exists and
    /// meaningless afterwards, so an application that has not thought about cross-process
    /// correlation does not accidentally get it.
    /// </remarks>
    public static HashingKey CreateEphemeral() => new(RandomNumberGenerator.GetBytes(MinimumLength));

    /// <summary>
    /// Computes the digest a token carries for a value.
    /// </summary>
    /// <param name="value">The value, which is document content.</param>
    /// <returns>The upper-case hexadecimal digest.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    internal string Digest(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        Span<byte> mac = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(_material, System.Text.Encoding.UTF8.GetBytes(value), mac);

        return Convert.ToHexString(mac[..DigestBytes]);
    }

    /// <summary>
    /// Returns a description of the key.
    /// </summary>
    /// <returns>A fixed string. Key material is never rendered.</returns>
    public override string ToString() => "HashingKey";
}
