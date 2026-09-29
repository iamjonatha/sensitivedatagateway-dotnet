using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SensitiveDataGateway.Anonymization;

namespace SensitiveDataGateway.Validation;

/// <summary>
/// The only thing the gateway will let out: sanitized content that has passed validation.
/// </summary>
/// <remarks>
/// <para>
/// This type has no public constructor and no public factory. It is produced by the
/// validation stage and by nothing else, which is what turns "a provider accepts only
/// validated content" into a statement about the type system rather than a convention
/// somebody has to follow. A test cannot forge one either, which is the point: if a test
/// could, the guarantee would be about discipline rather than about types.
/// </para>
/// <para>
/// A payload carries no pseudonym map, no entity list, no raw content and no reference to the
/// bundle it came from. Everything that would let a recipient undo the sanitization is
/// absent, by construction rather than by omission.
/// </para>
/// </remarks>
public sealed class ValidatedPayload
{
    private const int MaxContentHashLength = 128;

    private ValidatedPayload(
        BundleId bundleId,
        IReadOnlyList<SanitizedSegment> content,
        SanitizationProvenance provenance,
        string contentHash)
    {
        BundleId = bundleId;
        Content = content;
        Provenance = provenance;
        ContentHash = contentHash;
    }

    /// <summary>
    /// Gets the processing operation this payload belongs to.
    /// </summary>
    /// <remarks>
    /// The identifier correlates the payload with the audit record. It is opaque, and it is
    /// not derived from anything in the document.
    /// </remarks>
    public BundleId BundleId { get; }

    /// <summary>
    /// Gets the sanitized content, in reading order.
    /// </summary>
    public IReadOnlyList<SanitizedSegment> Content { get; }

    /// <summary>
    /// Gets how the content came to look the way it does.
    /// </summary>
    public SanitizationProvenance Provenance { get; }

    /// <summary>
    /// Gets a hash of the content as it was when validation passed.
    /// </summary>
    /// <remarks>
    /// The hash is what lets an audit record prove that what was transmitted is what was
    /// approved. It is computed over the sanitized text only, so it discloses nothing about
    /// the original.
    /// </remarks>
    public string ContentHash { get; }

    /// <summary>
    /// Creates a validated payload.
    /// </summary>
    /// <param name="bundleId">The processing operation this payload belongs to.</param>
    /// <param name="content">The sanitized content. The sequence is copied.</param>
    /// <param name="provenance">How the content came to look the way it does.</param>
    /// <param name="contentHash">A hash of the content as it was when validation passed.</param>
    /// <returns>The payload.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="bundleId"/> is the default value, the content contains
    /// <see langword="null"/>, or the hash is empty, too long, or has a control character.
    /// </exception>
    /// <remarks>
    /// This factory is internal on purpose: it is callable by the validation stage and by
    /// nothing else. See the remarks on the type for why that matters.
    /// </remarks>
    internal static ValidatedPayload Create(
        BundleId bundleId,
        IEnumerable<SanitizedSegment> content,
        SanitizationProvenance provenance,
        string contentHash)
    {
        if (bundleId.IsDefault)
        {
            throw new ArgumentException("The bundle identifier carries no value.", nameof(bundleId));
        }

        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(provenance);

        var copied = new List<SanitizedSegment>();

        foreach (var segment in content)
        {
            if (segment is null)
            {
                throw new ArgumentException("The content cannot contain a null segment.", nameof(content));
            }

            copied.Add(segment);
        }

        return new ValidatedPayload(
            bundleId,
            new ReadOnlyCollection<SanitizedSegment>(copied),
            provenance,
            Text.Require(contentHash, MaxContentHashLength, nameof(contentHash)));
    }

    /// <summary>
    /// Returns a description of the payload.
    /// </summary>
    /// <returns>
    /// The bundle, the number of segments and the content hash. It never includes the
    /// content, sanitized or not.
    /// </returns>
    public override string ToString() => $"{BundleId} ({Content.Count} segments, {ContentHash})";
}
