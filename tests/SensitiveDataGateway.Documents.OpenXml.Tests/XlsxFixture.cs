using System;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace SensitiveDataGateway.Documents.OpenXml.Tests;

/// <summary>
/// Builds the Excel workbooks the tests read.
/// </summary>
internal static class XlsxFixture
{
    /// <summary>
    /// Builds a workbook with one sheet whose cells hold their text inline.
    /// </summary>
    /// <param name="sheetName">The name of the sheet.</param>
    /// <param name="rows">The cells, row by row.</param>
    /// <returns>The bytes of the workbook.</returns>
    public static byte[] WithRows(string sheetName, params string[][] rows) =>
        Build(sheetName, data => Fill(data, rows), (_, _) => { });

    /// <summary>
    /// Builds a workbook whose single cell points into the shared string table.
    /// </summary>
    /// <param name="shared">The strings of the table.</param>
    /// <param name="index">The entry the cell points at.</param>
    /// <returns>The bytes of the workbook.</returns>
    public static byte[] WithSharedString(string[] shared, int index) =>
        Build(
            "Foglio",
            data => data.AppendChild(
                new Row(
                    new Cell
                    {
                        DataType = CellValues.SharedString,
                        CellValue = new CellValue(index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    })),
            (workbook, _) => workbook.AddNewPart<SharedStringTablePart>("rIdShared").SharedStringTable =
                new SharedStringTable(shared.Select(ItemOf).Cast<OpenXmlElement>()));

    /// <summary>
    /// Builds a workbook whose single cell is produced by a formula.
    /// </summary>
    /// <param name="formula">The formula.</param>
    /// <param name="cached">The value the spreadsheet last stored for it.</param>
    /// <returns>The bytes of the workbook.</returns>
    public static byte[] WithFormula(string formula, string cached) =>
        Build(
            "Foglio",
            data => data.AppendChild(
                new Row(
                    new Cell
                    {
                        CellFormula = new CellFormula(formula),
                        CellValue = new CellValue(cached),
                    })),
            (_, _) => { });

    /// <summary>
    /// Builds a workbook with a name an author gave to a range.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="reference">The range the name stands for.</param>
    /// <returns>The bytes of the workbook.</returns>
    public static byte[] WithDefinedName(string name, string reference) =>
        Build(
            "Foglio",
            _ => { },
            (_, book) => book.AppendChild(
                new DefinedNames(new DefinedName(reference) { Name = name })));

    /// <summary>
    /// Builds a workbook whose values are drawn from a different file.
    /// </summary>
    /// <returns>The bytes of the workbook.</returns>
    public static byte[] WithExternalWorkbook() =>
        Build(
            "Foglio",
            _ => { },
            (workbook, _) =>
            {
                var external = workbook.AddNewPart<ExternalWorkbookPart>("rIdExternal");

                external.ExternalLink = new ExternalLink(
                    new ExternalBook { Id = "rIdBook" });

                external.AddExternalRelationship(
                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships/externalLinkPath",
                    new Uri("https://example.invalid/prices.xlsx"),
                    "rIdBook");
            });

    /// <summary>
    /// Builds a workbook with entries in its package properties.
    /// </summary>
    /// <param name="title">The title.</param>
    /// <param name="creator">The creator.</param>
    /// <returns>The bytes of the workbook.</returns>
    public static byte[] WithProperties(string title, string creator)
    {
        using var buffer = new MemoryStream();

        using (var document = SpreadsheetDocument.Create(buffer, SpreadsheetDocumentType.Workbook))
        {
            Assemble(document, "Foglio", _ => { }, (_, _) => { });
            document.PackageProperties.Title = title;
            document.PackageProperties.Creator = creator;
        }

        return buffer.ToArray();
    }

    private static SharedStringItem ItemOf(string text) => new(new Text(text));

    private static void Fill(SheetData data, string[][] rows)
    {
        foreach (var cells in rows)
        {
            data.AppendChild(new Row(cells.Select(InlineCell).Cast<OpenXmlElement>()));
        }
    }

    private static Cell InlineCell(string text) => new()
    {
        DataType = CellValues.InlineString,
        InlineString = new InlineString(new Text(text)),
    };

    private static byte[] Build(
        string sheetName,
        Action<SheetData> fill,
        Action<WorkbookPart, Workbook> describe)
    {
        using var buffer = new MemoryStream();

        using (var document = SpreadsheetDocument.Create(buffer, SpreadsheetDocumentType.Workbook))
        {
            Assemble(document, sheetName, fill, describe);
        }

        return buffer.ToArray();
    }

    private static void Assemble(
        SpreadsheetDocument document,
        string sheetName,
        Action<SheetData> fill,
        Action<WorkbookPart, Workbook> describe)
    {
        var workbook = document.AddWorkbookPart();
        var data = new SheetData();
        var book = new Workbook();

        workbook.Workbook = book;

        var worksheet = workbook.AddNewPart<WorksheetPart>("rIdSheet");
        worksheet.Worksheet = new Worksheet(data);

        book.AppendChild(
            new Sheets(new Sheet { Id = "rIdSheet", SheetId = 1, Name = sheetName }));

        fill(data);
        describe(workbook, book);
    }
}
