namespace SensitiveDataGateway.Auditing;

/// <summary>
/// How an operation ended.
/// </summary>
/// <remarks>
/// A record is written for every outcome, including the ones nobody asked for. An operation
/// that blocked, threw or was cancelled is exactly the one an investigation later needs, and a
/// gap in the record is indistinguishable from an operation that never happened.
/// </remarks>
public enum AuditOutcome
{
    /// <summary>
    /// The outcome is not known. No record the gateway writes carries this value.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Validation passed and the payload was allowed to leave.
    /// </summary>
    Allowed = 1,

    /// <summary>
    /// Validation failed and nothing left the process.
    /// </summary>
    Blocked = 2,

    /// <summary>
    /// The operation raised and did not reach a decision.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// The operation was cancelled or ran out of its time budget.
    /// </summary>
    Cancelled = 4,
}
