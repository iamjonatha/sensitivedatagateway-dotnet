using System;
using System.Linq;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Tests;

public sealed class PseudonymMapTests
{
    private static readonly PseudonymScopeKey s_key = PseudonymScopeKey.ForBundle(BundleId.New());

    [Fact]
    public void Allocate_GivesOneValueOneToken()
    {
        var map = PseudonymMap.Create(s_key);

        var first = map.Allocate(EntityType.Person, "Dana Oakfield");
        var second = map.Allocate(EntityType.Person, "Dana Oakfield");

        Assert.Equal(first, second);
        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void Allocate_NumbersEachTypeSeparately()
    {
        var map = PseudonymMap.Create(s_key);

        Assert.Equal("[PERSON_001]", map.Allocate(EntityType.Person, "Dana Oakfield"));
        Assert.Equal("[PERSON_002]", map.Allocate(EntityType.Person, "Rhys Calloway"));
        Assert.Equal("[EMAIL_001]", map.Allocate(EntityType.Email, "dana@example.invalid"));
    }

    [Fact]
    public void Allocate_TellsTwoValuesApartEvenWhenTheyShareATokenShape()
    {
        var map = PseudonymMap.Create(s_key);

        var person = map.Allocate(EntityType.Person, "[PERSON_001]");
        var other = map.Allocate(EntityType.Person, "Dana Oakfield");

        Assert.NotEqual(person, other);
    }

    [Fact]
    public void Allocate_RejectsAnEmptyValue() =>
        Assert.Throws<ArgumentException>(() => PseudonymMap.Create(s_key).Allocate(EntityType.Person, ""));

    [Fact]
    public void TryReveal_ReturnsTheValueTheTokenReplaced()
    {
        var map = PseudonymMap.Create(s_key);
        var token = map.Allocate(EntityType.Person, "Dana Oakfield");

        Assert.True(map.TryReveal(token, out var value));
        Assert.Equal("Dana Oakfield", value);
    }

    [Fact]
    public void TryReveal_RefusesATokenThisMapNeverAllocated()
    {
        var map = PseudonymMap.Create(s_key);
        map.Allocate(EntityType.Person, "Dana Oakfield");

        Assert.False(map.TryReveal("[PERSON_999]", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void Clear_ForgetsEveryMapping()
    {
        var map = PseudonymMap.Create(s_key);
        var token = map.Allocate(EntityType.Person, "Dana Oakfield");

        map.Clear();

        Assert.Equal(0, map.Count);
        Assert.False(map.TryReveal(token, out _));
    }

    [Fact]
    public void ToString_RendersNeitherATokenNorAValue()
    {
        var map = PseudonymMap.Create(s_key);
        map.Allocate(EntityType.Person, "Dana Oakfield");

        var rendered = map.ToString();

        Assert.DoesNotContain("Dana Oakfield", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("PERSON_001", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_RejectsAScopeKeyThatCarriesNothing() =>
        Assert.Throws<ArgumentException>(() => PseudonymMap.Create(default));
}

public sealed class PseudonymScopeKeyTests
{
    [Fact]
    public void ForDocument_ScopesToTheDocument()
    {
        var key = PseudonymScopeKey.ForDocument(PartId.Create("body"));

        Assert.Equal(PseudonymScope.Document, key.Scope);
        Assert.Equal("body", key.Value);
    }

    [Fact]
    public void ForBundle_ScopesToTheBundle()
    {
        var bundle = BundleId.New();

        Assert.Equal(PseudonymScope.Bundle, PseudonymScopeKey.ForBundle(bundle).Scope);
    }

    [Fact]
    public void TwoKeysOfDifferentScopesAreNotTheSameKey()
    {
        var document = PseudonymScopeKey.ForDocument(PartId.Create("same"));
        var session = PseudonymScopeKey.ForWiderScope(PseudonymScope.Session, "same");

        Assert.NotEqual(document, session);
    }

    [Theory]
    [InlineData(PseudonymScope.Document)]
    [InlineData(PseudonymScope.Bundle)]
    public void ForWiderScope_RefusesAScopeThatIsNotWide(PseudonymScope scope) =>
        Assert.Throws<ArgumentException>(() => PseudonymScopeKey.ForWiderScope(scope, "name"));
}

public sealed class InMemoryPseudonymStoreTests
{
    [Fact]
    public void GetOrCreate_ReturnsTheSameMapForTheSameScope()
    {
        var store = new InMemoryPseudonymStore();
        var key = PseudonymScopeKey.ForBundle(BundleId.New());

        Assert.Same(store.GetOrCreate(key), store.GetOrCreate(key));
    }

    [Fact]
    public void GetOrCreate_KeepsScopesApart()
    {
        var store = new InMemoryPseudonymStore();

        var first = store.GetOrCreate(PseudonymScopeKey.ForBundle(BundleId.New()));
        var second = store.GetOrCreate(PseudonymScopeKey.ForBundle(BundleId.New()));

        Assert.NotSame(first, second);
    }

    [Fact]
    public void Release_ForgetsTheMapSoTheValuesCannotComeBack()
    {
        var store = new InMemoryPseudonymStore();
        var key = PseudonymScopeKey.ForBundle(BundleId.New());
        var token = store.GetOrCreate(key).Allocate(EntityType.Person, "Dana Oakfield");

        Assert.True(store.Release(key));
        Assert.False(store.TryGet(key, out _));
        Assert.False(store.GetOrCreate(key).TryReveal(token, out _));
    }

    [Fact]
    public void TryGet_DoesNotCreateAMap()
    {
        var store = new InMemoryPseudonymStore();

        Assert.False(store.TryGet(PseudonymScopeKey.ForBundle(BundleId.New()), out var map));
        Assert.Null(map);
    }
}

public sealed class MaskerTests
{
    [Theory]
    [InlineData("short", "*****")]
    [InlineData("1234567", "*******")]
    [InlineData("12345678", "12****78")]
    [InlineData("0123456789AB", "012******9AB")]
    [InlineData("0123456789ABCDEF", "0123********CDEF")]
    public void Apply_KeepsTheLengthAndMasksAtLeastHalf(string value, string expected)
    {
        var masked = Masker.Apply(value);

        Assert.Equal(expected, masked);
        Assert.Equal(value.Length, masked.Length);
    }

    [Fact]
    public void Apply_ProducesNothingThatLooksLikeAToken() =>
        Assert.False(MarkerSyntax.ContainsMarker(Masker.Apply("ABCDEFGHIJ")));

    [Fact]
    public void Apply_RejectsNull() => Assert.Throws<ArgumentNullException>(() => Masker.Apply(null!));
}

public sealed class RedactorTests
{
    [Fact]
    public void Apply_NamesTheTypeThatWasRemoved() =>
        Assert.Equal("[REDACTED_EMAIL]", Redactor.Apply(EntityType.Email));

    [Fact]
    public void Apply_IsTheSameForEveryValueOfAType() =>
        Assert.Equal(Redactor.Apply(EntityType.Person), Redactor.Apply(EntityType.Person));
}

public sealed class HashingTests
{
    [Fact]
    public void Create_RefusesKeyMaterialThatIsTooShort() =>
        Assert.Throws<ArgumentException>(() => HashingKey.Create(new byte[HashingKey.MinimumLength - 1]));

    [Fact]
    public void Apply_GivesOneValueOneTokenUnderOneKey()
    {
        var hasher = new Hasher(HashingKey.CreateEphemeral());

        Assert.Equal(
            hasher.Apply(EntityType.Iban, "XX00TESTBANK0000000001"),
            hasher.Apply(EntityType.Iban, "XX00TESTBANK0000000001"));
    }

    [Fact]
    public void Apply_GivesOneValueDifferentTokensUnderDifferentKeys()
    {
        var first = new Hasher(HashingKey.CreateEphemeral());
        var second = new Hasher(HashingKey.CreateEphemeral());

        Assert.NotEqual(
            first.Apply(EntityType.Iban, "XX00TESTBANK0000000001"),
            second.Apply(EntityType.Iban, "XX00TESTBANK0000000001"));
    }

    [Fact]
    public void Apply_IsStableAcrossProcessesWhenTheKeyIs()
    {
        var material = Enumerable.Range(0, HashingKey.MinimumLength).Select(value => (byte)value).ToArray();

        var first = new Hasher(HashingKey.Create(material));
        var second = new Hasher(HashingKey.Create(material));

        Assert.Equal(
            first.Apply(EntityType.TaxId, "TSTTST00A01H501Z"),
            second.Apply(EntityType.TaxId, "TSTTST00A01H501Z"));
    }

    [Fact]
    public void Apply_ProducesAMarkerOfTheDeclaredShape()
    {
        var token = new Hasher(HashingKey.CreateEphemeral()).Apply(EntityType.Iban, "XX00TESTBANK0000000001");

        Assert.StartsWith("[IBAN_H", token, StringComparison.Ordinal);
        Assert.EndsWith("]", token, StringComparison.Ordinal);
        Assert.Equal("[IBAN_H".Length + (HashingKey.DigestBytes * 2) + 1, token.Length);
    }

    [Fact]
    public void ToString_RendersNoKeyMaterial()
    {
        var material = Enumerable.Repeat((byte)0xAB, HashingKey.MinimumLength).ToArray();

        Assert.Equal("HashingKey", HashingKey.Create(material).ToString());
    }
}
