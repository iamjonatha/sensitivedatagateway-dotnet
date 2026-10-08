using System;
using System.Linq;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;

namespace SensitiveDataGateway.Tests;

public sealed class LocalReidentifierTests
{
    private const string Person = "Dana Oakfield";
    private const string Body = "Contact Dana Oakfield today";

    private static readonly PolicyEngine s_engine = new();

    [Fact]
    public void Restore_PutsBackTheValueAPseudonymStandsFor()
    {
        var store = new InMemoryPseudonymStore();
        var bundle = BundleId.New();
        var sanitized = Sanitize(store, bundle, TransformationAction.Pseudonymize);

        var restored = new LocalReidentifier(store).Restore(Text(sanitized), PseudonymScopeKey.ForBundle(bundle));

        Assert.Equal(Body, restored);
    }

    [Fact]
    public void Restore_PutsBackNothingAHashStandsFor()
    {
        var store = new InMemoryPseudonymStore();
        var bundle = BundleId.New();
        var sanitized = Sanitize(store, bundle, TransformationAction.Hash);

        var restored = new LocalReidentifier(store).Restore(Text(sanitized), PseudonymScopeKey.ForBundle(bundle));

        Assert.Equal(Text(sanitized), restored);
        Assert.DoesNotContain(Person, restored, StringComparison.Ordinal);
    }

    [Fact]
    public void Restore_PutsBackNothingARedactionStandsFor()
    {
        var store = new InMemoryPseudonymStore();
        var bundle = BundleId.New();
        var sanitized = Sanitize(store, bundle, TransformationAction.Redact);

        var restored = new LocalReidentifier(store).Restore(Text(sanitized), PseudonymScopeKey.ForBundle(bundle));

        Assert.Equal("Contact [REDACTED_PERSON] today", restored);
    }

    [Fact]
    public void Restore_UnescapesTextThatOnlyLookedLikeAToken()
    {
        var store = new InMemoryPseudonymStore();
        var bundle = BundleId.New();
        const string Forged = "[PERSON_001] met Dana Oakfield";
        var document = Document(bundle, Forged);
        var sanitized = Anonymize(store, document, TransformationAction.Pseudonymize);

        var restored = new LocalReidentifier(store).Restore(Text(sanitized), PseudonymScopeKey.ForBundle(bundle));

        Assert.Equal(Forged, restored);
    }

    [Fact]
    public void Restore_LeavesATokenOfAnotherScopeAsItStands()
    {
        var store = new InMemoryPseudonymStore();
        var bundle = BundleId.New();
        Sanitize(store, bundle, TransformationAction.Pseudonymize);

        var restored = new LocalReidentifier(store)
            .Restore("[EMAIL_007] wrote", PseudonymScopeKey.ForBundle(bundle));

        Assert.Equal("[EMAIL_007] wrote", restored);
    }

    [Fact]
    public void Restore_LeavesTheTextAloneWhenTheScopeHasNoMap()
    {
        var restored = new LocalReidentifier(new InMemoryPseudonymStore())
            .Restore("[PERSON_001] wrote", PseudonymScopeKey.ForBundle(BundleId.New()));

        Assert.Equal("[PERSON_001] wrote", restored);
    }

    [Fact]
    public void Restore_RestoresNothingOnceTheMapIsReleased()
    {
        var store = new InMemoryPseudonymStore();
        var bundle = BundleId.New();
        var sanitized = Sanitize(store, bundle, TransformationAction.Pseudonymize);
        var key = PseudonymScopeKey.ForBundle(bundle);

        store.Release(key);

        Assert.Equal(Text(sanitized), new LocalReidentifier(store).Restore(Text(sanitized), key));
    }

    [Fact]
    public void Restore_RejectsAScopeKeyThatCarriesNothing() =>
        Assert.Throws<ArgumentException>(
            () => new LocalReidentifier(new InMemoryPseudonymStore()).Restore("text", default));

    [Fact]
    public void Restore_RejectsNullText() =>
        Assert.Throws<ArgumentNullException>(() => new LocalReidentifier(new InMemoryPseudonymStore())
            .Restore(null!, PseudonymScopeKey.ForBundle(BundleId.New())));

    private static SanitizedDocument Sanitize(
        InMemoryPseudonymStore store,
        BundleId bundle,
        TransformationAction action) =>
        Anonymize(store, Document(bundle, Body), action);

    private static SanitizedDocument Anonymize(
        InMemoryPseudonymStore store,
        DetectedDocument document,
        TransformationAction action)
    {
        var policy = Fixture.Policy(PolicyRule.For(EntityType.Person, action));
        var anonymizer = new DocumentAnonymizer(
            store,
            AnonymizationOptions.Default.WithHashingKey(HashingKey.CreateEphemeral()));

        return anonymizer.Anonymize(document, s_engine.Evaluate(document, policy), Fixture.Provenance());
    }

    private static DetectedDocument Document(BundleId bundle, string text) =>
        Fixture.Document(
            bundle,
            Fixture.Part(
                "body",
                text,
                Fixture.At(EntityType.Person, text.IndexOf(Person, StringComparison.Ordinal), Person.Length)));

    private static string Text(SanitizedDocument document) =>
        string.Concat(document.Segments.Select(segment => segment.Text));
}
