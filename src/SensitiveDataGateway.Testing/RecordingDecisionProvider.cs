using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Testing;

/// <summary>
/// Factory helpers for <see cref="RecordingDecisionProvider{TRequest, TResult}"/>.
/// </summary>
public static class RecordingDecisionProvider
{
    /// <summary>
    /// Creates a recorder around a provider that always returns the same result.
    /// </summary>
    /// <typeparam name="TRequest">The request type the provider accepts.</typeparam>
    /// <typeparam name="TResult">The result type the provider returns.</typeparam>
    /// <param name="result">The result to return on every call.</param>
    /// <returns>The recording provider.</returns>
    public static RecordingDecisionProvider<TRequest, TResult> Returning<TRequest, TResult>(TResult result) =>
        new(FakeDecisionProvider.Returning<TRequest, TResult>(result));
}

/// <summary>
/// A decision provider that records every call before delegating to another provider.
/// </summary>
/// <typeparam name="TRequest">The request type the provider accepts.</typeparam>
/// <typeparam name="TResult">The result type the provider returns.</typeparam>
/// <remarks>
/// This is how a test learns what actually crossed the egress boundary. The gateway reports
/// what it decided; this reports what the far side received, which is the only account of
/// the boundary that cannot be wrong about itself.
/// </remarks>
public sealed class RecordingDecisionProvider<TRequest, TResult> : IDecisionProvider<TRequest, TResult>
{
    private readonly List<ProviderCall<TRequest>> _calls = [];
    private readonly IDecisionProvider<TRequest, TResult> _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingDecisionProvider{TRequest, TResult}"/> class.
    /// </summary>
    /// <param name="inner">The provider to record and delegate to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is <see langword="null"/>.</exception>
    public RecordingDecisionProvider(IDecisionProvider<TRequest, TResult> inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <inheritdoc/>
    public string Name => _inner.Name;

    /// <summary>
    /// Gets a snapshot of the calls recorded so far, in the order they arrived.
    /// </summary>
    public IReadOnlyList<ProviderCall<TRequest>> Calls
    {
        get
        {
            lock (_calls)
            {
                return _calls.ToArray();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the provider was called at least once.
    /// </summary>
    public bool WasCalled
    {
        get
        {
            lock (_calls)
            {
                return _calls.Count > 0;
            }
        }
    }

    /// <summary>
    /// Gets the payload of the most recent call, or <see langword="null"/> if there was none.
    /// </summary>
    public ValidatedPayload? LastPayload
    {
        get
        {
            lock (_calls)
            {
                return _calls.Count == 0 ? null : _calls[^1].Payload;
            }
        }
    }

    /// <inheritdoc/>
    public Task<TResult> DecideAsync(
        ValidatedPayload payload,
        TRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        lock (_calls)
        {
            _calls.Add(new ProviderCall<TRequest>(payload, request));
        }

        return _inner.DecideAsync(payload, request, cancellationToken);
    }
}
