using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Testing;

namespace SensitiveDataGateway.Security.Tests;

/// <summary>
/// Proves that a failure says what went wrong without saying what it was looking at.
/// </summary>
/// <remarks>
/// An exception is the easiest place for content to escape, because the message is written
/// for a human and a human wants to see the value. Every path below is driven to failure and
/// the whole exception — message, properties and inner exceptions — is read back and scanned
/// against the annotated corpus.
/// </remarks>
public sealed class ExceptionSurfaceTests
{
    [Fact]
    public async Task No_failure_on_any_path_carries_document_content()
    {
        var rendered = new List<string>();

        foreach (var text in await Failures())
        {
            rendered.Add(text);
        }

        Assert.NotEmpty(rendered);
        LeakAssert.NoneIn(rendered, SyntheticCorpus.Values());
    }

    [Fact]
    public async Task A_provider_failure_does_not_carry_the_payload()
    {
        var provider = FakeDecisionProvider.Failing<ReviewRequest, ReviewDecision>(
            new InvalidOperationException("The provider is unavailable."));

        var failure = await Assert.ThrowsAsync<ProviderException>(
            async () => await Harness.Over(Harness.Default(), provider).ExecuteAsync(
                SyntheticCorpus.All()[0].ToBundle(),
                Harness.Request,
                Harness.Permissive(),
                TestContext.Current.CancellationToken));

        Assert.Equal(GatewayErrorCategory.ProviderFailed, failure.Error.Category);
        LeakAssert.NoneIn([failure.ToString()], SyntheticCorpus.Values());
    }

    private static async Task<IReadOnlyList<string>> Failures()
    {
        var rendered = new List<string>();

        foreach (var document in SyntheticCorpus.All())
        {
            rendered.AddRange(await Limit(document));
            rendered.AddRange(await Blocked(document));
        }

        return rendered;
    }

    private static async Task<IReadOnlyList<string>> Limit(SyntheticDocument document)
    {
        var policy = TestPolicy.Within(ProcessingLimits.Create(
            maxBundleBytes: 64,
            maxPartBytes: 64,
            maxPartCount: 4,
            maxExtractedCharacters: 64,
            maxEntities: 8,
            maxProcessingTime: TimeSpan.FromSeconds(5)));

        try
        {
            _ = await Harness.Default().SanitizeAsync(
                document.ToBundle(),
                policy,
                TestContext.Current.CancellationToken);

            return [];
        }
        catch (SensitiveDataGatewayException exception)
        {
            return [exception.ToString()];
        }
    }

    private static async Task<IReadOnlyList<string>> Blocked(SyntheticDocument document)
    {
        var result = await Harness.Over(
            Harness.WithAnonymizer(new PassThroughAnonymizer()),
            Harness.Provider()).ExecuteAsync(
            document.ToBundle(),
            Harness.Request,
            Harness.Permissive(),
            TestContext.Current.CancellationToken);

        var rendered = new List<string> { result.ToString(), result.Report.ToString() };

        foreach (var error in result.Report.ToErrors())
        {
            rendered.Add(error.ToString());
        }

        try
        {
            _ = result.GetValueOrThrow();
        }
        catch (EgressBlockedException exception)
        {
            rendered.Add(exception.ToString());
        }

        return rendered;
    }
}
