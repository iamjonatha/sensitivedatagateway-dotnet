using System;
using System.Collections.Generic;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Policies;

/// <summary>
/// Decides what happens to what the detectors found.
/// </summary>
/// <remarks>
/// <para>
/// The engine reads an entity's type, sensitivity and confidence, and nothing else. It never
/// touches the text an entity was found in, and the plan it produces carries no content, so
/// a plan can be logged, stored and reviewed in full.
/// </para>
/// <para>
/// The engine decides; it does not act. Nothing here rewrites a document. That is the next
/// stage's work, and keeping the two apart is what makes a decision reviewable before it has
/// any effect.
/// </para>
/// <para>
/// Instances are stateless and safe to share between threads.
/// </para>
/// </remarks>
public sealed class PolicyEngine : IPolicyEngine
{
    /// <inheritdoc />
    public TransformationPlan Evaluate(DetectedDocument document, SanitizationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(policy);

        RequireEntityCountWithinLimit(document, policy);

        var evaluation = new Evaluation(policy, document.Entities.Count);

        if (policy.RequiresCompleteScan && !document.IsComplete)
        {
            evaluation.Blocking.Add(PolicyViolation.IncompleteScan());
        }

        foreach (var entity in document.Entities)
        {
            Resolve(entity, evaluation);
        }

        return TransformationPlan.Create(
            document.BundleId,
            policy.Id,
            policy.Version,
            evaluation.Decisions,
            evaluation.Unresolved,
            evaluation.Blocking);
    }

    private static void RequireEntityCountWithinLimit(DetectedDocument document, SanitizationPolicy policy)
    {
        if (document.Entities.Count <= policy.Limits.MaxEntities)
        {
            return;
        }

        throw new ProcessingLimitExceededException(GatewayError.Create(
            GatewayErrorCategory.LimitExceeded,
            "policy.too_many_entities",
            "The detectors reported more entities than the policy allows one operation to carry.")
            .InBundle(document.BundleId));
    }

    private static void Resolve(SensitiveEntity entity, Evaluation evaluation)
    {
        var policy = evaluation.Policy;
        var thresholds = policy.ThresholdsFor(entity.Type);
        var outcome = thresholds.Classify(entity.Confidence);

        if (outcome == ConfidenceOutcome.Unresolved)
        {
            LeaveUnresolved(entity, evaluation);
            return;
        }

        var decided = Decide(entity, policy, thresholds, outcome);

        evaluation.Decisions.Add(PolicyDecision.Create(
            entity.Id,
            entity.Type,
            decided.Action,
            decided.Scope,
            decided.Rationale));

        if (BlockingReason(decided.Action) is { } reason)
        {
            evaluation.Blocking.Add(
                PolicyViolation.ForEntity(reason, entity.Id, entity.Type, entity.Sensitivity));
        }
    }

    private static void LeaveUnresolved(SensitiveEntity entity, Evaluation evaluation)
    {
        evaluation.Unresolved.Add(entity);

        if (evaluation.Policy.ToleratesUnresolved(entity.Sensitivity))
        {
            return;
        }

        evaluation.Blocking.Add(PolicyViolation.ForEntity(
            PolicyViolationReason.UnresolvedEntity,
            entity.Id,
            entity.Type,
            entity.Sensitivity));
    }

    private static (TransformationAction Action, PseudonymScope? Scope, PolicyRationale Rationale) Decide(
        SensitiveEntity entity,
        SanitizationPolicy policy,
        ConfidenceThresholds thresholds,
        ConfidenceOutcome outcome)
    {
        if (policy.TryFindRule(entity, out var ruleIndex))
        {
            var rule = policy.Rules[ruleIndex];

            return (
                rule.Action,
                ScopeFor(rule.Action, rule.Scope, policy),
                PolicyRationale.FromRule(ruleIndex, outcome, thresholds, entity.Confidence));
        }

        if (policy.Mentions(entity.Type))
        {
            return (
                policy.DefaultAction,
                ScopeFor(policy.DefaultAction, ruleScope: null, policy),
                PolicyRationale.FromDefaultAction(outcome, thresholds, entity.Confidence));
        }

        return (
            policy.UnknownEntityAction,
            ScopeFor(policy.UnknownEntityAction, ruleScope: null, policy),
            PolicyRationale.FromUnknownEntityAction(outcome, thresholds, entity.Confidence));
    }

    private static PseudonymScope? ScopeFor(
        TransformationAction action,
        PseudonymScope? ruleScope,
        SanitizationPolicy policy) =>
        action is TransformationAction.Pseudonymize or TransformationAction.Hash
            ? ruleScope ?? policy.DefaultScope
            : null;

    private static PolicyViolationReason? BlockingReason(TransformationAction action) => action switch
    {
        TransformationAction.Block => PolicyViolationReason.BlockedEntity,
        TransformationAction.RequireReview => PolicyViolationReason.ReviewRequired,
        _ => null,
    };

    private sealed class Evaluation(SanitizationPolicy policy, int capacity)
    {
        public SanitizationPolicy Policy { get; } = policy;

        public List<PolicyDecision> Decisions { get; } = new(capacity);

        public List<SensitiveEntity> Unresolved { get; } = [];

        public List<PolicyViolation> Blocking { get; } = [];
    }
}
