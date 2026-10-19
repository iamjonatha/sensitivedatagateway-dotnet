using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace SensitiveDataGateway.Documents.Mime;

/// <summary>
/// The choices a <see cref="MimeBundleReader"/> makes while it flattens a message.
/// </summary>
/// <remarks>
/// <para>
/// Size and count bounds are not here. They belong to <see cref="ProcessingLimits"/>, which
/// is part of the policy, so that one set of numbers governs the whole operation and a
/// reader cannot be configured to admit a message the pipeline would then refuse.
/// </para>
/// <para>
/// The type is immutable. Each method returns a new instance, so options cannot be changed
/// after a reader was built from them.
/// </para>
/// </remarks>
public sealed class MimeReaderOptions
{
    /// <summary>
    /// The shallowest nesting depth that may be configured.
    /// </summary>
    public const int MinimumNestingDepth = 1;

    /// <summary>
    /// The deepest nesting depth that may be configured.
    /// </summary>
    /// <remarks>
    /// The ceiling exists because the reader hands the parser a recursion bound derived from
    /// it, so that reading untrusted input cannot exhaust the stack whatever a caller asks for.
    /// </remarks>
    public const int MaximumNestingDepth = 64;

    private static readonly ImmutableArray<string> s_defaultHeaders =
    [
        "From",
        "Sender",
        "Reply-To",
        "To",
        "Cc",
        "Bcc",
        "Subject",
    ];

    private MimeReaderOptions(int maxNestingDepth, ImmutableArray<string> headers)
    {
        MaxNestingDepth = maxNestingDepth;
        Headers = headers;
    }

    /// <summary>
    /// Gets the options a reader uses when the caller states none.
    /// </summary>
    /// <value>
    /// Eight levels of nesting, and the seven headers that carry the people involved in a
    /// message.
    /// </value>
    public static MimeReaderOptions Default { get; } = new(maxNestingDepth: 8, s_defaultHeaders);

    /// <summary>
    /// Gets the deepest the reader descends into nested multiparts and forwarded messages.
    /// </summary>
    /// <remarks>
    /// A message deeper than this is refused, not truncated. Truncation would drop the
    /// innermost message, which is the one an attacker would have put something in.
    /// </remarks>
    public int MaxNestingDepth { get; }

    /// <summary>
    /// Gets the header fields the reader turns into parts, compared without regard to case.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A header that is not on this list is not emitted, and what is not emitted is never
    /// extracted, never scanned and never transmitted. The list therefore bounds noise, not
    /// safety: adding a header makes a review more informative, and removing one cannot make
    /// a value escape.
    /// </para>
    /// <para>
    /// Header values are content and are scanned like any other text. The default list is
    /// the one that matters for privacy, because a sender address and a subject line are
    /// usually where a person's name appears first.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> Headers { get; }

    /// <summary>
    /// Returns options with a different nesting depth.
    /// </summary>
    /// <param name="depth">The deepest level of nesting the reader descends to.</param>
    /// <returns>The new options.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="depth"/> is outside <see cref="MinimumNestingDepth"/> and
    /// <see cref="MaximumNestingDepth"/>.
    /// </exception>
    public MimeReaderOptions WithMaxNestingDepth(int depth)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(depth, MinimumNestingDepth);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(depth, MaximumNestingDepth);

        return new MimeReaderOptions(depth, Headers.ToImmutableArray());
    }

    /// <summary>
    /// Returns options that emit a different set of headers.
    /// </summary>
    /// <param name="headers">The header field names. The sequence is copied.</param>
    /// <returns>The new options.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="headers"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="headers"/> is empty or contains a name that is <see langword="null"/>
    /// or white space.
    /// </exception>
    /// <remarks>
    /// An empty list is refused rather than accepted as "no headers". A reader configured to
    /// emit nothing from the envelope reads a message whose sender and subject were never
    /// examined, and that is a configuration mistake worth failing on.
    /// </remarks>
    public MimeReaderOptions WithHeaders(IEnumerable<string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var builder = ImmutableArray.CreateBuilder<string>();

        foreach (var header in headers)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                throw new ArgumentException("A header field name cannot be empty.", nameof(headers));
            }

            builder.Add(header.Trim());
        }

        if (builder.Count == 0)
        {
            throw new ArgumentException(
                "At least one header field name is required.",
                nameof(headers));
        }

        return new MimeReaderOptions(MaxNestingDepth, builder.ToImmutable());
    }

    /// <summary>
    /// Returns the configured header names as a set that compares without regard to case.
    /// </summary>
    /// <returns>The set.</returns>
    internal HashSet<string> HeaderSet() => new(Headers, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Formats an ordinal the same way on every machine.
    /// </summary>
    /// <param name="prefix">The identifier prefix.</param>
    /// <param name="ordinal">The ordinal.</param>
    /// <returns>The identifier text.</returns>
    internal static string Identifier(string prefix, int ordinal) =>
        string.Concat(prefix, ordinal.ToString(CultureInfo.InvariantCulture));
}
