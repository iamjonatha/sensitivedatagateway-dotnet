using System;

namespace SensitiveDataGateway.Policies;

/// <summary>
/// The form of <see cref="ProcessingLimits"/> that configuration binds to.
/// </summary>
/// <remarks>
/// Every member is optional and falls back to <see cref="ProcessingLimits.Default"/>, so a
/// configuration file states only the bounds it wants to tighten.
/// </remarks>
public sealed class ProcessingLimitsDescription
{
    /// <summary>
    /// Gets or sets the greatest total size of a bundle, in bytes.
    /// </summary>
    public long? MaxBundleBytes { get; set; }

    /// <summary>
    /// Gets or sets the greatest size of one part, in bytes.
    /// </summary>
    public long? MaxPartBytes { get; set; }

    /// <summary>
    /// Gets or sets the greatest number of parts a bundle may contain.
    /// </summary>
    public int? MaxPartCount { get; set; }

    /// <summary>
    /// Gets or sets the greatest number of characters extraction may produce.
    /// </summary>
    public int? MaxExtractedCharacters { get; set; }

    /// <summary>
    /// Gets or sets the greatest number of entities detection may report.
    /// </summary>
    public int? MaxEntities { get; set; }

    /// <summary>
    /// Gets or sets the greatest time one processing operation may take.
    /// </summary>
    public TimeSpan? MaxProcessingTime { get; set; }

    /// <summary>
    /// Describes a set of limits.
    /// </summary>
    /// <param name="limits">The limits.</param>
    /// <returns>The description.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="limits"/> is <see langword="null"/>.</exception>
    public static ProcessingLimitsDescription FromLimits(ProcessingLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        return new ProcessingLimitsDescription
        {
            MaxBundleBytes = limits.MaxBundleBytes,
            MaxPartBytes = limits.MaxPartBytes,
            MaxPartCount = limits.MaxPartCount,
            MaxExtractedCharacters = limits.MaxExtractedCharacters,
            MaxEntities = limits.MaxEntities,
            MaxProcessingTime = limits.MaxProcessingTime,
        };
    }

    /// <summary>
    /// Produces the limits this description denotes.
    /// </summary>
    /// <returns>The limits.</returns>
    /// <exception cref="PolicyConfigurationException">
    /// A bound is not positive, or the per-part size exceeds the per-bundle size.
    /// </exception>
    public ProcessingLimits ToLimits()
    {
        var fallback = ProcessingLimits.Default;
        var bundleBytes = Positive(MaxBundleBytes, fallback.MaxBundleBytes);
        var partBytes = Positive(MaxPartBytes, fallback.MaxPartBytes);
        var characters = Positive(MaxExtractedCharacters, fallback.MaxExtractedCharacters);
        var entities = Positive(MaxEntities, fallback.MaxEntities);
        var partCount = Positive(MaxPartCount, fallback.MaxPartCount);
        var processingTime = MaxProcessingTime ?? fallback.MaxProcessingTime;

        if (partBytes > bundleBytes)
        {
            throw PolicyConfigurationFailure.Invalid(
                "policy.part_limit_exceeds_bundle_limit",
                "The per-part size limit cannot be greater than the per-bundle size limit.");
        }

        if (processingTime <= TimeSpan.Zero)
        {
            throw NotPositive();
        }

        return ProcessingLimits.Create(bundleBytes, partBytes, partCount, characters, entities, processingTime);
    }

    private static PolicyConfigurationException NotPositive() =>
        PolicyConfigurationFailure.Invalid(
            "policy.limit_not_positive",
            "Every processing limit must be a positive value.");

    private static long Positive(long? value, long fallback)
    {
        var result = value ?? fallback;

        return result > 0 ? result : throw NotPositive();
    }

    private static int Positive(int? value, int fallback)
    {
        var result = value ?? fallback;

        return result > 0 ? result : throw NotPositive();
    }
}
