using System;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Validation;

/// <summary>
/// One reason the final privacy validator refuses a payload.
/// </summary>
/// <remarks>
/// A finding names the check that failed, and the entity or segment it concerns when there is
/// one. It has no free-text field, which is deliberate: a free-text field is how a value ends
/// up in a report, and this report is written precisely because a value must not travel.
/// </remarks>
public sealed class PrivacyFinding
{
    private PrivacyFinding(
        PrivacyCheck check,
        LeakSignal signal,
        EntityId? entity,
        EntityType? type,
        SegmentId? segment)
    {
        Check = check;
        Signal = signal;
        Entity = entity;
        Type = type;
        Segment = segment;
    }

    /// <summary>
    /// Gets the check that failed.
    /// </summary>
    public PrivacyCheck Check { get; }

    /// <summary>
    /// Gets how the anti-leak scanner recognized the value, when the finding came from it.
    /// </summary>
    public LeakSignal Signal { get; }

    /// <summary>
    /// Gets the entity the finding concerns, when it concerns one.
    /// </summary>
    public EntityId? Entity { get; }

    /// <summary>
    /// Gets the type of that entity, when the finding concerns one.
    /// </summary>
    public EntityType? Type { get; }

    /// <summary>
    /// Gets the segment the finding concerns, when it concerns one.
    /// </summary>
    public SegmentId? Segment { get; }

    /// <summary>
    /// Creates a finding about the document as a whole.
    /// </summary>
    /// <param name="check">The check that failed.</param>
    /// <returns>The finding.</returns>
    /// <exception cref="ArgumentException"><paramref name="check"/> is not a defined check.</exception>
    public static PrivacyFinding For(PrivacyCheck check) =>
        new(RequireCheck(check), LeakSignal.None, entity: null, type: null, segment: null);

    /// <summary>
    /// Creates a finding about one segment.
    /// </summary>
    /// <param name="check">The check that failed.</param>
    /// <param name="segment">The segment the finding concerns.</param>
    /// <returns>The finding.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="check"/> is not a defined check, or <paramref name="segment"/> is the
    /// default value.
    /// </exception>
    public static PrivacyFinding InSegment(PrivacyCheck check, SegmentId segment)
    {
        if (segment.IsDefault)
        {
            throw new ArgumentException("The segment identifier carries no value.", nameof(segment));
        }

        return new PrivacyFinding(RequireCheck(check), LeakSignal.None, entity: null, type: null, segment);
    }

    /// <summary>
    /// Creates a finding about one entity.
    /// </summary>
    /// <param name="check">The check that failed.</param>
    /// <param name="entity">The entity the finding concerns.</param>
    /// <param name="type">The type of that entity.</param>
    /// <returns>The finding.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="check"/> is not a defined check, or an identifier is the default value.
    /// </exception>
    public static PrivacyFinding ForEntity(PrivacyCheck check, EntityId entity, EntityType type)
    {
        RequireEntity(entity, type);

        return new PrivacyFinding(RequireCheck(check), LeakSignal.None, entity, type, segment: null);
    }

    /// <summary>
    /// Creates a finding raised by the anti-leak scanner.
    /// </summary>
    /// <param name="check">The check that failed.</param>
    /// <param name="signal">How the scanner recognized the value.</param>
    /// <param name="type">The type of value that was recognized.</param>
    /// <param name="segment">The segment it was recognized in.</param>
    /// <returns>The finding.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="check"/> is not a defined check, <paramref name="signal"/> is
    /// <see cref="LeakSignal.None"/> or undefined, <paramref name="type"/> is the default
    /// value, or <paramref name="segment"/> is the default value.
    /// </exception>
    public static PrivacyFinding ForLeak(
        PrivacyCheck check,
        LeakSignal signal,
        EntityType type,
        SegmentId segment)
    {
        RequireSignal(signal);

        if (type.IsDefault)
        {
            throw new ArgumentException("The entity type carries no value.", nameof(type));
        }

        if (segment.IsDefault)
        {
            throw new ArgumentException("The segment identifier carries no value.", nameof(segment));
        }

        return new PrivacyFinding(RequireCheck(check), signal, entity: null, type, segment);
    }

    /// <summary>
    /// Creates a finding raised by the anti-leak scanner about something it could not classify.
    /// </summary>
    /// <param name="check">The check that failed.</param>
    /// <param name="signal">How the scanner recognized the string.</param>
    /// <param name="segment">The segment it was recognized in.</param>
    /// <returns>The finding.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="check"/> is not a defined check, <paramref name="signal"/> is
    /// <see cref="LeakSignal.None"/> or undefined, or <paramref name="segment"/> is the
    /// default value.
    /// </exception>
    /// <remarks>
    /// The entropy check has no type to report: it blocks a string because of how it is
    /// shaped, not because anybody decided what it is.
    /// </remarks>
    public static PrivacyFinding ForLeak(PrivacyCheck check, LeakSignal signal, SegmentId segment)
    {
        RequireSignal(signal);

        if (segment.IsDefault)
        {
            throw new ArgumentException("The segment identifier carries no value.", nameof(segment));
        }

        return new PrivacyFinding(RequireCheck(check), signal, entity: null, type: null, segment);
    }

    /// <summary>
    /// Describes the finding as a content-free error.
    /// </summary>
    /// <returns>
    /// An error categorized as <see cref="GatewayErrorCategory.ValidationFailed"/>, naming the
    /// entity and the segment when the finding concerns them.
    /// </returns>
    public GatewayError ToError()
    {
        var error = GatewayError.Create(GatewayErrorCategory.ValidationFailed, Code(), Description());

        if (Entity is { } entity)
        {
            error = error.ForEntity(entity);
        }

        return Segment is { } segment ? error.InSegment(segment) : error;
    }

    /// <summary>
    /// Returns a description of the finding.
    /// </summary>
    /// <returns>
    /// The check, the leak signal when there is one, and the type it concerns. It carries no
    /// document content.
    /// </returns>
    public override string ToString() => (Signal, Type) switch
    {
        (LeakSignal.None, null) => Check.ToString(),
        (LeakSignal.None, { } type) => $"{Check} ({type})",
        (_, null) => $"{Check} ({Signal})",
        (_, { } type) => $"{Check} ({Signal}, {type})",
    };

    private static PrivacyCheck RequireCheck(PrivacyCheck check) =>
        check != PrivacyCheck.None && Enum.IsDefined(check)
            ? check
            : throw new ArgumentException("The check is not a defined value.", nameof(check));

    private static void RequireSignal(LeakSignal signal)
    {
        if (signal == LeakSignal.None || !Enum.IsDefined(signal))
        {
            throw new ArgumentException("The leak signal is not a defined value.", nameof(signal));
        }
    }

    private static void RequireEntity(EntityId entity, EntityType type)
    {
        if (entity.IsDefault)
        {
            throw new ArgumentException("The entity identifier carries no value.", nameof(entity));
        }

        if (type.IsDefault)
        {
            throw new ArgumentException("The entity type carries no value.", nameof(type));
        }
    }

    private string Code() => Check switch
    {
        PrivacyCheck.SanitizationStatus => "validation.sanitization_status",
        PrivacyCheck.ScanCompleteness => "validation.scan_incomplete",
        PrivacyCheck.UnresolvedEntities => "validation.unresolved_entity",
        PrivacyCheck.PolicyViolations => "validation.policy_violation",
        PrivacyCheck.TransformationCoverage => "validation.transformation_missing",
        PrivacyCheck.AntiLeakScan => "validation.value_leaked",
        PrivacyCheck.TokenResolution => "validation.token_resolvable",
        PrivacyCheck.MarkerIntegrity => "validation.marker_forged",
        PrivacyCheck.ProvenanceCompleteness => "validation.provenance_incomplete",
        _ => "validation.payload_structure",
    };

    private string Description() => Check switch
    {
        PrivacyCheck.SanitizationStatus =>
            "The document was not fully sanitized, and partial sanitization is not sanitization.",
        PrivacyCheck.ScanCompleteness =>
            "An extractor or a detector did not complete, so part of the document was never examined.",
        PrivacyCheck.UnresolvedEntities =>
            "A candidate was not resolved and its sensitivity exceeds what the policy tolerates.",
        PrivacyCheck.PolicyViolations =>
            "The plan carries a policy violation, and a violation is not a warning.",
        PrivacyCheck.TransformationCoverage =>
            "A planned transformation was not applied to the output.",
        PrivacyCheck.AntiLeakScan =>
            "A value a detector found in the original is still present in the output.",
        PrivacyCheck.TokenResolution =>
            "The output carries a pseudonymized value next to the token that stands for it.",
        PrivacyCheck.MarkerIntegrity =>
            "The output carries a marker the gateway did not write.",
        PrivacyCheck.ProvenanceCompleteness =>
            "The provenance does not record which policy and which detectors produced this output.",
        _ =>
            "The payload is malformed or exceeds the limits the policy sets.",
    };
}
