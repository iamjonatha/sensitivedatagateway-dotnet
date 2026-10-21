using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace SensitiveDataGateway.Documents.Recognition;

/// <summary>
/// Reads a part through an <see cref="ITextRecognizer"/>, as an ordinary extractor.
/// </summary>
/// <remarks>
/// <para>
/// This is the only way a recognizer reaches the pipeline, and it is never registered
/// automatically. The gateway ships no recognizer, so a host that does not construct one of
/// these has no recognition path at all, and a scanned page keeps failing as an unreadable
/// construct rather than quietly being sent somewhere to be read.
/// </para>
/// <para>
/// <see cref="Create(ITextRecognizer)"/> refuses an engine that declares itself
/// <see cref="RecognizerLocality.Remote"/>. Accepting it would mean transmitting raw,
/// un-sanitized content to a third party in order to decide whether content may be
/// transmitted to a third party. An application that has decided to do that anyway calls
/// <see cref="CreateOutsideTheTrustBoundary(ITextRecognizer)"/>, whose name is the point: the
/// decision is visible in the diff that introduced it, and it is not reachable by editing a
/// configuration file.
/// </para>
/// <para>
/// The extractor's recorded identity carries the recognizer's locality, so an audit record of
/// a bundle read this way says where the content went without anyone having to correlate it
/// with a registration.
/// </para>
/// </remarks>
public sealed class RecognizingExtractor : IDocumentExtractor
{
    private const string IdentifierPrefix = "recognizer/";

    private readonly ITextRecognizer _recognizer;
    private readonly RecognitionOptions _options;

    private RecognizingExtractor(ITextRecognizer recognizer, RecognitionOptions options, ExtractorVersion version)
    {
        _recognizer = recognizer;
        _options = options;
        Version = version;
    }

    /// <inheritdoc />
    public ExtractorVersion Version { get; }

    /// <summary>
    /// Gets where the recognizer behind this extractor runs.
    /// </summary>
    public RecognizerLocality Locality => _recognizer.Version.Locality;

    /// <summary>
    /// Creates an extractor over a recognizer that runs inside the trust boundary.
    /// </summary>
    /// <param name="recognizer">The recognizer.</param>
    /// <returns>The extractor, with the default bounds.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="recognizer"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The recognizer carries no version, declares itself
    /// <see cref="RecognizerLocality.Remote"/>, or is named too long to record.
    /// </exception>
    public static RecognizingExtractor Create(ITextRecognizer recognizer) =>
        Create(recognizer, RecognitionOptions.Default);

    /// <summary>
    /// Creates an extractor over a recognizer that runs inside the trust boundary.
    /// </summary>
    /// <param name="recognizer">The recognizer.</param>
    /// <param name="options">The bounds applied to what it returns.</param>
    /// <returns>The extractor.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The recognizer carries no version, declares itself
    /// <see cref="RecognizerLocality.Remote"/>, or is named too long to record.
    /// </exception>
    public static RecognizingExtractor Create(ITextRecognizer recognizer, RecognitionOptions options)
    {
        ArgumentNullException.ThrowIfNull(recognizer);

        var version = Describe(recognizer);

        if (!version.IsInsideTheTrustBoundary)
        {
            throw new ArgumentException(
                "The recognizer declares that it runs outside the trust boundary, which sends raw content to a third party. Use CreateOutsideTheTrustBoundary if the application has decided to accept that.",
                nameof(recognizer));
        }

        return Build(recognizer, options, version);
    }

    /// <summary>
    /// Creates an extractor over a recognizer that may run anywhere, including outside the
    /// trust boundary.
    /// </summary>
    /// <param name="recognizer">The recognizer.</param>
    /// <returns>The extractor, with the default bounds.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="recognizer"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The recognizer carries no version, or is named too long to record.
    /// </exception>
    /// <remarks>
    /// Calling this is a decision, not a configuration. The parts this extractor reads are
    /// transmitted, in full and un-sanitized, to whoever runs the engine; no guarantee this
    /// library makes survives that, and the only control left is the audit record, which does
    /// say so.
    /// </remarks>
    public static RecognizingExtractor CreateOutsideTheTrustBoundary(ITextRecognizer recognizer) =>
        CreateOutsideTheTrustBoundary(recognizer, RecognitionOptions.Default);

    /// <summary>
    /// Creates an extractor over a recognizer that may run anywhere, including outside the
    /// trust boundary.
    /// </summary>
    /// <param name="recognizer">The recognizer.</param>
    /// <param name="options">The bounds applied to what it returns.</param>
    /// <returns>The extractor.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The recognizer carries no version, or is named too long to record.
    /// </exception>
    /// <remarks>
    /// Calling this is a decision, not a configuration. The parts this extractor reads are
    /// transmitted, in full and un-sanitized, to whoever runs the engine; no guarantee this
    /// library makes survives that, and the only control left is the audit record, which does
    /// say so.
    /// </remarks>
    public static RecognizingExtractor CreateOutsideTheTrustBoundary(
        ITextRecognizer recognizer,
        RecognitionOptions options)
    {
        ArgumentNullException.ThrowIfNull(recognizer);

        return Build(recognizer, options, Describe(recognizer));
    }

    /// <inheritdoc />
    public bool CanExtract(string mediaType)
    {
        ArgumentNullException.ThrowIfNull(mediaType);

        return _recognizer.CanRecognize(mediaType);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<TextSegment>> ExtractAsync(
        DocumentPart part,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(part);

        var recognized = await _recognizer.RecognizeAsync(part, cancellationToken).ConfigureAwait(false)
            ?? throw new ExtractionException(RecognitionErrors.NoResult(part.Id));

        Validate(part.Id, recognized);

        return ToSegments(part.Id, recognized);
    }

    private static RecognizerVersion Describe(ITextRecognizer recognizer)
    {
        var version = recognizer.Version;

        return version.IsDefault
            ? throw new ArgumentException("The recognizer carries no version.", nameof(recognizer))
            : version;
    }

    private static RecognizingExtractor Build(
        ITextRecognizer recognizer,
        RecognitionOptions options,
        RecognizerVersion version)
    {
        ArgumentNullException.ThrowIfNull(options);

        var name = string.Concat(IdentifierPrefix, Abbreviate(version.Locality), "/", version.RecognizerId.Value);

        if (name.Length > ExtractorId.MaxLength)
        {
            throw new ArgumentException(
                $"The recognizer identifier is too long to record: the extractor identifier derived from it would exceed {ExtractorId.MaxLength.ToString(CultureInfo.InvariantCulture)} characters.",
                nameof(recognizer));
        }

        return new RecognizingExtractor(
            recognizer,
            options,
            ExtractorVersion.Create(ExtractorId.Create(name), version.Version));
    }

    private static string Abbreviate(RecognizerLocality locality) => locality switch
    {
        RecognizerLocality.InProcess => "in-process",
        RecognizerLocality.SelfHosted => "self-hosted",
        _ => "remote",
    };

    private void Validate(PartId partId, RecognizedText recognized)
    {
        if (recognized.IsEmpty)
        {
            throw new ExtractionException(RecognitionErrors.NothingRead(partId));
        }

        if (recognized.Lines.Count > _options.MaxLines)
        {
            throw new ExtractionException(RecognitionErrors.TooManyLines(partId, _options.MaxLines));
        }

        if (recognized.LowestConfidence < _options.MinimumConfidence)
        {
            throw new ExtractionException(
                RecognitionErrors.TooUncertain(partId, _options.MinimumConfidence));
        }
    }

    private static List<TextSegment> ToSegments(PartId partId, RecognizedText recognized)
    {
        var segments = new List<TextSegment>(recognized.Lines.Count);

        foreach (var line in recognized.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.Text))
            {
                continue;
            }

            segments.Add(
                TextSegment.Create(
                    SegmentIds.Paragraph(partId, line.Ordinal),
                    partId,
                    line.Text,
                    SegmentOrigin.At(SegmentOriginKind.Paragraph, line.Ordinal),
                    segments.Count));
        }

        return segments.Count > 0
            ? segments
            : throw new ExtractionException(RecognitionErrors.NothingRead(partId));
    }
}
