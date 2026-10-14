using System.Linq;
using System.Threading.Tasks;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Tests.Detection;

/// <summary>
/// Covers the detectors that find things which grant access.
/// </summary>
/// <remarks>
/// Every key, token and password below is invented, and several are deliberately malformed.
/// None of them opens anything.
/// </remarks>
public sealed class CredentialDetectorTests
{
    private const string Jwt =
        "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ6ZXJvIn0.ZmFrZS1zaWduYXR1cmUtdmFsdWU";

    [Fact]
    public async Task Jwt_reports_all_three_segments()
    {
        var matches = await Scan.MatchesIn(DeterministicDetectors.Jwt(), $"Authorization: Bearer {Jwt}");

        Assert.Equal(Jwt, Assert.Single(matches));
    }

    [Fact]
    public async Task Jwt_ignores_a_token_missing_a_segment()
    {
        var matches = await Scan.MatchesIn(
            DeterministicDetectors.Jwt(),
            "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ6ZXJvIn0");

        Assert.Empty(matches);
    }

    [Theory]
    [InlineData("AKIAZZZZZZZZZZZZZZZZ")]
    [InlineData("AIzaZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    [InlineData("sk_test_ZZZZZZZZZZZZZZZZ")]
    [InlineData("ghp_ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    [InlineData("xoxb-ZZZZZZZZZZZZ")]
    public async Task ApiKey_reports_a_shape_a_vendor_issues(string key)
    {
        var matches = await Scan.MatchesIn(DeterministicDetectors.ApiKey(), $"key is {key} here");

        Assert.Equal(key, Assert.Single(matches));
    }

    [Theory]
    [InlineData("api_key = \"ZZZZZZZZZZZZZZZZZZZZ\"", "ZZZZZZZZZZZZZZZZZZZZ")]
    [InlineData("client-secret: ZZZZZZZZZZZZZZZZZZZZ", "ZZZZZZZZZZZZZZZZZZZZ")]
    public async Task ApiKey_reports_the_value_and_not_its_label(string text, string expected)
    {
        var matches = await Scan.MatchesIn(DeterministicDetectors.ApiKey(), text);

        Assert.Equal(expected, Assert.Single(matches));
    }

    [Fact]
    public async Task ApiKey_ignores_a_short_assignment()
    {
        var matches = await Scan.MatchesIn(DeterministicDetectors.ApiKey(), "api_key = short");

        Assert.Empty(matches);
    }

    [Fact]
    public async Task PrivateKey_reports_the_whole_block()
    {
        const string Block =
            "-----BEGIN RSA PRIVATE KEY-----\nQUJDREVGRw==\n-----END RSA PRIVATE KEY-----";

        var matches = await Scan.MatchesIn(DeterministicDetectors.PrivateKey(), $"attached:\n{Block}\n");

        Assert.Contains(Block, matches);
    }

    [Fact]
    public async Task PrivateKey_reports_a_block_whose_end_is_missing()
    {
        var matches = await Scan.MatchesIn(
            DeterministicDetectors.PrivateKey(),
            "-----BEGIN PRIVATE KEY-----\nQUJDREVGRw==\n");

        Assert.Equal("-----BEGIN PRIVATE KEY-----", Assert.Single(matches));
    }

    [Theory]
    [InlineData("Server=db.example.net;Initial Catalog=app;Password=not-a-real-secret")]
    [InlineData("Data Source=db.example.net;User Id=svc;Pwd=not-a-real-secret")]
    public async Task ConnectionString_reports_a_string_that_carries_a_password(string text)
    {
        var matches = await Scan.MatchesIn(DeterministicDetectors.ConnectionString(), text);

        Assert.Equal(text, Assert.Single(matches));
    }

    [Fact]
    public async Task ConnectionString_reports_credentials_written_into_a_locator()
    {
        var matches = await Scan.MatchesIn(
            DeterministicDetectors.ConnectionString(),
            "postgres://svc:not-a-real-secret@db.example.net:5432/app");

        Assert.Equal("postgres://svc:not-a-real-secret@db.example.net:5432", Assert.Single(matches));
    }

    [Fact]
    public async Task ConnectionString_ignores_a_string_with_no_password()
    {
        var matches = await Scan.MatchesIn(
            DeterministicDetectors.ConnectionString(),
            "Server=db.example.net;Integrated Security=true");

        Assert.Empty(matches);
    }

    [Theory]
    [InlineData("password: not-a-real-secret", "not-a-real-secret")]
    [InlineData("PWD=\"not-a-real-secret\"", "not-a-real-secret")]
    public async Task Password_reports_the_value_and_not_its_label(string text, string expected)
    {
        var matches = await Scan.MatchesIn(DeterministicDetectors.Password(), text);

        Assert.Equal(expected, Assert.Single(matches));
    }

    [Fact]
    public async Task HighEntropy_reports_a_run_too_dense_to_be_prose()
    {
        const string Token = "aB3dE5fG7hJ9kLmNpQrStUvWxYz2";

        var matches = await Scan.MatchesIn(DeterministicDetectors.HighEntropy(), $"value {Token} end");

        Assert.Equal(Token, Assert.Single(matches));
    }

    [Theory]
    [InlineData("the quick brown fox jumps over the lazy dog again and again")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("internationalization123")]
    public async Task HighEntropy_ignores_text_that_is_not_dense(string text)
    {
        var matches = await Scan.MatchesIn(DeterministicDetectors.HighEntropy(), text);

        Assert.Empty(matches);
    }

    [Fact]
    public async Task HighEntropy_records_the_measurement_it_made()
    {
        var entities = await Scan.Entities(
            DeterministicDetectors.HighEntropy(),
            "aB3dE5fG7hJ9kLmNpQrStUvWxYz2");

        var evidence = Assert.Single(entities).Evidence;

        Assert.Null(evidence.ChecksumValidated);
        Assert.NotNull(evidence.Score);
        Assert.True(evidence.Score > EntropyDetectorOptions.Default.MinimumBitsPerCharacter);
    }

    [Fact]
    public async Task HighEntropy_honours_a_raised_threshold()
    {
        var strict = EntropyDetectorOptions.Default.WithMinimumBitsPerCharacter(6.0);

        var matches = await Scan.MatchesIn(
            DeterministicDetectors.HighEntropy(strict),
            "aB3dE5fG7hJ9kLmNpQrStUvWxYz2");

        Assert.Empty(matches);
    }

    [Fact]
    public async Task Every_built_in_detector_reports_a_version()
    {
        var detectors = DeterministicDetectors.All();

        Assert.All(detectors, detector => Assert.False(detector.Version.IsDefault));
        Assert.Equal(
            detectors.Count,
            detectors.Select(detector => detector.Version.DetectorId).Distinct().Count());

        await Task.CompletedTask;
    }
}
