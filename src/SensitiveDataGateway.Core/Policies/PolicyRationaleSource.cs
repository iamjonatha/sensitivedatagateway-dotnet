namespace SensitiveDataGateway.Policies;

/// <summary>
/// Where the action in a decision came from.
/// </summary>
/// <remarks>
/// The three sources are not interchangeable in review. A decision that came from a rule was
/// argued for; one that came from the unknown-entity action was reached because the policy
/// had nothing to say, and a policy that reaches that source often is a policy that is out of
/// date with its detectors.
/// </remarks>
public enum PolicyRationaleSource
{
    /// <summary>
    /// A rule in the policy matched the candidate.
    /// </summary>
    Rule = 1,

    /// <summary>
    /// The policy mentions the entity type but no rule matched, so the default action
    /// applied.
    /// </summary>
    DefaultAction = 2,

    /// <summary>
    /// The policy does not mention the entity type at all, so the unknown-entity action
    /// applied.
    /// </summary>
    UnknownEntityAction = 3,
}
