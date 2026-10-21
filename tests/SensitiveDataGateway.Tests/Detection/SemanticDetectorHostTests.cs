using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Detection.Semantic;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Tests.Detection;

public sealed class SemanticDetectorHostTests
{
    private static readonly DetectorVersion s_version = DetectorVersion.Create(DetectorId.Create("semantic-model"), "1.0.0");

    [Fact]
    public void Create_RefusesADetectorThatRunsOutsideTheTrustBoundary()
    {
        var detector = new StubDetector(TrustBoundaryLocality.Remote);

        var error = Assert.Throws<ArgumentException>(() => SemanticDetectorHost.Create(detector));

        Assert.Contains("CreateOutsideTheTrustBoundary", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(TrustBoundaryLocality.InProcess)]
    [InlineData(TrustBoundaryLocality.SelfHosted)]
    public void Create_AcceptsADetectorThatRunsInside(TrustBoundaryLocality locality)
    {
        var host = SemanticDetectorHost.Create(new StubDetector(locality));

        Assert.Equal(locality, host.Locality);
        Assert.Equal($"semantic/{locality.Describe()}/semantic-model", host.Version.DetectorId.Value);
        Assert.Equal(s_version.Version, host.Version.Version);
    }

    [Fact]
    public void Create_RecordsThatTheModelRanOutsideTheTrustBoundary()
    {
        var host = SemanticDetectorHost.CreateOutsideTheTrustBoundary(new StubDetector(TrustBoundaryLocality.Remote));

        Assert.Contains("remote", host.Version.DetectorId.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateOutsideTheTrustBoundary_AcceptsTheDetectorCreateRefuses()
    {
        var host = SemanticDetectorHost.CreateOutsideTheTrustBoundary(new StubDetector(TrustBoundaryLocality.Remote));

        Assert.Equal(TrustBoundaryLocality.Remote, host.Locality);
    }

    [Fact]
    public void Create_RefusesADetectorWithoutAVersion()
    {
        var detector = new StubDetector(TrustBoundaryLocality.InProcess) { Version = default };

        Assert.Throws<ArgumentException>(() => SemanticDetectorHost.Create(detector));
    }

    [Fact]
    public void Create_RefusesAnEmptyListOfEntityTypes()
    {
        Assert.Throws<ArgumentException>(
            () => SemanticDetectorHost.Create(new StubDetector(TrustBoundaryLocality.InProcess), []));
    }

    [Fact]
    public void DefaultEntityTypes_AreTheOnesAPatternCannotDescribe()
    {
        Assert.Contains(EntityType.Person, SemanticDetectorHost.DefaultEntityTypes);
        Assert.Contains(EntityType.Address, SemanticDetectorHost.DefaultEntityTypes);
        Assert.DoesNotContain(EntityType.Iban, SemanticDetectorHost.DefaultEntityTypes);
    }

    [Fact]
    public async Task DetectAsync_AsksTheDetectorAboutTheTypesTheHostWasGiven()
    {
        var detector = new StubDetector(TrustBoundaryLocality.InProcess);
        var host = SemanticDetectorHost.Create(detector, [EntityType.Person]);

        await host.DetectAsync(Extracted("Nothing to find."), TestContext.Current.CancellationToken);

        Assert.NotNull(detector.LastContext);
        Assert.Equal([EntityType.Person], detector.LastContext!.EntityTypes);
    }

    [Fact]
    public async Task DetectAsync_DoesNotAskAboutADocumentWithNoText()
    {
        var detector = new StubDetector(TrustBoundaryLocality.InProcess);
        var host = SemanticDetectorHost.Create(detector);

        var entities = await host.DetectAsync(
            ExtractedDocument.Create(BundleId.New(), [], [], []),
            TestContext.Current.CancellationToken);

        Assert.Empty(entities);
        Assert.Null(detector.LastContext);
    }

    [Fact]
    public async Task DetectAsync_KeepsAFindingThatPointsAtTextTheDocumentHas()
    {
        var document = Extracted("Giulia Ferri lives here.");
        var detector = Reporting(document, 0, 0, 12, EntityType.Person);
        var host = SemanticDetectorHost.Create(detector);

        var entities = await host.DetectAsync(document, TestContext.Current.CancellationToken);

        Assert.Single(entities);
    }

    [Fact]
    public async Task DetectAsync_RefusesAFindingThatPointsPastTheEndOfItsSegment()
    {
        var document = Extracted("short");
        var host = SemanticDetectorHost.Create(Reporting(document, 0, 3, 40, EntityType.Person));

        await AssertMalformed(host, document);
    }

    [Fact]
    public async Task DetectAsync_RefusesAFindingInASegmentTheDocumentDoesNotHave()
    {
        var document = Extracted("a paragraph");
        var detector = new StubDetector(TrustBoundaryLocality.InProcess)
        {
            Findings = context =>
            [
                Entity(SourceSpan.Create(SegmentId.Create("somewhere-else"), 0, 1), EntityType.Person, context.DetectorId),
            ],
        };

        await AssertMalformed(SemanticDetectorHost.Create(detector), document);
    }

    [Fact]
    public async Task DetectAsync_RefusesAFindingOfATypeTheDetectorWasNotAskedAbout()
    {
        var document = Extracted("IT60X0542811101000000123456");
        var host = SemanticDetectorHost.Create(Reporting(document, 0, 0, 27, EntityType.Iban), [EntityType.Person]);

        await AssertMalformed(host, document);
    }

    [Fact]
    public async Task DetectAsync_RefusesAFindingAttributedToAnotherDetector()
    {
        var document = Extracted("a paragraph");
        var detector = new StubDetector(TrustBoundaryLocality.InProcess)
        {
            Findings = context =>
            [
                Entity(
                    SourceSpan.Create(context.Segments[0].Id, 0, 1),
                    EntityType.Person,
                    DetectorId.Create("someone-else")),
            ],
        };

        await AssertMalformed(SemanticDetectorHost.Create(detector), document);
    }

    [Fact]
    public async Task DetectAsync_RefusesADetectorThatReportsNothingAtAll()
    {
        var document = Extracted("a paragraph");
        var detector = new StubDetector(TrustBoundaryLocality.InProcess) { Findings = _ => null! };

        await AssertMalformed(SemanticDetectorHost.Create(detector), document);
    }

    private static async Task AssertMalformed(SemanticDetectorHost host, ExtractedDocument document)
    {
        var error = await Assert.ThrowsAsync<DetectionException>(
            async () => await host.DetectAsync(document, TestContext.Current.CancellationToken));

        Assert.Equal("detection.malformed_finding", error.Error.Code);
        Assert.DoesNotContain(document.Segments[0].Text, error.Message, StringComparison.Ordinal);
    }

    private static StubDetector Reporting(
        ExtractedDocument document,
        int segment,
        int start,
        int length,
        EntityType type) =>
        new(TrustBoundaryLocality.InProcess)
        {
            Findings = context =>
            [
                Entity(
                    SourceSpan.Create(document.Segments[segment].Id, start, length),
                    type,
                    context.DetectorId),
            ],
        };

    private static SensitiveEntity Entity(SourceSpan span, EntityType type, DetectorId detector) =>
        SensitiveEntity.Create(
            EntityId.New(),
            type,
            span,
            DetectionConfidence.Create(0.8),
            detector,
            SensitivityLevel.High,
            DetectionEvidence.FromModel("semantic", 0.8, span.Length));

    private static ExtractedDocument Extracted(params string[] texts)
    {
        var segments = new TextSegment[texts.Length];

        for (var order = 0; order < texts.Length; order++)
        {
            segments[order] = TextSegment.Create(
                SegmentId.Create($"segment-{order}"),
                PartId.Create("part-1"),
                texts[order],
                SegmentOrigin.At(SegmentOriginKind.Paragraph, order + 1),
                order);
        }

        return ExtractedDocument.Create(BundleId.New(), segments, [], []);
    }

    private sealed class StubDetector(TrustBoundaryLocality locality) : ISemanticDetector
    {
        public DetectorVersion Version { get; init; } = s_version;

        public TrustBoundaryLocality Locality { get; } = locality;

        public Func<SemanticDetectionContext, IReadOnlyList<SensitiveEntity>> Findings { get; init; } = _ => [];

        public SemanticDetectionContext? LastContext { get; private set; }

        public ValueTask<IReadOnlyList<SensitiveEntity>> ExamineAsync(
            SemanticDetectionContext context,
            CancellationToken cancellationToken)
        {
            LastContext = context;

            return ValueTask.FromResult(Findings(context));
        }
    }
}
