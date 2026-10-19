namespace SensitiveDataGateway.Documents;

/// <summary>
/// Why a part could not be turned into text.
/// </summary>
/// <remarks>
/// The reason is a closed set on purpose. A failure record carries no free text, because free
/// text written next to a document that could not be read is exactly where a file name or a
/// fragment of content ends up.
/// </remarks>
public enum ExtractionFailureReason
{
    /// <summary>
    /// No registered extractor handles the declared media type.
    /// </summary>
    UnsupportedMediaType = 1,

    /// <summary>
    /// An extractor accepted the part and could not parse it.
    /// </summary>
    Malformed = 2,

    /// <summary>
    /// The part is encrypted or password protected.
    /// </summary>
    /// <remarks>
    /// An unreadable attachment is not an empty one. The policy decides whether a document
    /// carrying something nobody can scan may be transmitted at all.
    /// </remarks>
    PasswordProtected = 3,

    /// <summary>
    /// The part is larger than the limits allow, or it expands beyond them.
    /// </summary>
    LimitExceeded = 4,

    /// <summary>
    /// Extraction did not finish in the time the limits allow.
    /// </summary>
    TimedOut = 5,

    /// <summary>
    /// The extractor failed for a reason it did not classify.
    /// </summary>
    ExtractorFailed = 6,

    /// <summary>
    /// The part is well formed, and carries content in a construct the extractor cannot read
    /// in place.
    /// </summary>
    /// <remarks>
    /// A file embedded in a document, a form whose values live in a format the extractor does
    /// not read, a chunk in a foreign format, a link to a workbook that is not present, a page
    /// that is an image of text. The part parsed; part of what it carries was not read, and an
    /// extractor is not allowed to approximate the rest.
    /// </remarks>
    UnreadableConstruct = 7,
}
