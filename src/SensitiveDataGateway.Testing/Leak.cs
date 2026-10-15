namespace SensitiveDataGateway.Testing;

/// <summary>
/// One sensitive value found where it should not have been.
/// </summary>
/// <remarks>
/// A leak report never repeats the value that leaked. A test run that printed the value
/// would move it into a build log, which is the problem this library exists to prevent, so
/// a leak is described by its position and its length and nothing else. The test author
/// knows which sequence of values was passed to the assertion, and
/// <see cref="ValueIndex"/> points into it.
/// </remarks>
public sealed class Leak
{
    internal Leak(string location, int valueIndex, int valueLength)
    {
        Location = location;
        ValueIndex = valueIndex;
        ValueLength = valueLength;
    }

    /// <summary>
    /// Gets a description of where the value was found, such as the segment or the line.
    /// </summary>
    public string Location { get; }

    /// <summary>
    /// Gets the position of the value in the sequence that was passed to the assertion.
    /// </summary>
    public int ValueIndex { get; }

    /// <summary>
    /// Gets the length of the value in characters.
    /// </summary>
    public int ValueLength { get; }

    /// <summary>
    /// Returns a description that does not repeat the value.
    /// </summary>
    /// <returns>The index, the length and the location.</returns>
    public override string ToString() =>
        $"value #{ValueIndex} ({ValueLength} characters) survived in {Location}";
}
