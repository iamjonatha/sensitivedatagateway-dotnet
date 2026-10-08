using System;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Policies;

namespace SensitiveDataGateway.Core.Tests;

public sealed class ConfidenceThresholdsTests
{
    [Fact]
    public void Default_IsTheStrictStartingPoint()
    {
        Assert.Equal(0.90, ConfidenceThresholds.Default.Transform.Value);
        Assert.Equal(0.70, ConfidenceThresholds.Default.Flag.Value);
    }

    [Fact]
    public void Create_RejectsAFlaggingThresholdAboveTheTransformationThreshold() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ConfidenceThresholds.Create(0.60, 0.80));

    [Theory]
    [InlineData(1.00, ConfidenceOutcome.Transform)]
    [InlineData(0.90, ConfidenceOutcome.Transform)]
    [InlineData(0.89, ConfidenceOutcome.Flag)]
    [InlineData(0.70, ConfidenceOutcome.Flag)]
    [InlineData(0.69, ConfidenceOutcome.Unresolved)]
    [InlineData(0.00, ConfidenceOutcome.Unresolved)]
    public void Classify_TreatsEachThresholdAsInclusive(double score, ConfidenceOutcome expected) =>
        Assert.Equal(expected, ConfidenceThresholds.Default.Classify(DetectionConfidence.Create(score)));

    [Fact]
    public void ToString_QuotesNothingFromADocument() =>
        Assert.Equal("transform >= 0.9, flag >= 0.7", ConfidenceThresholds.Default.ToString());
}

public sealed class PolicyRuleTests
{
    [Fact]
    public void For_RejectsADefaultEntityType() =>
        Assert.Throws<ArgumentException>(() => PolicyRule.For(default, TransformationAction.Redact));

    [Fact]
    public void For_RejectsAnActionTheLibraryDoesNotDefine() =>
        Assert.Throws<ArgumentException>(() => PolicyRule.For(EntityType.Person, (TransformationAction)99));

    [Theory]
    [InlineData(TransformationAction.Pseudonymize)]
    [InlineData(TransformationAction.Hash)]
    public void WithScope_AcceptsAnActionThatProducesACorrelatableReplacement(TransformationAction action)
    {
        var rule = PolicyRule.For(EntityType.Person, action).WithScope(PseudonymScope.Tenant);

        Assert.Equal(PseudonymScope.Tenant, rule.Scope);
    }

    [Theory]
    [InlineData(TransformationAction.Redact)]
    [InlineData(TransformationAction.Mask)]
    [InlineData(TransformationAction.Block)]
    public void WithScope_RejectsAnActionThatProducesNoCorrelatableReplacement(TransformationAction action) =>
        Assert.Throws<ArgumentException>(() =>
            PolicyRule.For(EntityType.Person, action).WithScope(PseudonymScope.Bundle));

    [Fact]
    public void Matches_IgnoresSensitivityWhenTheRuleStatesNone()
    {
        var rule = PolicyRule.For(EntityType.Person, TransformationAction.Redact);

        Assert.True(rule.Matches(PolicyTestData.Entity(EntityType.Person, SensitivityLevel.Low)));
        Assert.True(rule.Matches(PolicyTestData.Entity(EntityType.Person, SensitivityLevel.Critical)));
    }

    [Fact]
    public void Matches_RequiresTheStatedSensitivity()
    {
        var rule = PolicyRule.For(EntityType.Person, TransformationAction.Block)
            .WhenSensitivityIs(SensitivityLevel.Critical);

        Assert.True(rule.Matches(PolicyTestData.Entity(EntityType.Person, SensitivityLevel.Critical)));
        Assert.False(rule.Matches(PolicyTestData.Entity(EntityType.Person, SensitivityLevel.Low)));
    }

    [Fact]
    public void Matches_RequiresTheSameType()
    {
        var rule = PolicyRule.For(EntityType.Person, TransformationAction.Redact);

        Assert.False(rule.Matches(PolicyTestData.Entity(EntityType.Email)));
    }

    [Fact]
    public void Shadows_IsTrueWhenAGeneralRuleComesBeforeASpecificOne()
    {
        var general = PolicyRule.For(EntityType.Person, TransformationAction.Redact);
        var specific = general.WhenSensitivityIs(SensitivityLevel.Critical);

        Assert.True(general.Shadows(specific));
        Assert.False(specific.Shadows(general));
    }

    [Fact]
    public void Shadows_IsFalseAcrossTypes()
    {
        var person = PolicyRule.For(EntityType.Person, TransformationAction.Redact);
        var email = PolicyRule.For(EntityType.Email, TransformationAction.Redact);

        Assert.False(person.Shadows(email));
    }
}

public sealed class PolicyRationaleTests
{
    [Fact]
    public void FromRule_RejectsANegativeIndex() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PolicyRationale.FromRule(
            -1,
            ConfidenceOutcome.Transform,
            ConfidenceThresholds.Default,
            DetectionConfidence.Certain));

    [Fact]
    public void FromRule_RecordsWhichRuleDecided()
    {
        var rationale = PolicyRationale.FromRule(
            3,
            ConfidenceOutcome.Flag,
            ConfidenceThresholds.Default,
            DetectionConfidence.Create(0.75));

        Assert.Equal(PolicyRationaleSource.Rule, rationale.Source);
        Assert.Equal(3, rationale.RuleIndex);
        Assert.Equal(ConfidenceOutcome.Flag, rationale.Outcome);
    }

    [Fact]
    public void FromDefaultAction_CarriesNoRuleIndex() =>
        Assert.Null(PolicyRationale.FromDefaultAction(
            ConfidenceOutcome.Transform,
            ConfidenceThresholds.Default,
            DetectionConfidence.Certain).RuleIndex);

    [Theory]
    [InlineData(PolicyRationaleSource.Rule)]
    [InlineData(PolicyRationaleSource.DefaultAction)]
    [InlineData(PolicyRationaleSource.UnknownEntityAction)]
    public void ToString_ExplainsEverySource(PolicyRationaleSource source)
    {
        var rationale = Rationale(source);

        Assert.False(string.IsNullOrWhiteSpace(rationale.ToString()));
    }

    private static PolicyRationale Rationale(PolicyRationaleSource source) => source switch
    {
        PolicyRationaleSource.Rule => PolicyRationale.FromRule(
            0,
            ConfidenceOutcome.Transform,
            ConfidenceThresholds.Default,
            DetectionConfidence.Certain),
        PolicyRationaleSource.DefaultAction => PolicyRationale.FromDefaultAction(
            ConfidenceOutcome.Transform,
            ConfidenceThresholds.Default,
            DetectionConfidence.Certain),
        _ => PolicyRationale.FromUnknownEntityAction(
            ConfidenceOutcome.Transform,
            ConfidenceThresholds.Default,
            DetectionConfidence.Certain),
    };
}

public sealed class PolicyViolationTests
{
    [Fact]
    public void ForEntity_RefusesToDescribeAnIncompleteScan() =>
        Assert.Throws<ArgumentException>(() => PolicyViolation.ForEntity(
            PolicyViolationReason.IncompleteScan,
            EntityId.New(),
            EntityType.Person,
            SensitivityLevel.High));

    [Fact]
    public void IncompleteScan_NamesNoCandidate()
    {
        var violation = PolicyViolation.IncompleteScan();

        Assert.Null(violation.Entity);
        Assert.Null(violation.Type);
        Assert.Null(violation.Sensitivity);
    }

    [Fact]
    public void ToError_IsCategorizedAsAPolicyViolation()
    {
        var entityId = EntityId.New();

        var error = PolicyViolation.ForEntity(
            PolicyViolationReason.BlockedEntity,
            entityId,
            EntityType.Password,
            SensitivityLevel.Critical).ToError();

        Assert.Equal(GatewayErrorCategory.PolicyViolation, error.Category);
        Assert.Equal("policy.blocked_entity", error.Code);
        Assert.Equal(entityId, error.EntityId);
    }
}

public sealed class PolicyDecisionTests
{
    [Fact]
    public void Create_RejectsAScopeForAnActionThatProducesNoCorrelatableReplacement() =>
        Assert.Throws<ArgumentException>(() => PolicyDecision.Create(
            EntityId.New(),
            EntityType.Person,
            TransformationAction.Redact,
            PseudonymScope.Bundle,
            Rationale()));

    [Fact]
    public void Create_RequiresAScopeForAnActionThatProducesOne() =>
        Assert.Throws<ArgumentException>(() => PolicyDecision.Create(
            EntityId.New(),
            EntityType.Person,
            TransformationAction.Pseudonymize,
            scope: null,
            Rationale()));

    [Fact]
    public void ToString_NamesTheTypeAndTheActionAndNothingElse()
    {
        var decision = PolicyDecision.Create(
            EntityId.New(),
            EntityType.Person,
            TransformationAction.Redact,
            scope: null,
            Rationale());

        Assert.StartsWith("PERSON -> Redact", decision.ToString(), StringComparison.Ordinal);
    }

    private static PolicyRationale Rationale() => PolicyRationale.FromDefaultAction(
        ConfidenceOutcome.Transform,
        ConfidenceThresholds.Default,
        DetectionConfidence.Certain);
}
