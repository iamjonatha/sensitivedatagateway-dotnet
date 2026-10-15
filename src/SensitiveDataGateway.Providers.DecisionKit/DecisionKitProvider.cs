using System;
using System.Threading;
using System.Threading.Tasks;
using DecisionKit.Errors;
using DecisionKit.Providers;
using DecisionKit.Questions;
using DecisionKit.Results;
using SensitiveDataGateway.Validation;
using DecisionEngine = DecisionKit.Providers.IDecisionProvider;

namespace SensitiveDataGateway.Providers.DecisionKit;

/// <summary>
/// Sends a validated payload to a DecisionKit decision provider.
/// </summary>
/// <remarks>
/// <para>
/// This is the only adapter between the two libraries, and it is deliberately thin. It maps
/// a <see cref="ValidatedPayload"/> onto a DecisionKit request, hands that request to the
/// provider the application composed, and returns what comes back. It interprets no answer,
/// retries nothing, and opens no socket: whatever network the decision needs belongs to the
/// DecisionKit provider behind it.
/// </para>
/// <para>
/// <b>The questions are the caller's responsibility.</b> The gateway sanitizes the document,
/// not the question set the caller passes alongside it. A question whose text quotes a
/// customer's name sends that name to the decision provider, and no stage of this pipeline
/// can see it happen. Build question text from constants, never from the document.
/// </para>
/// <para>
/// Failures are left alone. A <see cref="DecisionException"/> carries a
/// category and a retry hint that the application needs in order to decide what to do next,
/// so it propagates as thrown; the gateway wraps it in a
/// <see cref="ProviderException"/> without flattening it.
/// </para>
/// </remarks>
public sealed class DecisionKitProvider : IDecisionProvider<QuestionSet, DecisionResult>
{
    private readonly DecisionEngine _inner;
    private readonly DecisionKitPayloadFormat _payloadFormat;

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionKitProvider"/> class that sends
    /// the sanitized content as one block of text.
    /// </summary>
    /// <param name="inner">The DecisionKit provider that makes the decision.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is <see langword="null"/>.</exception>
    public DecisionKitProvider(DecisionEngine inner)
        : this(inner, DecisionKitPayloadFormat.Text)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecisionKitProvider"/> class.
    /// </summary>
    /// <param name="inner">The DecisionKit provider that makes the decision.</param>
    /// <param name="payloadFormat">How the sanitized content is presented.</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="payloadFormat"/> is not a defined value.</exception>
    public DecisionKitProvider(DecisionEngine inner, DecisionKitPayloadFormat payloadFormat)
    {
        ArgumentNullException.ThrowIfNull(inner);

        if (!Enum.IsDefined(payloadFormat))
        {
            throw new ArgumentOutOfRangeException(nameof(payloadFormat));
        }

        _inner = inner;
        _payloadFormat = payloadFormat;
    }

    /// <summary>
    /// Gets the name of this provider, for the audit record.
    /// </summary>
    /// <remarks>
    /// The name of the DecisionKit provider behind the adapter is kept, because an audit
    /// record that said only <c>decisionkit</c> would not tell a reader which engine
    /// actually answered.
    /// </remarks>
    public string Name => string.Concat("decisionkit:", _inner.Name);

    /// <summary>
    /// Asks the DecisionKit provider to decide.
    /// </summary>
    /// <param name="payload">The sanitized, validated content.</param>
    /// <param name="request">The questions to answer.</param>
    /// <param name="cancellationToken">A token to cancel the decision.</param>
    /// <returns>The result the DecisionKit provider produced.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="payload"/> or <paramref name="request"/> is <see langword="null"/>.
    /// </exception>
    public Task<DecisionResult> DecideAsync(
        ValidatedPayload payload,
        QuestionSet request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(request);

        var decision = new DecisionRequest(request)
        {
            Input = PayloadMapping.Input(payload, _payloadFormat),
            ClientRequestId = PayloadMapping.ClientRequestId(payload),
            Metadata = PayloadMapping.Metadata(payload),
        };

        return _inner.DecideAsync(decision, cancellationToken);
    }
}
