using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Policies;

namespace SensitiveDataGateway.Validation;

/// <summary>
/// The last barrier, and the only producer of a <see cref="ValidatedPayload"/>.
/// </summary>
/// <remarks>
/// <para>
/// The validator assumes every stage before it may have failed. That is why it is given what
/// those stages produced rather than their conclusions alone: it has to be able to contradict
/// them, and a check that trusts the stage it is checking is decoration.
/// </para>
/// <para>
/// It never repairs. A document that fails a check is refused, not corrected, because a
/// correction applied here would be a transformation nobody planned and nobody audited.
/// </para>
/// </remarks>
public interface IPrivacyValidator
{
    /// <summary>
    /// Validates a sanitized document against everything the pipeline claimed about it.
    /// </summary>
    /// <param name="detected">The document as detection left it, including the original text.</param>
    /// <param name="plan">The plan the policy engine produced.</param>
    /// <param name="sanitized">The document as anonymization left it.</param>
    /// <param name="policy">The policy the plan was built from.</param>
    /// <returns>
    /// A payload when every check passed, and the reasons there is none when any check failed.
    /// </returns>
    SanitizationResult Validate(
        DetectedDocument detected,
        TransformationPlan plan,
        SanitizedDocument sanitized,
        SanitizationPolicy policy);
}
