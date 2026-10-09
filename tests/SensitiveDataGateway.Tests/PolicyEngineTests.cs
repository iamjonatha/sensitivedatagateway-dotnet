using System;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;

namespace SensitiveDataGateway.Tests;

public sealed class PolicyEngineTests
{
    private static readonly SegmentId s_segment = SegmentId.Create("segment-1");
    private static readonly PartId s_part = PartId.Create("part-1");
    private static readonly DetectorId s_detector = DetectorId.Create("test-detector");
    private static readonly PolicyEngine s_engine = new();

    [Fact]
    public void Evaluate_RejectsANullDocument() =>
        Assert.Throws<ArgumentNullException>(() => s_engine.Evaluate(null!, SanitizationPolicy.Strict));

    [Fact]
    public void Evaluate_RejectsANullPolicy() =>
        Assert.Throws<ArgumentNullException>(() => s_engine.Evaluate(Document(), null!));

    [Fact]
    public void Evaluate_BlocksATypeThePolicyDoesNotMention()
    {
        var policy = Policy(builder => builder);
        var document = Document(Entity(EntityType.Create("UNHEARD_OF")));

        var plan = s_engine.Evaluate(document, policy);

        Assert.True(plan.IsBlocked);
        Assert.Equal(TransformationAction.Block, Assert.Single(plan.Decisions).Action);
        Assert.Equal(
            PolicyRationaleSource.UnknownEntityAction,
            plan.Decisions[0].Rationale.Source);
        Assert.Equal(PolicyViolationReason.BlockedEntity, Assert.Single(plan.Blocking).Reason);
    }

    [Fact]
    public void Evaluate_AppliesTheRuleThatMatchesFirst()
    {
        var policy = Policy(builder => builder
            .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Block)
                .WhenSensitivityIs(SensitivityLevel.Critical))
            .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Redact)));

        var plan = s_engine.Evaluate(Document(Entity(EntityType.Person)), policy);

        var decision = Assert.Single(plan.Decisions);
        Assert.Equal(TransformationAction.Redact, decision.Action);
        Assert.Equal(1, decision.Rationale.RuleIndex);
        Assert.False(plan.IsBlocked);
    }

    [Fact]
    public void Evaluate_FallsBackToTheDefaultActionForAMentionedTypeNoRuleMatches()
    {
        var policy = Policy(builder => builder
            .WithDefaultAction(TransformationAction.Redact)
            .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Mask)
                .WhenSensitivityIs(SensitivityLevel.Critical)));

        var plan = s_engine.Evaluate(Document(Entity(EntityType.Person)), policy);

        var decision = Assert.Single(plan.Decisions);
        Assert.Equal(TransformationAction.Redact, decision.Action);
        Assert.Equal(PolicyRationaleSource.DefaultAction, decision.Rationale.Source);
        Assert.Null(decision.Rationale.RuleIndex);
    }

    [Fact]
    public void Evaluate_GivesEveryDecisionANonEmptyRationale()
    {
        var plan = s_engine.Evaluate(
            Document(Entity(EntityType.Person), Entity(EntityType.Iban), Entity(EntityType.ApiKey)),
            SanitizationPolicy.Balanced);

        Assert.NotEmpty(plan.Decisions);
        Assert.All(plan.Decisions, decision =>
            Assert.False(string.IsNullOrWhiteSpace(decision.Rationale.ToString())));
    }

    [Fact]
    public void Evaluate_ResolvesTheScopeOfACorrelatableAction()
    {
        var policy = Policy(builder => builder
            .WithDefaultScope(PseudonymScope.Tenant)
            .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Pseudonymize))
            .AddRule(PolicyRule.For(EntityType.Customer, TransformationAction.Pseudonymize)
                .WithScope(PseudonymScope.Document)));

        var plan = s_engine.Evaluate(
            Document(Entity(EntityType.Person), Entity(EntityType.Customer)),
            policy);

        Assert.Equal(PseudonymScope.Tenant, plan.Decisions[0].Scope);
        Assert.Equal(PseudonymScope.Document, plan.Decisions[1].Scope);
    }

    [Fact]
    public void Evaluate_LeavesNoScopeOnAnActionThatProducesNoCorrelatableReplacement()
    {
        var policy = Policy(builder => builder
            .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Redact)));

        var plan = s_engine.Evaluate(Document(Entity(EntityType.Person)), policy);

        Assert.Null(Assert.Single(plan.Decisions).Scope);
    }

    [Fact]
    public void Evaluate_LeavesAWeakCandidateUnresolvedAndDecidesNothingAboutIt()
    {
        var policy = Policy(builder => builder
            .TolerateUnresolvedUpTo(SensitivityLevel.High)
            .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Redact)));

        var entity = Entity(EntityType.Person, DetectionConfidence.Create(0.10), SensitivityLevel.High);
        var plan = s_engine.Evaluate(Document(entity), policy);

        Assert.Empty(plan.Decisions);
        Assert.Same(entity, Assert.Single(plan.Unresolved));
        Assert.False(plan.IsBlocked);
    }

    [Fact]
    public void Evaluate_BlocksAnUnresolvedCandidateThePolicyDoesNotTolerate()
    {
        var policy = Policy(builder => builder
            .TolerateUnresolvedUpTo(SensitivityLevel.Low)
            .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Redact)));

        var entity = Entity(EntityType.Person, DetectionConfidence.Create(0.10), SensitivityLevel.Critical);
        var plan = s_engine.Evaluate(Document(entity), policy);

        Assert.Single(plan.Unresolved);
        Assert.Equal(PolicyViolationReason.UnresolvedEntity, Assert.Single(plan.Blocking).Reason);
    }

    [Fact]
    public void Evaluate_BlocksWhenAReviewIsRequired()
    {
        var policy = Policy(builder => builder
            .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.RequireReview)));

        var plan = s_engine.Evaluate(Document(Entity(EntityType.Person)), policy);

        Assert.Equal(PolicyViolationReason.ReviewRequired, Assert.Single(plan.Blocking).Reason);
    }

    [Fact]
    public void Evaluate_BlocksAnIncompleteScan()
    {
        var plan = s_engine.Evaluate(IncompleteDocument(), Policy(builder => builder));

        Assert.Equal(PolicyViolationReason.IncompleteScan, Assert.Single(plan.Blocking).Reason);
    }

    [Fact]
    public void Evaluate_BlocksAnIncompleteScanUnderEveryShippedPolicy()
    {
        foreach (var policy in new[] { SanitizationPolicy.Strict, SanitizationPolicy.Balanced })
        {
            var plan = s_engine.Evaluate(IncompleteDocument(), policy);

            Assert.Equal(PolicyViolationReason.IncompleteScan, Assert.Single(plan.Blocking).Reason);
        }
    }

    [Fact]
    public void Evaluate_StopsWhenThereAreMoreEntitiesThanThePolicyAllows()
    {
        var policy = Policy(builder => builder
            .WithLimits(ProcessingLimits.Create(1_024, 1_024, 1, 1_024, 1, TimeSpan.FromSeconds(1)))
            .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Redact)));

        var document = Document(Entity(EntityType.Person), Entity(EntityType.Person));

        var exception = Assert.Throws<ProcessingLimitExceededException>(() => s_engine.Evaluate(document, policy));

        Assert.Equal("policy.too_many_entities", exception.Error.Code);
    }

    [Fact]
    public void Evaluate_NamesThePolicyThatProducedThePlan()
    {
        var plan = s_engine.Evaluate(Document(), SanitizationPolicy.Strict);

        Assert.Equal(SanitizationPolicy.Strict.Id, plan.PolicyId);
        Assert.Equal(SanitizationPolicy.Strict.Version, plan.PolicyVersion);
    }

    [Fact]
    public void Evaluate_LeavesTheTextOfTheDocumentUntouched()
    {
        var document = Document(Entity(EntityType.Person));
        var before = document.Extracted.Segments[0].Text;

        s_engine.Evaluate(document, SanitizationPolicy.Balanced);

        Assert.Equal(before, document.Extracted.Segments[0].Text);
    }

    [Fact]
    public void Evaluate_IsRepeatable()
    {
        var document = Document(Entity(EntityType.Person), Entity(EntityType.Iban));

        var first = s_engine.Evaluate(document, SanitizationPolicy.Balanced);
        var second = s_engine.Evaluate(document, SanitizationPolicy.Balanced);

        Assert.Equal(first.ToString(), second.ToString());
    }

    private static SanitizationPolicy Policy(
        Func<SanitizationPolicyBuilder, SanitizationPolicyBuilder> configure) =>
        configure(SanitizationPolicy.CreateBuilder(PolicyId.Create("test"), PolicyVersion.Create("1.0")))
            .Build();

    private static SensitiveEntity Entity(EntityType type) =>
        Entity(type, DetectionConfidence.Certain, SensitivityLevel.High);

    private static SensitiveEntity Entity(
        EntityType type,
        DetectionConfidence confidence,
        SensitivityLevel sensitivity) =>
        SensitiveEntity.Create(
            EntityId.New(),
            type,
            SourceSpan.Create(s_segment, 0, 4),
            confidence,
            s_detector,
            sensitivity,
            DetectionEvidence.FromPattern("test", checksumValidated: false, 4));

    private static DetectedDocument Document(params SensitiveEntity[] entities) =>
        DetectedDocument.Create(Extracted(), entities, [DetectorVersion.Create(s_detector, "1.0.0")], []);

    private static DetectedDocument IncompleteDocument() =>
        DetectedDocument.Create(
            Extracted(),
            [],
            [],
            [DetectionFailure.Create(s_detector, DetectionFailureReason.TimedOut)]);

    private static ExtractedDocument Extracted() =>
        ExtractedDocument.Create(
            BundleId.New(),
            [TextSegment.Create(s_segment, s_part, "0123456789", SegmentOrigin.Unspecified, 0)],
            [],
            []);
}
