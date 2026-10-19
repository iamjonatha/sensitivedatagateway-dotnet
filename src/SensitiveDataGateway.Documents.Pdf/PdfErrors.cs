namespace SensitiveDataGateway.Documents.Pdf;

/// <summary>
/// The errors this package raises, and the codes automation matches on.
/// </summary>
/// <remarks>
/// Every description is a fixed sentence, and the only value any of them interpolates is an
/// ordinal or a configured bound. None of them quotes a title, an author, a file name or a
/// line of text taken from the document, which is what makes a PDF reading failure safe to
/// log in full even though the document that caused it is not.
/// </remarks>
internal static class PdfErrors
{
    /// <summary>
    /// Builds the error for a document the parser could not read.
    /// </summary>
    /// <param name="partId">The part that could not be parsed.</param>
    /// <returns>The error.</returns>
    /// <remarks>
    /// The extractor reads strictly, so a damaged cross-reference table is a failure rather
    /// than a recovery. A recovered document is one whose missing objects nobody counted, and
    /// an object that was not read is text that was not scanned.
    /// </remarks>
    public static GatewayError Malformed(PartId partId) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.Malformed,
            "The part is not a well-formed PDF document.")
            .InPart(partId);

    /// <summary>
    /// Builds the error for a part supplied as text rather than bytes.
    /// </summary>
    /// <param name="partId">The part.</param>
    /// <returns>The error.</returns>
    /// <remarks>
    /// A PDF is binary. A part that arrived as a string has already been through a decoder,
    /// and re-encoding it would hand the parser bytes that are not the ones the document was
    /// written with.
    /// </remarks>
    public static GatewayError NotBinary(PartId partId) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.Malformed,
            "The part was supplied as text, and a PDF document is binary.")
            .InPart(partId);

    /// <summary>
    /// Builds the error for a document that cannot be opened without a password.
    /// </summary>
    /// <param name="partId">The part that could not be opened.</param>
    /// <returns>The error.</returns>
    public static GatewayError Encrypted(PartId partId) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.PasswordProtected,
            "The document is encrypted, and no password is ever supplied on the caller's behalf.")
            .InPart(partId);

    /// <summary>
    /// Builds the error for a document carrying a file inside it.
    /// </summary>
    /// <param name="partId">The part that carries the embedded file.</param>
    /// <returns>The error.</returns>
    /// <remarks>
    /// An embedded file is a whole document of its own, in a format this extractor may not
    /// read, and the bundle model has no room to add a part once extraction has started. The
    /// document is refused rather than read without it.
    /// </remarks>
    public static GatewayError EmbeddedFile(PartId partId) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.UnreadableConstruct,
            "The document carries an embedded file, whose content cannot be read in place.")
            .InPart(partId);

    /// <summary>
    /// Builds the error for a form whose values live in an XML stream.
    /// </summary>
    /// <param name="partId">The part that carries the form.</param>
    /// <returns>The error.</returns>
    /// <remarks>
    /// An XFA form keeps the values a person typed in an XML document beside the page, and
    /// the fields drawn on the page may be empty while the form is full. Reading that XML
    /// would mean parsing attacker-supplied XML, which this package does not do.
    /// </remarks>
    public static GatewayError XfaForm(PartId partId) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.UnreadableConstruct,
            "The document carries an XFA form, whose values are not read from the page.")
            .InPart(partId);

    /// <summary>
    /// Builds the error for a page that draws images and yields no text.
    /// </summary>
    /// <param name="partId">The part the page belongs to.</param>
    /// <param name="pageNumber">The one-based page number.</param>
    /// <returns>The error.</returns>
    /// <remarks>
    /// This is a scanned page. The page number is an ordinal, not content, so it is safe to
    /// state and it is the one thing an operator needs in order to act.
    /// </remarks>
    public static GatewayError ImageOnlyPage(PartId partId, int pageNumber) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.UnreadableConstruct,
            $"Page {pageNumber} draws images and yields no text, so its content was not read.")
            .InPart(partId);

    /// <summary>
    /// Builds the error for a document with more pages than the options allow.
    /// </summary>
    /// <param name="partId">The part that is too long.</param>
    /// <param name="maxPageCount">The bound that was exceeded.</param>
    /// <returns>The error.</returns>
    /// <remarks>
    /// The category is <see cref="GatewayErrorCategory.ExtractionFailed"/> rather than
    /// <see cref="GatewayErrorCategory.LimitExceeded"/> because an extractor reports through
    /// <see cref="ExtractionException"/>, which is the only failure the pipeline turns into a
    /// failed part rather than a failed operation. The code is what carries the reason.
    /// </remarks>
    public static GatewayError TooManyPages(PartId partId, int maxPageCount) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.LimitExceeded,
            $"The document holds more than {maxPageCount} pages, which is the configured bound.")
            .InPart(partId);

    /// <summary>
    /// Builds the error for a document larger than the options allow.
    /// </summary>
    /// <param name="partId">The part that is too large.</param>
    /// <param name="maxBytes">The bound that was exceeded.</param>
    /// <returns>The error.</returns>
    public static GatewayError TooLarge(PartId partId, long maxBytes) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.LimitExceeded,
            $"The document holds more than {maxBytes} bytes, which is the configured bound.")
            .InPart(partId);
}
