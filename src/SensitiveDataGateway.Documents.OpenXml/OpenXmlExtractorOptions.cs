using System;

namespace SensitiveDataGateway.Documents.OpenXml;

/// <summary>
/// The bounds an Open XML extractor stays inside.
/// </summary>
/// <remarks>
/// <para>
/// An Open XML document is a zip archive of XML parts, which means a small file can describe
/// a very large one. The bounds here are what stops a hostile package from consuming the
/// host: the byte ceiling applies to the archive as it is read, the character ceiling to
/// each part as the parser expands it, and the segment ceiling to what the extractor
/// produces.
/// </para>
/// <para>
/// The type is immutable. A bound that can be lowered after an extractor was built is a
/// bound two threads can disagree about.
/// </para>
/// </remarks>
public sealed class OpenXmlExtractorOptions
{
    /// <summary>
    /// The smallest byte ceiling that can be configured.
    /// </summary>
    public const long MinimumBytes = 1024;

    /// <summary>
    /// The smallest segment ceiling that can be configured.
    /// </summary>
    public const int MinimumSegmentCount = 1;

    /// <summary>
    /// The largest segment ceiling that can be configured.
    /// </summary>
    public const int MaximumSegmentCount = 1_000_000;

    private OpenXmlExtractorOptions(long maxBytes, int maxSegmentCount, long maxCharactersInPart)
    {
        MaxBytes = maxBytes;
        MaxSegmentCount = maxSegmentCount;
        MaxCharactersInPart = maxCharactersInPart;
    }

    /// <summary>
    /// Gets the bounds an extractor uses when the application states none.
    /// </summary>
    public static OpenXmlExtractorOptions Default { get; } = new(
        maxBytes: 64L * 1024 * 1024,
        maxSegmentCount: 200_000,
        maxCharactersInPart: 32L * 1024 * 1024);

    /// <summary>
    /// Gets the largest package the extractor reads.
    /// </summary>
    public long MaxBytes { get; }

    /// <summary>
    /// Gets the largest number of segments the extractor produces.
    /// </summary>
    public int MaxSegmentCount { get; }

    /// <summary>
    /// Gets the largest number of characters the parser expands a single part to.
    /// </summary>
    /// <remarks>
    /// This is handed to the Open XML SDK, which enforces it while reading rather than after.
    /// It is the defence against a part that is a few kilobytes compressed and gigabytes
    /// expanded.
    /// </remarks>
    public long MaxCharactersInPart { get; }

    /// <summary>
    /// Returns these options with a different byte ceiling.
    /// </summary>
    /// <param name="maxBytes">The new ceiling.</param>
    /// <returns>The new options.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBytes"/> is below <see cref="MinimumBytes"/>.</exception>
    public OpenXmlExtractorOptions WithMaxBytes(long maxBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, MinimumBytes);

        return new OpenXmlExtractorOptions(maxBytes, MaxSegmentCount, MaxCharactersInPart);
    }

    /// <summary>
    /// Returns these options with a different segment ceiling.
    /// </summary>
    /// <param name="maxSegmentCount">The new ceiling.</param>
    /// <returns>The new options.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxSegmentCount"/> is outside the allowed range.</exception>
    public OpenXmlExtractorOptions WithMaxSegmentCount(int maxSegmentCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSegmentCount, MinimumSegmentCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxSegmentCount, MaximumSegmentCount);

        return new OpenXmlExtractorOptions(MaxBytes, maxSegmentCount, MaxCharactersInPart);
    }

    /// <summary>
    /// Returns these options with a different character ceiling per part.
    /// </summary>
    /// <param name="maxCharactersInPart">The new ceiling.</param>
    /// <returns>The new options.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxCharactersInPart"/> is not positive.</exception>
    public OpenXmlExtractorOptions WithMaxCharactersInPart(long maxCharactersInPart)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCharactersInPart);

        return new OpenXmlExtractorOptions(MaxBytes, MaxSegmentCount, maxCharactersInPart);
    }
}
