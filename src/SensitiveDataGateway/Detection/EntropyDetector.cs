using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// A detector that reports runs of text too dense to be prose.
/// </summary>
/// <remarks>
/// <para>
/// Every pattern detector knows what it is looking for. This one does not: it looks for text
/// that behaves like a key — long, drawn from a wide alphabet, and without the redundancy
/// every language has — and reports it as a candidate credential.
/// </para>
/// <para>
/// It reports below the confidence at which a strict policy transforms silently, which is
/// deliberate. A guess that is treated as a fact is worse than a guess that is reviewed.
/// </para>
/// </remarks>
internal sealed class EntropyDetector : ISensitiveDataDetector
{
    /// <summary>
    /// The confidence a run of dense text is reported with.
    /// </summary>
    /// <remarks>
    /// It sits below the default transformation threshold on purpose: the shipped policies
    /// lower the threshold for credentials specifically, so a dense run is acted on where it
    /// matters and reviewed everywhere else.
    /// </remarks>
    public static readonly DetectionConfidence Confidence = DetectionConfidence.Create(0.75);

    private readonly EntropyDetectorOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="EntropyDetector"/> class.
    /// </summary>
    /// <param name="version">The detector and the version of its thresholds.</param>
    /// <param name="options">What counts as dense enough.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="version"/> is the default value or its detector identifier is too long.
    /// </exception>
    public EntropyDetector(DetectorVersion version, EntropyDetectorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (version.IsDefault)
        {
            throw new ArgumentException("The detector version carries no value.", nameof(version));
        }

        DetectionIds.Require(version.DetectorId, nameof(version));

        Version = version;
        _options = options;
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

        var entities = new List<SensitiveEntity>();

        foreach (var segment in document.Segments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ScanSegment(segment, entities);
        }

        return new ValueTask<IReadOnlyList<SensitiveEntity>>(entities);
    }

    /// <summary>
    /// Determines whether a character can be part of a key.
    /// </summary>
    /// <param name="character">The character to test.</param>
    /// <returns><see langword="true"/> when it is part of a run.</returns>
    /// <remarks>
    /// The set is the union of the alphabets secrets are encoded in: base64, base64url, hex
    /// and the separators vendors put between a prefix and a body.
    /// </remarks>
    private static bool IsRunCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '+' or '/' or '=' or '_' or '-';

    /// <summary>
    /// Determines whether a run is varied enough to be worth measuring.
    /// </summary>
    /// <param name="run">The run to test.</param>
    /// <returns><see langword="true"/> when it draws on at least two character classes.</returns>
    /// <remarks>
    /// A long number is an order reference, and a long lowercase word is a word. Requiring two
    /// classes removes both without costing a single real key, because no encoding of random
    /// bytes stays inside one class for twenty characters.
    /// </remarks>
    private static bool IsMixed(ReadOnlySpan<char> run)
    {
        var lower = false;
        var upper = false;
        var digit = false;

        foreach (var character in run)
        {
            lower |= char.IsAsciiLetterLower(character);
            upper |= char.IsAsciiLetterUpper(character);
            digit |= char.IsAsciiDigit(character);
        }

        return (lower ? 1 : 0) + (upper ? 1 : 0) + (digit ? 1 : 0) >= 2;
    }

    private void ScanSegment(TextSegment segment, List<SensitiveEntity> entities)
    {
        var text = segment.Text.AsSpan();
        var start = 0;

        while (start < text.Length)
        {
            if (!IsRunCharacter(text[start]))
            {
                start++;
                continue;
            }

            var end = start;

            while (end < text.Length && IsRunCharacter(text[end]))
            {
                end++;
            }

            Consider(segment, text[start..end], start, entities);
            start = end;
        }
    }

    private void Consider(
        TextSegment segment,
        ReadOnlySpan<char> run,
        int start,
        List<SensitiveEntity> entities)
    {
        if (run.Length < _options.MinimumLength || !IsMixed(run))
        {
            return;
        }

        var entropy = ShannonEntropy.Measure(run);

        if (entropy < _options.MinimumBitsPerCharacter)
        {
            return;
        }

        entities.Add(SensitiveEntity.Create(
            DetectionIds.For(Version.DetectorId, entities.Count),
            EntityType.ApiKey,
            SourceSpan.Create(segment.Id, start, run.Length),
            Confidence,
            Version.DetectorId,
            SensitivityLevel.Critical,
            DetectionEvidence.FromModel("shannon-entropy", entropy, run.Length)));
    }
}
