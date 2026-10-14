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

    /// <summary>
    /// Refuses a document the policy engine already blocked, without anonymizing it.
    /// </summary>
    /// <param name="detected">The document as detection left it.</param>
    /// <param name="plan">The blocking plan the policy engine produced.</param>
    /// <param name="policy">The policy the plan was built from.</param>
    /// <returns>A refusal carrying the checks that could run and the reasons they failed.</returns>
    /// <remarks>
    /// A blocked plan is never applied, so there is no sanitized document to validate. The
    /// refusal still goes through the validator because it is the only type allowed to decide
    /// that nothing leaves, and the only one that can prove it never built a payload.
    /// </remarks>
    /// <exception cref="System.ArgumentException"><paramref name="plan"/> does not block.</exception>
    SanitizationResult Reject(DetectedDocument detected, TransformationPlan plan, SanitizationPolicy policy);
}
