using System;
using System.Threading;
using BenchmarkDotNet.Attributes;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Testing;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Benchmarks;

/// <summary>
/// What each stage costs on its own.
/// </summary>
/// <remarks>
/// <para>
/// Each benchmark measures one stage over an input the previous stages already produced, so
/// that the number attributed to detection is detection and not extraction plus detection.
/// The setup cost is therefore outside the measurement on purpose.
/// </para>
/// <para>
/// The policy is <see cref="TestPolicy.Permissive"/> throughout, which pseudonymizes
/// everything and blocks nothing. A blocking policy is strictly cheaper, because the pipeline
/// stops before it sanitizes; measuring it would publish a number that flatters the library.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class StageBenchmarks
{
    private static readonly SanitizationPolicy s_policy = TestPolicy.Permissive;

    private readonly ExtractionPipeline _extraction = new([new PlainTextExtractor()]);
    private readonly DetectionPipeline _detection = new(DeterministicDetectors.All());
    private readonly PolicyEngine _policyEngine = new();
    private readonly PrivacyValidator _validator = new();

    private DocumentBundle _bundle = default!;
    private ExtractedDocument _extracted = default!;
    private DetectedDocument _detected = default!;
    private TransformationPlan _plan = default!;
    private SanitizationProvenance _provenance = default!;
    private SanitizedDocument _sanitized = default!;

    /// <summary>
    /// Gets or sets how much of the document looks like something worth finding.
    /// </summary>
    [Params(CorpusShape.Clean, CorpusShape.Realistic, CorpusShape.Dense)]
    public CorpusShape Shape { get; set; }

    /// <summary>
    /// Gets or sets roughly how many characters the document holds.
    /// </summary>
    [Params(10_000, 200_000)]
    public int Characters { get; set; }

    /// <summary>
    /// Builds the input of every stage once, before anything is measured.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _bundle = Corpus.Bundle(Shape, Characters);
        _extracted = _extraction.ExtractAsync(_bundle, s_policy.Limits, CancellationToken.None).GetAwaiter().GetResult();
        _detected = _detection.DetectAsync(_extracted, CancellationToken.None).GetAwaiter().GetResult();
        _plan = _policyEngine.Evaluate(_detected, s_policy);
        _provenance = SanitizationProvenance.Create(
            _plan.PolicyId,
            _plan.PolicyVersion,
            _detected.Detectors,
            _extracted.Extractors,
            gatewayVersion: "benchmark",
            sanitizedAt: DateTimeOffset.UnixEpoch);
        _sanitized = Anonymizer().Anonymize(_detected, _plan, _provenance);
    }

    /// <summary>
    /// Reads the bundle into segments.
    /// </summary>
    /// <returns>The extracted document.</returns>
    [Benchmark(Description = "Extract")]
    public ExtractedDocument Extract() =>
        _extraction.ExtractAsync(_bundle, s_policy.Limits, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Runs every deterministic detector over the extracted text.
    /// </summary>
    /// <returns>The detected document.</returns>
    [Benchmark(Description = "Detect")]
    public DetectedDocument Detect() =>
        _detection.DetectAsync(_extracted, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Turns findings into a transformation plan.
    /// </summary>
    /// <returns>The plan.</returns>
    [Benchmark(Description = "Evaluate policy")]
    public TransformationPlan Evaluate() => _policyEngine.Evaluate(_detected, s_policy);

    /// <summary>
    /// Applies the plan to the text.
    /// </summary>
    /// <returns>The sanitized document.</returns>
    /// <remarks>
    /// A fresh anonymizer per invocation, because a pseudonym store that keeps growing across
    /// iterations measures the store rather than the transformation.
    /// </remarks>
    [Benchmark(Description = "Anonymize")]
    public SanitizedDocument Anonymize() => Anonymizer().Anonymize(_detected, _plan, _provenance);

    /// <summary>
    /// Re-derives what the sanitized text must not contain and checks that it does not.
    /// </summary>
    /// <returns>The result.</returns>
    [Benchmark(Description = "Validate")]
    public SanitizationResult Validate() => _validator.Validate(_detected, _plan, _sanitized, s_policy);

    private static DocumentAnonymizer Anonymizer() => new(new InMemoryPseudonymStore());
}
