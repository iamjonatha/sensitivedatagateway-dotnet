namespace SensitiveDataGateway;

/// <summary>
/// The kind of problem a <see cref="GatewayError"/> describes.
/// </summary>
/// <remarks>
/// The category keeps every distinction the caller might act on, so that the exception
/// hierarchy above it can stay one level deep. Code that needs to tell two failures apart
/// switches on the category; it does not test for a type.
/// </remarks>
public enum GatewayErrorCategory
{
    /// <summary>
    /// A part of the bundle could not be read.
    /// </summary>
    ExtractionFailed = 1,

    /// <summary>
    /// A detector did not complete, so the scan is incomplete.
    /// </summary>
    /// <remarks>
    /// An incomplete scan is an unknown, and an unknown blocks. It is never treated as
    /// "nothing was found".
    /// </remarks>
    DetectionFailed = 2,

    /// <summary>
    /// The policy forbids transmitting this document.
    /// </summary>
    /// <remarks>
    /// This category describes an expected outcome, not a fault. It is reported as a blocked
    /// result and has no exception type of its own: a caller must not need a
    /// <see langword="catch"/> block to reach the safe path.
    /// </remarks>
    PolicyViolation = 3,

    /// <summary>
    /// A planned transformation could not be applied, leaving the document partly sanitized.
    /// </summary>
    SanitizationFailed = 4,

    /// <summary>
    /// The final privacy validator rejected the payload.
    /// </summary>
    ValidationFailed = 5,

    /// <summary>
    /// Transmission was refused.
    /// </summary>
    EgressBlocked = 6,

    /// <summary>
    /// A size, count or time limit was reached.
    /// </summary>
    LimitExceeded = 7,

    /// <summary>
    /// The downstream decision provider failed.
    /// </summary>
    /// <remarks>
    /// This is the only category that describes something outside the trust boundary, and it
    /// is reported without flattening the provider's own failure into it.
    /// </remarks>
    ProviderFailed = 8,

    /// <summary>
    /// The policy, the limits or the options are not usable.
    /// </summary>
    /// <remarks>
    /// An invalid configuration fails at construction. It never degrades into a permissive
    /// default discovered at evaluation time.
    /// </remarks>
    ConfigurationInvalid = 9,
}
