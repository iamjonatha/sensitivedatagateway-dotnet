using System;
using System.Globalization;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// Why a detector believes it found something, said without repeating what it found.
/// </summary>
/// <remarks>
/// <para>
/// Evidence is what makes a false positive diagnosable. It names the rule that fired, says
/// whether a checksum validated, carries the model score and records how long the match was —
/// enough to understand a mistake without a log line containing the account number that
/// caused it.
/// </para>
/// <para>
/// Nothing here quotes the matched text, and nothing here may be used to reconstruct it. Not
/// a prefix, not a suffix, not a masked rendering: a masked rendering of a short value is
/// frequently still the value.
/// </para>
/// </remarks>
public sealed class DetectionEvidence
{
    /// <summary>
    /// The greatest number of characters in a pattern name.
    /// </summary>
    public const int MaxPatternNameLength = 128;

    private DetectionEvidence(string? patternName, bool? checksumValidated, double? score, int matchLength)
    {
        PatternName = patternName;
        ChecksumValidated = checksumValidated;
        Score = score;
        MatchLength = matchLength;
    }

    /// <summary>
    /// Gets evidence that states nothing beyond the fact that a detector reported a match.
    /// </summary>
    public static DetectionEvidence None { get; } = new(
        patternName: null,
        checksumValidated: null,
        score: null,
        matchLength: 0);

    /// <summary>
    /// Gets the name of the rule, pattern or model that fired, if the detector named one.
    /// </summary>
    /// <remarks>
    /// This is a name chosen by the detector's author. It is never derived from the document.
    /// </remarks>
    public string? PatternName { get; }

    /// <summary>
    /// Gets whether a structural check on the value succeeded, when the type has one.
    /// </summary>
    /// <value>
    /// <see langword="true"/> or <see langword="false"/> for types with a checksum, such as an
    /// IBAN or a payment card number; <see langword="null"/> when the type has no such check.
    /// </value>
    /// <remarks>
    /// A failed checksum is the single most useful fact for telling a real account number
    /// from a sequence of digits that merely looks like one.
    /// </remarks>
    public bool? ChecksumValidated { get; }

    /// <summary>
    /// Gets the raw score the detector produced, when it produced one.
    /// </summary>
    /// <remarks>
    /// This is the detector's own number, kept as it was reported. The
    /// <see cref="DetectionConfidence"/> on the entity is what the policy reads.
    /// </remarks>
    public double? Score { get; }

    /// <summary>
    /// Gets the number of characters the match covered.
    /// </summary>
    public int MatchLength { get; }

    /// <summary>
    /// Creates evidence for a match produced by a named rule.
    /// </summary>
    /// <param name="patternName">The name of the rule that fired.</param>
    /// <param name="matchLength">The number of characters the match covered.</param>
    /// <returns>The evidence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="patternName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The pattern name is empty, too long, or has a control character.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="matchLength"/> is negative.</exception>
    public static DetectionEvidence FromPattern(string patternName, int matchLength) =>
        Create(patternName, checksumValidated: null, score: null, matchLength);

    /// <summary>
    /// Creates evidence for a match produced by a named rule with a structural check.
    /// </summary>
    /// <param name="patternName">The name of the rule that fired.</param>
    /// <param name="checksumValidated">Whether the structural check succeeded.</param>
    /// <param name="matchLength">The number of characters the match covered.</param>
    /// <returns>The evidence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="patternName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The pattern name is empty, too long, or has a control character.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="matchLength"/> is negative.</exception>
    public static DetectionEvidence FromPattern(string patternName, bool checksumValidated, int matchLength) =>
        Create(patternName, checksumValidated, score: null, matchLength);

    /// <summary>
    /// Creates evidence for a match produced by a model.
    /// </summary>
    /// <param name="patternName">The name of the model or of the label it assigned.</param>
    /// <param name="score">The score the model produced.</param>
    /// <param name="matchLength">The number of characters the match covered.</param>
    /// <returns>The evidence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="patternName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The pattern name is empty, too long, or has a control character.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="matchLength"/> is negative, or <paramref name="score"/> is not a number.
    /// </exception>
    public static DetectionEvidence FromModel(string patternName, double score, int matchLength) =>
        Create(patternName, checksumValidated: null, score, matchLength);

    /// <summary>
    /// Creates evidence.
    /// </summary>
    /// <param name="patternName">The name of the rule or model that fired, or <see langword="null"/>.</param>
    /// <param name="checksumValidated">Whether a structural check succeeded, or <see langword="null"/>.</param>
    /// <param name="score">The raw score, or <see langword="null"/>.</param>
    /// <param name="matchLength">The number of characters the match covered.</param>
    /// <returns>The evidence.</returns>
    /// <exception cref="ArgumentException">The pattern name is empty, too long, or has a control character.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="matchLength"/> is negative, or <paramref name="score"/> is not a number.
    /// </exception>
    public static DetectionEvidence Create(
        string? patternName,
        bool? checksumValidated,
        double? score,
        int matchLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(matchLength);

        if (score is { } value && double.IsNaN(value))
        {
            throw new ArgumentOutOfRangeException(nameof(score), "A score must be a number.");
        }

        return new DetectionEvidence(
            Text.Allow(patternName, MaxPatternNameLength, nameof(patternName)),
            checksumValidated,
            score,
            matchLength);
    }

    /// <summary>
    /// Returns a description of the evidence.
    /// </summary>
    /// <returns>A diagnostic string that is safe to log in full.</returns>
    public override string ToString()
    {
        var name = PatternName ?? "unnamed";
        var checksum = ChecksumValidated is { } validated
            ? validated ? ", checksum ok" : ", checksum failed"
            : string.Empty;
        var score = Score is { } value
            ? string.Concat(", score ", value.ToString("0.###", CultureInfo.InvariantCulture))
            : string.Empty;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{name} ({MatchLength} characters{checksum}{score})");
    }
}
