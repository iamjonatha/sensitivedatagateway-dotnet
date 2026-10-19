using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Documents.Mime.Tests;

public sealed class MimeBundleReaderTests
{
    [Fact]
    public async Task Turns_each_configured_header_into_a_part()
    {
        using var message = MessageFixture.Message(
            "From: anna.verdi@example.invalid",
            "To: ufficio@example.invalid",
            "Subject: Pratica 12345",
            "X-Internal-Note: not on the list",
            "Content-Type: text/plain",
            string.Empty,
            "body text");

        var bundle = await MessageFixture.ReadAsync(message);
        var headers = bundle.Parts
            .Where(part => part.Kind is PartKind.Header or PartKind.Subject)
            .Select(MessageFixture.TextOf)
            .ToList();

        Assert.Equal(3, headers.Count);
        Assert.Contains("From: anna.verdi@example.invalid", headers);
        Assert.Contains("Subject: Pratica 12345", headers);
        Assert.DoesNotContain(headers, header => header.StartsWith("X-Internal-Note", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Carries_the_header_name_into_the_content_and_never_into_the_identifier()
    {
        using var message = MessageFixture.Message(
            "From: mario.rossi@example.invalid",
            "Subject: Rimborso per Giulia Bianchi",
            "Content-Type: text/plain",
            string.Empty,
            "body");

        var bundle = await MessageFixture.ReadAsync(message);

        Assert.All(
            bundle.Parts,
            part => Assert.DoesNotContain("Giulia", part.Id.Value, StringComparison.OrdinalIgnoreCase));
        Assert.All(
            bundle.Parts,
            part => Assert.DoesNotContain("rossi", part.Id.Value, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Reads_every_alternative_and_not_only_the_preferred_one()
    {
        using var message = MessageFixture.Message(
            "From: a@example.invalid",
            "Content-Type: multipart/alternative; boundary=\"B\"",
            string.Empty,
            "--B",
            "Content-Type: text/plain",
            string.Empty,
            "plain version",
            "--B",
            "Content-Type: text/html",
            string.Empty,
            "<p>html version with IT60X0542811101000000123456</p>",
            "--B--");

        var bundle = await MessageFixture.ReadAsync(message);
        var bodies = bundle.Parts.Where(part => part.Kind == PartKind.Body).ToList();

        Assert.Equal(2, bodies.Count);
        Assert.Contains(bodies, part => part.MediaType == "text/plain");
        Assert.Contains(bodies, part => part.MediaType == "text/html");
    }

    [Fact]
    public async Task Decodes_base64_and_quoted_printable_bodies()
    {
        using var message = MessageFixture.Message(
            "From: a@example.invalid",
            "Content-Type: multipart/mixed; boundary=\"B\"",
            string.Empty,
            "--B",
            "Content-Type: text/plain; charset=utf-8",
            "Content-Transfer-Encoding: base64",
            string.Empty,
            Convert.ToBase64String(Encoding.UTF8.GetBytes("decoded from base64")),
            "--B",
            "Content-Type: text/plain; charset=utf-8",
            "Content-Transfer-Encoding: quoted-printable",
            string.Empty,
            "decoded from quoted=2Dprintable",
            "--B--");

        var bundle = await MessageFixture.ReadAsync(message);
        var bodies = bundle.Parts
            .Where(part => part.Kind == PartKind.Body)
            .Select(MessageFixture.TextOf)
            .ToList();

        Assert.Contains(bodies, text => text.Contains("decoded from base64", StringComparison.Ordinal));
        Assert.Contains(bodies, text => text.Contains("decoded from quoted-printable", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Decodes_encoded_words_in_a_header()
    {
        using var message = MessageFixture.Message(
            "From: a@example.invalid",
            "Subject: =?utf-8?B?UmltYm9yc28gcGVyIE5pY29sw7I=?=",
            "Content-Type: text/plain",
            string.Empty,
            "body");

        var bundle = await MessageFixture.ReadAsync(message);
        var subject = bundle.Parts.Single(part => part.Kind == PartKind.Subject);

        Assert.Equal("Subject: Rimborso per Nicolò", MessageFixture.TextOf(subject));
    }

    [Fact]
    public async Task Decodes_a_body_that_is_not_utf8()
    {
        var header = Encoding.ASCII.GetBytes(string.Join(
            "\r\n",
            "From: a@example.invalid",
            "Content-Type: text/plain; charset=iso-8859-1",
            string.Empty,
            string.Empty));
        var body = Encoding.Latin1.GetBytes("Citt\u00e0 di Perugia");

        using var message = MessageFixture.Raw([.. header, .. body]);

        var bundle = await MessageFixture.ReadAsync(message);
        var text = MessageFixture.TextOf(bundle.Primary);

        Assert.Contains("Città di Perugia", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_the_media_type_and_the_file_name_of_an_attachment()
    {
        using var message = MessageFixture.Message(
            "From: a@example.invalid",
            "Content-Type: multipart/mixed; boundary=\"B\"",
            string.Empty,
            "--B",
            "Content-Type: text/plain",
            string.Empty,
            "see attached",
            "--B",
            "Content-Type: application/pdf",
            "Content-Disposition: attachment; filename=\"contratto-rossi.pdf\"",
            "Content-Transfer-Encoding: base64",
            string.Empty,
            Convert.ToBase64String([1, 2, 3, 4]),
            "--B--");

        var bundle = await MessageFixture.ReadAsync(message);
        var attachment = MessageFixture.PartWithMediaType(bundle, "application/pdf");

        Assert.Equal(PartKind.Attachment, attachment.Kind);
        Assert.Equal("contratto-rossi.pdf", attachment.Metadata.FileName);
        Assert.Equal(4, attachment.Content.Length);
    }

    [Fact]
    public async Task Flattens_a_forwarded_message_and_keeps_its_lineage()
    {
        using var message = MessageFixture.Message(
            "From: outer@example.invalid",
            "Subject: Fwd",
            "Content-Type: multipart/mixed; boundary=\"B\"",
            string.Empty,
            "--B",
            "Content-Type: text/plain",
            string.Empty,
            "forwarding this",
            "--B",
            "Content-Type: message/rfc822",
            string.Empty,
            "From: inner@example.invalid",
            "Subject: Original",
            "Content-Type: text/plain",
            string.Empty,
            "inner body",
            "--B--");

        var bundle = await MessageFixture.ReadAsync(message);
        var envelope = MessageFixture.PartWithMediaType(bundle, "message/rfc822");
        var nested = bundle.Parts.Where(part => part.Parent == envelope.Id).ToList();

        Assert.Equal(PartKind.Embedded, envelope.Kind);
        Assert.Contains(nested, part => MessageFixture.TextOf(part) == "From: inner@example.invalid");
        Assert.Contains(nested, part => MessageFixture.TextOf(part) == "inner body");
    }

    [Fact]
    public async Task Gives_an_empty_primary_to_a_message_with_no_top_level_body()
    {
        using var message = MessageFixture.Message(
            "From: a@example.invalid",
            "Content-Type: application/pdf",
            "Content-Disposition: attachment; filename=\"x.pdf\"",
            string.Empty,
            "%PDF-1.4");

        var bundle = await MessageFixture.ReadAsync(message);

        Assert.Equal(PartKind.Body, bundle.Primary.Kind);
        Assert.Null(bundle.Primary.Parent);
        Assert.Equal(string.Empty, MessageFixture.TextOf(bundle.Primary));
    }
}
