namespace SensitiveDataGateway.Documents;

/// <summary>
/// The errors the extraction stage raises, and the codes automation matches on.
/// </summary>
/// <remarks>
/// Every description here is a fixed sentence. None of them is assembled from a document, a
/// file name or a media type read out of a part, which is what makes an extraction error safe
/// to log in full and safe to return to a caller outside the trust boundary.
/// </remarks>
internal static class ExtractionErrors
{
    /// <summary>
    /// The code raised when no registered extractor accepts a part.
    /// </summary>
    public const string UnsupportedMediaTypeCode = "extraction.unsupported_media_type";

    /// <summary>
    /// The code raised when a part cannot be decoded or parsed.
    /// </summary>
    public const string MalformedCode = "extraction.malformed";

    /// <summary>
    /// The code raised when a part is encrypted or password protected.
    /// </summary>
    public const string PasswordProtectedCode = "extraction.password_protected";

    /// <summary>
    /// The code raised when an extractor runs out of time.
    /// </summary>
    public const string TimedOutCode = "extraction.timed_out";

    /// <summary>
    /// The code raised when a part cannot be read within the configured bounds.
    /// </summary>
    public const string LimitExceededCode = "extraction.limit_exceeded";

    /// <summary>
    /// Builds the error for a part whose bytes are not the encoding it declares.
    /// </summary>
    /// <param name="partId">The part that could not be decoded.</param>
    /// <returns>The error.</returns>
    public static GatewayError Undecodable(PartId partId) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            MalformedCode,
            "The part is not valid UTF-8, and an encoding is never guessed on the caller's behalf.")
            .InPart(partId);

    /// <summary>
    /// Builds the error for a bundle with more parts than the policy allows.
    /// </summary>
    /// <param name="maxPartCount">The bound that was exceeded.</param>
    /// <returns>The error.</returns>
    public static GatewayError TooManyParts(int maxPartCount) =>
        GatewayError.Create(
            GatewayErrorCategory.LimitExceeded,
            LimitExceededCode,
            $"The bundle has more than {maxPartCount} parts, which is the configured bound.");

    /// <summary>
    /// Builds the error for a bundle larger than the policy allows.
    /// </summary>
    /// <param name="maxBundleBytes">The bound that was exceeded.</param>
    /// <returns>The error.</returns>
    public static GatewayError BundleTooLarge(long maxBundleBytes) =>
        GatewayError.Create(
            GatewayErrorCategory.LimitExceeded,
            LimitExceededCode,
            $"The bundle declares more than {maxBundleBytes} bytes, which is the configured bound.");

    /// <summary>
    /// Builds the error for a bundle that yielded more text than the policy allows.
    /// </summary>
    /// <param name="maxExtractedCharacters">The bound that was exceeded.</param>
    /// <returns>The error.</returns>
    /// <remarks>
    /// Extraction stops at the bound instead of carrying on with what fits, because the part
    /// that did not fit is the part nothing would have scanned.
    /// </remarks>
    public static GatewayError TooMuchText(int maxExtractedCharacters) =>
        GatewayError.Create(
            GatewayErrorCategory.LimitExceeded,
            LimitExceededCode,
            $"Extraction produced more than {maxExtractedCharacters} characters, which is the configured bound.");

    /// <summary>
    /// Builds the error for a part whose identifier is too long to derive segment identifiers from.
    /// </summary>
    /// <param name="partId">The part with the oversized identifier.</param>
    /// <returns>The error.</returns>
    public static GatewayError PartIdentifierTooLong(PartId partId) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            MalformedCode,
            "The part identifier is too long to derive a distinct segment identifier from.")
            .InPart(partId);

    /// <summary>
    /// Builds the error for a bundle that yielded no text at all.
    /// </summary>
    /// <param name="bundleId">The bundle that produced nothing.</param>
    /// <returns>The error.</returns>
    /// <remarks>
    /// A bundle with no segments is not a clean bundle. It is one nothing was read from, and
    /// scanning nothing always passes.
    /// </remarks>
    public static GatewayError NothingExtracted(BundleId bundleId) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            MalformedCode,
            "Extraction produced no text at all, which cannot be told apart from a clean document.")
            .InBundle(bundleId);

    /// <summary>
    /// Maps an error raised by an extractor onto the reason recorded against its part.
    /// </summary>
    /// <param name="error">The error the extractor raised.</param>
    /// <returns>The reason.</returns>
    /// <remarks>
    /// The mapping goes through the code rather than the exception type so that an extractor
    /// in another package can report a precise reason without the host knowing the format.
    /// </remarks>
    public static ExtractionFailureReason ReasonFor(GatewayError error) => error.Code switch
    {
        UnsupportedMediaTypeCode => ExtractionFailureReason.UnsupportedMediaType,
        MalformedCode => ExtractionFailureReason.Malformed,
        PasswordProtectedCode => ExtractionFailureReason.PasswordProtected,
        TimedOutCode => ExtractionFailureReason.TimedOut,
        LimitExceededCode => ExtractionFailureReason.LimitExceeded,
        _ => ExtractionFailureReason.ExtractorFailed,
    };
}
