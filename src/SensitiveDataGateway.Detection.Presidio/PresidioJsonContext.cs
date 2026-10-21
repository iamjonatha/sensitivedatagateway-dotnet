using System.Text.Json.Serialization;

namespace SensitiveDataGateway.Detection.Presidio;

/// <summary>
/// What is sent to the analyzer's <c>/analyze</c> endpoint.
/// </summary>
/// <param name="Text">The text to examine.</param>
/// <param name="Language">The language code.</param>
/// <param name="ScoreThreshold">The lowest score the analyzer should report.</param>
internal sealed record PresidioAnalyzeRequest(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("language")] string Language,
    [property: JsonPropertyName("score_threshold")] double ScoreThreshold);

/// <summary>
/// One finding the analyzer reports.
/// </summary>
/// <param name="EntityType">The analyzer's own name for what it found.</param>
/// <param name="Start">The offset the finding starts at.</param>
/// <param name="End">The offset after the finding's last character.</param>
/// <param name="Score">How sure the analyzer is, between zero and one.</param>
internal sealed record PresidioAnalyzeResult(
    [property: JsonPropertyName("entity_type")] string EntityType,
    [property: JsonPropertyName("start")] int Start,
    [property: JsonPropertyName("end")] int End,
    [property: JsonPropertyName("score")] double Score);

/// <summary>
/// The serializer for the two shapes above, generated rather than reflected so that the
/// package stays usable when the application is trimmed or published Native AOT.
/// </summary>
[JsonSerializable(typeof(PresidioAnalyzeRequest))]
[JsonSerializable(typeof(PresidioAnalyzeResult[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
internal sealed partial class PresidioJsonContext : JsonSerializerContext;
