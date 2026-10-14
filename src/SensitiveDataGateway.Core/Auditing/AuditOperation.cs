namespace SensitiveDataGateway.Auditing;

/// <summary>
/// The operation an audit record describes.
/// </summary>
public enum AuditOperation
{
    /// <summary>
    /// The operation is not known. No record the gateway writes carries this value.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// A document was sanitized and the result was handed to a decision provider.
    /// </summary>
    Execute = 1,

    /// <summary>
    /// A document was sanitized and the result was returned to the caller.
    /// </summary>
    Sanitize = 2,

    /// <summary>
    /// A document was examined and nothing left the process.
    /// </summary>
    Inspect = 3,
}
