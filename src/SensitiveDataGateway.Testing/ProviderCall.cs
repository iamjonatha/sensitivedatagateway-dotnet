using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Testing;

/// <summary>
/// One call a decision provider received, recorded by <see cref="RecordingDecisionProvider{TRequest, TResult}"/>.
/// </summary>
/// <typeparam name="TRequest">The request type the provider accepts.</typeparam>
public sealed class ProviderCall<TRequest>
{
    internal ProviderCall(ValidatedPayload payload, TRequest request)
    {
        Payload = payload;
        Request = request;
    }

    /// <summary>
    /// Gets the payload the gateway handed to the provider.
    /// </summary>
    /// <remarks>
    /// This is the only content that crossed the egress boundary on this call. An assertion
    /// about what did not leave should be made against this object and nothing else.
    /// </remarks>
    public ValidatedPayload Payload { get; }

    /// <summary>
    /// Gets the caller's request, which the gateway passes through untouched.
    /// </summary>
    /// <remarks>
    /// The gateway never inspects, sanitizes or validates this value. Anything a test puts
    /// in it reaches the provider verbatim, by design.
    /// </remarks>
    public TRequest Request { get; }

    /// <summary>
    /// Returns a description that carries no document content.
    /// </summary>
    /// <returns>The bundle identifier and the number of segments in the payload.</returns>
    public override string ToString() =>
        $"ProviderCall(bundle: {Payload.BundleId}, segments: {Payload.Content.Count})";
}
