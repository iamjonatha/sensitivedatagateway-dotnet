namespace SensitiveDataGateway.Documents;

/// <summary>
/// The role a part plays in the bundle it belongs to.
/// </summary>
/// <remarks>
/// The kind is provenance, not trust. No kind is exempt from scanning: a subject line and a
/// file name are frequently the most sensitive strings in an email.
/// </remarks>
public enum PartKind
{
    /// <summary>
    /// The main text of the document.
    /// </summary>
    Body = 1,

    /// <summary>
    /// The subject of a message.
    /// </summary>
    Subject = 2,

    /// <summary>
    /// A named header, such as a sender or recipient field.
    /// </summary>
    Header = 3,

    /// <summary>
    /// A file carried alongside the document.
    /// </summary>
    Attachment = 4,

    /// <summary>
    /// Something nested inside another part, such as an attachment of a forwarded message.
    /// </summary>
    Embedded = 5,
}
