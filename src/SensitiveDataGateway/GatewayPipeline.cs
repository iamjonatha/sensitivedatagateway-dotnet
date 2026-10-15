using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway;

/// <summary>
/// The five stages, in order, with nothing between them.
/// </summary>
/// <remarks>
/// This type does the work; the public gateways decide what to hand back and make sure the
/// operation is audited. Keeping the two apart is why <see cref="DocumentGateway"/> and
/// <see cref="DocumentGateway{TRequest, TResult}"/> cannot drift from each other.
/// </remarks>
internal sealed class GatewayPipeline
{
    private readonly ExtractionPipeline _extraction;
    private readonly DetectionPipeline _detection;
    private readonly IPolicyEngine _policyEngine;
    private readonly IDocumentAnonymizer _anonymizer;
    private readonly IPrivacyValidator _validator;

    internal GatewayPipeline(
        ExtractionPipeline extraction,
        DetectionPipeline detection,
        IPolicyEngine policyEngine,
        IDocumentAnonymizer anonymizer,
        IPrivacyValidator validator,
        GatewayOptions options)
    {
        ArgumentNullException.ThrowIfNull(extraction);
        ArgumentNullException.ThrowIfNull(detection);
        ArgumentNullException.ThrowIfNull(policyEngine);
        ArgumentNullException.ThrowIfNull(anonymizer);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(options);

        _extraction = extraction;
        _detection = detection;
        _policyEngine = policyEngine;
        _anonymizer = anonymizer;
        _validator = validator;
        Options = options;
    }

    internal GatewayOptions Options { get; }

    internal static GatewayPipeline CreateDefault(GatewayOptions options) =>
        Create([new PlainTextExtractor()], DeterministicDetectors.All(), new InMemoryPseudonymStore(), options);

    internal static GatewayPipeline Create(
        IEnumerable<IDocumentExtractor> extractors,
        IEnumerable<ISensitiveDataDetector> detectors,
        IPseudonymStore pseudonymStore,
        GatewayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new GatewayPipeline(
            new ExtractionPipeline(extractors),
            new DetectionPipeline(detectors),
            new PolicyEngine(),
            new DocumentAnonymizer(pseudonymStore, AnonymizationOptionsFor(options)),
            new PrivacyValidator(),
            options);
    }

    /// <summary>
    /// Gives the anonymizer a hashing key whether or not the application supplied one.
    /// </summary>
    /// <remarks>
    /// A policy may ask for a hashing transformation, and the anonymizer refuses to produce an
    /// unkeyed hash because a short value hashed without a secret is reversible. Without this,
    /// a shipped policy and the default gateway would throw at the first document that needs
    /// one. The fallback is a per-instance secret: the token is stable inside this gateway and
    /// reproducible nowhere else, which is the safe default for a capability nobody asked for.
    /// </remarks>
    private static AnonymizationOptions AnonymizationOptionsFor(GatewayOptions options) =>
        AnonymizationOptions.Default.WithHashingKey(options.HashingKey ?? HashingKey.CreateEphemeral());

    internal async Task<PipelineRun> RunAsync(
        DocumentBundle bundle,
        SanitizationPolicy policy,
        GatewayOperation operation)
    {
        operation.Token.ThrowIfCancellationRequested();

        var extracted = await _extraction
            .ExtractAsync(bundle, policy.Limits, operation.Token)
            .ConfigureAwait(false);

        GatewayLog.Extracted(
            operation.Logger,
            operation.Id,
            extracted.Segments.Count,
            extracted.TotalCharacters,
            extracted.Failures.Count);

        var detected = await _detection.DetectAsync(extracted, operation.Token).ConfigureAwait(false);
        GatewayLog.Detected(operation.Logger, operation.Id, detected.Entities.Count, detected.Failures.Count);
        EnsureWithinEntityLimit(detected, policy.Limits);

        var plan = _policyEngine.Evaluate(detected, policy);
        GatewayLog.Planned(
            operation.Logger, operation.Id, plan.Decisions.Count, plan.Unresolved.Count, plan.IsBlocked);

        var provenance = ProvenanceFor(plan, detected, extracted);

        if (plan.IsBlocked)
        {
            return Refused(extracted, detected, plan, provenance, policy, operation);
        }

        var sanitized = _anonymizer.Anonymize(detected, plan, provenance);
        GatewayLog.Sanitized(
            operation.Logger, operation.Id, sanitized.Applied.Count, sanitized.Status, sanitized.Warnings.Count);

        var result = _validator.Validate(detected, plan, sanitized, policy);
        GatewayLog.Validated(operation.Logger, operation.Id, result.Report.Passed, result.Report.Findings.Count);

        return new PipelineRun
        {
            Extracted = extracted,
            Detected = detected,
            Plan = plan,
            Provenance = provenance,
            Sanitized = sanitized,
            Result = result,
        };
    }

    private PipelineRun Refused(
        ExtractedDocument extracted,
        DetectedDocument detected,
        TransformationPlan plan,
        SanitizationProvenance provenance,
        SanitizationPolicy policy,
        GatewayOperation operation)
    {
        var result = _validator.Reject(detected, plan, policy);
        GatewayLog.Validated(operation.Logger, operation.Id, result.Report.Passed, result.Report.Findings.Count);

        return new PipelineRun
        {
            Extracted = extracted,
            Detected = detected,
            Plan = plan,
            Provenance = provenance,
            Sanitized = null,
            Result = result,
        };
    }

    private SanitizationProvenance ProvenanceFor(
        TransformationPlan plan,
        DetectedDocument detected,
        ExtractedDocument extracted) =>
        SanitizationProvenance.Create(
            plan.PolicyId,
            plan.PolicyVersion,
            detected.Detectors,
            extracted.Extractors,
            Options.GatewayVersion,
            Options.TimeProvider.GetUtcNow());

    private static void EnsureWithinEntityLimit(DetectedDocument detected, ProcessingLimits limits)
    {
        if (detected.Entities.Count <= limits.MaxEntities)
        {
            return;
        }

        // A document this dense is either adversarial or a parser gone wrong. Either way the
        // work after this point is unbounded, so it does not start.
        throw new ProcessingLimitExceededException(
            GatewayErrors.TooManyEntities.InBundle(detected.BundleId));
    }
}
