using System;

namespace SensitiveDataGateway.Documents.Pdf;

/// <summary>
/// The media type this package reads, and the comparison it is recognized by.
/// </summary>
internal static class PdfMediaTypes
{
    /// <summary>
    /// The media type of a PDF document.
    /// </summary>
    public const string Pdf = "application/pdf";

    /// <summary>
    /// Returns the media type without its parameters, lowercased.
    /// </summary>
    /// <param name="mediaType">The declared media type.</param>
    /// <returns>The essence.</returns>
    /// <remarks>
    /// A part keeps the parameters it was declared with, so a comparison that does not strip
    /// them misses <c>application/pdf; version=1.7</c> and the part goes unread.
    /// </remarks>
    public static string Essence(string mediaType)
    {
        var separator = mediaType.IndexOf(';', StringComparison.Ordinal);
        var essence = separator < 0 ? mediaType : mediaType[..separator];

        return essence.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Returns whether a media type names a PDF document.
    /// </summary>
    /// <param name="essence">The media type, without parameters.</param>
    /// <returns><see langword="true"/> when the type is a PDF.</returns>
    public static bool IsPdf(string essence) =>
        string.Equals(essence, Pdf, StringComparison.Ordinal);
}
