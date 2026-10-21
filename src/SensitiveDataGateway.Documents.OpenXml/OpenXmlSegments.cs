using System;
using System.Collections.Generic;
using DocumentFormat.OpenXml.Packaging;

namespace SensitiveDataGateway.Documents.OpenXml;

/// <summary>
/// Accumulates the segments an Open XML extractor produces, inside the segment bound.
/// </summary>
/// <remarks>
/// Text that is empty once trimmed is dropped rather than recorded. An empty segment costs a
/// detector pass and an audit row and can hold nothing, and dropping it is not the same as
/// dropping a part: a part the extractor could not read is a failure, while a paragraph with
/// no characters is a paragraph with no characters.
/// </remarks>
internal sealed class OpenXmlSegments(PartId partId, OpenXmlExtractorOptions options)
{
    private readonly List<TextSegment> _segments = [];

    /// <summary>
    /// Gets the part the segments belong to.
    /// </summary>
    public PartId PartId { get; } = partId;

    /// <summary>
    /// Adds a segment, unless its text is blank.
    /// </summary>
    /// <param name="id">The identifier of the segment.</param>
    /// <param name="text">The text.</param>
    /// <param name="origin">Where in the document the text came from.</param>
    /// <exception cref="ExtractionException">The document yields more segments than the bound allows.</exception>
    public void Add(SegmentId id, string? text, SegmentOrigin origin)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (_segments.Count == options.MaxSegmentCount)
        {
            throw new ExtractionException(
                OpenXmlErrors.TooManySegments(PartId, options.MaxSegmentCount));
        }

        _segments.Add(TextSegment.Create(id, PartId, text, origin, _segments.Count));
    }

    /// <summary>
    /// Adds the package properties a person can type into.
    /// </summary>
    /// <param name="package">The package.</param>
    /// <remarks>
    /// The dates are left out on purpose. They are timestamps in a fixed syntax, and the only
    /// thing a detector can make of one is a date of birth that is not there.
    /// </remarks>
    public void AddProperties(OpenXmlPackage package)
    {
        var properties = package.PackageProperties;

        AddProperty("title", properties.Title);
        AddProperty("subject", properties.Subject);
        AddProperty("creator", properties.Creator);
        AddProperty("keywords", properties.Keywords);
        AddProperty("description", properties.Description);
        AddProperty("last-modified-by", properties.LastModifiedBy);
        AddProperty("category", properties.Category);
        AddProperty("content-status", properties.ContentStatus);
    }

    /// <summary>
    /// Returns the segments collected so far.
    /// </summary>
    /// <returns>The segments, in the order they were added.</returns>
    public IReadOnlyList<TextSegment> ToList() => _segments;

    private void AddProperty(string name, string? value) =>
        Add(
            SegmentIds.Metadata(PartId, name),
            value,
            SegmentOrigin.Named(SegmentOriginKind.Metadata, name));
}
