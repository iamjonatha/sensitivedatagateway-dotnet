using System;
using System.IO;
using System.Xml;
using DocumentFormat.OpenXml.Packaging;

namespace SensitiveDataGateway.Documents.OpenXml;

/// <summary>
/// Decides what an exception from the Open XML SDK means.
/// </summary>
internal static class OpenXmlFailures
{
    /// <summary>
    /// Returns whether an exception means the package could not be read.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns><see langword="true"/> when the document is malformed.</returns>
    /// <remarks>
    /// <para>
    /// A parser handed a hostile file throws more than its own exception type, and the ones
    /// listed here all mean the same thing: the bytes are not a package this reader can read.
    /// They are translated rather than swallowed — each one becomes a failure on the part,
    /// which blocks the operation.
    /// </para>
    /// <para>
    /// <see cref="XmlException"/> is on the list and carries weight beyond malformed markup:
    /// it is what the SDK throws when a part declares a <c>DOCTYPE</c>, because the reader
    /// prohibits document type definitions outright. A document that tries to declare an
    /// entity is refused here rather than resolved anywhere.
    /// </para>
    /// <para>
    /// Cancellation and resource exhaustion are absent on purpose, because neither is a
    /// statement about the document, and so is every failure this library raises itself.
    /// </para>
    /// </remarks>
    public static bool IsParseFailure(Exception exception) => exception
        is OpenXmlPackageException
        or FileFormatException
        or XmlException
        or InvalidDataException
        or InvalidOperationException
        or NotSupportedException
        or ArgumentException
        or FormatException
        or OverflowException
        or EndOfStreamException;
}
