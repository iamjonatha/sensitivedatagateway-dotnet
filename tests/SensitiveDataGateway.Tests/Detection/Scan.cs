using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Tests.Detection;

/// <summary>
/// Runs a detector over a single piece of text and reports what it claimed.
/// </summary>
/// <remarks>
/// Every piece of text in these tests is invented. The account numbers, tax codes and keys
/// are synthetic values built to satisfy the relevant check digit, and none of them belongs
/// to anyone.
/// </remarks>
internal static class Scan
{
    /// <summary>
    /// Returns the exact text each candidate covers.
    /// </summary>
    /// <param name="detector">The detector to run.</param>
    /// <param name="text">The text to run it over.</param>
    /// <returns>The matched text, in the order the detector reported it.</returns>
    public static async Task<IReadOnlyList<string>> MatchesIn(
        ISensitiveDataDetector detector,
        string text)
    {
        var entities = await Entities(detector, text);

        return [.. entities.Select(entity => text.Substring(entity.Span.Start, entity.Span.Length))];
    }

    /// <summary>
    /// Returns everything the detector claimed.
    /// </summary>
    /// <param name="detector">The detector to run.</param>
    /// <param name="text">The text to run it over.</param>
    /// <returns>The candidates.</returns>
    public static async Task<IReadOnlyList<SensitiveEntity>> Entities(
        ISensitiveDataDetector detector,
        string text) =>
        await detector.DetectAsync(Fixture.Extracted(text), CancellationToken.None);
}
