using System;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// A candidate: something a detector believes is sensitive, and where it is.
/// </summary>
/// <remarks>
/// <para>
/// An entity is a claim, not a verdict. A detector emits candidates and stops; deciding what
/// happens to one belongs to the policy, and keeping those two jobs apart is what lets the
/// same detector be used under opposite rules.
/// </para>
/// <para>
/// The type names a location and never a value. Anything that wanted to carry the matched
/// text would have to put it here, so the absence of such a member is the guarantee.
/// </para>
/// </remarks>
public sealed class SensitiveEntity
{
    private SensitiveEntity(
        EntityId id,
        EntityType type,
        SourceSpan span,
        DetectionConfidence confidence,
        DetectorId detector,
        SensitivityLevel sensitivity,
        DetectionEvidence evidence)
    {
        Id = id;
        Type = type;
        Span = span;
        Confidence = confidence;
        Detector = detector;
        Sensitivity = sensitivity;
        Evidence = evidence;
    }

    /// <summary>
    /// Gets the identifier of the candidate.
    /// </summary>
    /// <remarks>
    /// The identifier is what ties a detection to the transformation that was applied to it
    /// and to the audit record that explains why.
    /// </remarks>
    public EntityId Id { get; }

    /// <summary>
    /// Gets what kind of sensitive thing the detector believes this is.
    /// </summary>
    public EntityType Type { get; }

    /// <summary>
    /// Gets where in the extracted text it is.
    /// </summary>
    public SourceSpan Span { get; }

    /// <summary>
    /// Gets how sure the detector is.
    /// </summary>
    public DetectionConfidence Confidence { get; }

    /// <summary>
    /// Gets the detector that reported the candidate.
    /// </summary>
    public DetectorId Detector { get; }

    /// <summary>
    /// Gets how much damage disclosing a value of this type would do.
    /// </summary>
    public SensitivityLevel Sensitivity { get; }

    /// <summary>
    /// Gets why the detector believes this is a match.
    /// </summary>
    public DetectionEvidence Evidence { get; }

    /// <summary>
    /// Creates a candidate.
    /// </summary>
    /// <param name="id">The identifier of the candidate.</param>
    /// <param name="type">What kind of sensitive thing it is.</param>
    /// <param name="span">Where in the extracted text it is.</param>
    /// <param name="confidence">How sure the detector is.</param>
    /// <param name="detector">The detector that reported it.</param>
    /// <param name="sensitivity">How much damage disclosing it would do.</param>
    /// <param name="evidence">Why the detector believes this is a match.</param>
    /// <returns>The candidate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="evidence"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// An identifier or the type is the default value, the span is not anchored to a segment,
    /// or <paramref name="sensitivity"/> is undefined.
    /// </exception>
    public static SensitiveEntity Create(
        EntityId id,
        EntityType type,
        SourceSpan span,
        DetectionConfidence confidence,
        DetectorId detector,
        SensitivityLevel sensitivity,
        DetectionEvidence evidence)
    {
        if (id.IsDefault)
        {
            throw new ArgumentException("The entity identifier carries no value.", nameof(id));
        }

        if (type.IsDefault)
        {
            throw new ArgumentException("The entity type carries no value.", nameof(type));
        }

        if (span.SegmentId.IsDefault)
        {
            throw new ArgumentException("The span is not anchored to a segment.", nameof(span));
        }

        if (detector.IsDefault)
        {
            throw new ArgumentException("The detector identifier carries no value.", nameof(detector));
        }

        if (!Enum.IsDefined(sensitivity))
        {
            throw new ArgumentException("The sensitivity level is not a defined value.", nameof(sensitivity));
        }

        ArgumentNullException.ThrowIfNull(evidence);

        return new SensitiveEntity(id, type, span, confidence, detector, sensitivity, evidence);
    }

    /// <summary>
    /// Returns a description of the candidate.
    /// </summary>
    /// <returns>
    /// The type, the location, the confidence and the detector. It never includes the matched
    /// text, which is what makes a detection record safe to log.
    /// </returns>
    public override string ToString() => $"{Type} at {Span} ({Confidence}, {Detector})";
}
