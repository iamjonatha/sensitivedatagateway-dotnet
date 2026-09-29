using System;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// The record that part of the document was not scanned.
/// </summary>
/// <remarks>
/// <para>
/// A detector that fails is not a detector that found nothing. The distinction is the whole
/// point of recording this: "clean" and "unexamined" lead to opposite decisions, and a
/// pipeline that conflates them transmits exactly the documents it should refuse.
/// </para>
/// <para>
/// The record names the detector, the reason and the segment it was working on. It carries no
/// text, so it is safe to log in full.
/// </para>
/// </remarks>
public sealed class DetectionFailure
{
    private DetectionFailure(DetectorId detectorId, DetectionFailureReason reason, SegmentId? segmentId)
    {
        DetectorId = detectorId;
        Reason = reason;
        SegmentId = segmentId;
    }

    /// <summary>
    /// Gets the detector that did not finish.
    /// </summary>
    public DetectorId DetectorId { get; }

    /// <summary>
    /// Gets why it did not finish.
    /// </summary>
    public DetectionFailureReason Reason { get; }

    /// <summary>
    /// Gets the segment it was working on, when the failure is confined to one.
    /// </summary>
    /// <value>
    /// The segment, or <see langword="null"/> when the detector failed for the whole
    /// document. A failure with no segment means nothing was scanned by that detector.
    /// </value>
    public SegmentId? SegmentId { get; }

    /// <summary>
    /// Creates a failure record for a whole document.
    /// </summary>
    /// <param name="detectorId">The detector that did not finish.</param>
    /// <param name="reason">Why it did not finish.</param>
    /// <returns>The failure record.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="detectorId"/> is the default value, or <paramref name="reason"/> is undefined.
    /// </exception>
    public static DetectionFailure Create(DetectorId detectorId, DetectionFailureReason reason) =>
        Create(detectorId, reason, segmentId: null);

    /// <summary>
    /// Creates a failure record.
    /// </summary>
    /// <param name="detectorId">The detector that did not finish.</param>
    /// <param name="reason">Why it did not finish.</param>
    /// <param name="segmentId">The segment it was working on, or <see langword="null"/>.</param>
    /// <returns>The failure record.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="detectorId"/> is the default value, <paramref name="reason"/> is
    /// undefined, or <paramref name="segmentId"/> carries no value.
    /// </exception>
    public static DetectionFailure Create(
        DetectorId detectorId,
        DetectionFailureReason reason,
        SegmentId? segmentId)
    {
        if (detectorId.IsDefault)
        {
            throw new ArgumentException("The detector identifier carries no value.", nameof(detectorId));
        }

        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentException("The failure reason is not a defined value.", nameof(reason));
        }

        if (segmentId is { IsDefault: true })
        {
            throw new ArgumentException("The segment identifier carries no value.", nameof(segmentId));
        }

        return new DetectionFailure(detectorId, reason, segmentId);
    }

    /// <summary>
    /// Returns a description of the failure.
    /// </summary>
    /// <returns>The detector, the reason and the segment. It carries no document content.</returns>
    public override string ToString() =>
        SegmentId is { } segment
            ? $"{DetectorId}: {Reason} ({segment})"
            : $"{DetectorId}: {Reason}";
}
