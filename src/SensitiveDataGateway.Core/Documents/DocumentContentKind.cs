namespace SensitiveDataGateway.Documents;

/// <summary>
/// The form in which the content of a part is held.
/// </summary>
public enum DocumentContentKind
{
    /// <summary>
    /// Text the application already has in memory.
    /// </summary>
    Text = 1,

    /// <summary>
    /// Bytes the application already has in memory.
    /// </summary>
    Bytes = 2,

    /// <summary>
    /// Bytes an extractor obtains by opening a stream when it needs them.
    /// </summary>
    /// <remarks>
    /// A stream keeps a large attachment out of memory until something asks for it, and it
    /// lets the size limits refuse a part before a single byte is read.
    /// </remarks>
    Stream = 3,
}
