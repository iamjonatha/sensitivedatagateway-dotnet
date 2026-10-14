using System;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// The bounds the detection stage runs under.
/// </summary>
/// <remarks>
/// Both bounds exist to make a hostile document fail rather than hang. Exceeding either is
/// recorded as a gap in the scan, which blocks the document: the stage never responds to
/// pressure by scanning less and saying nothing about it.
/// </remarks>
public sealed class DetectionPipelineOptions
{
    /// <summary>
    /// The shortest time a detector may be given.
    /// </summary>
    public static readonly TimeSpan MinimumTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// The longest time a detector may be given.
    /// </summary>
    public static readonly TimeSpan MaximumTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The fewest candidates a detector may be allowed to report.
    /// </summary>
    public const int MinimumCandidates = 16;

    /// <summary>
    /// The most candidates a detector may be allowed to report.
    /// </summary>
    public const int MaximumCandidates = 1_000_000;

    private DetectionPipelineOptions(TimeSpan detectorTimeout, int maxCandidatesPerDetector)
    {
        DetectorTimeout = detectorTimeout;
        MaxCandidatesPerDetector = maxCandidatesPerDetector;
    }

    /// <summary>
    /// Gets the bounds the library uses when none are given.
    /// </summary>
    public static DetectionPipelineOptions Default { get; } =
        new(TimeSpan.FromSeconds(30), maxCandidatesPerDetector: 10_000);

    /// <summary>
    /// Gets how long one detector may spend on one document.
    /// </summary>
    public TimeSpan DetectorTimeout { get; }

    /// <summary>
    /// Gets how many candidates one detector may report before its scan is treated as failed.
    /// </summary>
    /// <remarks>
    /// A detector that reports tens of thousands of candidates has stopped discriminating,
    /// usually because the document was built to make it do so. Its findings are dropped and
    /// the gap is recorded, because keeping a truncated list would claim a completeness the
    /// scan does not have.
    /// </remarks>
    public int MaxCandidatesPerDetector { get; }

    /// <summary>
    /// Returns a copy of these options with a different time budget.
    /// </summary>
    /// <param name="detectorTimeout">How long one detector may spend on one document.</param>
    /// <returns>The new options.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="detectorTimeout"/> is outside the allowed range.
    /// </exception>
    public DetectionPipelineOptions WithDetectorTimeout(TimeSpan detectorTimeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(detectorTimeout, MinimumTimeout);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(detectorTimeout, MaximumTimeout);

        return new DetectionPipelineOptions(detectorTimeout, MaxCandidatesPerDetector);
    }

    /// <summary>
    /// Returns a copy of these options with a different candidate bound.
    /// </summary>
    /// <param name="maxCandidatesPerDetector">How many candidates one detector may report.</param>
    /// <returns>The new options.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="maxCandidatesPerDetector"/> is outside the allowed range.
    /// </exception>
    public DetectionPipelineOptions WithMaxCandidatesPerDetector(int maxCandidatesPerDetector)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCandidatesPerDetector, MinimumCandidates);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxCandidatesPerDetector, MaximumCandidates);

        return new DetectionPipelineOptions(DetectorTimeout, maxCandidatesPerDetector);
    }
}
