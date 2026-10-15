namespace SensitiveDataGateway.Providers.DecisionKit;

/// <summary>
/// How the sanitized content is presented to DecisionKit.
/// </summary>
/// <remarks>
/// The choice is about shape, not about safety. Both forms carry exactly the same sanitized
/// text, because both are built from the same validated payload and neither can reach
/// anything else.
/// </remarks>
public enum DecisionKitPayloadFormat
{
    /// <summary>
    /// One block of text, the segments joined in reading order.
    /// </summary>
    /// <remarks>
    /// This is the right form when the decision is made from prose — a model reading a
    /// complaint, a rule set matching on wording.
    /// </remarks>
    Text = 0,

    /// <summary>
    /// One property per segment, keyed by the segment identifier.
    /// </summary>
    /// <remarks>
    /// This is the right form when the decision is made field by field, and when the result
    /// has to point at which part of the document it came from. The keys are only as
    /// readable as the part identifiers the application chose: a bundle whose parts are
    /// named <c>body</c> and <c>attachment-1</c> produces keys such as <c>body:p1</c>.
    /// </remarks>
    Segments = 1,
}
