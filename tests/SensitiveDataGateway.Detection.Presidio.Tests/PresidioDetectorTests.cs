using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Detection.Semantic;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Detection.Presidio.Tests;

public sealed class PresidioDetectorTests
{
    private static readonly Uri s_endpoint = new("https://analyzer.internal/");

    [Fact]
    public async Task ExamineAsync_TurnsAnAnalyzerFindingIntoAnEntityInTheSegmentItNamed()
    {
        var handler = Answering("""[{"entity_type":"PERSON","start":0,"end":12,"score":0.85}]""");
        var detector = PresidioDetector.Create(new HttpClient(handler), PresidioOptions.ForSelfHosted(s_endpoint));
        var context = Context("Giulia Ferri lives here.");

        var entities = await detector.ExamineAsync(context, TestContext.Current.CancellationToken);

        var entity = Assert.Single(entities);
        Assert.Equal(EntityType.Person, entity.Type);
        Assert.Equal(context.Segments[0].Id, entity.Span.SegmentId);
        Assert.Equal(0, entity.Span.Start);
        Assert.Equal(12, entity.Span.Length);
        Assert.Equal(context.DetectorId, entity.Detector);
    }

    [Fact]
    public async Task ExamineAsync_AsksTheAnalyzerOnceForEachSegment()
    {
        var handler = Answering("[]");
        var detector = PresidioDetector.Create(new HttpClient(handler), PresidioOptions.ForSelfHosted(s_endpoint));

        await detector.ExamineAsync(Context("one", "two", "three"), TestContext.Current.CancellationToken);

        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.Equal("/analyze", request.AbsolutePath));
    }

    [Fact]
    public async Task ExamineAsync_DoesNotAskAboutASegmentWithNoText()
    {
        var handler = Answering("[]");
        var detector = PresidioDetector.Create(new HttpClient(handler), PresidioOptions.ForSelfHosted(s_endpoint));

        await detector.ExamineAsync(Context(string.Empty), TestContext.Current.CancellationToken);

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ExamineAsync_SendsTheTextAndTheLanguageTheOptionsName()
    {
        var handler = Answering("[]");
        var detector = PresidioDetector.Create(
            new HttpClient(handler),
            PresidioOptions.ForSelfHosted(s_endpoint, "it"));

        await detector.ExamineAsync(Context("Il signor Rossi."), TestContext.Current.CancellationToken);

        Assert.Contains("\"it\"", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("Il signor Rossi.", handler.Bodies[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExamineAsync_KeepsAFindingWhoseNameTheMapDoesNotHave()
    {
        var handler = Answering("""[{"entity_type":"AU_ABN","start":0,"end":3,"score":0.5}]""");
        var detector = PresidioDetector.Create(new HttpClient(handler), PresidioOptions.ForSelfHosted(s_endpoint));

        var entities = await detector.ExamineAsync(Context("abc"), TestContext.Current.CancellationToken);

        Assert.Equal(EntityType.Create("au_abn"), Assert.Single(entities).Type);
    }

    [Fact]
    public async Task ExamineAsync_DiscardsAFindingBelowTheScoreTheOptionsRequire()
    {
        var handler = Answering("""[{"entity_type":"PERSON","start":0,"end":3,"score":0.2}]""");
        var detector = PresidioDetector.Create(
            new HttpClient(handler),
            PresidioOptions.ForSelfHosted(s_endpoint).WithMinimumScore(0.5));

        var entities = await detector.ExamineAsync(Context("abc"), TestContext.Current.CancellationToken);

        Assert.Empty(entities);
    }

    [Theory]
    [InlineData("""[{"entity_type":"PERSON","start":0,"end":99,"score":0.9}]""")]
    [InlineData("""[{"entity_type":"PERSON","start":-1,"end":2,"score":0.9}]""")]
    [InlineData("""[{"entity_type":"PERSON","start":2,"end":2,"score":0.9}]""")]
    public async Task ExamineAsync_DiscardsAFindingThatDoesNotFitTheSegmentItDescribes(string body)
    {
        var handler = Answering(body);
        var detector = PresidioDetector.Create(new HttpClient(handler), PresidioOptions.ForSelfHosted(s_endpoint));

        var entities = await detector.ExamineAsync(Context("abc"), TestContext.Current.CancellationToken);

        Assert.Empty(entities);
    }

    [Fact]
    public async Task ExamineAsync_FailsWhenTheAnalyzerDoesNot()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError, "nope");
        var detector = PresidioDetector.Create(new HttpClient(handler), PresidioOptions.ForSelfHosted(s_endpoint));

        await Assert.ThrowsAsync<HttpRequestException>(
            async () => await detector.ExamineAsync(Context("abc"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExamineAsync_StopsWhenTheScanIsCancelled()
    {
        var handler = Answering("[]");
        var detector = PresidioDetector.Create(new HttpClient(handler), PresidioOptions.ForSelfHosted(s_endpoint));
        using var cancellation = new CancellationTokenSource();

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await detector.ExamineAsync(Context("abc"), cancellation.Token));
    }

    [Fact]
    public void Create_RejectsMissingArguments()
    {
        Assert.Throws<ArgumentNullException>(() => PresidioDetector.Create(null!, PresidioOptions.ForSelfHosted(s_endpoint)));
        Assert.Throws<ArgumentNullException>(() => PresidioDetector.Create(new HttpClient(), null!));
    }

    private static StubHandler Answering(string body) => new(HttpStatusCode.OK, body);

    private static SemanticDetectionContext Context(params string[] texts)
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

        return SemanticDetectionContext.Create(
            ExtractedDocument.Create(BundleId.New(), segments, [], []),
            DetectorId.Create("semantic/self-hosted/presidio"),
            [EntityType.Person, EntityType.Address, EntityType.Create("au_abn")]);
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Requests.Add(request.RequestUri!);
            Bodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }
}
