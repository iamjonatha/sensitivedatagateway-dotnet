using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// A detector that reports the candidates a set of patterns matches.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole of deterministic detection: run every pattern over every segment, keep
/// the matches that pass their structural check, and report where they were. It reads text
/// and never keeps any, it does no input or output, and it reaches no network.
/// </para>
/// <para>
/// A pattern that exceeds its match timeout is not a pattern that found nothing. It raises
/// <see cref="DetectionException"/>, which the pipeline records as a gap, so that the document
/// is refused rather than treated as scanned.
/// </para>
/// </remarks>
internal sealed class PatternScanDetector : ISensitiveDataDetector
{
    private readonly PatternRule[] _rules;

    /// <summary>
    /// Initializes a new instance of the <see cref="PatternScanDetector"/> class.
    /// </summary>
    /// <param name="version">The detector and the version of its rules.</param>
    /// <param name="rules">The patterns to look for.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rules"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="version"/> is the default value, its detector identifier is too long,
    /// or <paramref name="rules"/> is empty or contains a <see langword="null"/>.
    /// </exception>
    public PatternScanDetector(DetectorVersion version, params PatternRule[] rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (version.IsDefault)
        {
            throw new ArgumentException("The detector version carries no value.", nameof(version));
        }

        DetectionIds.Require(version.DetectorId, nameof(version));

        if (rules.Length == 0)
        {
            throw new ArgumentException("A detector with no patterns would always report nothing.", nameof(rules));
        }

        foreach (var rule in rules)
        {
            if (rule is null)
            {
                throw new ArgumentException("A pattern rule is null.", nameof(rules));
            }
        }

        Version = version;
        _rules = (PatternRule[])rules.Clone();
    }

    /// <inheritdoc />
    public DetectorVersion Version { get; }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<SensitiveEntity>> DetectAsync(
        ExtractedDocument document,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return new ValueTask<IReadOnlyList<SensitiveEntity>>(Scan(document, cancellationToken));
        }
        catch (RegexMatchTimeoutException exception)
        {
            throw new DetectionException(DetectionErrors.TimedOut(Version.DetectorId), exception);
        }
    }

    private List<SensitiveEntity> Scan(ExtractedDocument document, CancellationToken cancellationToken)
    {
        var entities = new List<SensitiveEntity>();
        var reported = new HashSet<(int Start, int Length)>();

        foreach (var segment in document.Segments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            reported.Clear();

            foreach (var rule in _rules)
            {
                ScanSegment(segment, rule, entities, reported);
            }
        }

        return entities;
    }

    /// <summary>
    /// Reports every match of one pattern in one segment, except the ones an earlier pattern
    /// already claimed character for character.
    /// </summary>
    /// <param name="segment">The text to scan.</param>
    /// <param name="rule">The pattern to look for.</param>
    /// <param name="entities">The candidates found so far.</param>
    /// <param name="reported">The spans already claimed in this segment.</param>
    /// <remarks>
    /// Patterns within one detector overlap on purpose — a fifteen-digit card matches both the
    /// grouped form and the plain one — and reporting the same characters twice would make the
    /// anonymizer rewrite them twice. The first pattern to claim a span keeps it, which is why
    /// rules are declared from the most specific to the most general. A partial overlap is
    /// kept: resolving that is the pipeline's job, and it needs both claims to do it.
    /// </remarks>
    private void ScanSegment(
        TextSegment segment,
        PatternRule rule,
        List<SensitiveEntity> entities,
        HashSet<(int Start, int Length)> reported)
    {
        foreach (Match match in rule.Pattern.Matches(segment.Text))
        {
            var group = match.Groups[PatternRule.ValueGroup];
            Capture capture = group.Success ? group : match;

            if (rule.Structure is { } structure && !structure(capture.Value))
            {
                continue;
            }

            if (!reported.Add((capture.Index, capture.Length)))
            {
                continue;
            }

            entities.Add(Report(segment, rule, capture.Index, capture.Length, entities.Count));
        }
    }

    private SensitiveEntity Report(
        TextSegment segment,
        PatternRule rule,
        int start,
        int length,
        int ordinal) =>
        SensitiveEntity.Create(
            DetectionIds.For(Version.DetectorId, ordinal),
            rule.Type,
            SourceSpan.Create(segment.Id, start, length),
            rule.Confidence,
            Version.DetectorId,
            rule.Sensitivity,
            DetectionEvidence.FromPattern(rule.PatternName, rule.ChecksumValidated, length));
}
