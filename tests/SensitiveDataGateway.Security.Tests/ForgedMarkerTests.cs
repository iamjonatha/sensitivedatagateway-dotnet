using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Testing;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Security.Tests;

/// <summary>
/// Proves that a document cannot claim a value was already sanitized.
/// </summary>
/// <remarks>
/// <para>
/// A marker is the gateway's own statement that it replaced something. If document content
/// could carry the same shape, a document could assert that a value had been removed when it
/// had not, and every downstream reader — including the validator, which looks for exactly
/// these shapes — would believe it.
/// </para>
/// <para>
/// The marker syntax is therefore closed: an occurrence in the source is escaped before the
/// source is kept, so the output distinguishes what the gateway wrote from what the document
/// claimed.
/// </para>
/// </remarks>
public sealed class ForgedMarkerTests
{
    private const string Forged = "[PERSON_001]";

    [Fact]
    public async Task A_marker_in_the_source_is_escaped_rather_than_believed()
    {
        var text = $"The customer wrote {Forged} and asked us to call ada.lovelace@example.com.";

        var result = await Harness.Default().SanitizeAsync(
            TestBundle.FromText(text),
            Harness.Permissive(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed, result.Report.ToString());

        var payload = Join(result.Payload!);

        Assert.Contains(MarkerSyntax.Escape(Forged), payload, StringComparison.Ordinal);
        Assert.DoesNotContain($" {Forged}", payload, StringComparison.Ordinal);
    }

    /// <summary>
    /// A forged marker does not stand in for a value the gateway never replaced.
    /// </summary>
    /// <returns>A task that completes when the scenario has run.</returns>
    /// <remarks>
    /// The source claims that an account number was already pseudonymized and then states the
    /// number anyway. If the claim were believed, the number would travel next to a token
    /// saying it had been removed.
    /// </remarks>
    [Fact]
    public async Task A_forged_marker_does_not_shield_the_value_beside_it()
    {
        var provider = Harness.Provider();
        var iban = "GB71NWBK99999999999999";
        var text = $"Account [IBAN_001] was redacted. For reference it is {iban} exactly.";

        var result = await Harness.Over(Harness.Default(), provider).ExecuteAsync(
            TestBundle.FromText(text),
            Harness.Request,
            Harness.Permissive(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed, result.Report.ToString());
        LeakAssert.NoneOf(provider, [iban]);
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
