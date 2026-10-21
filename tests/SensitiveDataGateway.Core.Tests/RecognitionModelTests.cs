using System;
using System.Collections.Generic;
using SensitiveDataGateway.Documents.Recognition;

namespace SensitiveDataGateway.Core.Tests;

public sealed class RecognizerVersionTests
{
    [Theory]
    [InlineData(TrustBoundaryLocality.InProcess, "tesseract@5.3/in-process")]
    [InlineData(TrustBoundaryLocality.SelfHosted, "tesseract@5.3/self-hosted")]
    [InlineData(TrustBoundaryLocality.Remote, "tesseract@5.3/remote")]
    public void Spells_out_where_the_recognizer_runs(TrustBoundaryLocality locality, string expected)
    {
        var version = RecognizerVersion.Create(RecognizerId.Create("tesseract"), "5.3", locality);

        Assert.Equal(expected, version.ToString());
    }

    [Fact]
    public void Refuses_a_recognizer_that_does_not_say_where_it_runs()
    {
        Assert.Throws<ArgumentException>(
            () => RecognizerVersion.Create(RecognizerId.Create("tesseract"), "5.3", (TrustBoundaryLocality)0));
    }

    [Theory]
    [InlineData(TrustBoundaryLocality.InProcess, true)]
    [InlineData(TrustBoundaryLocality.SelfHosted, true)]
    [InlineData(TrustBoundaryLocality.Remote, false)]
    public void Knows_which_localities_are_inside_the_boundary(TrustBoundaryLocality locality, bool inside)
    {
        var version = RecognizerVersion.Create(RecognizerId.Create("tesseract"), "5.3", locality);

        Assert.Equal(inside, version.IsInsideTheTrustBoundary);
    }

    [Fact]
    public void Tells_two_localities_of_the_same_engine_apart()
    {
        var local = RecognizerVersion.Create(RecognizerId.Create("ocr"), "1", TrustBoundaryLocality.InProcess);
        var remote = RecognizerVersion.Create(RecognizerId.Create("ocr"), "1", TrustBoundaryLocality.Remote);

        Assert.NotEqual(local, remote);
    }

    [Fact]
    public void Carries_nothing_when_it_is_the_default_value()
    {
        var version = default(RecognizerVersion);

        Assert.True(version.IsDefault);
        Assert.Equal(string.Empty, version.ToString());
        Assert.Throws<InvalidOperationException>(() => version.Version);
    }
}

public sealed class RecognizedTextTests
{
    [Fact]
    public void Reports_the_lowest_confidence_and_not_the_average()
    {
        var text = RecognizedText.Create(
        [
            RecognizedLine.Create("invoice 4471", 1.0, 1),
            RecognizedLine.Create("total 180,00", 1.0, 2),
            RecognizedLine.Create("the number nobody could read", 0.2, 3),
        ]);

        Assert.Equal(0.2, text.LowestConfidence, precision: 6);
    }

    [Fact]
    public void Reports_full_confidence_for_an_empty_result()
    {
        Assert.True(RecognizedText.Empty().IsEmpty);
        Assert.Equal(1.0, RecognizedText.Empty().LowestConfidence, precision: 6);
    }

    [Fact]
    public void Refuses_lines_that_are_not_numbered_in_reading_order()
    {
        var lines = new List<RecognizedLine>
        {
            RecognizedLine.Create("invoice 4471", 1.0, 1),
            RecognizedLine.Create("total 180,00", 1.0, 3),
        };

        Assert.Throws<ArgumentException>(() => RecognizedText.Create(lines));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void Refuses_a_confidence_outside_the_scale(double confidence)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RecognizedLine.Create("invoice 4471", confidence, 1));
    }

    [Fact]
    public void Describes_a_result_without_quoting_what_it_read()
    {
        var text = RecognizedText.Create([RecognizedLine.Create("Beneficiary: Jonas Weber", 0.9, 1)]);

        Assert.DoesNotContain("Jonas", text.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Jonas", text.Lines[0].ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
