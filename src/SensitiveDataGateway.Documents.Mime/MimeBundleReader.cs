using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MimeKit;

namespace SensitiveDataGateway.Documents.Mime;

/// <summary>
/// Turns an RFC 5322 message into a bundle the gateway can process.
/// </summary>
/// <remarks>
/// <para>
/// The reader flattens the whole message: the configured headers, every body of every
/// alternative, every attachment, and the same again for each forwarded message it contains.
/// It does not choose a "best" body. A <c>multipart/alternative</c> yields a part for the
/// plain text and a part for the HTML, because the two are frequently not the same text and
/// the one that was not read is the one nothing would have scanned.
/// </para>
/// <para>
/// It decodes transfer encodings and charsets and does nothing else. It opens no socket,
/// fetches no <c>message/external-body</c>, reassembles no <c>message/partial</c>, and
/// decrypts no <c>application/pkcs7-mime</c>. Each of those arrives as a part with its
/// declared media type, no extractor claims it, and the operation blocks — which is the
/// outcome this library prefers over reading something it cannot fully account for.
/// </para>
/// <para>
/// Failures are content-free. A malformed message raises
/// <see cref="ExtractionException"/> and a message that crosses a bound raises
/// <see cref="ProcessingLimitExceededException"/>; neither carries a header, an address or a
/// file name taken from the message.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var reader = new MimeBundleReader();
/// var bundle = await reader.ReadAsync(
///     BundleId.New(),
///     messageStream,
///     ProcessingLimits.Default,
///     cancellationToken);
/// </code>
/// </example>
public sealed class MimeBundleReader
{
    private readonly MimeReaderOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MimeBundleReader"/> class with the
    /// default options.
    /// </summary>
    public MimeBundleReader()
        : this(MimeReaderOptions.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MimeBundleReader"/> class.
    /// </summary>
    /// <param name="options">The choices the reader makes while it flattens a message.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public MimeBundleReader(MimeReaderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
    }

    /// <summary>
    /// Reads a message into a bundle.
    /// </summary>
    /// <param name="id">The identifier of the processing operation.</param>
    /// <param name="message">A readable stream positioned at the start of the message.</param>
    /// <param name="limits">The bounds the read refuses to cross.</param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>The bundle.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="message"/> or <paramref name="limits"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="id"/> is the default value.</exception>
    /// <exception cref="ExtractionException">The stream does not hold a well-formed message.</exception>
    /// <exception cref="ProcessingLimitExceededException">
    /// The message nests deeper, yields more parts, or holds more bytes than
    /// <paramref name="limits"/> allows.
    /// </exception>
    public async Task<DocumentBundle> ReadAsync(
        BundleId id,
        Stream message,
        ProcessingLimits limits,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(limits);

        var parsed = await LoadAsync(message, cancellationToken).ConfigureAwait(false);
        var builder = new MimeBundleBuilder(limits, _options);

        builder.AddHeaders(parsed, parent: null);

        if (parsed.Body is not null)
        {
            builder.AddEntity(parsed.Body, parent: null, depth: 0);
        }

        return builder.Build(id, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Parses the message, translating a parser failure into a gateway failure.
    /// </summary>
    /// <param name="message">The stream.</param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>The parsed message.</returns>
    /// <remarks>
    /// The parser's own exception is kept as the inner exception and never shown to a
    /// caller: it quotes the input it choked on, and the input is the document.
    /// </remarks>
    private static async Task<MimeMessage> LoadAsync(Stream message, CancellationToken cancellationToken)
    {
        try
        {
            return await MimeMessage.LoadAsync(CreateParserOptions(), message, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (FormatException exception)
        {
            throw new ExtractionException(MimeErrors.Malformed(), exception);
        }
    }

    /// <summary>
    /// Builds the parser configuration.
    /// </summary>
    /// <returns>The options.</returns>
    /// <remarks>
    /// The parser is given a ceiling one level above the deepest bound a caller may configure,
    /// not the configured bound itself. The parser does not fail when its ceiling is reached:
    /// it hands back the remainder of the message as one unparsed part. Keeping its ceiling
    /// above the configured bound means the reader's own check is always what a deeply nested
    /// message meets, so the outcome is a stated limit rather than a silent truncation, while
    /// the parser still stops recursing on a message crafted to exhaust the stack.
    /// </remarks>
    private static ParserOptions CreateParserOptions() =>
        new() { MaxMimeDepth = MimeReaderOptions.MaximumNestingDepth + 1 };
}
