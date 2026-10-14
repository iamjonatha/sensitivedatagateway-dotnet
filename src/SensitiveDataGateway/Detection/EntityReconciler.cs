using System;
using System.Collections.Generic;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// Resolves the candidates several detectors claim over the same characters.
/// </summary>
/// <remarks>
/// <para>
/// Detectors overlap by design: a connection string contains a password, a private key block
/// contains its own header, and a dense run of characters is often also an API key. Passing
/// all of them downstream would make the anonymizer rewrite the same characters twice, which
/// it refuses to do, so the overlap is resolved here, once, by a rule that does not depend on
/// the order the detectors happened to finish in.
/// </para>
/// <para>
/// The rule is that the most damaging claim wins. Where two claims are equally damaging the
/// more confident one wins, then the longer one, and finally the one whose detector sorts
/// first — not because that is meaningful, but because the result has to be the same on every
/// run and on every machine.
/// </para>
/// <para>
/// The claim that survives keeps the detector that made it. The full set of detectors that
/// examined the document is recorded on <see cref="DetectedDocument.Detectors"/>, so nothing
/// is lost by not folding the loser into the winner.
/// </para>
/// </remarks>
internal static class EntityReconciler
{
    /// <summary>
    /// Drops every candidate that overlaps a stronger one.
    /// </summary>
    /// <param name="segments">The segments of the document, in reading order.</param>
    /// <param name="candidates">Everything every detector reported.</param>
    /// <returns>The surviving candidates, ordered by position.</returns>
    public static List<SensitiveEntity> Reconcile(
        IReadOnlyList<TextSegment> segments,
        List<SensitiveEntity> candidates)
    {
        var order = PositionOf(segments);

        candidates.Sort(ComparePriority);

        var accepted = new List<SensitiveEntity>(candidates.Count);

        foreach (var candidate in candidates)
        {
            if (!OverlapsAccepted(accepted, candidate))
            {
                accepted.Add(candidate);
            }
        }

        accepted.Sort((x, y) => ComparePosition(order, x, y));

        return accepted;
    }

    private static Dictionary<SegmentId, int> PositionOf(IReadOnlyList<TextSegment> segments)
    {
        var order = new Dictionary<SegmentId, int>(segments.Count);

        for (var index = 0; index < segments.Count; index++)
        {
            order[segments[index].Id] = index;
        }

        return order;
    }

    private static bool OverlapsAccepted(List<SensitiveEntity> accepted, SensitiveEntity candidate)
    {
        foreach (var entity in accepted)
        {
            if (entity.Span.Overlaps(candidate.Span))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Orders two candidates by how strong a claim they are, strongest first.
    /// </summary>
    /// <param name="x">The first candidate.</param>
    /// <param name="y">The second candidate.</param>
    /// <returns>A negative number when <paramref name="x"/> wins.</returns>
    private static int ComparePriority(SensitiveEntity x, SensitiveEntity y)
    {
        var bySensitivity = y.Sensitivity.CompareTo(x.Sensitivity);

        if (bySensitivity != 0)
        {
            return bySensitivity;
        }

        var byConfidence = y.Confidence.CompareTo(x.Confidence);

        if (byConfidence != 0)
        {
            return byConfidence;
        }

        var byLength = y.Span.Length.CompareTo(x.Span.Length);

        return byLength != 0 ? byLength : CompareIdentity(x, y);
    }

    /// <summary>
    /// Orders two candidates that are equally strong, so that the outcome is reproducible.
    /// </summary>
    /// <param name="x">The first candidate.</param>
    /// <param name="y">The second candidate.</param>
    /// <returns>A negative number when <paramref name="x"/> sorts first.</returns>
    private static int CompareIdentity(SensitiveEntity x, SensitiveEntity y)
    {
        var byDetector = string.CompareOrdinal(x.Detector.Value, y.Detector.Value);

        return byDetector != 0 ? byDetector : string.CompareOrdinal(x.Id.Value, y.Id.Value);
    }

    private static int ComparePosition(
        Dictionary<SegmentId, int> order,
        SensitiveEntity x,
        SensitiveEntity y)
    {
        var bySegment = Position(order, x).CompareTo(Position(order, y));

        if (bySegment != 0)
        {
            return bySegment;
        }

        var byStart = x.Span.Start.CompareTo(y.Span.Start);

        return byStart != 0 ? byStart : CompareIdentity(x, y);
    }

    private static int Position(Dictionary<SegmentId, int> order, SensitiveEntity entity) =>
        order.TryGetValue(entity.Span.SegmentId, out var position) ? position : int.MaxValue;
}
