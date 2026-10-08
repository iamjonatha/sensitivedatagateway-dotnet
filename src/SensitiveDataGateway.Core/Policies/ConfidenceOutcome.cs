namespace SensitiveDataGateway.Policies;

/// <summary>
/// What a policy's confidence thresholds made of a detector's score.
/// </summary>
/// <remarks>
/// The three outcomes are the whole vocabulary of uncertainty in this library. A candidate is
/// acted on, acted on and recorded as weak, or not resolved at all; there is no fourth case
/// in which something uncertain is quietly treated as absent.
/// </remarks>
public enum ConfidenceOutcome
{
    /// <summary>
    /// The score reached the transformation threshold, so the decided action applies.
    /// </summary>
    Transform = 1,

    /// <summary>
    /// The score reached the flagging threshold but not the transformation threshold, so the
    /// decided action applies and the operation records a warning.
    /// </summary>
    Flag = 2,

    /// <summary>
    /// The score reached neither threshold, so the policy did not resolve the candidate.
    /// </summary>
    /// <remarks>
    /// An unresolved candidate is not a candidate that was dismissed. Its text is still in
    /// the document, which is why the policy states how much unresolved sensitivity it is
    /// willing to transmit.
    /// </remarks>
    Unresolved = 3,
}
