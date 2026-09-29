using System;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// The record that one value was rewritten, and how.
/// </summary>
/// <remarks>
/// This is the audit trail of a sanitization. It says which candidate was acted on, what was
/// done to it, where it was and how much text replaced it — everything needed to explain the
/// output, and nothing that would reproduce the input.
/// </remarks>
public sealed class AppliedTransformation
{
    private AppliedTransformation(
        EntityId entityId,
        EntityType entityType,
        TransformationAction action,
        SourceSpan span,
        int replacementLength)
    {
        EntityId = entityId;
        EntityType = entityType;
        Action = action;
        Span = span;
        ReplacementLength = replacementLength;
    }

    /// <summary>
    /// Gets the candidate that was acted on.
    /// </summary>
    public EntityId EntityId { get; }

    /// <summary>
    /// Gets what kind of sensitive thing it was.
    /// </summary>
    public EntityType EntityType { get; }

    /// <summary>
    /// Gets what was done to it.
    /// </summary>
    public TransformationAction Action { get; }

    /// <summary>
    /// Gets where it was in the extracted text, before the rewrite.
    /// </summary>
    /// <remarks>
    /// The span refers to the original segment. Offsets into the sanitized segment are not
    /// recorded, because a replacement of a different length moves everything after it and
    /// the resulting arithmetic is a good way to produce an audit record that is subtly
    /// wrong.
    /// </remarks>
    public SourceSpan Span { get; }

    /// <summary>
    /// Gets the number of characters that replaced it.
    /// </summary>
    public int ReplacementLength { get; }

    /// <summary>
    /// Creates a transformation record.
    /// </summary>
    /// <param name="entityId">The candidate that was acted on.</param>
    /// <param name="entityType">What kind of sensitive thing it was.</param>
    /// <param name="action">What was done to it.</param>
    /// <param name="span">Where it was in the extracted text.</param>
    /// <param name="replacementLength">The number of characters that replaced it.</param>
    /// <returns>The transformation record.</returns>
    /// <exception cref="ArgumentException">
    /// An identifier or the type is the default value, the span is not anchored to a segment,
    /// or <paramref name="action"/> is undefined.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="replacementLength"/> is negative.</exception>
    public static AppliedTransformation Create(
        EntityId entityId,
        EntityType entityType,
        TransformationAction action,
        SourceSpan span,
        int replacementLength)
    {
        if (entityId.IsDefault)
        {
            throw new ArgumentException("The entity identifier carries no value.", nameof(entityId));
        }

        if (entityType.IsDefault)
        {
            throw new ArgumentException("The entity type carries no value.", nameof(entityType));
        }

        if (!Enum.IsDefined(action))
        {
            throw new ArgumentException("The transformation action is not a defined value.", nameof(action));
        }

        if (span.SegmentId.IsDefault)
        {
            throw new ArgumentException("The span is not anchored to a segment.", nameof(span));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(replacementLength);

        return new AppliedTransformation(entityId, entityType, action, span, replacementLength);
    }

    /// <summary>
    /// Returns a description of the transformation.
    /// </summary>
    /// <returns>
    /// The action, the type and the location. It never includes the original value or its
    /// replacement.
    /// </returns>
    public override string ToString() => $"{Action} {EntityType} at {Span}";
}
