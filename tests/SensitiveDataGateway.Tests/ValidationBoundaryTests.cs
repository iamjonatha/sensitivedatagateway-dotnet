using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Tests;

/// <summary>
/// Holds the shape of the egress boundary in place.
/// </summary>
/// <remarks>
/// These tests fail when somebody adds a convenience that would let a caller produce a
/// validated payload without passing validation, or turn a check off. Both are easy to add by
/// accident and impossible to notice in a diff that otherwise looks reasonable.
/// </remarks>
public sealed class ValidationBoundaryTests
{
    [Theory]
    [InlineData(typeof(ValidatedPayload))]
    [InlineData(typeof(SanitizationResult))]
    [InlineData(typeof(PrivacyValidationReport))]
    public void TheResultTypes_CannotBeBuiltByACaller(Type type)
    {
        Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(PublicFactories(type));
    }

    [Fact]
    public void ValidationOptions_OffersNoWayToTurnACheckOff()
    {
        var members = typeof(ValidationOptions)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(member => member.Name);

        Assert.DoesNotContain(members, name =>
            name.Contains("Skip", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Disable", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Enable", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Ignore", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Bypass", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidationOptions_CarriesNoSwitch()
    {
        var flags = typeof(ValidationOptions)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(property => property.PropertyType == typeof(bool));

        Assert.Empty(flags);
    }

    [Fact]
    public void ValidationOptions_RefusesAThresholdThatWouldStopTheScanFromLooking()
    {
        var options = ValidationOptions.Default;

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => options.WithMinimumFragmentLength(1));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => options.WithMinimumEntropyLength(2));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => options.WithEntropyThreshold(0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => options.WithEntropyThreshold(7));
    }

    [Fact]
    public void ValidationOptions_KeepsEveryChangeToItself()
    {
        var options = ValidationOptions.Default;
        var stricter = options.WithMinimumFragmentLength(8);

        Assert.NotSame(options, stricter);
        Assert.Equal(5, options.MinimumFragmentLength);
        Assert.Equal(8, stricter.MinimumFragmentLength);
    }

    private static IEnumerable<MethodInfo> PublicFactories(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(method => type.IsAssignableFrom(method.ReturnType));
}
