using System;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Core.Tests;

public sealed class EntityTypeTests
{
    [Fact]
    public void Constructor_NormalizesToUpperCase()
    {
        var entityType = new EntityType("credit_card");

        Assert.Equal("CREDIT_CARD", entityType.Value);
    }

    [Fact]
    public void Constructor_TrimsSurroundingWhiteSpace()
    {
        var entityType = new EntityType("  claim_number\t");

        Assert.Equal("CLAIM_NUMBER", entityType.Value);
    }

    [Fact]
    public void Constructor_AcceptsADomainSpecificType()
    {
        var entityType = new EntityType("CLAIM_NUMBER");

        Assert.Equal("CLAIM_NUMBER", entityType.Value);
        Assert.NotEqual(EntityType.Person, entityType);
    }

    [Fact]
    public void Constructor_RejectsNull() => Assert.Throws<ArgumentNullException>(() => new EntityType(null!));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1_LEADING_DIGIT")]
    [InlineData("_LEADING_UNDERSCORE")]
    [InlineData("WITH SPACE")]
    [InlineData("WITH-HYPHEN")]
    [InlineData("WITH.DOT")]
    [InlineData("ACCENTED_É")]
    public void Constructor_RejectsAValueThatIsNotAnIdentifier(string value) =>
        Assert.Throws<ArgumentException>(() => new EntityType(value));

    [Fact]
    public void Constructor_RejectsAValueLongerThanTheMaximum()
    {
        var value = new string('A', EntityType.MaxLength + 1);

        Assert.Throws<ArgumentException>(() => new EntityType(value));
    }

    [Fact]
    public void Constructor_AcceptsAValueOfExactlyTheMaximumLength()
    {
        var value = new string('A', EntityType.MaxLength);

        Assert.Equal(value, new EntityType(value).Value);
    }

    [Fact]
    public void TryCreate_ReturnsFalseForAnInvalidValue()
    {
        Assert.False(EntityType.TryCreate("not valid", out var entityType));
        Assert.True(entityType.IsDefault);
    }

    [Fact]
    public void TryCreate_ReturnsTheNormalizedValue()
    {
        Assert.True(EntityType.TryCreate("iban", out var entityType));
        Assert.Equal(EntityType.Iban, entityType);
    }

    [Fact]
    public void Equals_IgnoresTheCaseItWasWrittenIn()
    {
        var lower = new EntityType("api_key");
        var upper = new EntityType("API_KEY");

        Assert.True(lower == upper);
        Assert.Equal(lower.GetHashCode(), upper.GetHashCode());
        Assert.Equal(EntityType.ApiKey, lower);
    }

    [Fact]
    public void Value_ThrowsForTheDefaultValue()
    {
        var entityType = default(EntityType);

        Assert.True(entityType.IsDefault);
        Assert.Throws<InvalidOperationException>(() => entityType.Value);
    }

    [Fact]
    public void ToString_ReturnsAnEmptyStringForTheDefaultValue() =>
        Assert.Equal(string.Empty, default(EntityType).ToString());

    [Fact]
    public void WellKnownTypes_UseTheDocumentedNames()
    {
        Assert.Equal("PERSON", EntityType.Person.Value);
        Assert.Equal("EMAIL", EntityType.Email.Value);
        Assert.Equal("IBAN", EntityType.Iban.Value);
        Assert.Equal("API_KEY", EntityType.ApiKey.Value);
        Assert.Equal("TRADE_SECRET", EntityType.TradeSecret.Value);
    }
}
