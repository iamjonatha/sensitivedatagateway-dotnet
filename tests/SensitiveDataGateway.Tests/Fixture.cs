using System;
using System.Collections.Generic;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;

namespace SensitiveDataGateway.Tests;

/// <summary>
/// Builds the documents, plans and policies the anonymization tests work on.
/// </summary>
/// <remarks>
/// Every value in here is invented. None of it is a real name, address or account, and a
/// fixture that carried one would be a leak in the repository rather than a test.
/// </remarks>
internal static class Fixture
{
    public const string GatewayVersion = "0.0.0-test";

    public static readonly DetectorId Detector = DetectorId.Create("test-detector");

    public static readonly PolicyId PolicyName = PolicyId.Create("test-policy");

    public static readonly PolicyVersion Version = PolicyVersion.Create("1.0");

    public static readonly DateTimeOffset SanitizedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static SanitizationProvenance Provenance() =>
        SanitizationProvenance.Create(
            PolicyName,
            Version,
            [DetectorVersion.Create(Detector, "1.0.0")],
            [],
            GatewayVersion,
            SanitizedAt);

    public static SanitizationPolicy Policy(params PolicyRule[] rules)
    {
        var builder = SanitizationPolicy.CreateBuilder(PolicyName, Version);

        foreach (var rule in rules)
        {
            builder = builder.AddRule(rule);
        }

        return builder.Build();
    }

    public static DetectedDocument Document(BundleId bundle, params PartSpec[] parts)
    {
        var segments = new List<TextSegment>();
        var entities = new List<SensitiveEntity>();
        var order = 0;

        foreach (var part in parts)
        {
            var segment = SegmentId.Create($"segment-{order}");
            var partId = PartId.Create(part.Name);
            segments.Add(TextSegment.Create(
                segment,
                partId,
                part.Text,
                SegmentOrigin.At(SegmentOriginKind.Paragraph, order + 1),
                order));
            entities.AddRange(Entities(segment, part));
            order++;
        }

        return DetectedDocument.Create(
            ExtractedDocument.Create(bundle, segments, [], []),
            entities,
            [DetectorVersion.Create(Detector, "1.0.0")],
            []);
    }

    public static DetectedDocument Document(params PartSpec[] parts) => Document(BundleId.New(), parts);

    /// <summary>
    /// Builds an extracted document with one segment per text, in the order given.
    /// </summary>
    /// <param name="texts">The text of each segment.</param>
    /// <returns>The document a detector runs over.</returns>
    public static ExtractedDocument Extracted(params string[] texts)
    {
        var segments = new List<TextSegment>(texts.Length);

        for (var order = 0; order < texts.Length; order++)
        {
            segments.Add(TextSegment.Create(
                SegmentId.Create($"segment-{order}"),
                PartId.Create("body"),
                texts[order],
                SegmentOrigin.At(SegmentOriginKind.Paragraph, order + 1),
                order));
        }

        return ExtractedDocument.Create(BundleId.New(), segments, [], []);
    }

    public static PartSpec Part(string name, string text, params EntitySpec[] entities) =>
        new(name, text, entities);

    public static EntitySpec At(EntityType type, int start, int length) => new(type, start, length);

    private static IEnumerable<SensitiveEntity> Entities(SegmentId segment, PartSpec part)
    {
        foreach (var spec in part.Entities)
        {
            yield return SensitiveEntity.Create(
                EntityId.New(),
                spec.Type,
                SourceSpan.Create(segment, spec.Start, spec.Length),
                DetectionConfidence.Certain,
                Detector,
                SensitivityLevel.High,
                DetectionEvidence.FromPattern("test", checksumValidated: false, spec.Length));
        }
    }

    internal sealed record PartSpec(string Name, string Text, EntitySpec[] Entities);

    internal sealed record EntitySpec(EntityType Type, int Start, int Length);
}
