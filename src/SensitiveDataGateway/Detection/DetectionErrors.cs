namespace SensitiveDataGateway.Detection;

/// <summary>
/// The errors the detection stage raises, and the codes automation matches on.
/// </summary>
/// <remarks>
/// Every description here is a fixed sentence. None of them is assembled from a document, a
/// file name or a match, which is what makes a detection error safe to log in full and safe
/// to return to a caller outside the trust boundary.
/// </remarks>
internal static class DetectionErrors
{
    /// <summary>
    /// The code of the error a detector raises when it runs out of time.
    /// </summary>
    public const string TimedOutCode = "detection.timed_out";

    /// <summary>
    /// The code of the error a detector raises when it would exceed a configured bound.
    /// </summary>
    public const string LimitExceededCode = "detection.limit_exceeded";

    /// <summary>
    /// Builds the error for a detector that ran out of time.
    /// </summary>
    /// <param name="detectorId">The detector that did not finish.</param>
    /// <returns>The error.</returns>
    public static GatewayError TimedOut(DetectorId detectorId) =>
        GatewayError.Create(
            GatewayErrorCategory.DetectionFailed,
            TimedOutCode,
            $"The detector '{detectorId.Value}' exceeded its time budget, so part of the document was not scanned.");

    /// <summary>
    /// Builds the error for a scan that found more candidates than it is allowed to carry.
    /// </summary>
    /// <param name="maxEntities">The bound that was exceeded.</param>
    /// <returns>The error.</returns>
    public static GatewayError TooManyEntities(int maxEntities) =>
        GatewayError.Create(
            GatewayErrorCategory.DetectionFailed,
            LimitExceededCode,
            $"Detection reported more than {maxEntities} candidates, which is the configured bound.");

    /// <summary>
    /// Maps an error raised by a detector onto the reason recorded against it.
    /// </summary>
    /// <param name="error">The error the detector raised.</param>
    /// <returns>The reason.</returns>
    public static DetectionFailureReason ReasonFor(GatewayError error) => error.Code switch
    {
        TimedOutCode => DetectionFailureReason.TimedOut,
        LimitExceededCode => DetectionFailureReason.LimitExceeded,
        _ => DetectionFailureReason.DetectorFailed,
    };
}
