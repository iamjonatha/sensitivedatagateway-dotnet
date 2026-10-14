using System;
using System.Globalization;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// Builds the identifiers detection assigns to the candidates it reports.
/// </summary>
/// <remarks>
/// <para>
/// An identifier is derived from the detector that found the candidate and the position of
/// the find in that detector's scan order. It is therefore deterministic: the same document
/// scanned twice produces the same identifiers, which is what lets two audit records of the
/// same operation be compared.
/// </para>
/// <para>
/// Nothing in the identifier comes from the document. An ordinal is a count of finds, and a
/// detector name is chosen by the code, never read out of the content.
/// </para>
/// </remarks>
internal static class DetectionIds
{
    /// <summary>
    /// The greatest number of characters a detector identifier may contain if its entity
    /// identifiers are to stay within the bound every identifier in the domain shares.
    /// </summary>
    public const int MaxDetectorIdLength = 96;

    /// <summary>
    /// Builds the identifier of one find.
    /// </summary>
    /// <param name="detectorId">The detector that found it.</param>
    /// <param name="ordinal">The zero-based position of the find in scan order.</param>
    /// <returns>The identifier.</returns>
    public static EntityId For(DetectorId detectorId, int ordinal) =>
        EntityId.Create(string.Create(
            CultureInfo.InvariantCulture,
            $"{detectorId.Value}-{ordinal:D6}"));

    /// <summary>
    /// Validates that a detector identifier leaves room for the ordinal.
    /// </summary>
    /// <param name="detectorId">The identifier to check.</param>
    /// <param name="parameterName">The name of the argument being validated.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="detectorId"/> is the default value or is too long.
    /// </exception>
    public static void Require(DetectorId detectorId, string parameterName)
    {
        if (detectorId.IsDefault)
        {
            throw new ArgumentException("The detector identifier carries no value.", parameterName);
        }

        if (detectorId.Value.Length > MaxDetectorIdLength)
        {
            throw new ArgumentException(
                $"A detector identifier may not exceed {MaxDetectorIdLength} characters, because "
                + "the identifier of every candidate it reports is derived from it.",
                parameterName);
        }
    }
}
