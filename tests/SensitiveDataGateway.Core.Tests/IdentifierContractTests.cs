using System;
using System.Linq;
using System.Reflection;

namespace SensitiveDataGateway.Core.Tests;

/// <summary>
/// The behaviour every identifier value shares, asserted once over every identifier type so
/// that a new identifier cannot be added with a weaker contract than the others.
/// </summary>
public sealed class IdentifierContractTests
{
    public static TheoryData<Type> IdentifierTypes =>
    [
        typeof(BundleId),
        typeof(PartId),
        typeof(SegmentId),
        typeof(EntityId),
        typeof(PolicyId),
        typeof(PolicyVersion),
        typeof(DetectorId),
        typeof(ExtractorId),
    ];

    [Theory]
    [MemberData(nameof(IdentifierTypes))]
    public void Identifier_IsAReadOnlyValue(Type type)
    {
        Assert.True(type.IsValueType);
        Assert.True(type.IsSealed);
        Assert.Contains(
            type.GetCustomAttributes(inherit: false),
            attribute => attribute.GetType().Name == "IsReadOnlyAttribute");
    }

    [Theory]
    [MemberData(nameof(IdentifierTypes))]
    public void Identifier_HasNoPublicConstructor(Type type) =>
        Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));

    [Theory]
    [MemberData(nameof(IdentifierTypes))]
    public void Create_TrimsSurroundingWhiteSpace(Type type) =>
        Assert.Equal("value", Value(Create(type, "  value  ")));

    [Theory]
    [MemberData(nameof(IdentifierTypes))]
    public void Create_KeepsTheCaseItWasGiven(Type type) =>
        Assert.Equal("MixedCase", Value(Create(type, "MixedCase")));

    [Theory]
    [MemberData(nameof(IdentifierTypes))]
    public void Create_RejectsNull(Type type) =>
        Assert.IsType<ArgumentNullException>(CreateFailure(type, null));

    [Theory]
    [MemberData(nameof(IdentifierTypes))]
    public void Create_RejectsEmptyText(Type type) =>
        Assert.IsType<ArgumentException>(CreateFailure(type, string.Empty));

    [Theory]
    [MemberData(nameof(IdentifierTypes))]
    public void Create_RejectsWhiteSpace(Type type) =>
        Assert.IsType<ArgumentException>(CreateFailure(type, "   "));

    [Theory]
    [MemberData(nameof(IdentifierTypes))]
    public void Create_RejectsAControlCharacter(Type type) =>
        Assert.IsType<ArgumentException>(CreateFailure(type, "a\tb"));

    [Theory]
    [MemberData(nameof(IdentifierTypes))]
    public void Create_RejectsTextLongerThanTheMaximum(Type type)
    {
        var maxLength = (int)type.GetField("MaxLength", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

        Assert.IsType<ArgumentException>(CreateFailure(type, new string('a', maxLength + 1)));
    }

    [Theory]
    [MemberData(nameof(IdentifierTypes))]
    public void Create_NeverQuotesTheRejectedValue(Type type)
    {
        var exception = CreateFailure(type, "secret\tvalue");

        Assert.DoesNotContain("secret", exception!.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(IdentifierTypes))]
    public void TryCreate_ReportsFailureWithoutThrowing(Type type)
    {
        var arguments = new object?[] { "  ", null };

        Assert.False((bool)Invoke(type, "TryCreate", arguments)!);
        Assert.True(IsDefault(arguments[1]!));
    }

    [Theory]
    [MemberData(nameof(IdentifierTypes))]
    public void Default_CarriesNoValue(Type type)
    {
        var identifier = Activator.CreateInstance(type)!;

        Assert.True(IsDefault(identifier));
        Assert.Equal(string.Empty, identifier.ToString());
        Assert.Throws<InvalidOperationException>(() => Value(identifier));
    }

    [Theory]
    [MemberData(nameof(IdentifierTypes))]
    public void Equality_IsOrdinalAndCaseSensitive(Type type)
    {
        var lower = Create(type, "value");
        var same = Create(type, "value");
        var upper = Create(type, "VALUE");

        Assert.Equal(lower, same);
        Assert.Equal(lower.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(lower, upper);
        Assert.True((bool)Invoke(type, "op_Equality", [lower, same])!);
        Assert.True((bool)Invoke(type, "op_Inequality", [lower, upper])!);
    }

    [Theory]
    [MemberData(nameof(IdentifierTypes))]
    public void Equality_IsFalseAgainstAnotherType(Type type) =>
        Assert.False(Create(type, "value").Equals("value"));

    [Theory]
    [MemberData(nameof(IdentifierTypes))]
    public void ToString_ReturnsTheValue(Type type) =>
        Assert.Equal("value", Create(type, "value").ToString());

    [Theory]
    [InlineData(typeof(BundleId))]
    [InlineData(typeof(PartId))]
    [InlineData(typeof(SegmentId))]
    [InlineData(typeof(EntityId))]
    public void New_ProducesAValueThatCarriesNoInformation(Type type)
    {
        var first = Invoke(type, "New", [])!;
        var second = Invoke(type, "New", [])!;

        Assert.NotEqual(first, second);
        Assert.False(IsDefault(first));
        Assert.Matches("^[0-9a-f]{32}$", first.ToString());
    }

    private static object Create(Type type, string value) => Invoke(type, "Create", [value])!;

    private static Exception? CreateFailure(Type type, string? value) =>
        Record.Exception(() => Invoke(type, "Create", [value]));

    private static string Value(object identifier)
    {
        try
        {
            return (string)identifier.GetType().GetProperty("Value")!.GetValue(identifier)!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static bool IsDefault(object identifier) =>
        (bool)identifier.GetType().GetProperty("IsDefault")!.GetValue(identifier)!;

    private static object? Invoke(Type type, string name, object?[] arguments)
    {
        var method = type
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(candidate => candidate.Name == name && candidate.GetParameters().Length == arguments.Length);

        try
        {
            return method.Invoke(null, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }
}
