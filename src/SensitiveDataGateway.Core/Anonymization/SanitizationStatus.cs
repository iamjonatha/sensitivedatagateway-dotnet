namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// How completely a plan was applied to a document.
/// </summary>
/// <remarks>
/// Only <see cref="Valid"/> is a candidate for transmission. The other two exist so that a
/// partly rewritten document has a name, rather than being indistinguishable from a finished
/// one.
/// </remarks>
public enum SanitizationStatus
{
    /// <summary>
    /// Every planned transformation was applied.
    /// </summary>
    Valid = 1,

    /// <summary>
    /// The document was rewritten, and something the plan asked for did not happen.
    /// </summary>
    /// <remarks>
    /// An incomplete document still contains values the plan meant to remove. It is not
    /// transmitted, and it is not "mostly safe".
    /// </remarks>
    Incomplete = 2,

    /// <summary>
    /// The document could not be rewritten.
    /// </summary>
    Failed = 3,
}
