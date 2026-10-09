using System;
using System.Collections.Generic;
using System.Linq;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Tests;

/// <summary>
/// Exercises the last stage of the pipeline.
/// </summary>
/// <remarks>
/// Each of the ten checks has a test that breaks exactly that check and nothing else. A
/// check with no test proving it can fail is a check that may already be unreachable.
/// </remarks>
public sealed class PrivacyValidatorTests
{
    private const string Person = "Dana Oakfield";
    private const string Email = "dana@example.invalid";
    private const string Body = "Contact Dana Oakfield at dana@example.invalid today";

    private static readonly PolicyEngine s_engine = new();

    [Fact]
    public void Validate_AllowsADocumentEveryCheckAgreesOn()
    {
        var result = Validate(Document(), Rules());

        Assert.True(result.IsAllowed);
        Assert.NotNull(result.Payload);
        Assert.True(result.Report.Passed);
    }

    [Fact]
    public void Validate_RunsEveryCheckBeforeItAllows()
    {
        var result = Validate(Document(), Rules());

        var expected = Enum.GetValues<PrivacyCheck>().Where(check => check != PrivacyCheck.None);

        Assert.Equal(expected, result.Report.Executed);
    }

    [Fact]
    public void Validate_HashesTheContentItLetsThrough()
    {
        var hash = Validate(Document(), Rules()).Payload!.ContentHash;
        var same = Validate(Document(), Rules()).Payload!.ContentHash;

        var other = Validate(
            Fixture.Document(Fixture.Part("body", Body, Span(Body, Person, EntityType.Person))),
            PolicyRule.For(EntityType.Person, TransformationAction.Redact)).Payload!.ContentHash;

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, same);
        Assert.NotEqual(hash, other);
    }

    [Fact]
    public void Validate_CarriesTheSanitizedContentIntoThePayload()
    {
        var result = Validate(Document(), Rules());

        Assert.Equal(
            "Contact [REDACTED_PERSON] at [REDACTED_EMAIL] today",
            result.Payload!.Content[0].Text);
    }

    [Fact]
    public void Validate_BlocksASanitizationThatDidNotComplete()
    {
        var result = Forge(sanitized => Rebuild(sanitized, status: SanitizationStatus.Incomplete));

        AssertBlockedBy(result, PrivacyCheck.SanitizationStatus);
    }

    [Fact]
    public void Validate_BlocksADocumentAnExtractorCouldNotRead()
    {
        var document = Document();
        var extracted = ExtractedDocument.Create(
            document.Extracted.BundleId,
            document.Extracted.Segments,
            [ExtractionFailure.Create(PartId.Create("attachment-1"), ExtractionFailureReason.Malformed, "application/pdf")],
            []);

        var incomplete = DetectedDocument.Create(
            extracted,
            document.Entities,
            [DetectorVersion.Create(Fixture.Detector, "1.0.0")],
            []);

        AssertBlockedBy(ValidateUnapplied(document, incomplete), PrivacyCheck.ScanCompleteness);
    }

    [Fact]
    public void Validate_BlocksADocumentADetectorCouldNotFinish()
    {
        var document = Document();
        var incomplete = DetectedDocument.Create(
            document.Extracted,
            document.Entities,
            [DetectorVersion.Create(Fixture.Detector, "1.0.0")],
            [DetectionFailure.Create(Fixture.Detector, DetectionFailureReason.TimedOut)]);

        AssertBlockedBy(ValidateUnapplied(document, incomplete), PrivacyCheck.ScanCompleteness);
    }

    [Fact]
    public void Validate_BlocksAnUnresolvedCandidateThePolicyDoesNotTolerate()
    {
        var policy = SanitizationPolicy
            .CreateBuilder(Fixture.PolicyName, Fixture.Version)
            .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Redact))
            .AddRule(PolicyRule.For(EntityType.Email, TransformationAction.Redact))
            .TolerateNoUnresolved()
            .Build();

        var document = Document();

        AssertBlockedBy(
            ValidateUnapplied(document, Uncertain(document), policy),
            PrivacyCheck.UnresolvedEntities);
    }

    [Fact]
    public void Validate_AllowsAnUnresolvedCandidateThePolicyTolerates()
    {
        var policy = SanitizationPolicy
            .CreateBuilder(Fixture.PolicyName, Fixture.Version)
            .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Redact))
            .AddRule(PolicyRule.For(EntityType.Email, TransformationAction.Redact))
            .TolerateUnresolvedUpTo(SensitivityLevel.High)
            .Build();

        var result = Validate(Uncertain(Document()), policy);

        Assert.True(result.IsAllowed);
        Assert.Contains(Person, result.Payload!.Content[0].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_BlocksAPolicyViolationThePlanCarried()
    {
        var policy = Fixture.Policy(
            Rule(EntityType.Person, TransformationAction.Block),
            Rule(EntityType.Email, TransformationAction.Redact));

        var document = Document();

        AssertBlockedBy(ValidateUnapplied(document, document, policy), PrivacyCheck.PolicyViolations);
    }

    [Fact]
    public void Validate_BlocksAPlannedTransformationThatWasNeverApplied()
    {
        var result = Forge(sanitized => Rebuild(
            sanitized,
            segments: [Segment(sanitized, Body)],
            applied: []));

        AssertBlockedBy(result, PrivacyCheck.TransformationCoverage);
    }

    [Fact]
    public void Validate_BlocksAValueThatSurvivedTheRewrite()
    {
        var result = Forge(sanitized => Rebuild(sanitized, segments: [Segment(sanitized, Body)]));

        AssertBlockedBy(result, PrivacyCheck.AntiLeakScan);
    }

    [Fact]
    public void Validate_BlocksATokenThatSpellsOutTheValueItStandsFor()
    {
        var result = Forge(
            sanitized => Rebuild(sanitized, segments: [Segment(sanitized, "Contact [PERSON_DANA_OAKFIELD] today")]),
            TransformationAction.Pseudonymize);

        AssertBlockedBy(result, PrivacyCheck.TokenResolution);
    }

    [Fact]
    public void Validate_BlocksAMarkerTheGatewayDidNotWrite()
    {
        var result = Forge(sanitized => Rebuild(
            sanitized,
            segments: [Segment(sanitized, "Contact [REDACTED_PERSON] at [REDACTED_EMAIL] today [REDACTED_IBAN]")]));

        AssertBlockedBy(result, PrivacyCheck.MarkerIntegrity);
    }

    [Fact]
    public void Validate_CountsAnEscapedMarkerAsContentRatherThanAsAToken()
    {
        const string Forged = "[PERSON_001] met Dana Oakfield at dana@example.invalid";
        var document = Fixture.Document(Fixture.Part(
            "body",
            Forged,
            Span(Forged, Person, EntityType.Person),
            Span(Forged, Email, EntityType.Email)));

        Assert.True(Validate(document, Rules()).IsAllowed);
    }

    [Fact]
    public void Validate_BlocksProvenanceThatNamesAnotherPolicy()
    {
        var provenance = SanitizationProvenance.Create(
            PolicyId.Create("another-policy"),
            Fixture.Version,
            [DetectorVersion.Create(Fixture.Detector, "1.0.0")],
            [],
            Fixture.GatewayVersion,
            Fixture.SanitizedAt);

        var result = Forge(sanitized => Rebuild(sanitized, provenance: provenance));

        AssertBlockedBy(result, PrivacyCheck.ProvenanceCompleteness);
    }

    [Fact]
    public void Validate_BlocksAPayloadLargerThanThePolicyAllows()
    {
        var policy = SanitizationPolicy
            .CreateBuilder(Fixture.PolicyName, Fixture.Version)
            .AddRule(PolicyRule.For(EntityType.Person, TransformationAction.Redact))
            .AddRule(PolicyRule.For(EntityType.Email, TransformationAction.Redact))
            .WithLimits(ProcessingLimits.Create(1024, 1024, 4, 8, 4, TimeSpan.FromSeconds(1)))
            .Build();

        AssertBlockedBy(Validate(Document(), policy), PrivacyCheck.PayloadStructure);
    }

    [Fact]
    public void Validate_ProducesNoPayloadWhenItBlocks()
    {
        var result = Forge(sanitized => Rebuild(sanitized, segments: [Segment(sanitized, Body)]));

        Assert.False(result.IsAllowed);
        Assert.Null(result.Payload);
    }

    [Fact]
    public void Validate_ReportsNothingThatCarriesTheValueItBlocked()
    {
        var result = Forge(sanitized => Rebuild(sanitized, segments: [Segment(sanitized, Body)]));

        var rendered = string.Join(
            ' ',
            result.ToString(),
            result.Report.ToString(),
            string.Join(' ', result.Report.Findings.Select(finding => finding.ToString())),
            string.Join(' ', result.Report.ToErrors().Select(error => error.ToString())));

        Assert.DoesNotContain(Person, rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Email, rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Oakfield", rendered, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsAMissingArgument()
    {
        var validator = new PrivacyValidator();
        var document = Document();
        var policy = Fixture.Policy(Rules());
        var plan = s_engine.Evaluate(document, policy);
        var sanitized = Anonymize(document, plan);

        Assert.Throws<ArgumentNullException>(() => validator.Validate(null!, plan, sanitized, policy));
        Assert.Throws<ArgumentNullException>(() => validator.Validate(document, null!, sanitized, policy));
        Assert.Throws<ArgumentNullException>(() => validator.Validate(document, plan, null!, policy));
        Assert.Throws<ArgumentNullException>(() => validator.Validate(document, plan, sanitized, null!));
    }

    [Fact]
    public void Constructor_RejectsMissingOptions() =>
        Assert.Throws<ArgumentNullException>(() => new PrivacyValidator(null!));

    private static DetectedDocument Document() => Fixture.Document(Fixture.Part(
        "body",
        Body,
        Span(Body, Person, EntityType.Person),
        Span(Body, Email, EntityType.Email)));

    private static Fixture.EntitySpec Span(string text, string value, EntityType type) =>
        Fixture.At(type, text.IndexOf(value, StringComparison.Ordinal), value.Length);

    private static PolicyRule[] Rules() =>
    [
        PolicyRule.For(EntityType.Person, TransformationAction.Redact),
        PolicyRule.For(EntityType.Email, TransformationAction.Redact),
    ];

    /// <summary>
    /// Restates a document with detections nobody is sure about.
    /// </summary>
    /// <param name="document">The detected document.</param>
    /// <returns>The same document, with every score below the flag threshold.</returns>
    private static DetectedDocument Uncertain(DetectedDocument document)
    {
        var entities = document.Entities.Select(entity => SensitiveEntity.Create(
            entity.Id,
            entity.Type,
            entity.Span,
            DetectionConfidence.Create(0.4),
            entity.Detector,
            entity.Sensitivity,
            entity.Evidence));

        return DetectedDocument.Create(document.Extracted, entities, document.Detectors, document.Failures);
    }

    private static PolicyRule Rule(EntityType type, TransformationAction action) =>
        PolicyRule.For(type, action);

    private static SanitizationResult Validate(DetectedDocument document, params PolicyRule[] rules) =>
        Validate(document, Fixture.Policy(rules));

    private static SanitizationResult Validate(DetectedDocument document, SanitizationPolicy policy)
    {
        var plan = s_engine.Evaluate(document, policy);

        return new PrivacyValidator().Validate(document, plan, Anonymize(document, plan), policy);
    }

    private static SanitizedDocument Anonymize(DetectedDocument document, TransformationPlan plan) =>
        new DocumentAnonymizer(new InMemoryPseudonymStore()).Anonymize(document, plan, Fixture.Provenance());

    /// <summary>
    /// Validates output against a plan the anonymizer would have refused to apply.
    /// </summary>
    /// <param name="applied">The document the output was produced from.</param>
    /// <param name="validated">The document the validator re-derives its facts from.</param>
    /// <returns>The result of validating output that should never have been produced.</returns>
    /// <remarks>
    /// The anonymizer refuses a blocked plan, so the only way this output exists is a caller
    /// that skipped the check. That is the caller the validator is there to catch, and the
    /// only way to test it is to be that caller.
    /// </remarks>
    private static SanitizationResult ValidateUnapplied(DetectedDocument applied, DetectedDocument validated) =>
        ValidateUnapplied(applied, validated, Fixture.Policy(Rules()));

    private static SanitizationResult ValidateUnapplied(
        DetectedDocument applied,
        DetectedDocument validated,
        SanitizationPolicy policy)
    {
        var sanitized = Anonymize(applied, s_engine.Evaluate(applied, Fixture.Policy(Rules())));

        return new PrivacyValidator().Validate(
            validated,
            s_engine.Evaluate(validated, policy),
            sanitized,
            policy);
    }

    /// <summary>
    /// Validates output that a defective anonymizer could have produced.
    /// </summary>
    /// <param name="damage">What the defective stage did to the output.</param>
    /// <param name="action">The action the policy applies to both entities.</param>
    /// <returns>The result of validating the damaged output.</returns>
    /// <remarks>
    /// The point of the validator is to catch a stage that is wrong, so every negative test
    /// has to produce output a correct stage never would.
    /// </remarks>
    private static SanitizationResult Forge(
        Func<SanitizedDocument, SanitizedDocument> damage,
        TransformationAction action = TransformationAction.Redact)
    {
        var document = Document();
        var policy = Fixture.Policy(Rule(EntityType.Person, action), Rule(EntityType.Email, action));
        var plan = s_engine.Evaluate(document, policy);

        return new PrivacyValidator().Validate(document, plan, damage(Anonymize(document, plan)), policy);
    }

    private static SanitizedSegment Segment(SanitizedDocument sanitized, string text)
    {
        var original = sanitized.Segments[0];

        return SanitizedSegment.Create(original.Id, original.PartId, text, original.Origin, original.Order);
    }

    private static SanitizedDocument Rebuild(
        SanitizedDocument sanitized,
        IReadOnlyList<SanitizedSegment>? segments = null,
        SanitizationStatus? status = null,
        IReadOnlyList<AppliedTransformation>? applied = null,
        SanitizationProvenance? provenance = null) =>
        SanitizedDocument.Create(
            sanitized.BundleId,
            segments ?? sanitized.Segments,
            status ?? sanitized.Status,
            applied ?? sanitized.Applied,
            sanitized.Warnings,
            provenance ?? sanitized.Provenance);

    private static void AssertBlockedBy(SanitizationResult result, PrivacyCheck check)
    {
        Assert.False(result.IsAllowed);
        Assert.Contains(result.Report.Findings, finding => finding.Check == check);
    }
}
