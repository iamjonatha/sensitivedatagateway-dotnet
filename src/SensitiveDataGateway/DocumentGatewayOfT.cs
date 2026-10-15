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
    private readonly string _providerName;

    /// <summary>
    /// Creates a gateway that hands validated payloads to a provider.
    /// </summary>
    /// <param name="gateway">The gateway that does the sanitizing.</param>
    /// <param name="provider">What decides, once there is something safe to decide on.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="provider"/> has no name. The audit record has no other way to say where
    /// a payload went, so an unnamed provider is refused at composition rather than producing
    /// records nobody can attribute.
    /// </exception>
    /// <remarks>
    /// The provider's name is read here, once, and before any document has been seen. A
    /// provider that built its name from content would have nothing to build it from yet.
    /// </remarks>
    public DocumentGateway(DocumentGateway gateway, IDecisionProvider<TRequest, TResult> provider)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(provider);

        var name = provider.Name;
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(provider));

        _gateway = gateway;
        _provider = provider;
        _providerName = name;
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
            var result = await DecideAsync(run, request, operation).ConfigureAwait(false);
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
        GatewayOperation operation)
    {
        var payload = run.Result.Payload;

        if (payload is null)
        {
            return new GatewayResult<TResult>(run.Result.BundleId, isAllowed: false, default, run.Result.Report);
        }

        var value = await InvokeAsync(payload, request, operation).ConfigureAwait(false);

        return new GatewayResult<TResult>(run.Result.BundleId, isAllowed: true, value, run.Result.Report);
    }

    /// <summary>
    /// Calls the provider and reports whatever it throws as a provider failure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the only place a provider is reached, which is why it is also the place the
    /// audit record learns the payload left: a record that names a provider is a record of
    /// content that crossed the boundary, including when the call then failed.
    /// </para>
    /// <para>
    /// A provider is outside the trust boundary and cannot be relied on to throw
    /// <see cref="ProviderException"/> as the interface asks. Translating here is what keeps
    /// the audit record of an outage attributable: without it, the record carries no error
    /// category and nothing downstream can tell a provider failure apart from a failure of
    /// the gateway itself. The original exception is preserved rather than flattened, and
    /// the description says nothing the provider was looking at.
    /// </para>
    /// </remarks>
    private async Task<TResult> InvokeAsync(
        ValidatedPayload payload,
        TRequest request,
        GatewayOperation operation)
    {
        operation.HandedTo(_providerName);

        try
        {
            return await _provider.DecideAsync(payload, request, operation.Token).ConfigureAwait(false);
        }
        catch (SensitiveDataGatewayException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A provider may throw anything; the failure is translated, not swallowed.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            throw new ProviderException(
                GatewayError.Create(
                    GatewayErrorCategory.ProviderFailed,
                    "provider.failed",
                    "The decision provider did not return a decision.")
                    .InBundle(payload.BundleId),
                exception);
        }
    }
}
