using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Policies;

/// <content>
/// The policies that ship with the library.
/// </content>
public sealed partial class SanitizationPolicy
{
    private static readonly EntityType[] s_credentials =
    [
        EntityType.Password,
        EntityType.ApiKey,
        EntityType.AccessToken,
        EntityType.Jwt,
        EntityType.PrivateKey,
        EntityType.ConnectionString,
    ];

    private static readonly EntityType[] s_directIdentifiers =
    [
        EntityType.Person,
        EntityType.Customer,
    ];

    private static readonly EntityType[] s_contactDetails =
    [
        EntityType.Email,
        EntityType.Phone,
        EntityType.Address,
    ];

    private static readonly EntityType[] s_identityDocuments =
    [
        EntityType.TaxId,
        EntityType.NationalId,
        EntityType.Passport,
        EntityType.DriverLicense,
    ];

    private static readonly EntityType[] s_financialIdentifiers =
    [
        EntityType.Iban,
        EntityType.CreditCard,
        EntityType.BankAccount,
        EntityType.FinancialReference,
    ];

    private static readonly EntityType[] s_specialCategories =
    [
        EntityType.Health,
        EntityType.Legal,
        EntityType.Hr,
        EntityType.FinancialContext,
        EntityType.TradeSecret,
    ];

    private static readonly EntityType[] s_internalReferences =
    [
        EntityType.Project,
        EntityType.Contract,
        EntityType.InternalId,
        EntityType.ConfidentialCode,
        EntityType.InternalSystem,
    ];

    private static readonly EntityType[] s_networkLocators =
    [
        EntityType.Url,
        EntityType.IpAddress,
    ];

    /// <summary>
    /// A policy that lets nothing identifying through.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything that can be removed is removed, and only names and internal references are
    /// pseudonymized, because losing them usually makes a decision impossible rather than
    /// merely harder. Credentials and security findings block outright: a credential has no
    /// safe redacted form once it has been seen by a third party.
    /// </para>
    /// <para>
    /// Use it when the receiving engine is outside your organization, or when you do not yet
    /// know what your documents contain.
    /// </para>
    /// </remarks>
    public static SanitizationPolicy Strict { get; } = BuildStrict();

    /// <summary>
    /// A policy that keeps a document usable while removing what identifies a person.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Identifiers become stable pseudonyms or hashes, so a decision engine can still see
    /// that two documents concern the same subject, and financial identifiers keep the last
    /// digits an operator needs to recognize an account. Credentials still block.
    /// </para>
    /// <para>
    /// Use it when the receiving engine is inside a boundary you control and the decision
    /// depends on relating documents to each other.
    /// </para>
    /// </remarks>
    public static SanitizationPolicy Balanced { get; } = BuildBalanced();

    private static SanitizationPolicy BuildStrict()
    {
        var builder = CreateBuilder(PolicyId.Create("strict"), PolicyVersion.Create("1.0"))
            .WithDefaultAction(TransformationAction.Redact)
            .WithUnknownEntityAction(TransformationAction.Block)
            .WithDefaultThresholds(ConfidenceThresholds.Create(0.90, 0.70))
            .WithDefaultScope(PseudonymScope.Bundle)
            .TolerateUnresolvedUpTo(SensitivityLevel.Low);

        LowerThresholdsForCredentials(builder);

        AddRules(builder, TransformationAction.Block, s_credentials);
        AddRule(builder, TransformationAction.Block, EntityType.Security);
        AddRules(builder, TransformationAction.Pseudonymize, s_directIdentifiers);
        AddRules(builder, TransformationAction.Pseudonymize, s_internalReferences);
        AddRules(builder, TransformationAction.Redact, s_contactDetails);
        AddRules(builder, TransformationAction.Redact, s_identityDocuments);
        AddRules(builder, TransformationAction.Redact, s_financialIdentifiers);
        AddRules(builder, TransformationAction.Redact, s_specialCategories);
        AddRules(builder, TransformationAction.Redact, s_networkLocators);
        AddRule(builder, TransformationAction.Redact, EntityType.DateOfBirth);

        return builder.Build();
    }

    private static SanitizationPolicy BuildBalanced()
    {
        var builder = CreateBuilder(PolicyId.Create("balanced"), PolicyVersion.Create("1.0"))
            .WithDefaultAction(TransformationAction.Pseudonymize)
            .WithUnknownEntityAction(TransformationAction.Block)
            .WithDefaultThresholds(ConfidenceThresholds.Create(0.80, 0.60))
            .WithDefaultScope(PseudonymScope.Bundle)
            .TolerateUnresolvedUpTo(SensitivityLevel.Moderate);

        LowerThresholdsForCredentials(builder);

        AddRules(builder, TransformationAction.Block, s_credentials);
        AddRule(builder, TransformationAction.Redact, EntityType.Security);
        AddRules(builder, TransformationAction.Hash, s_identityDocuments);
        AddRules(builder, TransformationAction.Mask, s_financialIdentifiers);
        AddRules(builder, TransformationAction.Redact, s_specialCategories);
        AddRule(builder, TransformationAction.Redact, EntityType.DateOfBirth);
        AddRules(builder, TransformationAction.Pseudonymize, s_directIdentifiers);
        AddRules(builder, TransformationAction.Pseudonymize, s_contactDetails);
        AddRules(builder, TransformationAction.Pseudonymize, s_internalReferences);
        AddRules(builder, TransformationAction.Pseudonymize, s_networkLocators);

        return builder.Build();
    }

    private static void LowerThresholdsForCredentials(SanitizationPolicyBuilder builder)
    {
        // A credential found on weak evidence costs a redaction; a credential missed costs the
        // credential. The asymmetry is the reason these thresholds sit below the rest.
        var thresholds = ConfidenceThresholds.Create(0.50, 0.30);

        foreach (var type in s_credentials)
        {
            builder.WithThresholds(type, thresholds);
        }

        builder.WithThresholds(EntityType.Security, thresholds);
    }

    private static void AddRules(
        SanitizationPolicyBuilder builder,
        TransformationAction action,
        EntityType[] types)
    {
        foreach (var type in types)
        {
            AddRule(builder, action, type);
        }
    }

    private static void AddRule(
        SanitizationPolicyBuilder builder,
        TransformationAction action,
        EntityType type) =>
        builder.AddRule(PolicyRule.For(type, action));
}
