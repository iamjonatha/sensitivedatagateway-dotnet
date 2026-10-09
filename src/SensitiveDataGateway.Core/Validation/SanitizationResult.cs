using System;

namespace SensitiveDataGateway.Validation;

/// <summary>
/// The outcome of the final privacy validation: a payload, or the reasons there is none.
/// </summary>
/// <remarks>
/// A block is a result, not an exception. The distinction is the one
/// <see href="https://github.com/iamjonatha/sensitivedatagateway-dotnet/blob/main/docs/adr/0004-fail-closed.md">ADR 0004</see>
/// makes: a refused document is the system working, and a caller must be able to read why
/// without catching anything.
/// </remarks>
public sealed class SanitizationResult
{
    private SanitizationResult(ValidatedPayload? payload, PrivacyValidationReport report)
    {
        Payload = payload;
        Report = report;
    }

    /// <summary>
    /// Gets a value indicating whether the payload may leave the trust boundary.
    /// </summary>
    public bool IsAllowed => Payload is not null;

    /// <summary>
    /// Gets the validated payload, or <see langword="null"/> when the document was refused.
    /// </summary>
    public ValidatedPayload? Payload { get; }

    /// <summary>
    /// Gets what was checked, and what was found.
    /// </summary>
    public PrivacyValidationReport Report { get; }

    /// <summary>
    /// Gets the processing operation this result concerns.
    /// </summary>
    public BundleId BundleId => Report.BundleId;

    /// <summary>
    /// Returns a description of the result.
    /// </summary>
    /// <returns>Whether the payload was allowed, and the report. It carries no content.</returns>
    public override string ToString() => IsAllowed ? $"Allowed: {Report}" : $"Blocked: {Report}";

    /// <summary>
    /// Creates the result of a document that passed every check.
    /// </summary>
    /// <param name="payload">The validated payload.</param>
    /// <param name="report">What was checked.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The report records findings.</exception>
    internal static SanitizationResult Allowed(ValidatedPayload payload, PrivacyValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(report);

        if (!report.Passed)
        {
            throw new ArgumentException("A report with findings cannot allow a payload.", nameof(report));
        }

        return new SanitizationResult(payload, report);
    }

    /// <summary>
    /// Creates the result of a document that was refused.
    /// </summary>
    /// <param name="report">What was checked, and what was found.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="report"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The report records no finding.</exception>
    internal static SanitizationResult Blocked(PrivacyValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (report.Passed)
        {
            throw new ArgumentException("A report without findings cannot block a payload.", nameof(report));
        }

        return new SanitizationResult(payload: null, report);
    }
}
