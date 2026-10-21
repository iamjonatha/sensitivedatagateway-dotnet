using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SensitiveDataGateway.Documents.OpenXml;

/// <summary>
/// Reads the bytes of a part into memory, inside the configured bound.
/// </summary>
/// <remarks>
/// The Open XML SDK needs a seekable stream, because a package is a zip archive whose
/// directory is at the end. So the content is buffered, and the bound is what keeps
/// buffering it safe.
/// </remarks>
internal static class OpenXmlContent
{
    private const int CopyBufferSize = 81_920;

    /// <summary>
    /// The signature of a compound file, which is what an encrypted Open XML document is.
    /// </summary>
    private static readonly byte[] s_compoundFileSignature =
        [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    /// <summary>
    /// Reads the part into a seekable stream.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <param name="options">The bounds.</param>
    /// <param name="cancellationToken">The token to cancel the read with.</param>
    /// <returns>The stream, positioned at the start. The caller disposes it.</returns>
    /// <exception cref="ExtractionException">
    /// The part is text, is encrypted, or is larger than the bound.
    /// </exception>
    public static async ValueTask<MemoryStream> ReadAsync(
        DocumentPart part,
        OpenXmlExtractorOptions options,
        CancellationToken cancellationToken)
    {
        if (part.Content.Kind == DocumentContentKind.Text)
        {
            throw new ExtractionException(OpenXmlErrors.NotBinary(part.Id));
        }

        var buffer = part.Content.Kind == DocumentContentKind.Bytes
            ? Copy(part, options)
            : await CopyAsync(part, options, cancellationToken).ConfigureAwait(false);

        RefuseCompoundFile(part.Id, buffer);

        return buffer;
    }

    private static MemoryStream Copy(DocumentPart part, OpenXmlExtractorOptions options)
    {
        var bytes = part.Content.AsBytes();

        if (bytes.Length > options.MaxBytes)
        {
            throw new ExtractionException(OpenXmlErrors.TooLarge(part.Id, options.MaxBytes));
        }

        var buffer = new MemoryStream(bytes.Length);
        buffer.Write(bytes.Span);
        buffer.Position = 0;

        return buffer;
    }

    /// <summary>
    /// Copies the part's stream into a buffer, stopping the moment it crosses the bound.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <param name="options">The bounds.</param>
    /// <param name="cancellationToken">The token to cancel the copy with.</param>
    /// <returns>The buffer.</returns>
    /// <exception cref="ExtractionException">The stream is larger than the bound.</exception>
    /// <remarks>
    /// The bound is checked while copying rather than afterwards, because a length checked
    /// after the fact has already cost the memory it was there to refuse. A stream need not
    /// report a length, and one that reports a false one is the case this guards against.
    /// </remarks>
    private static async ValueTask<MemoryStream> CopyAsync(
        DocumentPart part,
        OpenXmlExtractorOptions options,
        CancellationToken cancellationToken)
    {
        var source = await part.Content.OpenStreamAsync(cancellationToken).ConfigureAwait(false);

        await using (source.ConfigureAwait(false))
        {
            var buffer = new MemoryStream();
            var window = new byte[CopyBufferSize];
            int read;

            while ((read = await source.ReadAsync(window, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > options.MaxBytes)
                {
                    await buffer.DisposeAsync().ConfigureAwait(false);

                    throw new ExtractionException(OpenXmlErrors.TooLarge(part.Id, options.MaxBytes));
                }

                await buffer.WriteAsync(window.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            buffer.Position = 0;

            return buffer;
        }
    }

    /// <summary>
    /// Refuses a compound file before the package reader sees it.
    /// </summary>
    /// <param name="partId">The part.</param>
    /// <param name="buffer">The buffered content.</param>
    /// <exception cref="ExtractionException">The content is a compound file.</exception>
    /// <remarks>
    /// A password-protected Word or Excel file is not a zip archive at all: it is an OLE
    /// compound file whose streams hold the encrypted package. Handing it to the package
    /// reader produces the same "corrupted data" failure as a truncated archive, and the two
    /// are not the same thing to the caller — one is a document nobody can read, the other is
    /// a document nobody gave us the key to. The signature tells them apart.
    /// </remarks>
    private static void RefuseCompoundFile(PartId partId, MemoryStream buffer)
    {
        if (buffer.Length < s_compoundFileSignature.Length)
        {
            return;
        }

        var head = buffer.GetBuffer().AsSpan(0, s_compoundFileSignature.Length);

        if (head.SequenceEqual(s_compoundFileSignature))
        {
            buffer.Dispose();

            throw new ExtractionException(OpenXmlErrors.Encrypted(partId));
        }
    }
}
