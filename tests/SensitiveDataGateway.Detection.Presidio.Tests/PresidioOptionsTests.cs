using System;
using System.Collections.Generic;
using System.Net.Http;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Detection.Semantic;

namespace SensitiveDataGateway.Detection.Presidio.Tests;

public sealed class PresidioOptionsTests
{
    private static readonly Uri s_endpoint = new("https://analyzer.internal/");

    [Fact]
    public void ForSelfHosted_SaysTheAnalyzerIsInsideTheBoundary()
    {
        var options = PresidioOptions.ForSelfHosted(s_endpoint);

        Assert.Equal(TrustBoundaryLocality.SelfHosted, options.Locality);
        Assert.Equal("en", options.Language);
        Assert.Equal(0.0, options.MinimumScore);
    }

    [Fact]
    public void ForHostedService_SaysTheAnalyzerIsNot()
    {
        var options = PresidioOptions.ForHostedService(s_endpoint, "en");

        Assert.Equal(TrustBoundaryLocality.Remote, options.Locality);
    }

    [Fact]
    public void AHostedAnalyzerIsRefusedOnTheStrictPath()
    {
        var detector = PresidioDetector.Create(
            new HttpClient(),
            PresidioOptions.ForHostedService(s_endpoint, "en"));

        Assert.Throws<ArgumentException>(() => SemanticDetectorHost.Create(detector));
    }

    [Fact]
    public void ASelfHostedAnalyzerIsAccepted()
    {
        var detector = PresidioDetector.Create(new HttpClient(), PresidioOptions.ForSelfHosted(s_endpoint));

        var host = SemanticDetectorHost.Create(detector);

        Assert.Equal("semantic/self-hosted/presidio", host.Version.DetectorId.Value);
    }

    [Theory]
    [InlineData("ftp://analyzer.internal/")]
    [InlineData("file:///analyzer")]
    public void ForSelfHosted_RejectsAnEndpointThatIsNotHttp(string endpoint)
    {
        Assert.Throws<ArgumentException>(() => PresidioOptions.ForSelfHosted(new Uri(endpoint)));
    }

    [Fact]
    public void ForSelfHosted_RejectsARelativeEndpoint()
    {
        Assert.Throws<ArgumentException>(() => PresidioOptions.ForSelfHosted(new Uri("/analyze", UriKind.Relative)));
    }

    [Fact]
    public void ForSelfHosted_RejectsABlankLanguage()
    {
        Assert.Throws<ArgumentException>(() => PresidioOptions.ForSelfHosted(s_endpoint, "  "));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void WithMinimumScore_RejectsAScoreThatIsNotAScore(double score)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PresidioOptions.ForSelfHosted(s_endpoint).WithMinimumScore(score));
    }

    [Fact]
    public void WithMinimumScore_LeavesTheOriginalAlone()
    {
        var options = PresidioOptions.ForSelfHosted(s_endpoint);

        var stricter = options.WithMinimumScore(0.7);

        Assert.Equal(0.0, options.MinimumScore);
        Assert.Equal(0.7, stricter.MinimumScore);
    }

    [Fact]
    public void WithEntityTypes_ReplacesTheMapAndIgnoresCase()
    {
        var options = PresidioOptions
            .ForSelfHosted(s_endpoint)
            .WithEntityTypes(new Dictionary<string, EntityType> { ["anything"] = EntityType.Health });

        Assert.True(options.EntityTypes.TryGetValue("ANYTHING", out var type));
        Assert.Equal(EntityType.Health, type);
        Assert.False(options.EntityTypes.ContainsKey("PERSON"));
    }

    [Fact]
    public void WithEntityTypes_RejectsAnEmptyMap()
    {
        Assert.Throws<ArgumentException>(
            () => PresidioOptions.ForSelfHosted(s_endpoint).WithEntityTypes(new Dictionary<string, EntityType>()));
    }

    [Fact]
    public void TheDefaultMapCoversTheAnalyzerNamesTheGatewayHasATypeFor()
    {
        var options = PresidioOptions.ForSelfHosted(s_endpoint);

        Assert.Equal(EntityType.Person, options.EntityTypes["PERSON"]);
        Assert.Equal(EntityType.Address, options.EntityTypes["LOCATION"]);
        Assert.Equal(EntityType.Iban, options.EntityTypes["IBAN_CODE"]);
        Assert.Equal(EntityType.TaxId, options.EntityTypes["IT_FISCAL_CODE"]);
    }
}
