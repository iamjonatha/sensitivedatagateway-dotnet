using System;

namespace SensitiveDataGateway.Documents.Pdf;

/// <summary>
/// The bounds a <see cref="PdfExtractor"/> refuses to cross.
/// </summary>
/// <remarks>
/// <para>
/// An extractor is handed one part and a cancellation token, and not the policy, so the
/// bounds that belong to a single part live here rather than in <see cref="ProcessingLimits"/>.
/// They are bounds on the work one document can cause, not on what the pipeline admits: a
/// part that is too large has already been refused by the reader that produced the bundle.
/// </para>
/// <para>
/// The type is immutable. Each method returns a new instance, so options cannot be changed
/// after an extractor was built from them.
/// </para>
/// </remarks>
public sealed class PdfExtractorOptions
{
    /// <summary>
    /// The smallest page count that may be configured.
    /// </summary>
    public const int MinimumPageCount = 1;

    /// <summary>
    /// The largest page count that may be configured.
    /// </summary>
    /// <remarks>
    /// The ceiling exists because every page costs a parse of its content stream, and a
    /// document that declares a hundred thousand pages is a denial of service rather than a
    /// document.
    /// </remarks>
    public const int MaximumPageCount = 50_000;

    /// <summary>
    /// The smallest document size that may be configured, in bytes.
    /// </summary>
    public const long MinimumBytes = 1024;

    private PdfExtractorOptions(int maxPageCount, long maxBytes)
    {
        MaxPageCount = maxPageCount;
        MaxBytes = maxBytes;
    }

    /// <summary>
    /// Gets the options an extractor uses when the caller states none.
    /// </summary>
    /// <value>Two thousand pages, and sixty-four megabytes.</value>
    public static PdfExtractorOptions Default { get; } = new(maxPageCount: 2_000, maxBytes: 64L * 1024 * 1024);

    /// <summary>
    /// Gets the largest number of pages the extractor reads.
    /// </summary>
    /// <remarks>
    /// A longer document is refused, not truncated. Reading the first pages of a document and
    /// reporting success would transmit a document whose remaining pages nothing scanned.
    /// </remarks>
    public int MaxPageCount { get; }

    /// <summary>
    /// Gets the largest document the extractor opens, in bytes.
    /// </summary>
    /// <remarks>
    /// The size is checked before the parser is given the bytes, because a bound checked
    /// afterwards has already cost what it was meant to prevent.
    /// </remarks>
    public long MaxBytes { get; }

    /// <summary>
    /// Returns options with a different page bound.
    /// </summary>
    /// <param name="pages">The largest number of pages the extractor reads.</param>
    /// <returns>The new options.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="pages"/> is outside <see cref="MinimumPageCount"/> and
    /// <see cref="MaximumPageCount"/>.
    /// </exception>
    public PdfExtractorOptions WithMaxPageCount(int pages)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pages, MinimumPageCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pages, MaximumPageCount);

        return new PdfExtractorOptions(pages, MaxBytes);
    }

    /// <summary>
    /// Returns options with a different size bound.
    /// </summary>
    /// <param name="bytes">The largest document the extractor opens, in bytes.</param>
    /// <returns>The new options.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="bytes"/> is below <see cref="MinimumBytes"/>.
    /// </exception>
    public PdfExtractorOptions WithMaxBytes(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bytes, MinimumBytes);

        return new PdfExtractorOptions(MaxPageCount, bytes);
    }
}
