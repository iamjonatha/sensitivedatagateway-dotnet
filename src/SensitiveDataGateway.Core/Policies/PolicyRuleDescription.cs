using System;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Policies;

/// <summary>
/// The form of <see cref="PolicyRule"/> that configuration binds to.
/// </summary>
/// <remarks>
/// The entity type is text here, because configuration does not know the library's value
/// types and because the taxonomy is open: a rule may name a category this library has never
/// heard of.
/// </remarks>
public sealed class PolicyRuleDescription
{
    /// <summary>
    /// Gets or sets the name of the entity type the rule governs.
    /// </summary>
    public string? Match { get; set; }

    /// <summary>
    /// Gets or sets what happens to a candidate the rule matches.
    /// </summary>
    public TransformationAction? Action { get; set; }

    /// <summary>
    /// Gets or sets how far the replacement stays correlatable.
    /// </summary>
    public PseudonymScope? Scope { get; set; }

    /// <summary>
    /// Gets or sets the sensitivity a candidate must carry for the rule to match.
    /// </summary>
    public SensitivityLevel? Sensitivity { get; set; }

    /// <summary>
    /// Describes a rule.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The description.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rule"/> is <see langword="null"/>.</exception>
    public static PolicyRuleDescription FromRule(PolicyRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        return new PolicyRuleDescription
        {
            Match = rule.Match.Value,
            Action = rule.Action,
            Scope = rule.Scope,
            Sensitivity = rule.Sensitivity,
        };
    }

    /// <summary>
    /// Produces the rule this description denotes.
    /// </summary>
    /// <returns>The rule.</returns>
    /// <exception cref="PolicyConfigurationException">
    /// The entity type is missing or malformed, the action is missing or undefined, the
    /// sensitivity is undefined, or a scope is stated for an action that produces no
    /// correlatable replacement.
    /// </exception>
    public PolicyRule ToRule()
    {
        if (!EntityType.TryCreate(Match, out var match))
        {
            throw PolicyConfigurationFailure.Invalid(
                "policy.rule_type_invalid",
                "A rule must name an entity type that begins with a letter and continues with letters, digits and underscores.");
        }

        var rule = PolicyRule.For(match, ReadAction());

        if (Sensitivity is { } sensitivity)
        {
            rule = rule.WhenSensitivityIs(Defined(sensitivity));
        }

        return Scope is { } scope ? ApplyScope(rule, scope) : rule;
    }

    private static PolicyRule ApplyScope(PolicyRule rule, PseudonymScope scope)
    {
        if (!Enum.IsDefined(scope))
        {
            throw PolicyConfigurationFailure.Invalid(
                "policy.rule_scope_invalid",
                "A rule names a pseudonym scope the library does not define.");
        }

        if (rule.Action is not (TransformationAction.Pseudonymize or TransformationAction.Hash))
        {
            throw PolicyConfigurationFailure.Invalid(
                "policy.rule_scope_without_correlation",
                "A scope applies only to an action that produces a correlatable replacement.");
        }

        return rule.WithScope(scope);
    }

    private static SensitivityLevel Defined(SensitivityLevel sensitivity)
    {
        if (!Enum.IsDefined(sensitivity))
        {
            throw PolicyConfigurationFailure.Invalid(
                "policy.rule_sensitivity_invalid",
                "A rule names a sensitivity level the library does not define.");
        }

        return sensitivity;
    }

    private TransformationAction ReadAction()
    {
        if (Action is not { } action || !Enum.IsDefined(action))
        {
            throw PolicyConfigurationFailure.Invalid(
                "policy.rule_action_invalid",
                "A rule must name a transformation action the library defines.");
        }

        return action;
    }
}
