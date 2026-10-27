namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// What the policy decided to do with a detected value.
/// </summary>
/// <remarks>
/// The actions differ in two ways that matter more than how the output looks: whether the
/// original can be recovered, and whether two occurrences of the same value can still be
/// recognized as the same. A policy that does not think about both is choosing one of them by
/// accident.
/// </remarks>
public enum TransformationAction
{
    /// <summary>
    /// Leave the value as it is.
    /// </summary>
    /// <remarks>
    /// This is an explicit, auditable decision, not the absence of one. The audit record
    /// shows which rule chose to let a value through, which is the difference between an
    /// accepted risk and an oversight.
    /// </remarks>
    Preserve = 1,

    /// <summary>
    /// Keep the shape of the value and remove its content, as in <c>IT60****3456</c>.
    /// </summary>
    /// <remarks>
    /// Masking keeps a value recognizable to a human reader, which is useful and dangerous
    /// in equal measure: a partially masked short value is frequently still the value.
    /// </remarks>
    Mask = 2,

    /// <summary>
    /// Replace the value with a marker that carries no identity, as in <c>[REDACTED]</c>.
    /// </summary>
    Redact = 3,

    /// <summary>
    /// Replace the value with a stable token, as in <c>[PERSON_001]</c>.
    /// </summary>
    /// <remarks>
    /// A pseudonym preserves the fact that two mentions are the same person, which is
    /// frequently what makes a document still worth analyzing. It is reversible through the
    /// pseudonym map, which never leaves the trust boundary.
    /// </remarks>
    Pseudonymize = 4,

    /// <summary>
    /// Replace the value with a keyed digest.
    /// </summary>
    /// <remarks>
    /// A digest correlates across operations and is not reversible. The key is what stops it
    /// from being reversible by anyone who can guess the input, which for values like a
    /// national identity number is everyone.
    /// </remarks>
    Hash = 5,

    /// <summary>
    /// Refuse the whole operation.
    /// </summary>
    /// <remarks>
    /// Blocking is a decision about the document, not about the value: there is no sanitized
    /// version of a document that contains this.
    /// </remarks>
    Block = 6,

    /// <summary>
    /// Suspend the operation and hand it to a human inside the trust boundary.
    /// </summary>
    RequireReview = 7,
}
