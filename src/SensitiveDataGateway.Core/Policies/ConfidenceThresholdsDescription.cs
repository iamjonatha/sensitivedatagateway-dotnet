using System;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Policies;

/// <summary>
/// The form of <see cref="ConfidenceThresholds"/> that configuration binds to.
/// </summary>
/// <remarks>
/// Every member is optional and falls back to the shipped default, so a configuration file
/// states only the numbers it wants to change.
/// </remarks>
public sealed class ConfidenceThresholdsDescription
{
    /// <summary>
    /// Gets or sets the score at or above which the decided action applies.
    /// </summary>
    public double? Transform { get; set; }

    /// <summary>
    /// Gets or sets the score at or above which the action applies and is flagged.
    /// </summary>
    public double? Flag { get; set; }

    /// <summary>
    /// Describes a pair of thresholds.
    /// </summary>
    /// <param name="thresholds">The thresholds.</param>
    /// <returns>The description.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="thresholds"/> is <see langword="null"/>.</exception>
    public static ConfidenceThresholdsDescription FromThresholds(ConfidenceThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);

        return new ConfidenceThresholdsDescription
        {
            Transform = thresholds.Transform.Value,
            Flag = thresholds.Flag.Value,
        };
    }

    /// <summary>
    /// Produces the thresholds this description denotes.
    /// </summary>
    /// <returns>The thresholds.</returns>
    /// <exception cref="PolicyConfigurationException">
    /// A score is outside the range 0.0 to 1.0, or the flagging threshold is higher than the
    /// transformation threshold.
    /// </exception>
    public ConfidenceThresholds ToThresholds()
    {
        var transform = Score(Transform, ConfidenceThresholds.Default.Transform);
        var flag = Score(Flag, ConfidenceThresholds.Default.Flag);

        if (flag > transform)
        {
            throw PolicyConfigurationFailure.Invalid(
                "policy.flag_threshold_above_transform_threshold",
                "The flagging threshold cannot be higher than the transformation threshold.");
        }

        return ConfidenceThresholds.Create(transform, flag);
    }

    private static DetectionConfidence Score(double? value, DetectionConfidence fallback)
    {
        if (value is not { } score)
        {
            return fallback;
        }

        if (double.IsNaN(score) || score < 0.0 || score > 1.0)
        {
            throw PolicyConfigurationFailure.Invalid(
                "policy.threshold_out_of_range",
                "A confidence threshold is a number between 0.0 and 1.0 inclusive.");
        }

        return DetectionConfidence.Create(score);
    }
}
