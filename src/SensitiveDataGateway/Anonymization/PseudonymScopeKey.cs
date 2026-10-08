using System;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// The thing a pseudonym scope is scoped to.
/// </summary>
/// <remarks>
/// <para>
/// A scope is only half of an answer. <see cref="PseudonymScope.Bundle"/> says that a token
/// means one thing across a bundle; which bundle is what this type carries. Two operations on
/// two bundles share a scope and must not share tokens.
/// </para>
/// <para>
/// The key is an identifier, never a value: a bundle identifier, a part identifier, or a
/// session or tenant name the application chose. Nothing derived from document content
/// belongs here.
/// </para>
/// </remarks>
public readonly struct PseudonymScopeKey : IEquatable<PseudonymScopeKey>
{
    /// <summary>
    /// The greatest number of characters in a scope key.
    /// </summary>
    public const int MaxLength = 128;

    private readonly string? _value;

    private PseudonymScopeKey(PseudonymScope scope, string value)
    {
        Scope = scope;
        _value = value;
    }

    /// <summary>
    /// Gets how far a token stays correlatable.
    /// </summary>
    public PseudonymScope Scope { get; }

    /// <summary>
    /// Gets the identifier of the thing the scope is scoped to.
    /// </summary>
    /// <exception cref="InvalidOperationException">The key is the default value.</exception>
    public string Value => _value ?? throw new InvalidOperationException("The scope key carries no value.");

    /// <summary>
    /// Gets a value indicating whether the key carries nothing.
    /// </summary>
    public bool IsDefault => _value is null;

    /// <summary>
    /// Creates the key of a document-scoped allocation.
    /// </summary>
    /// <param name="partId">The part tokens are correlatable within.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentException"><paramref name="partId"/> is the default value.</exception>
    public static PseudonymScopeKey ForDocument(PartId partId)
    {
        if (partId.IsDefault)
        {
            throw new ArgumentException("The part identifier carries no value.", nameof(partId));
        }

        return new PseudonymScopeKey(PseudonymScope.Document, partId.Value);
    }

    /// <summary>
    /// Creates the key of a bundle-scoped allocation.
    /// </summary>
    /// <param name="bundleId">The bundle tokens are correlatable within.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentException"><paramref name="bundleId"/> is the default value.</exception>
    public static PseudonymScopeKey ForBundle(BundleId bundleId)
    {
        if (bundleId.IsDefault)
        {
            throw new ArgumentException("The bundle identifier carries no value.", nameof(bundleId));
        }

        return new PseudonymScopeKey(PseudonymScope.Bundle, bundleId.Value);
    }

    /// <summary>
    /// Creates the key of a session-scoped or tenant-scoped allocation.
    /// </summary>
    /// <param name="scope">The scope, which must be wider than a bundle.</param>
    /// <param name="name">The name of the session or the tenant.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="scope"/> is not <see cref="PseudonymScope.Session"/> or
    /// <see cref="PseudonymScope.Tenant"/>, or <paramref name="name"/> is empty, too long or
    /// holds a control character.
    /// </exception>
    /// <remarks>
    /// A scope wider than a bundle correlates tokens across documents. The application names
    /// the thing it is correlating within, because the gateway has no way to know what a
    /// session is in an application it has never seen.
    /// </remarks>
    public static PseudonymScopeKey ForWiderScope(PseudonymScope scope, string name)
    {
        if (scope is not (PseudonymScope.Session or PseudonymScope.Tenant))
        {
            throw new ArgumentException(
                "A session or tenant scope is the only scope named by the application.",
                nameof(scope));
        }

        return new PseudonymScopeKey(scope, Text.Require(name, MaxLength, nameof(name)));
    }

    /// <summary>
    /// Determines whether two keys name the same thing.
    /// </summary>
    /// <param name="left">The first key.</param>
    /// <param name="right">The second key.</param>
    /// <returns><see langword="true"/> when the keys are equal.</returns>
    public static bool operator ==(PseudonymScopeKey left, PseudonymScopeKey right) => left.Equals(right);

    /// <summary>
    /// Determines whether two keys name different things.
    /// </summary>
    /// <param name="left">The first key.</param>
    /// <param name="right">The second key.</param>
    /// <returns><see langword="true"/> when the keys differ.</returns>
    public static bool operator !=(PseudonymScopeKey left, PseudonymScopeKey right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(PseudonymScopeKey other) =>
        Scope == other.Scope && string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PseudonymScopeKey other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Scope, _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value));

    /// <summary>
    /// Returns the key.
    /// </summary>
    /// <returns>The scope and the identifier. Neither is document content.</returns>
    public override string ToString() => _value is null ? "(none)" : $"{Scope}:{_value}";
}
