using System;

namespace SensitiveDataGateway.Core.Tests;

public sealed class SourceSpanTests
{
    private static readonly SegmentId s_segment = SegmentId.Create("s_segment-1");
    private static readonly SegmentId s_otherSegment = SegmentId.Create("s_segment-2");

    [Fact]
    public void Create_KeepsTheRangeItWasGiven()
    {
        var span = SourceSpan.Create(s_segment, 10, 5);

        Assert.Equal(s_segment, span.SegmentId);
        Assert.Equal(10, span.Start);
        Assert.Equal(5, span.Length);
        Assert.Equal(15, span.End);
    }

    [Fact]
    public void Create_AcceptsAnEmptyRange()
    {
        var span = SourceSpan.Create(s_segment, 3, 0);

        Assert.True(span.IsEmpty);
        Assert.Equal(3, span.End);
    }

    [Fact]
    public void Create_RejectsASpanThatIsNotAnchoredToASegment() =>
        Assert.Throws<ArgumentException>(() => SourceSpan.Create(default, 0, 1));

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void Create_RejectsANegativeOffset(int start, int length) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SourceSpan.Create(s_segment, start, length));

    [Fact]
    public void Create_RejectsARangeThatWouldOverflow() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => SourceSpan.Create(s_segment, int.MaxValue - 1, 2));

    [Fact]
    public void IsWithin_AcceptsASpanThatEndsExactlyAtTheEndOfTheText()
    {
        var span = SourceSpan.Create(s_segment, 4, 6);

        Assert.True(span.IsWithin(10));
    }

    [Fact]
    public void IsWithin_RejectsASpanThatRunsPastTheEndOfTheText()
    {
        var span = SourceSpan.Create(s_segment, 4, 7);

        Assert.False(span.IsWithin(10));
    }

    [Fact]
    public void IsWithin_RejectsANegativeTextLength()
    {
        var span = SourceSpan.Create(s_segment, 0, 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => span.IsWithin(-1));
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(14, true)]
    [InlineData(15, false)]
    public void Contains_TreatsTheRangeAsHalfOpen(int position, bool expected)
    {
        var span = SourceSpan.Create(s_segment, 10, 5);

        Assert.Equal(expected, span.Contains(position));
    }

    [Fact]
    public void Contains_IsAlwaysFalseForAnEmptySpan()
    {
        var span = SourceSpan.Create(s_segment, 10, 0);

        Assert.False(span.Contains(10));
    }

    [Fact]
    public void Contains_AcceptsASpanThatLiesInside()
    {
        var outer = SourceSpan.Create(s_segment, 10, 10);
        var inner = SourceSpan.Create(s_segment, 12, 4);

        Assert.True(outer.Contains(inner));
    }

    [Fact]
    public void Contains_RejectsASpanInAnotherSegmentAtTheSameOffsets()
    {
        var outer = SourceSpan.Create(s_segment, 10, 10);
        var elsewhere = SourceSpan.Create(s_otherSegment, 12, 4);

        Assert.False(outer.Contains(elsewhere));
    }

    [Theory]
    [InlineData(0, 10, false)]
    [InlineData(0, 11, true)]
    [InlineData(19, 1, true)]
    [InlineData(20, 5, false)]
    public void Overlaps_IsTrueOnlyWhenAtLeastOneCharacterIsShared(int start, int length, bool expected)
    {
        var span = SourceSpan.Create(s_segment, 10, 10);
        var other = SourceSpan.Create(s_segment, start, length);

        Assert.Equal(expected, span.Overlaps(other));
    }

    [Fact]
    public void Overlaps_IsSymmetric()
    {
        var left = SourceSpan.Create(s_segment, 10, 10);
        var right = SourceSpan.Create(s_segment, 15, 10);

        Assert.Equal(left.Overlaps(right), right.Overlaps(left));
    }

    [Fact]
    public void Overlaps_IsAlwaysFalseForAnEmptySpan()
    {
        var span = SourceSpan.Create(s_segment, 10, 10);
        var empty = SourceSpan.Create(s_segment, 12, 0);

        Assert.False(span.Overlaps(empty));
        Assert.False(empty.Overlaps(span));
    }

    [Fact]
    public void Overlaps_RejectsASpanInAnotherSegment()
    {
        var span = SourceSpan.Create(s_segment, 10, 10);
        var elsewhere = SourceSpan.Create(s_otherSegment, 10, 10);

        Assert.False(span.Overlaps(elsewhere));
    }

    [Fact]
    public void TryIntersect_ReturnsTheSharedRange()
    {
        var left = SourceSpan.Create(s_segment, 10, 10);
        var right = SourceSpan.Create(s_segment, 15, 10);

        Assert.True(left.TryIntersect(right, out var intersection));
        Assert.Equal(15, intersection.Start);
        Assert.Equal(5, intersection.Length);
        Assert.Equal(s_segment, intersection.SegmentId);
    }

    [Fact]
    public void TryIntersect_ReturnsFalseWhenNothingIsShared()
    {
        var left = SourceSpan.Create(s_segment, 10, 5);
        var right = SourceSpan.Create(s_segment, 15, 5);

        Assert.False(left.TryIntersect(right, out var intersection));
        Assert.Equal(default, intersection);
    }

    [Fact]
    public void Equals_IsTrueForTheSameRangeOfTheSameSegment()
    {
        var left = SourceSpan.Create(s_segment, 2, 3);
        var right = SourceSpan.Create(s_segment, 2, 3);

        Assert.True(left == right);
        Assert.False(left != right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Equals_IsFalseForTheSameRangeOfAnotherSegment()
    {
        var left = SourceSpan.Create(s_segment, 2, 3);
        var right = SourceSpan.Create(s_otherSegment, 2, 3);

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void ToString_NamesTheLocationAndNeverTheText()
    {
        var span = SourceSpan.Create(s_segment, 2, 3);

        Assert.Equal("s_segment-1[2..5)", span.ToString());
    }
}
