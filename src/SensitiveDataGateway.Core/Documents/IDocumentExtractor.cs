using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SensitiveDataGateway.Documents;

/// <summary>
/// Turns one part of a bundle into ordered text segments.
/// </summary>
/// <remarks>
/// <para>
/// An extractor reads a format and nothing more. It <b>must not</b> classify: it has no
/// opinion about what a person's name is, and an extractor that starts guessing takes a
/// decision away from the stage that is allowed to make it.
/// </para>
/// <para>
/// An extractor <b>must not</b> follow a reference out of the part. No URL is fetched, no
/// linked file is opened and no external entity is resolved; XXE and server-side request
/// forgery are extraction-stage vulnerabilities, and this is the interface where they would
/// be introduced.
/// </para>
/// <para>
/// An extractor <b>must not</b> call the network at all. Optical character recognition and
/// cloud document intelligence are remote processors of raw content: they sit outside the
/// strict path, are registered explicitly, and are never selected automatically.
/// </para>
/// <para>
/// An extractor <b>must not</b> truncate. A part it cannot process within the limits fails,
/// so that the host records an <see cref="ExtractionFailure"/> rather than silently dropping
/// the tail nobody examined.
/// </para>
/// </remarks>
public interface IDocumentExtractor
{
    /// <summary>
    /// Gets the identity and version of this extractor, for the audit record.
    /// </summary>
    ExtractorVersion Version { get; }

    /// <summary>
    /// Determines whether this extractor handles a declared media type.
    /// </summary>
    /// <param name="mediaType">The lowercased media type declared by the part.</param>
    /// <returns><see langword="true"/> when the extractor accepts the part.</returns>
    /// <remarks>
    /// Returning <see langword="true"/> is a commitment: the host will not offer the part to
    /// another extractor, and a subsequent failure is recorded as a failure rather than
    /// retried elsewhere.
    /// </remarks>
    bool CanExtract(string mediaType);

    /// <summary>
    /// Extracts the text of a part.
    /// </summary>
    /// <param name="part">The part to read.</param>
    /// <param name="cancellationToken">A token that cancels the extraction.</param>
    /// <returns>
    /// The segments, in reading order. An empty list is a statement that the part genuinely
    /// contains no text; an extractor that cannot read the part throws instead.
    /// </returns>
    /// <exception cref="ExtractionException">
    /// The part cannot be read. The host converts this into an
    /// <see cref="ExtractionFailure"/> so that one unreadable attachment does not abandon the
    /// rest of the bundle.
    /// </exception>
    ValueTask<IReadOnlyList<TextSegment>> ExtractAsync(DocumentPart part, CancellationToken cancellationToken);
}
