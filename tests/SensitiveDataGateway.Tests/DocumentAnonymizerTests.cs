using System;
using System.Linq;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;

namespace SensitiveDataGateway.Tests;

public sealed class DocumentAnonymizerTests
{
    private const string Person = "Dana Oakfield";
    private const string Email = "dana@example.invalid";
    private const string Body = "Contact Dana Oakfield at dana@example.invalid today";

    private static readonly PolicyEngine s_engine = new();

    [Fact]
    public void Anonymize_ReplacesEveryPlannedSpan()
    {
        var document = Fixture.Document(Fixture.Part("body", Body, Span(Body, Person, EntityType.Person)));
        var sanitized = Run(document, Rule(EntityType.Person, TransformationAction.Redact));

        Assert.Equal("Contact [REDACTED_PERSON] at dana@example.invalid today", Text(sanitized));
        Assert.Equal(SanitizationStatus.Valid, sanitized.Status);
    }

    [Fact]
    public void Anonymize_LeavesTheInputUntouched()
    {
        var document = Fixture.Document(Fixture.Part("body", Body, Span(Body, Person, EntityType.Person)));

        Run(document, Rule(EntityType.Person, TransformationAction.Redact));

        Assert.Equal(Body, document.Extracted.Segments[0].Text);
    }

    [Fact]
    public void Anonymize_RewritesEverySpanOfASegmentWithoutDisturbingTheOthers()
    {
        var document = Fixture.Document(Fixture.Part(
            "body",
            Body,
            Span(Body, Person, EntityType.Person),
            Span(Body, Email, EntityType.Email)));

        var sanitized = Run(
            document,
            Rule(EntityType.Person, TransformationAction.Pseudonymize),
            Rule(EntityType.Email, TransformationAction.Redact));

        Assert.Equal("Contact [PERSON_001] at [REDACTED_EMAIL] today", Text(sanitized));
        Assert.Equal(2, sanitized.Applied.Count);
    }

    [Fact]
    public void Anonymize_KeepsNoneOfTheValuesItReplaced()
    {
        var document = Fixture.Document(Fixture.Part(
            "body",
            Body,
            Span(Body, Person, EntityType.Person),
            Span(Body, Email, EntityType.Email)));

        var sanitized = Run(
            document,
            Rule(EntityType.Person, TransformationAction.Pseudonymize),
            Rule(EntityType.Email, TransformationAction.Hash));

        var rendered = Text(sanitized) + sanitized.ToString();

        Assert.DoesNotContain(Person, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(Email, rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Anonymize_RecordsWhatItAppliedAgainstTheOriginalOffsets()
    {
        var document = Fixture.Document(Fixture.Part("body", Body, Span(Body, Person, EntityType.Person)));
        var sanitized = Run(document, Rule(EntityType.Person, TransformationAction.Redact));

        var applied = Assert.Single(sanitized.Applied);

        Assert.Equal(EntityType.Person, applied.EntityType);
        Assert.Equal(TransformationAction.Redact, applied.Action);
        Assert.Equal(Body.IndexOf(Person, StringComparison.Ordinal), applied.Span.Start);
        Assert.Equal(Person.Length, applied.Span.Length);
        Assert.Equal("[REDACTED_PERSON]".Length, applied.ReplacementLength);
    }

    [Fact]
    public void Anonymize_MasksWithoutChangingTheLengthOfTheValue()
    {
        var document = Fixture.Document(Fixture.Part("body", Body, Span(Body, Email, EntityType.Email)));
        var sanitized = Run(document, Rule(EntityType.Email, TransformationAction.Mask));

        Assert.Equal(Body.Length, Text(sanitized).Length);
        Assert.Contains("*", Text(sanitized), StringComparison.Ordinal);
    }

    [Fact]
    public void Anonymize_AppliesNothingForAPreservedEntity()
    {
        var document = Fixture.Document(Fixture.Part("body", Body, Span(Body, Person, EntityType.Person)));
        var sanitized = Run(document, Rule(EntityType.Person, TransformationAction.Preserve));

        Assert.Equal(Body, Text(sanitized));
        Assert.Empty(sanitized.Applied);
        Assert.Equal(SanitizationStatus.Valid, sanitized.Status);
    }

    [Fact]
    public void Anonymize_CarriesTheProvenanceItWasGiven()
    {
        var document = Fixture.Document(Fixture.Part("body", Body, Span(Body, Person, EntityType.Person)));
        var sanitized = Run(document, Rule(EntityType.Person, TransformationAction.Redact));

        Assert.Equal(Fixture.PolicyName, sanitized.Provenance.PolicyId);
        Assert.Equal(Fixture.SanitizedAt, sanitized.Provenance.SanitizedAt);
    }

    [Fact]
    public void Anonymize_CorrelatesOneValueAcrossTheWholeBundle()
    {
        var document = Fixture.Document(
            BundleId.New(),
            Fixture.Part("body", Body, Span(Body, Person, EntityType.Person)),
            Fixture.Part("attachment-1", $"Invoice for {Person}", Span($"Invoice for {Person}", Person, EntityType.Person)),
            Fixture.Part("attachment-2", $"Contract with {Person}", Span($"Contract with {Person}", Person, EntityType.Person)));

        var sanitized = Run(document, Rule(EntityType.Person, TransformationAction.Pseudonymize));

        Assert.All(sanitized.Segments, segment =>
            Assert.Contains("[PERSON_001]", segment.Text, StringComparison.Ordinal));
    }

    [Fact]
    public void Anonymize_KeepsDocumentScopedTokensInSeparateMaps()
    {
        var store = new InMemoryPseudonymStore();
        var document = Fixture.Document(
            BundleId.New(),
            Fixture.Part("body", Body, Span(Body, Person, EntityType.Person)),
            Fixture.Part("attachment-1", $"Invoice for {Person}", Span($"Invoice for {Person}", Person, EntityType.Person)));

        var policy = Fixture.Policy(PolicyRule
            .For(EntityType.Person, TransformationAction.Pseudonymize)
            .WithScope(PseudonymScope.Document));

        new DocumentAnonymizer(store).Anonymize(document, s_engine.Evaluate(document, policy), Fixture.Provenance());

        Assert.True(store.TryGet(PseudonymScopeKey.ForDocument(PartId.Create("body")), out var first));
        Assert.True(store.TryGet(PseudonymScopeKey.ForDocument(PartId.Create("attachment-1")), out var second));
        Assert.NotSame(first, second);
        Assert.Equal(1, first!.Count);
        Assert.Equal(1, second!.Count);
    }

    [Fact]
    public void Anonymize_EscapesTextThatIsShapedLikeAToken()
    {
        const string Forged = "The report says [PERSON_001] wrote it, signed Dana Oakfield";
        var document = Fixture.Document(Fixture.Part("body", Forged, Span(Forged, Person, EntityType.Person)));

        var sanitized = Run(document, Rule(EntityType.Person, TransformationAction.Pseudonymize));

        Assert.Contains("[[PERSON_001]", Text(sanitized), StringComparison.Ordinal);
        Assert.Contains(
            sanitized.Warnings,
            warning => warning.Code == SanitizationWarningCode.MarkerSyntaxEscaped);
    }

    [Fact]
    public void Anonymize_KeepsAForgedTokenApartFromTheOneItAllocates()
    {
        const string Forged = "[PERSON_001] met Dana Oakfield";
        var document = Fixture.Document(Fixture.Part("body", Forged, Span(Forged, Person, EntityType.Person)));

        var sanitized = Run(document, Rule(EntityType.Person, TransformationAction.Pseudonymize));

        Assert.Equal("[[PERSON_001] met [PERSON_001]", Text(sanitized));
    }

    [Fact]
    public void Anonymize_ReportsAnEntityCoveredByAStrongerOneWithoutBlocking()
    {
        const string Line = "Dana Oakfield Jr";
        var document = Fixture.Document(Fixture.Part(
            "body",
            Line,
            Fixture.At(EntityType.Person, 0, Line.Length),
            Fixture.At(EntityType.Customer, 0, 4)));

        var sanitized = Run(
            document,
            Rule(EntityType.Person, TransformationAction.Redact),
            Rule(EntityType.Customer, TransformationAction.Mask));

        Assert.Equal(SanitizationStatus.Valid, sanitized.Status);
        Assert.Equal("[REDACTED_PERSON]", Text(sanitized));
        Assert.Contains(
            sanitized.Warnings,
            warning => warning.Code == SanitizationWarningCode.OverlappingEntities);
    }

    [Fact]
    public void Anonymize_BlocksWhenTwoEntitiesOverlapOnlyInPart()
    {
        const string Line = "Dana Oakfield Jr";
        var document = Fixture.Document(Fixture.Part(
            "body",
            Line,
            Fixture.At(EntityType.Person, 0, 13),
            Fixture.At(EntityType.Customer, 5, 11)));

        var sanitized = Run(
            document,
            Rule(EntityType.Person, TransformationAction.Redact),
            Rule(EntityType.Customer, TransformationAction.Mask));

        Assert.Equal(SanitizationStatus.Incomplete, sanitized.Status);
        Assert.Contains(
            sanitized.Warnings,
            warning => warning.Code == SanitizationWarningCode.TransformationSkipped);
    }

    [Fact]
    public void Anonymize_BlocksWhenAPlannedEntityIsNotInTheDocument()
    {
        var bundle = BundleId.New();
        var detected = Fixture.Document(bundle, Fixture.Part("body", Body, Span(Body, Person, EntityType.Person)));
        var plan = s_engine.Evaluate(detected, Fixture.Policy(Rule(EntityType.Person, TransformationAction.Redact)));

        var other = Fixture.Document(bundle, Fixture.Part("body", Body));
        var sanitized = Anonymizer().Anonymize(other, plan, Fixture.Provenance());

        Assert.Equal(SanitizationStatus.Incomplete, sanitized.Status);
        Assert.Contains(
            sanitized.Warnings,
            warning => warning.Code == SanitizationWarningCode.UnresolvedEntity);
    }

    [Fact]
    public void Anonymize_RefusesAPlanBuiltForAnotherBundle()
    {
        var detected = Fixture.Document(Fixture.Part("body", Body, Span(Body, Person, EntityType.Person)));
        var plan = s_engine.Evaluate(detected, Fixture.Policy(Rule(EntityType.Person, TransformationAction.Redact)));
        var other = Fixture.Document(Fixture.Part("body", Body));

        Assert.Throws<ArgumentException>(() => Anonymizer().Anonymize(other, plan, Fixture.Provenance()));
    }

    [Fact]
    public void Anonymize_RefusesABlockedPlan()
    {
        var document = Fixture.Document(Fixture.Part("body", Body, Span(Body, Person, EntityType.Person)));
        var plan = s_engine.Evaluate(document, Fixture.Policy());

        Assert.True(plan.IsBlocked);
        Assert.Throws<InvalidOperationException>(() => Anonymizer().Anonymize(document, plan, Fixture.Provenance()));
    }

    [Fact]
    public void Anonymize_RefusesToHashWithoutAKey()
    {
        var document = Fixture.Document(Fixture.Part("body", Body, Span(Body, Email, EntityType.Email)));
        var plan = s_engine.Evaluate(document, Fixture.Policy(Rule(EntityType.Email, TransformationAction.Hash)));

        Assert.Throws<InvalidOperationException>(
            () => new DocumentAnonymizer(new InMemoryPseudonymStore()).Anonymize(document, plan, Fixture.Provenance()));
    }

    [Fact]
    public void Anonymize_RefusesAScopeTheApplicationDidNotName()
    {
        var document = Fixture.Document(Fixture.Part("body", Body, Span(Body, Person, EntityType.Person)));
        var policy = Fixture.Policy(PolicyRule
            .For(EntityType.Person, TransformationAction.Pseudonymize)
            .WithScope(PseudonymScope.Session));

        var plan = s_engine.Evaluate(document, policy);

        Assert.Throws<InvalidOperationException>(
            () => new DocumentAnonymizer(new InMemoryPseudonymStore()).Anonymize(document, plan, Fixture.Provenance()));
    }

    [Fact]
    public void Anonymize_CorrelatesAcrossBundlesWhenTheSessionIsNamed()
    {
        var options = AnonymizationOptions.Default.WithSession("session-1");
        var store = new InMemoryPseudonymStore();
        var anonymizer = new DocumentAnonymizer(store, options);
        var policy = Fixture.Policy(PolicyRule
            .For(EntityType.Person, TransformationAction.Pseudonymize)
            .WithScope(PseudonymScope.Session));

        var first = Sanitize(anonymizer, policy);
        var second = Sanitize(anonymizer, policy);

        Assert.Equal(Text(first), Text(second));
        Assert.True(store.TryGet(PseudonymScopeKey.ForWiderScope(PseudonymScope.Session, "session-1"), out var map));
        Assert.Equal(1, map!.Count);
    }

    [Fact]
    public void Anonymize_RejectsNullArguments()
    {
        var document = Fixture.Document(Fixture.Part("body", Body));
        var plan = s_engine.Evaluate(document, Fixture.Policy());
        var anonymizer = Anonymizer();

        Assert.Throws<ArgumentNullException>(() => anonymizer.Anonymize(null!, plan, Fixture.Provenance()));
        Assert.Throws<ArgumentNullException>(() => anonymizer.Anonymize(document, null!, Fixture.Provenance()));
        Assert.Throws<ArgumentNullException>(() => anonymizer.Anonymize(document, plan, null!));
    }

    private static SanitizedDocument Sanitize(DocumentAnonymizer anonymizer, SanitizationPolicy policy)
    {
        var document = Fixture.Document(Fixture.Part("body", Body, Span(Body, Person, EntityType.Person)));

        return anonymizer.Anonymize(document, s_engine.Evaluate(document, policy), Fixture.Provenance());
    }

    private static SanitizedDocument Run(DetectedDocument document, params PolicyRule[] rules) =>
        Anonymizer().Anonymize(
            document,
            s_engine.Evaluate(document, Fixture.Policy(rules)),
            Fixture.Provenance());

    private static DocumentAnonymizer Anonymizer() =>
        new(
            new InMemoryPseudonymStore(),
            AnonymizationOptions.Default.WithHashingKey(HashingKey.CreateEphemeral()));

    private static PolicyRule Rule(EntityType type, TransformationAction action) =>
        PolicyRule.For(type, action);

    private static Fixture.EntitySpec Span(string text, string value, EntityType type) =>
        Fixture.At(type, text.IndexOf(value, StringComparison.Ordinal), value.Length);

    private static string Text(SanitizedDocument document) =>
        string.Concat(document.Segments.Select(segment => segment.Text));
}
