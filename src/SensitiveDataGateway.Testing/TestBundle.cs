using System;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Testing;

/// <summary>
/// Builders for the document bundles a test feeds to the gateway.
/// </summary>
public static class TestBundle
{
    /// <summary>
    /// The media type every part built here declares.
    /// </summary>
    public const string MediaType = "text/plain";

    /// <summary>
    /// Builds a bundle with a single body part.
    /// </summary>
    /// <param name="text">The text of the body.</param>
    /// <returns>The bundle.</returns>
    public static DocumentBundle FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return DocumentBundle.Create(BundleId.New(), Part(PartKind.Body, text));
    }

    /// <summary>
    /// Builds a bundle whose body is followed by one attachment.
    /// </summary>
    /// <param name="body">The text of the body.</param>
    /// <param name="attachment">The text of the attachment.</param>
    /// <returns>The bundle.</returns>
    /// <remarks>
    /// Attachments are where hostile content usually arrives, so a test that only ever
    /// builds a body is not testing the interesting half of the surface.
    /// </remarks>
    public static DocumentBundle WithAttachment(string body, string attachment)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(attachment);

        return DocumentBundle.Create(
            BundleId.New(),
            Part(PartKind.Body, body),
            [Part(PartKind.Attachment, attachment)]);
    }

    /// <summary>
    /// Builds a bundle with one body part holding the given number of characters.
    /// </summary>
    /// <param name="characters">The number of characters the body holds.</param>
    /// <returns>The bundle.</returns>
    /// <remarks>
    /// The filler is ordinary prose with no sensitive shape in it, so a test that uses this
    /// to cross a size limit is measuring the limit and nothing else.
    /// </remarks>
    public static DocumentBundle OfSize(int characters)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(characters);
        return FromText(new string('a', characters));
    }

    private static DocumentPart Part(PartKind kind, string text) =>
        DocumentPart.Create(PartId.New(), kind, MediaType, DocumentContent.FromText(text));
}
