using System;
using System.Globalization;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Policies;

/// <summary>
/// Why a decision is what it is: which rule matched, and which threshold applied.
/// </summary>
/// <remarks>
/// <para>
/// Every decision carries one. A plan whose entries cannot be explained is a plan nobody can
/// review, and a privacy decision nobody can review is indistinguishable from an accident.
/// </para>
/// <para>
/// A rationale names a rule by its position in the policy, a source, an outcome and two
/// scores. It never names the value the decision was about.
/// </para>
/// </remarks>
public sealed class PolicyRationale
{
    private PolicyRationale(
        PolicyRationaleSource source,
        int? ruleIndex,
        ConfidenceOutcome outcome,
        ConfidenceThresholds thresholds,
        DetectionConfidence confidence)
    {
        Source = source;
        RuleIndex = ruleIndex;
        Outcome = outcome;
        Thresholds = thresholds;
        Confidence = confidence;
    }

    /// <summary>
    /// Gets where the action came from.
    /// </summary>
    public PolicyRationaleSource Source { get; }

    /// <summary>
    /// Gets the position of the matched rule in the policy.
    /// </summary>
    /// <value>
    /// The index, or <see langword="null"/> when no rule matched.
    /// </value>
    public int? RuleIndex { get; }

    /// <summary>
    /// Gets what the thresholds made of the detector's score.
    /// </summary>
    public ConfidenceOutcome Outcome { get; }

    /// <summary>
    /// Gets the thresholds that were applied.
    /// </summary>
    public ConfidenceThresholds Thresholds { get; }

    /// <summary>
    /// Gets the score the detector reported.
    /// </summary>
    public DetectionConfidence Confidence { get; }

    /// <summary>
    /// Creates a rationale for a decision a rule produced.
    /// </summary>
    /// <param name="ruleIndex">The position of the matched rule in the policy.</param>
    /// <param name="outcome">What the thresholds made of the score.</param>
    /// <param name="thresholds">The thresholds that were applied.</param>
    /// <param name="confidence">The score the detector reported.</param>
    /// <returns>The rationale.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="thresholds"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ruleIndex"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="outcome"/> is not a defined value.</exception>
    public static PolicyRationale FromRule(
        int ruleIndex,
        ConfidenceOutcome outcome,
        ConfidenceThresholds thresholds,
        DetectionConfidence confidence)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ruleIndex);

        return Create(PolicyRationaleSource.Rule, ruleIndex, outcome, thresholds, confidence);
    }

    /// <summary>
    /// Creates a rationale for a decision the policy's default action produced.
    /// </summary>
    /// <param name="outcome">What the thresholds made of the score.</param>
    /// <param name="thresholds">The thresholds that were applied.</param>
    /// <param name="confidence">The score the detector reported.</param>
    /// <returns>The rationale.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="thresholds"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="outcome"/> is not a defined value.</exception>
    public static PolicyRationale FromDefaultAction(
        ConfidenceOutcome outcome,
        ConfidenceThresholds thresholds,
        DetectionConfidence confidence) =>
        Create(PolicyRationaleSource.DefaultAction, ruleIndex: null, outcome, thresholds, confidence);

    /// <summary>
    /// Creates a rationale for a decision the policy's unknown-entity action produced.
    /// </summary>
    /// <param name="outcome">What the thresholds made of the score.</param>
    /// <param name="thresholds">The thresholds that were applied.</param>
    /// <param name="confidence">The score the detector reported.</param>
    /// <returns>The rationale.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="thresholds"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="outcome"/> is not a defined value.</exception>
    public static PolicyRationale FromUnknownEntityAction(
        ConfidenceOutcome outcome,
        ConfidenceThresholds thresholds,
        DetectionConfidence confidence) =>
        Create(PolicyRationaleSource.UnknownEntityAction, ruleIndex: null, outcome, thresholds, confidence);

    /// <summary>
    /// Returns a sentence explaining the decision.
    /// </summary>
    /// <returns>
    /// A non-empty description naming the source, the rule position when there is one, the
    /// score and the thresholds. It carries no document content.
    /// </returns>
    public override string ToString() => string.Concat(DescribeSource(), "; ", DescribeScore());

    private static PolicyRationale Create(
        PolicyRationaleSource source,
        int? ruleIndex,
        ConfidenceOutcome outcome,
        ConfidenceThresholds thresholds,
        DetectionConfidence confidence)
    {
        ArgumentNullException.ThrowIfNull(thresholds);

        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentException("The confidence outcome is not a defined value.", nameof(outcome));
        }

        return new PolicyRationale(source, ruleIndex, outcome, thresholds, confidence);
    }

    private string DescribeSource() => Source switch
    {
        PolicyRationaleSource.Rule =>
            $"rule {(RuleIndex ?? 0).ToString(CultureInfo.InvariantCulture)} matched",
        PolicyRationaleSource.DefaultAction =>
            "no rule matched, so the policy default action applied",
        _ =>
            "the policy does not mention the type, so the unknown-entity action applied",
    };

    private string DescribeScore() => $"confidence {Confidence} classified as {Outcome} by {Thresholds}";
}
