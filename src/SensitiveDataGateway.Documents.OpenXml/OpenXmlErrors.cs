namespace SensitiveDataGateway.Documents.OpenXml;

/// <summary>
/// The content-free errors the Open XML extractors raise.
/// </summary>
/// <remarks>
/// Every message here is a fixed sentence. The only values interpolated are ordinals and
/// configured bounds, which come from the caller rather than from the document, so no error
/// this type builds can repeat what the document said.
/// </remarks>
internal static class OpenXmlErrors
{
    /// <summary>
    /// Builds the error for a file the parser could not read.
    /// </summary>
    /// <param name="partId">The part that could not be read.</param>
    /// <returns>The error.</returns>
    public static GatewayError Malformed(PartId partId) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.Malformed,
            "The part is not an Open XML document that can be read without guessing.")
            .InPart(partId);

    /// <summary>
    /// Builds the error for a part supplied as text.
    /// </summary>
    /// <param name="partId">The part.</param>
    /// <returns>The error.</returns>
    public static GatewayError NotBinary(PartId partId) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.Malformed,
            "The part was supplied as text, and an Open XML document is a binary package.")
            .InPart(partId);

    /// <summary>
    /// Builds the error for a document the extractor cannot open.
    /// </summary>
    /// <param name="partId">The part.</param>
    /// <returns>The error.</returns>
    /// <remarks>
    /// An encrypted Open XML file is not a damaged package: it is a compound file holding
    /// the encrypted package, which is recognized by its own signature before the package
    /// reader is handed anything.
    /// </remarks>
    public static GatewayError Encrypted(PartId partId) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.PasswordProtected,
            "The document is encrypted, and this extractor is never given a password.")
            .InPart(partId);

    /// <summary>
    /// Builds the error for a package with no main part.
    /// </summary>
    /// <param name="partId">The part.</param>
    /// <returns>The error.</returns>
    public static GatewayError Headless(PartId partId) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.Malformed,
            "The package holds no main document part, so there is nothing to read.")
            .InPart(partId);

    /// <summary>
    /// Builds the error for a document that embeds content in another format.
    /// </summary>
    /// <param name="partId">The part.</param>
    /// <returns>The error.</returns>
    /// <remarks>
    /// An <c>altChunk</c> is a chunk of HTML, RTF or another document format that a word
    /// processor imports when it opens the file. Its text is part of what a reader sees and
    /// none of it is in the Word markup, so a document holding one is a document this
    /// extractor would scan in part.
    /// </remarks>
    public static GatewayError EmbeddedChunk(PartId partId) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.UnreadableConstruct,
            "The document embeds a chunk in another format, whose text is not in the markup.")
            .InPart(partId);

    /// <summary>
    /// Builds the error for a document assembled from other files.
    /// </summary>
    /// <param name="partId">The part.</param>
    /// <returns>The error.</returns>
    public static GatewayError ExternalContent(PartId partId) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.UnreadableConstruct,
            "The document draws content from another file, which is never opened.")
            .InPart(partId);

    /// <summary>
    /// Builds the error for a document with more segments than the options allow.
    /// </summary>
    /// <param name="partId">The part.</param>
    /// <param name="maxSegmentCount">The bound that was exceeded.</param>
    /// <returns>The error.</returns>
    public static GatewayError TooManySegments(PartId partId, int maxSegmentCount) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.LimitExceeded,
            $"The document yields more than {maxSegmentCount} segments, which is the configured bound.")
            .InPart(partId);

    /// <summary>
    /// Builds the error for a document larger than the options allow.
    /// </summary>
    /// <param name="partId">The part.</param>
    /// <param name="maxBytes">The bound that was exceeded.</param>
    /// <returns>The error.</returns>
    public static GatewayError TooLarge(PartId partId, long maxBytes) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.LimitExceeded,
            $"The document holds more than {maxBytes} bytes, which is the configured bound.")
            .InPart(partId);
}
