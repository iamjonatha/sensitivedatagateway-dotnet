using System;
using System.Collections.Generic;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway.Testing;

/// <summary>
/// Assertions that a set of sensitive values did not survive into a payload, a provider
/// call, a log or an audit record.
/// </summary>
/// <remarks>
/// <para>
/// This is a blunt instrument on purpose. It is a substring search over two forms of the
/// text, so it answers one question only: is the value still there, literally. It is not
/// the scan the validator performs before a payload is released, it does not know what a
/// pseudonym marker is, and it will not notice a value that was encoded, split across
/// segments or re-spelled. A test that passes here has not proven the gateway correct; a
/// test that fails here has proven it broken.
/// </para>
/// <para>
/// Failures throw <see cref="LeakDetectedException"/>, which never repeats the value.
/// </para>
/// </remarks>
public static class LeakAssert
{
    /// <summary>
    /// The shortest value the assertions will search for.
    /// </summary>
    /// <remarks>
    /// A two-character search term is present in almost any prose, so accepting one would
    /// turn this assertion into a generator of false accusations. Values shorter than this
    /// are rejected rather than skipped, because a silently skipped value is a test that
    /// claims to check something it never checked.
    /// </remarks>
    public const int MinimumValueLength = 4;

    /// <summary>
    /// Asserts that none of the values appears in a payload.
    /// </summary>
    /// <param name="payload">The payload the gateway released.</param>
    /// <param name="values">The values that must not appear.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A value is empty, white space, or shorter than <see cref="MinimumValueLength"/>.</exception>
    /// <exception cref="LeakDetectedException">A value survived.</exception>
    public static void NoneOf(ValidatedPayload payload, params string[] values) =>
        NoneOf(payload, (IEnumerable<string>)values);

    /// <summary>
    /// Asserts that none of the values appears in a payload.
    /// </summary>
    /// <param name="payload">The payload the gateway released.</param>
    /// <param name="values">The values that must not appear.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A value is empty, white space, or shorter than <see cref="MinimumValueLength"/>.</exception>
    /// <exception cref="LeakDetectedException">A value survived.</exception>
    public static void NoneOf(ValidatedPayload payload, IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(payload);
        Scan(Fragments(payload, prefix: string.Empty), values);
    }

    /// <summary>
    /// Asserts that none of the values reached a provider, across every call it received.
    /// </summary>
    /// <typeparam name="TRequest">The request type the provider accepts.</typeparam>
    /// <typeparam name="TResult">The result type the provider returns.</typeparam>
    /// <param name="provider">The recording provider that stood in for the decision engine.</param>
    /// <param name="values">The values that must not appear.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A value is empty, white space, or shorter than <see cref="MinimumValueLength"/>.</exception>
    /// <exception cref="LeakDetectedException">A value survived.</exception>
    /// <remarks>
    /// Only the payload of each call is searched. The request is the caller's own object,
    /// which the gateway passes through untouched by design.
    /// </remarks>
    public static void NoneOf<TRequest, TResult>(
        RecordingDecisionProvider<TRequest, TResult> provider,
        IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var calls = provider.Calls;
        var fragments = new List<Fragment>();
        for (var i = 0; i < calls.Count; i++)
        {
            fragments.AddRange(Fragments(calls[i].Payload, $"call {i} "));
        }

        Scan(fragments, values);
    }

    /// <summary>
    /// Asserts that none of the values appears in a sequence of written lines, such as log
    /// output, serialized audit records, or the text of an exception.
    /// </summary>
    /// <param name="lines">The lines that were written.</param>
    /// <param name="values">The values that must not appear.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A value is empty, white space, or shorter than <see cref="MinimumValueLength"/>.</exception>
    /// <exception cref="LeakDetectedException">A value survived.</exception>
    public static void NoneIn(IEnumerable<string> lines, IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var fragments = new List<Fragment>();
        var index = 0;
        foreach (var line in lines)
        {
            fragments.Add(new Fragment($"line {index}", line ?? string.Empty));
            index++;
        }

        Scan(fragments, values);
    }

    private static List<Fragment> Fragments(ValidatedPayload payload, string prefix)
    {
        var fragments = new List<Fragment>(payload.Content.Count);
        for (var i = 0; i < payload.Content.Count; i++)
        {
            fragments.Add(new Fragment($"{prefix}segment {i}", payload.Content[i].Text));
        }

        return fragments;
    }

    private static void Scan(IReadOnlyList<Fragment> fragments, IEnumerable<string> values)
    {
        var needles = Prepare(values);
        var leaks = new List<Leak>();

        foreach (var fragment in fragments)
        {
            var condensed = Condense(fragment.Text);
            for (var i = 0; i < needles.Length; i++)
            {
                if (Survives(fragment.Text, condensed, needles[i]))
                {
                    leaks.Add(new Leak(fragment.Location, i, needles[i].Length));
                }
            }
        }

        if (leaks.Count > 0)
        {
            throw new LeakDetectedException(leaks);
        }
    }

    private static bool Survives(string text, string condensedText, string needle)
    {
        if (text.Contains(needle, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var condensedNeedle = Condense(needle);
        return condensedNeedle.Length >= MinimumValueLength
            && condensedText.Contains(condensedNeedle, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] Prepare(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var prepared = new List<string>();
        foreach (var value in values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(values));
            if (value.Length < MinimumValueLength)
            {
                throw new ArgumentException(
                    $"A value of {value.Length} characters is too short to search for; "
                    + $"the minimum is {MinimumValueLength}.",
                    nameof(values));
            }

            prepared.Add(value);
        }

        return [.. prepared];
    }

    private static string Condense(string text)
    {
        var buffer = new char[text.Length];
        var length = 0;
        foreach (var character in text)
        {
            if (!char.IsWhiteSpace(character))
            {
                buffer[length] = character;
                length++;
            }
        }

        return length == text.Length ? text : new string(buffer, 0, length);
    }

    private readonly struct Fragment(string location, string text)
    {
        public string Location { get; } = location;

        public string Text { get; } = text;
    }
}
