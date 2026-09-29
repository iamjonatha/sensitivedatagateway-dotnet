using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace SensitiveDataGateway.Documents;

/// <summary>
/// What the application knows about a part or a bundle, as a bounded set of named values.
/// </summary>
/// <remarks>
/// <para>
/// Metadata is content. A file name, a subject, a sender header and a declared origin are
/// scanned exactly like the body, because in practice they are where the most sensitive
/// strings in a bundle are found. Nothing here is exempt, and nothing here is logged.
/// </para>
/// <para>
/// The bag is bounded on purpose. An unbounded property set is an unbounded amount of text
/// to scan, and a stage that has to scan an unknown quantity cannot be given a limit that
/// means anything.
/// </para>
/// </remarks>
public sealed class DocumentMetadata
{
    /// <summary>
    /// The greatest number of custom properties a metadata set may carry.
    /// </summary>
    public const int MaxProperties = 32;

    /// <summary>
    /// The greatest number of characters in a property name.
    /// </summary>
    public const int MaxPropertyNameLength = 64;

    /// <summary>
    /// The greatest number of characters in a property value.
    /// </summary>
    public const int MaxPropertyValueLength = 4096;

    /// <summary>
    /// The greatest number of characters in a file name.
    /// </summary>
    public const int MaxFileNameLength = 512;

    /// <summary>
    /// The greatest number of characters in a language tag.
    /// </summary>
    public const int MaxLanguageLength = 64;

    /// <summary>
    /// The greatest number of characters in an origin.
    /// </summary>
    public const int MaxOriginLength = 256;

    private DocumentMetadata(
        string? fileName,
        string? language,
        string? origin,
        FrozenDictionary<string, string> properties)
    {
        FileName = fileName;
        Language = language;
        Origin = origin;
        Properties = properties;
    }

    /// <summary>
    /// Gets a metadata set that states nothing.
    /// </summary>
    public static DocumentMetadata Empty { get; } = new(
        fileName: null,
        language: null,
        origin: null,
        FrozenDictionary<string, string>.Empty);

    /// <summary>
    /// Gets the name the application knows the part by, if any.
    /// </summary>
    public string? FileName { get; }

    /// <summary>
    /// Gets the language the application declares for the part, if any.
    /// </summary>
    /// <remarks>
    /// The value is what the application claims, not what the gateway verified. A detector
    /// may use it to choose a rule set, and must still behave correctly when it is wrong.
    /// </remarks>
    public string? Language { get; }

    /// <summary>
    /// Gets where the application says the part came from, if anywhere.
    /// </summary>
    public string? Origin { get; }

    /// <summary>
    /// Gets the custom properties, compared ordinally.
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties { get; }

    /// <summary>
    /// Creates a metadata set.
    /// </summary>
    /// <param name="fileName">The name the application knows the part by, or <see langword="null"/>.</param>
    /// <param name="language">The declared language, or <see langword="null"/>.</param>
    /// <param name="origin">The declared origin, or <see langword="null"/>.</param>
    /// <returns>The metadata.</returns>
    /// <exception cref="ArgumentException">A supplied value is empty, too long, or has a control character.</exception>
    public static DocumentMetadata Create(string? fileName, string? language, string? origin) =>
        new(
            Text.Allow(fileName, MaxFileNameLength, nameof(fileName)),
            Text.Allow(language, MaxLanguageLength, nameof(language)),
            Text.Allow(origin, MaxOriginLength, nameof(origin)),
            FrozenDictionary<string, string>.Empty);

    /// <summary>
    /// Creates a metadata set with custom properties.
    /// </summary>
    /// <param name="fileName">The name the application knows the part by, or <see langword="null"/>.</param>
    /// <param name="language">The declared language, or <see langword="null"/>.</param>
    /// <param name="origin">The declared origin, or <see langword="null"/>.</param>
    /// <param name="properties">The custom properties. They are copied.</param>
    /// <returns>The metadata.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="properties"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A supplied value is invalid, a property name repeats, or there are more than
    /// <see cref="MaxProperties"/> properties.
    /// </exception>
    public static DocumentMetadata Create(
        string? fileName,
        string? language,
        string? origin,
        IEnumerable<KeyValuePair<string, string>> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        var builder = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var property in properties)
        {
            var name = Text.Require(property.Key, MaxPropertyNameLength, nameof(properties));

            if (!builder.TryAdd(name, RequireValue(property.Value, nameof(properties))))
            {
                throw new ArgumentException(
                    "A property name appears more than once.",
                    nameof(properties));
            }
        }

        if (builder.Count > MaxProperties)
        {
            throw new ArgumentException(
                $"A metadata set carries at most {MaxProperties} properties.",
                nameof(properties));
        }

        return new DocumentMetadata(
            Text.Allow(fileName, MaxFileNameLength, nameof(fileName)),
            Text.Allow(language, MaxLanguageLength, nameof(language)),
            Text.Allow(origin, MaxOriginLength, nameof(origin)),
            builder.ToFrozenDictionary(StringComparer.Ordinal));
    }

    /// <summary>
    /// Returns a copy of this metadata set with one property added or replaced.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The property value.</param>
    /// <returns>The new metadata set.</returns>
    /// <exception cref="ArgumentException">
    /// The name or the value is invalid, or adding the property would exceed
    /// <see cref="MaxProperties"/>.
    /// </exception>
    public DocumentMetadata WithProperty(string name, string value)
    {
        var propertyName = Text.Require(name, MaxPropertyNameLength, nameof(name));
        var propertyValue = RequireValue(value, nameof(value));

        var builder = new Dictionary<string, string>(Properties, StringComparer.Ordinal)
        {
            [propertyName] = propertyValue,
        };

        if (builder.Count > MaxProperties)
        {
            throw new ArgumentException(
                $"A metadata set carries at most {MaxProperties} properties.",
                nameof(name));
        }

        return new DocumentMetadata(
            FileName,
            Language,
            Origin,
            builder.ToFrozenDictionary(StringComparer.Ordinal));
    }

    private static string RequireValue(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        if (value.Length > MaxPropertyValueLength)
        {
            throw new ArgumentException(
                $"A property value holds at most {MaxPropertyValueLength} characters.",
                parameterName);
        }

        return value;
    }
}
