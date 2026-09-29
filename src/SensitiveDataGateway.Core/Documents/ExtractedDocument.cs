using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SensitiveDataGateway.Documents;

/// <summary>
/// The canonical text of a bundle: every part turned into ordered, provenanced segments.
/// </summary>
/// <remarks>
/// This is the last stage that has an opinion about file formats. Everything after it works
/// on text and spans, which is what keeps a detector from having to know what a PDF is.
/// </remarks>
public sealed class ExtractedDocument
{
    private ExtractedDocument(
        BundleId bundleId,
        IReadOnlyList<TextSegment> segments,
        IReadOnlyList<ExtractionFailure> failures,
        IReadOnlyList<ExtractorVersion> extractors)
    {
        BundleId = bundleId;
        Segments = segments;
        Failures = failures;
        Extractors = extractors;
    }

    /// <summary>
    /// Gets the processing operation this extraction belongs to.
    /// </summary>
    public BundleId BundleId { get; }

    /// <summary>
    /// Gets the extracted segments.
    /// </summary>
    public IReadOnlyList<TextSegment> Segments { get; }

    /// <summary>
    /// Gets the parts that could not be read.
    /// </summary>
    /// <remarks>
    /// A non-empty list does not stop extraction, and it does not mean the document may be
    /// transmitted. The policy decides what an unreadable part is worth; the safe default is
    /// that it blocks.
    /// </remarks>
    public IReadOnlyList<ExtractionFailure> Failures { get; }

    /// <summary>
    /// Gets the extractors that ran, with their versions.
    /// </summary>
    public IReadOnlyList<ExtractorVersion> Extractors { get; }

    /// <summary>
    /// Gets a value indicating whether every part of the bundle was read.
    /// </summary>
    public bool IsComplete => Failures.Count == 0;

    /// <summary>
    /// Gets the total number of extracted characters.
    /// </summary>
    /// <remarks>
    /// This is the quantity <see cref="ProcessingLimits.MaxExtractedCharacters"/> bounds. It
    /// is the only limit that sees a small archive expanding into an enormous amount of text.
    /// </remarks>
    public long TotalCharacters
    {
        get
        {
            long total = 0;

            foreach (var segment in Segments)
            {
                total += segment.Text.Length;
            }

            return total;
        }
    }

    /// <summary>
    /// Creates an extraction result.
    /// </summary>
    /// <param name="bundleId">The processing operation this extraction belongs to.</param>
    /// <param name="segments">The extracted segments. The sequence is copied.</param>
    /// <param name="failures">The parts that could not be read. The sequence is copied.</param>
    /// <param name="extractors">The extractors that ran. The sequence is copied.</param>
    /// <returns>The extraction result.</returns>
    /// <exception cref="ArgumentNullException">A sequence is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="bundleId"/> is the default value, a sequence contains
    /// <see langword="null"/>, a segment identifier repeats, or an extractor version is the
    /// default value.
    /// </exception>
    public static ExtractedDocument Create(
        BundleId bundleId,
        IEnumerable<TextSegment> segments,
        IEnumerable<ExtractionFailure> failures,
        IEnumerable<ExtractorVersion> extractors)
    {
        if (bundleId.IsDefault)
        {
            throw new ArgumentException("The bundle identifier carries no value.", nameof(bundleId));
        }

        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(failures);
        ArgumentNullException.ThrowIfNull(extractors);

        var copiedSegments = new List<TextSegment>();
        var known = new HashSet<SegmentId>();

        foreach (var segment in segments)
        {
            if (segment is null)
            {
                throw new ArgumentException("A segment list cannot contain a null segment.", nameof(segments));
            }

            if (!known.Add(segment.Id))
            {
                throw new ArgumentException("Two segments share an identifier.", nameof(segments));
            }

            copiedSegments.Add(segment);
        }

        var copiedFailures = new List<ExtractionFailure>();

        foreach (var failure in failures)
        {
            if (failure is null)
            {
                throw new ArgumentException("A failure list cannot contain a null failure.", nameof(failures));
            }

            copiedFailures.Add(failure);
        }

        var copiedExtractors = new List<ExtractorVersion>();

        foreach (var extractor in extractors)
        {
            if (extractor.IsDefault)
            {
                throw new ArgumentException("An extractor version carries no value.", nameof(extractors));
            }

            copiedExtractors.Add(extractor);
        }

        return new ExtractedDocument(
            bundleId,
            new ReadOnlyCollection<TextSegment>(copiedSegments),
            new ReadOnlyCollection<ExtractionFailure>(copiedFailures),
            new ReadOnlyCollection<ExtractorVersion>(copiedExtractors));
    }

    /// <summary>
    /// Returns a description of the extraction.
    /// </summary>
    /// <returns>
    /// The bundle, the number of segments and the number of failures. It never includes text.
    /// </returns>
    public override string ToString() =>
        $"{BundleId} ({Segments.Count} segments, {Failures.Count} failures)";
}
