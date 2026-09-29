using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// Finds candidate sensitive values in extracted text.
/// </summary>
/// <remarks>
/// <para>
/// A detector reports what it believes it found and stops. It <b>must not</b> decide what
/// happens to a finding: masking, redacting and blocking belong to the policy, and a detector
/// that transforms anything has taken a decision the policy was supposed to make.
/// </para>
/// <para>
/// A detector <b>must not</b> put matched text anywhere — not in an entity, not in evidence,
/// not in a log, not in an exception message. <see cref="DetectionEvidence"/> exists so that
/// a match can be explained without being quoted.
/// </para>
/// <para>
/// A deterministic detector <b>must not</b> perform I/O. It runs in parallel with its peers
/// over the whole document, and a detector that blocks on a file or a socket turns a fast
/// stage into the slowest one.
/// </para>
/// <para>
/// A detector <b>must not</b> call the network, and a detector backed by a model must run
/// that model locally. Sending the document somewhere to find out whether it is safe to send
/// the document somewhere is the failure this library exists to prevent.
/// </para>
/// </remarks>
public interface ISensitiveDataDetector
{
    /// <summary>
    /// Gets the identity and version of this detector, for the audit record.
    /// </summary>
    DetectorVersion Version { get; }

    /// <summary>
    /// Finds candidates in an extracted document.
    /// </summary>
    /// <param name="document">The extracted text.</param>
    /// <param name="cancellationToken">A token that cancels the scan.</param>
    /// <returns>
    /// The candidates. An empty list is a statement that the detector scanned the document
    /// and found nothing; a detector that could not scan throws instead.
    /// </returns>
    /// <exception cref="DetectionException">
    /// The scan did not complete. The host converts this into a
    /// <see cref="DetectionFailure"/>, which is recorded as a gap rather than as a clean
    /// result.
    /// </exception>
    ValueTask<IReadOnlyList<SensitiveEntity>> DetectAsync(
        ExtractedDocument document,
        CancellationToken cancellationToken);
}
