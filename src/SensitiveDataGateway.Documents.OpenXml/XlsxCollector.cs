using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace SensitiveDataGateway.Documents.OpenXml;

/// <summary>
/// Walks an Excel workbook and records everything it holds as text.
/// </summary>
internal sealed class XlsxCollector(PartId partId, OpenXmlExtractorOptions options)
{
    private readonly OpenXmlSegments _segments = new(partId, options);

    private string[] _sharedStrings = [];
    private int _sheets;
    private int _annotations;

    /// <summary>
    /// Collects every segment of the workbook.
    /// </summary>
    /// <param name="document">The workbook.</param>
    /// <param name="cancellationToken">The token to cancel the walk with.</param>
    /// <returns>The segments.</returns>
    /// <exception cref="ExtractionException">
    /// The workbook has no main part, or draws values from another file.
    /// </exception>
    public IReadOnlyList<TextSegment> Collect(
        SpreadsheetDocument document,
        CancellationToken cancellationToken)
    {
        var workbook = document.WorkbookPart
            ?? throw new ExtractionException(OpenXmlErrors.Headless(partId));

        var book = workbook.Workbook
            ?? throw new ExtractionException(OpenXmlErrors.Headless(partId));

        Refuse(workbook);

        _sharedStrings = ReadSharedStrings(workbook);

        _segments.AddProperties(document);
        AddDefinedNames(book);
        AddSheets(workbook, book, cancellationToken);

        return _segments.ToList();
    }

    /// <summary>
    /// Refuses a workbook whose values are not all its own.
    /// </summary>
    /// <param name="workbook">The workbook part.</param>
    /// <exception cref="ExtractionException">The workbook links another workbook.</exception>
    /// <remarks>
    /// An external reference is a formula reading a cell of a different file. What the
    /// workbook shows for it is a cached value that may be stale, absent, or a reference the
    /// extractor would have to open a file to resolve. It opens nothing, so it refuses.
    /// </remarks>
    private void Refuse(WorkbookPart workbook)
    {
        if (workbook.ExternalWorkbookParts.Any())
        {
            throw new ExtractionException(OpenXmlErrors.ExternalContent(partId));
        }
    }

    /// <summary>
    /// Reads the shared string table a worksheet cell points into.
    /// </summary>
    /// <param name="workbook">The workbook part.</param>
    /// <returns>The strings, by index.</returns>
    /// <remarks>
    /// Most text in a workbook is not in the sheet that displays it: a cell holds an index
    /// into this table. A reader that skips the table reads a spreadsheet of integers.
    /// </remarks>
    private static string[] ReadSharedStrings(WorkbookPart workbook)
    {
        var table = workbook.SharedStringTablePart?.SharedStringTable;

        return table is null
            ? []
            : table.Elements<SharedStringItem>().Select(item => item.InnerText).ToArray();
    }

    /// <summary>
    /// Records the names an author gave to ranges of the workbook.
    /// </summary>
    /// <param name="book">The workbook.</param>
    private void AddDefinedNames(Workbook book)
    {
        foreach (var name in book.DefinedNames?.Elements<DefinedName>() ?? [])
        {
            _annotations++;

            _segments.Add(
                SegmentIds.Annotation(partId, _annotations),
                $"{name.Name?.Value} {name.InnerText}",
                SegmentOrigin.At(SegmentOriginKind.Annotation, _annotations));
        }
    }

    private void AddSheets(WorkbookPart workbook, Workbook book, CancellationToken cancellationToken)
    {
        foreach (var sheet in book.Sheets?.Elements<Sheet>() ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            _sheets++;

            _segments.Add(
                SegmentIds.Metadata(partId, $"sheet-{_sheets}"),
                sheet.Name?.Value,
                SegmentOrigin.Named(SegmentOriginKind.Metadata, $"sheet-{_sheets}"));

            if (sheet.Id?.Value is { } relationshipId
                && workbook.GetPartById(relationshipId) is WorksheetPart part)
            {
                AddRows(part, cancellationToken);
                AddComments(part);
            }
        }
    }

    /// <summary>
    /// Records a sheet, one segment per row.
    /// </summary>
    /// <param name="part">The worksheet part.</param>
    /// <param name="cancellationToken">The token to cancel the walk with.</param>
    /// <remarks>
    /// A row rather than a cell, for the reason a table row is one segment in a Word
    /// document: a value an author spread across adjacent columns is one value, and a
    /// detector handed the columns separately sees none of it.
    /// </remarks>
    private void AddRows(WorksheetPart part, CancellationToken cancellationToken)
    {
        var ordinal = 0;

        foreach (var row in part.Worksheet?.Descendants<Row>() ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            ordinal++;

            _segments.Add(
                SegmentIds.Row(partId, _sheets, ordinal),
                OpenXmlText.JoinCells(row.Elements<Cell>().Select(TextOf)),
                SegmentOrigin.At(SegmentOriginKind.Row, ordinal));
        }
    }

    /// <summary>
    /// Records every comment on a sheet.
    /// </summary>
    /// <param name="part">The worksheet part.</param>
    private void AddComments(WorksheetPart part)
    {
        var comments = part.WorksheetCommentsPart?.Comments;

        foreach (var text in comments?.CommentList?.Elements<Comment>().Select(c => c.InnerText) ?? [])
        {
            _annotations++;

            _segments.Add(
                SegmentIds.Annotation(partId, _annotations),
                text,
                SegmentOrigin.At(SegmentOriginKind.Annotation, _annotations));
        }
    }

    /// <summary>
    /// Returns the text of a cell, including the formula that produced it.
    /// </summary>
    /// <param name="cell">The cell.</param>
    /// <returns>The text.</returns>
    /// <remarks>
    /// The formula is content, not machinery. <c>="Mario "&amp;"Rossi"</c> holds a name that
    /// appears nowhere else in the file, and a reader that takes only the cached result of a
    /// formula is a reader that misses it.
    /// </remarks>
    private string TextOf(Cell cell)
    {
        var value = ValueOf(cell);
        var formula = cell.CellFormula?.Text;

        return string.IsNullOrEmpty(formula) ? value : $"{value} {formula}";
    }

    private string ValueOf(Cell cell)
    {
        if (cell.DataType?.Value == CellValues.SharedString)
        {
            return int.TryParse(cell.CellValue?.Text, out var index)
                && index >= 0
                && index < _sharedStrings.Length
                    ? _sharedStrings[index]
                    : string.Empty;
        }

        return cell.DataType?.Value == CellValues.InlineString
            ? cell.InlineString?.InnerText ?? string.Empty
            : cell.CellValue?.Text ?? string.Empty;
    }
}
