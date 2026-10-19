namespace SensitiveDataGateway.Documents;

/// <summary>
/// The codes an extractor reports a failure with, and the host maps onto an
/// <see cref="ExtractionFailureReason"/>.
/// </summary>
/// <remarks>
/// <para>
/// The mapping goes through the code rather than through an exception type so that an
/// extractor shipped in another package — or written by a consumer — can report a precise
/// reason without the host knowing the format. That makes these constants part of the
/// extension contract, which is why they live in the abstractions package.
/// </para>
/// <para>
/// A code names a class of failure, never an instance of one. None of them is assembled from
/// a document, a file name or a media type, which is what makes an extraction failure safe to
/// log in full.
/// </para>
/// </remarks>
public static class ExtractionErrorCodes
{
    /// <summary>
    /// The code reported when no registered extractor accepts a part.
    /// </summary>
    public const string UnsupportedMediaType = "extraction.unsupported_media_type";

    /// <summary>
    /// The code reported when a part cannot be decoded or parsed.
    /// </summary>
    public const string Malformed = "extraction.malformed";

    /// <summary>
    /// The code reported when a part is encrypted or password protected.
    /// </summary>
    public const string PasswordProtected = "extraction.password_protected";

    /// <summary>
    /// The code reported when an extractor runs out of time.
    /// </summary>
    public const string TimedOut = "extraction.timed_out";

    /// <summary>
    /// The code reported when a part cannot be read within the configured bounds.
    /// </summary>
    public const string LimitExceeded = "extraction.limit_exceeded";

    /// <summary>
    /// The code reported when a part is well formed, but carries content in a construct the
    /// extractor cannot read in place.
    /// </summary>
    /// <remarks>
    /// This is the fail-closed outcome for a file that is perfectly valid and still holds
    /// something the extractor is not able to account for: a file embedded inside a document,
    /// a form whose values live in a format the extractor does not read, a chunk in a foreign
    /// format, a link to a workbook that is not present, a page that is an image of text. The
    /// extraction is not degraded and the part is not approximated — reporting a construct
    /// that was not read is the only way the pipeline can refuse rather than transmit a
    /// document it scanned incompletely.
    /// </remarks>
    public const string UnreadableConstruct = "extraction.unreadable_construct";
}
