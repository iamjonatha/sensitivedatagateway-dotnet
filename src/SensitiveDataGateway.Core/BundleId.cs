using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace SensitiveDataGateway;

/// <summary>
/// Identifies one processing operation from end to end.
/// </summary>
/// <remarks>
/// <para>
/// A bundle identifier is the correlation key of the gateway: it appears in every log line,
/// audit record and report produced while a bundle is processed, and it is the only part of
/// an operation that is allowed to cross the trust boundary unchanged.
/// </para>
/// <para>
/// That privilege is exactly why the value must never be derived from document content. A
/// bundle identifier that embeds a subject line, a file name or a customer name is a leak
/// with an identifier's name. Callers that need a value carrying no information at all
/// should use <see cref="New"/>; callers correlating with an external system should pass an
/// identifier that system already treats as non-sensitive.
/// </para>
/// <para>
/// The type is a value: two identifiers with the same text are the same identifier, and
/// comparison is ordinal, never culture-sensitive.
/// </para>
/// </remarks>
public readonly struct BundleId : IEquatable<BundleId>
{
    /// <summary>
    /// The greatest number of characters a bundle identifier may contain.
    /// </summary>
    /// <remarks>
    /// The bound is deliberate rather than incidental. An unbounded identifier is a place to
    /// smuggle content into a log line, and it is a denial-of-service surface for anything
    /// that stores one identifier per record.
    /// </remarks>
    public const int MaxLength = 128;

    private readonly string? _value;

    private BundleId(string value) => _value = value;

    /// <summary>
    /// Gets the textual form of the identifier.
    /// </summary>
    /// <value>
    /// The trimmed, non-empty text the identifier was created from.
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The identifier is the default value of the struct and therefore carries no text.
    /// </exception>
    public string Value =>
        _value ?? throw new InvalidOperationException(
            "A default BundleId carries no value. Create one with BundleId.New or BundleId.Create.");

    /// <summary>
    /// Gets a value indicating whether this identifier is the default value of the struct.
    /// </summary>
    /// <value>
    /// <see langword="true"/> when the identifier was never created through <see cref="New"/>
    /// or <see cref="Create(string)"/>; otherwise <see langword="false"/>.
    /// </value>
    /// <remarks>
    /// A struct cannot forbid its own default value, so the gateway rejects a default
    /// identifier at the boundary it reaches rather than pretending it cannot exist.
    /// </remarks>
    public bool IsDefault => _value is null;

    /// <summary>
    /// Creates an identifier that carries no information about the bundle it identifies.
    /// </summary>
    /// <returns>A new identifier backed by a random value.</returns>
    public static BundleId New() =>
        new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    /// <summary>
    /// Creates an identifier from an existing value.
    /// </summary>
    /// <param name="value">
    /// The identifier text. Surrounding white space is removed. The result must be non-empty
    /// and no longer than <see cref="MaxLength"/> characters.
    /// </param>
    /// <returns>The identifier.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is empty or white space, contains a control character, or is
    /// longer than <see cref="MaxLength"/> characters once trimmed.
    /// </exception>
    /// <remarks>
    /// The exception message describes the rule that was broken and never quotes the rejected
    /// value, because a rejected identifier may be the very content the caller should not
    /// have passed.
    /// </remarks>
    public static BundleId Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!TryCreate(value, out var bundleId))
        {
            throw new ArgumentException(
                $"A bundle identifier must be non-empty, free of control characters and at most {MaxLength.ToString(CultureInfo.InvariantCulture)} characters once trimmed.",
                nameof(value));
        }

        return bundleId;
    }

    /// <summary>
    /// Attempts to create an identifier from an existing value.
    /// </summary>
    /// <param name="value">The identifier text, or <see langword="null"/>.</param>
    /// <param name="bundleId">
    /// When this method returns <see langword="true"/>, the identifier; otherwise the default
    /// value of the struct.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="value"/> satisfies the rules described on
    /// <see cref="Create(string)"/>; otherwise <see langword="false"/>.
    /// </returns>
    public static bool TryCreate([NotNullWhen(true)] string? value, out BundleId bundleId)
    {
        bundleId = default;

        if (value is null)
        {
            return false;
        }

        var trimmed = value.Trim();

        if (trimmed.Length is 0 or > MaxLength)
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

        bundleId = new BundleId(trimmed);
        return true;
    }

    /// <inheritdoc />
    public bool Equals(BundleId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is BundleId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>
    /// Returns the textual form of the identifier.
    /// </summary>
    /// <returns>
    /// The value of the identifier, or an empty string when it is the default value of the
    /// struct.
    /// </returns>
    /// <remarks>
    /// Unlike <see cref="Value"/> this method never throws, because it is reached from
    /// logging and diagnostics, where an exception would obscure the failure being reported.
    /// </remarks>
    public override string ToString() => _value ?? string.Empty;

    /// <summary>
    /// Determines whether two identifiers are the same.
    /// </summary>
    /// <param name="left">The first identifier.</param>
    /// <param name="right">The second identifier.</param>
    /// <returns><see langword="true"/> when they carry the same value.</returns>
    public static bool operator ==(BundleId left, BundleId right) => left.Equals(right);

    /// <summary>
    /// Determines whether two identifiers differ.
    /// </summary>
    /// <param name="left">The first identifier.</param>
    /// <param name="right">The second identifier.</param>
    /// <returns><see langword="true"/> when they carry different values.</returns>
    public static bool operator !=(BundleId left, BundleId right) => !left.Equals(right);
}
