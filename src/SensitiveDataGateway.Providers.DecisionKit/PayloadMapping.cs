using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DecisionKit.Identifiers;
using DecisionKit.Providers;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Providers.DecisionKit;

/// <summary>
/// Turns a validated payload into the pieces of a DecisionKit request.
/// </summary>
/// <remarks>
/// Every method here takes a <see cref="ValidatedPayload"/> and nothing else. That is the
/// whole point of the type: there is no overload that accepts a bundle, an extracted
/// document or a detected entity, so there is no path by which one could be mapped.
/// </remarks>
internal static class PayloadMapping
{
    /// <summary>
    /// What separates two segments when they are joined into one block of text.
    /// </summary>
    /// <remarks>
    /// Segments are paragraphs, so a blank line between them is what a reader — human or
    /// model — expects. This is deliberately not configurable: a caller who needs a
    /// different shape wants <see cref="DecisionKitPayloadFormat.Segments"/>, not a
    /// different separator.
    /// </remarks>
    private const string SegmentSeparator = "\n\n";

    public static DecisionInput Input(ValidatedPayload payload, DecisionKitPayloadFormat format) =>
        format == DecisionKitPayloadFormat.Segments
            ? DecisionInput.FromProperties(Properties(payload))
            : DecisionInput.FromText(Text(payload));

    public static RequestId? ClientRequestId(ValidatedPayload payload) =>
        RequestId.TryCreate(payload.BundleId.Value, out var requestId) ? requestId : null;

    public static IReadOnlyDictionary<string, object?> Metadata(ValidatedPayload payload)
    {
        var provenance = payload.Provenance;

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [DecisionKitMetadataKeys.PolicyId] = provenance.PolicyId.Value,
            [DecisionKitMetadataKeys.PolicyVersion] = provenance.PolicyVersion.Value,
            [DecisionKitMetadataKeys.GatewayVersion] = provenance.GatewayVersion,
            [DecisionKitMetadataKeys.ContentHash] = payload.ContentHash,
            [DecisionKitMetadataKeys.SanitizedAt] =
                provenance.SanitizedAt.ToString("O", CultureInfo.InvariantCulture),
        };
    }

    private static string Text(ValidatedPayload payload) =>
        string.Join(SegmentSeparator, payload.Content.Select(static segment => segment.Text));

    /// <summary>
    /// Keys the segments by their identifier.
    /// </summary>
    /// <param name="payload">The payload.</param>
    /// <returns>The properties.</returns>
    /// <exception cref="ArgumentException">
    /// Two segments share an identifier. The pipeline derives segment identifiers so that a
    /// bundle scanned twice produces the same ones, which also makes them unique within a
    /// payload; a duplicate means a segment would be dropped, and dropping one silently is
    /// worse than failing.
    /// </exception>
    private static Dictionary<string, object?> Properties(ValidatedPayload payload)
    {
        var properties = new Dictionary<string, object?>(payload.Content.Count, StringComparer.Ordinal);

        foreach (var segment in payload.Content)
        {
            properties.Add(segment.Id.Value, segment.Text);
        }

        return properties;
    }
}
