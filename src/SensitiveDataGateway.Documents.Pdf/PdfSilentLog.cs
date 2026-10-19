using System;
using UglyToad.PdfPig.Logging;

namespace SensitiveDataGateway.Documents.Pdf;

/// <summary>
/// The logger the parser is given, which discards everything.
/// </summary>
/// <remarks>
/// The parser logs the objects it stumbles over, and the object it stumbled over is a piece
/// of the document. The default logger already discards its input; this type is installed
/// anyway, so that the guarantee is stated in this repository rather than depended upon in
/// someone else's, and so that a future release of the parser cannot quietly start writing
/// document content to a console.
/// </remarks>
internal sealed class PdfSilentLog : ILog
{
    /// <summary>
    /// Gets the single instance.
    /// </summary>
    public static PdfSilentLog Instance { get; } = new();

    /// <inheritdoc/>
    public void Debug(string message)
    {
        // Discarded: the message may quote the document.
    }

    /// <inheritdoc/>
    public void Debug(string message, Exception ex)
    {
        // Discarded: the message may quote the document.
    }

    /// <inheritdoc/>
    public void Warn(string message)
    {
        // Discarded: the message may quote the document.
    }

    /// <inheritdoc/>
    public void Error(string message)
    {
        // Discarded: the message may quote the document.
    }

    /// <inheritdoc/>
    public void Error(string message, Exception ex)
    {
        // Discarded: the message may quote the document.
    }
}
