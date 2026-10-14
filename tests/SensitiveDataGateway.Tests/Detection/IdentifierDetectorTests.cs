using System.Threading.Tasks;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Tests.Detection;

/// <summary>
/// Covers the detectors that find things which identify a person or an account.
/// </summary>
/// <remarks>
/// Every account number and tax code below is synthetic. They satisfy the relevant check
/// digit because the test is about the check digit, and they identify nobody.
/// </remarks>
public sealed class IdentifierDetectorTests
{
    [Theory]
    [InlineData("write to first.last@example.com today", "first.last@example.com")]
    [InlineData("a+tag@mail.example.co.uk", "a+tag@mail.example.co.uk")]
    public async Task Email_reports_the_address(string text, string expected)
    {
        var matches = await Scan.MatchesIn(DeterministicDetectors.Email(), text);

        Assert.Equal(expected, Assert.Single(matches));
    }

    [Theory]
    [InlineData("not an address: user@@example.com")]
    [InlineData("a sentence about mail servers and nothing else")]
    public async Task Email_ignores_text_that_is_not_an_address(string text)
    {
        var matches = await Scan.MatchesIn(DeterministicDetectors.Email(), text);

        Assert.Empty(matches);
    }

    [Theory]
    [InlineData("call +39 02 1234 5678 now", "+39 02 1234 5678")]
    [InlineData("tel: 02 1234 5678", "02 1234 5678")]
    [InlineData("Phone 0039 02 12345678", "0039 02 12345678")]
    public async Task Phone_reports_the_number_without_its_label(string text, string expected)
    {
        var matches = await Scan.MatchesIn(DeterministicDetectors.Phone(), text);

        Assert.Contains(expected, matches);
    }

    [Fact]
    public async Task Phone_ignores_a_bare_national_number()
    {
        var matches = await Scan.MatchesIn(DeterministicDetectors.Phone(), "order 0212345678 shipped");

        Assert.Empty(matches);
    }

    [Theory]
    [InlineData("IT51X9999999999000000999999")]
    [InlineData("DE98999999990000009999")]
    [InlineData("GB14ZZZZ99999900009999")]
    public async Task Iban_reports_a_number_whose_checksum_agrees(string iban)
    {
        var matches = await Scan.MatchesIn(DeterministicDetectors.Iban(), $"credit {iban} please");

        Assert.Equal(iban, Assert.Single(matches));
    }

    [Theory]
    [InlineData("IT52X9999999999000000999999")]
    [InlineData("DE99999999990000009999")]
    public async Task Iban_drops_a_number_whose_checksum_disagrees(string iban)
    {
        var matches = await Scan.MatchesIn(DeterministicDetectors.Iban(), $"credit {iban} please");

        Assert.Empty(matches);
    }

    [Fact]
    public async Task Iban_records_that_a_checksum_was_applied()
    {
        var entities = await Scan.Entities(
            DeterministicDetectors.Iban(),
            "IT51X9999999999000000999999");

        Assert.True(Assert.Single(entities).Evidence.ChecksumValidated);
    }

    [Theory]
    [InlineData("4111111111111111")]
    [InlineData("4111 1111 1111 1111")]
    [InlineData("5555-5555-5555-4444")]
    [InlineData("378282246310005")]
    public async Task CreditCard_reports_a_number_that_passes_Luhn(string card)
    {
        var matches = await Scan.MatchesIn(DeterministicDetectors.CreditCard(), $"card {card} expires");

        Assert.Equal(card, Assert.Single(matches));
    }

    [Theory]
    [InlineData("4111111111111112")]
    [InlineData("1234 5678 9012 3456")]
    public async Task CreditCard_drops_a_number_that_fails_Luhn(string card)
    {
        var matches = await Scan.MatchesIn(DeterministicDetectors.CreditCard(), $"card {card} expires");

        Assert.Empty(matches);
    }

    [Theory]
    [InlineData("XQZXQZ80A01Z999G")]
    [InlineData("KWVJQX91M41H501G")]
    [InlineData("QRTVPL75D52L219T")]
    public async Task ItalianTaxId_reports_a_code_whose_check_character_agrees(string code)
    {
        var matches = await Scan.MatchesIn(DeterministicDetectors.ItalianTaxId(), $"CF {code} ok");

        Assert.Equal(code, Assert.Single(matches));
    }

    [Theory]
    [InlineData("XQZXQZ80A01Z999H")]
    [InlineData("ZZZZZZ00A00Z000S")]
    [InlineData("ZZZZZZ00A72Z000D")]
    public async Task ItalianTaxId_drops_a_code_that_is_not_one(string code)
    {
        var matches = await Scan.MatchesIn(DeterministicDetectors.ItalianTaxId(), $"CF {code} ok");

        Assert.Empty(matches);
    }

    [Fact]
    public async Task ItalianTaxId_reads_a_code_written_in_lower_case()
    {
        var matches = await Scan.MatchesIn(DeterministicDetectors.ItalianTaxId(), "cf xqzxqz80a01z999g");

        Assert.Equal("xqzxqz80a01z999g", Assert.Single(matches));
    }

    [Theory]
    [InlineData("see https://example.com/orders/7 for details", "https://example.com/orders/7")]
    [InlineData("ftp://files.example.org/a.txt", "ftp://files.example.org/a.txt")]
    public async Task Url_reports_the_locator(string text, string expected)
    {
        var matches = await Scan.MatchesIn(DeterministicDetectors.Url(), text);

        Assert.Equal(expected, Assert.Single(matches));
    }

    [Theory]
    [InlineData("host 192.0.2.17 responded", "192.0.2.17")]
    [InlineData("2001:0db8:0000:0000:0000:ff00:0042:8329", "2001:0db8:0000:0000:0000:ff00:0042:8329")]
    public async Task IpAddress_reports_the_address(string text, string expected)
    {
        var matches = await Scan.MatchesIn(DeterministicDetectors.IpAddress(), text);

        Assert.Equal(expected, Assert.Single(matches));
    }

    [Theory]
    [InlineData("the meeting starts at 12:34:56")]
    [InlineData("values 256.1.1.1 are out of range")]
    public async Task IpAddress_ignores_text_that_only_looks_like_an_address(string text)
    {
        var matches = await Scan.MatchesIn(DeterministicDetectors.IpAddress(), text);

        Assert.Empty(matches);
    }
}
