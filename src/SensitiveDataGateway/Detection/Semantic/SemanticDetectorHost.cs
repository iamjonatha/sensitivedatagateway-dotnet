using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Detection.Semantic;

/// <summary>
/// Runs an <see cref="ISemanticDetector"/> as an ordinary detector, and checks what it says.
/// </summary>
/// <remarks>
/// <para>
/// This is the only way a semantic detector reaches the pipeline, and it is never registered
/// automatically. The gateway ships no model, so an application that does not construct one
/// of these has no semantic detection at all, and a name in prose keeps going unnoticed
/// rather than quietly being sent somewhere to be read.
/// </para>
/// <para>
/// <see cref="Create(ISemanticDetector)"/> refuses a detector that declares itself
/// <see cref="TrustBoundaryLocality.Remote"/>. Accepting it would mean transmitting raw,
/// un-sanitized content to a third party in order to decide whether content may be
/// transmitted to a third party. An application that has decided to do that anyway calls
/// <see cref="CreateOutsideTheTrustBoundary(ISemanticDetector)"/>, whose name is the point:
/// the decision is visible in the diff that introduced it, and it is not reachable by editing
/// a configuration file.
/// </para>
/// <para>
/// Nothing here imposes a time budget or a candidate bound, because the detection stage
/// already does both for every detector, and a failure there is recorded as a gap in the scan
/// rather than as a clean result. A gap blocks.
/// </para>
/// <para>
/// What this does add is a check on the answer. A model is asked a question and returns a
/// structure; the structure is not trusted to be well formed, because a finding that points
/// outside the text it claims to describe would make the anonymizer transform something else.
/// </para>
/// </remarks>
public sealed class SemanticDetectorHost : ISensitiveDataDetector
{
    private const string IdentifierPrefix = "semantic/";
    private const string UnknownSegment = "it points at a segment the document does not have.";
    private const string OutsideTheSegment = "it points outside the segment it names.";
    private const string UnrequestedType = "it reports an entity type the detector was not asked about.";
    private const string ForeignDetector = "it is attributed to a different detector.";

    private readonly ISemanticDetector _detector;

    private SemanticDetectorHost(
        ISemanticDetector detector,
        DetectorVersion version,
        IReadOnlyList<EntityType> entityTypes)
    {
        _detector = detector;
        Version = version;
        EntityTypes = entityTypes;
    }

    /// <summary>
    /// Gets the entity types a semantic detector is asked about when none are given.
    /// </summary>
    /// <remarks>
    /// These are the types a pattern cannot describe. An address, a person's name or a
    /// sentence that is a medical fact has no checksum and no shape, which is the entire
    /// reason this stage exists. Types a deterministic detector finds exactly are left to it,
    /// because a model is the less reliable of the two at that job.
    /// </remarks>
    public static IReadOnlyList<EntityType> DefaultEntityTypes { get; } =
    [
        EntityType.Person,
        EntityType.Address,
        EntityType.DateOfBirth,
        EntityType.Health,
        EntityType.Legal,
        EntityType.Hr,
        EntityType.FinancialContext,
        EntityType.TradeSecret,
    ];

    /// <inheritdoc />
    /// <remarks>
    /// The identifier carries where the model runs, so an audit record of a document a
    /// semantic detector examined says whether anything left the trust boundary to read it.
    /// </remarks>
    public DetectorVersion Version { get; }

    /// <summary>
    /// Gets where the model behind this detector runs.
    /// </summary>
    public TrustBoundaryLocality Locality => _detector.Locality;

    /// <summary>
    /// Gets the entity types this host asks about.
    /// </summary>
    public IReadOnlyList<EntityType> EntityTypes { get; }

    /// <summary>
    /// Hosts a semantic detector that runs inside the trust boundary.
    /// </summary>
    /// <param name="detector">The detector.</param>
    /// <returns>The host, asking about <see cref="DefaultEntityTypes"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="detector"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The detector carries no version, or declares itself
    /// <see cref="TrustBoundaryLocality.Remote"/>.
    /// </exception>
    public static SemanticDetectorHost Create(ISemanticDetector detector) =>
        Create(detector, DefaultEntityTypes);

    /// <summary>
    /// Hosts a semantic detector that runs inside the trust boundary.
    /// </summary>
    /// <param name="detector">The detector.</param>
    /// <param name="entityTypes">The entity types to ask about.</param>
    /// <returns>The host.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The detector carries no version, declares itself
    /// <see cref="TrustBoundaryLocality.Remote"/>, or the type list is empty.
    /// </exception>
    public static SemanticDetectorHost Create(ISemanticDetector detector, IReadOnlyList<EntityType> entityTypes)
    {
        ArgumentNullException.ThrowIfNull(detector);

        if (!detector.Locality.IsInsideTheTrustBoundary())
        {
            throw new ArgumentException(
                "The detector declares that it runs outside the trust boundary, which sends raw content to a third party. Use CreateOutsideTheTrustBoundary if the application has decided to accept that.",
                nameof(detector));
        }

        return Build(detector, entityTypes);
    }

    /// <summary>
    /// Hosts a semantic detector that may run anywhere, including outside the trust boundary.
    /// </summary>
    /// <param name="detector">The detector.</param>
    /// <returns>The host, asking about <see cref="DefaultEntityTypes"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="detector"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The detector carries no version.</exception>
    /// <remarks>
    /// Calling this is a decision, not a configuration. Every document this detector examines
    /// is transmitted, in full and un-sanitized, to whoever runs the model; no guarantee this
    /// library makes survives that, and the only control left is the audit record, which does
    /// say so.
    /// </remarks>
    public static SemanticDetectorHost CreateOutsideTheTrustBoundary(ISemanticDetector detector) =>
        CreateOutsideTheTrustBoundary(detector, DefaultEntityTypes);

    /// <summary>
    /// Hosts a semantic detector that may run anywhere, including outside the trust boundary.
    /// </summary>
    /// <param name="detector">The detector.</param>
    /// <param name="entityTypes">The entity types to ask about.</param>
    /// <returns>The host.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The detector carries no version, or the type list is empty.
    /// </exception>
    /// <remarks>
    /// Calling this is a decision, not a configuration. Every document this detector examines
    /// is transmitted, in full and un-sanitized, to whoever runs the model; no guarantee this
    /// library makes survives that, and the only control left is the audit record, which does
    /// say so.
    /// </remarks>
    public static SemanticDetectorHost CreateOutsideTheTrustBoundary(
        ISemanticDetector detector,
        IReadOnlyList<EntityType> entityTypes)
    {
        ArgumentNullException.ThrowIfNull(detector);

        return Build(detector, entityTypes);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<SensitiveEntity>> DetectAsync(
        ExtractedDocument document,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.Segments.Count == 0)
        {
            return [];
        }

        var context = SemanticDetectionContext.Create(document, Version.DetectorId, EntityTypes);
        var entities = await _detector.ExamineAsync(context, cancellationToken).ConfigureAwait(false)
            ?? throw new DetectionException(
                DetectionErrors.MalformedFinding(Version.DetectorId, "it reported nothing at all, not even an empty result."));

        foreach (var entity in entities)
        {
            Verify(entity, document);
        }

        return entities;
    }

    private static SemanticDetectorHost Build(ISemanticDetector detector, IReadOnlyList<EntityType> entityTypes)
    {
        ArgumentNullException.ThrowIfNull(entityTypes);

        if (detector.Version.IsDefault)
        {
            throw new ArgumentException("The detector carries no version.", nameof(detector));
        }

        if (entityTypes.Count == 0)
        {
            throw new ArgumentException(
                "A semantic detector is asked about at least one entity type.",
                nameof(entityTypes));
        }

        return new SemanticDetectorHost(detector, Identify(detector), [.. entityTypes]);
    }

    private static DetectorVersion Identify(ISemanticDetector detector)
    {
        var version = detector.Version;
        var name = string.Concat(
            IdentifierPrefix,
            detector.Locality.Describe(),
            "/",
            version.DetectorId.Value);

        if (name.Length > DetectorId.MaxLength)
        {
            throw new ArgumentException(
                $"The detector's identifier is too long once the locality is added to it. Keep it under {DetectorId.MaxLength - IdentifierPrefix.Length - 12} characters.",
                nameof(detector));
        }

        return DetectorVersion.Create(DetectorId.Create(name), version.Version);
    }

    private void Verify(SensitiveEntity entity, ExtractedDocument document)
    {
        if (entity.Detector != Version.DetectorId)
        {
            throw Malformed(ForeignDetector);
        }

        if (!EntityTypes.Contains(entity.Type))
        {
            throw Malformed(UnrequestedType);
        }

        var segment = Find(document, entity.Span.SegmentId) ?? throw Malformed(UnknownSegment);

        if (entity.Span.Start + entity.Span.Length > segment.Text.Length)
        {
            throw Malformed(OutsideTheSegment);
        }
    }

    private static TextSegment? Find(ExtractedDocument document, SegmentId segmentId)
    {
        foreach (var segment in document.Segments)
        {
            if (segment.Id == segmentId)
            {
                return segment;
            }
        }

        return null;
    }

    private DetectionException Malformed(string problem) =>
        new(DetectionErrors.MalformedFinding(Version.DetectorId, problem));
}
