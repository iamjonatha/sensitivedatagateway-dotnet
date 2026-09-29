using System;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// Which detector produced a finding, and at which version.
/// </summary>
/// <remarks>
/// A sanitization is only reproducible if the set of detectors that ran is recorded. Without
/// it, "this document was scanned and found clean" is a statement about whatever happened to
/// be registered that day, which is not something an audit can rely on.
/// </remarks>
public readonly struct DetectorVersion : IEquatable<DetectorVersion>
{
    /// <summary>
    /// The greatest number of characters in a version string.
    /// </summary>
    public const int MaxVersionLength = 64;

    private readonly string? _version;

    private DetectorVersion(DetectorId detectorId, string version)
    {
        DetectorId = detectorId;
        _version = version;
    }

    /// <summary>
    /// Gets the detector.
    /// </summary>
    public DetectorId DetectorId { get; }

    /// <summary>
    /// Gets the version of the detector.
    /// </summary>
    /// <exception cref="InvalidOperationException">This is the default value of the struct.</exception>
    public string Version =>
        _version ?? throw new InvalidOperationException("The detector version carries no value.");

    /// <summary>
    /// Gets a value indicating whether this is the default value of the struct.
    /// </summary>
    public bool IsDefault => _version is null;

    /// <summary>
    /// Creates a detector version.
    /// </summary>
    /// <param name="detectorId">The detector.</param>
    /// <param name="version">The version of the detector.</param>
    /// <returns>The detector version.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="version"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="detectorId"/> is the default value, or the version is empty, too long,
    /// or has a control character.
    /// </exception>
    public static DetectorVersion Create(DetectorId detectorId, string version)
    {
        if (detectorId.IsDefault)
        {
            throw new ArgumentException("The detector identifier carries no value.", nameof(detectorId));
        }

        return new DetectorVersion(detectorId, Text.Require(version, MaxVersionLength, nameof(version)));
    }

    /// <inheritdoc />
    public bool Equals(DetectorVersion other) =>
        DetectorId == other.DetectorId && string.Equals(_version, other._version, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DetectorVersion other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(DetectorId, _version is null ? 0 : StringComparer.Ordinal.GetHashCode(_version));

    /// <summary>
    /// Returns the detector and its version.
    /// </summary>
    /// <returns>A diagnostic string, or an empty string for the default value.</returns>
    public override string ToString() =>
        _version is null ? string.Empty : string.Concat(DetectorId.ToString(), "@", _version);

    /// <summary>
    /// Determines whether two detector versions are the same.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when they name the same detector at the same version.</returns>
    public static bool operator ==(DetectorVersion left, DetectorVersion right) => left.Equals(right);

    /// <summary>
    /// Determines whether two detector versions differ.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    public static bool operator !=(DetectorVersion left, DetectorVersion right) => !left.Equals(right);
}
