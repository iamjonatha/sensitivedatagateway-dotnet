using DecisionKit.Identifiers;
using DecisionKit.Questions;
using DecisionKit.Results;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Testing;
using SensitiveDataGateway.Validation;
using DecisionEngineFake = DecisionKit.Testing.Providers.FakeDecisionProvider;

namespace SensitiveDataGateway.Providers.DecisionKit.Tests;

/// <summary>
/// Builds the two halves of the integration out of public types only.
/// </summary>
/// <remarks>
/// Nothing here reaches into either library's internals, because the point of this suite is
/// to prove what a consumer of the two published packages actually gets.
/// </remarks>
internal static class Harness
{
    public const string EngineName = "test-engine";

    /// <summary>
    /// Gets the one question every scenario asks.
    /// </summary>
    /// <remarks>
    /// Its text is a constant. A question built from the document would send the document to
    /// the engine unsanitized, which is the one failure mode this package cannot prevent and
    /// the suite must therefore not model as normal.
    /// </remarks>
    public static ProbabilityQuestion Risk { get; } = new(new QuestionId("risk"), "Is this risky?");

    public static QuestionSet Questions { get; } = QuestionSet.Create(Risk);

    public static DocumentGateway Gateway() =>
        new(
            new ExtractionPipeline([new PlainTextExtractor()]),
            new DetectionPipeline(DeterministicDetectors.All()),
            new PolicyEngine(),
            new DocumentAnonymizer(new InMemoryPseudonymStore()),
            new PrivacyValidator(),
            GatewayOptions.Default);

    public static DecisionEngineFake Engine()
    {
        var engine = new DecisionEngineFake(EngineName);
        engine.Returns(Risk, 0.5);

        return engine;
    }

    /// <summary>
    /// Puts a recorder between the gateway and the adapter.
    /// </summary>
    /// <param name="engine">The DecisionKit engine the adapter talks to.</param>
    /// <param name="format">How the sanitized content is presented.</param>
    /// <returns>The recorder, which also is the provider the gateway calls.</returns>
    /// <remarks>
    /// The recorder keeps the <see cref="ValidatedPayload"/> that went in while the engine
    /// keeps the request that came out, so a test can compare the two ends of the mapping
    /// rather than assert against values it wrote itself.
    /// </remarks>
    public static RecordingDecisionProvider<QuestionSet, DecisionResult> Recorder(
        DecisionEngineFake engine,
        DecisionKitPayloadFormat format) =>
        new(new DecisionKitProvider(engine, format));

    public static DocumentGateway<QuestionSet, DecisionResult> Over(
        IDecisionProvider<QuestionSet, DecisionResult> provider) =>
        new(Gateway(), provider);

    public static SanitizationPolicy Policy => TestPolicy.Permissive;
}
