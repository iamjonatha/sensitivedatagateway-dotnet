using System;
using System.Collections.Generic;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Validation;

/// <summary>
/// Looks for the values of stage 2 in the output of stage 4.
/// </summary>
/// <remarks>
/// <para>
/// This is the check that catches a bug in the stages before it. It is deliberately redundant
/// with them: two independent implementations of "this value must not be here" fail
/// independently, and the second one is the reason a defect in the first is not a breach.
/// </para>
/// <para>
/// It is not sufficient, and the design does not pretend otherwise. It can only look for what
/// detection found, so a value no detector saw is as invisible here as it was there. The
/// pattern and entropy checks are the part that does not depend on detection at all.
/// </para>
/// </remarks>
internal sealed class AntiLeakScanner
{
    private readonly ValidationOptions _options;

    public AntiLeakScanner(ValidationOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Scans sanitized output for values that should not have survived it.
    /// </summary>
    /// <param name="candidates">The values detection found in the original.</param>
    /// <param name="output">The sanitized segments.</param>
    /// <returns>One finding per value and segment, empty when the output is clean.</returns>
    public IReadOnlyList<PrivacyFinding> Scan(
        IReadOnlyList<LeakCandidate> candidates,
        IReadOnlyList<SanitizedSegment> output)
    {
        var findings = new List<PrivacyFinding>();

        foreach (var segment in output)
        {
            var visible = MarkerSyntax.WithoutMarkers(segment.Text);

            ScanSegment(candidates, segment.Id, visible, findings);
        }

        return findings;
    }

    private void ScanSegment(
        IReadOnlyList<LeakCandidate> candidates,
        SegmentId segment,
        string visible,
        List<PrivacyFinding> findings)
    {
        var reduced = TextNormalizer.Reduce(visible);

        foreach (var candidate in candidates)
        {
            var signal = Recognize(candidate, visible, reduced);

            if (signal != LeakSignal.None)
            {
                findings.Add(PrivacyFinding.ForLeak(
                    PrivacyCheck.AntiLeakScan,
                    signal,
                    candidate.Type,
                    segment));
            }
        }

        if (SecretPatterns.TryMatch(visible, out var secret))
        {
            findings.Add(PrivacyFinding.ForLeak(
                PrivacyCheck.AntiLeakScan,
                LeakSignal.SecretPattern,
                secret,
                segment));
        }

        if (CarriesKeyMaterial(visible))
        {
            findings.Add(PrivacyFinding.ForLeak(PrivacyCheck.AntiLeakScan, LeakSignal.HighEntropy, segment));
        }
    }

    private LeakSignal Recognize(LeakCandidate candidate, string visible, string reduced)
    {
        if (visible.Contains(candidate.Value, StringComparison.Ordinal))
        {
            return LeakSignal.Exact;
        }

        var value = TextNormalizer.Reduce(candidate.Value);

        if (value.Length >= _options.MinimumFragmentLength && reduced.Contains(value, StringComparison.Ordinal))
        {
            return LeakSignal.Normalized;
        }

        return CarriesFragment(candidate.Value, reduced) ? LeakSignal.Fragment : LeakSignal.None;
    }

    private bool CarriesFragment(string value, string reduced)
    {
        foreach (var fragment in Fragments(value))
        {
            if (reduced.Contains(fragment, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Splits a value into the parts that are long enough to identify it on their own.
    /// </summary>
    /// <param name="value">The detected value.</param>
    /// <returns>
    /// The reduced parts of at least the configured length. A value made of a single part
    /// yields nothing, because the whole of it was already compared.
    /// </returns>
    private List<string> Fragments(string value)
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

            if (reduced.Length >= _options.MinimumFragmentLength)
            {
                fragments.Add(reduced);
            }
        }

        return fragments;
    }

    private bool CarriesKeyMaterial(string visible)
    {
        var start = -1;

        for (var index = 0; index <= visible.Length; index++)
        {
            if (index < visible.Length && IsKeyCharacter(visible[index]))
            {
                start = start < 0 ? index : start;
                continue;
            }

            if (start >= 0 && IsKeyMaterial(visible.AsSpan(start, index - start)))
            {
                return true;
            }

            start = -1;
        }

        return false;
    }

    private bool IsKeyMaterial(ReadOnlySpan<char> run) =>
        run.Length >= _options.MinimumEntropyLength
        && ShannonEntropy.Measure(run) >= _options.EntropyThreshold;

    private static bool IsKeyCharacter(char value) =>
        char.IsAsciiLetterOrDigit(value) || value is '+' or '/' or '=' or '_' or '-';
}
