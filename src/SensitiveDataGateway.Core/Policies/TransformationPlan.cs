using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Policies;

/// <summary>
/// Everything the policy decided about one document, before any text is rewritten.
/// </summary>
/// <remarks>
/// <para>
/// The plan is data. It can be inspected, counted, logged in aggregate, serialized into an
/// audit record and asserted against in a test, and none of that requires the document it was
/// produced from. That is what makes the decision reviewable separately from its execution.
/// </para>
/// <para>
/// The plan carries no text. A candidate appears as an identifier, a type and a location, and
/// the words at that location stay in the document the anonymizer reads.
/// </para>
/// </remarks>
public sealed class TransformationPlan
{
    private TransformationPlan(
        BundleId bundleId,
        PolicyId policyId,
        PolicyVersion policyVersion,
        IReadOnlyList<PolicyDecision> decisions,
        IReadOnlyList<SensitiveEntity> unresolved,
        IReadOnlyList<PolicyViolation> blocking)
    {
        BundleId = bundleId;
        PolicyId = policyId;
        PolicyVersion = policyVersion;
        Decisions = decisions;
        Unresolved = unresolved;
        Blocking = blocking;
    }

    /// <summary>
    /// Gets the processing operation this plan belongs to.
    /// </summary>
    public BundleId BundleId { get; }

    /// <summary>
    /// Gets the policy that produced the plan.
    /// </summary>
    public PolicyId PolicyId { get; }

    /// <summary>
    /// Gets the revision of that policy.
    /// </summary>
    /// <remarks>
    /// The pair of identifier and version is what makes a sanitization reproducible: the same
    /// document under the same version yields the same plan.
    /// </remarks>
    public PolicyVersion PolicyVersion { get; }

    /// <summary>
    /// Gets what happens to each candidate the policy resolved.
    /// </summary>
    public IReadOnlyList<PolicyDecision> Decisions { get; }

    /// <summary>
    /// Gets the candidates the policy did not resolve.
    /// </summary>
    /// <remarks>
    /// An unresolved candidate has no decision, so its text is left alone. The list is the
    /// honest record of what the gateway knows it did not handle.
    /// </remarks>
    public IReadOnlyList<SensitiveEntity> Unresolved { get; }

    /// <summary>
    /// Gets the reasons the operation is refused.
    /// </summary>
    public IReadOnlyList<PolicyViolation> Blocking { get; }

    /// <summary>
    /// Gets a value indicating whether the policy refuses the operation.
    /// </summary>
    /// <remarks>
    /// A blocked plan is never partially applied. There is no sanitized version of a document
    /// the policy refused.
    /// </remarks>
    public bool IsBlocked => Blocking.Count > 0;

    /// <summary>
    /// Creates a plan.
    /// </summary>
    /// <param name="bundleId">The processing operation the plan belongs to.</param>
    /// <param name="policyId">The policy that produced it.</param>
    /// <param name="policyVersion">The revision of that policy.</param>
    /// <param name="decisions">What happens to each resolved candidate. The sequence is copied.</param>
    /// <param name="unresolved">The candidates the policy did not resolve. The sequence is copied.</param>
    /// <param name="blocking">The reasons the operation is refused. The sequence is copied.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="ArgumentNullException">A sequence is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// An identifier is the default value, a sequence contains <see langword="null"/>, a
    /// candidate has both a decision and a place among the unresolved, or two decisions name
    /// the same candidate.
    /// </exception>
    public static TransformationPlan Create(
        BundleId bundleId,
        PolicyId policyId,
        PolicyVersion policyVersion,
        IEnumerable<PolicyDecision> decisions,
        IEnumerable<SensitiveEntity> unresolved,
        IEnumerable<PolicyViolation> blocking)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(unresolved);
        ArgumentNullException.ThrowIfNull(blocking);
        RequireIdentity(bundleId, policyId, policyVersion);

        var decided = new HashSet<EntityId>();
        var copiedDecisions = CopyDecisions(decisions, decided);
        var copiedUnresolved = CopyUnresolved(unresolved, decided);
        var copiedBlocking = CopyBlocking(blocking);

        return new TransformationPlan(
            bundleId,
            policyId,
            policyVersion,
            copiedDecisions,
            copiedUnresolved,
            copiedBlocking);
    }

    /// <summary>
    /// Returns a description of the plan.
    /// </summary>
    /// <returns>
    /// The policy, the number of decisions, the number of unresolved candidates and the
    /// number of blocking reasons. It carries no document content.
    /// </returns>
    public override string ToString() =>
        string.Concat(
            $"{PolicyId} {PolicyVersion}: ",
            Decisions.Count.ToString(CultureInfo.InvariantCulture),
            " decisions, ",
            Unresolved.Count.ToString(CultureInfo.InvariantCulture),
            " unresolved, ",
            Blocking.Count.ToString(CultureInfo.InvariantCulture),
            " blocking");

    private static void RequireIdentity(BundleId bundleId, PolicyId policyId, PolicyVersion policyVersion)
    {
        if (bundleId.IsDefault)
        {
            throw new ArgumentException("The bundle identifier carries no value.", nameof(bundleId));
        }

        if (policyId.IsDefault)
        {
            throw new ArgumentException("The policy identifier carries no value.", nameof(policyId));
        }

        if (policyVersion.IsDefault)
        {
            throw new ArgumentException("The policy version carries no value.", nameof(policyVersion));
        }
    }

    private static ReadOnlyCollection<PolicyDecision> CopyDecisions(
        IEnumerable<PolicyDecision> decisions,
        HashSet<EntityId> decided)
    {
        var copied = new List<PolicyDecision>();

        foreach (var decision in decisions)
        {
            if (decision is null)
            {
                throw new ArgumentException("A decision list cannot contain a null decision.", nameof(decisions));
            }

            if (!decided.Add(decision.Entity))
            {
                throw new ArgumentException("Two decisions name the same candidate.", nameof(decisions));
            }

            copied.Add(decision);
        }

        return new ReadOnlyCollection<PolicyDecision>(copied);
    }

    private static ReadOnlyCollection<SensitiveEntity> CopyUnresolved(
        IEnumerable<SensitiveEntity> unresolved,
        HashSet<EntityId> decided)
    {
        var copied = new List<SensitiveEntity>();

        foreach (var entity in unresolved)
        {
            if (entity is null)
            {
                throw new ArgumentException("An entity list cannot contain a null entity.", nameof(unresolved));
            }

            if (decided.Contains(entity.Id))
            {
                throw new ArgumentException(
                    "A candidate cannot be both decided and unresolved.",
                    nameof(unresolved));
            }

            copied.Add(entity);
        }

        return new ReadOnlyCollection<SensitiveEntity>(copied);
    }

    private static ReadOnlyCollection<PolicyViolation> CopyBlocking(IEnumerable<PolicyViolation> blocking)
    {
        var copied = new List<PolicyViolation>();

        foreach (var violation in blocking)
        {
            if (violation is null)
            {
                throw new ArgumentException("A violation list cannot contain a null violation.", nameof(blocking));
            }

            copied.Add(violation);
        }

        return new ReadOnlyCollection<PolicyViolation>(copied);
    }
}
