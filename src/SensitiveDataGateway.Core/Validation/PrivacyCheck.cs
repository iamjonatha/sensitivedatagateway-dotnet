namespace SensitiveDataGateway.Validation;

/// <summary>
/// A check the final privacy validator performs before anything leaves the trust boundary.
/// </summary>
/// <remarks>
/// Every check must pass. There is no partial pass, no severity and no override: a check is
/// either satisfied or the payload does not leave.
/// </remarks>
public enum PrivacyCheck
{
    /// <summary>
    /// Not a check. The default exists so that an uninitialized value is never mistaken for a
    /// check that passed.
    /// </summary>
    None = 0,

    /// <summary>
    /// The sanitization status is <see cref="Anonymization.SanitizationStatus.Valid"/>.
    /// </summary>
    SanitizationStatus = 1,

    /// <summary>
    /// Every extractor and every detector completed. A failure means part of the document was
    /// never examined, and what was never examined is unknown.
    /// </summary>
    ScanCompleteness = 2,

    /// <summary>
    /// No unresolved candidate above the sensitivity the policy tolerates remains.
    /// </summary>
    UnresolvedEntities = 3,

    /// <summary>
    /// No policy violation is outstanding.
    /// </summary>
    PolicyViolations = 4,

    /// <summary>
    /// Every planned transformation was applied, over the span it was planned for.
    /// </summary>
    TransformationCoverage = 5,

    /// <summary>
    /// No value a detector found in the original appears in the sanitized output.
    /// </summary>
    AntiLeakScan = 6,

    /// <summary>
    /// No marker in the output carries the value it stands for. A marker body is syntactically
    /// free text, so a token that names its own subject is a valid marker and a leak.
    /// </summary>
    TokenResolution = 7,

    /// <summary>
    /// The output carries exactly as many markers as there are applied transformations that
    /// produce one. A surplus marker was forged by the source document; a missing one means a
    /// transformation left no trace.
    /// </summary>
    MarkerIntegrity = 8,

    /// <summary>
    /// The policy identifier, the policy version and the detector versions are present, and
    /// name the policy the plan was evaluated against.
    /// </summary>
    ProvenanceCompleteness = 9,

    /// <summary>
    /// The payload is well formed and within the limits the policy sets.
    /// </summary>
    PayloadStructure = 10,
}
