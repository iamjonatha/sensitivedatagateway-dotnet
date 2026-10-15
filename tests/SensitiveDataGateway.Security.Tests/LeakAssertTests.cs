using System;
using System.Collections.Generic;
using SensitiveDataGateway.Testing;

namespace SensitiveDataGateway.Security.Tests;

/// <summary>
/// Proves that the instrument the rest of this suite relies on actually works.
/// </summary>
/// <remarks>
/// Every other test here passes when <see cref="LeakAssert"/> finds nothing. An assertion
/// that can never fail would make all of them pass for free, so the scenarios below make it
/// fail on purpose, and check that it fails without repeating the value it found.
/// </remarks>
public sealed class LeakAssertTests
{
    private const string Value = "ada.lovelace@example.com";

    [Fact]
    public void A_surviving_value_is_reported()
    {
        var failure = Assert.Throws<LeakDetectedException>(
            () => LeakAssert.NoneIn([$"Write to {Value} today."], [Value]));

        var leak = Assert.Single(failure.Leaks);

        Assert.Equal("line 0", leak.Location);
        Assert.Equal(0, leak.ValueIndex);
        Assert.Equal(Value.Length, leak.ValueLength);
    }

    /// <summary>
    /// The report of a leak does not become a second leak.
    /// </summary>
    /// <remarks>
    /// A failure message is written to a console, a log file and a build artifact. Repeating
    /// the value there would publish it in three more places than the test was complaining
    /// about.
    /// </remarks>
    [Fact]
    public void A_report_never_repeats_the_value()
    {
        var failure = Assert.Throws<LeakDetectedException>(
            () => LeakAssert.NoneIn([$"Write to {Value} today."], [Value]));

        Assert.DoesNotContain(Value, failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Value, failure.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Value, Assert.Single(failure.Leaks).ToString(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A value broken up by white space is still the value.
    /// </summary>
    /// <remarks>
    /// Wrapping, column alignment and some extractors insert white space inside a number.
    /// Searching only for the literal form would let the most common accidental mangling
    /// through.
    /// </remarks>
    [Theory]
    [InlineData("GB71 NWBK 9999 9999 999999", "GB71NWBK99999999999999")]
    [InlineData("4111\n1111\n1111\n1111", "4111111111111111")]
    public void A_value_split_by_white_space_is_still_found(string line, string value)
    {
        var failure = Assert.Throws<LeakDetectedException>(
            () => LeakAssert.NoneIn([line], [value]));

        Assert.NotEmpty(failure.Leaks);
    }

    [Fact]
    public void A_clean_line_passes()
    {
        LeakAssert.NoneIn([$"Write to [EMAIL_001] today."], [Value]);
    }

    /// <summary>
    /// A value too short to search for is refused rather than skipped.
    /// </summary>
    /// <remarks>
    /// Skipping it would leave a test that reports success over a value it never looked at,
    /// which is worse than no test at all.
    /// </remarks>
    [Theory]
    [InlineData("ab")]
    [InlineData("abc")]
    public void A_value_shorter_than_the_minimum_is_refused(string value)
    {
        _ = Assert.Throws<ArgumentException>(
            () => LeakAssert.NoneIn(["Nothing to see."], [value]));
    }

    [Fact]
    public void A_missing_argument_is_refused()
    {
        _ = Assert.Throws<ArgumentNullException>(
            () => LeakAssert.NoneIn(null!, [Value]));

        _ = Assert.Throws<ArgumentNullException>(
            () => LeakAssert.NoneIn(["Nothing to see."], null!));

        _ = Assert.Throws<ArgumentException>(
            () => LeakAssert.NoneIn(["Nothing to see."], new List<string> { "   " }));
    }
}
