using System;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Policies;

/// <summary>
/// The two scores at which a policy starts acting on what a detector reported.
/// </summary>
/// <remarks>
/// <para>
/// The shipped numbers — transform at 0.90, flag at 0.70 — are a starting point for a strict
/// policy and not a constant. They are configurable per entity type because the right
/// threshold for an API key and for an organization name are not the same number: acting on
/// weak evidence costs a redacted word in one case and leaks a credential in the other.
/// </para>
/// <para>
/// Lowering a threshold makes the gateway act more often, which is the conservative
/// direction. Raising one makes it act less often, and everything it stops acting on stays in
/// the text.
/// </para>
/// </remarks>
public sealed class ConfidenceThresholds
{
    private ConfidenceThresholds(DetectionConfidence transform, DetectionConfidence flag)
    {
        Transform = transform;
        Flag = flag;
    }

    /// <summary>
    /// Gets the thresholds a policy applies to an entity type it states nothing else about.
    /// </summary>
    /// <value>Transform at 0.90, flag at 0.70.</value>
    public static ConfidenceThresholds Default { get; } = Create(0.90, 0.70);

    /// <summary>
    /// Gets the score at or above which the decided action applies.
    /// </summary>
    public DetectionConfidence Transform { get; }

    /// <summary>
    /// Gets the score at or above which the decided action applies and a warning is recorded.
    /// </summary>
    public DetectionConfidence Flag { get; }

    /// <summary>
    /// Creates a pair of thresholds.
    /// </summary>
    /// <param name="transform">The score at or above which the decided action applies.</param>
    /// <param name="flag">The score at or above which the action applies and is flagged.</param>
    /// <returns>The thresholds.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A score is outside the range 0.0 to 1.0, or <paramref name="flag"/> is higher than
    /// <paramref name="transform"/>.
    /// </exception>
    public static ConfidenceThresholds Create(double transform, double flag) =>
        Create(DetectionConfidence.Create(transform), DetectionConfidence.Create(flag));

    /// <summary>
    /// Creates a pair of thresholds.
    /// </summary>
    /// <param name="transform">The score at or above which the decided action applies.</param>
    /// <param name="flag">The score at or above which the action applies and is flagged.</param>
    /// <returns>The thresholds.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="flag"/> is higher than <paramref name="transform"/>.
    /// </exception>
    /// <remarks>
    /// A flagging threshold above the transformation threshold describes a policy that
    /// records a warning about values it never considered uncertain. It is rejected here
    /// rather than reordered, because the reordering a reader expects and the one the code
    /// would pick are not reliably the same.
    /// </remarks>
    public static ConfidenceThresholds Create(DetectionConfidence transform, DetectionConfidence flag)
    {
        if (flag > transform)
        {
            throw new ArgumentOutOfRangeException(
                nameof(flag),
                "The flagging threshold cannot be higher than the transformation threshold.");
        }

        return new ConfidenceThresholds(transform, flag);
    }

    /// <summary>
    /// Decides what a score is worth under these thresholds.
    /// </summary>
    /// <param name="confidence">The score a detector reported.</param>
    /// <returns>The outcome the policy engine acts on.</returns>
    public ConfidenceOutcome Classify(DetectionConfidence confidence)
    {
        if (confidence >= Transform)
        {
            return ConfidenceOutcome.Transform;
        }

        return confidence >= Flag ? ConfidenceOutcome.Flag : ConfidenceOutcome.Unresolved;
    }

    /// <summary>
    /// Returns a description of the thresholds.
    /// </summary>
    /// <returns>The two scores. They carry no document content.</returns>
    public override string ToString() => $"transform >= {Transform}, flag >= {Flag}";
}
