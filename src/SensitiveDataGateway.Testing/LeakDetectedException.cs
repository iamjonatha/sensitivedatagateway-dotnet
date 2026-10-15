using System;
using System.Collections.Generic;

namespace SensitiveDataGateway.Testing;

/// <summary>
/// Thrown when a sensitive value survived into somewhere it must never reach.
/// </summary>
/// <remarks>
/// Neither the message nor any property repeats the value that leaked. See
/// <see cref="Leak"/> for why.
/// </remarks>
public sealed class LeakDetectedException : Exception
{
    private static readonly Leak[] s_noLeaks = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="LeakDetectedException"/> class.
    /// </summary>
    public LeakDetectedException()
        : base("A sensitive value survived sanitization.") => Leaks = s_noLeaks;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeakDetectedException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the failure.</param>
    public LeakDetectedException(string message)
        : base(message) => Leaks = s_noLeaks;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeakDetectedException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the failure.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public LeakDetectedException(string message, Exception innerException)
        : base(message, innerException) => Leaks = s_noLeaks;

    internal LeakDetectedException(IReadOnlyList<Leak> leaks)
        : base(Describe(leaks)) => Leaks = leaks;

    /// <summary>
    /// Gets the leaks that were found.
    /// </summary>
    public IReadOnlyList<Leak> Leaks { get; }

    private static string Describe(IReadOnlyList<Leak> leaks)
    {
        var descriptions = new string[leaks.Count];
        for (var i = 0; i < leaks.Count; i++)
        {
            descriptions[i] = leaks[i].ToString();
        }

        return $"{leaks.Count} sensitive value(s) survived sanitization: "
            + string.Join("; ", descriptions)
            + ". The values themselves are deliberately not reported.";
    }
}
