using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway;

/// <summary>
/// Something that makes a decision from sanitized content.
/// </summary>
/// <typeparam name="TRequest">What the caller wants decided.</typeparam>
/// <typeparam name="TResult">What the provider decides.</typeparam>
/// <remarks>
/// <para>
/// This is the trust boundary, expressed as a type. A provider is handed a
/// <see cref="ValidatedPayload"/> and cannot be handed anything else: no bundle, no extracted
/// text, no detected entities, no pseudonym map. The guarantee is not that providers are
/// careful; it is that there is no parameter through which raw content could arrive.
/// </para>
/// <para>
/// The abstraction is the gateway's own. Rule engines, inference services and scoring APIs
/// are integrations behind it, never the shape it is built on. That is what keeps a
/// deployment from being unable to change providers without changing the pipeline.
/// </para>
/// <para>
/// <typeparamref name="TRequest"/> is the caller's own question — a rule set to evaluate, a
/// prompt template, a model name — and it is the caller's responsibility not to smuggle
/// document content into it. That is the one leak this interface cannot prevent, and it is
/// documented rather than hidden.
/// </para>
/// </remarks>
public interface IDecisionProvider<TRequest, TResult>
{
    /// <summary>
    /// Gets the name of the provider, for the audit record.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Decides.
    /// </summary>
    /// <param name="payload">The sanitized, validated content.</param>
    /// <param name="request">What the caller wants decided.</param>
    /// <param name="cancellationToken">A token that cancels the call.</param>
    /// <returns>The decision.</returns>
    /// <exception cref="ProviderException">
    /// The provider failed. The failure is reported as coming from outside the trust
    /// boundary, with the provider's own exception preserved rather than flattened.
    /// </exception>
    Task<TResult> DecideAsync(ValidatedPayload payload, TRequest request, CancellationToken cancellationToken);
}
