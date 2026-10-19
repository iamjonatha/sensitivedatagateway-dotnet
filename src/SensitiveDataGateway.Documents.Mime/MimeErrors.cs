namespace SensitiveDataGateway.Documents.Mime;

/// <summary>
/// The errors this package raises, and the codes automation matches on.
/// </summary>
/// <remarks>
/// Every description is a fixed sentence. None of them quotes a header, an address, a file
/// name or a media type taken from the message, which is what makes a MIME reading failure
/// safe to log in full even though the message that caused it is not.
/// </remarks>
internal static class MimeErrors
{
    /// <summary>
    /// Builds the error for a message the parser could not read.
    /// </summary>
    /// <returns>The error.</returns>
    public static GatewayError Malformed() =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.Malformed,
            "The message is not a well-formed MIME message.");

    /// <summary>
    /// Builds the error for a part whose bytes are not the encoding it declares.
    /// </summary>
    /// <param name="partId">The part that could not be decoded.</param>
    /// <returns>The error.</returns>
    public static GatewayError Undecodable(PartId partId) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.Malformed,
            "The part is not valid UTF-8, and an encoding is never guessed on the caller's behalf.")
            .InPart(partId);

    /// <summary>
    /// Builds the error for a composite the parser handed back without taking it apart.
    /// </summary>
    /// <returns>The error.</returns>
    /// <remarks>
    /// The reader refuses rather than carrying the raw bytes forward. Carrying them forward
    /// would put unscanned parts behind one part that no extractor reads, and the reason
    /// recorded would be the media type rather than the truncation that caused it.
    /// </remarks>
    public static GatewayError Truncated() =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.Malformed,
            "The message holds a composite part the parser could not take apart.");

    /// <summary>
    /// Builds the error for a message nested deeper than the reader descends.
    /// </summary>
    /// <param name="maxNestingDepth">The bound that was exceeded.</param>
    /// <returns>The error.</returns>
    public static GatewayError NestedTooDeeply(int maxNestingDepth) =>
        GatewayError.Create(
            GatewayErrorCategory.LimitExceeded,
            ExtractionErrorCodes.LimitExceeded,
            $"The message nests deeper than {maxNestingDepth} levels, which is the configured bound.");

    /// <summary>
    /// Builds the error for a message with more parts than the policy allows.
    /// </summary>
    /// <param name="maxPartCount">The bound that was exceeded.</param>
    /// <returns>The error.</returns>
    public static GatewayError TooManyParts(int maxPartCount) =>
        GatewayError.Create(
            GatewayErrorCategory.LimitExceeded,
            ExtractionErrorCodes.LimitExceeded,
            $"The message yields more than {maxPartCount} parts, which is the configured bound.");

    /// <summary>
    /// Builds the error for a part larger than the policy allows.
    /// </summary>
    /// <param name="maxPartBytes">The bound that was exceeded.</param>
    /// <returns>The error.</returns>
    public static GatewayError PartTooLarge(long maxPartBytes) =>
        GatewayError.Create(
            GatewayErrorCategory.LimitExceeded,
            ExtractionErrorCodes.LimitExceeded,
            $"A part of the message holds more than {maxPartBytes} bytes, which is the configured bound.");

    /// <summary>
    /// Builds the error for a message larger than the policy allows.
    /// </summary>
    /// <param name="maxBundleBytes">The bound that was exceeded.</param>
    /// <returns>The error.</returns>
    public static GatewayError MessageTooLarge(long maxBundleBytes) =>
        GatewayError.Create(
            GatewayErrorCategory.LimitExceeded,
            ExtractionErrorCodes.LimitExceeded,
            $"The message holds more than {maxBundleBytes} bytes, which is the configured bound.");
}
