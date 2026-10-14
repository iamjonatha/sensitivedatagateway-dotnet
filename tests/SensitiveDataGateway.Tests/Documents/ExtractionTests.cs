using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Tests.Documents;

/// <summary>
/// Builds the bundles the extraction tests read.
/// </summary>
/// <remarks>
/// Every value in here is invented. None of it is a real name, address or account.
/// </remarks>
internal static class ExtractionFixture
{
    public const string TextMediaType = "text/plain";

    public static DocumentPart Text(string id, string text) =>
        DocumentPart.Create(PartId.Create(id), PartKind.Body, TextMediaType, DocumentContent.FromText(text));

    public static DocumentPart Bytes(string id, string mediaType, byte[] bytes) =>
        DocumentPart.Create(PartId.Create(id), PartKind.Attachment, mediaType, DocumentContent.FromBytes(bytes));

    public static DocumentPart Streamed(string id, byte[] bytes, long? length) =>
        DocumentPart.Create(
            PartId.Create(id),
            PartKind.Attachment,
            TextMediaType,
            DocumentContent.FromStream(_ => new ValueTask<Stream>(new MemoryStream(bytes)), length));

    public static DocumentBundle Bundle(DocumentPart primary, params DocumentPart[] attachments) =>
        DocumentBundle.Create(BundleId.Create("bundle"), primary, attachments);

    public static ExtractionPipeline Pipeline() => new([new PlainTextExtractor()]);

    public static ProcessingLimits Limits(long maxPartBytes, int maxPartCount, int maxExtractedCharacters) =>
        ProcessingLimits.Create(
            maxBundleBytes: 1024,
            maxPartBytes,
            maxPartCount,
            maxExtractedCharacters,
            maxEntities: 16,
            maxProcessingTime: TimeSpan.FromMinutes(1));

    public static Task<ExtractedDocument> ExtractAsync(DocumentBundle bundle) =>
        Pipeline().ExtractAsync(bundle, ProcessingLimits.Default, CancellationToken.None);
}

public sealed class PlainTextExtractorTests
{
    [Fact]
    public void Accepts_only_textual_media_types()
    {
        var extractor = new PlainTextExtractor();

        Assert.True(extractor.CanExtract("text/plain"));
        Assert.True(extractor.CanExtract("text/html"));
        Assert.False(extractor.CanExtract("application/pdf"));
        Assert.False(extractor.CanExtract("application/octet-stream"));
    }

    [Fact]
    public async Task Splits_on_blank_lines_and_not_on_line_breaks()
    {
        var bundle = ExtractionFixture.Bundle(
            ExtractionFixture.Text("body", "first line\nsecond line\n\nsecond paragraph\n"));

        var extracted = await ExtractionFixture.ExtractAsync(bundle);

        Assert.Equal(
            ["first line\nsecond line", "second paragraph"],
            extracted.Segments.Select(segment => segment.Text));
    }

    [Fact]
    public async Task Keeps_a_value_written_across_two_lines_in_one_segment()
    {
        var bundle = ExtractionFixture.Bundle(
            ExtractionFixture.Text("body", "IBAN IT60X054\n2811101000000123456"));

        var extracted = await ExtractionFixture.ExtractAsync(bundle);

        var segment = Assert.Single(extracted.Segments);
        Assert.Contains("IT60X054\n2811101000000123456", segment.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Derives_the_same_identifiers_every_time()
    {
        var first = await ExtractionFixture.ExtractAsync(
            ExtractionFixture.Bundle(ExtractionFixture.Text("body", "one\n\ntwo")));
        var second = await ExtractionFixture.ExtractAsync(
            ExtractionFixture.Bundle(ExtractionFixture.Text("body", "one\n\ntwo")));

        Assert.Equal(
            first.Segments.Select(segment => segment.Id.Value),
            second.Segments.Select(segment => segment.Id.Value));
        Assert.Equal(["body:p1", "body:p2"], first.Segments.Select(segment => segment.Id.Value));
    }

    [Fact]
    public async Task Reads_bytes_as_strict_utf8()
    {
        var bundle = ExtractionFixture.Bundle(
            ExtractionFixture.Bytes("body", "text/plain", Encoding.UTF8.GetBytes("caff\u00e8")));

        var extracted = await ExtractionFixture.ExtractAsync(bundle);

        Assert.Equal("caff\u00e8", Assert.Single(extracted.Segments).Text);
    }

    [Fact]
    public async Task Records_a_failure_when_the_bytes_are_not_utf8()
    {
        var bundle = ExtractionFixture.Bundle(
            ExtractionFixture.Bytes("body", "text/plain", [0xC3, 0x28]));

        var extracted = await ExtractionFixture.ExtractAsync(bundle);

        Assert.False(extracted.IsComplete);
        Assert.Equal(ExtractionFailureReason.Malformed, Assert.Single(extracted.Failures).Reason);
        Assert.Empty(extracted.Segments);
    }

    [Fact]
    public async Task Reads_a_stream_of_declared_size()
    {
        var bytes = Encoding.UTF8.GetBytes("streamed text");
        var bundle = ExtractionFixture.Bundle(
            ExtractionFixture.Text("body", "body text"),
            ExtractionFixture.Streamed("attachment", bytes, bytes.Length));

        var extracted = await ExtractionFixture.ExtractAsync(bundle);

        Assert.True(extracted.IsComplete);
        Assert.Contains("streamed text", extracted.Segments.Select(segment => segment.Text), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Produces_no_segment_for_text_that_is_only_whitespace()
    {
        var bundle = ExtractionFixture.Bundle(
            ExtractionFixture.Text("body", "   \n\t\n   "),
            ExtractionFixture.Text("attachment", "real text"));

        var extracted = await ExtractionFixture.ExtractAsync(bundle);

        var segment = Assert.Single(extracted.Segments);
        Assert.Equal("real text", segment.Text);
    }
}

public sealed class ExtractionPipelineTests
{
    [Fact]
    public async Task Records_a_failure_for_a_part_no_extractor_accepts()
    {
        var bundle = ExtractionFixture.Bundle(
            ExtractionFixture.Text("body", "body text"),
            ExtractionFixture.Bytes("report", "application/pdf", [0x25, 0x50, 0x44, 0x46]));

        var extracted = await ExtractionFixture.ExtractAsync(bundle);

        Assert.False(extracted.IsComplete);
        var failure = Assert.Single(extracted.Failures);
        Assert.Equal(ExtractionFailureReason.UnsupportedMediaType, failure.Reason);
        Assert.Equal("report", failure.PartId.Value);
    }

    [Fact]
    public async Task Never_falls_back_to_the_text_extractor_for_binary_parts()
    {
        var bundle = ExtractionFixture.Bundle(
            ExtractionFixture.Text("body", "body text"),
            ExtractionFixture.Bytes("report", "application/pdf", Encoding.UTF8.GetBytes("%PDF-1.7 trailer")));

        var extracted = await ExtractionFixture.ExtractAsync(bundle);

        Assert.DoesNotContain(
            extracted.Segments,
            segment => segment.Text.Contains("PDF", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ignores_media_type_parameters_when_choosing_an_extractor()
    {
        var part = DocumentPart.Create(
            PartId.Create("body"),
            PartKind.Body,
            "text/plain; charset=utf-8",
            DocumentContent.FromText("body text"));

        var extracted = await ExtractionFixture.ExtractAsync(ExtractionFixture.Bundle(part));

        Assert.True(extracted.IsComplete);
        Assert.Equal("body text", Assert.Single(extracted.Segments).Text);
    }

    [Fact]
    public async Task Scans_the_file_name_and_the_origin_as_content()
    {
        var part = DocumentPart.Create(
            PartId.Create("body"),
            PartKind.Body,
            ExtractionFixture.TextMediaType,
            DocumentContent.FromText("body text"),
            parent: null,
            DocumentMetadata.Create("invoice-bianchi.txt", language: null, origin: "mailbox"));

        var extracted = await ExtractionFixture.ExtractAsync(ExtractionFixture.Bundle(part));

        Assert.Contains("invoice-bianchi.txt", extracted.Segments.Select(segment => segment.Text), StringComparer.Ordinal);
        Assert.Contains("mailbox", extracted.Segments.Select(segment => segment.Text), StringComparer.Ordinal);
        Assert.Contains(extracted.Segments, segment => segment.Origin.Kind == SegmentOriginKind.Metadata);
    }

    [Fact]
    public async Task Scans_bundle_metadata_and_properties_as_content()
    {
        var bundle = DocumentBundle.Create(
            BundleId.Create("bundle"),
            ExtractionFixture.Text("body", "body text"),
            [],
            DocumentMetadata.Create("thread.eml", language: null, origin: null)
                .WithProperty("subject", "Payment for Bianchi"),
            DateTimeOffset.UnixEpoch);

        var extracted = await ExtractionFixture.ExtractAsync(bundle);

        Assert.Contains("Payment for Bianchi", extracted.Segments.Select(segment => segment.Text), StringComparer.Ordinal);
        Assert.Contains(
            "body:m:bundle.prop.subject",
            extracted.Segments.Select(segment => segment.Id.Value),
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task Numbers_segments_within_a_part_without_a_gap_or_a_repeat()
    {
        var part = DocumentPart.Create(
            PartId.Create("body"),
            PartKind.Body,
            ExtractionFixture.TextMediaType,
            DocumentContent.FromText("one\n\ntwo"),
            parent: null,
            DocumentMetadata.Create("note.txt", language: null, origin: null));

        var extracted = await ExtractionFixture.ExtractAsync(ExtractionFixture.Bundle(part));

        Assert.Equal([0, 1, 2], extracted.Segments.Select(segment => segment.Order));
    }

    [Fact]
    public async Task Refuses_a_stream_whose_size_is_not_declared()
    {
        var bundle = ExtractionFixture.Bundle(
            ExtractionFixture.Text("body", "body text"),
            ExtractionFixture.Streamed("attachment", Encoding.UTF8.GetBytes("hidden"), length: null));

        var extracted = await ExtractionFixture.ExtractAsync(bundle);

        Assert.False(extracted.IsComplete);
        Assert.Equal(ExtractionFailureReason.LimitExceeded, Assert.Single(extracted.Failures).Reason);
    }

    [Fact]
    public async Task Records_a_failure_for_a_part_larger_than_the_policy_allows()
    {
        var bundle = ExtractionFixture.Bundle(
            ExtractionFixture.Text("body", "small"),
            ExtractionFixture.Text("attachment", "far too long to fit in the bound"));

        var extracted = await ExtractionFixture.Pipeline().ExtractAsync(
            bundle,
            ExtractionFixture.Limits(maxPartBytes: 8, maxPartCount: 8, maxExtractedCharacters: 1024),
            CancellationToken.None);

        Assert.False(extracted.IsComplete);
        var failure = Assert.Single(extracted.Failures);
        Assert.Equal(ExtractionFailureReason.LimitExceeded, failure.Reason);
        Assert.Equal("attachment", failure.PartId.Value);
    }

    [Fact]
    public async Task Refuses_a_bundle_with_more_parts_than_the_policy_allows()
    {
        var bundle = ExtractionFixture.Bundle(
            ExtractionFixture.Text("body", "one"),
            ExtractionFixture.Text("attachment", "two"));

        await Assert.ThrowsAsync<ProcessingLimitExceededException>(
            () => ExtractionFixture.Pipeline().ExtractAsync(
                bundle,
                ExtractionFixture.Limits(maxPartBytes: 1024, maxPartCount: 1, maxExtractedCharacters: 1024),
                CancellationToken.None));
    }

    [Fact]
    public async Task Stops_rather_than_truncating_when_the_text_exceeds_the_bound()
    {
        var bundle = ExtractionFixture.Bundle(ExtractionFixture.Text("body", "much longer than four"));

        await Assert.ThrowsAsync<ProcessingLimitExceededException>(
            () => ExtractionFixture.Pipeline().ExtractAsync(
                bundle,
                ExtractionFixture.Limits(maxPartBytes: 1024, maxPartCount: 8, maxExtractedCharacters: 4),
                CancellationToken.None));
    }

    [Fact]
    public async Task Refuses_a_bundle_that_declares_more_bytes_than_the_policy_allows()
    {
        var limits = ProcessingLimits.Create(
            maxBundleBytes: 8,
            maxPartBytes: 8,
            maxPartCount: 8,
            maxExtractedCharacters: 1024,
            maxEntities: 16,
            maxProcessingTime: TimeSpan.FromMinutes(1));

        var bundle = ExtractionFixture.Bundle(
            ExtractionFixture.Text("body", "five5"),
            ExtractionFixture.Text("attachment", "five5"));

        await Assert.ThrowsAsync<ProcessingLimitExceededException>(
            () => ExtractionFixture.Pipeline().ExtractAsync(bundle, limits, CancellationToken.None));
    }

    [Fact]
    public async Task Refuses_a_bundle_that_yielded_neither_text_nor_a_failure()
    {
        var bundle = ExtractionFixture.Bundle(ExtractionFixture.Text("body", string.Empty));

        await Assert.ThrowsAsync<ExtractionException>(() => ExtractionFixture.ExtractAsync(bundle));
    }

    [Fact]
    public void Refuses_to_be_built_without_an_extractor() =>
        Assert.Throws<ArgumentException>(() => new ExtractionPipeline([]));

    [Fact]
    public async Task Lets_an_unexpected_extractor_failure_propagate()
    {
        var pipeline = new ExtractionPipeline([new ThrowingExtractor()]);
        var bundle = ExtractionFixture.Bundle(ExtractionFixture.Text("body", "body text"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => pipeline.ExtractAsync(bundle, ProcessingLimits.Default, CancellationToken.None));
    }

    [Fact]
    public async Task Reports_the_extractor_that_ran()
    {
        var extracted = await ExtractionFixture.ExtractAsync(
            ExtractionFixture.Bundle(ExtractionFixture.Text("body", "body text")));

        var version = Assert.Single(extracted.Extractors);
        Assert.Equal("builtin.text", version.ExtractorId.Value);
    }

    private sealed class ThrowingExtractor : IDocumentExtractor
    {
        public ExtractorVersion Version { get; } =
            ExtractorVersion.Create(ExtractorId.Create("test.throwing"), "1.0.0");

        public bool CanExtract(string mediaType) => true;

        public ValueTask<IReadOnlyList<TextSegment>> ExtractAsync(
            DocumentPart part,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The extractor is broken.");
    }
}
