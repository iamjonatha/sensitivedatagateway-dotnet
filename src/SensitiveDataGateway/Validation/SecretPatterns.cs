using System.Collections.Generic;
using System.Text.RegularExpressions;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Validation;

/// <summary>
/// Shapes that are a secret wherever they occur, independently of what detection found.
/// </summary>
/// <remarks>
/// <para>
/// These patterns are the part of the anti-leak scan that does not depend on stage 2. A value
/// no detector reported is invisible to every other check, but a private key block is a
/// private key block whether anybody classified it or not.
/// </para>
/// <para>
/// The set is deliberately small and matches only structures that cannot be anything else.
/// Stage 5 is the barrier that must not produce false alarms the operator learns to
/// disregard; broad detection belongs to stage 2, where a finding is a candidate rather than
/// a block.
/// </para>
/// </remarks>
internal static partial class SecretPatterns
{
    private const int TimeoutMilliseconds = 100;

    private static readonly (Regex Pattern, EntityType Type)[] s_patterns =
    [
        (PemPrivateKey(), EntityType.PrivateKey),
        (JsonWebToken(), EntityType.Jwt),
        (AwsAccessKeyId(), EntityType.ApiKey),
        (GitHubToken(), EntityType.AccessToken),
        (SlackToken(), EntityType.AccessToken),
        (PasswordAssignment(), EntityType.Password),
        (CredentialsInUrl(), EntityType.ConnectionString),
    ];

    /// <summary>
    /// Finds the first pattern the text matches.
    /// </summary>
    /// <param name="text">The text to scan.</param>
    /// <param name="type">The type of secret that was matched.</param>
    /// <returns><see langword="true"/> when the text matches a pattern.</returns>
    public static bool TryMatch(string text, out EntityType type)
    {
        foreach (var (pattern, matched) in s_patterns)
        {
            if (pattern.IsMatch(text))
            {
                type = matched;
                return true;
            }
        }

        type = default;
        return false;
    }

    /// <summary>
    /// Gets the number of patterns in the set, so that a test can assert the set is not empty.
    /// </summary>
    public static int Count => s_patterns.Length;

    /// <summary>
    /// Gets the type each pattern reports, so that a test can cover every one of them.
    /// </summary>
    /// <returns>The types, in pattern order, with duplicates kept.</returns>
    public static IReadOnlyList<EntityType> Types()
    {
        var types = new List<EntityType>(s_patterns.Length);

        foreach (var (_, type) in s_patterns)
        {
            types.Add(type);
        }

        return types;
    }

    [GeneratedRegex(
        "-----BEGIN (?:[A-Z]+ )?PRIVATE KEY-----",
        RegexOptions.None,
        TimeoutMilliseconds)]
    private static partial Regex PemPrivateKey();

    [GeneratedRegex(
        @"\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}",
        RegexOptions.None,
        TimeoutMilliseconds)]
    private static partial Regex JsonWebToken();

    [GeneratedRegex(
        @"\bAKIA[0-9A-Z]{16}\b",
        RegexOptions.None,
        TimeoutMilliseconds)]
    private static partial Regex AwsAccessKeyId();

    [GeneratedRegex(
        @"\bgh[pousr]_[A-Za-z0-9]{20,}\b",
        RegexOptions.None,
        TimeoutMilliseconds)]
    private static partial Regex GitHubToken();

    [GeneratedRegex(
        @"\bxox[abprs]-[A-Za-z0-9-]{10,}",
        RegexOptions.None,
        TimeoutMilliseconds)]
    private static partial Regex SlackToken();

    [GeneratedRegex(
        @"\b(?:password|pwd)\s*=\s*[^;\s]{3,}",
        RegexOptions.IgnoreCase,
        TimeoutMilliseconds)]
    private static partial Regex PasswordAssignment();

    [GeneratedRegex(
        @"\b[a-z][a-z0-9+.-]*://[^\s:/@]+:[^\s:/@]+@",
        RegexOptions.IgnoreCase,
        TimeoutMilliseconds)]
    private static partial Regex CredentialsInUrl();
}
