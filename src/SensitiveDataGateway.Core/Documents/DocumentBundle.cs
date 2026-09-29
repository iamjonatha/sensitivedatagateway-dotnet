using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SensitiveDataGateway.Documents;

/// <summary>
/// The untrusted input of the pipeline: a primary document and everything that arrived with it.
/// </summary>
/// <remarks>
/// <para>
/// The input is not a string. The reference case is a message with attachments, and
/// flattening it to text before the gateway sees it destroys the provenance every later
/// stage depends on.
/// </para>
/// <para>
/// This is the boundary type. It is accepted by the gateway façade and by nothing else. No
/// provider package can name it, and no logging surface accepts it, which is what makes
/// "raw content cannot reach a provider" a property of the type system rather than a rule
/// somebody has to remember.
/// </para>
/// </remarks>
public sealed class DocumentBundle
{
    private DocumentBundle(
        BundleId id,
        DocumentPart primary,
        IReadOnlyList<DocumentPart> attachments,
        DocumentMetadata metadata,
        DateTimeOffset createdAt)
    {
        Id = id;
        Primary = primary;
        Attachments = attachments;
        Metadata = metadata;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Gets the identifier of the processing operation this bundle belongs to.
    /// </summary>
    public BundleId Id { get; }

    /// <summary>
    /// Gets the main document.
    /// </summary>
    public DocumentPart Primary { get; }

    /// <summary>
    /// Gets everything that arrived with the primary document, including nested parts.
    /// </summary>
    /// <remarks>
    /// Nesting is expressed by <see cref="DocumentPart.Parent"/> rather than by a tree, so
    /// that every stage can walk the bundle as a flat list and none of them has to recurse.
    /// </remarks>
    public IReadOnlyList<DocumentPart> Attachments { get; }

    /// <summary>
    /// Gets what the application knows about the bundle as a whole.
    /// </summary>
    public DocumentMetadata Metadata { get; }

    /// <summary>
    /// Gets when the application assembled the bundle.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets every part, starting with the primary document.
    /// </summary>
    public IEnumerable<DocumentPart> Parts
    {
        get
        {
            yield return Primary;

            foreach (var attachment in Attachments)
            {
                yield return attachment;
            }
        }
    }

    /// <summary>
    /// Creates a bundle with no attachments.
    /// </summary>
    /// <param name="id">The identifier of the processing operation.</param>
    /// <param name="primary">The main document.</param>
    /// <returns>The bundle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="primary"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="id"/> is the default value.</exception>
    public static DocumentBundle Create(BundleId id, DocumentPart primary) =>
        Create(id, primary, Array.Empty<DocumentPart>(), DocumentMetadata.Empty, DateTimeOffset.UtcNow);

    /// <summary>
    /// Creates a bundle.
    /// </summary>
    /// <param name="id">The identifier of the processing operation.</param>
    /// <param name="primary">The main document.</param>
    /// <param name="attachments">Everything that arrived with it. The sequence is copied.</param>
    /// <returns>The bundle.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="primary"/> or <paramref name="attachments"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> is the default value, an attachment is <see langword="null"/>, a
    /// part identifier repeats, or a part names a parent that is not in the bundle.
    /// </exception>
    public static DocumentBundle Create(
        BundleId id,
        DocumentPart primary,
        IEnumerable<DocumentPart> attachments) =>
        Create(id, primary, attachments, DocumentMetadata.Empty, DateTimeOffset.UtcNow);

    /// <summary>
    /// Creates a bundle.
    /// </summary>
    /// <param name="id">The identifier of the processing operation.</param>
    /// <param name="primary">The main document.</param>
    /// <param name="attachments">Everything that arrived with it. The sequence is copied.</param>
    /// <param name="metadata">What the application knows about the bundle as a whole.</param>
    /// <param name="createdAt">When the application assembled the bundle.</param>
    /// <returns>The bundle.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="primary"/>, <paramref name="attachments"/> or <paramref name="metadata"/>
    /// is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> is the default value, an attachment is <see langword="null"/>, a
    /// part identifier repeats, or a part names a parent that is not in the bundle.
    /// </exception>
    /// <remarks>
    /// The structural checks run here, at construction, so that no later stage has to decide
    /// what to do about a bundle that contradicts itself. A duplicate part identifier would
    /// make an audit record ambiguous, and a dangling parent would make provenance a lie.
    /// </remarks>
    public static DocumentBundle Create(
        BundleId id,
        DocumentPart primary,
        IEnumerable<DocumentPart> attachments,
        DocumentMetadata metadata,
        DateTimeOffset createdAt)
    {
        if (id.IsDefault)
        {
            throw new ArgumentException("The bundle identifier carries no value.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(attachments);
        ArgumentNullException.ThrowIfNull(metadata);

        var copied = new List<DocumentPart>();
        var known = new HashSet<PartId> { primary.Id };

        foreach (var attachment in attachments)
        {
            if (attachment is null)
            {
                throw new ArgumentException("A bundle cannot contain a null part.", nameof(attachments));
            }

            if (!known.Add(attachment.Id))
            {
                throw new ArgumentException(
                    "Two parts of the bundle share an identifier.",
                    nameof(attachments));
            }

            copied.Add(attachment);
        }

        if (primary.Parent is not null)
        {
            throw new ArgumentException("The primary document cannot be nested in another part.", nameof(primary));
        }

        foreach (var part in copied)
        {
            if (part.Parent is { } parent && !known.Contains(parent))
            {
                throw new ArgumentException(
                    "A part names a parent that is not in the bundle.",
                    nameof(attachments));
            }
        }

        return new DocumentBundle(
            id,
            primary,
            new ReadOnlyCollection<DocumentPart>(copied),
            metadata,
            createdAt);
    }

    /// <summary>
    /// Returns a description of the bundle.
    /// </summary>
    /// <returns>
    /// The identifier and the number of parts. It never includes content or metadata.
    /// </returns>
    public override string ToString() => $"{Id} ({Attachments.Count + 1} parts)";
}
