using System;
using System.Collections.Generic;
using System.Text;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Testing;

namespace SensitiveDataGateway.Benchmarks;

/// <summary>
/// The documents the benchmarks measure against.
/// </summary>
/// <remarks>
/// <para>
/// Three shapes, because the cost of this pipeline is driven by how much of the text looks
/// like something rather than by how much text there is. Prose with nothing in it exercises
/// the scanners and finds nothing; the realistic shape is the reference case; the dense shape
/// is the worst case the published numbers are allowed to claim.
/// </para>
/// <para>
/// Every value comes from <see cref="SyntheticCorpus"/>. Nothing here is real, and nothing
/// here is generated at random: a benchmark whose input changes between runs is a benchmark
/// whose numbers cannot be compared between runs.
/// </para>
/// </remarks>
public static class Corpus
{
    private const string Filler =
        "The claim was received by the office and logged against the file on the same day. " +
        "A handler reviewed the attached material and recorded the outcome in the register. ";

    /// <summary>
    /// Builds a bundle of roughly the requested size, in the requested shape.
    /// </summary>
    /// <param name="shape">Which kind of text the body holds.</param>
    /// <param name="characters">Roughly how many characters the body holds.</param>
    /// <returns>A single-part bundle.</returns>
    public static DocumentBundle Bundle(CorpusShape shape, int characters) =>
        TestBundle.FromText(Text(shape, characters));

    /// <summary>
    /// Builds the body text of a bundle.
    /// </summary>
    /// <param name="shape">Which kind of text to build.</param>
    /// <param name="characters">Roughly how many characters to build.</param>
    /// <returns>The text.</returns>
    public static string Text(CorpusShape shape, int characters) => shape switch
    {
        CorpusShape.Clean => Repeat([Filler], characters, separator: string.Empty),
        CorpusShape.Realistic => Repeat(Bodies(), characters, separator: "\n\n"),
        CorpusShape.Dense => Repeat(Lines(), characters, separator: "\n"),
        _ => throw new ArgumentOutOfRangeException(nameof(shape)),
    };

    private static List<string> Bodies()
    {
        var bodies = new List<string>();

        foreach (var document in SyntheticCorpus.All())
        {
            if (!document.IsPromptInjection)
            {
                bodies.Add(document.Body);
            }
        }

        return bodies;
    }

    /// <summary>
    /// Builds the densest text the detectors can be fed: one annotated value per short line.
    /// </summary>
    /// <returns>The lines.</returns>
    private static List<string> Lines()
    {
        var lines = new List<string>();

        foreach (var value in SyntheticCorpus.Values())
        {
            lines.Add($"Reference: {value}");
        }

        return lines;
    }

    private static string Repeat(List<string> pieces, int characters, string separator)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(characters);

        var builder = new StringBuilder(characters + 256);
        var index = 0;

        while (builder.Length < characters)
        {
            if (builder.Length > 0 && separator.Length > 0)
            {
                builder.Append(separator);
            }

            builder.Append(pieces[index % pieces.Count]);
            index++;
        }

        return builder.ToString();
    }
}
