using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace SensitiveDataGateway.Documents.OpenXml;

/// <summary>
/// Flattens Open XML markup into the text a reader of the document would see.
/// </summary>
internal static class OpenXmlText
{
    /// <summary>
    /// The separator between the cells of a row.
    /// </summary>
    /// <remarks>
    /// A single space, because that is what separates two cells for someone reading the row,
    /// and reading order is what the detectors are given everywhere else. Joining with
    /// nothing would run two cells together and hide the boundary a pattern needs; joining
    /// with a newline would split a value the author spread across two cells.
    /// </remarks>
    private const string CellSeparator = " ";

    /// <summary>
    /// The separator between the rows of a table.
    /// </summary>
    private const string RowSeparator = "\n";

    /// <summary>
    /// Returns the text of an element, keeping the cells of a table apart.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>The text.</returns>
    /// <remarks>
    /// <c>InnerText</c> already covers what a reader does not see and this library must: the
    /// text of a tracked deletion, which is a value the author removed and the file still
    /// holds, and the instruction of a field, which is where the target of a hyperlink lives.
    /// What it does not do is separate the cells of a table, so a table is walked here.
    /// </remarks>
    public static string Flatten(OpenXmlElement element) => element is Table table
        ? string.Join(RowSeparator, table.Elements<TableRow>().Select(FlattenRow))
        : element.InnerText;

    /// <summary>
    /// Returns the text of a table row, with its cells kept apart.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The text.</returns>
    public static string FlattenRow(TableRow row) =>
        string.Join(CellSeparator, row.Elements<TableCell>().Select(Flatten));

    /// <summary>
    /// Returns the text of a sequence of values, with the cell separator between them.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>The text.</returns>
    public static string JoinCells(IEnumerable<string> values) =>
        string.Join(CellSeparator, values);
}
