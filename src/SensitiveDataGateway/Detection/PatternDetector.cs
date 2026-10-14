using System;
using System.Text.RegularExpressions;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// Builds a detector from an expression a deployment supplies.
/// </summary>
/// <remarks>
/// <para>
/// The built-in detectors cover the shapes that look the same everywhere. An order reference,
/// a customer code, a case number or an employee identifier look the way one organization
/// decided they would look, and only that organization can write the pattern.
/// </para>
/// <para>
/// A supplied expression must carry a match timeout. The gateway will not run an expression
/// that can be made to take forever, because a scan that never finishes is a document that
/// never gets refused.
/// </para>
/// <para>
/// When the expression declares a group named <c>value</c>, the reported span covers that
/// group instead of the whole match. That is how a pattern can anchor on a label without the
/// anonymizer rewriting the label too.
/// </para>
/// </remarks>
public static class PatternDetector
{
    /// <summary>
    /// The name of the group that narrows the reported span.
    /// </summary>
    public const string ValueGroupName = PatternRule.ValueGroup;

    /// <summary>
    /// The greatest number of characters the identifier of a detector may contain.
    /// </summary>
    public const int MaxDetectorIdLength = DetectionIds.MaxDetectorIdLength;

    /// <summary>
    /// Creates a detector that reports what an expression matches.
    /// </summary>
    /// <param name="version">The detector and the version of its pattern.</param>
    /// <param name="patternName">The name to record as evidence. It must not quote the text.</param>
    /// <param name="type">The type to report.</param>
    /// <param name="sensitivity">How damaging disclosure of this type would be.</param>
    /// <param name="confidence">The confidence to report for a match.</param>
    /// <param name="pattern">The expression to look for.</param>
    /// <returns>The detector.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="patternName"/> or <paramref name="pattern"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="version"/> is the default value, its detector identifier is empty or
    /// longer than <see cref="MaxDetectorIdLength"/>, <paramref name="patternName"/> is empty
    /// or too long, <paramref name="type"/> is the default value,
    /// <paramref name="sensitivity"/> is not a defined value, or <paramref name="pattern"/>
    /// has no match timeout.
    /// </exception>
    public static ISensitiveDataDetector Create(
        DetectorVersion version,
        string patternName,
        EntityType type,
        SensitivityLevel sensitivity,
        DetectionConfidence confidence,
        Regex pattern) =>
        new PatternScanDetector(
            version,
            PatternRule.Create(pattern, patternName, type, sensitivity, confidence));
}
