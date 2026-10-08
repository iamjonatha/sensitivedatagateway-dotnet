using System;

namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// What a <see cref="DocumentAnonymizer"/> needs that a policy cannot state.
/// </summary>
/// <remarks>
/// A policy says <em>hash this</em> and <em>correlate that across the session</em>. It cannot
/// say which secret keys the hash or which session is meant, because those are properties of
/// the running application and not of the rule. They come from here, and the anonymizer
/// refuses to run a policy whose decisions need something this does not carry.
/// </remarks>
public sealed class AnonymizationOptions
{
    private AnonymizationOptions(HashingKey? hashingKey, string? sessionName, string? tenantName)
    {
        HashingKey = hashingKey;
        SessionName = sessionName;
        TenantName = tenantName;
    }

    /// <summary>
    /// Gets options that support no hashing and no scope wider than a bundle.
    /// </summary>
    public static AnonymizationOptions Default { get; } = new(hashingKey: null, sessionName: null, tenantName: null);

    /// <summary>
    /// Gets the secret that keys every hash token, when the application supplied one.
    /// </summary>
    public HashingKey? HashingKey { get; }

    /// <summary>
    /// Gets the name of the session that <see cref="PseudonymScope.Session"/> refers to.
    /// </summary>
    public string? SessionName { get; }

    /// <summary>
    /// Gets the name of the tenant that <see cref="PseudonymScope.Tenant"/> refers to.
    /// </summary>
    public string? TenantName { get; }

    /// <summary>
    /// Returns options that key hash tokens with a secret.
    /// </summary>
    /// <param name="hashingKey">The secret.</param>
    /// <returns>The new options.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="hashingKey"/> is <see langword="null"/>.</exception>
    public AnonymizationOptions WithHashingKey(HashingKey hashingKey)
    {
        ArgumentNullException.ThrowIfNull(hashingKey);

        return new AnonymizationOptions(hashingKey, SessionName, TenantName);
    }

    /// <summary>
    /// Returns options that name the session tokens correlate across.
    /// </summary>
    /// <param name="sessionName">The name of the session.</param>
    /// <returns>The new options.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sessionName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sessionName"/> is empty or too long.</exception>
    public AnonymizationOptions WithSession(string sessionName)
    {
        Text.Require(sessionName, PseudonymScopeKey.MaxLength, nameof(sessionName));

        return new AnonymizationOptions(HashingKey, sessionName, TenantName);
    }

    /// <summary>
    /// Returns options that name the tenant tokens correlate across.
    /// </summary>
    /// <param name="tenantName">The name of the tenant.</param>
    /// <returns>The new options.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tenantName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="tenantName"/> is empty or too long.</exception>
    public AnonymizationOptions WithTenant(string tenantName)
    {
        Text.Require(tenantName, PseudonymScopeKey.MaxLength, nameof(tenantName));

        return new AnonymizationOptions(HashingKey, SessionName, tenantName);
    }
}
