using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SensitiveDataGateway.Documents;

/// <summary>
/// Reads every part of a bundle with the registered extractors.
/// </summary>
/// <remarks>
/// <para>
/// The host owns three things the extractors do not: which extractor sees a part, the bounds
/// the read is allowed to cross, and the metadata segments. File names, subjects and sender
/// headers are content — they are where a name usually appears first — so the host emits them
/// as segments and the scan treats them like any other text.
/// </para>
/// <para>
/// A part no extractor claims is recorded as a failure. It is never skipped quietly: a
/// skipped part is one that was not scanned, and an unscanned part that reaches a provider is
/// the leak this library exists to prevent.
/// </para>
/// <para>
/// The host does no network I/O and writes nothing to a log. It is given content and it
/// returns text.
/// </para>
/// </remarks>
public sealed class ExtractionPipeline
{
    private const string BundleMetadataPrefix = "bundle.";
    private const string PropertyMetadataPrefix = "prop.";
    private const string FileNameMetadata = "file-name";
    private const string OriginMetadata = "origin";

    private readonly List<IDocumentExtractor> _extractors;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractionPipeline"/> class.
    /// </summary>
    /// <param name="extractors">
    /// The extractors, in the order they are offered a part. The first one whose
    /// <see cref="IDocumentExtractor.CanExtract"/> returns <see langword="true"/> reads it.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="extractors"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="extractors"/> is empty or contains <see langword="null"/>.
    /// </exception>
    public ExtractionPipeline(IEnumerable<IDocumentExtractor> extractors)
    {
        ArgumentNullException.ThrowIfNull(extractors);

        _extractors = [.. extractors];

        if (_extractors.Count == 0)
        {
            throw new ArgumentException("At least one extractor is required.", nameof(extractors));
        }

        if (_extractors.Any(extractor => extractor is null))
        {
            throw new ArgumentException("No extractor may be null.", nameof(extractors));
        }
    }

    /// <summary>
    /// Reads every part of a bundle.
    /// </summary>
    /// <param name="bundle">The bundle.</param>
    /// <param name="limits">The bounds the read refuses to cross.</param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>The text, the parts that could not be read, and the extractors that ran.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="bundle"/> or <paramref name="limits"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ProcessingLimitExceededException">The bundle crosses a bound in <paramref name="limits"/>.</exception>
    /// <exception cref="ExtractionException">The bundle produced neither a segment nor a failure.</exception>
    public async Task<ExtractedDocument> ExtractAsync(
        DocumentBundle bundle,
        ProcessingLimits limits,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(limits);

        var run = new ExtractionRun(limits);

        foreach (var part in bundle.Parts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ExtractPartAsync(run, bundle, part, cancellationToken).ConfigureAwait(false);
        }

        return run.ToDocument(bundle.Id);
    }

    /// <summary>
    /// Returns the media type without its parameters.
    /// </summary>
    /// <param name="mediaType">The media type a part declares, already lowercased.</param>
    /// <returns>The type and subtype, without a charset or any other parameter.</returns>
    private static string Essence(string mediaType)
    {
        var separator = mediaType.IndexOf(';', StringComparison.Ordinal);

        return separator < 0 ? mediaType.Trim() : mediaType[..separator].Trim();
    }

    private static void AddMetadata(ExtractionRun run, DocumentPart part, DocumentMetadata metadata, string prefix)
    {
        if (metadata.FileName is { } fileName)
        {
            run.AddMetadata(part, string.Concat(prefix, FileNameMetadata), fileName);
        }

        if (metadata.Origin is { } origin)
        {
            run.AddMetadata(part, string.Concat(prefix, OriginMetadata), origin);
        }

        foreach (var property in metadata.Properties.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            run.AddMetadata(part, string.Concat(prefix, PropertyMetadataPrefix, property.Key), property.Value);
        }
    }

    private async Task ExtractPartAsync(
        ExtractionRun run,
        DocumentBundle bundle,
        DocumentPart part,
        CancellationToken cancellationToken)
    {
        run.BeginPart(part);

        if (ReferenceEquals(part, bundle.Primary))
        {
            AddMetadata(run, part, bundle.Metadata, BundleMetadataPrefix);
        }

        AddMetadata(run, part, part.Metadata, prefix: string.Empty);

        if (!run.TryAdmit(part))
        {
            return;
        }

        var extractor = Select(Essence(part.MediaType));

        if (extractor is null)
        {
            run.Fail(part, ExtractionFailureReason.UnsupportedMediaType);
            return;
        }

        try
        {
            var segments = await extractor.ExtractAsync(part, cancellationToken).ConfigureAwait(false);
            run.AddExtracted(extractor.Version, segments ?? []);
        }
        catch (ExtractionException exception)
        {
            run.Fail(part, ExtractionErrors.ReasonFor(exception.Error));
        }
    }

    private IDocumentExtractor? Select(string mediaType)
    {
        foreach (var extractor in _extractors)
        {
            if (extractor.CanExtract(mediaType))
            {
                return extractor;
            }
        }

        return null;
    }
}
