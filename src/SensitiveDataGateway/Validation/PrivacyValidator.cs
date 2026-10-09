using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;

namespace SensitiveDataGateway.Validation;

/// <summary>
/// The last stage of the pipeline, and the only one entitled to produce a
/// <see cref="ValidatedPayload"/>.
/// </summary>
/// <remarks>
/// <para>
/// The validator assumes every stage before it is broken. It re-derives what it checks from
/// the original document rather than trusting the record each stage left behind, because a
/// check that reads its answer from the stage it is checking confirms the stage agrees with
/// itself.
/// </para>
/// <para>
/// There is no configuration that relaxes this. The thresholds in
/// <see cref="ValidationOptions"/> decide how hard the scan looks, never whether it looks.
/// </para>
/// </remarks>
public sealed class PrivacyValidator : IPrivacyValidator
{
    private readonly ValidationOptions _options;
    private readonly AntiLeakScanner _scanner;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivacyValidator"/> class with the
    /// default thresholds.
    /// </summary>
    public PrivacyValidator()
        : this(ValidationOptions.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivacyValidator"/> class.
    /// </summary>
    /// <param name="options">The thresholds the scan uses.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public PrivacyValidator(ValidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _scanner = new AntiLeakScanner(options);
    }

    /// <inheritdoc/>
    public SanitizationResult Validate(
        DetectedDocument detected,
        TransformationPlan plan,
        SanitizedDocument sanitized,
        SanitizationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(sanitized);
        ArgumentNullException.ThrowIfNull(policy);

        var run = new Run();

        CheckStatus(run, sanitized);
        CheckScan(run, detected);

        var candidates = Candidates(detected, plan, run);

        CheckUnresolved(run, plan, policy);
        CheckViolations(run, plan);
        CheckCoverage(run, plan, sanitized);
        CheckLeaks(run, candidates, sanitized);
        CheckTokens(run, candidates, sanitized);
        CheckMarkers(run, sanitized);
        CheckProvenance(run, plan, sanitized);
        CheckStructure(run, plan, sanitized, policy);

        var report = PrivacyValidationReport.Create(sanitized.BundleId, run.Executed, run.Findings);

        return report.Passed
            ? SanitizationResult.Allowed(Payload(sanitized), report)
            : SanitizationResult.Blocked(report);
    }

    private static void CheckStatus(Run run, SanitizedDocument sanitized)
    {
        run.Ran(PrivacyCheck.SanitizationStatus);

        if (sanitized.Status != SanitizationStatus.Valid)
        {
            run.Fail(PrivacyFinding.For(PrivacyCheck.SanitizationStatus));
        }
    }

    private static void CheckScan(Run run, DetectedDocument detected)
    {
        run.Ran(PrivacyCheck.ScanCompleteness);

        for (var index = 0; index < detected.Extracted.Failures.Count; index++)
        {
            run.Fail(PrivacyFinding.For(PrivacyCheck.ScanCompleteness));
        }

        foreach (var failure in detected.Failures)
        {
            run.Fail(failure.SegmentId is { } segment
                ? PrivacyFinding.InSegment(PrivacyCheck.ScanCompleteness, segment)
                : PrivacyFinding.For(PrivacyCheck.ScanCompleteness));
        }
    }

    private static void CheckUnresolved(Run run, TransformationPlan plan, SanitizationPolicy policy)
    {
        run.Ran(PrivacyCheck.UnresolvedEntities);

        foreach (var entity in plan.Unresolved)
        {
            if (!policy.ToleratesUnresolved(entity.Sensitivity))
            {
                run.Fail(PrivacyFinding.ForEntity(PrivacyCheck.UnresolvedEntities, entity.Id, entity.Type));
            }
        }
    }

    private static void CheckViolations(Run run, TransformationPlan plan)
    {
        run.Ran(PrivacyCheck.PolicyViolations);

        foreach (var violation in plan.Blocking)
        {
            run.Fail(violation is { Entity: { } entity, Type: { } type }
                ? PrivacyFinding.ForEntity(PrivacyCheck.PolicyViolations, entity, type)
                : PrivacyFinding.For(PrivacyCheck.PolicyViolations));
        }
    }

    private static void CheckCoverage(Run run, TransformationPlan plan, SanitizedDocument sanitized)
    {
        run.Ran(PrivacyCheck.TransformationCoverage);

        var applied = new HashSet<EntityId>();

        foreach (var transformation in sanitized.Applied)
        {
            _ = applied.Add(transformation.EntityId);
        }

        foreach (var decision in plan.Decisions)
        {
            if (Rewrites(decision.Action) && !applied.Contains(decision.Entity))
            {
                run.Fail(PrivacyFinding.ForEntity(
                    PrivacyCheck.TransformationCoverage,
                    decision.Entity,
                    decision.Type));
            }
        }
    }

    private void CheckLeaks(Run run, IReadOnlyList<LeakCandidate> candidates, SanitizedDocument sanitized)
    {
        run.Ran(PrivacyCheck.AntiLeakScan);

        foreach (var finding in _scanner.Scan(candidates, sanitized.Segments))
        {
            run.Fail(finding);
        }
    }

    private void CheckTokens(Run run, IReadOnlyList<LeakCandidate> candidates, SanitizedDocument sanitized)
    {
        run.Ran(PrivacyCheck.TokenResolution);

        foreach (var segment in sanitized.Segments)
        {
            var tokens = TextNormalizer.Reduce(MarkerSyntax.MarkersOnly(segment.Text));

            foreach (var candidate in candidates)
            {
                var value = TextNormalizer.Reduce(candidate.Value);

                if (value.Length >= _options.MinimumFragmentLength
                    && tokens.Contains(value, StringComparison.Ordinal))
                {
                    run.Fail(PrivacyFinding.ForLeak(
                        PrivacyCheck.TokenResolution,
                        LeakSignal.Normalized,
                        candidate.Type,
                        segment.Id));
                }
            }
        }
    }

    private static void CheckMarkers(Run run, SanitizedDocument sanitized)
    {
        run.Ran(PrivacyCheck.MarkerIntegrity);

        var expected = 0;

        foreach (var transformation in sanitized.Applied)
        {
            expected += Marks(transformation.Action) ? 1 : 0;
        }

        var found = 0;

        foreach (var segment in sanitized.Segments)
        {
            found += MarkerSyntax.CountMarkers(segment.Text);
        }

        if (found != expected)
        {
            run.Fail(PrivacyFinding.For(PrivacyCheck.MarkerIntegrity));
        }
    }

    private static void CheckProvenance(Run run, TransformationPlan plan, SanitizedDocument sanitized)
    {
        run.Ran(PrivacyCheck.ProvenanceCompleteness);

        var provenance = sanitized.Provenance;
        var stated = provenance.PolicyId == plan.PolicyId && provenance.PolicyVersion == plan.PolicyVersion;

        if (!stated || provenance.Detectors.Count == 0)
        {
            run.Fail(PrivacyFinding.For(PrivacyCheck.ProvenanceCompleteness));
        }
    }

    private static void CheckStructure(
        Run run,
        TransformationPlan plan,
        SanitizedDocument sanitized,
        SanitizationPolicy policy)
    {
        run.Ran(PrivacyCheck.PayloadStructure);

        var characters = 0L;

        foreach (var segment in sanitized.Segments)
        {
            characters += segment.Text.Length;
        }

        var limits = policy.Limits;
        var sound = sanitized.BundleId == plan.BundleId
            && sanitized.Segments.Count > 0
            && characters <= limits.MaxExtractedCharacters
            && sanitized.Applied.Count <= limits.MaxEntities;

        if (!sound)
        {
            run.Fail(PrivacyFinding.For(PrivacyCheck.PayloadStructure));
        }
    }

    /// <summary>
    /// Collects the values the scan looks for.
    /// </summary>
    /// <param name="detected">The detected document, which still holds the original text.</param>
    /// <param name="plan">The plan, which says which values were supposed to disappear.</param>
    /// <param name="run">The run, which records a value the validator could not read.</param>
    /// <returns>One candidate per entity whose value should no longer be in the output.</returns>
    /// <remarks>
    /// A preserved value is legitimately still there, and an unresolved one is governed by
    /// <see cref="PrivacyCheck.UnresolvedEntities"/> and the tolerance the policy states.
    /// Reporting either as a leak would make the scan fire on documents the policy allowed.
    /// </remarks>
    private static List<LeakCandidate> Candidates(
        DetectedDocument detected,
        TransformationPlan plan,
        Run run)
    {
        var rewritten = new Dictionary<EntityId, EntityType>();

        foreach (var decision in plan.Decisions)
        {
            if (Rewrites(decision.Action))
            {
                rewritten[decision.Entity] = decision.Type;
            }
        }

        var segments = Index(detected.Extracted.Segments);
        var candidates = new List<LeakCandidate>();

        foreach (var entity in detected.Entities)
        {
            if (rewritten.ContainsKey(entity.Id))
            {
                Collect(entity, segments, candidates, run);
            }
        }

        return candidates;
    }

    private static void Collect(
        SensitiveEntity entity,
        Dictionary<SegmentId, TextSegment> segments,
        List<LeakCandidate> candidates,
        Run run)
    {
        if (!segments.TryGetValue(entity.Span.SegmentId, out var segment)
            || !entity.Span.IsWithin(segment.Text.Length))
        {
            run.Fail(PrivacyFinding.ForEntity(PrivacyCheck.ScanCompleteness, entity.Id, entity.Type));

            return;
        }

        var value = segment.Text.Substring(entity.Span.Start, entity.Span.Length);

        if (value.Length > 0)
        {
            candidates.Add(new LeakCandidate(value, entity.Type));
        }
    }

    private static Dictionary<SegmentId, TextSegment> Index(IReadOnlyList<TextSegment> segments)
    {
        var index = new Dictionary<SegmentId, TextSegment>(segments.Count);

        foreach (var segment in segments)
        {
            index[segment.Id] = segment;
        }

        return index;
    }

    private static ValidatedPayload Payload(SanitizedDocument sanitized) =>
        ValidatedPayload.Create(
            sanitized.BundleId,
            sanitized.Segments,
            sanitized.Provenance,
            Hash(sanitized.Segments));

    /// <summary>
    /// Hashes the content that is about to leave.
    /// </summary>
    /// <param name="segments">The sanitized segments.</param>
    /// <returns>The hexadecimal digest.</returns>
    /// <remarks>
    /// The hash binds an audit record to the bytes that were sent without storing them. It is
    /// taken over the sanitized text only, so the record it supports can be kept for as long
    /// as the audit requires.
    /// </remarks>
    private static string Hash(IReadOnlyList<SanitizedSegment> segments)
    {
        var content = new StringBuilder();

        foreach (var segment in segments)
        {
            _ = content.Append(segment.Id.Value).Append('\u001f').Append(segment.Text).Append('\u001e');
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(content.ToString()));

        return Convert.ToHexString(digest);
    }

    private static bool Rewrites(TransformationAction action) => action != TransformationAction.Preserve;

    private static bool Marks(TransformationAction action) => action
        is TransformationAction.Redact
        or TransformationAction.Pseudonymize
        or TransformationAction.Hash;

    private sealed class Run
    {
        public List<PrivacyCheck> Executed { get; } = [];

        public List<PrivacyFinding> Findings { get; } = [];

        public void Ran(PrivacyCheck check) => Executed.Add(check);

        public void Fail(PrivacyFinding finding) => Findings.Add(finding);
    }
}
