using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Testing;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Security.Tests;

/// <summary>
/// Proves that a broken stage stops transmission instead of degrading it.
/// </summary>
/// <remarks>
/// Each scenario replaces exactly one stage with a hostile one and leaves the rest honest,
/// so the outcome is attributable. The policy is the permissive one throughout: the gateway
/// is given every reason to let the document through, and still does not.
/// </remarks>
public sealed class FailClosedTests
{
    [Fact]
    public async Task A_detector_that_throws_stops_the_provider_from_being_called()
    {
        var provider = Harness.Provider();
        var detectors = new List<ISensitiveDataDetector>(DeterministicDetectors.All())
        {
            new ThrowingDetector(),
        };

        var result = await Harness.Over(Harness.WithDetectors(detectors), provider).ExecuteAsync(
            SyntheticCorpus.All()[0].ToBundle(),
            Harness.Request,
            Harness.Permissive(),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsAllowed);
        Assert.False(provider.WasCalled);
        Assert.Contains(PrivacyCheck.ScanCompleteness, result.Report.Findings.Select(f => f.Check));
    }

    [Fact]
    public async Task An_anonymizer_that_rewrites_nothing_is_caught_by_the_validator()
    {
        var provider = Harness.Provider();
        var document = SyntheticCorpus.All()[0];

        var result = await Harness.Over(
            Harness.WithAnonymizer(new PassThroughAnonymizer()),
            provider).ExecuteAsync(
            document.ToBundle(),
            Harness.Request,
            Harness.Permissive(),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsAllowed);
        Assert.False(provider.WasCalled);
        Assert.Contains(PrivacyCheck.AntiLeakScan, result.Report.Findings.Select(f => f.Check));
    }

    [Fact]
    public async Task An_unresolved_entity_the_policy_does_not_tolerate_stops_transmission()
    {
        var provider = Harness.Provider();
        var policy = TestPolicy.CreateBuilder()
            .WithDefaultAction(TransformationAction.Pseudonymize)
            .WithUnknownEntityAction(TransformationAction.Pseudonymize)
            .WithDefaultThresholds(ConfidenceThresholds.Create(0.999, 0.999))
            .TolerateNoUnresolved()
            .Build();

        var result = await Harness.Over(Harness.Default(), provider).ExecuteAsync(
            SyntheticCorpus.All()[0].ToBundle(),
            Harness.Request,
            policy,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsAllowed);
        Assert.False(provider.WasCalled);
        Assert.Contains(PrivacyCheck.UnresolvedEntities, result.Report.Findings.Select(f => f.Check));
    }

    /// <summary>
    /// An oversized part is refused rather than cut down to size.
    /// </summary>
    /// <returns>A task that completes when the scenario has run.</returns>
    /// <remarks>
    /// Truncation would be the friendly behaviour and the wrong one: the part that did not
    /// fit is the part nobody scanned, and a document that was only partly read is a
    /// document nobody can make a statement about.
    /// </remarks>
    [Fact]
    public async Task An_oversized_part_is_refused_and_never_truncated()
    {
        var provider = Harness.Provider();
        var policy = TestPolicy.Within(ProcessingLimits.Create(
            maxBundleBytes: 512,
            maxPartBytes: 256,
            maxPartCount: 4,
            maxExtractedCharacters: 256,
            maxEntities: 32,
            maxProcessingTime: TimeSpan.FromSeconds(5)));

        var failure = await Assert.ThrowsAsync<ProcessingLimitExceededException>(
            async () => await Harness.Over(Harness.Default(), provider).ExecuteAsync(
                TestBundle.OfSize(4096),
                Harness.Request,
                policy,
                TestContext.Current.CancellationToken));

        Assert.Equal(GatewayErrorCategory.LimitExceeded, failure.Error.Category);
        Assert.False(provider.WasCalled);
    }

    [Fact]
    public async Task A_blocked_result_refuses_to_hand_over_a_value()
    {
        var result = await Harness.Over(
            Harness.WithAnonymizer(new PassThroughAnonymizer()),
            Harness.Provider()).ExecuteAsync(
            SyntheticCorpus.All()[0].ToBundle(),
            Harness.Request,
            Harness.Permissive(),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsAllowed);
        Assert.Null(result.Value);
        _ = Assert.Throws<EgressBlockedException>(result.GetValueOrThrow);
    }
}
