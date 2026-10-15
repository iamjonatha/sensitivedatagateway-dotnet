using System;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Testing;

/// <summary>
/// Factory helpers for <see cref="FakeDecisionProvider{TRequest, TResult}"/>.
/// </summary>
public static class FakeDecisionProvider
{
    /// <summary>
    /// Creates a provider that always returns the same result.
    /// </summary>
    /// <typeparam name="TRequest">The request type the provider accepts.</typeparam>
    /// <typeparam name="TResult">The result type the provider returns.</typeparam>
    /// <param name="result">The result to return on every call.</param>
    /// <returns>The provider.</returns>
    public static FakeDecisionProvider<TRequest, TResult> Returning<TRequest, TResult>(TResult result) =>
        new("fake", (_, _) => result, failure: null);

    /// <summary>
    /// Creates a provider that computes its result from the call.
    /// </summary>
    /// <typeparam name="TRequest">The request type the provider accepts.</typeparam>
    /// <typeparam name="TResult">The result type the provider returns.</typeparam>
    /// <param name="decide">The function to invoke on every call.</param>
    /// <returns>The provider.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="decide"/> is <see langword="null"/>.</exception>
    public static FakeDecisionProvider<TRequest, TResult> Deciding<TRequest, TResult>(
        Func<ValidatedPayload, TRequest, TResult> decide)
    {
        ArgumentNullException.ThrowIfNull(decide);
        return new FakeDecisionProvider<TRequest, TResult>("fake", decide, failure: null);
    }

    /// <summary>
    /// Creates a provider that throws on every call.
    /// </summary>
    /// <typeparam name="TRequest">The request type the provider accepts.</typeparam>
    /// <typeparam name="TResult">The result type the provider returns.</typeparam>
    /// <param name="failure">The exception to throw.</param>
    /// <returns>The provider.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is <see langword="null"/>.</exception>
    public static FakeDecisionProvider<TRequest, TResult> Failing<TRequest, TResult>(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new FakeDecisionProvider<TRequest, TResult>("fake", decide: null, failure);
    }
}

/// <summary>
/// A decision provider that performs no work: it returns what it was told to return, or
/// throws what it was told to throw.
/// </summary>
/// <typeparam name="TRequest">The request type the provider accepts.</typeparam>
/// <typeparam name="TResult">The result type the provider returns.</typeparam>
/// <remarks>
/// <para>
/// This type opens no socket, reads no file and holds no credential. It exists so that a
/// test can exercise the gateway without a real decision engine, and so that a test can
/// make a provider fail on demand without waiting for a real one to.
/// </para>
/// <para>
/// To assert on what the provider received, wrap this in a
/// <see cref="RecordingDecisionProvider{TRequest, TResult}"/>.
/// </para>
/// </remarks>
public sealed class FakeDecisionProvider<TRequest, TResult> : IDecisionProvider<TRequest, TResult>
{
    private readonly Func<ValidatedPayload, TRequest, TResult>? _decide;
    private readonly Exception? _failure;

    internal FakeDecisionProvider(
        string name,
        Func<ValidatedPayload, TRequest, TResult>? decide,
        Exception? failure)
    {
        Name = name;
        _decide = decide;
        _failure = failure;
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <summary>
    /// Returns a copy of this provider under a different name.
    /// </summary>
    /// <param name="name">The name the gateway reports in audit records and errors.</param>
    /// <returns>The renamed provider.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or white space.</exception>
    public FakeDecisionProvider<TRequest, TResult> Named(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new FakeDecisionProvider<TRequest, TResult>(name, _decide, _failure);
    }

    /// <inheritdoc/>
    public Task<TResult> DecideAsync(
        ValidatedPayload payload,
        TRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        cancellationToken.ThrowIfCancellationRequested();

        return _failure is not null
            ? Task.FromException<TResult>(_failure)
            : Task.FromResult(_decide!(payload, request));
    }
}
