using System;

namespace SensitiveDataGateway.Documents.Recognition;

/// <summary>
/// The bounds a <see cref="RecognizingExtractor"/> applies to what a recognizer returns.
/// </summary>
/// <remarks>
/// Both bounds exist because a recognizer is told what to read by whoever sent the document.
/// A page of noise can be reported as ten thousand unsure lines, and an engine asked to read
/// an image of static will happily produce characters; neither is text, and neither should be
/// allowed to pass as a scanned part.
/// </remarks>
public sealed class RecognitionOptions
{
    /// <summary>
    /// The lowest threshold that can be configured.
    /// </summary>
    /// <remarks>
    /// Zero is excluded. A threshold of zero accepts whatever the engine produces, which is
    /// the configuration that turns a guarantee into a warning.
    /// </remarks>
    public const double MinimumThreshold = 0.05;

    private RecognitionOptions(double minimumConfidence, int maxLines)
    {
        MinimumConfidence = minimumConfidence;
        MaxLines = maxLines;
    }

    /// <summary>
    /// Gets the confidence every line must reach for the part to be considered read.
    /// </summary>
    /// <value>The threshold, from <see cref="MinimumThreshold"/> to 1 inclusive. The default is 0.80.</value>
    /// <remarks>
    /// It applies to the lowest line, not to the average, and a part that falls below it fails
    /// rather than losing the line. A page is read whole or it is not read.
    /// </remarks>
    public double MinimumConfidence { get; }

    /// <summary>
    /// Gets the greatest number of lines a recognizer may return for one part.
    /// </summary>
    /// <value>The bound. The default is 50,000.</value>
    public int MaxLines { get; }

    /// <summary>
    /// Gets the default bounds.
    /// </summary>
    public static RecognitionOptions Default { get; } = new(minimumConfidence: 0.80, maxLines: 50_000);

    /// <summary>
    /// Returns these bounds with a different confidence threshold.
    /// </summary>
    /// <param name="minimumConfidence">
    /// The confidence every line must reach, from <see cref="MinimumThreshold"/> to 1
    /// inclusive.
    /// </param>
    /// <returns>The new bounds.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="minimumConfidence"/> is below <see cref="MinimumThreshold"/>, above 1,
    /// or is not a number.
    /// </exception>
    public RecognitionOptions WithMinimumConfidence(double minimumConfidence)
    {
        if (double.IsNaN(minimumConfidence) || minimumConfidence < MinimumThreshold || minimumConfidence > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumConfidence),
                $"A confidence threshold runs from {MinimumThreshold} to 1 inclusive.");
        }

        return new RecognitionOptions(minimumConfidence, MaxLines);
    }

    /// <summary>
    /// Returns these bounds with a different line bound.
    /// </summary>
    /// <param name="maxLines">The greatest number of lines a recognizer may return for one part.</param>
    /// <returns>The new bounds.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLines"/> is not positive.</exception>
    public RecognitionOptions WithMaxLines(int maxLines)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLines);

        return new RecognitionOptions(MinimumConfidence, maxLines);
    }
}
