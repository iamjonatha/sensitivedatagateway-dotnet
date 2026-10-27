using System;
using System.Linq;
using System.Threading.Tasks;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Testing;

namespace SensitiveDataGateway.Documents.Mime.Tests;

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
/// Proves that a mail message with an attachment goes through the whole gateway, and that
/// the attachment is sanitized rather than skipped.
/// </summary>
/// <remarks>
/// <para>
/// The other suites in this project stop at the bundle: they prove the reader produces the
/// right parts. The stages after it are proved elsewhere, over bundles built in memory.
/// Neither half proves the join, and the join is where an attachment is quietly lost.
/// </para>
/// <para>
/// "Nothing leaked" is not enough on its own here. An attachment that is never extracted
/// also leaks nothing, so every scenario below asserts both halves: the attachment's text
/// arrives, and the sensitive values in it do not.
/// </para>
/// </remarks>
public sealed class MailEndToEndTests
{
    private const string Iban = "GB71NWBK99999999999999";
    private const string Address = "claims@example.invalid";
    private const string AttachmentMarker = "Settlement statement follows.";
    private const string BodyMarker = "Please review the statement.";

    [Fact]
    public async Task An_attachment_reaches_the_provider_with_its_values_removed()
    {
        var provider = RecordingDecisionProvider.Returning<ReviewRequest, ReviewDecision>(
            new ReviewDecision("approved"));
        var gateway = Gateway(provider);

        var result = await gateway.ExecuteAsync(
            await ReadMessageAsync(),
            new ReviewRequest("claim-review"),
            TestPolicy.Permissive,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsAllowed);
        Assert.True(provider.WasCalled);
        LeakAssert.NoneOf(provider, [Iban, Address]);
    }

    /// <summary>
    /// The assertion that keeps the previous one honest.
    /// </summary>
    /// <returns>A task that completes when the scenario has run.</returns>
    /// <remarks>
    /// Dropping the attachment would satisfy "no value reached the provider" perfectly, and
    /// would turn the gateway into something that silently discards the half of a mail
    /// message that usually carries the document.
    /// </remarks>
    [Fact]
    public async Task The_attachment_text_arrives_rather_than_being_dropped()
    {
        var provider = RecordingDecisionProvider.Returning<ReviewRequest, ReviewDecision>(
            new ReviewDecision("approved"));
        var gateway = Gateway(provider);

        await gateway.ExecuteAsync(
            await ReadMessageAsync(),
            new ReviewRequest("claim-review"),
            TestPolicy.Permissive,
            TestContext.Current.CancellationToken);

        var payload = provider.LastPayload;

        Assert.NotNull(payload);
        Assert.Contains(payload.Content, segment => segment.Text.Contains(AttachmentMarker, StringComparison.Ordinal));
        Assert.Contains(payload.Content, segment => segment.Text.Contains(BodyMarker, StringComparison.Ordinal));
    }

    /// <summary>
    /// The attachment stays a part of its own, rather than being flattened into the body.
    /// </summary>
    /// <returns>A task that completes when the scenario has run.</returns>
    /// <remarks>
    /// Provenance per part is what lets an application say which document a finding came
    /// from. A payload whose segments all claim the same part has lost that, and nothing
    /// downstream can tell.
    /// </remarks>
    [Fact]
    public async Task The_attachment_keeps_a_part_identity_of_its_own()
    {
        var provider = RecordingDecisionProvider.Returning<ReviewRequest, ReviewDecision>(
            new ReviewDecision("approved"));
        var gateway = Gateway(provider);

        await gateway.ExecuteAsync(
            await ReadMessageAsync(),
            new ReviewRequest("claim-review"),
            TestPolicy.Permissive,
            TestContext.Current.CancellationToken);

        var payload = provider.LastPayload;

        Assert.NotNull(payload);

        var body = payload.Content.Single(segment => segment.Text.Contains(BodyMarker, StringComparison.Ordinal));
        var attachment = payload.Content.Single(segment => segment.Text.Contains(AttachmentMarker, StringComparison.Ordinal));

        Assert.NotEqual(body.PartId, attachment.PartId);
    }

    private static DocumentGateway<ReviewRequest, ReviewDecision> Gateway(
        IDecisionProvider<ReviewRequest, ReviewDecision> provider) =>
        new(
            new DocumentGateway(
                new ExtractionPipeline([new MimeStructureExtractor(), new PlainTextExtractor()]),
                new DetectionPipeline(DeterministicDetectors.All()),
                new PolicyEngine(),
                new DocumentAnonymizer(new InMemoryPseudonymStore()),
                GatewayOptions.Default),
            provider);

    /// <summary>
    /// A mail message carrying a text attachment, built as bytes rather than as objects.
    /// </summary>
    /// <returns>The parsed bundle.</returns>
    /// <remarks>
    /// Every address, name and account number in here is invented, and the account number is
    /// the documented example IBAN. None of it belongs to anybody.
    /// </remarks>
    private static Task<DocumentBundle> ReadMessageAsync() =>
        MessageFixture.ReadAsync(MessageFixture.Message(
            "From: adjuster@example.invalid",
            "To: review@example.invalid",
            "Subject: Claim 4711",
            "MIME-Version: 1.0",
            "Content-Type: multipart/mixed; boundary=\"frontier\"",
            string.Empty,
            "--frontier",
            "Content-Type: text/plain; charset=utf-8",
            string.Empty,
            BodyMarker,
            $"Write back to {Address} if anything is missing.",
            "--frontier",
            "Content-Type: text/plain; charset=utf-8",
            "Content-Disposition: attachment; filename=\"statement.txt\"",
            string.Empty,
            AttachmentMarker,
            $"The balance is payable to {Iban}.",
            "--frontier--"));
}
