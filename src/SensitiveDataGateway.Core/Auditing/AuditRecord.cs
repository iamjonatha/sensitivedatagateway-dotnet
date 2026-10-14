using System;
using System.Collections.Generic;
using System.Globalization;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Auditing;

/// <summary>
/// What one operation did, in counts and statuses.
/// </summary>
/// <remarks>
/// <para>
/// An audit record is written for every operation, whichever way it ended, and it is written
/// exactly once. It carries no document text, no entity value, no pseudonym and no mapping:
/// an audit trail that could reconstruct a document is a copy of the document kept somewhere
/// nobody is watching.
/// </para>
/// <para>
/// <see cref="DocumentHash"/> and <see cref="PayloadHash"/> are the only way a record ties
/// back to content, and only when the application configured a salt. They are keyed digests,
/// so they correlate two records without letting anyone test a guess.
/// </para>
/// </remarks>
public sealed class AuditRecord
{
    /// <summary>
    /// Gets the identifier of the operation, which ties the record to the log lines it emitted.
    /// </summary>
    public required Guid OperationId { get; init; }

    /// <summary>
    /// Gets the operation the record describes.
    /// </summary>
    public required AuditOperation Operation { get; init; }

    /// <summary>
    /// Gets how the operation ended.
    /// </summary>
    public required AuditOutcome Outcome { get; init; }

    /// <summary>
    /// Gets the bundle the operation ran on.
    /// </summary>
    public required BundleId BundleId { get; init; }

    /// <summary>
    /// Gets the policy that was applied.
    /// </summary>
    public required PolicyId PolicyId { get; init; }

    /// <summary>
    /// Gets the version of the policy that was applied.
    /// </summary>
    public required PolicyVersion PolicyVersion { get; init; }

    /// <summary>
    /// Gets the version of the gateway that ran.
    /// </summary>
    public required string GatewayVersion { get; init; }

    /// <summary>
    /// Gets the moment the operation started.
    /// </summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Gets how long the operation took.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the number of candidates detection reported.
    /// </summary>
    public int EntityCount { get; init; }

    /// <summary>
    /// Gets the number of candidates the anonymizer rewrote.
    /// </summary>
    public int TransformedCount { get; init; }

    /// <summary>
    /// Gets the number of parts and segments that could not be read or scanned.
    /// </summary>
    /// <value>
    /// Zero when the scan was complete. A non-zero count is the reason a clean document can
    /// still be blocked.
    /// </value>
    public int GapCount { get; init; }

    /// <summary>
    /// Gets the checks that did not pass.
    /// </summary>
    public IReadOnlyList<PrivacyCheck> FailedChecks { get; init; } = [];

    /// <summary>
    /// Gets the category of the error that ended the operation, when one did.
    /// </summary>
    public GatewayErrorCategory? ErrorCategory { get; init; }

    /// <summary>
    /// Gets the code of the error that ended the operation, when one did.
    /// </summary>
    /// <value>
    /// A fixed identifier such as <c>extraction.malformed</c>. It is never a message assembled
    /// from a document.
    /// </value>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Gets the keyed digest of the document, when the application configured a salt.
    /// </summary>
    public string? DocumentHash { get; init; }

    /// <summary>
    /// Gets the keyed digest of the payload that was transmitted, when one was and the
    /// application configured a salt.
    /// </summary>
    public string? PayloadHash { get; init; }

    /// <summary>
    /// Returns a description of the record.
    /// </summary>
    /// <returns>
    /// The operation, the outcome and the bundle. It never includes a hash or a count that
    /// would narrow down what the document said.
    /// </returns>
    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Operation} {Outcome} on bundle {BundleId.Value}");
}
