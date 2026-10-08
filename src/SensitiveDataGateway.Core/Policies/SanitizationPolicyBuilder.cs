using System;
using System.Collections.Generic;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Policies;

/// <summary>
/// Builds a <see cref="SanitizationPolicy"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every default is the closed one. A policy built without a single call still blocks
/// anything it does not recognize, resolves nothing on weak evidence, tolerates no unresolved
/// candidate and demands a complete scan. Forgetting to configure something therefore makes
/// the gateway stricter, never more permissive.
/// </para>
/// <para>
/// The builder is not thread-safe and is meant to be used and discarded.
/// </para>
/// </remarks>
public sealed class SanitizationPolicyBuilder
{
    internal SanitizationPolicyBuilder(PolicyId id, PolicyVersion version)
    {
        if (id.IsDefault)
        {
            throw new ArgumentException("The policy identifier carries no value.", nameof(id));
        }

        if (version.IsDefault)
        {
            throw new ArgumentException("The policy version carries no value.", nameof(version));
        }

        Id = id;
        Version = version;
    }

    internal PolicyId Id { get; }

    internal PolicyVersion Version { get; }

    internal TransformationAction DefaultAction { get; private set; } = TransformationAction.Block;

    internal TransformationAction UnknownEntityAction { get; private set; } = TransformationAction.Block;

    internal ConfidenceThresholds DefaultThresholds { get; private set; } = ConfidenceThresholds.Default;

    internal PseudonymScope DefaultScope { get; private set; } = PseudonymScope.Bundle;

    internal ProcessingLimits Limits { get; private set; } = ProcessingLimits.Default;

    internal SensitivityLevel? UnresolvedTolerance { get; private set; }

    internal bool RequiresCompleteScan { get; private set; } = true;

    internal List<PolicyRule> Rules { get; } = new();

    internal Dictionary<EntityType, ConfidenceThresholds> Thresholds { get; } = new();

    /// <summary>
    /// States what happens to a candidate whose type the policy mentions but whose rules do
    /// not match.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="action"/> is not a defined value.</exception>
    public SanitizationPolicyBuilder WithDefaultAction(TransformationAction action)
    {
        DefaultAction = Defined(action);
        return this;
    }

    /// <summary>
    /// States what happens to a candidate whose type the policy does not mention.
    /// </summary>
    /// <param name="action">The action.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="action"/> is not a defined value.</exception>
    /// <remarks>
    /// Setting anything other than <see cref="TransformationAction.Block"/> here is a
    /// deliberate decision to let an unrecognized category through. It is allowed, it is
    /// recorded in the plan's rationale, and it is visible in review.
    /// </remarks>
    public SanitizationPolicyBuilder WithUnknownEntityAction(TransformationAction action)
    {
        UnknownEntityAction = Defined(action);
        return this;
    }

    /// <summary>
    /// States the thresholds that apply to an entity type the policy states no others for.
    /// </summary>
    /// <param name="thresholds">The thresholds.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="thresholds"/> is <see langword="null"/>.</exception>
    public SanitizationPolicyBuilder WithDefaultThresholds(ConfidenceThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);

        DefaultThresholds = thresholds;
        return this;
    }

    /// <summary>
    /// States the thresholds that apply to one entity type.
    /// </summary>
    /// <param name="type">The entity type.</param>
    /// <param name="thresholds">The thresholds.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="thresholds"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="type"/> is the default value.</exception>
    /// <remarks>
    /// Stating thresholds for a type also makes the policy mention it, so a type that has
    /// thresholds and no rule falls to <see cref="SanitizationPolicy.DefaultAction"/> rather
    /// than to the unknown-entity action.
    /// </remarks>
    public SanitizationPolicyBuilder WithThresholds(EntityType type, ConfidenceThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);

        if (type.IsDefault)
        {
            throw new ArgumentException("The entity type carries no value.", nameof(type));
        }

        Thresholds[type] = thresholds;
        return this;
    }

    /// <summary>
    /// States how far a replacement stays correlatable when a rule does not state a scope.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="scope"/> is not a defined value.</exception>
    public SanitizationPolicyBuilder WithDefaultScope(PseudonymScope scope)
    {
        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentException("The pseudonym scope is not a defined value.", nameof(scope));
        }

        DefaultScope = scope;
        return this;
    }

    /// <summary>
    /// Adds a rule, after every rule already added.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rule"/> is <see langword="null"/>.</exception>
    public SanitizationPolicyBuilder AddRule(PolicyRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        Rules.Add(rule);
        return this;
    }

    /// <summary>
    /// States the size, count and time bounds an operation under this policy must stay
    /// inside.
    /// </summary>
    /// <param name="limits">The limits.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="limits"/> is <see langword="null"/>.</exception>
    public SanitizationPolicyBuilder WithLimits(ProcessingLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        Limits = limits;
        return this;
    }

    /// <summary>
    /// States the highest sensitivity an unresolved candidate may carry without blocking.
    /// </summary>
    /// <param name="sensitivity">The tolerated level.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentException"><paramref name="sensitivity"/> is not a defined value.</exception>
    public SanitizationPolicyBuilder TolerateUnresolvedUpTo(SensitivityLevel sensitivity)
    {
        if (!Enum.IsDefined(sensitivity))
        {
            throw new ArgumentException("The sensitivity level is not a defined value.", nameof(sensitivity));
        }

        UnresolvedTolerance = sensitivity;
        return this;
    }

    /// <summary>
    /// States that no unresolved candidate may be transmitted.
    /// </summary>
    /// <returns>The same builder.</returns>
    public SanitizationPolicyBuilder TolerateNoUnresolved()
    {
        UnresolvedTolerance = null;
        return this;
    }

    /// <summary>
    /// States whether every part of the bundle must have been read and scanned.
    /// </summary>
    /// <param name="required">Whether a complete pass is required.</param>
    /// <returns>The same builder.</returns>
    public SanitizationPolicyBuilder RequireCompleteScan(bool required)
    {
        RequiresCompleteScan = required;
        return this;
    }

    /// <summary>
    /// Validates the policy and builds it.
    /// </summary>
    /// <returns>The policy.</returns>
    /// <exception cref="PolicyConfigurationException">
    /// A rule repeats or is made unreachable by an earlier one.
    /// </exception>
    /// <remarks>
    /// A rule that can never match is refused rather than ignored. In a privacy policy an
    /// ignored rule reads, to everyone but the compiler, as a protection that is in place.
    /// </remarks>
    public SanitizationPolicy Build()
    {
        RequireReachableRules();

        return new SanitizationPolicy(this);
    }

    private static TransformationAction Defined(TransformationAction action)
    {
        if (!Enum.IsDefined(action))
        {
            throw new ArgumentException("The transformation action is not a defined value.", nameof(action));
        }

        return action;
    }

    private void RequireReachableRules()
    {
        for (var later = 1; later < Rules.Count; later++)
        {
            for (var earlier = 0; earlier < later; earlier++)
            {
                if (Rules[earlier].Shadows(Rules[later]))
                {
                    throw new PolicyConfigurationException(GatewayError.Create(
                        GatewayErrorCategory.ConfigurationInvalid,
                        "policy.unreachable_rule",
                        "A rule can never match because an earlier rule governs everything it governs."));
                }
            }
        }
    }
}
