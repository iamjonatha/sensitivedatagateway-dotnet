using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Documents.Recognition;

namespace SensitiveDataGateway.Tests.Documents;

public sealed class RecognizingExtractorCompositionTests
{
    [Fact]
    public void Refuses_a_recognizer_that_runs_outside_the_boundary()
    {
        var recognizer = FakeRecognizer.Reading(RecognizerLocality.Remote, ("invoice 4471", 0.99));

        var exception = Assert.Throws<ArgumentException>(() => RecognizingExtractor.Create(recognizer));

        Assert.Contains("outside the trust boundary", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Accepts_a_recognizer_that_runs_outside_the_boundary_only_through_the_named_factory()
    {
        var recognizer = FakeRecognizer.Reading(RecognizerLocality.Remote, ("invoice 4471", 0.99));

        var extractor = RecognizingExtractor.CreateOutsideTheTrustBoundary(recognizer);

        Assert.Equal(RecognizerLocality.Remote, extractor.Locality);
    }

    [Theory]
    [InlineData(RecognizerLocality.InProcess)]
    [InlineData(RecognizerLocality.SelfHosted)]
    public void Accepts_a_recognizer_that_runs_inside_the_boundary(RecognizerLocality locality)
    {
        var extractor = RecognizingExtractor.Create(FakeRecognizer.Reading(locality, ("invoice 4471", 0.99)));

        Assert.Equal(locality, extractor.Locality);
    }

    [Theory]
    [InlineData(RecognizerLocality.InProcess, "recognizer/in-process/vendor-ocr@1.0")]
    [InlineData(RecognizerLocality.SelfHosted, "recognizer/self-hosted/vendor-ocr@1.0")]
    public void Records_where_the_recognizer_runs_in_the_extractor_identity(
        RecognizerLocality locality,
        string expected)
    {
        var extractor = RecognizingExtractor.Create(FakeRecognizer.Named("vendor-ocr", locality));

        Assert.Equal(expected, extractor.Version.ToString());
    }

    [Fact]
    public void Records_a_remote_recognizer_as_remote()
    {
        var extractor = RecognizingExtractor.CreateOutsideTheTrustBoundary(
            FakeRecognizer.Named("vendor-ocr", RecognizerLocality.Remote));

        Assert.Equal("recognizer/remote/vendor-ocr@1.0", extractor.Version.ToString());
    }

    [Fact]
    public void Refuses_a_recognizer_that_carries_no_version()
    {
        var exception = Assert.Throws<ArgumentException>(() => RecognizingExtractor.Create(new UnversionedRecognizer()));

        Assert.Contains("no version", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_a_recognizer_named_too_long_to_record()
    {
        var recognizer = FakeRecognizer.Named(new string('r', RecognizerId.MaxLength), RecognizerLocality.SelfHosted);

        var exception = Assert.Throws<ArgumentException>(() => RecognizingExtractor.Create(recognizer));

        Assert.Contains("too long to record", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Offers_the_part_to_the_recognizer_to_decide_the_media_type()
    {
        var extractor = RecognizingExtractor.Create(FakeRecognizer.Reading(("invoice 4471", 0.99)));

        Assert.True(extractor.CanExtract("image/png"));
        Assert.False(extractor.CanExtract("application/pdf"));
    }

    private sealed class UnversionedRecognizer : ITextRecognizer
    {
        public RecognizerVersion Version => default;

        public bool CanRecognize(string mediaType) => true;

        public ValueTask<RecognizedText> RecognizeAsync(DocumentPart part, CancellationToken cancellationToken) =>
            new(RecognizedText.Empty());
    }
}

public sealed class RecognizingExtractorTests
{
    [Fact]
    public async Task Produces_one_segment_per_line_in_reading_order()
    {
        var extractor = RecognizingExtractor.Create(
            FakeRecognizer.Reading(
                ("Beneficiary: Jonas Weber", 0.98),
                ("Account: NL91 ABNA 0417 1643 00", 0.95)));

        var segments = await RecognitionFixture.ReadAsync(extractor);

        Assert.Equal(
            ["Beneficiary: Jonas Weber", "Account: NL91 ABNA 0417 1643 00"],
            segments.Select(segment => segment.Text));
    }

    [Fact]
    public async Task Marks_every_segment_as_coming_from_the_part_that_was_recognized()
    {
        var extractor = RecognizingExtractor.Create(FakeRecognizer.Reading(("invoice 4471", 0.99)));

        var segments = await RecognitionFixture.ReadAsync(extractor);

        var segment = Assert.Single(segments);
        Assert.Equal(PartId.Create("scan"), segment.PartId);
        Assert.Equal(SegmentOriginKind.Paragraph, segment.Origin.Kind);
    }

    [Fact]
    public async Task Drops_a_blank_line_without_dropping_the_part()
    {
        var extractor = RecognizingExtractor.Create(
            FakeRecognizer.Reading(("invoice 4471", 0.99), ("   ", 0.99), ("total 180,00", 0.97)));

        var segments = await RecognitionFixture.ReadAsync(extractor);

        Assert.Equal(["invoice 4471", "total 180,00"], segments.Select(segment => segment.Text));
    }

    [Fact]
    public async Task Reads_the_part_once()
    {
        var recognizer = FakeRecognizer.Reading(("invoice 4471", 0.99));

        await RecognitionFixture.ReadAsync(RecognizingExtractor.Create(recognizer));

        Assert.Equal(1, recognizer.Calls);
    }
}

public sealed class RecognitionFailClosedTests
{
    [Fact]
    public async Task Refuses_a_part_the_recognizer_read_nothing_from()
    {
        var error = await RecognitionFixture.RefusedAsync(
            RecognizingExtractor.Create(FakeRecognizer.ReadingNothing()));

        Assert.Equal(ExtractionErrorCodes.UnreadableConstruct, error.Code);
    }

    [Fact]
    public async Task Refuses_a_part_whose_lines_are_all_blank()
    {
        var error = await RecognitionFixture.RefusedAsync(
            RecognizingExtractor.Create(FakeRecognizer.Reading(("   ", 0.99), ("\t", 0.99))));

        Assert.Equal(ExtractionErrorCodes.UnreadableConstruct, error.Code);
    }

    [Fact]
    public async Task Refuses_the_whole_part_when_one_line_falls_below_the_threshold()
    {
        var extractor = RecognizingExtractor.Create(
            FakeRecognizer.Reading(
                ("Beneficiary: Jonas Weber", 0.99),
                ("Account: NL91 ABNA 0417 1643 00", 0.41)));

        var error = await RecognitionFixture.RefusedAsync(extractor);

        Assert.Equal(ExtractionErrorCodes.UnreadableConstruct, error.Code);
    }

    [Fact]
    public async Task Does_not_average_away_one_unreadable_line()
    {
        var lines = Enumerable.Range(1, 40)
            .Select(ordinal => ($"line {ordinal}", 1.0))
            .Append(("the number nobody could read", 0.10))
            .ToArray();

        var error = await RecognitionFixture.RefusedAsync(
            RecognizingExtractor.Create(FakeRecognizer.Reading(lines)));

        Assert.Equal(ExtractionErrorCodes.UnreadableConstruct, error.Code);
    }

    [Fact]
    public async Task Refuses_a_part_that_yields_more_lines_than_the_bound_allows()
    {
        var lines = Enumerable.Range(1, 5).Select(ordinal => ($"line {ordinal}", 1.0)).ToArray();
        var options = RecognitionOptions.Default.WithMaxLines(4);

        var error = await RecognitionFixture.RefusedAsync(
            RecognizingExtractor.Create(FakeRecognizer.Reading(lines), options));

        Assert.Equal(ExtractionErrorCodes.LimitExceeded, error.Code);
    }

    [Fact]
    public async Task Refuses_a_recognizer_that_returns_no_result()
    {
        var error = await RecognitionFixture.RefusedAsync(
            RecognizingExtractor.Create(FakeRecognizer.ReturningNull()));

        Assert.Equal(ExtractionErrorCodes.UnreadableConstruct, error.Code);
    }

    [Fact]
    public async Task Passes_on_the_failure_the_recognizer_raised()
    {
        var raised = GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.TimedOut,
            "The recognizer ran out of time.");

        var error = await RecognitionFixture.RefusedAsync(
            RecognizingExtractor.Create(FakeRecognizer.Failing(raised)));

        Assert.Equal(ExtractionErrorCodes.TimedOut, error.Code);
    }

    [Fact]
    public async Task Names_no_recognized_text_in_any_refusal()
    {
        var extractor = RecognizingExtractor.Create(
            FakeRecognizer.Reading(("Beneficiary: Jonas Weber", 0.11)));

        var error = await RecognitionFixture.RefusedAsync(extractor);

        Assert.DoesNotContain("Jonas", error.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Weber", error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Refuses_a_threshold_that_accepts_anything()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RecognitionOptions.Default.WithMinimumConfidence(0));
    }

    [Fact]
    public async Task Accepts_a_part_the_recognizer_was_sure_of()
    {
        var options = RecognitionOptions.Default.WithMinimumConfidence(0.90);
        var extractor = RecognizingExtractor.Create(FakeRecognizer.Reading(("invoice 4471", 0.90)), options);

        var segments = await RecognitionFixture.ReadAsync(extractor);

        Assert.Single(segments);
    }
}
