using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Auditing;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Tests.Gateway;

public sealed class DocumentGatewayTests
{
    private const string CleanText = "The quarterly report is ready for the review meeting.";

    private const string SensitiveText =
        "Contact anna.verdi@example.invalid or marco.rossi@example.invalid about the renewal.";

    [Fact]
    public async Task Allows_a_document_the_policy_accepts()
    {
        var sink = new RecordingAuditSink();

        var result = await GatewayFixture.Gateway(sink)
            .SanitizeAsync(GatewayFixture.Bundle(CleanText), SanitizationPolicy.Balanced, TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
        Assert.NotNull(result.Payload);
        Assert.NotEmpty(result.Payload.Content);
    }

    [Fact]
    public async Task Pseudonymizes_what_the_policy_asks_it_to_pseudonymize()
    {
        var sink = new RecordingAuditSink();

        var result = await GatewayFixture.Gateway(sink)
            .SanitizeAsync(GatewayFixture.Bundle(SensitiveText), SanitizationPolicy.Balanced, TestContext.Current.CancellationToken);

        var text = string.Join('\n', result.Payload!.Content.Select(segment => segment.Text));

        Assert.True(result.IsAllowed);
        Assert.DoesNotContain("anna.verdi@example.invalid", text, StringComparison.Ordinal);
        Assert.DoesNotContain("marco.rossi@example.invalid", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Blocks_a_document_it_could_not_read_under_a_strict_policy()
    {
        var sink = new RecordingAuditSink();

        var result = await GatewayFixture.Gateway(sink)
            .SanitizeAsync(GatewayFixture.Unreadable(), SanitizationPolicy.Strict, TestContext.Current.CancellationToken);

        Assert.False(result.IsAllowed);
        Assert.Null(result.Payload);
    }

    [Fact]
    public async Task Writes_exactly_one_audit_record_for_an_allowed_operation()
    {
        var sink = new RecordingAuditSink();

        await GatewayFixture.Gateway(sink)
            .SanitizeAsync(GatewayFixture.Bundle(CleanText), SanitizationPolicy.Balanced, TestContext.Current.CancellationToken);

        var record = Assert.Single(sink.Records);

        Assert.Equal(AuditOperation.Sanitize, record.Operation);
        Assert.Equal(AuditOutcome.Allowed, record.Outcome);
        Assert.Equal(SanitizationPolicy.Balanced.Id, record.PolicyId);
        Assert.NotEqual(Guid.Empty, record.OperationId);
    }

    [Fact]
    public async Task Writes_exactly_one_audit_record_for_a_blocked_operation()
    {
        var sink = new RecordingAuditSink();

        await GatewayFixture.Gateway(sink)
            .SanitizeAsync(GatewayFixture.Unreadable(), SanitizationPolicy.Strict, TestContext.Current.CancellationToken);

        var record = Assert.Single(sink.Records);

        Assert.Equal(AuditOutcome.Blocked, record.Outcome);
        Assert.NotEmpty(record.FailedChecks);
    }

    [Fact]
    public async Task Writes_exactly_one_audit_record_when_a_limit_stops_the_operation()
    {
        var sink = new RecordingAuditSink();
        var policy = GatewayFixture.TightLimits(maxEntities: 1);

        var exception = await Assert.ThrowsAsync<ProcessingLimitExceededException>(
            () => GatewayFixture.Gateway(sink)
                .SanitizeAsync(GatewayFixture.Bundle(SensitiveText), policy, TestContext.Current.CancellationToken));

        var record = Assert.Single(sink.Records);

        Assert.Equal(AuditOutcome.Failed, record.Outcome);
        Assert.Equal(exception.Error.Code, record.ErrorCode);
        Assert.Equal(GatewayErrorCategory.LimitExceeded, record.ErrorCategory);
    }

    [Fact]
    public async Task Writes_exactly_one_audit_record_when_the_caller_cancels()
    {
        var sink = new RecordingAuditSink();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => GatewayFixture.Gateway(sink)
                .SanitizeAsync(GatewayFixture.Bundle(CleanText), SanitizationPolicy.Balanced, cancellation.Token));

        var record = Assert.Single(sink.Records);

        Assert.Equal(AuditOutcome.Cancelled, record.Outcome);
    }

    [Fact]
    public async Task Writes_an_inspection_audit_record_that_carries_no_payload_digest()
    {
        var sink = new RecordingAuditSink();
        var options = GatewayFixture.Options(sink).WithHashing(HashingKey.CreateEphemeral());

        var report = await GatewayFixture.Gateway(options)
            .InspectAsync(GatewayFixture.Bundle(SensitiveText), SanitizationPolicy.Balanced, TestContext.Current.CancellationToken);

        var record = Assert.Single(sink.Records);

        Assert.Equal(AuditOperation.Inspect, record.Operation);
        Assert.NotNull(record.DocumentHash);
        Assert.Null(record.PayloadHash);
        Assert.True(report.WouldBeAllowed);
        Assert.True(report.SegmentCount > 0);
    }

    [Fact]
    public async Task Carries_no_digest_unless_the_application_supplied_a_key()
    {
        var sink = new RecordingAuditSink();

        await GatewayFixture.Gateway(sink)
            .SanitizeAsync(GatewayFixture.Bundle(CleanText), SanitizationPolicy.Balanced, TestContext.Current.CancellationToken);

        var record = Assert.Single(sink.Records);

        Assert.Null(record.DocumentHash);
        Assert.Null(record.PayloadHash);
    }

    [Fact]
    public async Task Digests_the_same_document_to_the_same_value_and_a_different_one_otherwise()
    {
        var key = HashingKey.CreateEphemeral();
        var sink = new RecordingAuditSink();
        var gateway = GatewayFixture.Gateway(GatewayFixture.Options(sink).WithHashing(key));

        await gateway.SanitizeAsync(GatewayFixture.Bundle(CleanText), SanitizationPolicy.Balanced, TestContext.Current.CancellationToken);
        await gateway.SanitizeAsync(GatewayFixture.Bundle("other", CleanText), SanitizationPolicy.Balanced, TestContext.Current.CancellationToken);
        await gateway.SanitizeAsync(GatewayFixture.Bundle("third", SensitiveText), SanitizationPolicy.Balanced, TestContext.Current.CancellationToken);

        Assert.Equal(sink.Records[0].DocumentHash, sink.Records[1].DocumentHash);
        Assert.NotEqual(sink.Records[0].DocumentHash, sink.Records[2].DocumentHash);
        Assert.NotNull(sink.Records[0].PayloadHash);
    }

    [Fact]
    public async Task Surfaces_an_audit_sink_failure_instead_of_hiding_it()
    {
        var gateway = DocumentGateway.CreateDefault(GatewayOptions.Default.WithAuditSink(new FailingAuditSink()));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => gateway.SanitizeAsync(GatewayFixture.Bundle(CleanText), SanitizationPolicy.Balanced, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Tolerates_the_sink_that_does_nothing()
    {
        Assert.Same(NullAuditSink.Instance, GatewayOptions.Default.AuditSink);
    }
}

public sealed class DecisionGatewayTests
{
    private const string CleanText = "The quarterly report is ready for the review meeting.";

    [Fact]
    public async Task Reaches_the_provider_with_the_validated_payload()
    {
        var sink = new RecordingAuditSink();
        var provider = new RecordingProvider();
        var gateway = new DocumentGateway<string, string>(GatewayFixture.Gateway(sink), provider);

        var result = await gateway.ExecuteAsync(
            GatewayFixture.Bundle(CleanText),
            "request",
            SanitizationPolicy.Balanced,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
        Assert.Equal(1, provider.Calls);
        Assert.NotNull(provider.LastPayload);
        Assert.Equal(BundleId.Create("bundle"), provider.LastPayload.BundleId);

        var record = Assert.Single(sink.Records);

        Assert.Equal(AuditOperation.Execute, record.Operation);
        Assert.Equal(AuditOutcome.Allowed, record.Outcome);
    }

    [Fact]
    public async Task Never_reaches_the_provider_when_the_document_is_blocked()
    {
        var sink = new RecordingAuditSink();
        var provider = new RecordingProvider();
        var gateway = new DocumentGateway<string, string>(GatewayFixture.Gateway(sink), provider);

        var result = await gateway.ExecuteAsync(
            GatewayFixture.Unreadable(),
            "request",
            SanitizationPolicy.Strict,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsAllowed);
        Assert.Equal(0, provider.Calls);
        Assert.Equal(AuditOutcome.Blocked, Assert.Single(sink.Records).Outcome);
    }

    [Fact]
    public async Task Inspects_without_reaching_the_provider()
    {
        var sink = new RecordingAuditSink();
        var provider = new RecordingProvider();
        var gateway = new DocumentGateway<string, string>(GatewayFixture.Gateway(sink), provider);

        await gateway.InspectAsync(GatewayFixture.Bundle(CleanText), SanitizationPolicy.Balanced, TestContext.Current.CancellationToken);

        Assert.Equal(0, provider.Calls);
        Assert.Equal(AuditOperation.Inspect, Assert.Single(sink.Records).Operation);
    }

    /// <summary>
    /// A provider that breaks its contract is still reported as a provider failure.
    /// </summary>
    /// <returns>A task that completes when the scenario has run.</returns>
    /// <remarks>
    /// The interface asks a provider to throw <see cref="ProviderException"/>. A provider is
    /// third-party code and may throw anything at all, so the gateway translates whatever it
    /// gets, keeping the original as the inner exception. Without this, the audit record of
    /// an outage carries no category and nothing downstream can tell a provider failure from
    /// a failure of the gateway itself.
    /// </remarks>
    [Fact]
    public async Task Reports_a_failing_provider_as_a_provider_failure()
    {
        var sink = new RecordingAuditSink();
        var gateway = new DocumentGateway<string, string>(GatewayFixture.Gateway(sink), new ThrowingProvider());

        var failure = await Assert.ThrowsAsync<ProviderException>(
            async () => await gateway.ExecuteAsync(
                GatewayFixture.Bundle(CleanText),
                "request",
                SanitizationPolicy.Balanced,
                TestContext.Current.CancellationToken));

        Assert.Equal(GatewayErrorCategory.ProviderFailed, failure.Error.Category);
        Assert.Equal(BundleId.Create("bundle"), failure.Error.BundleId);
        _ = Assert.IsType<InvalidOperationException>(failure.InnerException);

        var record = Assert.Single(sink.Records);

        Assert.Equal(AuditOutcome.Failed, record.Outcome);
        Assert.Equal(GatewayErrorCategory.ProviderFailed, record.ErrorCategory);
    }

    /// <summary>
    /// A cancelled decision is cancellation, not a provider failure.
    /// </summary>
    /// <returns>A task that completes when the scenario has run.</returns>
    [Fact]
    public async Task Leaves_a_cancelled_decision_as_cancellation()
    {
        var sink = new RecordingAuditSink();
        var gateway = new DocumentGateway<string, string>(GatewayFixture.Gateway(sink), new CancellingProvider());

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await gateway.ExecuteAsync(
                GatewayFixture.Bundle(CleanText),
                "request",
                SanitizationPolicy.Balanced,
                TestContext.Current.CancellationToken));

        Assert.Equal(AuditOutcome.Cancelled, Assert.Single(sink.Records).Outcome);
    }

    /// <summary>
    /// A provider that already honours the contract is passed through unchanged.
    /// </summary>
    /// <returns>A task that completes when the scenario has run.</returns>
    [Fact]
    public async Task Does_not_wrap_a_failure_the_provider_already_reported_correctly()
    {
        var error = GatewayError.Create(
            GatewayErrorCategory.ProviderFailed,
            "provider.quota",
            "The provider refused the request.");
        var provider = FakeProvider.Failing(new ProviderException(error));
        var gateway = new DocumentGateway<string, string>(
            GatewayFixture.Gateway(new RecordingAuditSink()),
            provider);

        var failure = await Assert.ThrowsAsync<ProviderException>(
            async () => await gateway.ExecuteAsync(
                GatewayFixture.Bundle(CleanText),
                "request",
                SanitizationPolicy.Balanced,
                TestContext.Current.CancellationToken));

        Assert.Equal("provider.quota", failure.Error.Code);
        Assert.Null(failure.InnerException);
    }
}
