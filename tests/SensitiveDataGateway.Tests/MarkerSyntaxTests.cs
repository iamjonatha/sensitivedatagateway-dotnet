using System;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Tests;

public sealed class MarkerSyntaxTests
{
    [Theory]
    [InlineData(1, "[PERSON_001]")]
    [InlineData(42, "[PERSON_042]")]
    [InlineData(1_000, "[PERSON_1000]")]
    public void Pseudonym_NumbersTheTokenWithinItsType(int sequence, string expected) =>
        Assert.Equal(expected, MarkerSyntax.Pseudonym(EntityType.Person, sequence));

    [Fact]
    public void Pseudonym_RejectsASequenceThatIsNotPositive() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => MarkerSyntax.Pseudonym(EntityType.Person, 0));

    [Fact]
    public void Hash_MarksTheDigestSoItCannotBeReadAsASequence() =>
        Assert.Equal("[IBAN_H3F9A2C71]", MarkerSyntax.Hash(EntityType.Iban, "3F9A2C71"));

    [Theory]
    [InlineData("")]
    [InlineData("3f9a2c71")]
    [InlineData("NOTHEX")]
    public void Hash_RejectsADigestThatIsNotUpperCaseHexadecimal(string digest) =>
        Assert.Throws<ArgumentException>(() => MarkerSyntax.Hash(EntityType.Iban, digest));

    [Fact]
    public void Redaction_TellsTheTypeAndNothingElse() =>
        Assert.Equal("[REDACTED_EMAIL]", MarkerSyntax.Redaction(EntityType.Email));

    [Fact]
    public void Redaction_IsTheSameForTwoValuesOfOneType() =>
        Assert.Equal(MarkerSyntax.Redaction(EntityType.Email), MarkerSyntax.Redaction(EntityType.Email));

    [Theory]
    [InlineData("[PERSON_001]", true)]
    [InlineData("before [REDACTED_EMAIL] after", true)]
    [InlineData("[OK]", true)]
    [InlineData("[]", false)]
    [InlineData("[lower]", false)]
    [InlineData("[unclosed", false)]
    [InlineData("nothing here", false)]
    public void ContainsMarker_ReportsTheShapeAndNotTheMeaning(string text, bool expected) =>
        Assert.Equal(expected, MarkerSyntax.ContainsMarker(text));

    [Fact]
    public void Escape_DoublesTheOpeningBracketOfEveryMarkerShapedRun() =>
        Assert.Equal("[[PERSON_001] and [[OK]", MarkerSyntax.Escape("[PERSON_001] and [OK]"));

    [Fact]
    public void Escape_LeavesTextThatCannotBeMistakenForAToken()
    {
        const string Text = "nothing [lower] here";

        Assert.Same(Text, MarkerSyntax.Escape(Text));
    }

    [Fact]
    public void Escape_IsNotIdempotentBecauseItsResultIsNeverEscapedTwice()
    {
        var once = MarkerSyntax.Escape("[PERSON_001]");

        Assert.Equal("[[PERSON_001]", once);
        Assert.Equal("[[[PERSON_001]", MarkerSyntax.Escape(once));
    }

    [Theory]
    [InlineData("[PERSON_001] and [OK]")]
    [InlineData("a [TAX_ID_H0A1B] b")]
    [InlineData("no markers at all")]
    public void Unescape_UndoesEscape(string text) =>
        Assert.Equal(text, MarkerSyntax.Unescape(MarkerSyntax.Escape(text)));

    [Fact]
    public void Unescape_LeavesATokenTheGatewayWrote() =>
        Assert.Equal("[PERSON_001]", MarkerSyntax.Unescape("[PERSON_001]"));
}
