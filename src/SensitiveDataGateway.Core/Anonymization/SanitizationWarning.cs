using System;

namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// Something worth recording about a sanitization that did not stop it.
/// </summary>
/// <remarks>
/// A warning names a code and, when it is about one thing, the thing it is about. It never
/// explains itself in prose, so it is safe to log in full.
/// </remarks>
public sealed class SanitizationWarning
{
    private SanitizationWarning(SanitizationWarningCode code, EntityId? entityId, SegmentId? segmentId)
    {
        Code = code;
        EntityId = entityId;
        SegmentId = segmentId;
    }

    /// <summary>
    /// Gets what the warning is about.
    /// </summary>
    public SanitizationWarningCode Code { get; }

    /// <summary>
    /// Gets the candidate the warning concerns, when it concerns one.
    /// </summary>
    public EntityId? EntityId { get; }

    /// <summary>
    /// Gets the segment the warning concerns, when it concerns one.
    /// </summary>
    public SegmentId? SegmentId { get; }

    /// <summary>
    /// Creates a warning about the document as a whole.
    /// </summary>
    /// <param name="code">What the warning is about.</param>
    /// <returns>The warning.</returns>
    /// <exception cref="ArgumentException"><paramref name="code"/> is not a defined value.</exception>
    public static SanitizationWarning Create(SanitizationWarningCode code) =>
        Create(code, entityId: null, segmentId: null);

    /// <summary>
    /// Creates a warning.
    /// </summary>
    /// <param name="code">What the warning is about.</param>
    /// <param name="entityId">The candidate it concerns, or <see langword="null"/>.</param>
    /// <param name="segmentId">The segment it concerns, or <see langword="null"/>.</param>
    /// <returns>The warning.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="code"/> is not a defined value, or a supplied identifier carries no
    /// value.
    /// </exception>
    public static SanitizationWarning Create(
        SanitizationWarningCode code,
        EntityId? entityId,
        SegmentId? segmentId)
    {
        if (!Enum.IsDefined(code))
        {
            throw new ArgumentException("The warning code is not a defined value.", nameof(code));
        }

        if (entityId is { IsDefault: true })
        {
            throw new ArgumentException("The entity identifier carries no value.", nameof(entityId));
        }

        if (segmentId is { IsDefault: true })
        {
            throw new ArgumentException("The segment identifier carries no value.", nameof(segmentId));
        }

        return new SanitizationWarning(code, entityId, segmentId);
    }

    /// <summary>
    /// Returns a description of the warning.
    /// </summary>
    /// <returns>The code and what it concerns. It carries no document content.</returns>
    public override string ToString()
    {
        if (EntityId is { } entity)
        {
            return $"{Code} ({entity})";
        }

        return SegmentId is { } segment ? $"{Code} ({segment})" : Code.ToString();
    }
}
