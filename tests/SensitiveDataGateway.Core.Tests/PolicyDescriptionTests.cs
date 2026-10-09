using System;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Policies;

namespace SensitiveDataGateway.Core.Tests;

public sealed class PolicyDescriptionTests
{
    [Fact]
    public void ToPolicy_RebuildsAShippedPolicyExactly()
    {
        var rebuilt = SanitizationPolicy.Strict.ToDescription().ToPolicy();

        AssertEquivalent(SanitizationPolicy.Strict, rebuilt);
    }

    [Fact]
    public void ToPolicy_RebuildsTheBalancedPolicyExactly()
    {
        var rebuilt = SanitizationPolicy.Balanced.ToDescription().ToPolicy();

        AssertEquivalent(SanitizationPolicy.Balanced, rebuilt);
    }

    [Fact]
    public void ToPolicy_KeepsTheOrderOfTheRules()
    {
        var original = SanitizationPolicy.Balanced;
        var rebuilt = original.ToDescription().ToPolicy();

        for (var index = 0; index < original.Rules.Count; index++)
        {
            Assert.Equal(original.Rules[index].Match, rebuilt.Rules[index].Match);
            Assert.Equal(original.Rules[index].Action, rebuilt.Rules[index].Action);
            Assert.Equal(original.Rules[index].Scope, rebuilt.Rules[index].Scope);
            Assert.Equal(original.Rules[index].Sensitivity, rebuilt.Rules[index].Sensitivity);
        }
    }

    [Fact]
    public void ToPolicy_LeavesAnEmptyDescriptionBlockingEverything()
    {
        var policy = Minimal().ToPolicy();

        Assert.Equal(TransformationAction.Block, policy.DefaultAction);
        Assert.Equal(TransformationAction.Block, policy.UnknownEntityAction);
        Assert.Null(policy.UnresolvedTolerance);
        Assert.Empty(policy.Rules);
    }

    [Fact]
    public void ToPolicy_ReadsNoneAsToleratingNothing()
    {
        var description = Minimal();
        description.UnresolvedTolerance = SanitizationPolicyDescription.NoTolerance;

        Assert.Null(description.ToPolicy().UnresolvedTolerance);
    }

    [Fact]
    public void ToPolicy_ReadsATolerance()
    {
        var description = Minimal();
        description.UnresolvedTolerance = "moderate";

        Assert.Equal(SensitivityLevel.Moderate, description.ToPolicy().UnresolvedTolerance);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u0001")]
    public void ToPolicy_RefusesAMalformedIdentifier(string id)
    {
        var description = Minimal();
        description.Id = id;

        AssertRefused(description, "policy.id_invalid");
    }

    [Fact]
    public void ToPolicy_RefusesAMissingIdentifier()
    {
        var description = new SanitizationPolicyDescription { Version = "1.0" };

        AssertRefused(description, "policy.id_invalid");
    }

    [Fact]
    public void ToPolicy_RefusesAToleranceTheLibraryDoesNotDefine()
    {
        var description = Minimal();
        description.UnresolvedTolerance = "whatever";

        AssertRefused(description, "policy.tolerance_invalid");
    }

    [Fact]
    public void ToPolicy_RefusesARuleWithoutAnAction()
    {
        var description = Minimal();
        description.Rules.Add(new PolicyRuleDescription { Match = "PERSON" });

        AssertRefused(description, "policy.rule_action_invalid");
    }

    [Fact]
    public void ToPolicy_RefusesARuleWithoutAType()
    {
        var description = Minimal();
        description.Rules.Add(new PolicyRuleDescription { Action = TransformationAction.Redact });

        AssertRefused(description, "policy.rule_type_invalid");
    }

    [Fact]
    public void ToPolicy_RefusesAScopeOnAnActionThatProducesNoCorrelatableReplacement()
    {
        var description = Minimal();
        description.Rules.Add(new PolicyRuleDescription
        {
            Match = "PERSON",
            Action = TransformationAction.Redact,
            Scope = PseudonymScope.Bundle,
        });

        AssertRefused(description, "policy.rule_scope_without_correlation");
    }

    [Fact]
    public void ToPolicy_RefusesAThresholdOutsideTheUnitInterval()
    {
        var description = Minimal();
        description.DefaultThresholds = new ConfidenceThresholdsDescription { Transform = 1.5 };

        AssertRefused(description, "policy.threshold_out_of_range");
    }

    [Fact]
    public void ToPolicy_RefusesAFlaggingThresholdAboveTheTransformationThreshold()
    {
        var description = Minimal();
        description.DefaultThresholds = new ConfidenceThresholdsDescription { Transform = 0.5, Flag = 0.9 };

        AssertRefused(description, "policy.flag_threshold_above_transform_threshold");
    }

    [Fact]
    public void ToPolicy_RefusesARuleThatAnEarlierRuleMakesUnreachable()
    {
        var description = Minimal();
        description.Rules.Add(new PolicyRuleDescription { Match = "PERSON", Action = TransformationAction.Redact });
        description.Rules.Add(new PolicyRuleDescription { Match = "PERSON", Action = TransformationAction.Block });

        AssertRefused(description, "policy.unreachable_rule");
    }

    [Fact]
    public void ToPolicy_RefusesALimitThatIsNotPositive()
    {
        var description = Minimal();
        description.Limits = new ProcessingLimitsDescription { MaxEntities = 0 };

        AssertRefused(description, "policy.limit_not_positive");
    }

    [Fact]
    public void ToPolicy_RefusesAPartLimitAboveTheBundleLimit()
    {
        var description = Minimal();
        description.Limits = new ProcessingLimitsDescription { MaxBundleBytes = 1_000, MaxPartBytes = 2_000 };

        AssertRefused(description, "policy.part_limit_exceeds_bundle_limit");
    }

    [Fact]
    public void ToLimits_FallsBackToTheShippedBoundsMemberByMember()
    {
        var limits = new ProcessingLimitsDescription { MaxEntities = 7 }.ToLimits();

        Assert.Equal(7, limits.MaxEntities);
        Assert.Equal(ProcessingLimits.Default.MaxBundleBytes, limits.MaxBundleBytes);
        Assert.Equal(ProcessingLimits.Default.MaxProcessingTime, limits.MaxProcessingTime);
    }

    private static void AssertRefused(SanitizationPolicyDescription description, string code)
    {
        var exception = Assert.Throws<PolicyConfigurationException>(description.ToPolicy);

        Assert.Equal(GatewayErrorCategory.ConfigurationInvalid, exception.Error.Category);
        Assert.Equal(code, exception.Error.Code);
    }

    private static void AssertEquivalent(SanitizationPolicy expected, SanitizationPolicy actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.DefaultAction, actual.DefaultAction);
        Assert.Equal(expected.UnknownEntityAction, actual.UnknownEntityAction);
        Assert.Equal(expected.DefaultScope, actual.DefaultScope);
        Assert.Equal(expected.UnresolvedTolerance, actual.UnresolvedTolerance);
        Assert.Equal(expected.Rules.Count, actual.Rules.Count);
        Assert.Equal(expected.Thresholds.Count, actual.Thresholds.Count);
        Assert.Equal(expected.DefaultThresholds.Transform, actual.DefaultThresholds.Transform);
        Assert.Equal(expected.DefaultThresholds.Flag, actual.DefaultThresholds.Flag);
        Assert.Equal(expected.Limits.MaxEntities, actual.Limits.MaxEntities);
        Assert.Equal(expected.Limits.MaxProcessingTime, actual.Limits.MaxProcessingTime);

        foreach (var type in EntityType.WellKnown)
        {
            Assert.Equal(expected.ThresholdsFor(type).Transform, actual.ThresholdsFor(type).Transform);
        }
    }

    private static SanitizationPolicyDescription Minimal() =>
        new() { Id = "from-configuration", Version = "1.0" };
}

public sealed class TransformationPlanTests
{
    [Fact]
    public void Create_RejectsACandidateThatIsBothDecidedAndUnresolved()
    {
        var entity = PolicyTestData.Entity(EntityType.Person);

        Assert.Throws<ArgumentException>(() => TransformationPlan.Create(
            BundleId.New(),
            PolicyId.Create("test"),
            PolicyVersion.Create("1.0"),
            [Decision(entity)],
            [entity],
            []));
    }

    [Fact]
    public void Create_RejectsTwoDecisionsAboutTheSameCandidate()
    {
        var entity = PolicyTestData.Entity(EntityType.Person);

        Assert.Throws<ArgumentException>(() => TransformationPlan.Create(
            BundleId.New(),
            PolicyId.Create("test"),
            PolicyVersion.Create("1.0"),
            [Decision(entity), Decision(entity)],
            [],
            []));
    }

    [Fact]
    public void IsBlocked_FollowsTheBlockingReasons()
    {
        var plan = TransformationPlan.Create(
            BundleId.New(),
            PolicyId.Create("test"),
            PolicyVersion.Create("1.0"),
            [],
            [],
            [PolicyViolation.IncompleteScan()]);

        Assert.True(plan.IsBlocked);
    }

    [Fact]
    public void ToString_CountsTheDecisionsAndQuotesNothing()
    {
        var plan = TransformationPlan.Create(
            BundleId.New(),
            PolicyId.Create("test"),
            PolicyVersion.Create("1.0"),
            [],
            [],
            []);

        Assert.Equal("test 1.0: 0 decisions, 0 unresolved, 0 blocking", plan.ToString());
    }

    private static PolicyDecision Decision(SensitiveEntity entity) => PolicyDecision.Create(
        entity.Id,
        entity.Type,
        TransformationAction.Redact,
        scope: null,
        PolicyRationale.FromDefaultAction(
            ConfidenceOutcome.Transform,
            ConfidenceThresholds.Default,
            entity.Confidence));
}
