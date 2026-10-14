using System;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Auditing;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway;

/// <summary>
/// The gateway over documents, with a decision provider behind it.
/// </summary>
/// <typeparam name="TRequest">The request the provider needs alongside the payload.</typeparam>
/// <typeparam name="TResult">What the provider decides.</typeparam>
/// <remarks>
/// The provider is reached from exactly one place, on exactly one condition: the validator
/// produced a payload. A blocked bundle returns a result with no value, and the provider was
/// never asked.
/// </remarks>
public sealed class DocumentGateway<TRequest, TResult> : ISensitiveDataGateway<TRequest, TResult>
{
    private readonly DocumentGateway _gateway;
    private readonly IDecisionProvider<TRequest, TResult> _provider;

    /// <summary>
    /// Creates a gateway that hands validated payloads to a provider.
    /// </summary>
    /// <param name="gateway">The gateway that does the sanitizing.</param>
    /// <param name="provider">What decides, once there is something safe to decide on.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public DocumentGateway(DocumentGateway gateway, IDecisionProvider<TRequest, TResult> provider)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(provider);

        _gateway = gateway;
        _provider = provider;
    }

    /// <inheritdoc/>
    public Task<SanitizationResult> SanitizeAsync(
        DocumentBundle bundle,
        SanitizationPolicy policy,
        CancellationToken cancellationToken) =>
        _gateway.SanitizeAsync(bundle, policy, cancellationToken);

    /// <inheritdoc/>
    public Task<InspectionReport> InspectAsync(
        DocumentBundle bundle,
        SanitizationPolicy policy,
        CancellationToken cancellationToken) =>
        _gateway.InspectAsync(bundle, policy, cancellationToken);

    /// <inheritdoc/>
    public async Task<GatewayResult<TResult>> ExecuteAsync(
        DocumentBundle bundle,
        TRequest request,
        SanitizationPolicy policy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(policy);

        var pipeline = _gateway.Pipeline;

        using var operation = new GatewayOperation(
            pipeline.Options, AuditOperation.Execute, bundle.Id, policy, cancellationToken);

        try
        {
            var run = await pipeline.RunAsync(bundle, policy, operation).ConfigureAwait(false);
            var result = await DecideAsync(run, request, operation.Token).ConfigureAwait(false);
            var key = pipeline.Options.HashingKey;

            operation.Completed(
                result.IsAllowed ? AuditOutcome.Allowed : AuditOutcome.Blocked,
                run,
                GatewayHashes.Document(key, run.Extracted),
                GatewayHashes.Payload(key, run.Result.Payload));

            return result;
        }
        catch (Exception exception)
        {
            operation.Failed(exception);
            throw;
        }
        finally
        {
            await operation.WriteAsync().ConfigureAwait(false);
        }
    }

    private async Task<GatewayResult<TResult>> DecideAsync(
        PipelineRun run,
        TRequest request,
        CancellationToken cancellationToken)
    {
        var payload = run.Result.Payload;

        if (payload is null)
        {
            return new GatewayResult<TResult>(run.Result.BundleId, isAllowed: false, default, run.Result.Report);
        }

        var value = await _provider.DecideAsync(payload, request, cancellationToken).ConfigureAwait(false);

        return new GatewayResult<TResult>(run.Result.BundleId, isAllowed: true, value, run.Result.Report);
    }
}
