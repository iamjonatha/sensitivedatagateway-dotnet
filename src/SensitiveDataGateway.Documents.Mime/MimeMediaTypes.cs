using System;
using System.Collections.Generic;

namespace SensitiveDataGateway.Documents.Mime;

/// <summary>
/// The media types this package recognises, and what it does with each.
/// </summary>
/// <remarks>
/// The lists are allow-lists, deliberately. A <c>message/*</c> type that is not named here —
/// <c>message/external-body</c> and <c>message/partial</c> above all — is left as an opaque
/// part with its declared type, no extractor claims it, and the operation blocks. That is the
/// intended outcome: an external body is a pointer at something this library is not allowed
/// to fetch, and a partial message is a fragment whose other half was never scanned.
/// </remarks>
internal static class MimeMediaTypes
{
    /// <summary>
    /// The media type a text part is given once the reader has decoded it.
    /// </summary>
    public const string PlainText = "text/plain";

    /// <summary>
    /// The media type of a forwarded message.
    /// </summary>
    public const string Rfc822Message = "message/rfc822";

    private static readonly HashSet<string> s_envelopes = new(StringComparer.Ordinal)
    {
        Rfc822Message,
        "message/global",
        "message/news",
    };

    private static readonly HashSet<string> s_statusReports = new(StringComparer.Ordinal)
    {
        "message/delivery-status",
        "message/global-delivery-status",
        "message/disposition-notification",
        "message/global-disposition-notification",
    };

    /// <summary>
    /// Determines whether a media type is a message the reader may descend into.
    /// </summary>
    /// <param name="mediaType">The declared media type.</param>
    /// <returns><see langword="true"/> when the part carries a complete nested message.</returns>
    public static bool IsEnvelope(string mediaType) => s_envelopes.Contains(mediaType);

    /// <summary>
    /// Determines whether a media type announces a composite the parser did not take apart.
    /// </summary>
    /// <param name="mediaType">The declared media type.</param>
    /// <returns><see langword="true"/> when a leaf claims to be a multipart.</returns>
    /// <remarks>
    /// A <c>multipart/*</c> entity that did not arrive as a parsed multipart is a composite the
    /// parser gave up on and handed back as raw bytes. Its children were never walked, so no
    /// bound was ever applied to them and nothing inside it was scanned.
    /// </remarks>
    public static bool IsUnparsedComposite(string mediaType) =>
        Essence(mediaType).StartsWith("multipart/", StringComparison.Ordinal);

    /// <summary>
    /// Determines whether a media type is a delivery or disposition report.
    /// </summary>
    /// <param name="mediaType">The declared media type.</param>
    /// <returns><see langword="true"/> when the part carries machine-readable status fields.</returns>
    public static bool IsStatusReport(string mediaType) => s_statusReports.Contains(mediaType);

    /// <summary>
    /// Determines whether this package extracts a media type.
    /// </summary>
    /// <param name="mediaType">The declared media type.</param>
    /// <returns><see langword="true"/> when the structure extractor claims the part.</returns>
    public static bool IsStructure(string mediaType) =>
        IsEnvelope(mediaType) || IsStatusReport(mediaType);

    /// <summary>
    /// Returns a media type without its parameters.
    /// </summary>
    /// <param name="mediaType">The declared media type, already lowercased.</param>
    /// <returns>The type and subtype on their own.</returns>
    public static string Essence(string mediaType)
    {
        var separator = mediaType.IndexOf(';', StringComparison.Ordinal);

        return separator < 0 ? mediaType.Trim() : mediaType[..separator].Trim();
    }
}
