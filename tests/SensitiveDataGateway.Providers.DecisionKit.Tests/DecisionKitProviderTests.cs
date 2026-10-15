using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DecisionKit.Errors;
using DecisionKit.Questions;
using DecisionKit.Results;
using DecisionKit.Testing.Assertions;
using SensitiveDataGateway.Testing;
using DecisionEngineFake = DecisionKit.Testing.Providers.FakeDecisionProvider;

namespace SensitiveDataGateway.Providers.DecisionKit.Tests;

/// <summary>
/// What the adapter puts on a DecisionKit request, and what it refuses to.
/// </summary>
public sealed class DecisionKitProviderTests
{
    private const string Body = "First paragraph.\n\nSecond paragraph.";

    [Fact]
    public void Rejects_a_missing_engine() =>
        Assert.Throws<ArgumentNullException>(static () => new DecisionKitProvider(null!));

    [Fact]
    public void Rejects_a_payload_format_that_does_not_exist() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            static () => new DecisionKitProvider(Harness.Engine(), (DecisionKitPayloadFormat)42));

    [Fact]
    public void Keeps_the_name_of_the_engine_behind_it() =>
        Assert.Equal(
            $"decisionkit:{Harness.EngineName}",
            new DecisionKitProvider(Harness.Engine()).Name);

    [Fact]
    public async Task Sends_the_segments_as_one_block_of_text()
    {
        var engine = Harness.Engine();
        var recorder = Harness.Recorder(engine, DecisionKitPayloadFormat.Text);

        await Harness.Over(recorder).ExecuteAsync(
            TestBundle.FromText(Body),
            Harness.Questions,
            Harness.Policy,
            TestContext.Current.CancellationToken);

        var expected = string.Join("\n\n", recorder.LastPayload!.Content.Select(static s => s.Text));

        Assert.Equal(expected, engine.LastRequest!.Input.Text);
        Assert.Empty(engine.LastRequest.Input.Properties);
    }

    [Fact]
    public async Task Sends_one_property_per_segment_when_asked_to()
    {
        var engine = Harness.Engine();
        var recorder = Harness.Recorder(engine, DecisionKitPayloadFormat.Segments);

        await Harness.Over(recorder).ExecuteAsync(
            TestBundle.FromText(Body),
            Harness.Questions,
            Harness.Policy,
            TestContext.Current.CancellationToken);

        var properties = engine.LastRequest!.Input.Properties;
        var content = recorder.LastPayload!.Content;

        Assert.Null(engine.LastRequest.Input.Text);
        Assert.Equal(content.Count, properties.Count);
        Assert.All(content, segment => Assert.Equal(segment.Text, properties[segment.Id.Value]));
    }

    [Fact]
    public async Task Carries_the_bundle_identifier_as_the_client_request_id()
    {
        var engine = Harness.Engine();
        var recorder = Harness.Recorder(engine, DecisionKitPayloadFormat.Text);

        await Harness.Over(recorder).ExecuteAsync(
            TestBundle.FromText(Body),
            Harness.Questions,
            Harness.Policy,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            recorder.LastPayload!.BundleId.Value,
            engine.LastRequest!.ClientRequestId?.Value);
    }

    [Fact]
    public async Task Carries_the_provenance_as_metadata()
    {
        var engine = Harness.Engine();
        var recorder = Harness.Recorder(engine, DecisionKitPayloadFormat.Text);

        await Harness.Over(recorder).ExecuteAsync(
            TestBundle.FromText(Body),
            Harness.Questions,
            Harness.Policy,
            TestContext.Current.CancellationToken);

        var payload = recorder.LastPayload!;
        var metadata = engine.LastRequest!.Metadata;

        Assert.Equal(payload.Provenance.PolicyId.Value, metadata[DecisionKitMetadataKeys.PolicyId]);
        Assert.Equal(payload.Provenance.PolicyVersion.Value, metadata[DecisionKitMetadataKeys.PolicyVersion]);
        Assert.Equal(payload.Provenance.GatewayVersion, metadata[DecisionKitMetadataKeys.GatewayVersion]);
        Assert.Equal(payload.ContentHash, metadata[DecisionKitMetadataKeys.ContentHash]);
        Assert.Equal(
            payload.Provenance.SanitizedAt.ToString("O", CultureInfo.InvariantCulture),
            metadata[DecisionKitMetadataKeys.SanitizedAt]);
    }

    [Fact]
    public async Task Asks_the_questions_it_was_given()
    {
        var engine = Harness.Engine();

        await Harness.Over(Harness.Recorder(engine, DecisionKitPayloadFormat.Text)).ExecuteAsync(
            TestBundle.FromText(Body),
            Harness.Questions,
            Harness.Policy,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, engine.CallCount);
        Assert.Equal(Harness.Risk.Id, Assert.Single(engine.LastRequest!.Questions).Id);
    }

    /// <summary>
    /// A failed decision stays a failed decision.
    /// </summary>
    /// <returns>A task that completes when the scenario has run.</returns>
    /// <remarks>
    /// The category and the retry hint on a DecisionKit failure are what the application
    /// needs in order to decide whether to try again, so the adapter must not swallow them
    /// or replace them with something of its own.
    /// </remarks>
    [Fact]
    public async Task Lets_a_failed_decision_through_with_its_category()
    {
        var engine = new DecisionEngineFake(Harness.EngineName);
        engine.Fails(DecisionErrorCategory.RateLimit, "slow down");

        var failure = await Assert.ThrowsAsync<ProviderException>(
            () => Harness.Over(Harness.Recorder(engine, DecisionKitPayloadFormat.Text)).ExecuteAsync(
                TestBundle.FromText(Body),
                Harness.Questions,
                Harness.Policy,
                TestContext.Current.CancellationToken));

        var inner = Assert.IsAssignableFrom<DecisionException>(failure.InnerException);

        Assert.Equal(DecisionErrorCategory.RateLimit, inner.Category);
    }

    /// <summary>
    /// The decision comes back as the engine made it.
    /// </summary>
    /// <returns>A task that completes when the scenario has run.</returns>
    /// <remarks>
    /// The adapter is a boundary, not an interpreter. It must not rescale, round or
    /// reinterpret an answer on the way back, because an application that cannot trust the
    /// number it reads has no reason to run the engine.
    /// </remarks>
    [Fact]
    public async Task Returns_the_decision_untouched()
    {
        var engine = Harness.Engine();

        var result = await Harness.Over(Harness.Recorder(engine, DecisionKitPayloadFormat.Text)).ExecuteAsync(
            TestBundle.FromText(Body),
            Harness.Questions,
            Harness.Policy,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
        DecisionAssert.HasProbability(result.GetValueOrThrow(), Harness.Risk, 0.5);
    }

    [Fact]
    public async Task Does_not_reach_the_engine_when_the_gateway_blocks()
    {
        var engine = Harness.Engine();

        var result = await Harness.Over(Harness.Recorder(engine, DecisionKitPayloadFormat.Text)).ExecuteAsync(
            TestBundle.FromText("Contact mario.rossi@example.com for details."),
            Harness.Questions,
            TestPolicy.Blocking,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsAllowed);
        Assert.Equal(0, engine.CallCount);
    }
}
