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
}
