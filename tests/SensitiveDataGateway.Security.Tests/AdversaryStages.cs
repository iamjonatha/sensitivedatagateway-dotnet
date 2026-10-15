using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;

namespace SensitiveDataGateway.Security.Tests;

/// <summary>
/// A detector that cannot scan.
/// </summary>
/// <remarks>
/// It throws something the pipeline does not know, rather than a
/// <see cref="DetectionException"/>, because a stage that only fails in the way the pipeline
/// anticipated proves less than one that fails in a way it did not.
/// </remarks>
internal sealed class ThrowingDetector : ISensitiveDataDetector
{
    public DetectorVersion Version { get; } =
        DetectorVersion.Create(DetectorId.Create("throwing"), "1.0.0");

    public ValueTask<IReadOnlyList<SensitiveEntity>> DetectAsync(
        ExtractedDocument document,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The scan could not run.");
}

/// <summary>
/// An anonymizer that rewrites nothing and claims it rewrote everything.
/// </summary>
/// <remarks>
/// This is the stage the validator exists for. It returns the original text with a status of
/// <see cref="SanitizationStatus.Valid"/>, which is the most dishonest thing a stage in this
/// position can do: every value the policy decided to remove is still there, and the record
/// says otherwise.
/// </remarks>
internal sealed class PassThroughAnonymizer : IDocumentAnonymizer
{
    public SanitizedDocument Anonymize(
        DetectedDocument document,
        TransformationPlan plan,
        SanitizationProvenance provenance)
    {
        var segments = new List<SanitizedSegment>();

        foreach (var segment in document.Extracted.Segments)
        {
            segments.Add(SanitizedSegment.Create(
                segment.Id,
                segment.PartId,
                segment.Text,
                segment.Origin,
                segment.Order));
        }

        return SanitizedDocument.Create(
            document.BundleId,
            segments,
            SanitizationStatus.Valid,
            [],
            [],
            provenance);
    }
}
