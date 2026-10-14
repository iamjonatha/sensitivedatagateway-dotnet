using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway;

/// <summary>
/// What a document would do, without doing it.
/// </summary>
/// <remarks>
/// <para>
/// An inspection answers "what is in here, and would it pass?" and nothing else. It transmits
/// nothing, and it holds counts, statuses and versions: no text, no entity value, no
/// pseudonym, no offset. An offset is omitted on purpose, because an offset plus a copy of
/// the document a dashboard already has is the value itself.
/// </para>
/// <para>
/// This is the type a reviewer looks at before turning a policy on, and the type a dry run
/// over a corpus aggregates.
/// </para>
/// </remarks>
public sealed class InspectionReport
{
    /// <summary>
    /// Gets the bundle that was inspected.
    /// </summary>
    public required BundleId BundleId { get; init; }

    /// <summary>
    /// Gets the moment the inspection ran.
    /// </summary>
    public required DateTimeOffset InspectedAt { get; init; }

    /// <summary>
    /// Gets the policy, detector and extractor versions the inspection ran with.
    /// </summary>
    /// <remarks>
    /// Two inspections that disagree are only comparable when they agree here, which is why
    /// the versions travel with the counts instead of being looked up afterwards.
    /// </remarks>
    public required SanitizationProvenance Provenance { get; init; }

    /// <summary>
    /// Gets a value indicating whether the same document, sanitized under the same policy,
    /// would be allowed to leave.
    /// </summary>
    public required bool WouldBeAllowed { get; init; }

    /// <summary>
    /// Gets the status the anonymizer reached, or <see langword="null"/> when the policy
    /// blocked the document and anonymization never ran.
    /// </summary>
    public required SanitizationStatus? Status { get; init; }

    /// <summary>
    /// Gets the number of segments that were scanned.
    /// </summary>
    public int SegmentCount { get; init; }

    /// <summary>
    /// Gets the number of characters that were scanned.
    /// </summary>
    public long TotalCharacters { get; init; }

    /// <summary>
    /// Gets the number of parts that could not be read.
    /// </summary>
    public int ExtractionFailureCount { get; init; }

    /// <summary>
    /// Gets the number of segments a detector did not finish.
    /// </summary>
    public int DetectionFailureCount { get; init; }

    /// <summary>
    /// Gets how many candidates of each type were found.
    /// </summary>
    public IReadOnlyDictionary<EntityType, int> EntityCounts { get; init; } =
        ReadOnlyDictionary<EntityType, int>.Empty;

    /// <summary>
    /// Gets how many candidates the policy assigned to each action.
    /// </summary>
    public IReadOnlyDictionary<TransformationAction, int> ActionCounts { get; init; } =
        ReadOnlyDictionary<TransformationAction, int>.Empty;

    /// <summary>
    /// Gets the number of candidates no rule covered.
    /// </summary>
    /// <value>
    /// Zero under a policy with a default rule. A non-zero count means the policy has a hole,
    /// and a hole is why the document would be blocked.
    /// </value>
    public int UnresolvedCount { get; init; }

    /// <summary>
    /// Gets the reasons the policy would block, without naming what triggered them.
    /// </summary>
    public IReadOnlyList<PolicyViolationReason> BlockingReasons { get; init; } = [];

    /// <summary>
    /// Gets the validation checks that would not pass.
    /// </summary>
    public IReadOnlyList<PrivacyCheck> FailedChecks { get; init; } = [];

    /// <summary>
    /// Returns a description of the report.
    /// </summary>
    /// <returns>
    /// The bundle and whether it would pass. It never includes what was found.
    /// </returns>
    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Bundle {BundleId.Value}: {(WouldBeAllowed ? "would be allowed" : "would be blocked")}");
}
