using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SensitiveDataGateway.Testing;

namespace SensitiveDataGateway.Security.Tests;

/// <summary>
/// Proves that the corpus is honest and that nothing in it reaches a provider.
/// </summary>
/// <remarks>
/// The honesty check comes first on purpose. An egress assertion over a value no detector
/// recognizes would pass for the wrong reason: the value would be absent from the payload
/// only because the payload is the original text and the assertion looked for the wrong
/// shape. Proving every annotated value is actually removed makes the rest meaningful.
/// </remarks>
public sealed class CorpusEgressTests
{
    public static TheoryData<string> Documents()
    {
        var names = new TheoryData<string>();

        foreach (var document in SyntheticCorpus.All())
        {
            names.Add(document.Name);
        }

        return names;
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public async Task Every_annotated_value_is_removed_from_the_payload(string name)
    {
        var document = SyntheticCorpus.All().Single(candidate => candidate.Name == name);
        var gateway = Harness.Default();

        var result = await gateway.SanitizeAsync(
            document.ToBundle(),
            Harness.Permissive(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed, $"The permissive policy blocked '{name}': {result.Report}");
        LeakAssert.NoneOf(result.Payload!, document.Texts);
    }

    [Fact]
    public void The_corpus_annotates_at_least_twenty_values()
    {
        Assert.True(
            SyntheticCorpus.Values().Count >= 20,
            $"The corpus annotates {SyntheticCorpus.Values().Count} values.");
    }

    [Fact]
    public async Task No_annotated_value_reaches_the_provider()
    {
        var provider = Harness.Provider();
        var gateway = Harness.Over(Harness.Default(), provider);

        foreach (var document in SyntheticCorpus.All())
        {
            _ = await gateway.ExecuteAsync(
                document.ToBundle(),
                Harness.Request,
                Harness.Permissive(),
                TestContext.Current.CancellationToken);
        }

        Assert.Equal(SyntheticCorpus.All().Count, provider.Calls.Count);
        LeakAssert.NoneOf(provider, SyntheticCorpus.Values());
    }

    [Fact]
    public async Task No_annotated_value_reaches_an_audit_record()
    {
        var sink = new RecordingAuditSink();
        var gateway = Harness.WithOptions(GatewayOptions.Default.WithAuditSink(sink));

        foreach (var document in SyntheticCorpus.All())
        {
            _ = await gateway.SanitizeAsync(
                document.ToBundle(),
                Harness.Permissive(),
                TestContext.Current.CancellationToken);
        }

        Assert.NotEmpty(sink.Records);
        LeakAssert.NoneIn(Rendered(sink), SyntheticCorpus.Values());
    }

    private static IEnumerable<string> Rendered(RecordingAuditSink sink)
    {
        foreach (var record in sink.Records)
        {
            yield return record.ToString();
        }
    }
}
