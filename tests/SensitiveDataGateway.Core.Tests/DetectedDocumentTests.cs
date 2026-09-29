using System;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Core.Tests;

public sealed class DetectedDocumentTests
{
    private static readonly SegmentId s_segment = SegmentId.Create("s_segment-1");
    private static readonly DetectorId s_detector = DetectorId.Create("iban-s_detector");

    [Fact]
    public void Create_KeepsAnEntityThatFitsInsideItsSegment()
    {
        var extracted = Extracted("0123456789");

        var document = DetectedDocument.Create(
            extracted,
            [Entity(SourceSpan.Create(s_segment, 2, 4))],
            [DetectorVersion.Create(s_detector, "1.0.0")],
            []);

        Assert.Single(document.Entities);
        Assert.True(document.IsComplete);
    }

    [Fact]
    public void Create_RejectsAnEntityThatPointsPastTheEndOfItsSegment()
    {
        var extracted = Extracted("0123456789");

        Assert.Throws<ArgumentException>(() => DetectedDocument.Create(
            extracted,
            [Entity(SourceSpan.Create(s_segment, 8, 5))],
            [],
            []));
    }

    [Fact]
    public void Create_RejectsAnEntityInASegmentThatWasNotExtracted()
    {
        var extracted = Extracted("0123456789");
        var elsewhere = SourceSpan.Create(SegmentId.Create("s_segment-2"), 0, 1);

        Assert.Throws<ArgumentException>(() => DetectedDocument.Create(extracted, [Entity(elsewhere)], [], []));
    }

    [Fact]
    public void Create_RejectsTwoEntitiesThatShareAnIdentifier()
    {
        var extracted = Extracted("0123456789");
        var entityId = EntityId.New();
        var span = SourceSpan.Create(s_segment, 0, 1);

        Assert.Throws<ArgumentException>(() => DetectedDocument.Create(
            extracted,
            [Entity(span, entityId), Entity(span, entityId)],
            [],
            []));
    }

    [Fact]
    public void IsComplete_IsFalseWhenADetectorDidNotFinish()
    {
        var document = DetectedDocument.Create(
            Extracted("abc"),
            [],
            [],
            [DetectionFailure.Create(s_detector, DetectionFailureReason.TimedOut)]);

        Assert.False(document.IsComplete);
    }

    [Fact]
    public void IsComplete_IsFalseWhenAPartCouldNotBeRead()
    {
        var extracted = ExtractedDocument.Create(
            BundleId.New(),
            [],
            [ExtractionFailure.Create(PartId.Create("part-1"), ExtractionFailureReason.PasswordProtected, "application/pdf")],
            []);

        var document = DetectedDocument.Create(extracted, [], [], []);

        Assert.False(document.IsComplete);
    }

    [Fact]
    public void ToString_CountsTheFindingsAndQuotesNothing()
    {
        var document = DetectedDocument.Create(Extracted("abc"), [], [], []);

        Assert.EndsWith("(0 entities, 0 failures)", document.ToString(), StringComparison.Ordinal);
    }

    private static ExtractedDocument Extracted(string text) =>
        ExtractedDocument.Create(
            BundleId.New(),
            [TextSegment.Create(s_segment, PartId.Create("part-1"), text, SegmentOrigin.Unspecified, 0)],
            [],
            []);

    private static SensitiveEntity Entity(SourceSpan span) => Entity(span, EntityId.New());

    private static SensitiveEntity Entity(SourceSpan span, EntityId entityId) =>
        SensitiveEntity.Create(
            entityId,
            EntityType.Iban,
            span,
            DetectionConfidence.Certain,
            s_detector,
            SensitivityLevel.High,
            DetectionEvidence.FromPattern("iban", checksumValidated: true, span.Length));
}

public sealed class DetectionConfidenceTests
{
    [Fact]
    public void None_IsTheDefaultValue() => Assert.Equal(default, DetectionConfidence.None);

    [Fact]
    public void Certain_IsOne() => Assert.Equal(1.0, DetectionConfidence.Certain.Value);

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Create_AcceptsAScoreInRange(double value) =>
        Assert.Equal(value, DetectionConfidence.Create(value).Value);

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Create_RejectsAScoreOutOfRangeInsteadOfClampingIt(double value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => DetectionConfidence.Create(value));

    [Fact]
    public void Comparison_OrdersByScore()
    {
        var low = DetectionConfidence.Create(0.4);
        var high = DetectionConfidence.Create(0.9);

        Assert.True(low < high);
        Assert.True(high > low);
        Assert.True(low <= DetectionConfidence.Create(0.4));
        Assert.True(high >= low);
        Assert.True(low.CompareTo(high) < 0);
    }

    [Fact]
    public void ToString_UsesTheInvariantCulture() =>
        Assert.Equal("0.85", DetectionConfidence.Create(0.85).ToString());
}
