using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Documents.Mime.Tests;

public sealed class MimeStructureExtractorTests
{
    [Fact]
    public void Claims_envelopes_and_reports_and_nothing_else()
    {
        var extractor = new MimeStructureExtractor();

        Assert.True(extractor.CanExtract("message/rfc822"));
        Assert.True(extractor.CanExtract("message/global"));
        Assert.True(extractor.CanExtract("message/delivery-status"));
        Assert.True(extractor.CanExtract("message/disposition-notification"));

        Assert.False(extractor.CanExtract("message/external-body"));
        Assert.False(extractor.CanExtract("message/partial"));
        Assert.False(extractor.CanExtract("application/pdf"));
        Assert.False(extractor.CanExtract("text/plain"));
    }

    [Fact]
    public void Ignores_media_type_parameters_when_it_decides()
    {
        var extractor = new MimeStructureExtractor();

        Assert.True(extractor.CanExtract("message/rfc822; charset=us-ascii"));
    }

    [Fact]
    public async Task Yields_nothing_for_an_envelope_because_its_parts_were_flattened()
    {
        var part = DocumentPart.Create(
            PartId.Create("m1"),
            PartKind.Embedded,
            "message/rfc822",
            DocumentContent.FromBytes(ReadOnlyMemory<byte>.Empty));

        var segments = await new MimeStructureExtractor().ExtractAsync(part, CancellationToken.None);

        Assert.Empty(segments);
    }

    [Fact]
    public async Task Turns_the_fields_of_a_delivery_report_into_segments()
    {
        var report = string.Join(
            "\r\n",
            "Reporting-MTA: dns; mail.example.invalid",
            "Final-Recipient: rfc822; carla.neri@example.invalid",
            "Action: failed",
            "Status: 5.1.1");

        var part = Report("message/delivery-status", report);
        var segments = await new MimeStructureExtractor().ExtractAsync(part, CancellationToken.None);

        Assert.Equal(4, segments.Count);
        Assert.Contains(segments, segment => segment.Text == "Final-Recipient: rfc822; carla.neri@example.invalid");
        Assert.All(segments, segment => Assert.Equal(SegmentOriginKind.Header, segment.Origin.Kind));
    }

    [Fact]
    public async Task Joins_a_folded_field_onto_the_field_it_belongs_to()
    {
        var report = string.Join(
            "\r\n",
            "Final-Recipient: rfc822;",
            "\tcarla.neri@example.invalid",
            "Action: failed");

        var part = Report("message/delivery-status", report);
        var segments = await new MimeStructureExtractor().ExtractAsync(part, CancellationToken.None);

        Assert.Equal(2, segments.Count);
        Assert.Equal("Final-Recipient: rfc822; carla.neri@example.invalid", segments[0].Text);
    }

    [Fact]
    public async Task Fails_rather_than_replacing_a_byte_it_cannot_decode()
    {
        var part = DocumentPart.Create(
            PartId.Create("d1"),
            PartKind.Attachment,
            "message/delivery-status",
            DocumentContent.FromBytes(new byte[] { 0x41, 0xC3, 0x28 }));

        var failure = await Assert.ThrowsAsync<ExtractionException>(
            async () => await new MimeStructureExtractor().ExtractAsync(part, CancellationToken.None));

        Assert.Equal(ExtractionErrorCodes.Malformed, failure.Error.Code);
    }

    [Fact]
    public async Task Scans_the_recipient_of_a_bounce_end_to_end()
    {
        using var message = MessageFixture.Message(
            "From: postmaster@example.invalid",
            "Subject: Undelivered Mail",
            "Content-Type: multipart/report; report-type=delivery-status; boundary=\"B\"",
            string.Empty,
            "--B",
            "Content-Type: text/plain",
            string.Empty,
            "delivery failed",
            "--B",
            "Content-Type: message/delivery-status",
            string.Empty,
            "Final-Recipient: rfc822; carla.neri@example.invalid",
            "Action: failed",
            "--B--");

        var bundle = await MessageFixture.ReadAsync(message);
        var extracted = await MessageFixture.ExtractAsync(bundle);

        Assert.Empty(extracted.Failures);
        Assert.Contains(
            extracted.Segments,
            segment => segment.Text.Contains("carla.neri@example.invalid", StringComparison.Ordinal));
    }

    private static DocumentPart Report(string mediaType, string text) =>
        DocumentPart.Create(
            PartId.Create("d1"),
            PartKind.Attachment,
            mediaType,
            DocumentContent.FromBytes(Encoding.ASCII.GetBytes(text)));
}

public sealed class MimeReaderOptionsTests
{
    [Fact]
    public void Defaults_to_the_headers_that_carry_the_people_involved()
    {
        Assert.Contains("From", MimeReaderOptions.Default.Headers);
        Assert.Contains("Subject", MimeReaderOptions.Default.Headers);
        Assert.Equal(8, MimeReaderOptions.Default.MaxNestingDepth);
    }

    [Fact]
    public void Refuses_a_nesting_depth_outside_the_allowed_range()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MimeReaderOptions.Default.WithMaxNestingDepth(0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MimeReaderOptions.Default.WithMaxNestingDepth(MimeReaderOptions.MaximumNestingDepth + 1));
    }

    [Fact]
    public void Refuses_an_empty_header_list()
    {
        Assert.Throws<ArgumentException>(() => MimeReaderOptions.Default.WithHeaders([]));
        Assert.Throws<ArgumentException>(() => MimeReaderOptions.Default.WithHeaders([" "]));
    }

    [Fact]
    public void Keeps_the_previous_instance_unchanged()
    {
        var narrowed = MimeReaderOptions.Default.WithHeaders(["Subject"]);

        Assert.Single(narrowed.Headers);
        Assert.Equal(7, MimeReaderOptions.Default.Headers.Count);
    }

    [Fact]
    public async Task Matches_header_names_without_regard_to_case()
    {
        using var message = MessageFixture.Message(
            "sUbJeCt: Pratica",
            "From: a@example.invalid",
            "Content-Type: text/plain",
            string.Empty,
            "body");

        var bundle = await MessageFixture.ReadAsync(message);
        var emitted = bundle.Parts
            .Where(part => part.Kind is PartKind.Header or PartKind.Subject)
            .Select(MessageFixture.TextOf)
            .ToList();

        Assert.Contains("sUbJeCt: Pratica", emitted);
    }

    [Fact]
    public async Task Emits_the_headers_a_caller_adds()
    {
        var options = MimeReaderOptions.Default.WithHeaders(["Subject", "X-Case-Owner"]);

        using var message = MessageFixture.Message(
            "From: a@example.invalid",
            "Subject: s",
            "X-Case-Owner: Luisa Fermi",
            "Content-Type: text/plain",
            string.Empty,
            "body");

        var bundle = await MessageFixture.ReadAsync(message, options);
        var emitted = bundle.Parts
            .Where(part => part.Kind is PartKind.Header or PartKind.Subject)
            .Select(MessageFixture.TextOf)
            .ToList();

        Assert.Equal(2, emitted.Count);
        Assert.Contains("X-Case-Owner: Luisa Fermi", emitted);
    }
}
