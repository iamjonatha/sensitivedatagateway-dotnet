using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Detection.Semantic;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Detection.Presidio;

/// <summary>
/// Finds entities by asking a Presidio analyzer.
/// </summary>
/// <remarks>
/// <para>
/// Presidio is a recognizer, not a language model: it is told a text and answers with spans
/// and scores. Nothing in a document can change what it is asked, so the prompt discipline
/// <see cref="SemanticDetectionContext"/> enforces costs nothing here and is kept anyway —
/// the entity types come from the context, and the text comes from its segments.
/// </para>
/// <para>
/// One request carries one segment, because Presidio reports offsets into the text it was
/// given and the gateway needs offsets into a segment. A document with many segments is many
/// requests; the detection stage's time budget covers all of them together.
/// </para>
/// <para>
/// The detector sends raw, un-extracted-from document text over the network. Where that text
/// goes is the endpoint's business and the deployment's responsibility; see
/// <see cref="PresidioOptions"/> and <c>docs/guides/semantic-detection.md</c>.
/// </para>
/// </remarks>
public sealed class PresidioDetector : ISemanticDetector
{
    private const string AnalyzePath = "analyze";

    private readonly HttpClient _client;
    private readonly PresidioOptions _options;

    private PresidioDetector(HttpClient client, PresidioOptions options, DetectorVersion version)
    {
        _client = client;
        _options = options;
        Version = version;
    }

    /// <inheritdoc />
    public DetectorVersion Version { get; }

    /// <inheritdoc />
    public TrustBoundaryLocality Locality => _options.Locality;

    /// <summary>
    /// Builds a detector that talks to the analyzer the options describe.
    /// </summary>
    /// <param name="client">The client to send requests with.</param>
    /// <param name="options">Where the analyzer is and what to keep.</param>
    /// <returns>The detector.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <remarks>
    /// The client's lifetime belongs to the caller, so that a deployment can give this one a
    /// handler with its own certificate pinning, proxy or timeout without the library
    /// deciding any of that on its behalf.
    /// </remarks>
    public static PresidioDetector Create(HttpClient client, PresidioOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        return new PresidioDetector(
            client,
            options,
            DetectorVersion.Create(DetectorId.Create("presidio"), "2.0"));
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<SensitiveEntity>> ExamineAsync(
        SemanticDetectionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var entities = new List<SensitiveEntity>();

        foreach (var segment in context.Segments)
        {
            if (segment.Text.Length == 0)
            {
                continue;
            }

            await AnalyzeAsync(context, segment, entities, cancellationToken).ConfigureAwait(false);
        }

        return entities;
    }

    private async Task AnalyzeAsync(
        SemanticDetectionContext context,
        TextSegment segment,
        List<SensitiveEntity> entities,
        CancellationToken cancellationToken)
    {
        var request = new PresidioAnalyzeRequest(segment.Text, _options.Language, _options.MinimumScore);

        using var response = await _client
            .PostAsJsonAsync(
                new Uri(_options.Endpoint, AnalyzePath),
                request,
                PresidioJsonContext.Default.PresidioAnalyzeRequest,
                cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var results = await response.Content
            .ReadFromJsonAsync(PresidioJsonContext.Default.PresidioAnalyzeResultArray, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new HttpRequestException("The Presidio analyzer returned no body.");

        foreach (var result in results)
        {
            Collect(context, segment, result, entities);
        }
    }

    private void Collect(
        SemanticDetectionContext context,
        TextSegment segment,
        PresidioAnalyzeResult result,
        List<SensitiveEntity> entities)
    {
        if (result.Score < _options.MinimumScore || !Fits(result, segment))
        {
            return;
        }

        var length = result.End - result.Start;

        entities.Add(SensitiveEntity.Create(
            EntityId.New(),
            TypeOf(result.EntityType),
            SourceSpan.Create(segment.Id, result.Start, length),
            DetectionConfidence.Create(Math.Clamp(result.Score, 0.0, 1.0)),
            context.DetectorId,
            SensitivityLevel.High,
            DetectionEvidence.FromModel(result.EntityType, result.Score, length)));
    }

    private static bool Fits(PresidioAnalyzeResult result, TextSegment segment) =>
        result.Start >= 0 && result.End > result.Start && result.End <= segment.Text.Length;

    private EntityType TypeOf(string name) =>
        _options.EntityTypes.TryGetValue(name, out var type) ? type : EntityType.Create(name.ToLowerInvariant());
}
