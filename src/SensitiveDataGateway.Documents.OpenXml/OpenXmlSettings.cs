using DocumentFormat.OpenXml.Packaging;

namespace SensitiveDataGateway.Documents.OpenXml;

/// <summary>
/// Builds the settings the Open XML SDK is driven with.
/// </summary>
internal static class OpenXmlSettings
{
    /// <summary>
    /// Returns the settings for the given bounds.
    /// </summary>
    /// <param name="options">The bounds.</param>
    /// <returns>The settings.</returns>
    /// <remarks>
    /// <para>
    /// <c>AutoSave</c> is off because this library never writes a document. A reader that can
    /// flush changes back is a reader that can corrupt the caller's file, and nothing here
    /// has any business modifying what it was asked to scan.
    /// </para>
    /// <para>
    /// <c>MaxCharactersInPart</c> is the SDK's own defence against a part that is small
    /// compressed and enormous expanded. It is enforced while reading, which is the only
    /// place a bound on expansion is of any use.
    /// </para>
    /// <para>
    /// Markup compatibility processing is left alone. It decides which alternate content
    /// branch of the markup is read, and asking the SDK to process it means asking it to
    /// discard the branches it did not choose — branches whose text is in the file and would
    /// then be in nothing this library scanned.
    /// </para>
    /// </remarks>
    public static OpenSettings For(OpenXmlExtractorOptions options) => new()
    {
        AutoSave = false,
        MaxCharactersInPart = options.MaxCharactersInPart,
    };
}
