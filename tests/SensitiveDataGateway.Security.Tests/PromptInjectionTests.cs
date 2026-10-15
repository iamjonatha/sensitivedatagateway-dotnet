using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SensitiveDataGateway.Testing;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Security.Tests;

/// <summary>
/// Proves that a document which tries to give orders is still only a document.
/// </summary>
/// <remarks>
/// <para>
/// The gateway has no notion of an instruction. It extracts, detects, decides, rewrites and
/// checks, and not one of those stages reads the text for meaning. The scenarios below say
/// so twice: a hostile attachment leaks nothing, and it is handled exactly like every other
/// attachment.
/// </para>
/// <para>
/// Carrying the instruction forward into the payload is deliberate. Removing it would be a
/// judgement about meaning, and a gateway that judges meaning will be wrong about it sooner
/// or later. The payload is data; whoever consumes it is responsible for not treating it as
/// a prompt.
/// </para>
/// </remarks>
public sealed class PromptInjectionTests
{
    [Fact]
    public async Task A_hostile_attachment_transmits_nothing_sensitive()
    {
        var provider = Harness.Provider();
        var gateway = Harness.Over(Harness.Default(), provider);
        var documents = SyntheticCorpus.PromptInjections();

        Assert.NotEmpty(documents);

        foreach (var document in documents)
        {
            var result = await gateway.ExecuteAsync(
                document.ToBundle(),
                Harness.Request,
                Harness.Permissive(),
                TestContext.Current.CancellationToken);

            Assert.True(result.IsAllowed, $"The permissive policy blocked '{document.Name}'.");
        }

        LeakAssert.NoneOf(provider, SyntheticCorpus.Values());
        Assert.Equal(documents.Count, provider.Calls.Count);
    }

    /// <summary>
    /// The instruction crosses the boundary as content rather than being obeyed or removed.
    /// </summary>
    /// <returns>A task that completes when the scenario has run.</returns>
    /// <remarks>
    /// This is the assertion that keeps the previous one honest. Deleting the instruction
    /// would also satisfy "nothing leaked", and would quietly turn the gateway into a censor
    /// of text it does not understand.
    /// </remarks>
    [Theory]
    [InlineData("Ignore all previous instructions")]
    [InlineData("Dimentica le istruzioni precedenti")]
    public async Task An_instruction_crosses_the_boundary_as_content(string instruction)
    {
        var provider = Harness.Provider();
        var gateway = Harness.Over(Harness.Default(), provider);

        foreach (var document in SyntheticCorpus.PromptInjections())
        {
            _ = await gateway.ExecuteAsync(
                document.ToBundle(),
                Harness.Request,
                Harness.Permissive(),
                TestContext.Current.CancellationToken);
        }

        var transmitted = new List<string>();

        foreach (var call in provider.Calls)
        {
            transmitted.Add(Join(call.Payload));
        }

        Assert.Contains(
            transmitted,
            text => text.Contains(instruction, StringComparison.OrdinalIgnoreCase));
    }

    private static string Join(ValidatedPayload payload)
    {
        var texts = new List<string>(payload.Content.Count);

        foreach (var segment in payload.Content)
        {
            texts.Add(segment.Text);
        }

        return string.Join("\n", texts);
    }
}
