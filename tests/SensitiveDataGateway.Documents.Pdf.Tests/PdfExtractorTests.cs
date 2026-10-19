using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Documents.Pdf.Tests;

public sealed class PdfExtractorTests
{
    [Fact]
    public void Claims_pdf_and_nothing_else()
    {
        var extractor = new PdfExtractor();

        Assert.True(extractor.CanExtract("application/pdf"));
        Assert.True(extractor.CanExtract("APPLICATION/PDF"));
        Assert.True(extractor.CanExtract("application/pdf; version=1.7"));

        Assert.False(extractor.CanExtract("application/x-pdf"));
        Assert.False(extractor.CanExtract("text/plain"));
        Assert.False(extractor.CanExtract("message/rfc822"));
    }

    [Fact]
    public async Task Yields_one_segment_per_page_in_order()
    {
        var bytes = PdfFixture.WithPages("Primo foglio", "Secondo foglio", "Terzo foglio");

        var segments = await PdfFixture.ReadAsync(bytes);
        var pages = segments.Where(segment => segment.Origin.Kind == SegmentOriginKind.Page).ToList();

        Assert.Equal(3, pages.Count);
        Assert.Equal([1, 2, 3], pages.Select(page => page.Origin.Ordinal));
        Assert.Contains("Primo foglio", pages[0].Text, StringComparison.Ordinal);
        Assert.Contains("Terzo foglio", pages[2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gives_every_segment_an_identifier_derived_from_an_ordinal()
    {
        var bytes = PdfFixture.WithInformation("Contratto", "Carla Neri");

        var segments = await PdfFixture.ReadAsync(bytes);

        Assert.All(segments, segment => Assert.Equal(PartId.Create("p1"), segment.PartId));
        Assert.DoesNotContain(segments, segment => segment.Id.Value.Contains("Contratto", StringComparison.Ordinal));
        Assert.DoesNotContain(segments, segment => segment.Id.Value.Contains("Carla", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reads_the_document_information_a_person_wrote()
    {
        var bytes = PdfFixture.WithInformation("Contratto di mandato", "Carla Neri");

        var segments = await PdfFixture.ReadAsync(bytes);
        var metadata = segments.Where(segment => segment.Origin.Kind == SegmentOriginKind.Metadata).ToList();

        Assert.Contains(metadata, segment => segment.Text == "Contratto di mandato");
        Assert.Contains(metadata, segment => segment.Text == "Carla Neri");
        Assert.Contains(metadata, segment => segment.Origin.Locator == "title");
        Assert.Contains(metadata, segment => segment.Origin.Locator == "author");
    }

    [Fact]
    public async Task Reads_the_target_of_a_link_because_an_address_hides_in_one()
    {
        var bytes = PdfFixture.WithLink("mailto:carla.neri@example.invalid");

        var segments = await PdfFixture.ReadAsync(bytes);
        var annotations = segments.Where(segment => segment.Origin.Kind == SegmentOriginKind.Annotation).ToList();

        Assert.Contains(
            annotations,
            segment => segment.Text.Contains("carla.neri@example.invalid", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reads_the_value_a_person_typed_into_a_form_field()
    {
        var bytes = RawPdf.OnePage(
            catalogEntries: "/AcroForm << /Fields [4 0 R] >>",
            pageEntries: "/Annots [4 0 R]",
            extraObjects:
            [
                "<< /Type /Annot /Subtype /Widget /Rect [50 700 250 720] /FT /Tx /T (holder) /V (Carla Neri) >>",
            ]);

        var segments = await PdfFixture.ReadAsync(bytes);

        Assert.Contains(segments, segment => segment.Text == "Carla Neri");
        Assert.All(
            segments.Where(segment => segment.Text == "Carla Neri"),
            segment => Assert.Equal(SegmentOriginKind.Annotation, segment.Origin.Kind));
    }

    [Fact]
    public async Task Reads_a_page_that_carries_no_text_and_draws_nothing()
    {
        var segments = await PdfFixture.ReadAsync(RawPdf.OnePage());

        Assert.Empty(segments);
    }

    [Fact]
    public async Task Stops_reading_when_the_token_is_cancelled()
    {
        var bytes = PdfFixture.WithPages("Uno", "Due", "Tre");
        using var source = new CancellationTokenSource();

        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await new PdfExtractor().ExtractAsync(PdfFixture.Part(bytes), source.Token));
    }

    [Fact]
    public void Refuses_options_outside_their_bounds()
    {
        var options = PdfExtractorOptions.Default;

        Assert.Throws<ArgumentOutOfRangeException>(() => options.WithMaxPageCount(0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => options.WithMaxPageCount(PdfExtractorOptions.MaximumPageCount + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => options.WithMaxBytes(0));
    }

    [Fact]
    public void Leaves_the_options_it_was_built_from_alone()
    {
        var original = PdfExtractorOptions.Default;

        var narrowed = original.WithMaxPageCount(1).WithMaxBytes(PdfExtractorOptions.MinimumBytes);

        Assert.NotEqual(original.MaxPageCount, narrowed.MaxPageCount);
        Assert.Equal(1, narrowed.MaxPageCount);
        Assert.Equal(PdfExtractorOptions.MinimumBytes, narrowed.MaxBytes);
    }
}
