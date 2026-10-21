using System;

namespace SensitiveDataGateway.Documents.Recognition;

/// <summary>
/// Which recognizer read a part, at which version, and from where.
/// </summary>
/// <remarks>
/// The locality belongs in the version rather than beside it because it is part of the claim
/// an audit record makes. "This page was read by engine X at version Y" does not say whether
/// the page left the building; "by engine X at version Y, remotely" does, and that sentence
/// is the one a reviewer needs.
/// </remarks>
public readonly struct RecognizerVersion : IEquatable<RecognizerVersion>
{
    /// <summary>
    /// The greatest number of characters in a version string.
    /// </summary>
    public const int MaxVersionLength = 64;

    private readonly string? _version;

    private RecognizerVersion(RecognizerId recognizerId, string version, TrustBoundaryLocality locality)
    {
        RecognizerId = recognizerId;
        _version = version;
        Locality = locality;
    }

    /// <summary>
    /// Gets the recognizer.
    /// </summary>
    public RecognizerId RecognizerId { get; }

    /// <summary>
    /// Gets where the recognizer runs.
    /// </summary>
    public TrustBoundaryLocality Locality { get; }

    /// <summary>
    /// Gets the version of the recognizer.
    /// </summary>
    /// <exception cref="InvalidOperationException">This is the default value of the struct.</exception>
    public string Version =>
        _version ?? throw new InvalidOperationException("The recognizer version carries no value.");

    /// <summary>
    /// Gets a value indicating whether this is the default value of the struct.
    /// </summary>
    public bool IsDefault => _version is null;

    /// <summary>
    /// Gets a value indicating whether the recognizer reads content inside the trust boundary.
    /// </summary>
    public bool IsInsideTheTrustBoundary => Locality.IsInsideTheTrustBoundary();

    /// <summary>
    /// Creates a recognizer version.
    /// </summary>
    /// <param name="recognizerId">The recognizer.</param>
    /// <param name="version">The version of the recognizer.</param>
    /// <param name="locality">Where the recognizer runs.</param>
    /// <returns>The recognizer version.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="version"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="recognizerId"/> is the default value, the version is empty, too long or
    /// has a control character, or the locality is not one of the declared values.
    /// </exception>
    /// <remarks>
    /// An undeclared locality is refused rather than defaulted. The safe default would be
    /// <see cref="TrustBoundaryLocality.Remote"/>, which would refuse every engine that forgot
    /// to say; the honest answer is that an engine which does not state where it runs has not
    /// been reviewed.
    /// </remarks>
    public static RecognizerVersion Create(RecognizerId recognizerId, string version, TrustBoundaryLocality locality)
    {
        if (recognizerId.IsDefault)
        {
            throw new ArgumentException("The recognizer identifier carries no value.", nameof(recognizerId));
        }

        if (!locality.IsDeclared())
        {
            throw new ArgumentException("The recognizer must state where it runs.", nameof(locality));
        }

        return new RecognizerVersion(
            recognizerId,
            Text.Require(version, MaxVersionLength, nameof(version)),
            locality);
    }

    /// <inheritdoc />
    public bool Equals(RecognizerVersion other) =>
        RecognizerId == other.RecognizerId
        && Locality == other.Locality
        && string.Equals(_version, other._version, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is RecognizerVersion other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(
            RecognizerId,
            Locality,
            _version is null ? 0 : StringComparer.Ordinal.GetHashCode(_version));

    /// <summary>
    /// Returns the recognizer, its version and its locality.
    /// </summary>
    /// <returns>A diagnostic string, or an empty string for the default value.</returns>
    /// <remarks>
    /// This is the string that reaches an audit record through the extractor version, so the
    /// locality is spelled out rather than abbreviated.
    /// </remarks>
    public override string ToString() =>
        _version is null
            ? string.Empty
            : string.Concat(RecognizerId.ToString(), "@", _version, "/", Locality.Describe());

    /// <summary>
    /// Determines whether two recognizer versions are the same.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when they name the same recognizer, version and locality.</returns>
    public static bool operator ==(RecognizerVersion left, RecognizerVersion right) => left.Equals(right);

    /// <summary>
    /// Determines whether two recognizer versions differ.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    public static bool operator !=(RecognizerVersion left, RecognizerVersion right) => !left.Equals(right);
}
