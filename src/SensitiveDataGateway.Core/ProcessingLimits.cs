using System;

namespace SensitiveDataGateway;

/// <summary>
/// The size, count and time bounds a processing operation must stay inside.
/// </summary>
/// <remarks>
/// <para>
/// Every limit is a blocking limit. Exceeding one refuses the operation; it never truncates
/// and continues. A truncated document is a document whose unexamined tail was silently
/// dropped, and an unexamined tail is exactly where an attacker would put the payload.
/// </para>
/// <para>
/// Limits are part of the policy, not of the host configuration, so that the same policy
/// version always means the same bounds and a sanitization stays reproducible.
/// </para>
/// </remarks>
public sealed class ProcessingLimits
{
    private ProcessingLimits(
        long maxBundleBytes,
        long maxPartBytes,
        int maxPartCount,
        int maxExtractedCharacters,
        int maxEntities,
        TimeSpan maxProcessingTime)
    {
        MaxBundleBytes = maxBundleBytes;
        MaxPartBytes = maxPartBytes;
        MaxPartCount = maxPartCount;
        MaxExtractedCharacters = maxExtractedCharacters;
        MaxEntities = maxEntities;
        MaxProcessingTime = maxProcessingTime;
    }

    /// <summary>
    /// Gets the limits applied when a policy does not state its own.
    /// </summary>
    /// <value>
    /// 32 MiB per bundle, 16 MiB per part, 64 parts, 2,000,000 extracted characters, 50,000
    /// entities and two minutes.
    /// </value>
    /// <remarks>
    /// The numbers are a starting point sized for the reference case — an email with
    /// attachments — and are expected to be tightened by an application that knows what it
    /// receives. They are not a security boundary on their own; they bound the cost of
    /// processing something hostile.
    /// </remarks>
    public static ProcessingLimits Default { get; } = new(
        maxBundleBytes: 32L * 1024 * 1024,
        maxPartBytes: 16L * 1024 * 1024,
        maxPartCount: 64,
        maxExtractedCharacters: 2_000_000,
        maxEntities: 50_000,
        maxProcessingTime: TimeSpan.FromMinutes(2));

    /// <summary>
    /// Gets the greatest total size, in bytes, of all parts of a bundle.
    /// </summary>
    public long MaxBundleBytes { get; }

    /// <summary>
    /// Gets the greatest size, in bytes, of a single part.
    /// </summary>
    public long MaxPartBytes { get; }

    /// <summary>
    /// Gets the greatest number of parts a bundle may contain, counting nested parts.
    /// </summary>
    public int MaxPartCount { get; }

    /// <summary>
    /// Gets the greatest number of characters extraction may produce for a whole bundle.
    /// </summary>
    /// <remarks>
    /// This is the bound that matters against a decompression bomb: a small part can expand
    /// into an enormous amount of text, and the byte limits do not see it.
    /// </remarks>
    public int MaxExtractedCharacters { get; }

    /// <summary>
    /// Gets the greatest number of entities detection may report for a whole bundle.
    /// </summary>
    public int MaxEntities { get; }

    /// <summary>
    /// Gets the greatest time one processing operation may take.
    /// </summary>
    /// <remarks>
    /// The limit bounds the gateway's own work. It does not bound the provider call, which
    /// the caller controls with its own cancellation token.
    /// </remarks>
    public TimeSpan MaxProcessingTime { get; }

    /// <summary>
    /// Creates a set of limits.
    /// </summary>
    /// <param name="maxBundleBytes">The greatest total size of a bundle, in bytes.</param>
    /// <param name="maxPartBytes">The greatest size of one part, in bytes.</param>
    /// <param name="maxPartCount">The greatest number of parts.</param>
    /// <param name="maxExtractedCharacters">The greatest number of extracted characters.</param>
    /// <param name="maxEntities">The greatest number of detected entities.</param>
    /// <param name="maxProcessingTime">The greatest duration of one operation.</param>
    /// <returns>The limits.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A size or count is not positive, the per-part size exceeds the per-bundle size, or the
    /// duration is not positive.
    /// </exception>
    /// <remarks>
    /// An unusable set of limits is a configuration failure at construction, never a
    /// permissive default discovered halfway through an operation.
    /// </remarks>
    public static ProcessingLimits Create(
        long maxBundleBytes,
        long maxPartBytes,
        int maxPartCount,
        int maxExtractedCharacters,
        int maxEntities,
        TimeSpan maxProcessingTime)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBundleBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPartBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPartCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExtractedCharacters);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEntities);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxPartBytes, maxBundleBytes);

        if (maxProcessingTime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxProcessingTime),
                "The processing time limit must be positive.");
        }

        return new ProcessingLimits(
            maxBundleBytes,
            maxPartBytes,
            maxPartCount,
            maxExtractedCharacters,
            maxEntities,
            maxProcessingTime);
    }
}
