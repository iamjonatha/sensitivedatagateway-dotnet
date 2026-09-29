using System;

namespace SensitiveDataGateway.Core.Tests;

public sealed class GatewayErrorTests
{
    [Fact]
    public void Create_KeepsTheCodeAndTheDescription()
    {
        var error = GatewayError.Create(
            GatewayErrorCategory.ExtractionFailed,
            "extraction.unsupported_media_type",
            "No registered extractor handles the declared media type.");

        Assert.Equal(GatewayErrorCategory.ExtractionFailed, error.Category);
        Assert.Equal("extraction.unsupported_media_type", error.Code);
        Assert.Null(error.BundleId);
        Assert.Null(error.PartId);
        Assert.Null(error.SegmentId);
        Assert.Null(error.EntityId);
    }

    [Fact]
    public void Create_RejectsAnUndefinedCategory() =>
        Assert.Throws<ArgumentException>(() => GatewayError.Create((GatewayErrorCategory)999, "code", "text"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("with\ncontrol")]
    public void Create_RejectsACodeThatIsNotStructural(string code) =>
        Assert.Throws<ArgumentException>(() =>
            GatewayError.Create(GatewayErrorCategory.ExtractionFailed, code, "text"));

    [Fact]
    public void Create_RejectsADescriptionLongerThanTheMaximum()
    {
        var description = new string('a', GatewayError.MaxDescriptionLength + 1);

        Assert.Throws<ArgumentException>(() =>
            GatewayError.Create(GatewayErrorCategory.ExtractionFailed, "code", description));
    }

    [Fact]
    public void Locators_LeaveTheOriginalErrorUnchanged()
    {
        var error = Error();

        var located = error.InBundle(BundleId.Create("case-1")).InPart(PartId.Create("part-1"));

        Assert.Null(error.BundleId);
        Assert.Equal(BundleId.Create("case-1"), located.BundleId);
        Assert.Equal(PartId.Create("part-1"), located.PartId);
    }

    [Fact]
    public void Locators_RejectAnIdentifierThatCarriesNoValue()
    {
        var error = Error();

        Assert.Throws<ArgumentException>(() => error.InBundle(default));
        Assert.Throws<ArgumentException>(() => error.InPart(default));
        Assert.Throws<ArgumentException>(() => error.InSegment(default));
        Assert.Throws<ArgumentException>(() => error.ForEntity(default));
    }

    [Fact]
    public void ToString_JoinsTheCodeAndTheDescription() =>
        Assert.Equal("extraction.malformed: The part could not be parsed.", Error().ToString());

    private static GatewayError Error() => GatewayError.Create(
        GatewayErrorCategory.ExtractionFailed,
        "extraction.malformed",
        "The part could not be parsed.");
}

public sealed class GatewayExceptionTests
{
    [Fact]
    public void Exception_CarriesTheErrorItWasGiven()
    {
        var error = Error(GatewayErrorCategory.ExtractionFailed);

        var exception = new ExtractionException(error);

        Assert.Same(error, exception.Error);
        Assert.Equal(error.ToString(), exception.Message);
    }

    [Fact]
    public void Exception_KeepsTheFailureThatCausedIt()
    {
        var inner = new InvalidOperationException();

        var exception = new ProviderException(Error(GatewayErrorCategory.ProviderFailed), inner);

        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void Exception_RejectsAnErrorOfAnotherCategory() =>
        Assert.Throws<ArgumentException>(() => new ExtractionException(Error(GatewayErrorCategory.ProviderFailed)));

    [Fact]
    public void Exception_RejectsANullError() =>
        Assert.Throws<ArgumentNullException>(() => new ExtractionException(null!));

    [Fact]
    public void EveryCategoryWithAnExceptionTypeCanBeRaised()
    {
        Assert.IsType<ExtractionException>(new ExtractionException(Error(GatewayErrorCategory.ExtractionFailed)));
        Assert.IsType<DetectionException>(new DetectionException(Error(GatewayErrorCategory.DetectionFailed)));
        Assert.IsType<SanitizationException>(new SanitizationException(Error(GatewayErrorCategory.SanitizationFailed)));
        Assert.IsType<PrivacyValidationException>(
            new PrivacyValidationException(Error(GatewayErrorCategory.ValidationFailed)));
        Assert.IsType<EgressBlockedException>(new EgressBlockedException(Error(GatewayErrorCategory.EgressBlocked)));
        Assert.IsType<ProcessingLimitExceededException>(
            new ProcessingLimitExceededException(Error(GatewayErrorCategory.LimitExceeded)));
        Assert.IsType<ProviderException>(new ProviderException(Error(GatewayErrorCategory.ProviderFailed)));
        Assert.IsType<PolicyConfigurationException>(
            new PolicyConfigurationException(Error(GatewayErrorCategory.ConfigurationInvalid)));
    }

    private static GatewayError Error(GatewayErrorCategory category) =>
        GatewayError.Create(category, "test.code", "A description that quotes nothing.");
}
