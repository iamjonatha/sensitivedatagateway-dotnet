using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Testing;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Benchmarks;

/// <summary>
/// What one document costs end to end.
/// </summary>
/// <remarks>
/// This is the number an application budgets against: one call, five stages, an audit record
/// written to a sink that does nothing. It includes the pseudonym store, which a long-lived
/// gateway would share across calls, so the figure is the pessimistic one.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class GatewayBenchmarks
{
    private static readonly SanitizationPolicy s_policy = TestPolicy.Permissive;

    private DocumentGateway _gateway = default!;
    private DocumentBundle _bundle = default!;

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
    /// Builds the gateway and the bundle once, before anything is measured.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _gateway = DocumentGateway.CreateDefault();
        _bundle = Corpus.Bundle(Shape, Characters);
    }

    /// <summary>
    /// Sanitizes one bundle.
    /// </summary>
    /// <returns>The result.</returns>
    [Benchmark(Description = "SanitizeAsync")]
    public Task<SanitizationResult> Sanitize() =>
        _gateway.SanitizeAsync(_bundle, s_policy, CancellationToken.None);
}
