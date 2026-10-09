namespace SensitiveDataGateway.Policies;

/// <summary>
/// Why a policy refuses to let a document through.
/// </summary>
/// <remarks>
/// Every reason describes an expected outcome rather than a fault. A blocked operation is a
/// result a caller reads, not an exception a caller catches, which is what keeps the safe
/// path from depending on a <see langword="catch"/> block.
/// </remarks>
public enum PolicyViolationReason
{
    /// <summary>
    /// The action the policy resolved for a candidate is
    /// <see cref="Anonymization.TransformationAction.Block"/>.
    /// </summary>
    BlockedEntity = 1,

    /// <summary>
    /// The action the policy resolved for a candidate is
    /// <see cref="Anonymization.TransformationAction.RequireReview"/>, so the operation
    /// cannot complete without a person inside the trust boundary.
    /// </summary>
    ReviewRequired = 2,

    /// <summary>
    /// A candidate was not resolved and its sensitivity is higher than the policy tolerates.
    /// </summary>
    /// <remarks>
    /// An unresolved candidate keeps its text, so tolerating one is a decision to transmit
    /// it. The policy states how much of that it accepts, and everything above that line
    /// blocks.
    /// </remarks>
    UnresolvedEntity = 3,

    /// <summary>
    /// Part of the bundle was not read or not scanned.
    /// </summary>
    /// <remarks>
    /// An incomplete scan is an unknown, and an unknown blocks. A document that was not fully
    /// examined has not been shown to be safe; it has only failed to be shown unsafe. No
    /// policy can accept one: the final privacy validation refuses it regardless.
    /// </remarks>
    IncompleteScan = 4,
}
