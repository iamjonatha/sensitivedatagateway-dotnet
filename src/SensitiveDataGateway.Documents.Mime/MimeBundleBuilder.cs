using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MimeKit;

namespace SensitiveDataGateway.Documents.Mime;

/// <summary>
/// Turns the tree a MIME parser produces into the flat list of parts a bundle is made of.
/// </summary>
/// <remarks>
/// <para>
/// The bundle model is flat on purpose, so every part is offered to an extractor exactly
/// once and nothing can hide one level further down than the loop goes. Structure survives
/// as <see cref="DocumentPart.Parent"/>, which is enough to tell a reviewer that an address
/// came from a forwarded message rather than from the message that was received.
/// </para>
/// <para>
/// Identifiers are ordinals. None of them is derived from a header name, a file name or a
/// subject, because an identifier is the one thing about a part that is allowed to appear in
/// a log line unchanged.
/// </para>
/// </remarks>
internal sealed class MimeBundleBuilder
{
    private const string SynthesizedBodyId = "body";

    private readonly ProcessingLimits _limits;
    private readonly MimeReaderOptions _options;
    private readonly HashSet<string> _headerNames;
    private readonly List<DocumentPart> _parts = [];
    private int _headers;
    private int _bodies;
    private int _attachments;
    private int _envelopes;
    private long _bytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="MimeBundleBuilder"/> class.
    /// </summary>
    /// <param name="limits">The bounds the reader refuses to cross.</param>
    /// <param name="options">The choices the reader makes.</param>
    public MimeBundleBuilder(ProcessingLimits limits, MimeReaderOptions options)
    {
        _limits = limits;
        _options = options;
        _headerNames = options.HeaderSet();
    }

    /// <summary>
    /// Emits one part for each configured header the message carries.
    /// </summary>
    /// <param name="message">The message, which may be a forwarded one.</param>
    /// <param name="parent">The envelope the message was found in, or <see langword="null"/>.</param>
    /// <exception cref="ProcessingLimitExceededException">The message yields too many parts.</exception>
    public void AddHeaders(MimeMessage message, PartId? parent)
    {
        foreach (var header in message.Headers)
        {
            if (!_headerNames.Contains(header.Field))
            {
                continue;
            }

            var kind = IsSubject(header.Field) ? PartKind.Subject : PartKind.Header;

            Add(DocumentPart.Create(
                Next("h", ref _headers),
                kind,
                MimeMediaTypes.PlainText,
                Admit(DocumentContent.FromText(string.Concat(header.Field, ": ", header.Value))),
                parent,
                DocumentMetadata.Empty));
        }
    }

    /// <summary>
    /// Emits the parts of one entity and of everything inside it.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="parent">The envelope it was found in, or <see langword="null"/>.</param>
    /// <param name="depth">How far inside the message this entity sits.</param>
    /// <exception cref="ProcessingLimitExceededException">
    /// The message nests too deeply, yields too many parts, or holds too many bytes.
    /// </exception>
    /// <exception cref="ExtractionException">
    /// The parser handed back a composite it had not taken apart.
    /// </exception>
    public void AddEntity(MimeEntity entity, PartId? parent, int depth)
    {
        if (depth > _options.MaxNestingDepth)
        {
            throw new ProcessingLimitExceededException(MimeErrors.NestedTooDeeply(_options.MaxNestingDepth));
        }

        switch (entity)
        {
            case Multipart multipart:
                AddChildren(multipart, parent, depth);
                break;

            case MessagePart envelope when MimeMediaTypes.IsEnvelope(envelope.ContentType.MimeType):
                AddEnvelope(envelope, parent, depth);
                break;

            case MimePart leaf when MimeMediaTypes.IsUnparsedComposite(leaf.ContentType.MimeType):
                throw new ExtractionException(MimeErrors.Truncated());

            case MimePart leaf:
                AddLeaf(leaf, parent);
                break;

            default:
                AddOpaque(entity, parent);
                break;
        }
    }

    /// <summary>
    /// Assembles the bundle.
    /// </summary>
    /// <param name="id">The identifier of the processing operation.</param>
    /// <param name="createdAt">When the bundle was assembled.</param>
    /// <returns>The bundle.</returns>
    /// <remarks>
    /// The primary document is the first top-level body the message carries. A message with
    /// none — an envelope whose only child is an attachment, say — is given an empty primary
    /// rather than being refused, because the headers alone are still worth scanning.
    /// </remarks>
    public DocumentBundle Build(BundleId id, DateTimeOffset createdAt)
    {
        var primary = _parts.Find(part => part.Parent is null && part.Kind == PartKind.Body);

        if (primary is null)
        {
            return DocumentBundle.Create(id, EmptyBody(), _parts, DocumentMetadata.Empty, createdAt);
        }

        var rest = _parts.Where(part => !ReferenceEquals(part, primary)).ToList();

        return DocumentBundle.Create(id, primary, rest, DocumentMetadata.Empty, createdAt);
    }

    private static bool IsSubject(string field) =>
        string.Equals(field, "Subject", StringComparison.OrdinalIgnoreCase);

    private static DocumentPart EmptyBody() =>
        DocumentPart.Create(
            PartId.Create(SynthesizedBodyId),
            PartKind.Body,
            MimeMediaTypes.PlainText,
            DocumentContent.FromText(string.Empty));

    /// <summary>
    /// Returns the metadata of a leaf, which is its file name when it has a usable one.
    /// </summary>
    /// <param name="leaf">The leaf.</param>
    /// <returns>The metadata.</returns>
    /// <remarks>
    /// A file name longer than the model allows, or carrying a control character, is dropped
    /// rather than trimmed or rejected. Dropping it costs a line of a review; it cannot leak,
    /// because what is not carried into the bundle is never extracted and never transmitted.
    /// </remarks>
    private static DocumentMetadata MetadataOf(MimePart leaf) =>
        IsUsableFileName(leaf.FileName)
            ? DocumentMetadata.Create(leaf.FileName, language: null, origin: null)
            : DocumentMetadata.Empty;

    private static bool IsUsableFileName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && fileName.Length <= DocumentMetadata.MaxFileNameLength
        && !fileName.Any(char.IsControl);

    private static ReadOnlyMemory<byte> Decode(MimePart leaf)
    {
        if (leaf.Content is null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        using var buffer = new MemoryStream();
        leaf.Content.DecodeTo(buffer);

        return buffer.ToArray();
    }

    private static PartId Next(string prefix, ref int counter) =>
        PartId.Create(MimeReaderOptions.Identifier(prefix, ++counter));

    private void AddChildren(Multipart multipart, PartId? parent, int depth)
    {
        foreach (var child in multipart)
        {
            AddEntity(child, parent, depth + 1);
        }
    }

    private void AddEnvelope(MessagePart envelope, PartId? parent, int depth)
    {
        var id = Next("m", ref _envelopes);

        Add(DocumentPart.Create(
            id,
            PartKind.Embedded,
            envelope.ContentType.MimeType,
            DocumentContent.FromBytes(ReadOnlyMemory<byte>.Empty),
            parent,
            DocumentMetadata.Empty));

        if (envelope.Message is not { } message)
        {
            return;
        }

        AddHeaders(message, id);

        if (message.Body is not null)
        {
            AddEntity(message.Body, id, depth + 1);
        }
    }

    private void AddLeaf(MimePart leaf, PartId? parent)
    {
        var isAttachment = leaf.IsAttachment || leaf is not TextPart;
        var kind = isAttachment ? PartKind.Attachment : PartKind.Body;
        var id = isAttachment
            ? Next("a", ref _attachments)
            : Next("b", ref _bodies);

        Add(DocumentPart.Create(
            id,
            kind,
            leaf.ContentType.MimeType,
            Admit(ContentOf(leaf)),
            parent,
            MetadataOf(leaf)));
    }

    /// <summary>
    /// Emits a part for an entity that has no content of its own and is not an envelope.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="parent">The envelope it was found in, or <see langword="null"/>.</param>
    /// <remarks>
    /// This is where <c>message/external-body</c> arrives. The part keeps its declared media
    /// type and carries nothing, so no extractor claims it and the operation blocks. The
    /// alternative — resolving the reference — is the request forgery this library refuses to
    /// perform.
    /// </remarks>
    private void AddOpaque(MimeEntity entity, PartId? parent) =>
        Add(DocumentPart.Create(
            Next("a", ref _attachments),
            PartKind.Attachment,
            entity.ContentType.MimeType,
            DocumentContent.FromBytes(ReadOnlyMemory<byte>.Empty),
            parent,
            DocumentMetadata.Empty));

    /// <summary>
    /// Reads the content of a leaf, decoding its transfer encoding and its charset.
    /// </summary>
    /// <param name="leaf">The leaf.</param>
    /// <returns>The content.</returns>
    /// <remarks>
    /// The encoded size is checked before anything is decoded, because a bound that is
    /// checked after the read has already cost what it was meant to prevent. Every transfer
    /// encoding in use is at least as large encoded as decoded, so the encoded size is a
    /// sound upper bound to refuse on.
    /// </remarks>
    private DocumentContent ContentOf(MimePart leaf)
    {
        var encoded = leaf.Content?.Stream is { CanSeek: true } stream ? stream.Length : 0;

        if (encoded > _limits.MaxPartBytes)
        {
            throw new ProcessingLimitExceededException(MimeErrors.PartTooLarge(_limits.MaxPartBytes));
        }

        return leaf is TextPart text
            ? DocumentContent.FromText(text.Text ?? string.Empty)
            : DocumentContent.FromBytes(Decode(leaf));
    }

    private DocumentContent Admit(DocumentContent content)
    {
        var length = content.Length ?? 0;

        if (length > _limits.MaxPartBytes)
        {
            throw new ProcessingLimitExceededException(MimeErrors.PartTooLarge(_limits.MaxPartBytes));
        }

        _bytes += length;

        if (_bytes > _limits.MaxBundleBytes)
        {
            throw new ProcessingLimitExceededException(MimeErrors.MessageTooLarge(_limits.MaxBundleBytes));
        }

        return content;
    }

    private void Add(DocumentPart part)
    {
        if (_parts.Count >= _limits.MaxPartCount)
        {
            throw new ProcessingLimitExceededException(MimeErrors.TooManyParts(_limits.MaxPartCount));
        }

        _parts.Add(part);
    }
}
