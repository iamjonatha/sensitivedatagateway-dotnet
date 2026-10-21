using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Detection.Semantic;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Testing;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Security.Tests;

/// <summary>
/// Proves that a document cannot talk its way past the stage that asks a model about it.
/// </summary>
/// <remarks>
/// <para>
/// A semantic detector is the one place in this library where document text is handed to
/// something that reads text for meaning, so it is the one place where an instruction hidden
/// in a document has anything to act on. The scenarios here cover the two halves of that:
/// what the document can do to the question, and what a model that has been talked into
/// misbehaving can do to the scan.
/// </para>
/// <para>
/// The answer to the first is "nothing": the question is a constant and the content sits
/// inside a fence the document cannot write. The answer to the second is "stop it": a model
/// that reports nothing leaves the deterministic detectors untouched, and a model that
/// reports nonsense fails the scan, which blocks.
/// </para>
/// </remarks>
public sealed class SemanticInjectionTests
{
    private static readonly EntityType[] s_asked = [EntityType.Person, EntityType.Address];

    [Fact]
    public void Every_hostile_document_is_asked_exactly_the_same_question()
    {
        var questions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var document in SyntheticCorpus.PromptInjections())
        {
            var context = Context(document);

            questions.Add(context.Render().Split("CONTENT BEGINS", StringSplitOptions.None)[0]);
        }

        Assert.Single(questions);
    }

    [Fact]
    public void A_hostile_document_cannot_close_the_block_it_sits_in()
    {
        foreach (var document in SyntheticCorpus.PromptInjections())
        {
            var context = Context(document);
            var rendered = context.Render();

            Assert.EndsWith($"CONTENT ENDS {context.Marker}", rendered, StringComparison.Ordinal);
            Assert.Equal(2, Occurrences(rendered, context.Marker));
        }
    }

    [Fact]
    public void A_hostile_document_reaches_the_model_unaltered()
    {
        foreach (var document in SyntheticCorpus.PromptInjections())
        {
            var context = Context(document);
            var rendered = context.Render();

            foreach (var segment in context.Segments)
            {
                Assert.Contains(segment.Text, rendered, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public async Task A_model_that_obeys_the_document_leaks_nothing()
    {
        var provider = Harness.Provider();
        var gateway = Harness.Over(GatewayWith(Obedient()), provider);

        foreach (var document in SyntheticCorpus.PromptInjections())
        {
            var result = await gateway.ExecuteAsync(
                document.ToBundle(),
                Harness.Request,
                Harness.Permissive(),
                TestContext.Current.CancellationToken);

            Assert.True(result.IsAllowed, $"The permissive policy blocked '{document.Name}'.");
        }

        LeakAssert.NoneOf(provider, SyntheticCorpus.Values());
    }

    [Fact]
    public async Task A_model_that_obeys_the_document_changes_nothing_that_crosses()
    {
        var withoutModel = Harness.Provider();
        var withModel = Harness.Provider();

        await RunCorpus(Harness.Over(Harness.Default(), withoutModel));
        await RunCorpus(Harness.Over(GatewayWith(Obedient()), withModel));

        Assert.Equal(withoutModel.Calls.Count, withModel.Calls.Count);

        for (var index = 0; index < withoutModel.Calls.Count; index++)
        {
            Assert.Equal(Join(withoutModel.Calls[index].Payload), Join(withModel.Calls[index].Payload));
        }
    }

    [Fact]
    public async Task A_model_talked_into_reporting_nonsense_blocks_the_document()
    {
        var provider = Harness.Provider();
        var gateway = Harness.Over(GatewayWith(Deceived()), provider);

        foreach (var document in SyntheticCorpus.PromptInjections())
        {
            var result = await gateway.ExecuteAsync(
                document.ToBundle(),
                Harness.Request,
                Harness.Permissive(),
                TestContext.Current.CancellationToken);

            Assert.False(result.IsAllowed, $"A broken scan let '{document.Name}' through.");
        }

        Assert.Empty(provider.Calls);
    }

    [Fact]
    public async Task A_model_that_refuses_to_answer_blocks_the_document()
    {
        var provider = Harness.Provider();
        var gateway = Harness.Over(GatewayWith(Refusing()), provider);

        foreach (var document in SyntheticCorpus.PromptInjections())
        {
            var result = await gateway.ExecuteAsync(
                document.ToBundle(),
                Harness.Request,
                Harness.Permissive(),
                TestContext.Current.CancellationToken);

            Assert.False(result.IsAllowed, $"A failed scan let '{document.Name}' through.");
        }

        Assert.Empty(provider.Calls);
    }

    private static async Task RunCorpus(DocumentGateway<ReviewRequest, ReviewDecision> gateway)
    {
        foreach (var document in SyntheticCorpus.PromptInjections())
        {
            _ = await gateway.ExecuteAsync(
                document.ToBundle(),
                Harness.Request,
                Harness.Permissive(),
                TestContext.Current.CancellationToken);
        }
    }

    private static DocumentGateway GatewayWith(ISemanticDetector detector)
    {
        var detectors = new List<ISensitiveDataDetector>(DeterministicDetectors.All())
        {
            SemanticDetectorHost.Create(detector, s_asked),
        };

        return Harness.WithDetectors(detectors);
    }

    private static SemanticDetectionContext Context(SyntheticDocument document)
    {
        var segments = new List<TextSegment>();
        var order = 0;

        foreach (var part in document.ToBundle().Parts)
        {
            segments.Add(TextSegment.Create(
                SegmentId.Create($"segment-{order}"),
                part.Id,
                part.Content.AsText(),
                SegmentOrigin.At(SegmentOriginKind.Paragraph, order + 1),
                order));
            order++;
        }

        return SemanticDetectionContext.Create(
            ExtractedDocument.Create(BundleId.New(), segments, [], []),
            DetectorId.Create("semantic/in-process/corpus"),
            s_asked);
    }

    private static StubSemanticDetector Obedient() => new(_ => []);

    private static StubSemanticDetector Deceived() =>
        new(context =>
        [
            SensitiveEntity.Create(
                EntityId.New(),
                EntityType.Person,
                SourceSpan.Create(context.Segments[0].Id, 0, context.Segments[0].Text.Length + 100),
                DetectionConfidence.Certain,
                context.DetectorId,
                SensitivityLevel.High,
                DetectionEvidence.FromModel("obedient", 1.0, 1)),
        ]);

    private static StubSemanticDetector Refusing() =>
        new(_ => throw new InvalidOperationException("The model declined."));

    private static string Join(ValidatedPayload payload)
    {
        var texts = new List<string>(payload.Content.Count);

        foreach (var segment in payload.Content)
        {
            texts.Add(segment.Text);
        }

        return string.Join("\n", texts);
    }

    private static int Occurrences(string text, string value)
    {
        var count = 0;
        var index = text.IndexOf(value, StringComparison.Ordinal);

        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }

    private sealed class StubSemanticDetector(Func<SemanticDetectionContext, IReadOnlyList<SensitiveEntity>> findings)
        : ISemanticDetector
    {
        public DetectorVersion Version { get; } =
            DetectorVersion.Create(DetectorId.Create("corpus"), "1.0.0");

        public TrustBoundaryLocality Locality => TrustBoundaryLocality.InProcess;

        public ValueTask<IReadOnlyList<SensitiveEntity>> ExamineAsync(
            SemanticDetectionContext context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(findings(context));
    }
}
