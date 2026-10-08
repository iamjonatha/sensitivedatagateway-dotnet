using System;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Policies;

/// <summary>
/// One reason a plan refuses the operation.
/// </summary>
/// <remarks>
/// A violation names a reason, and the candidate it concerns when there is one. It never
/// names the value: the whole point of blocking is that the value must not travel, and an
/// explanation that reproduced it would defeat the decision it is explaining.
/// </remarks>
public sealed class PolicyViolation
{
    private PolicyViolation(
        PolicyViolationReason reason,
        EntityId? entity,
        EntityType? type,
        SensitivityLevel? sensitivity)
    {
        Reason = reason;
        Entity = entity;
        Type = type;
        Sensitivity = sensitivity;
    }

    /// <summary>
    /// Gets why the operation is refused.
    /// </summary>
    public PolicyViolationReason Reason { get; }

    /// <summary>
    /// Gets the candidate the violation concerns, when it concerns one.
    /// </summary>
    public EntityId? Entity { get; }

    /// <summary>
    /// Gets the type of that candidate, when the violation concerns one.
    /// </summary>
    public EntityType? Type { get; }

    /// <summary>
    /// Gets the sensitivity of that candidate, when the violation concerns one.
    /// </summary>
    public SensitivityLevel? Sensitivity { get; }

    /// <summary>
    /// Creates a violation about one candidate.
    /// </summary>
    /// <param name="reason">Why the operation is refused.</param>
    /// <param name="entity">The candidate the violation concerns.</param>
    /// <param name="type">The type of the candidate.</param>
    /// <param name="sensitivity">The sensitivity of the candidate.</param>
    /// <returns>The violation.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="reason"/> is not a defined value or does not concern a candidate, an
    /// identifier is the default value, or <paramref name="sensitivity"/> is undefined.
    /// </exception>
    public static PolicyViolation ForEntity(
        PolicyViolationReason reason,
        EntityId entity,
        EntityType type,
        SensitivityLevel sensitivity)
    {
        if (reason == PolicyViolationReason.IncompleteScan || !Enum.IsDefined(reason))
        {
            throw new ArgumentException("The reason does not describe a single candidate.", nameof(reason));
        }

        if (entity.IsDefault)
        {
            throw new ArgumentException("The entity identifier carries no value.", nameof(entity));
        }

        if (type.IsDefault)
        {
            throw new ArgumentException("The entity type carries no value.", nameof(type));
        }

        if (!Enum.IsDefined(sensitivity))
        {
            throw new ArgumentException("The sensitivity level is not a defined value.", nameof(sensitivity));
        }

        return new PolicyViolation(reason, entity, type, sensitivity);
    }

    /// <summary>
    /// Creates the violation raised when part of the bundle was not read or not scanned.
    /// </summary>
    /// <returns>The violation.</returns>
    public static PolicyViolation IncompleteScan() =>
        new(PolicyViolationReason.IncompleteScan, entity: null, type: null, sensitivity: null);

    /// <summary>
    /// Describes the violation as a content-free error.
    /// </summary>
    /// <returns>
    /// An error categorized as <see cref="GatewayErrorCategory.PolicyViolation"/>, naming the
    /// candidate when the violation concerns one.
    /// </returns>
    /// <remarks>
    /// The error is a description, not a failure. Nothing throws it: a block is a result the
    /// caller reads.
    /// </remarks>
    public GatewayError ToError()
    {
        var error = GatewayError.Create(GatewayErrorCategory.PolicyViolation, Code(), Description());

        return Entity is { } entity ? error.ForEntity(entity) : error;
    }

    /// <summary>
    /// Returns a description of the violation.
    /// </summary>
    /// <returns>The reason and the type it concerns. It carries no document content.</returns>
    public override string ToString() =>
        Type is { } type ? $"{Reason} ({type})" : Reason.ToString();

    private string Code() => Reason switch
    {
        PolicyViolationReason.BlockedEntity => "policy.blocked_entity",
        PolicyViolationReason.ReviewRequired => "policy.review_required",
        PolicyViolationReason.UnresolvedEntity => "policy.unresolved_entity",
        _ => "policy.incomplete_scan",
    };

    private string Description() => Reason switch
    {
        PolicyViolationReason.BlockedEntity =>
            "The policy blocks documents that contain a value of this type.",
        PolicyViolationReason.ReviewRequired =>
            "The policy requires a person inside the trust boundary to review this value.",
        PolicyViolationReason.UnresolvedEntity =>
            "A candidate was not resolved and its sensitivity exceeds what the policy tolerates.",
        _ =>
            "Part of the bundle was not read or not scanned, and the policy requires a complete pass.",
    };
}
