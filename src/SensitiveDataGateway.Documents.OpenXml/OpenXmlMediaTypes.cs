using System;

namespace SensitiveDataGateway.Documents.OpenXml;

/// <summary>
/// The media types the Open XML extractors claim.
/// </summary>
internal static class OpenXmlMediaTypes
{
    /// <summary>
    /// The media type of a Word document.
    /// </summary>
    public const string Wordprocessing =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    /// <summary>
    /// The media type of an Excel workbook.
    /// </summary>
    public const string Spreadsheet =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>
    /// Returns the media type without its parameters, lowercased.
    /// </summary>
    /// <param name="mediaType">The media type as it was declared.</param>
    /// <returns>The essence.</returns>
    public static string Essence(string mediaType)
    {
        var separator = mediaType.IndexOf(';', StringComparison.Ordinal);
        var essence = separator < 0 ? mediaType : mediaType[..separator];

        return essence.Trim().ToLowerInvariant();
    }
}
