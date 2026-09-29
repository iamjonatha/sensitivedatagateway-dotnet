namespace SensitiveDataGateway.Detection;

/// <summary>
/// Why a detector did not finish.
/// </summary>
/// <remarks>
/// Every value here means the same thing to the policy: part of the document was not scanned.
/// An incomplete scan is an unknown, and the safe reading of an unknown is that it blocks.
/// </remarks>
public enum DetectionFailureReason
{
    /// <summary>
    /// The detector threw for a reason it did not classify.
    /// </summary>
    DetectorFailed = 1,

    /// <summary>
    /// The detector did not finish in the time the limits allow.
    /// </summary>
    TimedOut = 2,

    /// <summary>
    /// The document has more entities or more text than the limits allow.
    /// </summary>
    LimitExceeded = 3,

    /// <summary>
    /// A model the detector depends on could not be loaded.
    /// </summary>
    /// <remarks>
    /// A detector that cannot load its model is missing, not permissive. It never degrades
    /// into a detector that finds nothing.
    /// </remarks>
    ModelUnavailable = 4,

    /// <summary>
    /// The operation was cancelled before the detector finished.
    /// </summary>
    Cancelled = 5,
}
