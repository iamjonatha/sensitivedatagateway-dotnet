using System;
using System.Collections.Generic;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway;

/// <summary>
/// Turns a pipeline run into the counts a reviewer may see.
/// </summary>
/// <remarks>
/// Every projection here is lossy on purpose. A count of credit cards is useful; the cards
/// are not, and neither are the offsets that would find them again.
/// </remarks>
internal static class Inspections
{
    internal static InspectionReport From(PipelineRun run, DateTimeOffset inspectedAt) =>
        new()
        {
            BundleId = run.Detected.BundleId,
            InspectedAt = inspectedAt,
            Provenance = run.Provenance,
            WouldBeAllowed = run.Result.IsAllowed,
            Status = run.Sanitized?.Status,
            SegmentCount = run.Extracted.Segments.Count,
            TotalCharacters = run.Extracted.TotalCharacters,
            ExtractionFailureCount = run.Extracted.Failures.Count,
            DetectionFailureCount = run.Detected.Failures.Count,
            EntityCounts = CountByType(run.Detected),
            ActionCounts = CountByAction(run.Plan),
            UnresolvedCount = run.Plan.Unresolved.Count,
            BlockingReasons = ReasonsOf(run.Plan),
            FailedChecks = FailedChecksOf(run.Result.Report),
        };

    internal static List<PrivacyCheck> FailedChecksOf(PrivacyValidationReport report)
    {
        var checks = new List<PrivacyCheck>();

        foreach (var finding in report.Findings)
        {
            if (!checks.Contains(finding.Check))
            {
                checks.Add(finding.Check);
            }
        }

        return checks;
    }

    private static Dictionary<EntityType, int> CountByType(DetectedDocument detected)
    {
        var counts = new Dictionary<EntityType, int>();

        foreach (var entity in detected.Entities)
        {
            counts[entity.Type] = counts.TryGetValue(entity.Type, out var count) ? count + 1 : 1;
        }

        return counts;
    }

    private static Dictionary<TransformationAction, int> CountByAction(TransformationPlan plan)
    {
        var counts = new Dictionary<TransformationAction, int>();

        foreach (var decision in plan.Decisions)
        {
            counts[decision.Action] = counts.TryGetValue(decision.Action, out var count) ? count + 1 : 1;
        }

        return counts;
    }

    private static List<PolicyViolationReason> ReasonsOf(TransformationPlan plan)
    {
        var reasons = new List<PolicyViolationReason>();

        foreach (var violation in plan.Blocking)
        {
            if (!reasons.Contains(violation.Reason))
            {
                reasons.Add(violation.Reason);
            }
        }

        return reasons;
    }
}
