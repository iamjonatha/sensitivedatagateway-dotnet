using System;

namespace SensitiveDataGateway;

/// <summary>
/// A content-free description of something that went wrong, and of where.
/// </summary>
/// <remarks>
/// <para>
/// An error names a category, a stable code and the location in the bundle it concerns. It
/// never carries the text at that location. This is the rule that lets an error be logged,
/// serialized into an audit record and returned to a caller outside the trust boundary
/// without review.
/// </para>
/// <para>
/// <see cref="Description"/> is a fixed sentence chosen by the code path that raised the
/// error. It is never assembled from document content, from a file name or from any other
/// value read out of the bundle. The type bounds its length and rejects control characters,
/// which limits the damage of a mistake but cannot prevent one: the rule is a review gate,
/// not a mechanism.
/// </para>
/// </remarks>
public sealed class GatewayError
{
    /// <summary>
    /// The greatest number of characters a <see cref="Code"/> may contain.
    /// </summary>
    public const int MaxCodeLength = 64;

    /// <summary>
    /// The greatest number of characters a <see cref="Description"/> may contain.
    /// </summary>
    public const int MaxDescriptionLength = 512;

    private GatewayError(
        GatewayErrorCategory category,
        string code,
        string description,
        BundleId? bundleId,
        PartId? partId,
        SegmentId? segmentId,
        EntityId? entityId)
    {
        Category = category;
        Code = code;
        Description = description;
        BundleId = bundleId;
        PartId = partId;
        SegmentId = segmentId;
        EntityId = entityId;
    }

    /// <summary>
    /// Gets the kind of problem this error describes.
    /// </summary>
    public GatewayErrorCategory Category { get; }

    /// <summary>
    /// Gets the stable, machine-readable code of the error.
    /// </summary>
    /// <value>
    /// A short identifier such as <c>extraction.unsupported_media_type</c>, chosen from a
    /// fixed set by the code path that raised the error.
    /// </value>
    /// <remarks>
    /// The code is what automation matches on. It is part of the compatibility surface: it
    /// may be added to, but an existing code does not change meaning.
    /// </remarks>
    public string Code { get; }

    /// <summary>
    /// Gets a fixed, content-free sentence describing the problem.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the processing operation the error belongs to, when one is known.
    /// </summary>
    public BundleId? BundleId { get; }

    /// <summary>
    /// Gets the part the error concerns, when one is known.
    /// </summary>
    public PartId? PartId { get; }

    /// <summary>
    /// Gets the segment the error concerns, when one is known.
    /// </summary>
    public SegmentId? SegmentId { get; }

    /// <summary>
    /// Gets the entity the error concerns, when one is known.
    /// </summary>
    public EntityId? EntityId { get; }

    /// <summary>
    /// Creates an error.
    /// </summary>
    /// <param name="category">The kind of problem.</param>
    /// <param name="code">The stable, machine-readable code.</param>
    /// <param name="description">A fixed, content-free sentence describing the problem.</param>
    /// <returns>The error.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="code"/> or <paramref name="description"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="category"/> is not a defined value, or a text argument is empty,
    /// too long, or contains a control character.
    /// </exception>
    public static GatewayError Create(
        GatewayErrorCategory category,
        string code,
        string description)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentException("The error category is not a defined value.", nameof(category));
        }

        return new GatewayError(
            category,
            Text.Require(code, MaxCodeLength, nameof(code)),
            Text.Require(description, MaxDescriptionLength, nameof(description)),
            bundleId: null,
            partId: null,
            segmentId: null,
            entityId: null);
    }

    /// <summary>
    /// Returns a copy of this error that names the processing operation it belongs to.
    /// </summary>
    /// <param name="bundleId">The bundle identifier.</param>
    /// <returns>The new error.</returns>
    /// <exception cref="ArgumentException"><paramref name="bundleId"/> is the default value.</exception>
    public GatewayError InBundle(BundleId bundleId)
    {
        if (bundleId.IsDefault)
        {
            throw new ArgumentException("The bundle identifier carries no value.", nameof(bundleId));
        }

        return new GatewayError(Category, Code, Description, bundleId, PartId, SegmentId, EntityId);
    }

    /// <summary>
    /// Returns a copy of this error that names the part it concerns.
    /// </summary>
    /// <param name="partId">The part identifier.</param>
    /// <returns>The new error.</returns>
    /// <exception cref="ArgumentException"><paramref name="partId"/> is the default value.</exception>
    public GatewayError InPart(PartId partId)
    {
        if (partId.IsDefault)
        {
            throw new ArgumentException("The part identifier carries no value.", nameof(partId));
        }

        return new GatewayError(Category, Code, Description, BundleId, partId, SegmentId, EntityId);
    }

    /// <summary>
    /// Returns a copy of this error that names the segment it concerns.
    /// </summary>
    /// <param name="segmentId">The segment identifier.</param>
    /// <returns>The new error.</returns>
    /// <exception cref="ArgumentException"><paramref name="segmentId"/> is the default value.</exception>
    public GatewayError InSegment(SegmentId segmentId)
    {
        if (segmentId.IsDefault)
        {
            throw new ArgumentException("The segment identifier carries no value.", nameof(segmentId));
        }

        return new GatewayError(Category, Code, Description, BundleId, PartId, segmentId, EntityId);
    }

    /// <summary>
    /// Returns a copy of this error that names the entity it concerns.
    /// </summary>
    /// <param name="entityId">The entity identifier.</param>
    /// <returns>The new error.</returns>
    /// <exception cref="ArgumentException"><paramref name="entityId"/> is the default value.</exception>
    public GatewayError ForEntity(EntityId entityId)
    {
        if (entityId.IsDefault)
        {
            throw new ArgumentException("The entity identifier carries no value.", nameof(entityId));
        }

        return new GatewayError(Category, Code, Description, BundleId, PartId, SegmentId, entityId);
    }

    /// <summary>
    /// Returns the code and the description of the error.
    /// </summary>
    /// <returns>A diagnostic string that carries no document content.</returns>
    public override string ToString() => string.Concat(Code, ": ", Description);
}
