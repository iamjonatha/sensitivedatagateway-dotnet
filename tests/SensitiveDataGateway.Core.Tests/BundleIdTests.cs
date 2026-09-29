using System;
using System.Collections.Generic;

namespace SensitiveDataGateway.Core.Tests;

public sealed class BundleIdTests
{
    [Fact]
    public void New_ProducesAnIdentifierThatIsNotDefault()
    {
        var bundleId = BundleId.New();

        Assert.False(bundleId.IsDefault);
        Assert.NotEmpty(bundleId.Value);
    }

    [Fact]
    public void New_ProducesADifferentIdentifierEveryTime()
    {
        var identifiers = new HashSet<BundleId>();

        for (var index = 0; index < 1_000; index++)
        {
            Assert.True(identifiers.Add(BundleId.New()));
        }
    }

    [Fact]
    public void Create_KeepsTheValueItWasGiven()
    {
        var bundleId = BundleId.Create("case-4711");

        Assert.Equal("case-4711", bundleId.Value);
    }

    [Fact]
    public void Create_TrimsSurroundingWhiteSpace()
    {
        var bundleId = BundleId.Create("  case-4711\t");

        Assert.Equal("case-4711", bundleId.Value);
    }

    [Fact]
    public void Create_AcceptsAValueOfExactlyTheMaximumLength()
    {
        var value = new string('a', BundleId.MaxLength);

        Assert.Equal(value, BundleId.Create(value).Value);
    }

    [Fact]
    public void Create_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => BundleId.Create(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Create_RejectsAnEmptyValue(string value)
    {
        Assert.Throws<ArgumentException>(() => BundleId.Create(value));
    }

    [Fact]
    public void Create_RejectsAValueLongerThanTheMaximum()
    {
        var value = new string('a', BundleId.MaxLength + 1);

        Assert.Throws<ArgumentException>(() => BundleId.Create(value));
    }

    [Theory]
    [InlineData("case\n4711")]
    [InlineData("case\r4711")]
    [InlineData("case\u00004711")]
    public void Create_RejectsControlCharacters(string value)
    {
        Assert.Throws<ArgumentException>(() => BundleId.Create(value));
    }

    // A rejected identifier may be the very content the caller should not have passed, so the
    // diagnostic must describe the rule and not the value that broke it.
    [Fact]
    public void Create_DoesNotQuoteTheRejectedValueInTheException()
    {
        var secret = "mario.rossi@example.test";

        var exception = Assert.Throws<ArgumentException>(
            () => BundleId.Create(secret + new string('a', BundleId.MaxLength)));

        Assert.DoesNotContain(secret, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryCreate_ReportsFailureWithoutThrowing()
    {
        Assert.False(BundleId.TryCreate(null, out var bundleId));
        Assert.True(bundleId.IsDefault);

        Assert.False(BundleId.TryCreate("   ", out bundleId));
        Assert.True(bundleId.IsDefault);
    }

    [Fact]
    public void TryCreate_ReportsSuccess()
    {
        Assert.True(BundleId.TryCreate(" case-4711 ", out var bundleId));
        Assert.Equal("case-4711", bundleId.Value);
    }

    [Fact]
    public void Equality_IsOrdinalAndCaseSensitive()
    {
        var lower = BundleId.Create("case-4711");
        var upper = BundleId.Create("CASE-4711");

        Assert.Equal(lower, BundleId.Create("case-4711"));
        Assert.True(lower == BundleId.Create("case-4711"));
        Assert.NotEqual(lower, upper);
        Assert.True(lower != upper);
    }

    [Fact]
    public void Equality_TreatsEquivalentValuesAsTheSameDictionaryKey()
    {
        var map = new Dictionary<BundleId, int>
        {
            [BundleId.Create("case-4711")] = 1,
        };

        Assert.True(map.ContainsKey(BundleId.Create(" case-4711 ")));
    }

    [Fact]
    public void Default_IsReportedAsDefaultAndDeniesItsValue()
    {
        BundleId bundleId = default;

        Assert.True(bundleId.IsDefault);
        Assert.Throws<InvalidOperationException>(() => bundleId.Value);
    }

    // ToString is reached from logging, where an exception would hide the failure being
    // reported rather than surface a bug.
    [Fact]
    public void ToString_NeverThrows()
    {
        BundleId bundleId = default;

        Assert.Equal(string.Empty, bundleId.ToString());
        Assert.Equal("case-4711", BundleId.Create("case-4711").ToString());
    }

    [Fact]
    public void GetHashCode_IsStableForEquivalentValues()
    {
        Assert.Equal(
            BundleId.Create("case-4711").GetHashCode(),
            BundleId.Create(" case-4711 ").GetHashCode());

        Assert.Equal(0, default(BundleId).GetHashCode());
    }
}
