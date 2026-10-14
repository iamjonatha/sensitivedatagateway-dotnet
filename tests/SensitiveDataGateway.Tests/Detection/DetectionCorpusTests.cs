using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Tests.Detection;

/// <summary>
/// Measures the built-in detectors against the annotated corpus.
/// </summary>
/// <remarks>
/// <para>
/// These are the numbers published in the detector guide. They are asserted rather than
/// printed, because a precision claim that nothing enforces drifts the moment a pattern is
/// widened to make one more case pass.
/// </para>
/// <para>
/// Recall is held higher than precision on purpose. A value the gateway misses is a value
/// that reaches a model; a value it reports in error is a redaction someone has to review.
/// The two mistakes are not comparable, and the thresholds say so.
/// </para>
/// </remarks>
public sealed class DetectionCorpusTests
{
    private const double MinimumPrecision = 0.90;

    private const double MinimumRecall = 1.00;

    [Fact]
    public async Task Finds_every_annotated_value()
    {
        var measurement = await Measure();

        Assert.Empty(measurement.Missed);
        Assert.True(
            measurement.Recall >= MinimumRecall,
            $"Recall {Format(measurement.Recall)} is below {Format(MinimumRecall)}.");
    }

    [Fact]
    public async Task Reports_few_enough_values_that_are_not_there()
    {
        var measurement = await Measure();

        Assert.True(
            measurement.Precision >= MinimumPrecision,
            $"Precision {Format(measurement.Precision)} is below {Format(MinimumPrecision)}. "
                + $"Unexpected: {string.Join(", ", measurement.Unexpected)}");
    }

    [Fact]
    public async Task Scans_every_case_completely()
    {
        var pipeline = new DetectionPipeline(DeterministicDetectors.All());

        foreach (var testCase in DetectionCorpus.All())
        {
            var detected = await pipeline.DetectAsync(
                Fixture.Extracted(testCase.Text),
                CancellationToken.None);

            Assert.True(detected.IsComplete, $"The scan of '{testCase.Name}' was incomplete.");
        }
    }

    private static async Task<Measurement> Measure()
    {
        var pipeline = new DetectionPipeline(DeterministicDetectors.All());
        var truePositives = 0;
        var missed = new List<string>();
        var unexpected = new List<string>();

        foreach (var testCase in DetectionCorpus.All())
        {
            var detected = await pipeline.DetectAsync(
                Fixture.Extracted(testCase.Text),
                CancellationToken.None);

            var reported = detected.Entities
                .Select(entity => new DetectionCorpus.Annotation(
                    entity.Type,
                    testCase.Text.Substring(entity.Span.Start, entity.Span.Length)))
                .ToList();

            truePositives += Reconcile(testCase, reported, missed, unexpected);
        }

        return new Measurement(truePositives, missed, unexpected);
    }

    /// <summary>
    /// Pairs what was reported with what was annotated, and records what is left over on
    /// either side.
    /// </summary>
    /// <param name="testCase">The case being measured.</param>
    /// <param name="reported">What the pipeline claimed.</param>
    /// <param name="missed">Collects the annotations nothing matched.</param>
    /// <param name="unexpected">Collects the claims no annotation matched.</param>
    /// <returns>How many claims paired with an annotation.</returns>
    private static int Reconcile(
        DetectionCorpus.Case testCase,
        List<DetectionCorpus.Annotation> reported,
        List<string> missed,
        List<string> unexpected)
    {
        var paired = 0;

        foreach (var expected in testCase.Expected)
        {
            if (reported.Remove(expected))
            {
                paired++;
            }
            else
            {
                missed.Add($"{testCase.Name}: {expected.Type.Value}");
            }
        }

        unexpected.AddRange(reported.Select(claim => $"{testCase.Name}: {claim.Type.Value} '{claim.Value}'"));

        return paired;
    }

    private static string Format(double value) =>
        value.ToString("P1", CultureInfo.InvariantCulture);

    private sealed record Measurement(
        int TruePositives,
        IReadOnlyList<string> Missed,
        IReadOnlyList<string> Unexpected)
    {
        public double Precision => Ratio(TruePositives, TruePositives + Unexpected.Count);

        public double Recall => Ratio(TruePositives, TruePositives + Missed.Count);

        private static double Ratio(int numerator, int denominator) =>
            denominator == 0 ? 1.0 : (double)numerator / denominator;
    }
}
