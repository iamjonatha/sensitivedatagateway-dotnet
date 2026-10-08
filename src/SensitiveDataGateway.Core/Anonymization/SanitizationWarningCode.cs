namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// What a sanitization warning is about.
/// </summary>
/// <remarks>
/// A warning never stops an operation on its own; it is a fact the audit record keeps so that
/// a decision can be explained afterwards. The set is closed because a warning with free text
/// beside a document that was just sanitized is where a fragment of that document would end
/// up.
/// </remarks>
public enum SanitizationWarningCode
{
    /// <summary>
    /// A value was transformed on evidence weaker than the policy's threshold for certainty.
    /// </summary>
    LowConfidence = 1,

    /// <summary>
    /// Two detectors claimed overlapping text and the stronger claim was applied.
    /// </summary>
    OverlappingEntities = 2,

    /// <summary>
    /// A candidate was neither transformed nor cleared, and the policy allowed the document
    /// through anyway.
    /// </summary>
    UnresolvedEntity = 3,

    /// <summary>
    /// A planned transformation was not applied.
    /// </summary>
    TransformationSkipped = 4,

    /// <summary>
    /// Part of the bundle could not be read, so it was never scanned.
    /// </summary>
    ExtractionIncomplete = 5,

    /// <summary>
    /// A detector did not finish, so part of the document was never scanned.
    /// </summary>
    DetectionIncomplete = 6,

    /// <summary>
    /// A pseudonym was reused from a scope wider than the document it appears in.
    /// </summary>
    /// <remarks>
    /// A wider scope makes tokens correlatable across documents, which is sometimes the
    /// point and always worth recording.
    /// </remarks>
    PseudonymScopeWidened = 7,

    /// <summary>
    /// The source document already contained the gateway's marker syntax, so the markers it
    /// contained were escaped.
    /// </summary>
    /// <remarks>
    /// This is an attempt — deliberate or accidental — to make document content look like
    /// gateway output. It is escaped rather than blocked, and recorded here because a
    /// document that forges tokens is worth noticing even when the escape worked.
    /// </remarks>
    MarkerSyntaxEscaped = 8,
}
