using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Tests.Detection;

/// <summary>
/// Covers the structural checks that decide whether a candidate is a value or a lookalike.
/// </summary>
/// <remarks>
/// Every number in this file is invented. The card numbers are the sequences the card
/// networks publish for testing and assign to nobody, the account numbers use a bank code
/// that does not exist, and the tax codes encode names that are not names.
/// </remarks>
public sealed class ChecksumTests
{
    [Theory]
    [InlineData("4111111111111111")]
    [InlineData("5555555555554444")]
    [InlineData("378282246310005")]
    [InlineData("4111 1111 1111 1111")]
    [InlineData("4111-1111-1111-1111")]
    public void Luhn_AcceptsAConsistentCardNumber(string value) =>
        Assert.True(Luhn.IsValid(value));

    [Theory]
    [InlineData("4111111111111112")]
    [InlineData("5555555555554445")]
    [InlineData("1234567890123456")]
    public void Luhn_RejectsAnInconsistentCheckDigit(string value) =>
        Assert.False(Luhn.IsValid(value));

    [Theory]
    [InlineData("41111111111")]
    [InlineData("41111111111111111111")]
    public void Luhn_RejectsALengthNoCardUses(string value) =>
        Assert.False(Luhn.IsValid(value));

    [Theory]
    [InlineData("4111111111111a11")]
    [InlineData("")]
    public void Luhn_RejectsAnythingThatIsNotDigits(string value) =>
        Assert.False(Luhn.IsValid(value));

    [Theory]
    [InlineData("IT51X9999999999000000999999")]
    [InlineData("DE98999999990000009999")]
    [InlineData("GB14ZZZZ99999900009999")]
    [InlineData("IT51 X999 9999 9990 0000 0999 999")]
    [InlineData("it51x9999999999000000999999")]
    public void Iban_AcceptsAConsistentAccountNumber(string value) =>
        Assert.True(IbanChecksum.IsValid(value));

    [Theory]
    [InlineData("IT52X9999999999000000999999")]
    [InlineData("DE99999999990000009999")]
    [InlineData("IT51X9999999999000000999998")]
    public void Iban_RejectsAnInconsistentCheckNumber(string value) =>
        Assert.False(IbanChecksum.IsValid(value));

    [Theory]
    [InlineData("IT51X99999999990")]
    [InlineData("I151X9999999999000000999999")]
    [InlineData("ITX1X9999999999000000999999")]
    [InlineData("IT51X99999999990000009999991234567890")]
    [InlineData("IT51X99999_99990000009999")]
    public void Iban_RejectsAnythingThatIsNotShapedLikeAnAccountNumber(string value) =>
        Assert.False(IbanChecksum.IsValid(value));

    [Theory]
    [InlineData("XQZXQZ80A01Z999G")]
    [InlineData("KWVJQX91M41H501G")]
    [InlineData("QRTVPL75D52L219T")]
    public void ItalianTaxCode_AcceptsAConsistentCode(string value) =>
        Assert.True(ItalianTaxCode.IsValid(value));

    [Theory]
    [InlineData("XQZXQZ80A01Z999H")]
    [InlineData("KWVJQX91M41H501A")]
    public void ItalianTaxCode_RejectsAnInconsistentCheckCharacter(string value) =>
        Assert.False(ItalianTaxCode.IsValid(value));

    [Theory]
    [InlineData("XQZXQ980A01Z999G")]
    [InlineData("XQZXQZ80K01Z999G")]
    [InlineData("XQZXQZ80A019999G")]
    [InlineData("XQZXQZ80A01Z999")]
    [InlineData("XQZXQZ80A01Z999GG")]
    [InlineData("xqzxqz80a01z999g")]
    public void ItalianTaxCode_RejectsAnythingThatIsNotShapedLikeACode(string value) =>
        Assert.False(ItalianTaxCode.IsValid(value));

    [Theory]
    [InlineData("ZZZZZZ00A00Z000S")]
    [InlineData("ZZZZZZ00A72Z000D")]
    public void ItalianTaxCode_RejectsADayOfBirthThatDoesNotExist(string value) =>
        Assert.False(ItalianTaxCode.IsValid(value));
}
