using System.Globalization;

namespace SensitiveDataGateway.Documents;

/// <summary>
/// Derives segment identifiers from the part they belong to.
/// </summary>
/// <remarks>
/// Identifiers are derived rather than generated because the pipeline is required to be
/// reproducible: the same bundle, scanned twice, must produce the same identifiers, and a
/// freshly generated one would make every audit record disagree with the last.
/// </remarks>
internal static class SegmentIds
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
        Derive(partId, string.Concat("p", ordinal.ToString(CultureInfo.InvariantCulture)));

    /// <summary>
    /// Derives the identifier of a segment carrying one piece of a part's metadata.
    /// </summary>
    /// <param name="partId">The part the metadata belongs to.</param>
    /// <param name="name">The name of the metadata field.</param>
    /// <returns>The identifier.</returns>
    /// <exception cref="ExtractionException">
    /// The part identifier and the field name are together too long to derive a distinct
    /// segment identifier from.
    /// </exception>
    public static SegmentId Metadata(PartId partId, string name) =>
        Derive(partId, string.Concat("m", Separator, name));

    private static SegmentId Derive(PartId partId, string suffix)
    {
        var value = string.Concat(partId.Value, Separator, suffix);

        return value.Length <= SegmentId.MaxLength
            ? SegmentId.Create(value)
            : throw new ExtractionException(ExtractionErrors.PartIdentifierTooLong(partId));
    }
}
