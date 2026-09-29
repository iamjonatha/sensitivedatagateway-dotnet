using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// The rewritten document: sanitized text, what was done to it, and why it can be trusted.
/// </summary>
/// <remarks>
/// <para>
/// This is a new object. The original is never mutated, which is what lets the two be
/// compared — and comparing them is exactly what the final validation does before anything
/// crosses the trust boundary.
/// </para>
/// <para>
/// A sanitized document is not yet transmittable. It becomes transmittable only by passing
/// validation, which produces a payload the providers can accept.
/// </para>
/// </remarks>
public sealed class SanitizedDocument
{
    private SanitizedDocument(
        BundleId bundleId,
        IReadOnlyList<SanitizedSegment> segments,
        SanitizationStatus status,
        IReadOnlyList<AppliedTransformation> applied,
        IReadOnlyList<SanitizationWarning> warnings,
        SanitizationProvenance provenance)
    {
        BundleId = bundleId;
        Segments = segments;
        Status = status;
        Applied = applied;
        Warnings = warnings;
        Provenance = provenance;
    }

    /// <summary>
    /// Gets the processing operation this document belongs to.
    /// </summary>
    public BundleId BundleId { get; }

    /// <summary>
    /// Gets the sanitized segments.
    /// </summary>
    public IReadOnlyList<SanitizedSegment> Segments { get; }

    /// <summary>
    /// Gets how completely the plan was applied.
    /// </summary>
    public SanitizationStatus Status { get; }

    /// <summary>
    /// Gets what was done, value by value.
    /// </summary>
    public IReadOnlyList<AppliedTransformation> Applied { get; }

    /// <summary>
    /// Gets what is worth recording about the sanitization.
    /// </summary>
    public IReadOnlyList<SanitizationWarning> Warnings { get; }

    /// <summary>
    /// Gets how this document came to look the way it does.
    /// </summary>
    public SanitizationProvenance Provenance { get; }

    /// <summary>
    /// Creates a sanitized document.
    /// </summary>
    /// <param name="bundleId">The processing operation this document belongs to.</param>
    /// <param name="segments">The sanitized segments. The sequence is copied.</param>
    /// <param name="status">How completely the plan was applied.</param>
    /// <param name="applied">What was done, value by value. The sequence is copied.</param>
    /// <param name="warnings">What is worth recording. The sequence is copied.</param>
    /// <param name="provenance">How the document came to look the way it does.</param>
    /// <returns>The sanitized document.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="bundleId"/> is the default value, <paramref name="status"/> is
    /// undefined, a sequence contains <see langword="null"/>, or a segment identifier repeats.
    /// </exception>
    public static SanitizedDocument Create(
        BundleId bundleId,
        IEnumerable<SanitizedSegment> segments,
        SanitizationStatus status,
        IEnumerable<AppliedTransformation> applied,
        IEnumerable<SanitizationWarning> warnings,
        SanitizationProvenance provenance)
    {
        if (bundleId.IsDefault)
        {
            throw new ArgumentException("The bundle identifier carries no value.", nameof(bundleId));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentException("The sanitization status is not a defined value.", nameof(status));
        }

        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(applied);
        ArgumentNullException.ThrowIfNull(warnings);
        ArgumentNullException.ThrowIfNull(provenance);

        var copiedSegments = new List<SanitizedSegment>();
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

        var copiedApplied = new List<AppliedTransformation>();

        foreach (var transformation in applied)
        {
            if (transformation is null)
            {
                throw new ArgumentException(
                    "A transformation list cannot contain a null transformation.",
                    nameof(applied));
            }

            copiedApplied.Add(transformation);
        }

        var copiedWarnings = new List<SanitizationWarning>();

        foreach (var warning in warnings)
        {
            if (warning is null)
            {
                throw new ArgumentException("A warning list cannot contain a null warning.", nameof(warnings));
            }

            copiedWarnings.Add(warning);
        }

        return new SanitizedDocument(
            bundleId,
            new ReadOnlyCollection<SanitizedSegment>(copiedSegments),
            status,
            new ReadOnlyCollection<AppliedTransformation>(copiedApplied),
            new ReadOnlyCollection<SanitizationWarning>(copiedWarnings),
            provenance);
    }

    /// <summary>
    /// Returns a description of the document.
    /// </summary>
    /// <returns>
    /// The bundle, the status and the number of transformations. It never includes text.
    /// </returns>
    public override string ToString() =>
        $"{BundleId} ({Status}, {Applied.Count} transformations)";
}
