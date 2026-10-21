using System;
using System.Linq;
using System.Threading.Tasks;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Documents.OpenXml.Tests;

/// <summary>
/// Covers what the Excel extractor reads out of a workbook.
/// </summary>
public sealed class XlsxExtractorTests
{
    [Fact]
    public void Accepts_the_media_type_of_a_workbook() =>
        Assert.True(new XlsxExtractor().CanExtract(OpenXmlFixture.ExcelMediaType));

    [Fact]
    public void Declines_a_word_document() =>
        Assert.False(new XlsxExtractor().CanExtract(OpenXmlFixture.WordMediaType));

    /// <summary>
    /// Proves that a value spread across adjacent columns stays in one segment.
    /// </summary>
    /// <returns>A task that completes when the workbook has been read.</returns>
    [Fact]
    public async Task Reads_a_row_as_one_segment()
    {
        var segments = await OpenXmlFixture.ReadXlsxAsync(
            XlsxFixture.WithRows("Clienti", ["IT60", "X0542811101"], ["Seconda", "riga"]));

        var rows = segments
            .Where(segment => segment.Origin.Kind == SegmentOriginKind.Row)
            .Select(segment => segment.Text);

        Assert.Equal(["IT60 X0542811101", "Seconda riga"], rows);
    }

    [Fact]
    public async Task Reads_the_name_of_the_sheet()
    {
        var segments = await OpenXmlFixture.ReadXlsxAsync(
            XlsxFixture.WithRows("Anagrafiche", ["Uno"]));

        Assert.Contains("Anagrafiche", OpenXmlFixture.TextOf(segments), StringComparison.Ordinal);
    }

    /// <summary>
    /// Proves that text stored once and referenced many times is resolved.
    /// </summary>
    /// <returns>A task that completes when the workbook has been read.</returns>
    /// <remarks>
    /// Most text in a workbook is not in the sheet that displays it: a cell holds an index
    /// into the shared string table. A reader that skips the table reads a spreadsheet of
    /// integers and reports it clean.
    /// </remarks>
    [Fact]
    public async Task Resolves_a_cell_that_points_into_the_shared_string_table()
    {
        var segments = await OpenXmlFixture.ReadXlsxAsync(
            XlsxFixture.WithSharedString(["Primo", "Mario Rossi"], index: 1));

        Assert.Contains("Mario Rossi", OpenXmlFixture.TextOf(segments), StringComparison.Ordinal);
    }

    /// <summary>
    /// Proves that a formula is read as content.
    /// </summary>
    /// <returns>A task that completes when the workbook has been read.</returns>
    /// <remarks>
    /// A formula is content, not machinery. A name assembled from string literals appears
    /// nowhere else in the file, and a reader that takes only the cached result misses it.
    /// </remarks>
    [Fact]
    public async Task Reads_the_text_of_a_formula()
    {
        var segments = await OpenXmlFixture.ReadXlsxAsync(
            XlsxFixture.WithFormula("\"Mario \"&\"Rossi\"", cached: "0"));

        Assert.Contains("Mario ", OpenXmlFixture.TextOf(segments), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reads_a_name_an_author_gave_to_a_range()
    {
        var segments = await OpenXmlFixture.ReadXlsxAsync(
            XlsxFixture.WithDefinedName("Clienti_riservati", "Foglio!$A$1:$B$2"));

        Assert.Contains(
            "Clienti_riservati",
            OpenXmlFixture.TextOf(segments),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reads_the_package_properties()
    {
        var segments = await OpenXmlFixture.ReadXlsxAsync(
            XlsxFixture.WithProperties("Listino", "Giulia Bianchi"));

        var metadata = segments
            .Where(segment => segment.Origin.Kind == SegmentOriginKind.Metadata)
            .Select(segment => segment.Text);

        Assert.Contains("Listino", metadata);
        Assert.Contains("Giulia Bianchi", metadata);
    }

    [Fact]
    public async Task Attributes_every_segment_to_the_part_it_came_from()
    {
        var segments = await OpenXmlFixture.ReadXlsxAsync(XlsxFixture.WithRows("Foglio", ["Uno"]));

        Assert.All(segments, segment => Assert.Equal(PartId.Create("p1"), segment.PartId));
    }

    [Fact]
    public void Reports_its_own_identity() =>
        Assert.Equal(ExtractorId.Create("xlsx"), new XlsxExtractor().Version.ExtractorId);
}
