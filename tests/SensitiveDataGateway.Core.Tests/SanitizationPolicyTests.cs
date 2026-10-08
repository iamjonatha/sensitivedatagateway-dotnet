using System;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Policies;

namespace SensitiveDataGateway.Core.Tests;

public sealed class SanitizationPolicyBuilderTests
{
    [Fact]
    public void CreateBuilder_RejectsADefaultIdentifier() =>
        Assert.Throws<ArgumentException>(() =>
            SanitizationPolicy.CreateBuilder(default, PolicyVersion.Create("1.0")));

    [Fact]
    public void CreateBuilder_RejectsADefaultVersion() =>
        Assert.Throws<ArgumentException>(() =>
            SanitizationPolicy.CreateBuilder(PolicyId.Create("p"), default));

    [Fact]
    public void Build_LeavesEveryUnstatedDefaultClosed()
    {
        var policy = Builder().Build();

        Assert.Equal(TransformationAction.Block, policy.DefaultAction);
        Assert.Equal(TransformationAction.Block, policy.UnknownEntityAction);
        Assert.Null(policy.UnresolvedTolerance);
        Assert.True(policy.RequiresCompleteScan);
        Assert.Same(ConfidenceThresholds.Default, policy.DefaultThresholds);
        Assert.Same(ProcessingLimits.Default, policy.Limits);
        Assert.Equal(PseudonymScope.Bundle, policy.DefaultScope);
    }

    [Fact]
    public void Build_RefusesARuleAnEarlierRuleMakesUnreachable()
    {
        var builder = Builder()
            .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Redact))
            .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Block)
                .WhenSensitivityIs(SensitivityLevel.Critical));

        var exception = Assert.Throws<PolicyConfigurationException>(builder.Build);

        Assert.Equal(GatewayErrorCategory.ConfigurationInvalid, exception.Error.Category);
        Assert.Equal("policy.unreachable_rule", exception.Error.Code);
    }

    [Fact]
    public void Build_RefusesARuleThatRepeatsAnEarlierOne()
    {
        var builder = Builder()
            .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Redact))
            .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Mask));

        Assert.Throws<PolicyConfigurationException>(builder.Build);
    }

    [Fact]
    public void Build_KeepsASpecificRuleBeforeAGeneralOne()
    {
        var policy = Builder()
            .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Block)
                .WhenSensitivityIs(SensitivityLevel.Critical))
            .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Redact))
            .Build();

        Assert.Equal(2, policy.Rules.Count);
    }

    [Fact]
    public void Build_CopiesTheRulesSoLaterChangesDoNotReachThePolicy()
    {
        var builder = Builder().AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Redact));
        var policy = builder.Build();

        builder.AddRule(PolicyRule.For(EntityType.Email, TransformationAction.Redact));

        Assert.Single(policy.Rules);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(0)]
    public void WithDefaultAction_RejectsAnActionTheLibraryDoesNotDefine(int action) =>
        Assert.Throws<ArgumentException>(() => Builder().WithDefaultAction((TransformationAction)action));

    [Fact]
    public void TolerateNoUnresolved_UndoesAnEarlierTolerance()
    {
        var policy = Builder()
            .TolerateUnresolvedUpTo(SensitivityLevel.High)
            .TolerateNoUnresolved()
            .Build();

        Assert.Null(policy.UnresolvedTolerance);
    }

    private static SanitizationPolicyBuilder Builder() =>
        SanitizationPolicy.CreateBuilder(PolicyId.Create("test"), PolicyVersion.Create("1.0"));
}

public sealed class SanitizationPolicyTests
{
    [Fact]
    public void Mentions_IsTrueForATypeARuleGoverns()
    {
        var policy = Builder()
            .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Redact))
            .Build();

        Assert.True(policy.Mentions(EntityType.Person));
        Assert.False(policy.Mentions(EntityType.Email));
    }

    [Fact]
    public void Mentions_IsTrueForATypeWithThresholdsAndNoRule()
    {
        var policy = Builder()
            .WithThresholds(EntityType.Email, ConfidenceThresholds.Create(0.5, 0.4))
            .Build();

        Assert.True(policy.Mentions(EntityType.Email));
    }

    [Fact]
    public void ThresholdsFor_FallsBackToTheDefault()
    {
        var stated = ConfidenceThresholds.Create(0.5, 0.4);

        var policy = Builder().WithThresholds(EntityType.Email, stated).Build();

        Assert.Same(stated, policy.ThresholdsFor(EntityType.Email));
        Assert.Same(policy.DefaultThresholds, policy.ThresholdsFor(EntityType.Person));
    }

    [Fact]
    public void TryFindRule_ReturnsTheFirstRuleThatMatches()
    {
        var policy = Builder()
            .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Block)
                .WhenSensitivityIs(SensitivityLevel.Critical))
            .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Redact))
            .Build();

        Assert.True(policy.TryFindRule(PolicyTestData.Entity(EntityType.Person, SensitivityLevel.Critical), out var critical));
        Assert.Equal(0, critical);

        Assert.True(policy.TryFindRule(PolicyTestData.Entity(EntityType.Person, SensitivityLevel.Low), out var low));
        Assert.Equal(1, low);
    }

    [Fact]
    public void TryFindRule_ReportsNoRuleForAnUnmentionedType()
    {
        var policy = Builder().Build();

        Assert.False(policy.TryFindRule(PolicyTestData.Entity(EntityType.Person), out var index));
        Assert.Equal(-1, index);
    }

    [Theory]
    [InlineData(SensitivityLevel.Low, true)]
    [InlineData(SensitivityLevel.Moderate, true)]
    [InlineData(SensitivityLevel.High, false)]
    [InlineData(SensitivityLevel.Critical, false)]
    public void ToleratesUnresolved_StopsAtTheStatedLevel(SensitivityLevel sensitivity, bool expected)
    {
        var policy = Builder().TolerateUnresolvedUpTo(SensitivityLevel.Moderate).Build();

        Assert.Equal(expected, policy.ToleratesUnresolved(sensitivity));
    }

    [Theory]
    [InlineData(SensitivityLevel.Low)]
    [InlineData(SensitivityLevel.Critical)]
    public void ToleratesUnresolved_IsFalseAtEveryLevelWhenNothingIsTolerated(SensitivityLevel sensitivity) =>
        Assert.False(Builder().Build().ToleratesUnresolved(sensitivity));

    [Fact]
    public void ToString_NamesThePolicyAndNothingFromADocument() =>
        Assert.Equal("test 1.0", Builder().Build().ToString());

    private static SanitizationPolicyBuilder Builder() =>
        SanitizationPolicy.CreateBuilder(PolicyId.Create("test"), PolicyVersion.Create("1.0"));
}

public sealed class ShippedPolicyTests
{
    public static TheoryData<string> ShippedPolicyNames => new("strict", "balanced");

    [Theory]
    [MemberData(nameof(ShippedPolicyNames))]
    public void EveryShippedPolicyBlocksATypeItDoesNotMention(string name)
    {
        var policy = Shipped(name);

        Assert.Equal(TransformationAction.Block, policy.UnknownEntityAction);
        Assert.False(policy.Mentions(EntityType.Create("SOMETHING_NOBODY_HAS_HEARD_OF")));
    }

    [Theory]
    [MemberData(nameof(ShippedPolicyNames))]
    public void EveryShippedPolicyGovernsEveryWellKnownType(string name)
    {
        var policy = Shipped(name);

        Assert.All(EntityType.WellKnown, type => Assert.True(policy.Mentions(type), type.Value));
    }

    [Theory]
    [MemberData(nameof(ShippedPolicyNames))]
    public void EveryShippedPolicyBlocksACredential(string name)
    {
        var policy = Shipped(name);

        Assert.True(policy.TryFindRule(PolicyTestData.Entity(EntityType.ApiKey), out var index));
        Assert.Equal(TransformationAction.Block, policy.Rules[index].Action);
    }

    [Fact]
    public void Strict_RemovesWhatItDoesNotNeedToCorrelate()
    {
        Assert.Equal("strict", SanitizationPolicy.Strict.Id.Value);
        Assert.Equal(TransformationAction.Redact, SanitizationPolicy.Strict.DefaultAction);
        Assert.True(SanitizationPolicy.Strict.RequiresCompleteScan);
        Assert.Equal(SensitivityLevel.Low, SanitizationPolicy.Strict.UnresolvedTolerance);
        Assert.Equal(TransformationAction.Redact, ActionFor(SanitizationPolicy.Strict, EntityType.Iban));
    }

    [Fact]
    public void Balanced_KeepsTheDocumentUsable()
    {
        Assert.Equal("balanced", SanitizationPolicy.Balanced.Id.Value);
        Assert.Equal(SensitivityLevel.Moderate, SanitizationPolicy.Balanced.UnresolvedTolerance);
        Assert.Equal(TransformationAction.Pseudonymize, ActionFor(SanitizationPolicy.Balanced, EntityType.Person));
        Assert.Equal(TransformationAction.Hash, ActionFor(SanitizationPolicy.Balanced, EntityType.TaxId));
        Assert.Equal(TransformationAction.Mask, ActionFor(SanitizationPolicy.Balanced, EntityType.Iban));
    }

    [Fact]
    public void Strict_IsAtLeastAsClosedAsBalancedOnCredentials()
    {
        var strict = SanitizationPolicy.Strict.ThresholdsFor(EntityType.Password);

        Assert.True(strict.Transform <= SanitizationPolicy.Strict.DefaultThresholds.Transform);
    }

    private static TransformationAction ActionFor(SanitizationPolicy policy, EntityType type)
    {
        Assert.True(policy.TryFindRule(PolicyTestData.Entity(type), out var index));

        return policy.Rules[index].Action;
    }

    private static SanitizationPolicy Shipped(string name) =>
        string.Equals(name, "strict", StringComparison.Ordinal)
            ? SanitizationPolicy.Strict
            : SanitizationPolicy.Balanced;
}
