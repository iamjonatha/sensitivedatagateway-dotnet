using System;

namespace SensitiveDataGateway.Documents;

/// <summary>
/// Which extractor produced part of an extraction, and at which version.
/// </summary>
/// <remarks>
/// Extraction is the stage that decides what text the rest of the pipeline will ever see, so
/// an audit record that does not say which extractor ran cannot explain a result. Recording
/// the version is what makes "this document was scanned" a reproducible claim rather than a
/// statement about the software that happened to be deployed that day.
/// </remarks>
public readonly struct ExtractorVersion : IEquatable<ExtractorVersion>
{
    /// <summary>
    /// The greatest number of characters in a version string.
    /// </summary>
    public const int MaxVersionLength = 64;

    private readonly string? _version;

    private ExtractorVersion(ExtractorId extractorId, string version)
    {
        ExtractorId = extractorId;
        _version = version;
    }

    /// <summary>
    /// Gets the extractor.
    /// </summary>
    public ExtractorId ExtractorId { get; }

    /// <summary>
    /// Gets the version of the extractor.
    /// </summary>
    /// <exception cref="InvalidOperationException">This is the default value of the struct.</exception>
    public string Version =>
        _version ?? throw new InvalidOperationException("The extractor version carries no value.");

    /// <summary>
    /// Gets a value indicating whether this is the default value of the struct.
    /// </summary>
    public bool IsDefault => _version is null;

    /// <summary>
    /// Creates an extractor version.
    /// </summary>
    /// <param name="extractorId">The extractor.</param>
    /// <param name="version">The version of the extractor.</param>
    /// <returns>The extractor version.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="version"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="extractorId"/> is the default value, or the version is empty, too long,
    /// or has a control character.
    /// </exception>
    public static ExtractorVersion Create(ExtractorId extractorId, string version)
    {
        if (extractorId.IsDefault)
        {
            throw new ArgumentException("The extractor identifier carries no value.", nameof(extractorId));
        }

        return new ExtractorVersion(extractorId, Text.Require(version, MaxVersionLength, nameof(version)));
    }

    /// <inheritdoc />
    public bool Equals(ExtractorVersion other) =>
        ExtractorId == other.ExtractorId && string.Equals(_version, other._version, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ExtractorVersion other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(ExtractorId, _version is null ? 0 : StringComparer.Ordinal.GetHashCode(_version));

    /// <summary>
    /// Returns the extractor and its version.
    /// </summary>
    /// <returns>A diagnostic string, or an empty string for the default value.</returns>
    public override string ToString() =>
        _version is null ? string.Empty : string.Concat(ExtractorId.ToString(), "@", _version);

    /// <summary>
    /// Determines whether two extractor versions are the same.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when they name the same extractor at the same version.</returns>
    public static bool operator ==(ExtractorVersion left, ExtractorVersion right) => left.Equals(right);

    /// <summary>
    /// Determines whether two extractor versions differ.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    public static bool operator !=(ExtractorVersion left, ExtractorVersion right) => !left.Equals(right);
}
