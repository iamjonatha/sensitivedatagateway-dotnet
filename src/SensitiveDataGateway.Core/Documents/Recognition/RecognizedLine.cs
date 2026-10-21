using System;
using System.Globalization;

namespace SensitiveDataGateway.Documents.Recognition;

/// <summary>
/// One line of text a recognizer read out of an image, with how sure it is of it.
/// </summary>
/// <remarks>
/// <para>
/// A line is the unit because it is the unit every engine agrees on, and because a value and
/// the label beside it are usually on the same line. Splitting into words would hand a
/// detector "Rossi" with no indication that the word before it was "beneficiary".
/// </para>
/// <para>
/// The confidence is the reason this type exists rather than a plain string. Recognition is
/// the one extraction path that can be confidently wrong, and a pipeline that cannot see the
/// engine's own doubt has no way to fail closed on it.
/// </para>
/// </remarks>
public sealed class RecognizedLine
{
    private RecognizedLine(string text, double confidence, int ordinal)
    {
        Text = text;
        Confidence = confidence;
        Ordinal = ordinal;
    }

    /// <summary>
    /// Gets the text of the line.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets how sure the recognizer is of the line, from 0 to 1 inclusive.
    /// </summary>
    /// <remarks>
    /// An engine that reports no confidence reports 1, and says so in its documentation. A
    /// recognizer that invents a number it does not have is worse than one that declines to
    /// guess, because the pipeline would then enforce a threshold against noise.
    /// </remarks>
    public double Confidence { get; }

    /// <summary>
    /// Gets the one-based position of the line in reading order.
    /// </summary>
    public int Ordinal { get; }

    /// <summary>
    /// Creates a recognized line.
    /// </summary>
    /// <param name="text">The text of the line. It may be empty.</param>
    /// <param name="confidence">How sure the recognizer is, from 0 to 1 inclusive.</param>
    /// <param name="ordinal">The one-based position of the line in reading order.</param>
    /// <returns>The line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="confidence"/> is outside 0 to 1 or is not a number, or
    /// <paramref name="ordinal"/> is not positive.
    /// </exception>
    public static RecognizedLine Create(string text, double confidence, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ordinal);

        if (double.IsNaN(confidence) || confidence < 0 || confidence > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidence),
                "A recognition confidence runs from 0 to 1 inclusive.");
        }

        return new RecognizedLine(text, confidence, ordinal);
    }

    /// <summary>
    /// Returns a description of the line.
    /// </summary>
    /// <returns>
    /// The position and the confidence. It never includes the text, which is content read out
    /// of a document nobody has scanned yet.
    /// </returns>
    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"line {Ordinal} ({Text.Length} characters, confidence {Confidence:F2})");
}
