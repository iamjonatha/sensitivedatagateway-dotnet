using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SensitiveDataGateway.Documents;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// Runs a set of detectors over a document and reconciles what they report.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline does not decide what happens to anything it finds. It produces the complete,
/// deduplicated list of candidates and an honest record of which detectors did not finish;
/// the policy engine decides, and refuses to decide at all when the record shows a gap.
/// </para>
/// <para>
/// Detectors run concurrently and are expected to be pure functions of the document. A
/// detector that reaches the network, writes to disk or depends on another detector's result
/// violates the contract, and the stage gives it no way to express such a dependency.
/// </para>
/// <para>
/// A detector that throws, exceeds its time budget or floods the stage with candidates does
/// not fail the operation: it is recorded as a gap and its findings are discarded. The
/// document then carries the evidence that part of it was never examined, which every later
/// stage treats as a reason to refuse rather than to proceed.
/// </para>
/// </remarks>
public sealed class DetectionPipeline
{
    private readonly ISensitiveDataDetector[] _detectors;
    private readonly DetectionPipelineOptions _options;

    /// <summary>
    /// Creates a pipeline that runs the given detectors under the default bounds.
    /// </summary>
    /// <param name="detectors">The detectors to run.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="detectors"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="detectors"/> is empty or contains a <see langword="null"/> element.
    /// </exception>
    public DetectionPipeline(IEnumerable<ISensitiveDataDetector> detectors)
        : this(detectors, DetectionPipelineOptions.Default)
    {
    }

    /// <summary>
    /// Creates a pipeline that runs the given detectors under the given bounds.
    /// </summary>
    /// <param name="detectors">The detectors to run.</param>
    /// <param name="options">The bounds to run them under.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="detectors"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="detectors"/> is empty or contains a <see langword="null"/> element.
    /// </exception>
    public DetectionPipeline(
        IEnumerable<ISensitiveDataDetector> detectors,
        DetectionPipelineOptions options)
    {
        ArgumentNullException.ThrowIfNull(detectors);
        ArgumentNullException.ThrowIfNull(options);

        _detectors = [.. detectors];
        _options = options;

        if (_detectors.Length == 0)
        {
            throw new ArgumentException(
                "A detection pipeline with no detectors would report every document as clean.",
                nameof(detectors));
        }

        foreach (var detector in _detectors)
        {
            if (detector is null)
            {
                throw new ArgumentException(
                    "A detector is null.",
                    nameof(detectors));
            }
        }
    }

    /// <summary>
    /// Runs every detector over the document and reconciles the result.
    /// </summary>
    /// <param name="document">The extracted document to examine.</param>
    /// <param name="cancellationToken">The token that cancels the whole stage.</param>
    /// <returns>The document and everything the detectors claimed about it.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="document"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled.
    /// </exception>
    public async Task<DetectedDocument> DetectAsync(
        ExtractedDocument document,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        var running = new Task<DetectorOutcome>[_detectors.Length];

        for (var index = 0; index < _detectors.Length; index++)
        {
            running[index] = RunAsync(_detectors[index], document, cancellationToken);
        }

        var outcomes = await Task.WhenAll(running).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        var candidates = new List<SensitiveEntity>();
        var versions = new List<DetectorVersion>(outcomes.Length);
        var failures = new List<DetectionFailure>();

        foreach (var outcome in outcomes)
        {
            versions.Add(outcome.Version);

            if (outcome.Failure is { } failure)
            {
                failures.Add(failure);
            }
            else
            {
                candidates.AddRange(outcome.Entities);
            }
        }

        var entities = EntityReconciler.Reconcile(document.Segments, candidates, cancellationToken);

        return DetectedDocument.Create(document, entities, versions, failures);
    }

    private async Task<DetectorOutcome> RunAsync(
        ISensitiveDataDetector detector,
        ExtractedDocument document,
        CancellationToken cancellationToken)
    {
        var version = detector.Version;

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        budget.CancelAfter(_options.DetectorTimeout);

        try
        {
            var entities = await Task
                .Run(
                    async () => await detector.DetectAsync(document, budget.Token).ConfigureAwait(false),
                    budget.Token)
                .ConfigureAwait(false);

            return Accept(version, entities);
        }
        catch (DetectionException exception)
        {
            return Gap(version, DetectionErrors.ReasonFor(exception.Error));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Gap(version, DetectionFailureReason.TimedOut);
        }
        catch (OperationCanceledException)
        {
            return Gap(version, DetectionFailureReason.Cancelled);
        }
#pragma warning disable CA1031 // A detector is third-party code; its failure is a gap, not a crash.
        catch (Exception)
#pragma warning restore CA1031
        {
            return Gap(version, DetectionFailureReason.DetectorFailed);
        }
    }

    private DetectorOutcome Accept(
        DetectorVersion version,
        IReadOnlyList<SensitiveEntity>? entities)
    {
        if (entities is null)
        {
            return Gap(version, DetectionFailureReason.DetectorFailed);
        }

        if (entities.Count > _options.MaxCandidatesPerDetector)
        {
            return Gap(version, DetectionFailureReason.LimitExceeded);
        }

        return new DetectorOutcome(version, entities, Failure: null);
    }

    private static DetectorOutcome Gap(DetectorVersion version, DetectionFailureReason reason) =>
        new(version, [], DetectionFailure.Create(version.DetectorId, reason));

    private readonly record struct DetectorOutcome(
        DetectorVersion Version,
        IReadOnlyList<SensitiveEntity> Entities,
        DetectionFailure? Failure);
}
