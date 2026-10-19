using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Documents.Mime.Tests;

/// <summary>
/// The outcomes that make the reader safe to point at a mailbox: a reference out of the
/// message is never followed, and anything the reader cannot fully account for blocks.
/// </summary>
public sealed class MimeFailClosedTests
{
    [Fact]
    public async Task Does_not_resolve_an_external_body()
    {
        using var message = MessageFixture.Message(
            "From: a@example.invalid",
            "Content-Type: multipart/mixed; boundary=\"B\"",
            string.Empty,
            "--B",
            "Content-Type: text/plain",
            string.Empty,
            "see the link",
            "--B",
            "Content-Type: message/external-body; access-type=URL; URL=\"http://127.0.0.1:1/secret\"",
            string.Empty,
            "Content-Type: text/plain",
            string.Empty,
            "--B--");

        var bundle = await MessageFixture.ReadAsync(message);
        var external = MessageFixture.PartWithMediaType(bundle, "message/external-body");

        Assert.Equal(0, external.Content.Length);

        var extracted = await MessageFixture.ExtractAsync(bundle);

        Assert.Contains(
            extracted.Failures,
            failure => failure.PartId == external.Id
                && failure.Reason == ExtractionFailureReason.UnsupportedMediaType);
    }

    [Fact]
    public async Task Does_not_reassemble_a_partial_message()
    {
        using var message = MessageFixture.Message(
            "From: a@example.invalid",
            "Content-Type: multipart/mixed; boundary=\"B\"",
            string.Empty,
            "--B",
            "Content-Type: text/plain",
            string.Empty,
            "first half only",
            "--B",
            "Content-Type: message/partial; id=\"1\"; number=1; total=2",
            string.Empty,
            "From: inner@example.invalid",
            "--B--");

        var bundle = await MessageFixture.ReadAsync(message);
        var partial = MessageFixture.PartWithMediaType(bundle, "message/partial");
        var extracted = await MessageFixture.ExtractAsync(bundle);

        Assert.Contains(
            extracted.Failures,
            failure => failure.PartId == partial.Id
                && failure.Reason == ExtractionFailureReason.UnsupportedMediaType);
    }

    [Fact]
    public async Task Does_not_decrypt_an_encrypted_part()
    {
        using var message = MessageFixture.Message(
            "From: a@example.invalid",
            "Content-Type: application/pkcs7-mime; smime-type=enveloped-data",
            "Content-Transfer-Encoding: base64",
            string.Empty,
            Convert.ToBase64String(Encoding.ASCII.GetBytes("not really encrypted")));

        var bundle = await MessageFixture.ReadAsync(message);
        var extracted = await MessageFixture.ExtractAsync(bundle);

        Assert.Contains(
            extracted.Failures,
            failure => failure.Reason == ExtractionFailureReason.UnsupportedMediaType);
    }

    [Fact]
    public async Task Refuses_a_message_nested_deeper_than_the_bound()
    {
        using var message = MessageFixture.Raw(Encoding.ASCII.GetBytes(DeeplyNested(depth: 6)));
        var options = MimeReaderOptions.Default.WithMaxNestingDepth(3);

        await Assert.ThrowsAsync<ProcessingLimitExceededException>(
            () => MessageFixture.ReadAsync(message, options));
    }

    [Fact]
    public async Task Refuses_a_message_nested_deeper_than_the_parser_itself_descends()
    {
        using var message = MessageFixture.Raw(
            Encoding.ASCII.GetBytes(DeeplyNested(MimeReaderOptions.MaximumNestingDepth + 4)));
        var options = MimeReaderOptions.Default.WithMaxNestingDepth(MimeReaderOptions.MaximumNestingDepth);

        await Assert.ThrowsAsync<ProcessingLimitExceededException>(
            () => MessageFixture.ReadAsync(message, options));
    }

    [Fact]
    public async Task Refuses_a_message_with_more_parts_than_the_bound()
    {
        using var message = MessageFixture.Message(
            "From: a@example.invalid",
            "To: b@example.invalid",
            "Subject: s",
            "Content-Type: text/plain",
            string.Empty,
            "body");

        await Assert.ThrowsAsync<ProcessingLimitExceededException>(
            () => MessageFixture.ReadAsync(message, MessageFixture.Limits(maxPartCount: 2)));
    }

    [Fact]
    public async Task Refuses_a_part_larger_than_the_bound()
    {
        using var message = MessageFixture.Message(
            "From: a@example.invalid",
            "Content-Type: text/plain",
            string.Empty,
            new string('x', 4096));

        await Assert.ThrowsAsync<ProcessingLimitExceededException>(
            () => MessageFixture.ReadAsync(message, MessageFixture.Limits(maxPartBytes: 512, maxBundleBytes: 1024)));
    }

    [Fact]
    public async Task Refuses_a_message_larger_than_the_bound()
    {
        using var message = MessageFixture.Message(
            "From: a@example.invalid",
            "Content-Type: multipart/mixed; boundary=\"B\"",
            string.Empty,
            "--B",
            "Content-Type: text/plain",
            string.Empty,
            new string('x', 400),
            "--B",
            "Content-Type: text/plain",
            string.Empty,
            new string('y', 400),
            "--B--");

        await Assert.ThrowsAsync<ProcessingLimitExceededException>(
            () => MessageFixture.ReadAsync(message, MessageFixture.Limits(maxPartBytes: 512, maxBundleBytes: 600)));
    }

    [Fact]
    public async Task Fails_on_a_stream_that_holds_no_message()
    {
        using var message = MessageFixture.Raw([]);

        var failure = await Assert.ThrowsAsync<ExtractionException>(() => MessageFixture.ReadAsync(message));

        Assert.Equal(ExtractionErrorCodes.Malformed, failure.Error.Code);
    }

    [Fact]
    public async Task Never_quotes_the_message_in_a_failure()
    {
        using var message = MessageFixture.Raw([]);

        var failure = await Assert.ThrowsAsync<ExtractionException>(() => MessageFixture.ReadAsync(message));

        Assert.DoesNotContain("example.invalid", failure.Error.Description, StringComparison.Ordinal);
        Assert.Equal(GatewayErrorCategory.ExtractionFailed, failure.Error.Category);
    }

    [Fact]
    public async Task Reads_an_attachment_whose_file_name_is_unusable_without_the_file_name()
    {
        var hostile = new string('n', DocumentMetadata.MaxFileNameLength + 1);

        using var message = MessageFixture.Message(
            "From: a@example.invalid",
            "Content-Type: multipart/mixed; boundary=\"B\"",
            string.Empty,
            "--B",
            "Content-Type: text/plain",
            string.Empty,
            "body",
            "--B",
            "Content-Type: application/octet-stream",
            $"Content-Disposition: attachment; filename=\"{hostile}\"",
            string.Empty,
            "payload",
            "--B--");

        var bundle = await MessageFixture.ReadAsync(message);
        var attachment = MessageFixture.PartWithMediaType(bundle, "application/octet-stream");

        Assert.Null(attachment.Metadata.FileName);
    }

    private static string DeeplyNested(int depth)
    {
        var builder = new StringBuilder()
            .Append("From: a@example.invalid\r\n");

        for (var level = 0; level < depth; level++)
        {
            builder
                .Append("Content-Type: multipart/mixed; boundary=\"B")
                .Append(level)
                .Append("\"\r\n\r\n--B")
                .Append(level)
                .Append("\r\n");
        }

        builder.Append("Content-Type: text/plain\r\n\r\ninnermost\r\n");

        for (var level = depth - 1; level >= 0; level--)
        {
            builder.Append("--B").Append(level).Append("--\r\n");
        }

        return builder.ToString();
    }
}
