using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SensitiveDataGateway.Auditing;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway;

/// <summary>
/// One call to the gateway, from the clock starting to the audit record being written.
/// </summary>
/// <remarks>
/// <para>
/// The operation owns the processing budget and the audit record, which is why no public
/// method can forget either. It is written exactly once, in a <c>finally</c>, so an operation
/// that threw is as visible as one that succeeded.
/// </para>
/// <para>
/// The record is written with <see cref="CancellationToken.None"/> on purpose: an operation
/// the caller cancelled is precisely the one an auditor wants to find later.
/// </para>
/// </remarks>
internal sealed class GatewayOperation : IDisposable
{
    private readonly GatewayOptions _options;
    private readonly AuditOperation _operation;
    private readonly BundleId _bundleId;
    private readonly SanitizationPolicy _policy;
    private readonly DateTimeOffset _startedAt;
    private readonly long _startedTimestamp;
    private readonly CancellationTokenSource _budget;
    private readonly CancellationTokenSource _linked;

    private AuditOutcome _outcome = AuditOutcome.Unspecified;
    private PipelineRun? _run;
    private string? _documentHash;
    private string? _payloadHash;
    private string? _providerName;
    private GatewayError? _error;

    internal GatewayOperation(
        GatewayOptions options,
        AuditOperation operation,
        BundleId bundleId,
        SanitizationPolicy policy,
        CancellationToken cancellationToken)
    {
        _options = options;
        _operation = operation;
        _bundleId = bundleId;
        _policy = policy;
        _startedAt = options.TimeProvider.GetUtcNow();
        _startedTimestamp = options.TimeProvider.GetTimestamp();
        _budget = new CancellationTokenSource(policy.Limits.MaxProcessingTime, options.TimeProvider);
        _linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _budget.Token);

        GatewayLog.Started(options.Logger, Id, operation, bundleId, policy.Id, policy.Version);
    }

    internal Guid Id { get; } = Guid.NewGuid();

    internal ILogger Logger => _options.Logger;

    internal CancellationToken Token => _linked.Token;

    /// <summary>
    /// Records that the payload was handed to a provider.
    /// </summary>
    /// <param name="providerName">
    /// The provider's name, read when it was composed into the gateway.
    /// </param>
    /// <remarks>
    /// Called from the one place that reaches a provider, so the field means what it says: the
    /// record names a destination when, and only when, content left the gateway. An operation
    /// that was blocked names nobody, because nobody saw it.
    /// </remarks>
    internal void HandedTo(string providerName) => _providerName = providerName;

    internal void Completed(AuditOutcome outcome, PipelineRun run, string? documentHash, string? payloadHash)
    {
        _outcome = outcome;
        _run = run;
        _documentHash = documentHash;
        _payloadHash = payloadHash;

        var elapsed = _options.TimeProvider.GetElapsedTime(_startedTimestamp);

        if (outcome == AuditOutcome.Allowed)
        {
            GatewayLog.Allowed(_options.Logger, Id, _bundleId, elapsed);
            return;
        }

        GatewayLog.Blocked(_options.Logger, Id, _bundleId, run.Result.Report.Findings.Count, elapsed);
    }

    internal void Failed(Exception exception)
    {
        _outcome = exception is OperationCanceledException ? AuditOutcome.Cancelled : AuditOutcome.Failed;
        _error = (exception as SensitiveDataGatewayException)?.Error;

        var elapsed = _options.TimeProvider.GetElapsedTime(_startedTimestamp);

        if (_outcome == AuditOutcome.Cancelled)
        {
            GatewayLog.Cancelled(_options.Logger, Id, _bundleId, elapsed);
            return;
        }

        GatewayLog.Failed(_options.Logger, Id, _bundleId, _error?.Category, elapsed);
    }

    internal async ValueTask WriteAsync()
    {
        var record = new AuditRecord
        {
            OperationId = Id,
            Operation = _operation,
            Outcome = _outcome,
            BundleId = _bundleId,
            PolicyId = _policy.Id,
            PolicyVersion = _policy.Version,
            GatewayVersion = _options.GatewayVersion,
            ProviderName = _providerName,
            StartedAt = _startedAt,
            Duration = _options.TimeProvider.GetElapsedTime(_startedTimestamp),
            EntityCount = _run?.Detected.Entities.Count ?? 0,
            TransformedCount = _run?.Sanitized?.Applied.Count ?? 0,
            GapCount = GapsOf(_run),
            FailedChecks = _run is null ? [] : Inspections.FailedChecksOf(_run.Result.Report),
            ErrorCategory = _error?.Category,
            ErrorCode = _error?.Code,
            DocumentHash = _documentHash,
            PayloadHash = _payloadHash,
        };

        await _options.AuditSink.WriteAsync(record, CancellationToken.None).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _linked.Dispose();
        _budget.Dispose();
    }

    private static int GapsOf(PipelineRun? run) =>
        run is null ? 0 : run.Extracted.Failures.Count + run.Detected.Failures.Count;
}
