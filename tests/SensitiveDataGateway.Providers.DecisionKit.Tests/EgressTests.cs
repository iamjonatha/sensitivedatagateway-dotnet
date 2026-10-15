using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DecisionKit.Providers;
using SensitiveDataGateway.Testing;

namespace SensitiveDataGateway.Providers.DecisionKit.Tests;

/// <summary>
/// What actually arrives at a DecisionKit engine, for every document in the corpus.
/// </summary>
/// <remarks>
/// The security suite proves that a <see cref="Validation.ValidatedPayload"/> carries no
/// sensitive value. This suite proves the adapter does not reintroduce one on the way out:
/// the request handed to the engine is flattened back into strings — input, properties,
/// metadata, correlation identifier — and every value the document was built from is looked
/// for in it.
/// </remarks>
public sealed class EgressTests
{
    public static TheoryData<string, DecisionKitPayloadFormat> Corpus()
    {
        var data = new TheoryData<string, DecisionKitPayloadFormat>();

        foreach (var document in SyntheticCorpus.All())
        {
            data.Add(document.Name, DecisionKitPayloadFormat.Text);
            data.Add(document.Name, DecisionKitPayloadFormat.Segments);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Corpus))]
    public async Task Sends_no_sensitive_value_to_the_engine(string name, DecisionKitPayloadFormat format)
    {
        var document = SyntheticCorpus.All().Single(candidate => candidate.Name == name);
        var engine = Harness.Engine();

        var result = await Harness.Over(Harness.Recorder(engine, format)).ExecuteAsync(
            document.ToBundle(),
            Harness.Questions,
            Harness.Policy,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);

        LeakAssert.NoneIn(
            Flatten(engine.LastRequest!),
            document.Values.Select(static value => value.Text));
    }

    /// <summary>
    /// Turns a request into the lines a leak check can read.
    /// </summary>
    /// <param name="request">The request the engine received.</param>
    /// <returns>Every string the request carries.</returns>
    /// <remarks>
    /// Everything is flattened, not just the input. A correlation identifier or a metadata
    /// value that happened to carry content would be just as much of a leak as the text,
    /// and far easier to miss.
    /// </remarks>
    private static IEnumerable<string> Flatten(DecisionRequest request)
    {
        if (request.Input.Text is { } text)
        {
            yield return text;
        }

        foreach (var property in request.Input.Properties)
        {
            yield return property.Key;
            yield return Describe(property.Value);
        }

        foreach (var entry in request.Metadata)
        {
            yield return entry.Key;
            yield return Describe(entry.Value);
        }

        yield return request.ClientRequestId?.Value ?? string.Empty;
    }

    private static string Describe(object? value) =>
        value switch
        {
            null => string.Empty,
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
}
