using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Policies;

/// <summary>
/// Decides what happens to each candidate a detection pass reported.
/// </summary>
/// <remarks>
/// <para>
/// This is the only stage of the pipeline that makes a judgement. Detectors report, the
/// anonymizer applies, the validator checks; between them there is exactly one place where
/// the question "and what should we do about it?" is answered, and this is it.
/// </para>
/// <para>
/// Evaluation is pure: it reads a document and a policy, it allocates a plan and it reaches
/// nothing else. It performs no input or output, so it is not asynchronous and takes no
/// cancellation token.
/// </para>
/// </remarks>
public interface IPolicyEngine
{
    /// <summary>
    /// Decides what happens to every candidate in a detected document.
    /// </summary>
    /// <param name="document">The extracted text together with what the detectors found.</param>
    /// <param name="policy">The rules to decide under.</param>
    /// <returns>The plan, blocked or not.</returns>
    /// <remarks>
    /// An implementation never rewrites text, never re-classifies a candidate and never
    /// downgrades a block into a warning.
    /// </remarks>
    TransformationPlan Evaluate(DetectedDocument document, SanitizationPolicy policy);
}
