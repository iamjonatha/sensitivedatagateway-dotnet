using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway;

/// <summary>
/// Everything one pass through the pipeline produced.
/// </summary>
/// <remarks>
/// It is internal because it holds every stage's output at once, including the text. The
/// public surface hands back one of these projections at a time, never the set.
/// </remarks>
internal sealed class PipelineRun
{
    internal required ExtractedDocument Extracted { get; init; }

    internal required DetectedDocument Detected { get; init; }

    internal required TransformationPlan Plan { get; init; }

    internal required SanitizationProvenance Provenance { get; init; }

    /// <summary>
    /// Gets the sanitized document, or <see langword="null"/> when the plan blocked and the
    /// pipeline stopped before anonymization.
    /// </summary>
    internal required SanitizedDocument? Sanitized { get; init; }

    internal required SanitizationResult Result { get; init; }
}
