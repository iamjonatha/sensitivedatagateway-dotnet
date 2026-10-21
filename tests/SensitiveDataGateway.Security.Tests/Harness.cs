using System.Collections.Generic;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Testing;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Security.Tests;

/// <summary>
/// What a request and a decision look like to this suite.
/// </summary>
/// <param name="Purpose">Why the caller wants a decision.</param>
public sealed record ReviewRequest(string Purpose);

/// <summary>
/// What a provider hands back.
/// </summary>
/// <param name="Outcome">The decision, which no test here depends on.</param>
public sealed record ReviewDecision(string Outcome);

/// <summary>
/// Builds gateways out of public types only.
/// </summary>
/// <remarks>
/// <para>
/// This project has no access to the internals of the pipeline, by design. Every scenario
/// below is therefore something a consumer of the published packages could write, which is
/// the point: a guarantee that only holds for code inside the assembly is not a guarantee.
/// </para>
/// <para>
/// The stages are assembled by hand rather than through
/// <see cref="DocumentGateway.CreateDefault()"/>, because an adversarial scenario needs to
/// replace one stage with a broken one and keep the rest honest.
/// </para>
/// </remarks>
internal static class Harness
{
    public static ReviewRequest Request { get; } = new("fraud-review");

    public static ReviewDecision Decision { get; } = new("approved");

    public static DocumentGateway Default() =>
        Build(DeterministicDetectors.All(), Anonymizer(), GatewayOptions.Default);

    public static DocumentGateway WithDetectors(IEnumerable<ISensitiveDataDetector> detectors) =>
        Build(detectors, Anonymizer(), GatewayOptions.Default);

    public static DocumentGateway WithDetectors(
        IEnumerable<ISensitiveDataDetector> detectors,
        DetectionPipelineOptions detectionOptions) =>
        new(
            new ExtractionPipeline([new PlainTextExtractor()]),
            new DetectionPipeline(detectors, detectionOptions),
            new PolicyEngine(),
            Anonymizer(),
            new PrivacyValidator(),
            GatewayOptions.Default);

    public static DocumentGateway WithAnonymizer(IDocumentAnonymizer anonymizer) =>
        Build(DeterministicDetectors.All(), anonymizer, GatewayOptions.Default);

    public static DocumentGateway WithOptions(GatewayOptions options) =>
        Build(DeterministicDetectors.All(), Anonymizer(), options);

    public static DocumentGateway Build(
        IEnumerable<ISensitiveDataDetector> detectors,
        IDocumentAnonymizer anonymizer,
        GatewayOptions options) =>
        new(
            new ExtractionPipeline([new PlainTextExtractor()]),
            new DetectionPipeline(detectors),
            new PolicyEngine(),
            anonymizer,
            new PrivacyValidator(),
            options);

    public static DocumentGateway<ReviewRequest, ReviewDecision> Over(
        DocumentGateway gateway,
        IDecisionProvider<ReviewRequest, ReviewDecision> provider) =>
        new(gateway, provider);

    public static RecordingDecisionProvider<ReviewRequest, ReviewDecision> Provider() =>
        RecordingDecisionProvider.Returning<ReviewRequest, ReviewDecision>(Decision);

    public static IDocumentAnonymizer Anonymizer() => new DocumentAnonymizer(new InMemoryPseudonymStore());

    /// <summary>
    /// A policy that leaves every door the gateway has open.
    /// </summary>
    /// <returns>The policy.</returns>
    /// <remarks>
    /// An egress test that uses a strict policy proves the policy, not the boundary. Every
    /// scenario here hands the gateway the most permissive configuration it accepts, so that
    /// a block is a property of the pipeline rather than of the configuration.
    /// </remarks>
    public static SanitizationPolicy Permissive() => TestPolicy.Permissive;
}
