using System;
using System.Diagnostics.CodeAnalysis;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// What kind of sensitive thing a detector believes it found.
/// </summary>
/// <remarks>
/// <para>
/// The taxonomy is open, not an enumeration. Every organization that runs a privacy gateway
/// has categories nobody else has — a claim number, a case reference, an internal product
/// code — and a closed enumeration would make those second-class citizens that policies
/// cannot express. A domain-specific type is created the same way a well-known one is used:
/// <c>new EntityType("CLAIM_NUMBER")</c>.
/// </para>
/// <para>
/// Types are normalized to upper case with underscores, so that <c>credit_card</c> and
/// <c>CreditCard</c> cannot become two policy rules that silently disagree.
/// </para>
/// <para>
/// A type a policy does not mention is governed by that policy's rule for unknown types,
/// whose safe default is to block. An unrecognized category is never assumed harmless.
/// </para>
/// </remarks>
public readonly partial struct EntityType : IEquatable<EntityType>
{
    /// <summary>
    /// The greatest number of characters an entity type may contain.
    /// </summary>
    public const int MaxLength = 64;

    private readonly string? _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntityType"/> struct.
    /// </summary>
    /// <param name="value">The name of the type.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The name is empty, longer than <see cref="MaxLength"/>, does not start with a letter,
    /// or contains something other than letters, digits and underscores.
    /// </exception>
    public EntityType(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!TryNormalize(value, out var normalized))
        {
            throw new ArgumentException(
                $"An entity type starts with a letter, holds at most {MaxLength} letters, digits and underscores, and cannot be empty.",
                nameof(value));
        }

        _value = normalized;
    }

    /// <summary>
    /// Gets the normalized name of the type.
    /// </summary>
    /// <exception cref="InvalidOperationException">This is the default value of the struct.</exception>
    public string Value =>
        _value ?? throw new InvalidOperationException("The entity type carries no value.");

    /// <summary>
    /// Gets a value indicating whether this is the default value of the struct.
    /// </summary>
    public bool IsDefault => _value is null;

    /// <summary>
    /// Creates an entity type.
    /// </summary>
    /// <param name="value">The name of the type.</param>
    /// <returns>The entity type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The name is not a valid entity type.</exception>
    public static EntityType Create(string value) => new(value);

    /// <summary>
    /// Tries to create an entity type.
    /// </summary>
    /// <param name="value">The candidate name.</param>
    /// <param name="entityType">When this method returns <see langword="true"/>, the entity type.</param>
    /// <returns><see langword="true"/> when the name is a valid entity type.</returns>
    public static bool TryCreate([NotNullWhen(true)] string? value, out EntityType entityType)
    {
        if (value is not null && TryNormalize(value, out var normalized))
        {
            entityType = new EntityType(normalized);
            return true;
        }

        entityType = default;
        return false;
    }

    /// <inheritdoc />
    public bool Equals(EntityType other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is EntityType other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>
    /// Returns the normalized name of the type.
    /// </summary>
    /// <returns>The name, or an empty string for the default value.</returns>
    /// <remarks>
    /// An entity type names a category and never a value, so it is safe to log.
    /// </remarks>
    public override string ToString() => _value ?? string.Empty;

    /// <summary>
    /// Determines whether two entity types are the same.
    /// </summary>
    /// <param name="left">The first type.</param>
    /// <param name="right">The second type.</param>
    /// <returns><see langword="true"/> when they normalize to the same name.</returns>
    public static bool operator ==(EntityType left, EntityType right) => left.Equals(right);

    /// <summary>
    /// Determines whether two entity types differ.
    /// </summary>
    /// <param name="left">The first type.</param>
    /// <param name="right">The second type.</param>
    /// <returns><see langword="true"/> when they normalize to different names.</returns>
    public static bool operator !=(EntityType left, EntityType right) => !left.Equals(right);

    private static bool TryNormalize(string value, [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;

        var trimmed = value.Trim();

        if (trimmed.Length == 0 || trimmed.Length > MaxLength || !char.IsAsciiLetter(trimmed[0]))
        {
            return false;
        }

        foreach (var character in trimmed)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '_')
            {
                return false;
            }
        }

        normalized = trimmed.ToUpperInvariant();
        return true;
    }
}
