using System;
using System.Collections.Generic;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// The detectors that find a value by its shape rather than by its meaning.
/// </summary>
/// <remarks>
/// <para>
/// Every detector here is deterministic: the same text produces the same candidates, on every
/// machine and in every run. None of them reads a file, opens a socket or consults a model,
/// so none of them can leak the document it is scanning, and none of them can fail because
/// something elsewhere is down.
/// </para>
/// <para>
/// Each detector reports candidates. It does not decide: a candidate becomes a redaction
/// because a policy says so, and a shape that carries a check digit is reported only when the
/// digit agrees, because a number that fails its own checksum is not that kind of number at
/// all.
/// </para>
/// <para>
/// Detectors are versioned. A change to what a detector matches is a change to its version,
/// so an audit record from last year states which rules produced it.
/// </para>
/// </remarks>
public static class DeterministicDetectors
{
    /// <summary>
    /// The version every built-in detector reports until its rules change.
    /// </summary>
    public const string Version = "1.0.0";

    /// <summary>
    /// Creates a detector for email addresses.
    /// </summary>
    /// <returns>The detector.</returns>
    public static ISensitiveDataDetector Email() =>
        new PatternScanDetector(
            VersionOf("email"),
            PatternRule.Create(
                BuiltInPatterns.Email(),
                "email",
                EntityType.Email,
                SensitivityLevel.Moderate,
                DetectionConfidence.Create(0.95)));

    /// <summary>
    /// Creates a detector for telephone numbers.
    /// </summary>
    /// <returns>The detector.</returns>
    /// <remarks>
    /// It finds numbers written in international form, and numbers a label introduces. A bare
    /// national number is indistinguishable from any other run of digits, so it is left to a
    /// deployment that knows its own formats to add a pattern for it.
    /// </remarks>
    public static ISensitiveDataDetector Phone() =>
        new PatternScanDetector(
            VersionOf("phone"),
            PatternRule.Create(
                BuiltInPatterns.InternationalPhone(),
                "phone-international",
                EntityType.Phone,
                SensitivityLevel.Moderate,
                DetectionConfidence.Create(0.92)),
            PatternRule.Create(
                BuiltInPatterns.LabelledPhone(),
                "phone-labelled",
                EntityType.Phone,
                SensitivityLevel.Moderate,
                DetectionConfidence.Create(0.95)));

    /// <summary>
    /// Creates a detector for international bank account numbers.
    /// </summary>
    /// <returns>The detector.</returns>
    /// <remarks>
    /// The mod-97 check is strong enough that a candidate which passes it is reported as
    /// certain, and a candidate which fails it is dropped rather than reported weakly.
    /// </remarks>
    public static ISensitiveDataDetector Iban() =>
        new PatternScanDetector(
            VersionOf("iban"),
            PatternRule.Create(
                BuiltInPatterns.Iban(),
                "iban-mod97",
                EntityType.Iban,
                SensitivityLevel.High,
                DetectionConfidence.Certain,
                static value => IbanChecksum.IsValid(value)));

    /// <summary>
    /// Creates a detector for payment card numbers.
    /// </summary>
    /// <returns>The detector.</returns>
    /// <remarks>
    /// Luhn rejects most sequences of digits, but one in ten passes it by chance, so a card
    /// number is reported just below certainty.
    /// </remarks>
    public static ISensitiveDataDetector CreditCard() =>
        new PatternScanDetector(
            VersionOf("credit-card"),
            PatternRule.Create(
                BuiltInPatterns.PaymentCard(),
                "card-luhn",
                EntityType.CreditCard,
                SensitivityLevel.High,
                DetectionConfidence.Create(0.95),
                static value => Luhn.IsValid(value)),
            PatternRule.Create(
                BuiltInPatterns.FifteenDigitPaymentCard(),
                "card-luhn-15",
                EntityType.CreditCard,
                SensitivityLevel.High,
                DetectionConfidence.Create(0.95),
                static value => Luhn.IsValid(value)));

    /// <summary>
    /// Creates a detector for Italian tax codes.
    /// </summary>
    /// <returns>The detector.</returns>
    /// <remarks>
    /// Tax identification is national by nature, so this is the first of a family rather than
    /// a general detector. Another country's code is another detector, with its own structure,
    /// its own check and its own version.
    /// </remarks>
    public static ISensitiveDataDetector ItalianTaxId() =>
        new PatternScanDetector(
            VersionOf("tax-id-it"),
            PatternRule.Create(
                BuiltInPatterns.ItalianTaxCode(),
                "tax-id-it",
                EntityType.TaxId,
                SensitivityLevel.High,
                DetectionConfidence.Certain,
                static value => ItalianTaxCode.IsValid(value.ToUpperInvariant())));

    /// <summary>
    /// Creates a detector for JSON web tokens.
    /// </summary>
    /// <returns>The detector.</returns>
    public static ISensitiveDataDetector Jwt() =>
        new PatternScanDetector(
            VersionOf("jwt"),
            PatternRule.Create(
                BuiltInPatterns.JsonWebToken(),
                "jwt",
                EntityType.Jwt,
                SensitivityLevel.Critical,
                DetectionConfidence.Certain));

    /// <summary>
    /// Creates a detector for API keys and access tokens.
    /// </summary>
    /// <returns>The detector.</returns>
    /// <remarks>
    /// It knows the shapes several widely used services issue, and it knows what an assignment
    /// to something called a key or a token looks like. It cannot know every vendor's format,
    /// which is what <see cref="HighEntropy()"/> is for.
    /// </remarks>
    public static ISensitiveDataDetector ApiKey() =>
        new PatternScanDetector(
            VersionOf("api-key"),
            PatternRule.Create(
                BuiltInPatterns.AwsAccessKeyId(),
                "aws-access-key-id",
                EntityType.ApiKey,
                SensitivityLevel.Critical,
                DetectionConfidence.Certain),
            PatternRule.Create(
                BuiltInPatterns.GoogleApiKey(),
                "google-api-key",
                EntityType.ApiKey,
                SensitivityLevel.Critical,
                DetectionConfidence.Certain),
            PatternRule.Create(
                BuiltInPatterns.StripeSecretKey(),
                "stripe-secret-key",
                EntityType.ApiKey,
                SensitivityLevel.Critical,
                DetectionConfidence.Certain),
            PatternRule.Create(
                BuiltInPatterns.GitHubToken(),
                "github-token",
                EntityType.AccessToken,
                SensitivityLevel.Critical,
                DetectionConfidence.Certain),
            PatternRule.Create(
                BuiltInPatterns.SlackToken(),
                "slack-token",
                EntityType.AccessToken,
                SensitivityLevel.Critical,
                DetectionConfidence.Certain),
            PatternRule.Create(
                BuiltInPatterns.AssignedSecret(),
                "assigned-secret",
                EntityType.ApiKey,
                SensitivityLevel.Critical,
                DetectionConfidence.Create(0.85)));

    /// <summary>
    /// Creates a detector for private keys.
    /// </summary>
    /// <returns>The detector.</returns>
    /// <remarks>
    /// A complete block is reported whole, so that removing it removes the key rather than its
    /// first line. A block whose end is missing is reported by its header, because a truncated
    /// key is still a disclosure and still has to be refused.
    /// </remarks>
    public static ISensitiveDataDetector PrivateKey() =>
        new PatternScanDetector(
            VersionOf("private-key"),
            PatternRule.Create(
                BuiltInPatterns.PrivateKeyBlock(),
                "pem-private-key-block",
                EntityType.PrivateKey,
                SensitivityLevel.Critical,
                DetectionConfidence.Certain),
            PatternRule.Create(
                BuiltInPatterns.PrivateKeyHeader(),
                "pem-private-key-header",
                EntityType.PrivateKey,
                SensitivityLevel.Critical,
                DetectionConfidence.Certain));

    /// <summary>
    /// Creates a detector for connection strings.
    /// </summary>
    /// <returns>The detector.</returns>
    public static ISensitiveDataDetector ConnectionString() =>
        new PatternScanDetector(
            VersionOf("connection-string"),
            PatternRule.Create(
                BuiltInPatterns.AdoConnectionString(),
                "ado-connection-string",
                EntityType.ConnectionString,
                SensitivityLevel.Critical,
                DetectionConfidence.Create(0.95)),
            PatternRule.Create(
                BuiltInPatterns.CredentialsInUrl(),
                "credentials-in-url",
                EntityType.ConnectionString,
                SensitivityLevel.Critical,
                DetectionConfidence.Certain));

    /// <summary>
    /// Creates a detector for passwords written next to their name.
    /// </summary>
    /// <returns>The detector.</returns>
    /// <remarks>
    /// The reported span covers the value and not the label, so that a redacted document still
    /// reads as a configuration file with a password in it.
    /// </remarks>
    public static ISensitiveDataDetector Password() =>
        new PatternScanDetector(
            VersionOf("password"),
            PatternRule.Create(
                BuiltInPatterns.AssignedPassword(),
                "assigned-password",
                EntityType.Password,
                SensitivityLevel.Critical,
                DetectionConfidence.Create(0.85)));

    /// <summary>
    /// Creates a detector for uniform resource locators.
    /// </summary>
    /// <returns>The detector.</returns>
    public static ISensitiveDataDetector Url() =>
        new PatternScanDetector(
            VersionOf("url"),
            PatternRule.Create(
                BuiltInPatterns.Url(),
                "url",
                EntityType.Url,
                SensitivityLevel.Moderate,
                DetectionConfidence.Certain));

    /// <summary>
    /// Creates a detector for internet protocol addresses.
    /// </summary>
    /// <returns>The detector.</returns>
    /// <remarks>
    /// Version six is matched only in its full eight-group form. The abbreviated forms are
    /// indistinguishable from a time, a ratio or a chapter reference, and a detector that
    /// redacts timestamps is a detector an operator switches off.
    /// </remarks>
    public static ISensitiveDataDetector IpAddress() =>
        new PatternScanDetector(
            VersionOf("ip-address"),
            PatternRule.Create(
                BuiltInPatterns.IpV4Address(),
                "ipv4",
                EntityType.IpAddress,
                SensitivityLevel.Moderate,
                DetectionConfidence.Create(0.90)),
            PatternRule.Create(
                BuiltInPatterns.IpV6Address(),
                "ipv6",
                EntityType.IpAddress,
                SensitivityLevel.Moderate,
                DetectionConfidence.Create(0.90)));

    /// <summary>
    /// Creates a detector for runs of text too dense to be prose.
    /// </summary>
    /// <returns>The detector.</returns>
    public static ISensitiveDataDetector HighEntropy() => HighEntropy(EntropyDetectorOptions.Default);

    /// <summary>
    /// Creates a detector for runs of text too dense to be prose.
    /// </summary>
    /// <param name="options">What counts as dense enough.</param>
    /// <returns>The detector.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static ISensitiveDataDetector HighEntropy(EntropyDetectorOptions options) =>
        new EntropyDetector(VersionOf("high-entropy"), options);

    /// <summary>
    /// Creates every built-in detector.
    /// </summary>
    /// <returns>The detectors, in a fixed order.</returns>
    /// <remarks>
    /// This is the set a deployment starts from. Removing one is a decision about what the
    /// gateway is allowed to miss, and it belongs in the policy review rather than in the
    /// wiring.
    /// </remarks>
    public static IReadOnlyList<ISensitiveDataDetector> All() =>
    [
        Email(),
        Phone(),
        Iban(),
        CreditCard(),
        ItalianTaxId(),
        Jwt(),
        ApiKey(),
        PrivateKey(),
        ConnectionString(),
        Password(),
        Url(),
        IpAddress(),
        HighEntropy(),
    ];

    private static DetectorVersion VersionOf(string name) =>
        DetectorVersion.Create(DetectorId.Create(name), Version);
}
