using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Auditing;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway;

/// <summary>
/// The gateway over documents.
/// </summary>
/// <remarks>
/// <para>
/// It reads a bundle, finds what is sensitive, applies a policy, rewrites the text and checks
/// the result before anyone may have it. The checking is not optional and not configurable:
/// <see cref="SanitizationResult.Payload"/> exists only when every check passed.
/// </para>
/// <para>
/// This type is named for what it gates, not for the library. A gateway over something other
/// than a document bundle would be a different type with the same interface.
/// </para>
/// </remarks>
public sealed class DocumentGateway : ISensitiveDataGateway
{
    /// <summary>
    /// Creates a gateway over a set of stages.
    /// </summary>
    /// <param name="extraction">Turns a bundle into text.</param>
    /// <param name="detection">Finds candidates in the text.</param>
    /// <param name="policyEngine">Decides what happens to each candidate.</param>
    /// <param name="anonymizer">Rewrites the text.</param>
    /// <param name="options">What the gateway itself does: audit, clock, version.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <remarks>
    /// The privacy validator is not a parameter, and there is no overload that takes one. It
    /// is the stage that decides whether anything may leave, so a gateway assembled from
    /// caller-supplied parts still checks its own output with the validator this library
    /// ships. A replaceable validator would make <see cref="ValidatedPayload"/> mean
    /// "somebody said this was checked" rather than "this library checked it".
    /// </remarks>
    public DocumentGateway(
        ExtractionPipeline extraction,
        DetectionPipeline detection,
        IPolicyEngine policyEngine,
        IDocumentAnonymizer anonymizer,
        GatewayOptions options)
        : this(new GatewayPipeline(extraction, detection, policyEngine, anonymizer, new PrivacyValidator(), options))
    {
    }

    internal DocumentGateway(GatewayPipeline pipeline) => Pipeline = pipeline;

    internal GatewayPipeline Pipeline { get; }

    /// <summary>
    /// Creates a gateway over the built-in stages.
    /// </summary>
    /// <returns>
    /// A gateway that reads plain text, runs every deterministic detector, and keeps
    /// pseudonyms in memory for as long as the process lives.
    /// </returns>
    /// <remarks>
    /// The in-memory pseudonym store is what makes this suitable for a console application and
    /// unsuitable for a web application behind more than one instance, where two requests that
    /// should agree on a pseudonym would not.
    /// </remarks>
    public static DocumentGateway CreateDefault() => CreateDefault(GatewayOptions.Default);

    /// <summary>
    /// Creates a gateway over the built-in stages, with the given options.
    /// </summary>
    /// <param name="options">What the gateway itself does: audit, clock, version.</param>
    /// <returns>A gateway over the built-in stages.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static DocumentGateway CreateDefault(GatewayOptions options) =>
        new(GatewayPipeline.CreateDefault(options));

    /// <summary>
    /// Creates a gateway over the built-in policy engine, anonymizer and validator, with the
    /// extractors, detectors and pseudonym store the application chose.
    /// </summary>
    /// <param name="extractors">Turn each part of a bundle into text.</param>
    /// <param name="detectors">Find candidates in that text.</param>
    /// <param name="pseudonymStore">Where pseudonyms live, and for how long.</param>
    /// <param name="options">What the gateway itself does: audit, clock, version.</param>
    /// <returns>A gateway over those stages.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="extractors"/> is empty.</exception>
    /// <remarks>
    /// The policy engine, the anonymizer and the privacy validator are not parameters here.
    /// The policy engine and the anonymizer can be replaced through the full constructor. The
    /// validator cannot be replaced anywhere, by design.
    /// </remarks>
    public static DocumentGateway Create(
        IEnumerable<IDocumentExtractor> extractors,
        IEnumerable<ISensitiveDataDetector> detectors,
        IPseudonymStore pseudonymStore,
        GatewayOptions options) =>
        new(GatewayPipeline.Create(extractors, detectors, pseudonymStore, options));

    /// <inheritdoc/>
    public async Task<SanitizationResult> SanitizeAsync(
        DocumentBundle bundle,
        SanitizationPolicy policy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(policy);

        using var operation = new GatewayOperation(
            Pipeline.Options, AuditOperation.Sanitize, bundle.Id, policy, cancellationToken);

        try
        {
            var run = await Pipeline.RunAsync(bundle, policy, operation).ConfigureAwait(false);
            var key = Pipeline.Options.HashingKey;

            operation.Completed(
                run.Result.IsAllowed ? AuditOutcome.Allowed : AuditOutcome.Blocked,
                run,
                GatewayHashes.Document(key, run.Extracted),
                GatewayHashes.Payload(key, run.Result.Payload));

            return run.Result;
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

    /// <inheritdoc/>
    public async Task<InspectionReport> InspectAsync(
        DocumentBundle bundle,
        SanitizationPolicy policy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(policy);

        using var operation = new GatewayOperation(
            Pipeline.Options, AuditOperation.Inspect, bundle.Id, policy, cancellationToken);

        try
        {
            var run = await Pipeline.RunAsync(bundle, policy, operation).ConfigureAwait(false);

            // An inspection transmits nothing, so there is no payload to tie the record to.
            operation.Completed(
                run.Result.IsAllowed ? AuditOutcome.Allowed : AuditOutcome.Blocked,
                run,
                GatewayHashes.Document(Pipeline.Options.HashingKey, run.Extracted),
                payloadHash: null);

            return Inspections.From(run, Pipeline.Options.TimeProvider.GetUtcNow());
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
}
