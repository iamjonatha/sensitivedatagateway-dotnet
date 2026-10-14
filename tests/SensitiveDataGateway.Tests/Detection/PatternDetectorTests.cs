using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Tests.Detection;

/// <summary>
/// Covers the detector a deployment builds from its own expression, and the guards that stop
/// an expression from becoming a denial of service.
/// </summary>
public sealed partial class PatternDetectorTests
{
    private static readonly DetectorVersion s_version =
        DetectorVersion.Create(DetectorId.Create("order-reference"), "1.0.0");

    [Fact]
    public async Task Reports_what_the_expression_matches()
    {
        var detector = PatternDetector.Create(
            s_version,
            "order-reference",
            EntityType.Create("ORDER_REFERENCE"),
            SensitivityLevel.Low,
            DetectionConfidence.Create(0.9),
            OrderReference());

        var matches = await Scan.MatchesIn(detector, "see ORD-12345 for details");

        Assert.Equal("ORD-12345", Assert.Single(matches));
    }

    [Fact]
    public async Task Reports_the_value_group_when_the_expression_declares_one()
    {
        var detector = PatternDetector.Create(
            s_version,
            "labelled-reference",
            EntityType.Create("ORDER_REFERENCE"),
            SensitivityLevel.Low,
            DetectionConfidence.Create(0.9),
            LabelledReference());

        var matches = await Scan.MatchesIn(detector, "order: ORD-12345");

        Assert.Equal("ORD-12345", Assert.Single(matches));
    }

    [Fact]
    public void Refuses_an_expression_with_no_match_timeout()
    {
        var unbounded = new Regex("ORD-[0-9]{1,8}", RegexOptions.None);

        var error = Assert.Throws<ArgumentException>(() => PatternDetector.Create(
            s_version,
            "order-reference",
            EntityType.Create("ORDER_REFERENCE"),
            SensitivityLevel.Low,
            DetectionConfidence.Create(0.9),
            unbounded));

        Assert.Equal("pattern", error.ParamName);
    }

    [Fact]
    public async Task Records_a_gap_when_an_expression_runs_out_of_time()
    {
        var detector = PatternDetector.Create(
            s_version,
            "catastrophic",
            EntityType.Create("ORDER_REFERENCE"),
            SensitivityLevel.Low,
            DetectionConfidence.Create(0.9),
            new Regex("(a+)+$", RegexOptions.None, TimeSpan.FromMilliseconds(50)));

        var pipeline = new DetectionPipeline([detector]);

        var started = Stopwatch.StartNew();

        var detected = await pipeline.DetectAsync(
            Fixture.Extracted(new string('a', 40) + "!"),
            CancellationToken.None);

        started.Stop();

        Assert.Equal(DetectionFailureReason.TimedOut, Assert.Single(detected.Failures).Reason);
        Assert.False(detected.IsComplete);
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Built_in_expressions_do_not_backtrack_on_a_hostile_line()
    {
        var hostile = new string('a', 2000) + "@" + new string('b', 2000);

        var pipeline = new DetectionPipeline(DeterministicDetectors.All());

        var started = Stopwatch.StartNew();

        var detected = await pipeline.DetectAsync(Fixture.Extracted(hostile), CancellationToken.None);

        started.Stop();

        Assert.True(detected.IsComplete);
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(10));
    }

    [GeneratedRegex(@"\bORD-\d{5}\b", RegexOptions.None, 1000)]
    private static partial Regex OrderReference();

    [GeneratedRegex(@"\border:[ ]?(?<value>ORD-\d{5})\b", RegexOptions.None, 1000)]
    private static partial Regex LabelledReference();
}
