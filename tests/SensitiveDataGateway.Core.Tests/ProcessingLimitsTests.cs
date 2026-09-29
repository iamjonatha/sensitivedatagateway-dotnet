using System;

namespace SensitiveDataGateway.Core.Tests;

public sealed class ProcessingLimitsTests
{
    [Fact]
    public void Default_IsUsable()
    {
        var limits = ProcessingLimits.Default;

        Assert.True(limits.MaxBundleBytes > 0);
        Assert.True(limits.MaxPartBytes <= limits.MaxBundleBytes);
        Assert.True(limits.MaxProcessingTime > TimeSpan.Zero);
    }

    [Fact]
    public void Create_KeepsTheValuesItWasGiven()
    {
        var limits = ProcessingLimits.Create(1024, 512, 4, 2048, 16, TimeSpan.FromSeconds(30));

        Assert.Equal(1024, limits.MaxBundleBytes);
        Assert.Equal(512, limits.MaxPartBytes);
        Assert.Equal(4, limits.MaxPartCount);
        Assert.Equal(2048, limits.MaxExtractedCharacters);
        Assert.Equal(16, limits.MaxEntities);
        Assert.Equal(TimeSpan.FromSeconds(30), limits.MaxProcessingTime);
    }

    [Fact]
    public void Create_RejectsAPartLimitLargerThanTheBundleLimit() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProcessingLimits.Create(512, 1024, 4, 2048, 16, TimeSpan.FromSeconds(30)));

    [Theory]
    [InlineData(0, 1, 1, 1, 1)]
    [InlineData(1, 0, 1, 1, 1)]
    [InlineData(1, 1, 0, 1, 1)]
    [InlineData(1, 1, 1, 0, 1)]
    [InlineData(1, 1, 1, 1, 0)]
    public void Create_RejectsALimitThatIsNotPositive(
        long bundleBytes,
        long partBytes,
        int partCount,
        int characters,
        int entities) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProcessingLimits.Create(
                bundleBytes,
                partBytes,
                partCount,
                characters,
                entities,
                TimeSpan.FromSeconds(1)));

    [Fact]
    public void Create_RejectsADurationThatIsNotPositive() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ProcessingLimits.Create(1024, 512, 4, 2048, 16, TimeSpan.Zero));
}
