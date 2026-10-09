using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SensitiveDataGateway.Validation;

/// <summary>
/// What the final privacy validator checked, and what it found.
/// </summary>
/// <remarks>
/// <para>
/// The report lists every check that ran, not only the ones that failed. A report that
/// recorded failures alone could not tell "nothing was wrong" apart from "nothing was
/// looked at", and those are the two states a privacy boundary must never confuse.
/// </para>
/// <para>
/// It carries no document content. Its findings name checks, entities, segments and types,
/// and the types they name are categories rather than values.
/// </para>
/// </remarks>
public sealed class PrivacyValidationReport
{
    private PrivacyValidationReport(
        BundleId bundleId,
        IReadOnlyList<PrivacyCheck> executed,
        IReadOnlyList<PrivacyFinding> findings)
    {
        BundleId = bundleId;
        Executed = executed;
        Findings = findings;
    }

    /// <summary>
    /// Gets the processing operation the report concerns.
    /// </summary>
    public BundleId BundleId { get; }

    /// <summary>
    /// Gets every check that ran, in the order it ran.
    /// </summary>
    public IReadOnlyList<PrivacyCheck> Executed { get; }

    /// <summary>
    /// Gets every reason the payload was refused. It is empty when the payload passed.
    /// </summary>
    public IReadOnlyList<PrivacyFinding> Findings { get; }

    /// <summary>
    /// Gets a value indicating whether every check passed.
    /// </summary>
    public bool Passed => Findings.Count == 0;

    /// <summary>
    /// Describes every finding as a content-free error.
    /// </summary>
    /// <returns>One error per finding, in the order the findings were raised.</returns>
    public IReadOnlyList<GatewayError> ToErrors()
    {
        var errors = new List<GatewayError>(Findings.Count);

        foreach (var finding in Findings)
        {
            errors.Add(finding.ToError().InBundle(BundleId));
        }

        return new ReadOnlyCollection<GatewayError>(errors);
    }

    /// <summary>
    /// Returns a description of the report.
    /// </summary>
    /// <returns>
    /// The bundle, the number of checks that ran and the number of findings. It carries no
    /// document content.
    /// </returns>
    public override string ToString() =>
        Passed
            ? $"{BundleId}: passed {Executed.Count} checks"
            : $"{BundleId}: {Findings.Count} findings over {Executed.Count} checks";

    /// <summary>
    /// Creates a report.
    /// </summary>
    /// <param name="bundleId">The processing operation the report concerns.</param>
    /// <param name="executed">Every check that ran. The sequence is copied.</param>
    /// <param name="findings">Every reason the payload was refused. The sequence is copied.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="bundleId"/> is the default value, no check ran, or a sequence contains
    /// an undefined check or a <see langword="null"/> finding.
    /// </exception>
    /// <remarks>
    /// This factory is internal on purpose. A report is evidence that the checks ran, and
    /// evidence anybody can manufacture is not evidence.
    /// </remarks>
    internal static PrivacyValidationReport Create(
        BundleId bundleId,
        IEnumerable<PrivacyCheck> executed,
        IEnumerable<PrivacyFinding> findings)
    {
        if (bundleId.IsDefault)
        {
            throw new ArgumentException("The bundle identifier carries no value.", nameof(bundleId));
        }

        ArgumentNullException.ThrowIfNull(executed);
        ArgumentNullException.ThrowIfNull(findings);

        var checks = new List<PrivacyCheck>();

        foreach (var check in executed)
        {
            if (check == PrivacyCheck.None || !Enum.IsDefined(check))
            {
                throw new ArgumentException("The sequence contains an undefined check.", nameof(executed));
            }

            checks.Add(check);
        }

        if (checks.Count == 0)
        {
            throw new ArgumentException("A report must record at least one check.", nameof(executed));
        }

        return new PrivacyValidationReport(
            bundleId,
            new ReadOnlyCollection<PrivacyCheck>(checks),
            Copy(findings));
    }

    private static ReadOnlyCollection<PrivacyFinding> Copy(IEnumerable<PrivacyFinding> findings)
    {
        var copied = new List<PrivacyFinding>();

        foreach (var finding in findings)
        {
            if (finding is null)
            {
                throw new ArgumentException("The sequence cannot contain a null finding.", nameof(findings));
            }

            copied.Add(finding);
        }

        return new ReadOnlyCollection<PrivacyFinding>(copied);
    }
}
