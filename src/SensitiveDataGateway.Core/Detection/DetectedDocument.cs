using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// The extracted text together with everything the detectors believe is sensitive in it.
/// </summary>
/// <remarks>
/// This is the last stage that still carries the original text. The policy reads it, the
/// anonymizer rewrites from it, and nothing after that point has the words back.
/// </remarks>
public sealed class DetectedDocument
{
    private DetectedDocument(
        ExtractedDocument extracted,
        IReadOnlyList<SensitiveEntity> entities,
        IReadOnlyList<DetectorVersion> detectors,
        IReadOnlyList<DetectionFailure> failures)
    {
        Extracted = extracted;
        Entities = entities;
        Detectors = detectors;
        Failures = failures;
    }

    /// <summary>
    /// Gets the extracted text the detectors ran over.
    /// </summary>
    public ExtractedDocument Extracted { get; }

    /// <summary>
    /// Gets the processing operation this detection belongs to.
    /// </summary>
    public BundleId BundleId => Extracted.BundleId;

    /// <summary>
    /// Gets the candidates, after the host has reconciled overlapping claims.
    /// </summary>
    public IReadOnlyList<SensitiveEntity> Entities { get; }

    /// <summary>
    /// Gets the detectors that ran, with their versions.
    /// </summary>
    public IReadOnlyList<DetectorVersion> Detectors { get; }

    /// <summary>
    /// Gets the detectors that did not finish.
    /// </summary>
    public IReadOnlyList<DetectionFailure> Failures { get; }

    /// <summary>
    /// Gets a value indicating whether every part of the document was both read and scanned.
    /// </summary>
    /// <remarks>
    /// This is the question the policy asks before it is willing to describe a document as
    /// clean. A document that is not complete has not been shown to be safe; it has only
    /// failed to be shown unsafe.
    /// </remarks>
    public bool IsComplete => Failures.Count == 0 && Extracted.IsComplete;

    /// <summary>
    /// Creates a detection result.
    /// </summary>
    /// <param name="extracted">The extracted text the detectors ran over.</param>
    /// <param name="entities">The candidates. The sequence is copied.</param>
    /// <param name="detectors">The detectors that ran. The sequence is copied.</param>
    /// <param name="failures">The detectors that did not finish. The sequence is copied.</param>
    /// <returns>The detection result.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A sequence contains <see langword="null"/>, an entity identifier repeats, an entity
    /// points at a segment that is not in the extraction, or a detector version is the default
    /// value.
    /// </exception>
    /// <remarks>
    /// An entity that points outside the extraction is rejected here rather than clamped
    /// later. A span that does not match the text it claims to describe would make the
    /// anonymizer rewrite the wrong characters, which is the failure this library exists to
    /// prevent.
    /// </remarks>
    public static DetectedDocument Create(
        ExtractedDocument extracted,
        IEnumerable<SensitiveEntity> entities,
        IEnumerable<DetectorVersion> detectors,
        IEnumerable<DetectionFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(extracted);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(detectors);
        ArgumentNullException.ThrowIfNull(failures);

        var lengths = new Dictionary<SegmentId, int>();

        foreach (var segment in extracted.Segments)
        {
            lengths[segment.Id] = segment.Text.Length;
        }

        var copiedEntities = new List<SensitiveEntity>();
        var known = new HashSet<EntityId>();

        foreach (var entity in entities)
        {
            if (entity is null)
            {
                throw new ArgumentException("An entity list cannot contain a null entity.", nameof(entities));
            }

            if (!known.Add(entity.Id))
            {
                throw new ArgumentException("Two entities share an identifier.", nameof(entities));
            }

            if (!lengths.TryGetValue(entity.Span.SegmentId, out var length))
            {
                throw new ArgumentException(
                    "An entity points at a segment that is not part of the extraction.",
                    nameof(entities));
            }

            if (!entity.Span.IsWithin(length))
            {
                throw new ArgumentException(
                    "An entity points past the end of the segment it belongs to.",
                    nameof(entities));
            }

            copiedEntities.Add(entity);
        }

        var copiedDetectors = new List<DetectorVersion>();

        foreach (var detector in detectors)
        {
            if (detector.IsDefault)
            {
                throw new ArgumentException("A detector version carries no value.", nameof(detectors));
            }

            copiedDetectors.Add(detector);
        }

        var copiedFailures = new List<DetectionFailure>();

        foreach (var failure in failures)
        {
            if (failure is null)
            {
                throw new ArgumentException("A failure list cannot contain a null failure.", nameof(failures));
            }

            copiedFailures.Add(failure);
        }

        return new DetectedDocument(
            extracted,
            new ReadOnlyCollection<SensitiveEntity>(copiedEntities),
            new ReadOnlyCollection<DetectorVersion>(copiedDetectors),
            new ReadOnlyCollection<DetectionFailure>(copiedFailures));
    }

    /// <summary>
    /// Returns a description of the detection.
    /// </summary>
    /// <returns>
    /// The bundle, the number of candidates and the number of failures. It never includes
    /// text.
    /// </returns>
    public override string ToString() =>
        $"{BundleId} ({Entities.Count} entities, {Failures.Count} failures)";
}
