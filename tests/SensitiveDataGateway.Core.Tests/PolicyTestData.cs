using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Core.Tests;

internal static class PolicyTestData
{
    internal static readonly SegmentId Segment = SegmentId.Create("segment-1");

    internal static readonly DetectorId Detector = DetectorId.Create("test-detector");

    internal static SensitiveEntity Entity(EntityType type) =>
        Entity(type, DetectionConfidence.Certain, SensitivityLevel.High);

    internal static SensitiveEntity Entity(EntityType type, SensitivityLevel sensitivity) =>
        Entity(type, DetectionConfidence.Certain, sensitivity);

    internal static SensitiveEntity Entity(
        EntityType type,
        DetectionConfidence confidence,
        SensitivityLevel sensitivity) =>
        SensitiveEntity.Create(
            EntityId.New(),
            type,
            SourceSpan.Create(Segment, 0, 4),
            confidence,
            Detector,
            sensitivity,
            DetectionEvidence.FromPattern("test", checksumValidated: false, 4));
}
