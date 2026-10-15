using System;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Policies;

namespace SensitiveDataGateway.Testing;

/// <summary>
/// Policies written for tests rather than for production.
/// </summary>
/// <remarks>
/// The shipped policies block credentials, so a test that only ever uses them never gets
/// far enough to find out what a provider would have received. The policies here are
/// deliberately more permissive than anything that should run in production, because the
/// interesting question is what survives when the gateway is told to let a document
/// through, not what survives when it is told to stop.
/// </remarks>
public static class TestPolicy
{
    private static readonly PolicyId s_testPolicyId = PolicyId.Create("test");
    private static readonly PolicyVersion s_testPolicyVersion = PolicyVersion.Create("1.0.0");

    /// <summary>
    /// Gets a policy that transforms everything it finds and blocks nothing.
    /// </summary>
    /// <remarks>
    /// Every entity is pseudonymized, unknown types included, and an unresolved candidate
    /// is tolerated at any sensitivity. This is the worst case for the egress boundary: the
    /// gateway has been given every reason to let the document through, so anything that
    /// still leaves in clear is a defect in sanitization and not a policy decision.
    /// </remarks>
    public static SanitizationPolicy Permissive { get; } = CreateBuilder()
        .WithDefaultAction(TransformationAction.Pseudonymize)
        .WithUnknownEntityAction(TransformationAction.Pseudonymize)
        .TolerateUnresolvedUpTo(SensitivityLevel.Critical)
        .Build();

    /// <summary>
    /// Gets a policy that blocks every document in which anything at all was found.
    /// </summary>
    public static SanitizationPolicy Blocking { get; } = CreateBuilder()
        .WithDefaultAction(TransformationAction.Block)
        .WithUnknownEntityAction(TransformationAction.Block)
        .Build();

    /// <summary>
    /// Creates a builder with the test identifier and version already set.
    /// </summary>
    /// <returns>The builder.</returns>
    public static SanitizationPolicyBuilder CreateBuilder() =>
        SanitizationPolicy.CreateBuilder(s_testPolicyId, s_testPolicyVersion);

    /// <summary>
    /// Creates a copy of <see cref="Permissive"/> under different processing limits.
    /// </summary>
    /// <param name="limits">The limits to apply.</param>
    /// <returns>The policy.</returns>
    public static SanitizationPolicy Within(ProcessingLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        return CreateBuilder()
            .WithDefaultAction(TransformationAction.Pseudonymize)
            .WithUnknownEntityAction(TransformationAction.Pseudonymize)
            .TolerateUnresolvedUpTo(SensitivityLevel.Critical)
            .WithLimits(limits)
            .Build();
    }
}
