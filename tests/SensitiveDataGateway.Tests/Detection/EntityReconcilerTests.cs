using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Tests.Detection;

/// <summary>
/// Covers the rule that resolves two detectors claiming the same characters, and the bounds
/// the resolution runs under.
/// </summary>
/// <remarks>
/// The outcome of reconciliation is tested through the pipeline in
/// <see cref="DetectionPipelineTests"/>. What is tested here is what the pipeline cannot
/// reach: that the search for a collision is confined to the segment the candidate is in,
/// that it agrees with <see cref="SourceSpan.Overlaps"/> on the boundary cases, and that a
/// document carrying enough candidates to make the stage work for a long time can still be
/// cancelled while it is doing so.
/// </remarks>
public sealed class EntityReconcilerTests
{
    [Fact]
    public void Observes_cancellation_while_resolving()
    {
        var candidates = Candidates(0, 2_000, segment: 0);

        using var cancelled = new CancellationTokenSource();

        cancelled.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => EntityReconciler.Reconcile(Segments(1), candidates, cancelled.Token));
    }

    [Fact]
    public void Keeps_the_same_offsets_in_two_different_segments()
    {
        var candidates = Candidates(0, 1, segment: 0);

        candidates.AddRange(Candidates(0, 1, segment: 1));

        var accepted = EntityReconciler.Reconcile(Segments(2), candidates, CancellationToken.None);

        Assert.Equal(2, accepted.Count);
    }

    [Fact]
    public void Keeps_two_spans_that_touch_without_sharing_a_character()
    {
        var candidates = new List<SensitiveEntity>
        {
            Entity("a", 0, 10, segment: 0),
            Entity("b", 10, 10, segment: 0),
        };

        var accepted = EntityReconciler.Reconcile(Segments(1), candidates, CancellationToken.None);

        Assert.Equal(2, accepted.Count);
    }

    [Theory]
    [InlineData(10, 10)] // The same characters.
    [InlineData(12, 2)]  // Inside the accepted span.
    [InlineData(5, 20)]  // Around the accepted span.
    [InlineData(5, 10)]  // Overlapping its start.
    [InlineData(15, 10)] // Overlapping its end.
    public void Drops_a_weaker_claim_that_shares_a_character(int start, int length)
    {
        var candidates = new List<SensitiveEntity>
        {
            Entity("strong", 10, 10, segment: 0, SensitivityLevel.Critical),
            Entity("weak", start, length, segment: 0, SensitivityLevel.Low),
        };

        var accepted = EntityReconciler.Reconcile(Segments(1), candidates, CancellationToken.None);

        Assert.Equal(10, Assert.Single(accepted).Span.Start);
    }

    [Fact]
    public void Agrees_with_the_span_itself_on_every_pair()
    {
        var candidates = Enumerable
            .Range(0, 60)
            .Select(index => Entity($"e{index:D2}", index * 3, 7, segment: 0))
            .ToList();

        var accepted = EntityReconciler.Reconcile(Segments(1), [.. candidates], CancellationToken.None);

        for (var index = 1; index < accepted.Count; index++)
        {
            Assert.False(accepted[index - 1].Span.Overlaps(accepted[index].Span));
        }

        Assert.Equal(
            Naive(candidates).Select(entity => entity.Id),
            accepted.Select(entity => entity.Id));
    }

    /// <summary>
    /// Resolves the same candidates by comparing every pair, which is what the stage used to
    /// do and what its replacement has to agree with.
    /// </summary>
    /// <param name="candidates">
    /// The candidates to resolve, in the order the reconciler would consider them.
    /// </param>
    /// <returns>The candidates that survive.</returns>
    private static List<SensitiveEntity> Naive(List<SensitiveEntity> candidates)
    {
        var accepted = new List<SensitiveEntity>();

        foreach (var candidate in candidates)
        {
            if (!accepted.Any(entity => entity.Span.Overlaps(candidate.Span)))
            {
                accepted.Add(candidate);
            }
        }

        return accepted;
    }

    private static List<SensitiveEntity> Candidates(int start, int count, int segment) =>
        Enumerable
            .Range(0, count)
            .Select(index => Entity($"s{segment}-e{index}", start + (index * 4), 2, segment))
            .ToList();

    private static List<TextSegment> Segments(int count) =>
        Enumerable
            .Range(0, count)
            .Select(order => TextSegment.Create(
                SegmentId.Create($"segment-{order}"),
                PartId.Create("body"),
                new string('x', 16_000),
                SegmentOrigin.At(SegmentOriginKind.Paragraph, order + 1),
                order))
            .ToList();

    private static SensitiveEntity Entity(
        string id,
        int start,
        int length,
        int segment,
        SensitivityLevel sensitivity = SensitivityLevel.Moderate) =>
        SensitiveEntity.Create(
            EntityId.Create(id),
            EntityType.Email,
            SourceSpan.Create(SegmentId.Create($"segment-{segment}"), start, length),
            DetectionConfidence.Certain,
            Fixture.Detector,
            sensitivity,
            DetectionEvidence.FromPattern("test", checksumValidated: false, length));
}
