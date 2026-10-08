using System;
using System.Collections.Generic;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Policies;

/// <summary>
/// The form of <see cref="SanitizationPolicy"/> that configuration binds to.
/// </summary>
/// <remarks>
/// <para>
/// A policy is an operational document: it is reviewed, approved and changed by people who
/// do not rebuild the application. This type is what those people edit, and
/// <see cref="ToPolicy"/> is the single place where their text becomes a policy object.
/// </para>
/// <para>
/// Binding is ordinary property binding. The library never reflects over policy types to
/// discover behaviour, so what a description can express is exactly what is written here.
/// </para>
/// </remarks>
public sealed class SanitizationPolicyDescription
{
    /// <summary>
    /// The value of <see cref="UnresolvedTolerance"/> that tolerates no unresolved candidate.
    /// </summary>
    public const string NoTolerance = "None";

    /// <summary>
    /// Gets or sets the identifier of the policy.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the revision of the policy.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets what happens to a candidate whose type the policy mentions but whose
    /// rules do not match.
    /// </summary>
    public TransformationAction? DefaultAction { get; set; }

    /// <summary>
    /// Gets or sets what happens to a candidate whose type the policy does not mention.
    /// </summary>
    public TransformationAction? UnknownEntityAction { get; set; }

    /// <summary>
    /// Gets or sets how far a replacement stays correlatable when a rule states no scope.
    /// </summary>
    public PseudonymScope? DefaultScope { get; set; }

    /// <summary>
    /// Gets or sets the thresholds applied to an entity type the policy states no others for.
    /// </summary>
    public ConfidenceThresholdsDescription? DefaultThresholds { get; set; }

    /// <summary>
    /// Gets or sets the highest sensitivity an unresolved candidate may carry without
    /// blocking.
    /// </summary>
    /// <value>
    /// The name of a <see cref="SensitivityLevel"/>, or <see cref="NoTolerance"/>. An absent
    /// value tolerates nothing, so omitting this never loosens the policy.
    /// </value>
    public string? UnresolvedTolerance { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether every part must have been read and scanned.
    /// </summary>
    public bool? RequireCompleteScan { get; set; }

    /// <summary>
    /// Gets or sets the size, count and time bounds of an operation under this policy.
    /// </summary>
    public ProcessingLimitsDescription? Limits { get; set; }

    /// <summary>
    /// Gets the rules, in the order they are evaluated.
    /// </summary>
    public IList<PolicyRuleDescription> Rules { get; } = new List<PolicyRuleDescription>();

    /// <summary>
    /// Gets the thresholds stated per entity type, keyed by the entity type name.
    /// </summary>
    public IDictionary<string, ConfidenceThresholdsDescription> Thresholds { get; } =
        new Dictionary<string, ConfidenceThresholdsDescription>(StringComparer.Ordinal);

    /// <summary>
    /// Produces the policy this description denotes.
    /// </summary>
    /// <returns>The policy.</returns>
    /// <exception cref="PolicyConfigurationException">
    /// The description is incomplete, names something the library does not define, or states
    /// a rule that an earlier rule makes unreachable.
    /// </exception>
    public SanitizationPolicy ToPolicy()
    {
        var builder = SanitizationPolicy.CreateBuilder(ReadId(), ReadVersion())
            .WithDefaultAction(ReadAction(DefaultAction, TransformationAction.Block))
            .WithUnknownEntityAction(ReadAction(UnknownEntityAction, TransformationAction.Block))
            .WithDefaultScope(ReadScope())
            .RequireCompleteScan(RequireCompleteScan ?? true);

        ApplyTolerance(builder);
        ApplyThresholds(builder);
        ApplyRules(builder);

        if (Limits is { } limits)
        {
            builder.WithLimits(limits.ToLimits());
        }

        return builder.Build();
    }

    private static TransformationAction ReadAction(TransformationAction? value, TransformationAction fallback)
    {
        if (value is not { } action)
        {
            return fallback;
        }

        if (!Enum.IsDefined(action))
        {
            throw PolicyConfigurationFailure.Invalid(
                "policy.action_invalid",
                "The policy names a transformation action the library does not define.");
        }

        return action;
    }

    private PolicyId ReadId()
    {
        if (!PolicyId.TryCreate(Id, out var id))
        {
            throw PolicyConfigurationFailure.Invalid(
                "policy.id_invalid",
                "A policy must carry a non-empty identifier of at most 128 characters, free of control characters.");
        }

        return id;
    }

    private PolicyVersion ReadVersion()
    {
        if (!PolicyVersion.TryCreate(Version, out var version))
        {
            throw PolicyConfigurationFailure.Invalid(
                "policy.version_invalid",
                "A policy must carry a non-empty version of at most 128 characters, free of control characters.");
        }

        return version;
    }

    private PseudonymScope ReadScope()
    {
        if (DefaultScope is not { } scope)
        {
            return PseudonymScope.Bundle;
        }

        if (!Enum.IsDefined(scope))
        {
            throw PolicyConfigurationFailure.Invalid(
                "policy.scope_invalid",
                "The policy names a pseudonym scope the library does not define.");
        }

        return scope;
    }

    private void ApplyTolerance(SanitizationPolicyBuilder builder)
    {
        var value = UnresolvedTolerance?.Trim();

        if (string.IsNullOrEmpty(value) || string.Equals(value, NoTolerance, StringComparison.OrdinalIgnoreCase))
        {
            builder.TolerateNoUnresolved();
            return;
        }

        if (!Enum.TryParse<SensitivityLevel>(value, ignoreCase: true, out var level) || !Enum.IsDefined(level))
        {
            throw PolicyConfigurationFailure.Invalid(
                "policy.tolerance_invalid",
                "The unresolved tolerance must name a sensitivity level the library defines, or None.");
        }

        builder.TolerateUnresolvedUpTo(level);
    }

    private void ApplyThresholds(SanitizationPolicyBuilder builder)
    {
        if (DefaultThresholds is { } defaults)
        {
            builder.WithDefaultThresholds(defaults.ToThresholds());
        }

        foreach (var entry in Thresholds)
        {
            if (!EntityType.TryCreate(entry.Key, out var type) || entry.Value is null)
            {
                throw PolicyConfigurationFailure.Invalid(
                    "policy.thresholds_entry_invalid",
                    "Thresholds must be stated against an entity type that begins with a letter and continues with letters, digits and underscores.");
            }

            builder.WithThresholds(type, entry.Value.ToThresholds());
        }
    }

    private void ApplyRules(SanitizationPolicyBuilder builder)
    {
        foreach (var rule in Rules)
        {
            if (rule is null)
            {
                throw PolicyConfigurationFailure.Invalid(
                    "policy.rule_missing",
                    "The policy holds a rule entry with nothing in it.");
            }

            builder.AddRule(rule.ToRule());
        }
    }
}
