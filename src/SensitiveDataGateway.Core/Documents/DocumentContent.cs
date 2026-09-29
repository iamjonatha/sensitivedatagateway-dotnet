using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SensitiveDataGateway.Documents;

/// <summary>
/// The content of one part of a bundle, held as text, as bytes, or as a stream opened on demand.
/// </summary>
/// <remarks>
/// <para>
/// Content is the untrusted material the gateway exists to handle. Nothing in the library
/// writes it to a log, puts it in an exception message or copies it into a result. The type
/// therefore offers no formatting: <see cref="ToString"/> describes the container, never what
/// is in it.
/// </para>
/// <para>
/// The three forms exist because the reference case mixes them. An email body arrives as
/// text, a small attachment as bytes, and a large one as something the application would
/// rather open only if the limits allow it.
/// </para>
/// </remarks>
public sealed class DocumentContent
{
    private readonly string? _text;
    private readonly ReadOnlyMemory<byte> _bytes;
    private readonly Func<CancellationToken, ValueTask<Stream>>? _streamFactory;

    private DocumentContent(
        DocumentContentKind kind,
        string? text,
        ReadOnlyMemory<byte> bytes,
        Func<CancellationToken, ValueTask<Stream>>? streamFactory,
        long? length)
    {
        Kind = kind;
        _text = text;
        _bytes = bytes;
        _streamFactory = streamFactory;
        Length = length;
    }

    /// <summary>
    /// Gets the form in which the content is held.
    /// </summary>
    public DocumentContentKind Kind { get; }

    /// <summary>
    /// Gets the size of the content in bytes, when it is known without reading it.
    /// </summary>
    /// <value>
    /// The encoded size for text and bytes, the declared size for a stream, or
    /// <see langword="null"/> when a stream was supplied without one.
    /// </value>
    /// <remarks>
    /// An unknown size is not treated as a small one. A part whose size is unknown is still
    /// subject to <see cref="ProcessingLimits.MaxPartBytes"/> while it is read, and the read
    /// stops by refusing the operation rather than by truncating.
    /// </remarks>
    public long? Length { get; }

    /// <summary>
    /// Creates content from text.
    /// </summary>
    /// <param name="text">The text. It may be empty, which is a fact, not a failure.</param>
    /// <returns>The content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static DocumentContent FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return new DocumentContent(
            DocumentContentKind.Text,
            text,
            bytes: default,
            streamFactory: null,
            length: Encoding.UTF8.GetByteCount(text));
    }

    /// <summary>
    /// Creates content from bytes.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The content.</returns>
    public static DocumentContent FromBytes(ReadOnlyMemory<byte> bytes) =>
        new(DocumentContentKind.Bytes, text: null, bytes, streamFactory: null, length: bytes.Length);

    /// <summary>
    /// Creates content an extractor obtains by opening a stream.
    /// </summary>
    /// <param name="streamFactory">
    /// A function that opens a readable stream over the content. It may be called more than
    /// once, and must return a stream positioned at the beginning every time.
    /// </param>
    /// <param name="length">The size in bytes, when the application knows it.</param>
    /// <returns>The content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="streamFactory"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    /// <remarks>
    /// The factory is the only place the library will reach for content, which keeps the
    /// question of where a document lives — disk, blob storage, a request body — outside the
    /// domain model.
    /// </remarks>
    public static DocumentContent FromStream(
        Func<CancellationToken, ValueTask<Stream>> streamFactory,
        long? length)
    {
        ArgumentNullException.ThrowIfNull(streamFactory);

        if (length is { } declared)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(declared, nameof(length));
        }

        return new DocumentContent(
            DocumentContentKind.Stream,
            text: null,
            bytes: default,
            streamFactory,
            length);
    }

    /// <summary>
    /// Returns the content as text.
    /// </summary>
    /// <returns>The text.</returns>
    /// <exception cref="InvalidOperationException">
    /// The content is not held as text. It is not decoded on the caller's behalf, because
    /// guessing an encoding is how a scan comes to read different characters than the
    /// recipient will.
    /// </exception>
    public string AsText() =>
        Kind == DocumentContentKind.Text && _text is not null
            ? _text
            : throw new InvalidOperationException($"The content is held as {Kind} and not as text.");

    /// <summary>
    /// Returns the content as bytes.
    /// </summary>
    /// <returns>The bytes.</returns>
    /// <exception cref="InvalidOperationException">The content is not held as bytes.</exception>
    public ReadOnlyMemory<byte> AsBytes() =>
        Kind == DocumentContentKind.Bytes
            ? _bytes
            : throw new InvalidOperationException($"The content is held as {Kind} and not as bytes.");

    /// <summary>
    /// Opens a readable stream over the content.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels opening the stream.</param>
    /// <returns>A stream positioned at the beginning of the content. The caller disposes it.</returns>
    /// <exception cref="InvalidOperationException">
    /// The content is not held as a stream, or the supplied factory returned
    /// <see langword="null"/>.
    /// </exception>
    public async ValueTask<Stream> OpenStreamAsync(CancellationToken cancellationToken)
    {
        if (Kind != DocumentContentKind.Stream || _streamFactory is null)
        {
            throw new InvalidOperationException($"The content is held as {Kind} and not as a stream.");
        }

        var stream = await _streamFactory(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The stream factory returned no stream.");

        return stream;
    }

    /// <summary>
    /// Returns a description of the container.
    /// </summary>
    /// <returns>
    /// The form and the known size. It never includes the content, so that an accidental
    /// interpolation into a log line cannot leak a document.
    /// </returns>
    public override string ToString() =>
        Length is { } length
            ? $"{Kind} ({length} bytes)"
            : $"{Kind} (size unknown)";
}
