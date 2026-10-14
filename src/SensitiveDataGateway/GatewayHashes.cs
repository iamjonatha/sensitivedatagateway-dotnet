using System.Text;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway;

/// <summary>
/// The digests an audit record may carry.
/// </summary>
/// <remarks>
/// A digest is produced only when the application supplied a key. Without one these return
/// <see langword="null"/>, and the record carries no tie to content at all — which is the
/// state an application gets without asking for anything.
/// </remarks>
internal static class GatewayHashes
{
    internal static string? Document(HashingKey? key, ExtractedDocument extracted)
    {
        if (key is null)
        {
            return null;
        }

        var builder = new StringBuilder();

        foreach (var segment in extracted.Segments)
        {
            builder.Append(segment.Text).Append('\n');
        }

        return key.Digest(builder.ToString());
    }

    internal static string? Payload(HashingKey? key, ValidatedPayload? payload) =>
        key is null || payload is null ? null : key.Digest(payload.ContentHash);
}
