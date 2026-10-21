using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace SensitiveDataGateway.Documents.Recognition;

/// <summary>
/// Names a text recognizer.
/// </summary>
/// <remarks>
/// <para>
/// A recognizer is the only component in the library that may be asked to read raw content
/// outside the trust boundary, so naming it is not a convenience. The identifier is what an
/// audit record carries to say which engine was handed a page, and it is the string a
/// reviewer looks for when asking where a document went.
/// </para>
/// <para>
/// The value is never derived from document content, for the reason stated on
/// <see cref="ExtractorId"/>: an identifier is allowed out of the boundary unchanged.
/// </para>
/// </remarks>
public readonly struct RecognizerId : IEquatable<RecognizerId>
{
    /// <summary>
    /// The greatest number of characters a recognizer identifier may contain once trimmed.
    /// </summary>
    public const int MaxLength = IdentifierValue.MaxLength;

    private readonly string? _value;

    private RecognizerId(string value) => _value = value;

    /// <summary>
    /// Gets the textual form of the identifier.
    /// </summary>
    /// <value>The trimmed, non-empty text the identifier was created from.</value>
    /// <exception cref="InvalidOperationException">
    /// The identifier is the default value of the struct and therefore carries no text.
    /// </exception>
    public string Value =>
        _value ?? throw new InvalidOperationException(
            "A default RecognizerId carries no value. Create one with RecognizerId.Create.");

    /// <summary>
    /// Gets a value indicating whether this identifier is the default value of the struct.
    /// </summary>
    public bool IsDefault => _value is null;

    /// <summary>
    /// Creates a recognizer identifier from an existing value.
    /// </summary>
    /// <param name="value">
    /// The identifier text. Surrounding white space is removed. The result must be non-empty
    /// and no longer than <see cref="MaxLength"/> characters.
    /// </param>
    /// <returns>The identifier.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is empty or white space, contains a control character, or is
    /// longer than <see cref="MaxLength"/> characters once trimmed.
    /// </exception>
    public static RecognizerId Create(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!TryCreate(value, out var identifier))
        {
            throw new ArgumentException(
                $"A recognizer identifier must be non-empty, free of control characters and at most {MaxLength.ToString(CultureInfo.InvariantCulture)} characters once trimmed.",
                nameof(value));
        }

        return identifier;
    }

    /// <summary>
    /// Attempts to create a recognizer identifier from an existing value.
    /// </summary>
    /// <param name="value">The identifier text, or <see langword="null"/>.</param>
    /// <param name="recognizerId">
    /// When this method returns <see langword="true"/>, the identifier; otherwise the default
    /// value of the struct.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when <paramref name="value"/> satisfies the rules described on
    /// <see cref="Create(string)"/>; otherwise <see langword="false"/>.
    /// </returns>
    public static bool TryCreate([NotNullWhen(true)] string? value, out RecognizerId recognizerId)
    {
        if (IdentifierValue.TryNormalize(value, out var normalized))
        {
            recognizerId = new RecognizerId(normalized);
            return true;
        }

        recognizerId = default;
        return false;
    }

    /// <inheritdoc />
    public bool Equals(RecognizerId other) =>
        string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RecognizerId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>
    /// Returns the textual form of the identifier.
    /// </summary>
    /// <returns>
    /// The value of the identifier, or an empty string when it is the default value of the
    /// struct.
    /// </returns>
    public override string ToString() => _value ?? string.Empty;

    /// <summary>
    /// Determines whether two identifiers are the same.
    /// </summary>
    /// <param name="left">The first identifier.</param>
    /// <param name="right">The second identifier.</param>
    /// <returns><see langword="true"/> when they carry the same value.</returns>
    public static bool operator ==(RecognizerId left, RecognizerId right) => left.Equals(right);

    /// <summary>
    /// Determines whether two identifiers differ.
    /// </summary>
    /// <param name="left">The first identifier.</param>
    /// <param name="right">The second identifier.</param>
    /// <returns><see langword="true"/> when they carry different values.</returns>
    public static bool operator !=(RecognizerId left, RecognizerId right) => !left.Equals(right);
}
