using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Policies;

namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// Applies a plan to a document's text.
/// </summary>
/// <remarks>
/// <para>
/// The anonymizer is the only stage that rewrites text, and it rewrites exactly what the plan
/// tells it to. It does not re-classify, it does not decide, and it does not reconsider a
/// decision it finds surprising.
/// </para>
/// <para>
/// The pseudonym mapping is deliberately absent from this contract. An implementation that
/// allocates pseudonyms deposits them where the application can reach them and the output
/// cannot, so there is no return value, property or output parameter through which a
/// sanitized document could carry the way back to the original.
/// </para>
/// </remarks>
public interface IDocumentAnonymizer
{
    /// <summary>
    /// Produces the sanitized form of a document.
    /// </summary>
    /// <param name="document">The document the detectors reported on.</param>
    /// <param name="plan">What the policy decided for each candidate.</param>
    /// <param name="provenance">How the result came to look the way it does.</param>
    /// <returns>
    /// The sanitized document. Its status is
    /// <see cref="SanitizationStatus.Incomplete"/> when a planned transformation could not be
    /// applied: a partly sanitized document is an unknown document, and the next stage
    /// refuses it.
    /// </returns>
    SanitizedDocument Anonymize(
        DetectedDocument document,
        TransformationPlan plan,
        SanitizationProvenance provenance);
}
