using System;
using System.Globalization;

namespace SensitiveDataGateway.Documents;

/// <summary>
/// Where in a part a text segment came from.
/// </summary>
/// <remarks>
/// <para>
/// Provenance is what makes an audit record meaningful and human review possible. Knowing
/// that a transformation was applied to "page 4" or to the cell "B12" is the difference
/// between an explanation and a claim.
/// </para>
/// <para>
/// <see cref="Locator"/> may itself come from the document — a sheet name or a header name is
/// authored text. <see cref="ToString"/> therefore omits it, so that the obvious way to write
/// an origin into a log is also the safe one.
/// </para>
/// </remarks>
public readonly struct SegmentOrigin : IEquatable<SegmentOrigin>
{
    /// <summary>
    /// The greatest number of characters in a locator.
    /// </summary>
    public const int MaxLocatorLength = 128;

    private SegmentOrigin(SegmentOriginKind kind, int? ordinal, string? locator)
    {
        Kind = kind;
        Ordinal = ordinal;
        Locator = locator;
    }

    /// <summary>
    /// Gets a value indicating that the extractor could not describe the structure.
    /// </summary>
    public static SegmentOrigin Unspecified => default;

    /// <summary>
    /// Gets the structural element the segment came from.
    /// </summary>
    public SegmentOriginKind Kind { get; }

    /// <summary>
    /// Gets the one-based position of the element within the part, when it has one.
    /// </summary>
    /// <value>A page number, a paragraph index, a row number, or <see langword="null"/>.</value>
    public int? Ordinal { get; }

    /// <summary>
    /// Gets the name of the element, when it has one.
    /// </summary>
    /// <value>
    /// A cell reference, a sheet name or a header name, or <see langword="null"/> when the
    /// element is only numbered.
    /// </value>
    /// <remarks>
    /// A locator that names something the document's author chose is content. Treat it as
    /// such: it belongs in an audit record that is stored under the same controls as the
    /// document, and not in an application log.
    /// </remarks>
    public string? Locator { get; }

    /// <summary>
    /// Creates an origin that numbers an element.
    /// </summary>
    /// <param name="kind">The structural element.</param>
    /// <param name="ordinal">The one-based position of the element within the part.</param>
    /// <returns>The origin.</returns>
    /// <exception cref="ArgumentException"><paramref name="kind"/> is not a defined value.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ordinal"/> is not positive.</exception>
    public static SegmentOrigin At(SegmentOriginKind kind, int ordinal)
    {
        RequireDefined(kind);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ordinal);

        return new SegmentOrigin(kind, ordinal, locator: null);
    }

    /// <summary>
    /// Creates an origin that names an element.
    /// </summary>
    /// <param name="kind">The structural element.</param>
    /// <param name="locator">The name of the element.</param>
    /// <returns>The origin.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="kind"/> is not a defined value, or the locator is empty, too long, or
    /// has a control character.
    /// </exception>
    public static SegmentOrigin Named(SegmentOriginKind kind, string locator)
    {
        RequireDefined(kind);

        return new SegmentOrigin(kind, ordinal: null, Text.Require(locator, MaxLocatorLength, nameof(locator)));
    }

    /// <summary>
    /// Creates an origin that both numbers and names an element.
    /// </summary>
    /// <param name="kind">The structural element.</param>
    /// <param name="ordinal">The one-based position of the element within the part.</param>
    /// <param name="locator">The name of the element.</param>
    /// <returns>The origin.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="kind"/> is not a defined value, or the locator is empty, too long, or
    /// has a control character.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ordinal"/> is not positive.</exception>
    public static SegmentOrigin Create(SegmentOriginKind kind, int ordinal, string locator)
    {
        RequireDefined(kind);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ordinal);

        return new SegmentOrigin(kind, ordinal, Text.Require(locator, MaxLocatorLength, nameof(locator)));
    }

    /// <inheritdoc />
    public bool Equals(SegmentOrigin other) =>
        Kind == other.Kind
        && Ordinal == other.Ordinal
        && string.Equals(Locator, other.Locator, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SegmentOrigin other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(Kind, Ordinal, Locator is null ? 0 : StringComparer.Ordinal.GetHashCode(Locator));

    /// <summary>
    /// Returns a description of the origin.
    /// </summary>
    /// <returns>
    /// The structural element and its number. The locator is deliberately left out, because
    /// it can be authored text.
    /// </returns>
    public override string ToString() =>
        Ordinal is { } ordinal
            ? string.Concat(Kind.ToString(), " ", ordinal.ToString(CultureInfo.InvariantCulture))
            : Kind.ToString();

    /// <summary>
    /// Determines whether two origins are the same.
    /// </summary>
    /// <param name="left">The first origin.</param>
    /// <param name="right">The second origin.</param>
    /// <returns><see langword="true"/> when they describe the same element.</returns>
    public static bool operator ==(SegmentOrigin left, SegmentOrigin right) => left.Equals(right);

    /// <summary>
    /// Determines whether two origins differ.
    /// </summary>
    /// <param name="left">The first origin.</param>
    /// <param name="right">The second origin.</param>
    /// <returns><see langword="true"/> when they describe different elements.</returns>
    public static bool operator !=(SegmentOrigin left, SegmentOrigin right) => !left.Equals(right);

    private static void RequireDefined(SegmentOriginKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentException("The origin kind is not a defined value.", nameof(kind));
        }
    }
}
