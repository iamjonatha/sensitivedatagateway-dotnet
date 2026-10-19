using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Exceptions;

namespace SensitiveDataGateway.Documents.Pdf;

/// <summary>
/// Reads the text out of a PDF document.
/// </summary>
/// <remarks>
/// <para>
/// The extractor produces one segment per page in reading order, one per entry of the
/// document information dictionary, one for the XMP packet, one per annotation that carries
/// text or points somewhere, and one per form field a person filled in. Everything a reader
/// of the document can see, and several things only a parser can, end up in front of the
/// detectors.
/// </para>
/// <para>
/// It refuses rather than reads around anything it cannot account for: a document that needs
/// a password, a document carrying a file inside it, a form whose values live in an XML
/// stream, a page that is an image of text, and a document the parser cannot read without
/// guessing. Each of those is a part of the document that nothing scanned, and a document
/// scanned in part must not be transmitted.
/// </para>
/// <para>
/// The parser is configured strictly and silently: no password is ever supplied, lenient
/// recovery is off, a missing font is a failure rather than a gap, and the logger discards
/// everything so that no fragment of the document reaches a console.
/// </para>
/// </remarks>
public sealed class PdfExtractor : IDocumentExtractor
{
    private const int CopyBufferSize = 81_920;

    private readonly PdfExtractorOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfExtractor"/> class with the default
    /// bounds.
    /// </summary>
    public PdfExtractor()
        : this(PdfExtractorOptions.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfExtractor"/> class.
    /// </summary>
    /// <param name="options">The bounds the extractor stays inside.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public PdfExtractor(PdfExtractorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        Version = ExtractorVersion.Create(ExtractorId.Create("pdf"), "1.0.0");
    }

    /// <inheritdoc/>
    public ExtractorVersion Version { get; }

    /// <inheritdoc/>
    public bool CanExtract(string mediaType)
    {
        ArgumentNullException.ThrowIfNull(mediaType);

        return PdfMediaTypes.IsPdf(PdfMediaTypes.Essence(mediaType));
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<TextSegment>> ExtractAsync(
        DocumentPart part,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(part);

        var bytes = await ReadAsync(part, cancellationToken).ConfigureAwait(false);

        return Parse(part.Id, bytes, cancellationToken);
    }

    /// <summary>
    /// Returns the options the parser is driven with.
    /// </summary>
    /// <returns>The options.</returns>
    /// <remarks>
    /// <para>
    /// Lenient parsing is off. It exists to salvage a document with a damaged cross-reference
    /// table by scanning for objects, and a salvaged document is one whose missing objects
    /// nobody counted. Here a damaged document is refused, and the operation blocks.
    /// </para>
    /// <para>
    /// A missing font is a failure for the same reason: a page whose glyphs cannot be mapped
    /// back to characters yields text that is wrong rather than text that is absent, and
    /// wrong text is what a detector fails to match.
    /// </para>
    /// <para>
    /// <c>UseActualText</c> is on, so that the replacement text an author attached to a
    /// ligature or an image is read as the author meant it. No password is set, in any form:
    /// a library that guesses at a password decrypts a document its caller never showed it
    /// the key to.
    /// </para>
    /// </remarks>
    private static ParsingOptions StrictParsing() => new()
    {
        ClipPaths = false,
        UseLenientParsing = false,
        SkipMissingFonts = false,
        UseActualText = true,
        Logger = PdfSilentLog.Instance,
    };

    /// <summary>
    /// Returns whether an exception means the parser could not read the document.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns><see langword="true"/> when the document is malformed.</returns>
    /// <remarks>
    /// A parser handed a hostile file throws more than its own exception type, and the ones
    /// listed here all mean the same thing: the bytes are not a document this parser can
    /// read. They are translated rather than swallowed — each one becomes a failure on the
    /// part, which blocks the operation. Cancellation and resource exhaustion are absent on
    /// purpose, because neither is a statement about the document, and so is every failure
    /// this library raises itself.
    /// </remarks>
    private static bool IsParseFailure(Exception exception) => exception
        is PdfDocumentFormatException
        or PdfDocumentStackDepthException
        or InvalidOperationException
        or NotSupportedException
        or KeyNotFoundException
        or IndexOutOfRangeException
        or ArgumentException
        or FormatException
        or OverflowException
        or EndOfStreamException;

    private IReadOnlyList<TextSegment> Parse(
        PartId partId,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = PdfDocument.Open(bytes, StrictParsing());

            return new PdfSegmentCollector(partId, _options).Collect(document, cancellationToken);
        }
        catch (PdfDocumentEncryptedException exception)
        {
            throw new ExtractionException(PdfErrors.Encrypted(partId), exception);
        }
        catch (Exception exception)
            when (exception is not SensitiveDataGatewayException && IsParseFailure(exception))
        {
            throw new ExtractionException(PdfErrors.Malformed(partId), exception);
        }
    }

    /// <summary>
    /// Reads the bytes of the part, refusing one that is larger than the bound.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <param name="cancellationToken">The token to cancel the read with.</param>
    /// <returns>The bytes.</returns>
    /// <exception cref="ExtractionException">The part is text, or larger than the bound.</exception>
    private async ValueTask<ReadOnlyMemory<byte>> ReadAsync(DocumentPart part, CancellationToken cancellationToken)
    {
        if (part.Content.Kind == DocumentContentKind.Text)
        {
            throw new ExtractionException(PdfErrors.NotBinary(part.Id));
        }

        if (part.Content.Kind == DocumentContentKind.Bytes)
        {
            return Bound(part.Id, part.Content.AsBytes());
        }

        var stream = await part.Content.OpenStreamAsync(cancellationToken).ConfigureAwait(false);

        await using (stream.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            await CopyAsync(part.Id, stream, buffer, cancellationToken).ConfigureAwait(false);

            return buffer.GetBuffer().AsMemory(0, (int)buffer.Length);
        }
    }

    private ReadOnlyMemory<byte> Bound(PartId partId, ReadOnlyMemory<byte> bytes) =>
        bytes.Length > _options.MaxBytes
            ? throw new ExtractionException(PdfErrors.TooLarge(partId, _options.MaxBytes))
            : bytes;

    /// <summary>
    /// Copies a stream into a buffer, stopping the moment it crosses the bound.
    /// </summary>
    /// <param name="partId">The part being read.</param>
    /// <param name="source">The stream.</param>
    /// <param name="destination">The buffer.</param>
    /// <param name="cancellationToken">The token to cancel the copy with.</param>
    /// <returns>A task that completes when the stream has been copied.</returns>
    /// <exception cref="ExtractionException">The stream is larger than the bound.</exception>
    /// <remarks>
    /// The bound is checked while copying rather than afterwards, because a length checked
    /// after the fact has already cost the memory it was there to refuse. A stream need not
    /// report a length, and one that reports a false one is the case this guards against.
    /// </remarks>
    private async Task CopyAsync(
        PartId partId,
        Stream source,
        MemoryStream destination,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[CopyBufferSize];
        int read;

        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (destination.Length + read > _options.MaxBytes)
            {
                throw new ExtractionException(PdfErrors.TooLarge(partId, _options.MaxBytes));
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
    }
}
