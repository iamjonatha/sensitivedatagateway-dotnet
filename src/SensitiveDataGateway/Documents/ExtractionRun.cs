using System;
using System.Collections.Generic;

namespace SensitiveDataGateway.Documents;

/// <summary>
/// Accumulates the result of reading one bundle.
/// </summary>
/// <remarks>
/// The run owns the reading order within a part, because it is the only component that sees
/// both the metadata segments it emits itself and the segments the extractor returns.
/// </remarks>
internal sealed class ExtractionRun
{
    private readonly ProcessingLimits _limits;
    private readonly List<TextSegment> _segments = [];
    private readonly List<ExtractionFailure> _failures = [];
    private readonly List<ExtractorVersion> _extractors = [];
    private long _declaredBytes;
    private long _characters;
    private int _partCount;
    private int _orderInPart;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractionRun"/> class.
    /// </summary>
    /// <param name="limits">The bounds the run refuses to cross.</param>
    public ExtractionRun(ProcessingLimits limits) => _limits = limits;

    /// <summary>
    /// Admits one more part and resets the per-part reading order.
    /// </summary>
    /// <param name="part">The part about to be read.</param>
    /// <exception cref="ProcessingLimitExceededException">
    /// The bundle has more parts, or declares more bytes, than the policy allows.
    /// </exception>
    public void BeginPart(DocumentPart part)
    {
        _orderInPart = 0;
        _partCount++;

        if (_partCount > _limits.MaxPartCount)
        {
            throw new ProcessingLimitExceededException(ExtractionErrors.TooManyParts(_limits.MaxPartCount));
        }

        _declaredBytes += part.Content.Length ?? 0;

        if (_declaredBytes > _limits.MaxBundleBytes)
        {
            throw new ProcessingLimitExceededException(ExtractionErrors.BundleTooLarge(_limits.MaxBundleBytes));
        }
    }

    /// <summary>
    /// Decides whether a part is small enough to read.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <returns>
    /// <see langword="true"/> when the part may be read; <see langword="false"/> when it was
    /// recorded as a failure instead.
    /// </returns>
    /// <remarks>
    /// A stream of undeclared size is refused rather than read, because a bound that is only
    /// checked after the read has already cost what it was meant to prevent.
    /// </remarks>
    public bool TryAdmit(DocumentPart part)
    {
        if (part.Content.Length is not { } declared)
        {
            Fail(part, ExtractionFailureReason.LimitExceeded);
            return false;
        }

        if (declared > _limits.MaxPartBytes)
        {
            Fail(part, ExtractionFailureReason.LimitExceeded);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Adds a segment carrying one piece of metadata.
    /// </summary>
    /// <param name="part">The part the metadata belongs to.</param>
    /// <param name="name">The name of the field, which becomes part of the segment identifier.</param>
    /// <param name="value">The value, which is content and is scanned like any other.</param>
    /// <exception cref="ProcessingLimitExceededException">
    /// The bundle yields more text than the policy allows.
    /// </exception>
    public void AddMetadata(DocumentPart part, string name, string value) =>
        Add(TextSegment.Create(
            SegmentIds.Metadata(part.Id, name),
            part.Id,
            value,
            SegmentOrigin.Named(SegmentOriginKind.Metadata, name),
            _orderInPart));

    /// <summary>
    /// Adds the segments an extractor produced, renumbered into the part's reading order.
    /// </summary>
    /// <param name="version">The extractor that produced them.</param>
    /// <param name="segments">The segments.</param>
    /// <exception cref="ProcessingLimitExceededException">
    /// The bundle yields more text than the policy allows.
    /// </exception>
    public void AddExtracted(ExtractorVersion version, IReadOnlyList<TextSegment> segments)
    {
        if (!_extractors.Contains(version))
        {
            _extractors.Add(version);
        }

        foreach (var segment in segments)
        {
            Add(TextSegment.Create(segment.Id, segment.PartId, segment.Text, segment.Origin, _orderInPart));
        }
    }

    /// <summary>
    /// Records that a part could not be read.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <param name="reason">Why it could not be read.</param>
    public void Fail(DocumentPart part, ExtractionFailureReason reason) =>
        _failures.Add(ExtractionFailure.Create(part.Id, reason, part.MediaType));

    /// <summary>
    /// Builds the extracted document.
    /// </summary>
    /// <param name="bundleId">The bundle that was read.</param>
    /// <returns>The document.</returns>
    /// <exception cref="ExtractionException">
    /// The bundle produced neither a segment nor a failure, which cannot be told apart from a
    /// clean document.
    /// </exception>
    public ExtractedDocument ToDocument(BundleId bundleId) =>
        _segments.Count == 0 && _failures.Count == 0
            ? throw new ExtractionException(ExtractionErrors.NothingExtracted(bundleId))
            : ExtractedDocument.Create(bundleId, _segments, _failures, _extractors);

    private void Add(TextSegment segment)
    {
        _characters += segment.Text.Length;

        if (_characters > _limits.MaxExtractedCharacters)
        {
            throw new ProcessingLimitExceededException(ExtractionErrors.TooMuchText(_limits.MaxExtractedCharacters));
        }

        _segments.Add(segment);
        _orderInPart++;
    }
}
