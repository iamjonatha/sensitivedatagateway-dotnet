using System;
using System.Collections.Generic;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// The spans the reconciler has already granted, arranged so that asking whether a new span
/// collides with any of them does not cost a scan of all of them.
/// </summary>
/// <remarks>
/// <para>
/// Reconciliation accepts candidates strongest first and rejects any that touches an earlier
/// acceptance, so the accepted set is pairwise disjoint at every step. That is the property
/// this type trades on: within one segment the accepted spans form an ordered, non-touching
/// sequence, so a binary search for the candidate's start leaves only two spans that could
/// possibly overlap it — the last one starting before it and the first one starting at or
/// after it.
/// </para>
/// <para>
/// The naive alternative compares each candidate against every acceptance. On an ordinary
/// document that is a handful of comparisons and the difference is invisible. On a document
/// built to be hostile it is not: the stage admits up to
/// <see cref="DetectionPipelineOptions.MaxCandidatesPerDetector"/> candidates from each
/// detector, and the work grows as the square of their total, which is how a scan that was
/// meant to be bounded becomes a way to spend an afternoon of CPU on one request.
/// </para>
/// </remarks>
internal sealed class AcceptedSpans
{
    private readonly Dictionary<SegmentId, List<SourceSpan>> _bySegment = [];

    /// <summary>
    /// Reports whether a span shares a character with one already accepted.
    /// </summary>
    /// <param name="span">The span to test.</param>
    /// <returns><see langword="true"/> when the span collides.</returns>
    public bool Overlaps(SourceSpan span)
    {
        if (span.IsEmpty || !_bySegment.TryGetValue(span.SegmentId, out var accepted))
        {
            return false;
        }

        var index = FirstStartingAtOrAfter(accepted, span.Start);

        return (index < accepted.Count && accepted[index].Start < span.End)
            || (index > 0 && accepted[index - 1].End > span.Start);
    }

    /// <summary>
    /// Records a span as accepted.
    /// </summary>
    /// <param name="span">The span to record.</param>
    public void Add(SourceSpan span)
    {
        if (span.IsEmpty)
        {
            return;
        }

        if (!_bySegment.TryGetValue(span.SegmentId, out var accepted))
        {
            accepted = [];
            _bySegment[span.SegmentId] = accepted;
        }

        accepted.Insert(FirstStartingAtOrAfter(accepted, span.Start), span);
    }

    /// <summary>
    /// Finds where a start position falls in an ordered list of accepted spans.
    /// </summary>
    /// <param name="accepted">The accepted spans of one segment, ordered by start.</param>
    /// <param name="start">The position to place.</param>
    /// <returns>
    /// The index of the first span starting at or after <paramref name="start"/>, or the count
    /// when there is none.
    /// </returns>
    private static int FirstStartingAtOrAfter(List<SourceSpan> accepted, int start)
    {
        var low = 0;
        var high = accepted.Count;

        while (low < high)
        {
            var middle = low + ((high - low) / 2);

            if (accepted[middle].Start < start)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }
}
