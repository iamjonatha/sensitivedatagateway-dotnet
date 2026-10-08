using System;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Policies;

/// <summary>
/// What the policy decided about one candidate, and why.
/// </summary>
/// <remarks>
/// A decision is data. It names a candidate by identifier, states an action and carries the
/// rationale that produced it, so that a plan can be inspected, counted and asserted against
/// before a single character of text has been rewritten.
/// </remarks>
public sealed class PolicyDecision
{
    private PolicyDecision(
        EntityId entity,
        EntityType type,
        TransformationAction action,
        PseudonymScope? scope,
        PolicyRationale rationale)
    {
        Entity = entity;
        Type = type;
        Action = action;
        Scope = scope;
        Rationale = rationale;
    }

    /// <summary>
    /// Gets the candidate the decision is about.
    /// </summary>
    public EntityId Entity { get; }

    /// <summary>
    /// Gets the type of that candidate.
    /// </summary>
    /// <remarks>
    /// The type is repeated here so that a plan is readable without the document it was
    /// produced from, which is what an audit record keeps.
    /// </remarks>
    public EntityType Type { get; }

    /// <summary>
    /// Gets what happens to the candidate.
    /// </summary>
    public TransformationAction Action { get; }

    /// <summary>
    /// Gets how far the replacement stays correlatable.
    /// </summary>
    /// <value>
    /// The resolved scope when <see cref="Action"/> produces a correlatable replacement;
    /// otherwise <see langword="null"/>.
    /// </value>
    /// <remarks>
    /// The scope is resolved here, not left to the stage that applies the plan. An anonymizer
    /// that had to pick a default would be making a policy decision under another name.
    /// </remarks>
    public PseudonymScope? Scope { get; }

    /// <summary>
    /// Gets why the decision is what it is.
    /// </summary>
    public PolicyRationale Rationale { get; }

    /// <summary>
    /// Creates a decision.
    /// </summary>
    /// <param name="entity">The candidate the decision is about.</param>
    /// <param name="type">The type of that candidate.</param>
    /// <param name="action">What happens to it.</param>
    /// <param name="scope">The resolved scope, or <see langword="null"/>.</param>
    /// <param name="rationale">Why the decision is what it is.</param>
    /// <returns>The decision.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="rationale"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// An identifier is the default value, an enumeration argument is undefined, or a scope
    /// is stated for an action that produces no correlatable replacement.
    /// </exception>
    public static PolicyDecision Create(
        EntityId entity,
        EntityType type,
        TransformationAction action,
        PseudonymScope? scope,
        PolicyRationale rationale)
    {
        ArgumentNullException.ThrowIfNull(rationale);

        if (entity.IsDefault)
        {
            throw new ArgumentException("The entity identifier carries no value.", nameof(entity));
        }

        if (type.IsDefault)
        {
            throw new ArgumentException("The entity type carries no value.", nameof(type));
        }

        if (!Enum.IsDefined(action))
        {
            throw new ArgumentException("The transformation action is not a defined value.", nameof(action));
        }

        ValidateScope(action, scope);

        return new PolicyDecision(entity, type, action, scope, rationale);
    }

    /// <summary>
    /// Returns a description of the decision.
    /// </summary>
    /// <returns>The type, the action and the rationale. It carries no document content.</returns>
    public override string ToString() => $"{Type} -> {Action} ({Rationale})";

    private static void ValidateScope(TransformationAction action, PseudonymScope? scope)
    {
        var correlates = action is TransformationAction.Pseudonymize or TransformationAction.Hash;

        if (scope is { } value && !Enum.IsDefined(value))
        {
            throw new ArgumentException("The pseudonym scope is not a defined value.", nameof(scope));
        }

        if (scope is not null && !correlates)
        {
            throw new ArgumentException(
                "A scope applies only to an action that produces a correlatable replacement.",
                nameof(scope));
        }

        if (scope is null && correlates)
        {
            throw new ArgumentException(
                "An action that produces a correlatable replacement must state its scope.",
                nameof(scope));
        }
    }
}
