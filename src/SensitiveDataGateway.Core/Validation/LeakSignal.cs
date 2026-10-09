namespace SensitiveDataGateway.Validation;

/// <summary>
/// How the anti-leak scanner recognized a value it should not have found.
/// </summary>
/// <remarks>
/// The signal says what kind of match occurred, never what matched. A report that could name
/// the value would be a copy of the thing the scan exists to keep out.
/// </remarks>
public enum LeakSignal
{
    /// <summary>
    /// Not a signal.
    /// </summary>
    None = 0,

    /// <summary>
    /// The value appears in the output exactly as the detector found it.
    /// </summary>
    Exact = 1,

    /// <summary>
    /// The value appears once case, whitespace, punctuation and diacritics are disregarded.
    /// </summary>
    Normalized = 2,

    /// <summary>
    /// A long enough part of the value appears, so that a multi-word value cannot be smuggled
    /// through one word at a time.
    /// </summary>
    Fragment = 3,

    /// <summary>
    /// The output matches a pattern that is a secret wherever it occurs, independently of
    /// what detection found.
    /// </summary>
    SecretPattern = 4,

    /// <summary>
    /// The output carries a string random enough to be a key, a token or a credential.
    /// </summary>
    HighEntropy = 5,
}
