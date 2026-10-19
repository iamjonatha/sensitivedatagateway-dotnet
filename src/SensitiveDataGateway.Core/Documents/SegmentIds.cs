using System.Globalization;

namespace SensitiveDataGateway.Documents;

/// <summary>
/// Derives segment identifiers from the part they belong to.
/// </summary>
/// <remarks>
/// <para>
/// Identifiers are derived rather than generated because the pipeline is required to be
/// reproducible: the same bundle, scanned twice, must produce the same identifiers, and a
/// freshly generated one would make every audit record disagree with the last.
/// </para>
/// <para>
/// Every factory here takes ordinals, never text read out of a document. A sheet name, a
/// header name or a bookmark title is content, and an identifier is the one string the
/// gateway lets out of the trust boundary unchanged. Deriving an identifier from content
/// would put content in a log line with an identifier's name on it.
/// </para>
/// <para>
/// The derivation lives in the abstractions package so that an extractor shipped in another
/// package produces identifiers the host would have produced itself.
/// </para>
/// </remarks>
public static class SegmentIds
{
    private const string Separator = ":";

    /// <summary>
    /// Derives the identifier of an extracted paragraph.
    /// </summary>
    /// <param name="partId">The part the paragraph came from.</param>
    /// <param name="ordinal">The one-based position of the paragraph within the part.</param>
    /// <returns>The identifier.</returns>
    /// <exception cref="ExtractionException">
    /// The part identifier is too long to derive a distinct segment identifier from.
    /// </exception>
    public static SegmentId Paragraph(PartId partId, int ordinal) =>
        Derive(partId, "p", ordinal);

    /// <summary>
    /// Derives the identifier of an extracted page.
    /// </summary>
    /// <param name="partId">The part the page came from.</param>
    /// <param name="ordinal">The one-based page number.</param>
    /// <returns>The identifier.</returns>
    /// <exception cref="ExtractionException">
    /// The part identifier is too long to derive a distinct segment identifier from.
    /// </exception>
    public static SegmentId Page(PartId partId, int ordinal) =>
        Derive(partId, "pg", ordinal);

    /// <summary>
    /// Derives the identifier of an extracted slide.
    /// </summary>
    /// <param name="partId">The part the slide came from.</param>
    /// <param name="ordinal">The one-based slide number.</param>
    /// <returns>The identifier.</returns>
    /// <exception cref="ExtractionException">
    /// The part identifier is too long to derive a distinct segment identifier from.
    /// </exception>
    public static SegmentId Slide(PartId partId, int ordinal) =>
        Derive(partId, "sl", ordinal);

    /// <summary>
    /// Derives the identifier of an extracted header.
    /// </summary>
    /// <param name="partId">The part the header came from.</param>
    /// <param name="ordinal">The one-based position of the header within the part.</param>
    /// <returns>The identifier.</returns>
    /// <exception cref="ExtractionException">
    /// The part identifier is too long to derive a distinct segment identifier from.
    /// </exception>
    /// <remarks>
    /// The position is used rather than the header name because a header name is as
    /// attacker-controlled as a header value.
    /// </remarks>
    public static SegmentId Header(PartId partId, int ordinal) =>
        Derive(partId, "h", ordinal);

    /// <summary>
    /// Derives the identifier of an extracted comment, note or tracked change.
    /// </summary>
    /// <param name="partId">The part the annotation came from.</param>
    /// <param name="ordinal">The one-based position of the annotation within the part.</param>
    /// <returns>The identifier.</returns>
    /// <exception cref="ExtractionException">
    /// The part identifier is too long to derive a distinct segment identifier from.
    /// </exception>
    public static SegmentId Annotation(PartId partId, int ordinal) =>
        Derive(partId, "an", ordinal);

    /// <summary>
    /// Derives the identifier of an extracted table or spreadsheet row.
    /// </summary>
    /// <param name="partId">The part the row came from.</param>
    /// <param name="sheetOrdinal">The one-based position of the sheet or table within the part.</param>
    /// <param name="rowOrdinal">The one-based position of the row within the sheet or table.</param>
    /// <returns>The identifier.</returns>
    /// <exception cref="ExtractionException">
    /// The part identifier is too long to derive a distinct segment identifier from.
    /// </exception>
    public static SegmentId Row(PartId partId, int sheetOrdinal, int rowOrdinal) =>
        Derive(partId, string.Concat("s", Format(sheetOrdinal), ".r"), rowOrdinal);

    /// <summary>
    /// Derives the identifier of an extracted table or spreadsheet cell.
    /// </summary>
    /// <param name="partId">The part the cell came from.</param>
    /// <param name="sheetOrdinal">The one-based position of the sheet or table within the part.</param>
    /// <param name="rowOrdinal">The one-based position of the row.</param>
    /// <param name="columnOrdinal">The one-based position of the column.</param>
    /// <returns>The identifier.</returns>
    /// <exception cref="ExtractionException">
    /// The part identifier is too long to derive a distinct segment identifier from.
    /// </exception>
    /// <remarks>
    /// The column is a number rather than a spreadsheet column letter, so that the
    /// identifier does not depend on the conventions of the application that wrote the file.
    /// </remarks>
    public static SegmentId Cell(PartId partId, int sheetOrdinal, int rowOrdinal, int columnOrdinal) =>
        Derive(
            partId,
            string.Concat("s", Format(sheetOrdinal), ".r", Format(rowOrdinal), ".c"),
            columnOrdinal);

    /// <summary>
    /// Derives the identifier of a segment carrying one piece of a part's metadata.
    /// </summary>
    /// <param name="partId">The part the metadata belongs to.</param>
    /// <param name="name">
    /// The name of the metadata field. It is a name the host or the application chose, never
    /// a string read out of the document.
    /// </param>
    /// <returns>The identifier.</returns>
    /// <exception cref="ExtractionException">
    /// The part identifier and the field name are together too long to derive a distinct
    /// segment identifier from.
    /// </exception>
    public static SegmentId Metadata(PartId partId, string name) =>
        Derive(partId, string.Concat("m", Separator, name));

    private static string Format(int ordinal) => ordinal.ToString(CultureInfo.InvariantCulture);

    private static SegmentId Derive(PartId partId, string prefix, int ordinal) =>
        Derive(partId, string.Concat(prefix, Format(ordinal)));

    private static SegmentId Derive(PartId partId, string suffix)
    {
        var value = string.Concat(partId.Value, Separator, suffix);

        return value.Length <= SegmentId.MaxLength
            ? SegmentId.Create(value)
            : throw new ExtractionException(PartIdentifierTooLong(partId));
    }

    private static GatewayError PartIdentifierTooLong(PartId partId) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.Malformed,
            "The part identifier is too long to derive a distinct segment identifier from.")
            .InPart(partId);
}
