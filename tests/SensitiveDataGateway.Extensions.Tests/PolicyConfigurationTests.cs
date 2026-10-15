using System;
using System.Linq;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Policies;
using Xunit;

namespace SensitiveDataGateway.Extensions.Tests;

/// <summary>
/// What a policy written in configuration turns into.
/// </summary>
/// <remarks>
/// The reader is written by hand, which buys trimmability and costs the guarantee that a new
/// property on a description is picked up for free. These tests are that guarantee: every key
/// the schema documents is read here, and a key the reader forgets fails a test rather than
/// silently falling back to a default.
/// </remarks>
public sealed class PolicyConfigurationTests
{
    [Fact]
    public void ReadPolicy_ReadsTheIdentityOfThePolicy()
    {
        var policy = PolicyConfiguration.ReadPolicy(Harness.MinimalSection());

        Assert.Equal("test.policy", policy.Id.Value);
        Assert.Equal("1", policy.Version.Value);
    }

    [Fact]
    public void ReadPolicy_BlocksByDefault()
    {
        var policy = PolicyConfiguration.ReadPolicy(Harness.MinimalSection());

        Assert.Equal(TransformationAction.Block, policy.DefaultAction);
        Assert.Equal(TransformationAction.Block, policy.UnknownEntityAction);
        Assert.Null(policy.UnresolvedTolerance);
    }

    [Fact]
    public void ReadPolicy_ReadsTheStatedActionsAndScope()
    {
        var policy = PolicyConfiguration.ReadPolicy(Harness.MinimalSection(
            ("DefaultAction", "Pseudonymize"),
            ("UnknownEntityAction", "Redact"),
            ("DefaultScope", "Tenant"),
            ("UnresolvedTolerance", "Low")));

        Assert.Equal(TransformationAction.Pseudonymize, policy.DefaultAction);
        Assert.Equal(TransformationAction.Redact, policy.UnknownEntityAction);
        Assert.Equal(PseudonymScope.Tenant, policy.DefaultScope);
        Assert.Equal(SensitivityLevel.Low, policy.UnresolvedTolerance);
    }

    [Fact]
    public void ReadPolicy_ReadsEnumsWithoutRegardToCase()
    {
        var policy = PolicyConfiguration.ReadPolicy(Harness.MinimalSection(("DefaultAction", "redact")));

        Assert.Equal(TransformationAction.Redact, policy.DefaultAction);
    }

    [Fact]
    public void ReadPolicy_KeepsRulesInTheOrderTheyAreWritten()
    {
        var policy = PolicyConfiguration.ReadPolicy(Harness.MinimalSection(
            ("Rules:0:Match", "IBAN"),
            ("Rules:0:Action", "Pseudonymize"),
            ("Rules:0:Scope", "Bundle"),
            ("Rules:1:Match", "EMAIL"),
            ("Rules:1:Action", "Redact"),
            ("Rules:1:Sensitivity", "High")));

        Assert.Equal(["IBAN", "EMAIL"], policy.Rules.Select(rule => rule.Match.Value));
        Assert.Equal(TransformationAction.Pseudonymize, policy.Rules[0].Action);
        Assert.Equal(PseudonymScope.Bundle, policy.Rules[0].Scope);
        Assert.Equal(SensitivityLevel.High, policy.Rules[1].Sensitivity);
    }

    [Fact]
    public void ReadPolicy_ReadsThresholdsStatedPerEntityType()
    {
        var policy = PolicyConfiguration.ReadPolicy(Harness.MinimalSection(
            ("DefaultThresholds:Transform", "0.8"),
            ("DefaultThresholds:Flag", "0.4"),
            ("Thresholds:IBAN:Transform", "0.95"),
            ("Thresholds:IBAN:Flag", "0.9")));

        Assert.Equal(0.8, policy.DefaultThresholds.Transform.Value);
        Assert.Equal(0.4, policy.DefaultThresholds.Flag.Value);
        Assert.Equal(0.95, policy.ThresholdsFor(EntityType.Iban).Transform.Value);
    }

    [Fact]
    public void ReadPolicy_ReadsDecimalsTheSameWhateverTheMachineCallsAPoint()
    {
        var policy = PolicyConfiguration.ReadPolicy(Harness.MinimalSection(("DefaultThresholds:Transform", "0.75")));

        Assert.Equal(0.75, policy.DefaultThresholds.Transform.Value);
    }

    [Fact]
    public void ReadPolicy_ReadsTheLimitsThatAreStatedAndLeavesTheRest()
    {
        var policy = PolicyConfiguration.ReadPolicy(Harness.MinimalSection(
            ("Limits:MaxPartCount", "3"),
            ("Limits:MaxProcessingTime", "00:00:07")));

        Assert.Equal(3, policy.Limits.MaxPartCount);
        Assert.Equal(TimeSpan.FromSeconds(7), policy.Limits.MaxProcessingTime);
        Assert.Equal(ProcessingLimits.Default.MaxEntities, policy.Limits.MaxEntities);
    }

    [Fact]
    public void ReadPolicy_LeavesTheShippedLimitsAloneWhenNoneAreStated()
    {
        var policy = PolicyConfiguration.ReadPolicy(Harness.MinimalSection());

        Assert.Equal(ProcessingLimits.Default.MaxBundleBytes, policy.Limits.MaxBundleBytes);
    }

    [Fact]
    public void ReadPolicy_TreatsABlankValueAsAbsent()
    {
        var policy = PolicyConfiguration.ReadPolicy(Harness.MinimalSection(("DefaultAction", "   ")));

        Assert.Equal(TransformationAction.Block, policy.DefaultAction);
    }

    [Theory]
    [InlineData("DefaultAction", "Obliterate")]
    [InlineData("DefaultScope", "Galaxy")]
    [InlineData("DefaultThresholds:Transform", "almost")]
    [InlineData("Limits:MaxPartCount", "many")]
    [InlineData("Limits:MaxBundleBytes", "9.5")]
    [InlineData("Limits:MaxProcessingTime", "soon")]
    public void ReadPolicy_RefusesAValueThatIsNotOfTheStatedType(string key, string value)
    {
        var failure = Assert.Throws<PolicyConfigurationException>(
            () => PolicyConfiguration.ReadPolicy(Harness.MinimalSection((key, value))));

        Assert.Equal(GatewayErrorCategory.ConfigurationInvalid, failure.Error.Category);
        Assert.Equal("policy.configuration_value_malformed", failure.Error.Code);
    }

    [Fact]
    public void ReadPolicy_NamesTheKeyAndNotTheValueItRefused()
    {
        var failure = Assert.Throws<PolicyConfigurationException>(
            () => PolicyConfiguration.ReadPolicy(Harness.MinimalSection(("DefaultAction", "Erase-Mario-Rossi"))));

        Assert.Contains("DefaultAction", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Mario", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadPolicy_RefusesAPolicyThatCarriesNoIdentity()
    {
        var failure = Assert.Throws<PolicyConfigurationException>(
            () => PolicyConfiguration.ReadPolicy(Harness.Section(("Version", "1"))));

        Assert.Equal("policy.id_invalid", failure.Error.Code);
    }

    [Fact]
    public void ReadDescription_DoesNotBuildThePolicy()
    {
        var description = PolicyConfiguration.ReadDescription(Harness.Section(("Version", "1")));

        Assert.Null(description.Id);
        Assert.Equal("1", description.Version);
    }

    [Fact]
    public void ReadPolicy_RefusesNothingToReadFrom()
    {
        Assert.Throws<ArgumentNullException>(() => PolicyConfiguration.ReadPolicy(null!));
        Assert.Throws<ArgumentNullException>(() => PolicyConfiguration.ReadDescription(null!));
    }
}
