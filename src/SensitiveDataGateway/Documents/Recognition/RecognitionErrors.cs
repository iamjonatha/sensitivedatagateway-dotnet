using System.Globalization;

namespace SensitiveDataGateway.Documents.Recognition;

/// <summary>
/// The errors raised while turning a recognition result into segments.
/// </summary>
/// <remarks>
/// Every description is a fixed sentence. None of them quotes a line, a confidence read off a
/// document or a media type, so a recognition failure is safe to log in full.
/// </remarks>
internal static class RecognitionErrors
{
    /// <summary>
    /// Builds the error for a part a recognizer read nothing from.
    /// </summary>
    /// <param name="partId">The part.</param>
    /// <returns>The error.</returns>
    /// <remarks>
    /// An image with no readable text cannot be told apart from an image whose text the engine
    /// failed to find, and the second one is a scanned document that would pass as clean.
    /// </remarks>
    public static GatewayError NothingRead(PartId partId) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.UnreadableConstruct,
            "The recognizer read no text from the part, which cannot be told apart from text it failed to find.")
            .InPart(partId);

    /// <summary>
    /// Builds the error for a part the recognizer was not sure it read.
    /// </summary>
    /// <param name="partId">The part.</param>
    /// <param name="minimumConfidence">The threshold that was not reached.</param>
    /// <returns>The error.</returns>
    public static GatewayError TooUncertain(PartId partId, double minimumConfidence) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.UnreadableConstruct,
            string.Create(
                CultureInfo.InvariantCulture,
                $"A line fell below the recognition threshold of {minimumConfidence:F2}, so the part was not read whole."))
            .InPart(partId);

    /// <summary>
    /// Builds the error for a part the recognizer returned too many lines for.
    /// </summary>
    /// <param name="partId">The part.</param>
    /// <param name="maxLines">The bound that was exceeded.</param>
    /// <returns>The error.</returns>
    /// <remarks>
    /// The category is the stage that refused, and the code is what automation matches on. A
    /// bound crossed while a part is read is an extraction failure carrying the limit code,
    /// exactly as it is for every other adapter.
    /// </remarks>
    public static GatewayError TooManyLines(PartId partId, int maxLines) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.LimitExceeded,
            $"The recognizer returned more than {maxLines} lines, which is the configured bound.")
            .InPart(partId);

    /// <summary>
    /// Builds the error for a recognizer that returned nothing at all.
    /// </summary>
    /// <param name="partId">The part.</param>
    /// <returns>The error.</returns>
    public static GatewayError NoResult(PartId partId) =>
        GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            ExtractionErrorCodes.UnreadableConstruct,
            "The recognizer returned no result for the part.")
            .InPart(partId);
}
