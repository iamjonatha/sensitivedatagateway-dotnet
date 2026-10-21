using System;
using System.Linq;
using System.Threading.Tasks;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Documents.OpenXml.Tests;

/// <summary>
/// Covers what the Word extractor reads out of a document.
/// </summary>
public sealed class DocxExtractorTests
{
    [Fact]
    public void Accepts_the_media_type_of_a_word_document() =>
        Assert.True(new DocxExtractor().CanExtract(OpenXmlFixture.WordMediaType));

    [Fact]
    public void Accepts_the_media_type_with_parameters_attached() =>
        Assert.True(new DocxExtractor().CanExtract($"{OpenXmlFixture.WordMediaType}; charset=utf-8"));

    [Fact]
    public void Declines_a_workbook() =>
        Assert.False(new DocxExtractor().CanExtract(OpenXmlFixture.ExcelMediaType));

    [Fact]
    public async Task Reads_one_segment_per_paragraph()
    {
        var segments = await OpenXmlFixture.ReadDocxAsync(
            DocxFixture.WithParagraphs("Prima riga", "Seconda riga"));

        Assert.Equal(["Prima riga", "Seconda riga"], segments.Select(segment => segment.Text));
    }

    [Fact]
    public async Task Numbers_the_segments_in_reading_order()
    {
        var segments = await OpenXmlFixture.ReadDocxAsync(
            DocxFixture.WithParagraphs("Uno", "Due", "Tre"));

        Assert.Equal([0, 1, 2], segments.Select(segment => segment.Order));
    }

    /// <summary>
    /// Proves that a value spread across adjacent cells stays in one segment.
    /// </summary>
    /// <returns>A task that completes when the document has been read.</returns>
    /// <remarks>
    /// An author who puts an account number in one column and its holder in the next has
    /// written one fact across two cells. Handed the cells separately a detector sees a
    /// fragment of an account number and a fragment of a name, and matches neither.
    /// </remarks>
    [Fact]
    public async Task Reads_a_table_row_as_one_segment()
    {
        var segments = await OpenXmlFixture.ReadDocxAsync(
            DocxFixture.WithTable(["IT60", "X0542811101"], ["Seconda", "riga"]));

        Assert.Equal(
            ["IT60 X0542811101", "Seconda riga"],
            segments.Select(segment => segment.Text));
    }

    [Fact]
    public async Task Marks_a_table_row_as_coming_from_a_row()
    {
        var segments = await OpenXmlFixture.ReadDocxAsync(DocxFixture.WithTable(["Uno", "Due"]));

        Assert.Equal(SegmentOriginKind.Row, segments.Single().Origin.Kind);
    }

    [Fact]
    public async Task Reads_the_header_and_the_footer()
    {
        var segments = await OpenXmlFixture.ReadDocxAsync(
            DocxFixture.WithHeaderAndFooter("Intestazione", "Pie di pagina"));

        var text = OpenXmlFixture.TextOf(segments);

        Assert.Contains("Intestazione", text, StringComparison.Ordinal);
        Assert.Contains("Pie di pagina", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reads_a_comment_together_with_the_name_on_it()
    {
        var segments = await OpenXmlFixture.ReadDocxAsync(
            DocxFixture.WithComment("Giulia Bianchi", "Chiedere il codice fiscale"));

        var comment = segments.Single(segment => segment.Origin.Kind == SegmentOriginKind.Annotation);

        Assert.Contains("Giulia Bianchi", comment.Text, StringComparison.Ordinal);
        Assert.Contains("Chiedere il codice fiscale", comment.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reads_a_footnote()
    {
        var segments = await OpenXmlFixture.ReadDocxAsync(
            DocxFixture.WithFootnote("Nota in calce"));

        Assert.Contains("Nota in calce", OpenXmlFixture.TextOf(segments), StringComparison.Ordinal);
    }

    /// <summary>
    /// Proves that the target of a link is read as a string.
    /// </summary>
    /// <returns>A task that completes when the document has been read.</returns>
    /// <remarks>
    /// The target is read, never opened. A <c>mailto:</c> link is an address written down,
    /// and it is nothing more than a string here.
    /// </remarks>
    [Fact]
    public async Task Reads_the_target_of_a_link_without_following_it()
    {
        var segments = await OpenXmlFixture.ReadDocxAsync(
            DocxFixture.WithHyperlink("mailto:mario.rossi@example.invalid"));

        Assert.Contains(
            "mario.rossi@example.invalid",
            OpenXmlFixture.TextOf(segments),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Proves that text a reviewer struck out is still read.
    /// </summary>
    /// <returns>A task that completes when the document has been read.</returns>
    /// <remarks>
    /// A tracked deletion is invisible to a reader with revisions hidden and perfectly
    /// present in the file. A name an author removed before sending is exactly the kind of
    /// value this library exists to keep out of a request.
    /// </remarks>
    [Fact]
    public async Task Reads_text_a_reviewer_deleted()
    {
        var segments = await OpenXmlFixture.ReadDocxAsync(
            DocxFixture.WithTrackedDeletion("Il cliente ", "Mario Rossi"));

        Assert.Contains("Mario Rossi", OpenXmlFixture.TextOf(segments), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reads_the_package_properties()
    {
        var segments = await OpenXmlFixture.ReadDocxAsync(
            DocxFixture.WithProperties("Contratto", "Giulia Bianchi"));

        var metadata = segments
            .Where(segment => segment.Origin.Kind == SegmentOriginKind.Metadata)
            .Select(segment => segment.Text);

        Assert.Equal(["Contratto", "Giulia Bianchi"], metadata);
    }

    [Fact]
    public async Task Drops_a_paragraph_with_no_characters()
    {
        var segments = await OpenXmlFixture.ReadDocxAsync(
            DocxFixture.WithParagraphs("Uno", "   ", "Due"));

        Assert.Equal(["Uno", "Due"], segments.Select(segment => segment.Text));
    }

    [Fact]
    public async Task Attributes_every_segment_to_the_part_it_came_from()
    {
        var segments = await OpenXmlFixture.ReadDocxAsync(DocxFixture.WithParagraphs("Uno"));

        Assert.All(segments, segment => Assert.Equal(PartId.Create("p1"), segment.PartId));
    }

    [Fact]
    public void Reports_its_own_identity() =>
        Assert.Equal(ExtractorId.Create("docx"), new DocxExtractor().Version.ExtractorId);
}
