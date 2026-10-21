using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SensitiveDataGateway.Detection.Semantic;

/// <summary>
/// Finds the entities a pattern cannot describe: a person's name, an organization, a place,
/// a sentence that is a medical fact about someone.
/// </summary>
/// <remarks>
/// <para>
/// This is not an <see cref="ISensitiveDataDetector"/>, and deliberately so. A deterministic
/// detector is a function of its input; a semantic detector has a model behind it, a latency,
/// a failure mode and a place where it runs. Those four things need somewhere to live, and
/// hiding them inside the deterministic contract would make the detection stage look uniform
/// when it is not.
/// </para>
/// <para>
/// An implementation reaches the pipeline through <c>SemanticDetectorHost</c>, which applies
/// the bounds, enforces the timeout and turns a failure into a gap rather than a clean scan.
/// Nothing else puts a semantic detector into the detection stage.
/// </para>
/// <para>
/// A semantic detector <b>must not</b> return matched text, in an entity, in evidence, in a
/// log or in an exception. <see cref="DetectionEvidence.FromModel"/> exists so a model's score
/// can be recorded without quoting what it scored.
/// </para>
/// <para>
/// A semantic detector <b>must not</b> decide anything. It reports spans, types and
/// confidences; masking, blocking and thresholds belong to the policy. A detector that was
/// successfully talked out of reporting an entity has produced an incomplete scan, and an
/// incomplete scan blocks.
/// </para>
/// </remarks>
public interface ISemanticDetector
{
    /// <summary>
    /// Gets the identity and version of this detector, for the audit record.
    /// </summary>
    DetectorVersion Version { get; }

    /// <summary>
    /// Gets where the model runs, relative to the trust boundary.
    /// </summary>
    /// <remarks>
    /// Declared by the implementation and taken at its word. The host refuses a detector
    /// outside the boundary unless the application asked for that in a call a reviewer can
    /// see in a diff.
    /// </remarks>
    TrustBoundaryLocality Locality { get; }

    /// <summary>
    /// Examines a document.
    /// </summary>
    /// <param name="context">The instructions, the entity types and the text.</param>
    /// <param name="cancellationToken">A token that cancels the examination.</param>
    /// <returns>
    /// The entities found. An empty list is a statement that the detector examined the
    /// document and found nothing; a detector that could not examine it throws instead.
    /// </returns>
    /// <exception cref="DetectionException">
    /// The examination did not complete. The host converts this into a
    /// <see cref="DetectionFailure"/>, which is recorded as a gap rather than as a clean
    /// result.
    /// </exception>
    ValueTask<IReadOnlyList<SensitiveEntity>> ExamineAsync(
        SemanticDetectionContext context,
        CancellationToken cancellationToken);
}
