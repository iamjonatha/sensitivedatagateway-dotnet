namespace SensitiveDataGateway.Documents;

/// <summary>
/// The structural element a text segment was extracted from.
/// </summary>
public enum SegmentOriginKind
{
    /// <summary>
    /// The extractor could not describe the structure, only that the text came from the part.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// A page of a paginated document.
    /// </summary>
    Page = 1,

    /// <summary>
    /// A paragraph of flowing text.
    /// </summary>
    Paragraph = 2,

    /// <summary>
    /// A cell of a spreadsheet or of a table.
    /// </summary>
    Cell = 3,

    /// <summary>
    /// A row of a table, taken as a whole.
    /// </summary>
    Row = 4,

    /// <summary>
    /// A slide of a presentation.
    /// </summary>
    Slide = 5,

    /// <summary>
    /// A named header of a message.
    /// </summary>
    Header = 6,

    /// <summary>
    /// A metadata value, such as a file name or a document title.
    /// </summary>
    /// <remarks>
    /// Metadata is extracted as segments precisely so that it is scanned like everything
    /// else, rather than travelling beside the document unexamined.
    /// </remarks>
    Metadata = 7,

    /// <summary>
    /// A comment, a note or a tracked change.
    /// </summary>
    /// <remarks>
    /// These are the parts of a document a human author forgets are there, which is what
    /// makes them worth naming separately.
    /// </remarks>
    Annotation = 8,
}
