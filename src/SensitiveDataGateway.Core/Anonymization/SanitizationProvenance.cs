using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// Everything needed to say how a sanitized document came to look the way it does.
/// </summary>
/// <remarks>
/// Provenance is what turns "the document was sanitized" into a claim that can be checked a
/// year later. Without the policy version, the detector versions and the version of the
/// library itself, a stored result only records that some software once approved something.
/// </remarks>
public sealed class SanitizationProvenance
{
    /// <summary>
    /// The greatest number of characters in a gateway version string.
    /// </summary>
    public const int MaxGatewayVersionLength = 64;

    private SanitizationProvenance(
        PolicyId policyId,
        PolicyVersion policyVersion,
        IReadOnlyList<DetectorVersion> detectors,
        IReadOnlyList<ExtractorVersion> extractors,
        string gatewayVersion,
        DateTimeOffset sanitizedAt)
    {
        PolicyId = policyId;
        PolicyVersion = policyVersion;
        Detectors = detectors;
        Extractors = extractors;
        GatewayVersion = gatewayVersion;
        SanitizedAt = sanitizedAt;
    }

    /// <summary>
    /// Gets the policy that was applied.
    /// </summary>
    public PolicyId PolicyId { get; }

    /// <summary>
    /// Gets the version of the policy that was applied.
    /// </summary>
    /// <remarks>
    /// Policies change. A result that records only the policy name cannot be reproduced once
    /// the rule that produced it has been edited.
    /// </remarks>
    public PolicyVersion PolicyVersion { get; }

    /// <summary>
    /// Gets the detectors that ran, with their versions.
    /// </summary>
    public IReadOnlyList<DetectorVersion> Detectors { get; }

    /// <summary>
    /// Gets the extractors that ran, with their versions.
    /// </summary>
    public IReadOnlyList<ExtractorVersion> Extractors { get; }

    /// <summary>
    /// Gets the version of the gateway that produced the result.
    /// </summary>
    public string GatewayVersion { get; }

    /// <summary>
    /// Gets when the result was produced.
    /// </summary>
    public DateTimeOffset SanitizedAt { get; }

    /// <summary>
    /// Creates a provenance record.
    /// </summary>
    /// <param name="policyId">The policy that was applied.</param>
    /// <param name="policyVersion">The version of that policy.</param>
    /// <param name="detectors">The detectors that ran. The sequence is copied.</param>
    /// <param name="extractors">The extractors that ran. The sequence is copied.</param>
    /// <param name="gatewayVersion">The version of the gateway.</param>
    /// <param name="sanitizedAt">When the result was produced.</param>
    /// <returns>The provenance record.</returns>
    /// <exception cref="ArgumentNullException">A sequence or the version string is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// An identifier is the default value, a sequence contains a default value, or the version
    /// string is empty, too long, or has a control character.
    /// </exception>
    public static SanitizationProvenance Create(
        PolicyId policyId,
        PolicyVersion policyVersion,
        IEnumerable<DetectorVersion> detectors,
        IEnumerable<ExtractorVersion> extractors,
        string gatewayVersion,
        DateTimeOffset sanitizedAt)
    {
        if (policyId.IsDefault)
        {
            throw new ArgumentException("The policy identifier carries no value.", nameof(policyId));
        }

        if (policyVersion.IsDefault)
        {
            throw new ArgumentException("The policy version carries no value.", nameof(policyVersion));
        }

        ArgumentNullException.ThrowIfNull(detectors);
        ArgumentNullException.ThrowIfNull(extractors);

        var copiedDetectors = new List<DetectorVersion>();

        foreach (var detector in detectors)
        {
            if (detector.IsDefault)
            {
                throw new ArgumentException("A detector version carries no value.", nameof(detectors));
            }

            copiedDetectors.Add(detector);
        }

        var copiedExtractors = new List<ExtractorVersion>();

        foreach (var extractor in extractors)
        {
            if (extractor.IsDefault)
            {
                throw new ArgumentException("An extractor version carries no value.", nameof(extractors));
            }

            copiedExtractors.Add(extractor);
        }

        return new SanitizationProvenance(
            policyId,
            policyVersion,
            new ReadOnlyCollection<DetectorVersion>(copiedDetectors),
            new ReadOnlyCollection<ExtractorVersion>(copiedExtractors),
            Text.Require(gatewayVersion, MaxGatewayVersionLength, nameof(gatewayVersion)),
            sanitizedAt);
    }

    /// <summary>
    /// Returns a description of the provenance.
    /// </summary>
    /// <returns>The policy, its version and the gateway version. It carries no document content.</returns>
    public override string ToString() => $"{PolicyId}@{PolicyVersion} (gateway {GatewayVersion})";
}
