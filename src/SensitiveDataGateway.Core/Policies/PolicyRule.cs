using System;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Policies;

/// <summary>
/// One statement of what a policy does with one kind of candidate.
/// </summary>
/// <remarks>
/// <para>
/// A rule matches on the entity type and, optionally, on the sensitivity the detector
/// assigned. A rule that states a sensitivity is more specific than one that does not, and a
/// policy refuses to hold a general rule that would make a specific one unreachable.
/// </para>
/// <para>
/// A rule names a category and an action. It never names a value, a pattern or a document,
/// which is what lets a policy be exported, reviewed and committed to a repository.
/// </para>
/// </remarks>
public sealed class PolicyRule
{
    private PolicyRule(
        EntityType match,
        TransformationAction action,
        PseudonymScope? scope,
        SensitivityLevel? sensitivity)
    {
        Match = match;
        Action = action;
        Scope = scope;
        Sensitivity = sensitivity;
    }

    /// <summary>
    /// Gets the entity type this rule governs.
    /// </summary>
    public EntityType Match { get; }

    /// <summary>
    /// Gets what happens to a candidate this rule matches.
    /// </summary>
    public TransformationAction Action { get; }

    /// <summary>
    /// Gets how far the replacement stays correlatable, when the rule states it.
    /// </summary>
    /// <value>
    /// The scope, or <see langword="null"/> when the policy's default scope applies.
    /// </value>
    /// <remarks>
    /// A scope is meaningful only for the two actions that produce a correlatable
    /// replacement, <see cref="TransformationAction.Pseudonymize"/> and
    /// <see cref="TransformationAction.Hash"/>. Setting one on any other action is rejected,
    /// because a scope that has no effect reads in review as a guarantee that was given.
    /// </remarks>
    public PseudonymScope? Scope { get; }

    /// <summary>
    /// Gets the sensitivity a candidate must carry for this rule to match.
    /// </summary>
    /// <value>
    /// The sensitivity, or <see langword="null"/> when the rule governs the type whatever the
    /// detector assigned.
    /// </value>
    public SensitivityLevel? Sensitivity { get; }

    /// <summary>
    /// Creates a rule that governs an entity type.
    /// </summary>
    /// <param name="match">The entity type the rule governs.</param>
    /// <param name="action">What happens to a candidate of that type.</param>
    /// <returns>The rule.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="match"/> is the default value, or <paramref name="action"/> is not a
    /// defined value.
    /// </exception>
    public static PolicyRule For(EntityType match, TransformationAction action)
    {
        if (match.IsDefault)
        {
            throw new ArgumentException("The entity type carries no value.", nameof(match));
        }

        if (!Enum.IsDefined(action))
        {
            throw new ArgumentException("The transformation action is not a defined value.", nameof(action));
        }

        return new PolicyRule(match, action, scope: null, sensitivity: null);
    }

    /// <summary>
    /// Returns a copy of this rule that states how far the replacement stays correlatable.
    /// </summary>
    /// <param name="scope">The scope.</param>
    /// <returns>The new rule.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="scope"/> is not a defined value, or the action does not produce a
    /// correlatable replacement.
    /// </exception>
    public PolicyRule WithScope(PseudonymScope scope)
    {
        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentException("The pseudonym scope is not a defined value.", nameof(scope));
        }

        if (Action is not (TransformationAction.Pseudonymize or TransformationAction.Hash))
        {
            throw new ArgumentException(
                "A scope applies only to an action that produces a correlatable replacement.",
                nameof(scope));
        }

        return new PolicyRule(Match, Action, scope, Sensitivity);
    }

    /// <summary>
    /// Returns a copy of this rule that matches only candidates of one sensitivity.
    /// </summary>
    /// <param name="sensitivity">The sensitivity a candidate must carry.</param>
    /// <returns>The new rule.</returns>
    /// <exception cref="ArgumentException"><paramref name="sensitivity"/> is not a defined value.</exception>
    public PolicyRule WhenSensitivityIs(SensitivityLevel sensitivity)
    {
        if (!Enum.IsDefined(sensitivity))
        {
            throw new ArgumentException("The sensitivity level is not a defined value.", nameof(sensitivity));
        }

        return new PolicyRule(Match, Action, Scope, sensitivity);
    }

    /// <summary>
    /// Determines whether this rule governs a candidate.
    /// </summary>
    /// <param name="entity">The candidate.</param>
    /// <returns>
    /// <see langword="true"/> when the types are the same and the rule either states no
    /// sensitivity or states the one the candidate carries.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="entity"/> is <see langword="null"/>.</exception>
    public bool Matches(SensitiveEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return entity.Type == Match && (Sensitivity is null || entity.Sensitivity == Sensitivity);
    }

    /// <summary>
    /// Determines whether this rule would leave another rule unreachable.
    /// </summary>
    /// <param name="other">The rule that would come after this one.</param>
    /// <returns>
    /// <see langword="true"/> when the two govern the same type and this rule matches at
    /// least everything <paramref name="other"/> matches.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    public bool Shadows(PolicyRule other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Match == other.Match && (Sensitivity is null || Sensitivity == other.Sensitivity);
    }

    /// <summary>
    /// Returns a description of the rule.
    /// </summary>
    /// <returns>The type, the sensitivity it is restricted to and the action.</returns>
    public override string ToString() =>
        Sensitivity is { } sensitivity
            ? $"{Match} ({sensitivity}) -> {Action}"
            : $"{Match} -> {Action}";
}
