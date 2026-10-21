using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace SensitiveDataGateway.Documents.Recognition;

/// <summary>
/// What a recognizer read out of one part.
/// </summary>
/// <remarks>
/// <para>
/// The result is deliberately thin: lines, in order, each with a confidence. A recognizer
/// returns what it read and nothing about what it means, for the same reason an extractor
/// does — the stage that is allowed to classify comes later.
/// </para>
/// <para>
/// A result with no lines is a statement that the part carries no readable text. It is a
/// legitimate answer for a blank page and a dangerous one for a photograph of a document, so
/// the extractor that consumes it decides what an empty result is worth rather than treating
/// it as success.
/// </para>
/// </remarks>
public sealed class RecognizedText
{
    private RecognizedText(IReadOnlyList<RecognizedLine> lines) => Lines = lines;

    /// <summary>
    /// Gets the lines, in reading order.
    /// </summary>
    public IReadOnlyList<RecognizedLine> Lines { get; }

    /// <summary>
    /// Gets a value indicating whether the recognizer read nothing at all.
    /// </summary>
    public bool IsEmpty => Lines.Count == 0;

    /// <summary>
    /// Gets the lowest confidence of any line.
    /// </summary>
    /// <value>The lowest confidence, or 1 when there are no lines.</value>
    /// <remarks>
    /// The lowest, not the mean. A page of forty confident lines and one the engine could not
    /// read is a page the gateway has not read, and averaging is how that fact disappears.
    /// </remarks>
    public double LowestConfidence
    {
        get
        {
            var lowest = 1d;

            foreach (var line in Lines)
            {
                if (line.Confidence < lowest)
                {
                    lowest = line.Confidence;
                }
            }

            return lowest;
        }
    }

    /// <summary>
    /// Creates a recognition result.
    /// </summary>
    /// <param name="lines">The lines, in reading order. The sequence is copied.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lines"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="lines"/> contains <see langword="null"/>, or the ordinals are not 1, 2,
    /// 3 and so on.
    /// </exception>
    /// <remarks>
    /// The ordinals are checked because reading order is the only structure the result
    /// carries. A gap means a line was dropped somewhere, and a dropped line is a piece of a
    /// document that nothing will ever scan.
    /// </remarks>
    public static RecognizedText Create(IEnumerable<RecognizedLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var copied = new List<RecognizedLine>();

        foreach (var line in lines)
        {
            if (line is null)
            {
                throw new ArgumentException("A line list cannot contain a null line.", nameof(lines));
            }

            if (line.Ordinal != copied.Count + 1)
            {
                throw new ArgumentException("The lines must be numbered 1, 2, 3 in reading order.", nameof(lines));
            }

            copied.Add(line);
        }

        return new RecognizedText(new ReadOnlyCollection<RecognizedLine>(copied));
    }

    /// <summary>
    /// Returns an empty recognition result.
    /// </summary>
    /// <returns>A result with no lines.</returns>
    public static RecognizedText Empty() => new(Array.Empty<RecognizedLine>());

    /// <summary>
    /// Returns a description of the result.
    /// </summary>
    /// <returns>
    /// The number of lines and the lowest confidence. It never includes the text.
    /// </returns>
    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Lines.Count} lines (lowest confidence {LowestConfidence:F2})");
}
