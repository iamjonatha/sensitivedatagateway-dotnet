using System;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway;

/// <summary>
/// Sanitizes documents and reports what they contain.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole surface an application needs when it is not calling a decision provider:
/// sanitize a bundle and keep the payload, or inspect one and keep the counts. Neither call
/// sends anything anywhere.
/// </para>
/// <para>
/// Every implementation writes exactly one audit record per call, whichever way the call ends.
/// </para>
/// </remarks>
public interface ISensitiveDataGateway
{
    /// <summary>
    /// Sanitizes a bundle under a policy.
    /// </summary>
    /// <param name="bundle">The bundle. Its content is untrusted.</param>
    /// <param name="policy">The policy to apply.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>
    /// The payload, when every check passed, or the report that says which one did not.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="bundle"/> or <paramref name="policy"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ExtractionException">A part could not be read at all.</exception>
    /// <exception cref="ProcessingLimitExceededException">The bundle crosses a bound in the policy.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled or ran out of time.</exception>
    Task<SanitizationResult> SanitizeAsync(
        DocumentBundle bundle,
        SanitizationPolicy policy,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reports what a bundle contains and whether it would pass, without transmitting anything.
    /// </summary>
    /// <param name="bundle">The bundle. Its content is untrusted.</param>
    /// <param name="policy">The policy to evaluate against.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The counts, the statuses and the versions. Never a value out of the document.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="bundle"/> or <paramref name="policy"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ExtractionException">A part could not be read at all.</exception>
    /// <exception cref="ProcessingLimitExceededException">The bundle crosses a bound in the policy.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled or ran out of time.</exception>
    Task<InspectionReport> InspectAsync(
        DocumentBundle bundle,
        SanitizationPolicy policy,
        CancellationToken cancellationToken);
}

/// <summary>
/// Sanitizes documents and hands the result to a decision provider.
/// </summary>
/// <typeparam name="TRequest">The request the provider needs alongside the payload.</typeparam>
/// <typeparam name="TResult">What the provider decides.</typeparam>
/// <remarks>
/// The provider is reached only when every check passed. There is no path from a bundle to a
/// provider that does not go through validation, which is the guarantee the two-interface
/// split makes visible: <see cref="ISensitiveDataGateway"/> has no provider to reach.
/// </remarks>
public interface ISensitiveDataGateway<in TRequest, TResult> : ISensitiveDataGateway
{
    /// <summary>
    /// Sanitizes a bundle and, if it passes, asks the provider to decide.
    /// </summary>
    /// <param name="bundle">The bundle. Its content is untrusted.</param>
    /// <param name="request">The request the provider needs alongside the payload.</param>
    /// <param name="policy">The policy to apply.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>
    /// The decision, when every check passed, or the report that says which one did not. A
    /// blocked result means the provider was never called.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="bundle"/> or <paramref name="policy"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ExtractionException">A part could not be read at all.</exception>
    /// <exception cref="ProcessingLimitExceededException">The bundle crosses a bound in the policy.</exception>
    /// <exception cref="ProviderException">The provider failed.</exception>
    /// <exception cref="OperationCanceledException">The operation was cancelled or ran out of time.</exception>
    Task<GatewayResult<TResult>> ExecuteAsync(
        DocumentBundle bundle,
        TRequest request,
        SanitizationPolicy policy,
        CancellationToken cancellationToken);
}
