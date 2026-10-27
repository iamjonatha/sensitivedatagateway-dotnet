using System;
using System.Collections.Generic;

namespace SensitiveDataGateway.Validation;

/// <summary>
/// A leak candidate together with the forms the scan compares the output against.
/// </summary>
/// <remarks>
/// <para>
/// The scan compares every candidate against every segment, because a value anonymized in
/// one paragraph must not reappear in another. The comparison is unavoidable; deriving the
/// forms it compares is not. Reducing a value and splitting it into fragments depends on the
/// value alone, so it is done once here rather than once per segment.
/// </para>
/// <para>
/// This type is internal for the same reason <see cref="LeakCandidate"/> is: it holds
/// sensitive values next to their classification, and lives for the duration of one scan.
/// </para>
/// </remarks>
internal sealed class ScannedCandidate
{
    private ScannedCandidate(LeakCandidate candidate, string reduced, List<string> fragments)
    {
        Candidate = candidate;
        Reduced = reduced;
        Fragments = fragments;
    }

    /// <summary>
    /// Gets the value as the detector pointed at it, and what it said it was.
    /// </summary>
    public LeakCandidate Candidate { get; }

    /// <summary>
    /// Gets the reduced form of the value, which may be empty.
    /// </summary>
    public string Reduced { get; }

    /// <summary>
    /// Gets the reduced parts long enough to identify the value on their own.
    /// </summary>
    public IReadOnlyList<string> Fragments { get; }

    /// <summary>
    /// Derives the comparison forms of every candidate.
    /// </summary>
    /// <param name="candidates">The values detection found in the original.</param>
    /// <param name="minimumFragmentLength">The length below which a fragment is not evidence.</param>
    /// <returns>One projection per candidate, in the order they were given.</returns>
    public static List<ScannedCandidate> Project(
        IReadOnlyList<LeakCandidate> candidates,
        int minimumFragmentLength)
    {
        var projected = new List<ScannedCandidate>(candidates.Count);

        foreach (var candidate in candidates)
        {
            projected.Add(new ScannedCandidate(
                candidate,
                TextNormalizer.Reduce(candidate.Value),
                SplitIntoFragments(candidate.Value, minimumFragmentLength)));
        }

        return projected;
    }

    /// <summary>
    /// Splits a value into the parts that are long enough to identify it on their own.
    /// </summary>
    /// <param name="value">The detected value.</param>
    /// <param name="minimumFragmentLength">The length below which a fragment is not evidence.</param>
    /// <returns>
    /// The reduced parts of at least the given length. A value made of a single part yields
    /// nothing, because the whole of it is already compared on its own.
    /// </returns>
    private static List<string> SplitIntoFragments(string value, int minimumFragmentLength)
    {
        var parts = value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var fragments = new List<string>();

        if (parts.Length < 2)
        {
            return fragments;
        }

        foreach (var part in parts)
        {
            var reduced = TextNormalizer.Reduce(part);

            if (reduced.Length >= minimumFragmentLength)
            {
                fragments.Add(reduced);
            }
        }

        return fragments;
    }
}
