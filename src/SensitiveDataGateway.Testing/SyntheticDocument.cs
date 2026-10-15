using System.Collections.Generic;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Testing;

/// <summary>
/// A document from <see cref="SyntheticCorpus"/>, together with the values it is known to
/// contain.
/// </summary>
public sealed class SyntheticDocument
{
    private readonly string? _attachment;

    internal SyntheticDocument(
        string name,
        string language,
        string body,
        string? attachment,
        bool isPromptInjection,
        IReadOnlyList<SensitiveValue> values)
    {
        Name = name;
        Language = language;
        Body = body;
        _attachment = attachment;
        IsPromptInjection = isPromptInjection;
        Values = values;
    }

    /// <summary>
    /// Gets the name of the document, used in assertion failures.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the BCP 47 tag of the language the prose is written in.
    /// </summary>
    /// <remarks>
    /// The detectors this library ships recognize shapes, not words, so they are expected
    /// to behave identically whatever this says. A corpus in one language would never catch
    /// a detector that had quietly been anchored to English prose.
    /// </remarks>
    public string Language { get; }

    /// <summary>
    /// Gets the text of the body part.
    /// </summary>
    public string Body { get; }

    /// <summary>
    /// Gets a value indicating whether the document carries an attachment.
    /// </summary>
    public bool HasAttachment => _attachment is not null;

    /// <summary>
    /// Gets a value indicating whether the document tries to instruct whoever reads it.
    /// </summary>
    /// <remarks>
    /// The text in these documents is data. It is in the corpus so that a test can prove
    /// the gateway treats it as data, and it is not an instruction to anything that reads
    /// this repository.
    /// </remarks>
    public bool IsPromptInjection { get; }

    /// <summary>
    /// Gets the values the document is known to contain, none of which may reach a provider.
    /// </summary>
    public IReadOnlyList<SensitiveValue> Values { get; }

    /// <summary>
    /// Gets the text of each annotated value.
    /// </summary>
    public IReadOnlyList<string> Texts
    {
        get
        {
            var texts = new string[Values.Count];
            for (var i = 0; i < Values.Count; i++)
            {
                texts[i] = Values[i].Text;
            }

            return texts;
        }
    }

    /// <summary>
    /// Builds a bundle from this document.
    /// </summary>
    /// <returns>A bundle with a fresh identifier, a body part, and an attachment if the document has one.</returns>
    public DocumentBundle ToBundle() =>
        _attachment is null ? TestBundle.FromText(Body) : TestBundle.WithAttachment(Body, _attachment);

    /// <summary>
    /// Returns a description that does not repeat the content.
    /// </summary>
    /// <returns>The name, the language and the number of annotated values.</returns>
    public override string ToString() => $"{Name} [{Language}] with {Values.Count} annotated value(s)";
}
