using System;

namespace SensitiveDataGateway.Documents;

/// <summary>
/// The record that a part of the bundle could not be read.
/// </summary>
/// <remarks>
/// <para>
/// Silence and failure are different facts. An extractor that cannot handle a part records a
/// failure instead of returning empty text, because empty text would be scanned, found
/// clean, and allowed through.
/// </para>
/// <para>
/// A failure names the part, the reason and the declared media type, and nothing else. It is
/// safe to log in full.
/// </para>
/// </remarks>
public sealed class ExtractionFailure
{
    private ExtractionFailure(PartId partId, ExtractionFailureReason reason, string mediaType)
    {
        PartId = partId;
        Reason = reason;
        MediaType = mediaType;
    }

    /// <summary>
    /// Gets the part that could not be read.
    /// </summary>
    public PartId PartId { get; }

    /// <summary>
    /// Gets why it could not be read.
    /// </summary>
    public ExtractionFailureReason Reason { get; }

    /// <summary>
    /// Gets the media type the part declared.
    /// </summary>
    public string MediaType { get; }

    /// <summary>
    /// Creates a failure record.
    /// </summary>
    /// <param name="partId">The part that could not be read.</param>
    /// <param name="reason">Why it could not be read.</param>
    /// <param name="mediaType">The media type the part declared.</param>
    /// <returns>The failure record.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mediaType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="partId"/> is the default value, <paramref name="reason"/> is undefined,
    /// or the media type is invalid.
    /// </exception>
    public static ExtractionFailure Create(
        PartId partId,
        ExtractionFailureReason reason,
        string mediaType)
    {
        if (partId.IsDefault)
        {
            throw new ArgumentException("The part identifier carries no value.", nameof(partId));
        }

        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentException("The failure reason is not a defined value.", nameof(reason));
        }

        return new ExtractionFailure(
            partId,
            reason,
            Text.Require(mediaType, DocumentPart.MaxMediaTypeLength, nameof(mediaType)).ToLowerInvariant());
    }

    /// <summary>
    /// Returns a description of the failure.
    /// </summary>
    /// <returns>The part, the reason and the media type. It carries no document content.</returns>
    public override string ToString() => $"{PartId}: {Reason} ({MediaType})";
}
