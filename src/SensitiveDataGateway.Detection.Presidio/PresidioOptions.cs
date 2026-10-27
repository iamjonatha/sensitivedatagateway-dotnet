using System;
using System.Collections.Generic;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Detection.Presidio;

/// <summary>
/// How to reach a Presidio analyzer, and what to do with what it says.
/// </summary>
/// <remarks>
/// Nothing here has a default endpoint. A detector that silently pointed at
/// <c>localhost</c> would work on the developer's machine and send documents to whatever
/// answers on that port everywhere else.
/// </remarks>
public sealed class PresidioOptions
{
    private static readonly IReadOnlyDictionary<string, EntityType> s_defaultMap = BuildDefaultMap();

    private PresidioOptions(
        Uri endpoint,
        string language,
        double minimumScore,
        TrustBoundaryLocality locality,
        IReadOnlyDictionary<string, EntityType> entityTypes)
    {
        Endpoint = endpoint;
        Language = language;
        MinimumScore = minimumScore;
        Locality = locality;
        EntityTypes = entityTypes;
    }

    /// <summary>
    /// Gets the analyzer's base address.
    /// </summary>
    public Uri Endpoint { get; }

    /// <summary>
    /// Gets the language code sent with every request.
    /// </summary>
    public string Language { get; }

    /// <summary>
    /// Gets the score below which a result is discarded.
    /// </summary>
    /// <remarks>
    /// Presidio returns a score between zero and one. Raising this threshold makes the
    /// gateway miss things, which is the direction that leaks; the default keeps everything
    /// the analyzer was willing to report.
    /// </remarks>
    public double MinimumScore { get; }

    /// <summary>
    /// Gets where the analyzer runs, as the deployment declares it.
    /// </summary>
    public TrustBoundaryLocality Locality { get; }

    /// <summary>
    /// Gets the map from a Presidio entity name to a gateway entity type.
    /// </summary>
    public IReadOnlyDictionary<string, EntityType> EntityTypes { get; }

    /// <summary>
    /// Describes an analyzer the deployment runs itself.
    /// </summary>
    /// <param name="endpoint">The analyzer's base address.</param>
    /// <returns>The options, in English, keeping every result the analyzer reports.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoint"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="endpoint"/> is not an absolute HTTP address.</exception>
    /// <remarks>
    /// This says the endpoint is inside the trust boundary. It does not check: an address is
    /// not evidence of who operates it, and the library cannot tell a container on the same
    /// host from a hosted service behind a private DNS name. The claim belongs to whoever
    /// writes the address.
    /// </remarks>
    public static PresidioOptions ForSelfHosted(Uri endpoint) => ForSelfHosted(endpoint, "en");

    /// <summary>
    /// Describes an analyzer the deployment runs itself.
    /// </summary>
    /// <param name="endpoint">The analyzer's base address.</param>
    /// <param name="language">The language code to analyze in.</param>
    /// <returns>The options, keeping every result the analyzer reports.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="endpoint"/> is not an absolute HTTP address, or
    /// <paramref name="language"/> is blank.
    /// </exception>
    public static PresidioOptions ForSelfHosted(Uri endpoint, string language) =>
        new(Checked(endpoint), Checked(language), 0.0, TrustBoundaryLocality.SelfHosted, s_defaultMap);

    /// <summary>
    /// Describes an analyzer somebody else runs.
    /// </summary>
    /// <param name="endpoint">The analyzer's base address.</param>
    /// <param name="language">The language code to analyze in.</param>
    /// <returns>The options, keeping every result the analyzer reports.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="endpoint"/> is not an absolute https address, or
    /// <paramref name="language"/> is blank.
    /// </exception>
    /// <remarks>
    /// Every document examined is transmitted in full, before any sanitization, to whoever
    /// operates that address. A detector built on these options is refused by
    /// <c>SemanticDetectorHost.Create</c> and has to be admitted deliberately. The address
    /// must be <c>https</c>: raw content crossing a network somebody else operates is the one
    /// case where the transport is part of the boundary rather than a deployment detail.
    /// </remarks>
    public static PresidioOptions ForHostedService(Uri endpoint, string language) =>
        new(CheckedRemote(endpoint), Checked(language), 0.0, TrustBoundaryLocality.Remote, s_defaultMap);

    /// <summary>
    /// Returns the same options, discarding results the analyzer was less sure about.
    /// </summary>
    /// <param name="minimumScore">The lowest score to keep, between zero and one.</param>
    /// <returns>The new options.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The score is outside zero to one.</exception>
    public PresidioOptions WithMinimumScore(double minimumScore)
    {
        if (minimumScore is < 0.0 or > 1.0 || double.IsNaN(minimumScore))
        {
            throw new ArgumentOutOfRangeException(nameof(minimumScore), "A score lies between zero and one.");
        }

        return new PresidioOptions(Endpoint, Language, minimumScore, Locality, EntityTypes);
    }

    /// <summary>
    /// Returns the same options, with the analyzer's entity names mapped differently.
    /// </summary>
    /// <param name="entityTypes">The map from a Presidio entity name to a gateway type.</param>
    /// <returns>The new options.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entityTypes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The map is empty.</exception>
    /// <remarks>
    /// A name this map does not contain is reported under <see cref="EntityType.Create"/> of
    /// the analyzer's own name rather than dropped, because an unrecognized finding is still
    /// a finding and dropping it is the direction that leaks.
    /// </remarks>
    public PresidioOptions WithEntityTypes(IReadOnlyDictionary<string, EntityType> entityTypes)
    {
        ArgumentNullException.ThrowIfNull(entityTypes);

        if (entityTypes.Count == 0)
        {
            throw new ArgumentException("An analyzer is asked about at least one entity type.", nameof(entityTypes));
        }

        return new PresidioOptions(
            Endpoint,
            Language,
            MinimumScore,
            Locality,
            new Dictionary<string, EntityType>(entityTypes, StringComparer.OrdinalIgnoreCase));
    }

    private static Uri Checked(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (!endpoint.IsAbsoluteUri || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("A Presidio endpoint is an absolute http or https address.", nameof(endpoint));
        }

        return endpoint;
    }

    /// <summary>
    /// Checks the address of an analyzer outside the trust boundary.
    /// </summary>
    /// <param name="endpoint">The analyzer's base address.</param>
    /// <returns>The same address.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoint"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="endpoint"/> is not an absolute https address.</exception>
    /// <remarks>
    /// <c>http</c> is allowed for a self-hosted analyzer because a container on the same host
    /// is reached over a loopback or a bridge the deployment owns, and demanding a
    /// certificate there buys nothing. It is refused here because the content is unsanitized
    /// and the network is not the deployment's: cleartext would hand every document to anyone
    /// on the path, which is not the risk the caller accepted by choosing a hosted analyzer.
    /// </remarks>
    private static Uri CheckedRemote(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException(
                "A hosted Presidio analyzer receives unsanitized content and is reached over https.",
                nameof(endpoint));
        }

        return endpoint;
    }

    private static string Checked(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        return language;
    }

    private static Dictionary<string, EntityType> BuildDefaultMap() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["PERSON"] = EntityType.Person,
            ["EMAIL_ADDRESS"] = EntityType.Email,
            ["PHONE_NUMBER"] = EntityType.Phone,
            ["LOCATION"] = EntityType.Address,
            ["DATE_TIME"] = EntityType.DateOfBirth,
            ["IBAN_CODE"] = EntityType.Iban,
            ["CREDIT_CARD"] = EntityType.CreditCard,
            ["US_BANK_NUMBER"] = EntityType.BankAccount,
            ["US_SSN"] = EntityType.NationalId,
            ["US_ITIN"] = EntityType.TaxId,
            ["US_PASSPORT"] = EntityType.Passport,
            ["US_DRIVER_LICENSE"] = EntityType.DriverLicense,
            ["IT_FISCAL_CODE"] = EntityType.TaxId,
            ["IT_VAT_CODE"] = EntityType.TaxId,
            ["IT_PASSPORT"] = EntityType.Passport,
            ["IT_DRIVER_LICENSE"] = EntityType.DriverLicense,
            ["IT_IDENTITY_CARD"] = EntityType.NationalId,
            ["UK_NHS"] = EntityType.Health,
            ["MEDICAL_LICENSE"] = EntityType.Health,
            ["IP_ADDRESS"] = EntityType.IpAddress,
            ["URL"] = EntityType.Url,
            ["CRYPTO"] = EntityType.FinancialReference,
            ["NRP"] = EntityType.Person,
        };
}
