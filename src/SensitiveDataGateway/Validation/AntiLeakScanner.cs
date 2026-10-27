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
        IReadOnlyList<ScannedCandidate> candidates,
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
        IReadOnlyList<ScannedCandidate> candidates,
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
                    candidate.Candidate.Type,
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

    private LeakSignal Recognize(ScannedCandidate candidate, string visible, string reduced)
    {
        if (visible.Contains(candidate.Candidate.Value, StringComparison.Ordinal))
        {
            return LeakSignal.Exact;
        }

        if (candidate.Reduced.Length >= _options.MinimumFragmentLength
            && reduced.Contains(candidate.Reduced, StringComparison.Ordinal))
        {
            return LeakSignal.Normalized;
        }

        return CarriesFragment(candidate.Fragments, reduced) ? LeakSignal.Fragment : LeakSignal.None;
    }

    private static bool CarriesFragment(IReadOnlyList<string> fragments, string reduced)
    {
        foreach (var fragment in fragments)
        {
            if (reduced.Contains(fragment, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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
