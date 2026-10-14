using System;
using System.Text.RegularExpressions;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// One shape a pattern detector looks for, and what finding it means.
/// </summary>
/// <remarks>
/// <para>
/// A rule pairs a regular expression with the classification to report when it matches, and
/// optionally with the structural check that decides whether the match is a value at all. The
/// check is not a confidence adjustment: a candidate that fails it is discarded, because
/// sixteen digits that fail Luhn are not a card number with low confidence, they are not a
/// card number.
/// </para>
/// <para>
/// When the expression declares a group named <c>value</c>, the reported span covers that
/// group rather than the whole match. This is how a rule can anchor on surrounding text — an
/// assignment, a label, a prefix — without the anonymizer having to rewrite the anchor too.
/// </para>
/// </remarks>
internal sealed class PatternRule
{
    /// <summary>
    /// The name of the group that narrows the reported span, when the expression declares it.
    /// </summary>
    public const string ValueGroup = "value";

    private PatternRule(
        Regex pattern,
        string patternName,
        EntityType type,
        SensitivityLevel sensitivity,
        DetectionConfidence confidence,
        Func<string, bool>? structure)
    {
        Pattern = pattern;
        PatternName = patternName;
        Type = type;
        Sensitivity = sensitivity;
        Confidence = confidence;
        Structure = structure;
    }

    /// <summary>
    /// Gets the expression to look for.
    /// </summary>
    public Regex Pattern { get; }

    /// <summary>
    /// Gets the name recorded in the evidence, which names the shape and never the match.
    /// </summary>
    public string PatternName { get; }

    /// <summary>
    /// Gets the type to report.
    /// </summary>
    public EntityType Type { get; }

    /// <summary>
    /// Gets how much damage disclosing a value of this type would do.
    /// </summary>
    public SensitivityLevel Sensitivity { get; }

    /// <summary>
    /// Gets the confidence to report for a match that passes every check.
    /// </summary>
    public DetectionConfidence Confidence { get; }

    /// <summary>
    /// Gets the structural check a match must pass, or <see langword="null"/> when the shape
    /// carries no check digit.
    /// </summary>
    public Func<string, bool>? Structure { get; }

    /// <summary>
    /// Gets a value indicating whether a reported match was verified by a check digit.
    /// </summary>
    public bool ChecksumValidated => Structure is not null;

    /// <summary>
    /// Creates a rule for a shape that carries no check digit.
    /// </summary>
    /// <param name="pattern">The expression to look for.</param>
    /// <param name="patternName">The name to record in the evidence.</param>
    /// <param name="type">The type to report.</param>
    /// <param name="sensitivity">How damaging disclosure of this type would be.</param>
    /// <param name="confidence">The confidence to report.</param>
    /// <returns>The rule.</returns>
    public static PatternRule Create(
        Regex pattern,
        string patternName,
        EntityType type,
        SensitivityLevel sensitivity,
        DetectionConfidence confidence) =>
        Create(pattern, patternName, type, sensitivity, confidence, structure: null);

    /// <summary>
    /// Creates a rule.
    /// </summary>
    /// <param name="pattern">The expression to look for.</param>
    /// <param name="patternName">The name to record in the evidence.</param>
    /// <param name="type">The type to report.</param>
    /// <param name="sensitivity">How damaging disclosure of this type would be.</param>
    /// <param name="confidence">The confidence to report.</param>
    /// <param name="structure">
    /// The check a match must pass to be reported, or <see langword="null"/>.
    /// </param>
    /// <returns>The rule.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="pattern"/> or <paramref name="patternName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="patternName"/> is empty or too long, <paramref name="type"/> is the
    /// default value, <paramref name="sensitivity"/> is undefined, or the expression carries
    /// no match timeout.
    /// </exception>
    public static PatternRule Create(
        Regex pattern,
        string patternName,
        EntityType type,
        SensitivityLevel sensitivity,
        DetectionConfidence confidence,
        Func<string, bool>? structure)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        if (pattern.MatchTimeout == Regex.InfiniteMatchTimeout)
        {
            throw new ArgumentException(
                "A detection pattern must carry a match timeout, so that a pathological input "
                + "fails the scan instead of hanging it.",
                nameof(pattern));
        }

        if (type.IsDefault)
        {
            throw new ArgumentException("The entity type carries no value.", nameof(type));
        }

        if (!Enum.IsDefined(sensitivity))
        {
            throw new ArgumentException("The sensitivity level is not a defined value.", nameof(sensitivity));
        }

        return new PatternRule(
            pattern,
            Text.Require(patternName, DetectionEvidence.MaxPatternNameLength, nameof(patternName)),
            type,
            sensitivity,
            confidence,
            structure);
    }
}
