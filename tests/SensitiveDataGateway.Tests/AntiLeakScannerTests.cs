using System;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Tests;

/// <summary>
/// Exercises the scan that looks for stage 2 values in stage 4 output.
/// </summary>
/// <remarks>
/// The scanner is the part of validation that does not believe the anonymizer, so its tests
/// hand it output the anonymizer would never produce.
/// </remarks>
public sealed class AntiLeakScannerTests
{
    private const string Person = "Dana Oakfield";

    private static readonly AntiLeakScanner s_scanner = new(ValidationOptions.Default);

    [Fact]
    public void Scan_PassesOutputThatKeptNothing()
    {
        var findings = Scan("Contact [REDACTED_PERSON] today", new LeakCandidate(Person, EntityType.Person));

        Assert.Empty(findings);
    }

    [Fact]
    public void Scan_ReportsAValueThatIsStillThere()
    {
        var findings = Scan($"Contact {Person} today", new LeakCandidate(Person, EntityType.Person));

        Assert.Equal(LeakSignal.Exact, Assert.Single(findings).Signal);
    }

    [Fact]
    public void Scan_ReportsAValueThatOnlyLooksDifferent()
    {
        var findings = Scan("Contact DANA-OAKFIELD today", new LeakCandidate(Person, EntityType.Person));

        Assert.Equal(LeakSignal.Normalized, Assert.Single(findings).Signal);
    }

    [Fact]
    public void Scan_ReportsAValueSmuggledThroughOneWordAtATime()
    {
        var findings = Scan("The surname is Oakfield", new LeakCandidate(Person, EntityType.Person));

        Assert.Equal(LeakSignal.Fragment, Assert.Single(findings).Signal);
    }

    [Fact]
    public void Scan_IgnoresAFragmentShorterThanTheThreshold()
    {
        var findings = Scan("Anna lives here", new LeakCandidate("Anna Oakfield", EntityType.Person));

        Assert.Empty(findings);
    }

    [Fact]
    public void Scan_ReportsASecretNoDetectorHadToFind()
    {
        var findings = Scan("Use AKIAIOSFODNN7EXAMPLE to sign in");

        var finding = Assert.Single(findings);

        Assert.Equal(LeakSignal.SecretPattern, finding.Signal);
    }

    [Fact]
    public void Scan_ReportsKeyMaterialNoPatternDescribes()
    {
        var findings = Scan("token qX7vR2pLmZ4wT9aK3bN8sE6yU1cH5dJ0gF");

        Assert.Equal(LeakSignal.HighEntropy, Assert.Single(findings).Signal);
    }

    [Fact]
    public void Scan_DoesNotReportTheTokensItWroteItself()
    {
        var findings = Scan("Contact [PERSON_001] about [IBAN_H3F9A2C71B4D5E6F7A8B9C0D1E2F3A4B5]");

        Assert.Empty(findings);
    }

    [Fact]
    public void Scan_DoesNotReportOrdinaryProse()
    {
        var findings = Scan(
            "The quarterly report was circulated to the committee before the meeting on Tuesday.");

        Assert.Empty(findings);
    }

    [Fact]
    public void Scan_DoesNotReportAMaskedValue()
    {
        var findings = Scan("Card ****1234", new LeakCandidate("4111111111111234", EntityType.CreditCard));

        Assert.Empty(findings);
    }

    [Fact]
    public void Scan_NamesTheSegmentItFoundTheValueIn()
    {
        var findings = Scan($"Contact {Person} today", new LeakCandidate(Person, EntityType.Person));

        Assert.Equal(SegmentId.Create("segment-0"), Assert.Single(findings).Segment);
    }

    [Fact]
    public void Scan_CarriesNoValueInWhatItReports()
    {
        var findings = Scan($"Contact {Person} today", new LeakCandidate(Person, EntityType.Person));

        Assert.DoesNotContain("Oakfield", Assert.Single(findings).ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static System.Collections.Generic.IReadOnlyList<PrivacyFinding> Scan(
        string text,
        params LeakCandidate[] candidates) =>
        s_scanner.Scan(
            candidates,
            [
                SanitizedSegment.Create(
                    SegmentId.Create("segment-0"),
                    PartId.Create("body"),
                    text,
                    SegmentOrigin.At(SegmentOriginKind.Paragraph, 1),
                    0),
            ]);
}
