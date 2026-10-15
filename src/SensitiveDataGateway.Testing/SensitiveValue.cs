using System;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Testing;

/// <summary>
/// One value in a synthetic document that must never reach a decision provider.
/// </summary>
/// <remarks>
/// Every value in this library's corpus is invented. None of them belongs to a person,
/// none of them is a working credential, and the ones that carry a check digit were
/// generated to satisfy it so that the deterministic detectors treat them as real shapes.
/// </remarks>
public sealed class SensitiveValue
{
    private SensitiveValue(EntityType type, string text)
    {
        Type = type;
        Text = text;
    }

    /// <summary>
    /// Gets the kind of value this is.
    /// </summary>
    public EntityType Type { get; }

    /// <summary>
    /// Gets the exact text as it appears in the document.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Creates an annotated value.
    /// </summary>
    /// <param name="type">The kind of value.</param>
    /// <param name="text">The exact text as it appears in the document.</param>
    /// <returns>The annotated value.</returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> is empty, white space, or spans more than one line.</exception>
    public static SensitiveValue Of(EntityType type, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (text.Contains('\n', StringComparison.Ordinal) || text.Contains('\r', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "An annotated value must sit on one line, because extraction is free to "
                + "split a document at a line break and a value split across two segments "
                + "cannot be asserted on as one string.",
                nameof(text));
        }

        return new SensitiveValue(type, text);
    }

    /// <summary>
    /// Returns a description that does not repeat the value.
    /// </summary>
    /// <returns>The entity type and the length of the value.</returns>
    public override string ToString() => $"{Type} ({Text.Length} characters)";
}
