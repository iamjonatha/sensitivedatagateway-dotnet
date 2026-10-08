using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;

namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// Applies a transformation plan to the text of a document.
/// </summary>
/// <remarks>
/// <para>
/// This is the stage that actually removes the content. It makes no decisions: every rewrite
/// it performs was decided by a policy, and an entity the plan did not mention is left alone.
/// What it adds is the mechanics — which token replaces which span, how the untouched text is
/// kept from looking like a token, and what happens when two decisions claim overlapping text.
/// </para>
/// <para>
/// A pseudonym is reversible, and the way back is a <see cref="PseudonymMap"/> held by the
/// store this was constructed with. The map is never attached to the result: a
/// <see cref="SanitizedDocument"/> has no path to it, which is what lets the document travel
/// while the mapping does not.
/// </para>
/// <para>
/// Partial sanitization is not an acceptable outcome. When a planned transformation cannot be
/// applied, the result carries <see cref="SanitizationStatus.Incomplete"/> and the validator
/// refuses it, because a document that is partly clean is a document of unknown cleanliness.
/// </para>
/// </remarks>
public sealed class DocumentAnonymizer : IDocumentAnonymizer
{
    private readonly IPseudonymStore _store;
    private readonly AnonymizationOptions _options;
    private readonly Hasher? _hasher;

    /// <summary>
    /// Creates an anonymizer that supports no hashing and no scope wider than a bundle.
    /// </summary>
    /// <param name="store">Where pseudonym maps are kept.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is <see langword="null"/>.</exception>
    public DocumentAnonymizer(IPseudonymStore store)
        : this(store, AnonymizationOptions.Default)
    {
    }

    /// <summary>
    /// Creates an anonymizer.
    /// </summary>
    /// <param name="store">Where pseudonym maps are kept.</param>
    /// <param name="options">What the policy needs that the policy cannot state.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="store"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public DocumentAnonymizer(IPseudonymStore store, AnonymizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);

        _store = store;
        _options = options;
        _hasher = options.HashingKey is null ? null : new Hasher(options.HashingKey);
    }

    /// <inheritdoc/>
    public SanitizedDocument Anonymize(
        DetectedDocument document,
        TransformationPlan plan,
        SanitizationProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(provenance);
        RequireApplicable(document, plan);

        var run = new Run();
        var planned = PlanBySegment(document, plan, run);
        var segments = new List<SanitizedSegment>(document.Extracted.Segments.Count);

        foreach (var segment in document.Extracted.Segments)
        {
            segments.Add(RewriteSegment(segment, document.Extracted.BundleId, planned, run));
        }

        ReportInputGaps(document, plan, run);

        return SanitizedDocument.Create(
            document.Extracted.BundleId,
            segments,
            run.IsComplete ? SanitizationStatus.Valid : SanitizationStatus.Incomplete,
            run.Applied,
            run.Warnings,
            provenance);
    }

    private static void RequireApplicable(DetectedDocument document, TransformationPlan plan)
    {
        if (!plan.BundleId.Equals(document.Extracted.BundleId))
        {
            throw new ArgumentException(
                "The plan was built for another bundle than the document it is applied to.",
                nameof(plan));
        }

        if (plan.IsBlocked)
        {
            throw new InvalidOperationException(
                "A blocked plan is not applied. The operation stops before anonymization.");
        }
    }

    private static Dictionary<SegmentId, List<PlannedRewrite>> PlanBySegment(
        DetectedDocument document,
        TransformationPlan plan,
        Run run)
    {
        var entities = new Dictionary<EntityId, SensitiveEntity>();

        foreach (var entity in document.Entities)
        {
            entities[entity.Id] = entity;
        }

        var planned = new Dictionary<SegmentId, List<PlannedRewrite>>();

        foreach (var decision in plan.Decisions)
        {
            AddRewrite(planned, entities, decision, run);
        }

        return planned;
    }

    private static void AddRewrite(
        Dictionary<SegmentId, List<PlannedRewrite>> planned,
        Dictionary<EntityId, SensitiveEntity> entities,
        PolicyDecision decision,
        Run run)
    {
        if (decision.Action == TransformationAction.Preserve)
        {
            return;
        }

        if (!entities.TryGetValue(decision.Entity, out var entity))
        {
            run.Fail(SanitizationWarningCode.UnresolvedEntity, decision.Entity, segment: null);

            return;
        }

        if (!planned.TryGetValue(entity.Span.SegmentId, out var rewrites))
        {
            rewrites = [];
            planned[entity.Span.SegmentId] = rewrites;
        }

        rewrites.Add(new PlannedRewrite(decision, entity));
    }

    private SanitizedSegment RewriteSegment(
        TextSegment segment,
        BundleId bundleId,
        Dictionary<SegmentId, List<PlannedRewrite>> planned,
        Run run)
    {
        var rewrites = planned.TryGetValue(segment.Id, out var found) ? Resolve(found, run) : [];
        var text = ApplyRewrites(segment, bundleId, rewrites, run);

        return SanitizedSegment.Create(segment.Id, segment.PartId, text, segment.Origin, segment.Order);
    }

    private static List<PlannedRewrite> Resolve(List<PlannedRewrite> candidates, Run run)
    {
        candidates.Sort(CompareByPrecedence);

        var accepted = new List<PlannedRewrite>(candidates.Count);

        foreach (var candidate in candidates)
        {
            Admit(accepted, candidate, run);
        }

        accepted.Sort(static (left, right) => left.Entity.Span.Start.CompareTo(right.Entity.Span.Start));

        return accepted;
    }

    private static void Admit(List<PlannedRewrite> accepted, PlannedRewrite candidate, Run run)
    {
        var span = candidate.Entity.Span;

        foreach (var taken in accepted)
        {
            if (!taken.Entity.Span.Overlaps(span))
            {
                continue;
            }

            ReportClash(taken.Entity.Span, candidate, run);

            return;
        }

        accepted.Add(candidate);
    }

    private static void ReportClash(SourceSpan taken, PlannedRewrite candidate, Run run)
    {
        var span = candidate.Entity.Span;
        var covered = span.Start >= taken.Start && span.End <= taken.End;

        if (covered)
        {
            run.Warn(SanitizationWarningCode.OverlappingEntities, candidate.Decision.Entity, span.SegmentId);

            return;
        }

        run.Fail(SanitizationWarningCode.TransformationSkipped, candidate.Decision.Entity, span.SegmentId);
    }

    private static int CompareByPrecedence(PlannedRewrite left, PlannedRewrite right)
    {
        var byStrength = Strength(right.Decision.Action).CompareTo(Strength(left.Decision.Action));

        if (byStrength != 0)
        {
            return byStrength;
        }

        var byLength = right.Entity.Span.Length.CompareTo(left.Entity.Span.Length);

        return byLength != 0 ? byLength : left.Entity.Span.Start.CompareTo(right.Entity.Span.Start);
    }

    private static int Strength(TransformationAction action) => action switch
    {
        TransformationAction.Redact => 4,
        TransformationAction.Hash => 3,
        TransformationAction.Pseudonymize => 2,
        TransformationAction.Mask => 1,
        _ => 0,
    };

    private string ApplyRewrites(
        TextSegment segment,
        BundleId bundleId,
        List<PlannedRewrite> rewrites,
        Run run)
    {
        var text = segment.Text;
        var builder = new StringBuilder(text.Length);
        var cursor = 0;
        var escaped = false;

        foreach (var rewrite in rewrites)
        {
            var span = rewrite.Entity.Span;
            escaped |= AppendKept(builder, text, cursor, span.Start - cursor);

            var replacement = Replacement(rewrite, text.Substring(span.Start, span.Length), segment.PartId, bundleId);
            builder.Append(replacement);
            run.Applied.Add(AppliedTransformation.Create(
                rewrite.Decision.Entity,
                rewrite.Decision.Type,
                rewrite.Decision.Action,
                span,
                replacement.Length));
            cursor = span.End;
        }

        escaped |= AppendKept(builder, text, cursor, text.Length - cursor);

        if (escaped)
        {
            run.Warn(SanitizationWarningCode.MarkerSyntaxEscaped, entity: null, segment.Id);
        }

        return builder.ToString();
    }

    private static bool AppendKept(StringBuilder builder, string text, int start, int length)
    {
        if (length <= 0)
        {
            return false;
        }

        var kept = text.Substring(start, length);
        var escaped = MarkerSyntax.Escape(kept);
        builder.Append(escaped);

        return !ReferenceEquals(escaped, kept);
    }

    private string Replacement(PlannedRewrite rewrite, string value, PartId partId, BundleId bundleId) =>
        rewrite.Decision.Action switch
        {
            TransformationAction.Mask => Masker.Apply(value),
            TransformationAction.Redact => Redactor.Apply(rewrite.Decision.Type),
            TransformationAction.Pseudonymize => Pseudonym(rewrite, value, partId, bundleId),
            TransformationAction.Hash => RequireHasher().Apply(rewrite.Decision.Type, value),
            _ => throw new NotSupportedException(Unsupported(rewrite.Decision.Action)),
        };

    private string Pseudonym(PlannedRewrite rewrite, string value, PartId partId, BundleId bundleId)
    {
        var scope = rewrite.Decision.Scope ?? PseudonymScope.Bundle;
        var map = _store.GetOrCreate(ScopeKey(scope, partId, bundleId));

        return map.Allocate(rewrite.Decision.Type, value);
    }

    private PseudonymScopeKey ScopeKey(PseudonymScope scope, PartId partId, BundleId bundleId) => scope switch
    {
        PseudonymScope.Document => PseudonymScopeKey.ForDocument(partId),
        PseudonymScope.Bundle => PseudonymScopeKey.ForBundle(bundleId),
        PseudonymScope.Session => PseudonymScopeKey.ForWiderScope(scope, RequireName(_options.SessionName, scope)),
        PseudonymScope.Tenant => PseudonymScopeKey.ForWiderScope(scope, RequireName(_options.TenantName, scope)),
        _ => throw new NotSupportedException(UnsupportedScope(scope)),
    };

    private Hasher RequireHasher() =>
        _hasher ?? throw new InvalidOperationException(
            "The policy hashes a value and no hashing key was supplied. An unkeyed hash of a "
            + "short value is reversible, so the gateway does not produce one.");

    private static string RequireName(string? name, PseudonymScope scope) =>
        name ?? throw new InvalidOperationException(
            $"The policy correlates pseudonyms across the {scope} scope and no name for it was "
            + "supplied. A scope with no name is a scope that cannot be told from another.");

    private static string Unsupported(TransformationAction action) =>
        string.Format(CultureInfo.InvariantCulture, "The anonymizer does not apply {0}.", action);

    private static string UnsupportedScope(PseudonymScope scope) =>
        string.Format(CultureInfo.InvariantCulture, "The anonymizer does not know the {0} scope.", scope);

    private static void ReportInputGaps(DetectedDocument document, TransformationPlan plan, Run run)
    {
        if (!document.Extracted.IsComplete)
        {
            run.Fail(SanitizationWarningCode.ExtractionIncomplete, entity: null, segment: null);
        }

        if (document.Failures.Count > 0)
        {
            run.Fail(SanitizationWarningCode.DetectionIncomplete, entity: null, segment: null);
        }

        foreach (var entity in plan.Unresolved)
        {
            run.Fail(SanitizationWarningCode.UnresolvedEntity, entity.Id, entity.Span.SegmentId);
        }
    }

    private readonly record struct PlannedRewrite(PolicyDecision Decision, SensitiveEntity Entity);

    private sealed class Run
    {
        public List<AppliedTransformation> Applied { get; } = [];

        public List<SanitizationWarning> Warnings { get; } = [];

        public bool IsComplete { get; private set; } = true;

        public void Warn(SanitizationWarningCode code, EntityId? entity, SegmentId? segment) =>
            Warnings.Add(SanitizationWarning.Create(code, entity, segment));

        public void Fail(SanitizationWarningCode code, EntityId? entity, SegmentId? segment)
        {
            Warn(code, entity, segment);
            IsComplete = false;
        }
    }
}
