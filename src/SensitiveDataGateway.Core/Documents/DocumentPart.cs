using System;

namespace SensitiveDataGateway.Documents;

/// <summary>
/// One addressable piece of a bundle: a body, a subject, a header or an attachment.
/// </summary>
/// <remarks>
/// A part keeps its lineage. An attachment inside a forwarded message names the part it came
/// from, so that a transformation applied three levels down can still be explained in the
/// audit record.
/// </remarks>
public sealed class DocumentPart
{
    /// <summary>
    /// The greatest number of characters in a media type.
    /// </summary>
    public const int MaxMediaTypeLength = 255;

    private DocumentPart(
        PartId id,
        PartKind kind,
        string mediaType,
        DocumentContent content,
        PartId? parent,
        DocumentMetadata metadata)
    {
        Id = id;
        Kind = kind;
        MediaType = mediaType;
        Content = content;
        Parent = parent;
        Metadata = metadata;
    }

    /// <summary>
    /// Gets the identifier of the part within its bundle.
    /// </summary>
    public PartId Id { get; }

    /// <summary>
    /// Gets the role the part plays in the bundle.
    /// </summary>
    public PartKind Kind { get; }

    /// <summary>
    /// Gets the declared media type, lowercased.
    /// </summary>
    /// <value>A value such as <c>text/plain</c> or <c>application/pdf</c>.</value>
    /// <remarks>
    /// This is what the application declares, not what the bytes are. Extractor selection
    /// uses it, and an extractor that is handed something else must fail rather than guess.
    /// </remarks>
    public string MediaType { get; }

    /// <summary>
    /// Gets the content of the part.
    /// </summary>
    public DocumentContent Content { get; }

    /// <summary>
    /// Gets the part this one was found inside, if any.
    /// </summary>
    public PartId? Parent { get; }

    /// <summary>
    /// Gets what the application knows about the part.
    /// </summary>
    public DocumentMetadata Metadata { get; }

    /// <summary>
    /// Creates a top-level part.
    /// </summary>
    /// <param name="id">The identifier of the part.</param>
    /// <param name="kind">The role the part plays.</param>
    /// <param name="mediaType">The declared media type.</param>
    /// <param name="content">The content.</param>
    /// <returns>The part.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mediaType"/> or <paramref name="content"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="id"/> is the default value, <paramref name="kind"/> is undefined, or the media type is invalid.</exception>
    public static DocumentPart Create(
        PartId id,
        PartKind kind,
        string mediaType,
        DocumentContent content) =>
        Create(id, kind, mediaType, content, parent: null, DocumentMetadata.Empty);

    /// <summary>
    /// Creates a part.
    /// </summary>
    /// <param name="id">The identifier of the part.</param>
    /// <param name="kind">The role the part plays.</param>
    /// <param name="mediaType">The declared media type.</param>
    /// <param name="content">The content.</param>
    /// <param name="parent">The part this one was found inside, or <see langword="null"/>.</param>
    /// <param name="metadata">What the application knows about the part.</param>
    /// <returns>The part.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="mediaType"/>, <paramref name="content"/> or <paramref name="metadata"/>
    /// is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/> is the default value, <paramref name="kind"/> is undefined, the
    /// media type is invalid, or the part names itself as its own parent.
    /// </exception>
    public static DocumentPart Create(
        PartId id,
        PartKind kind,
        string mediaType,
        DocumentContent content,
        PartId? parent,
        DocumentMetadata metadata)
    {
        if (id.IsDefault)
        {
            throw new ArgumentException("The part identifier carries no value.", nameof(id));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentException("The part kind is not a defined value.", nameof(kind));
        }

        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(metadata);

        if (parent is { } parentId)
        {
            if (parentId.IsDefault)
            {
                throw new ArgumentException("The parent identifier carries no value.", nameof(parent));
            }

            if (parentId == id)
            {
                throw new ArgumentException("A part cannot be its own parent.", nameof(parent));
            }
        }

        var normalizedMediaType = Text
            .Require(mediaType, MaxMediaTypeLength, nameof(mediaType))
            .ToLowerInvariant();

        return new DocumentPart(id, kind, normalizedMediaType, content, parent, metadata);
    }

    /// <summary>
    /// Returns a description of the part.
    /// </summary>
    /// <returns>
    /// The identifier, the kind and the media type. It never includes the content or the
    /// metadata, both of which are document content.
    /// </returns>
    public override string ToString() => $"{Id} ({Kind}, {MediaType})";
}
