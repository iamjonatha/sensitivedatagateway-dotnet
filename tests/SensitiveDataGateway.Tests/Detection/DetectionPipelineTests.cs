using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Tests.Detection;

/// <summary>
/// A detector the test controls completely.
/// </summary>
internal sealed class StubDetector : ISensitiveDataDetector
{
    private readonly Func<ExtractedDocument, CancellationToken, ValueTask<IReadOnlyList<SensitiveEntity>>> _behaviour;

    private StubDetector(
        string id,
        Func<ExtractedDocument, CancellationToken, ValueTask<IReadOnlyList<SensitiveEntity>>> behaviour)
    {
        Version = DetectorVersion.Create(DetectorId.Create(id), "1.0.0");
        _behaviour = behaviour;
    }

    public DetectorVersion Version { get; }

    public static StubDetector Reporting(string id, params SensitiveEntity[] entities) =>
        new(id, (_, _) => new ValueTask<IReadOnlyList<SensitiveEntity>>(entities));

    public static StubDetector Throwing(string id, Exception exception) =>
        new(id, (_, _) => throw exception);

    public static StubDetector Hanging(string id) =>
        new(id, async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);

            return [];
        });

    public ValueTask<IReadOnlyList<SensitiveEntity>> DetectAsync(
        ExtractedDocument document,
        CancellationToken cancellationToken) =>
        _behaviour(document, cancellationToken);
}

/// <summary>
/// Covers what the detection stage does with what the detectors report, and with what they
/// fail to report.
/// </summary>
public sealed class DetectionPipelineTests
{
    private static readonly SegmentId s_segment = SegmentId.Create("segment-0");

    [Fact]
    public void Rejects_a_pipeline_with_no_detectors() =>
        Assert.Throws<ArgumentException>(() => new DetectionPipeline([]));

    [Fact]
    public async Task Reports_every_detector_that_examined_the_document()
    {
        var pipeline = new DetectionPipeline(DeterministicDetectors.All());

        var detected = await pipeline.DetectAsync(Fixture.Extracted("nothing here"), CancellationToken.None);

        Assert.Equal(DeterministicDetectors.All().Count, detected.Detectors.Count);
        Assert.Empty(detected.Entities);
        Assert.True(detected.IsComplete);
    }

    [Fact]
    public async Task Keeps_the_more_damaging_of_two_overlapping_claims()
    {
        var pipeline = new DetectionPipeline(
        [
            StubDetector.Reporting("weak", Entity("a", EntityType.Password, 0, 10, SensitivityLevel.Moderate)),
            StubDetector.Reporting("strong", Entity("b", EntityType.ApiKey, 2, 6, SensitivityLevel.Critical)),
        ]);

        var detected = await pipeline.DetectAsync(Fixture.Extracted("0123456789"), CancellationToken.None);

        var entity = Assert.Single(detected.Entities);

        Assert.Equal(EntityType.ApiKey, entity.Type);
        Assert.Equal(2, entity.Span.Start);
    }

    [Fact]
    public async Task Keeps_the_longer_of_two_equally_strong_claims()
    {
        var pipeline = new DetectionPipeline(
        [
            StubDetector.Reporting("short", Entity("a", EntityType.PrivateKey, 0, 4, SensitivityLevel.Critical)),
            StubDetector.Reporting("long", Entity("b", EntityType.PrivateKey, 0, 10, SensitivityLevel.Critical)),
        ]);

        var detected = await pipeline.DetectAsync(Fixture.Extracted("0123456789"), CancellationToken.None);

        Assert.Equal(10, Assert.Single(detected.Entities).Span.Length);
    }

    [Fact]
    public async Task Keeps_claims_that_do_not_overlap()
    {
        var pipeline = new DetectionPipeline(
        [
            StubDetector.Reporting("first", Entity("a", EntityType.Email, 0, 4, SensitivityLevel.Moderate)),
            StubDetector.Reporting("second", Entity("b", EntityType.Phone, 6, 4, SensitivityLevel.Moderate)),
        ]);

        var detected = await pipeline.DetectAsync(Fixture.Extracted("0123456789"), CancellationToken.None);

        Assert.Equal(2, detected.Entities.Count);
    }

    [Fact]
    public async Task Orders_the_result_by_position()
    {
        var pipeline = new DetectionPipeline(
        [
            StubDetector.Reporting(
                "one",
                Entity("a", EntityType.Email, 6, 4, SensitivityLevel.Moderate),
                Entity("b", EntityType.Email, 0, 4, SensitivityLevel.Moderate)),
        ]);

        var detected = await pipeline.DetectAsync(Fixture.Extracted("0123456789"), CancellationToken.None);

        Assert.Equal([0, 6], detected.Entities.Select(entity => entity.Span.Start));
    }

    [Fact]
    public async Task Produces_the_same_result_on_every_run()
    {
        var document = Fixture.Extracted(
            "write to first.last@example.com or call +39 02 1234 5678",
            "credit IT51X9999999999000000999999, card 4111111111111111");

        var first = await Run(document);
        var second = await Run(document);

        Assert.Equal(first, second);

        static async Task<string[]> Run(ExtractedDocument document)
        {
            var detected = await new DetectionPipeline(DeterministicDetectors.All())
                .DetectAsync(document, CancellationToken.None);

            return [.. detected.Entities.Select(entity =>
                $"{entity.Type.Value}:{entity.Span.Start}:{entity.Span.Length}")];
        }
    }

    [Fact]
    public async Task Records_a_gap_when_a_detector_throws()
    {
        var pipeline = new DetectionPipeline(
        [
            StubDetector.Reporting("good", Entity("a", EntityType.Email, 0, 4, SensitivityLevel.Moderate)),
            StubDetector.Throwing("bad", new InvalidOperationException("boom")),
        ]);

        var detected = await pipeline.DetectAsync(Fixture.Extracted("0123456789"), CancellationToken.None);

        var failure = Assert.Single(detected.Failures);

        Assert.Equal(DetectorId.Create("bad"), failure.DetectorId);
        Assert.Equal(DetectionFailureReason.DetectorFailed, failure.Reason);
        Assert.False(detected.IsComplete);
    }

    [Fact]
    public async Task Discards_what_a_detector_reported_before_it_failed()
    {
        var pipeline = new DetectionPipeline(
        [
            StubDetector.Throwing("bad", new InvalidOperationException("boom")),
        ]);

        var detected = await pipeline.DetectAsync(Fixture.Extracted("0123456789"), CancellationToken.None);

        Assert.Empty(detected.Entities);
        Assert.False(detected.IsComplete);
    }

    [Fact]
    public async Task Records_a_gap_when_a_detector_runs_out_of_time()
    {
        var options = DetectionPipelineOptions.Default
            .WithDetectorTimeout(DetectionPipelineOptions.MinimumTimeout);

        var pipeline = new DetectionPipeline([StubDetector.Hanging("slow")], options);

        var detected = await pipeline.DetectAsync(Fixture.Extracted("0123456789"), CancellationToken.None);

        Assert.Equal(DetectionFailureReason.TimedOut, Assert.Single(detected.Failures).Reason);
    }

    [Fact]
    public async Task Records_a_gap_when_a_detector_floods_the_stage()
    {
        var candidates = Enumerable
            .Range(0, DetectionPipelineOptions.MinimumCandidates + 1)
            .Select(index => Entity($"e{index}", EntityType.Email, index * 2, 1, SensitivityLevel.Moderate))
            .ToArray();

        var options = DetectionPipelineOptions.Default
            .WithMaxCandidatesPerDetector(DetectionPipelineOptions.MinimumCandidates);

        var pipeline = new DetectionPipeline([StubDetector.Reporting("flood", candidates)], options);

        var detected = await pipeline.DetectAsync(Fixture.Extracted(new string('x', 100)), CancellationToken.None);

        Assert.Equal(DetectionFailureReason.LimitExceeded, Assert.Single(detected.Failures).Reason);
        Assert.Empty(detected.Entities);
    }

    [Fact]
    public async Task Propagates_cancellation_of_the_whole_stage()
    {
        using var cancellation = new CancellationTokenSource();

        var pipeline = new DetectionPipeline([StubDetector.Hanging("slow")]);

        var running = pipeline.DetectAsync(Fixture.Extracted("0123456789"), cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
    }

    [Fact]
    public async Task Prefers_the_whole_private_key_block_over_its_header()
    {
        const string Block =
            "-----BEGIN RSA PRIVATE KEY-----\nQUJDREVGRw==\n-----END RSA PRIVATE KEY-----";

        var pipeline = new DetectionPipeline([DeterministicDetectors.PrivateKey()]);

        var detected = await pipeline.DetectAsync(Fixture.Extracted(Block), CancellationToken.None);

        Assert.Equal(Block.Length, Assert.Single(detected.Entities).Span.Length);
    }

    [Fact]
    public async Task Prefers_the_connection_string_over_the_locator_inside_it()
    {
        const string Text = "postgres://svc:not-a-real-secret@db.example.net:5432/app";

        var pipeline = new DetectionPipeline(
        [
            DeterministicDetectors.ConnectionString(),
            DeterministicDetectors.Url(),
        ]);

        var detected = await pipeline.DetectAsync(Fixture.Extracted(Text), CancellationToken.None);

        Assert.Equal(EntityType.ConnectionString, Assert.Single(detected.Entities).Type);
    }

    private static SensitiveEntity Entity(
        string id,
        EntityType type,
        int start,
        int length,
        SensitivityLevel sensitivity) =>
        SensitiveEntity.Create(
            EntityId.Create(id),
            type,
            SourceSpan.Create(s_segment, start, length),
            DetectionConfidence.Certain,
            DetectorId.Create("stub"),
            sensitivity,
            DetectionEvidence.FromPattern("stub", checksumValidated: false, length));
}
