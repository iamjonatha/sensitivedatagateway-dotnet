using System.Threading;
using System.Threading.Tasks;

namespace SensitiveDataGateway.Documents.Recognition;

/// <summary>
/// Reads text out of a part whose content is an image rather than characters.
/// </summary>
/// <remarks>
/// <para>
/// This interface exists so that optical character recognition has a shape in the library
/// without the library having an opinion about which engine performs it. Nothing in this
/// repository implements it, and nothing selects an implementation on an application's
/// behalf: a recognizer reaches the pipeline only through
/// <c>RecognizingExtractor</c>, which the application constructs itself.
/// </para>
/// <para>
/// A recognizer is the one component that may be given raw content from outside the trust
/// boundary, and <see cref="RecognizerVersion.Locality"/> is where it says so. An
/// implementation that reaches a third party's service and reports
/// <see cref="RecognizerLocality.InProcess"/> defeats every control the library has; the
/// locality is a declaration under review, not a measurement.
/// </para>
/// <para>
/// A recognizer <b>must not</b> classify, exactly as an extractor must not. It reads
/// characters off pixels and reports how sure it is; whether what it read is a name is a
/// later stage's decision.
/// </para>
/// <para>
/// A recognizer <b>must not</b> return its best guess silently. A line it could not read is
/// reported with a low confidence, or the call fails. Dropping an unreadable line produces a
/// page that scans clean because the part nobody could read was the part that mattered.
/// </para>
/// </remarks>
public interface ITextRecognizer
{
    /// <summary>
    /// Gets the identity, version and locality of this recognizer, for the audit record.
    /// </summary>
    RecognizerVersion Version { get; }

    /// <summary>
    /// Determines whether this recognizer handles a declared media type.
    /// </summary>
    /// <param name="mediaType">The lowercased media type declared by the part.</param>
    /// <returns><see langword="true"/> when the recognizer accepts the part.</returns>
    bool CanRecognize(string mediaType);

    /// <summary>
    /// Reads the text of a part.
    /// </summary>
    /// <param name="part">The part to read.</param>
    /// <param name="cancellationToken">A token that cancels the recognition.</param>
    /// <returns>The lines, in reading order, each with a confidence.</returns>
    /// <exception cref="ExtractionException">
    /// The part cannot be read. Recognition is a way of extracting, so it fails the way
    /// extraction fails and the host records it as one failed part rather than an abandoned
    /// bundle.
    /// </exception>
    ValueTask<RecognizedText> RecognizeAsync(DocumentPart part, CancellationToken cancellationToken);
}
