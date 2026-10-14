using System.Text.RegularExpressions;

namespace SensitiveDataGateway.Detection;

/// <summary>
/// The expressions the built-in detectors look for.
/// </summary>
/// <remarks>
/// <para>
/// Every expression here is source-generated, carries a match timeout, and uses bounded
/// quantifiers. The bounds are not tidiness: an unbounded quantifier next to another one is
/// how a regular expression turns a long line of text into minutes of backtracking, and a
/// detector that hangs is a detector that stops a document from being scanned.
/// </para>
/// <para>
/// The expressions recognize shapes, not meanings. Whether a shape is a value is decided
/// afterwards, by a check digit where one exists, and by the policy in every other case.
/// </para>
/// </remarks>
internal static partial class BuiltInPatterns
{
    /// <summary>
    /// The time any single pattern may spend on one segment before the scan is declared
    /// incomplete.
    /// </summary>
    public const int TimeoutMilliseconds = 250;

    [GeneratedRegex(
        @"\b[A-Za-z0-9._%+-]{1,64}@(?:[A-Za-z0-9-]{1,63}\.){1,8}[A-Za-z]{2,24}\b",
        RegexOptions.None,
        TimeoutMilliseconds)]
    public static partial Regex Email();

    [GeneratedRegex(
        @"(?<![\w+])(?:\+|00)\d{1,3}[ .-]?\d{2,4}(?:[ .-]?\d{2,4}){1,3}(?!\d)",
        RegexOptions.None,
        TimeoutMilliseconds)]
    public static partial Regex InternationalPhone();

    [GeneratedRegex(
        @"\b(?:tel|phone|mobile|cell|fax)\b[.:]?[ \t]{0,4}(?<value>\+?\d[\d .-]{6,20}\d)",
        RegexOptions.IgnoreCase,
        TimeoutMilliseconds)]
    public static partial Regex LabelledPhone();

    [GeneratedRegex(
        @"\b[A-Z]{2}\d{2}(?: ?[A-Z0-9]{4}){2,7} ?[A-Z0-9]{1,4}\b",
        RegexOptions.IgnoreCase,
        TimeoutMilliseconds)]
    public static partial Regex Iban();

    [GeneratedRegex(
        @"(?<!\d)(?:\d{4}[ -]?){3}\d{1,4}(?!\d)",
        RegexOptions.None,
        TimeoutMilliseconds)]
    public static partial Regex PaymentCard();

    [GeneratedRegex(
        @"(?<!\d)\d{4}[ -]?\d{6}[ -]?\d{5}(?!\d)",
        RegexOptions.None,
        TimeoutMilliseconds)]
    public static partial Regex FifteenDigitPaymentCard();

    [GeneratedRegex(
        @"\b[A-Z]{6}[0-9LMNPQRSTUV]{2}[ABCDEHLMPRST][0-9LMNPQRSTUV]{2}[A-Z][0-9LMNPQRSTUV]{3}[A-Z]\b",
        RegexOptions.IgnoreCase,
        TimeoutMilliseconds)]
    public static partial Regex ItalianTaxCode();

    [GeneratedRegex(
        @"\beyJ[A-Za-z0-9_-]{8,2048}\.[A-Za-z0-9_-]{8,8192}\.[A-Za-z0-9_-]{8,2048}",
        RegexOptions.None,
        TimeoutMilliseconds)]
    public static partial Regex JsonWebToken();

    [GeneratedRegex(
        @"\bAKIA[0-9A-Z]{16}\b",
        RegexOptions.None,
        TimeoutMilliseconds)]
    public static partial Regex AwsAccessKeyId();

    [GeneratedRegex(
        @"\bAIza[0-9A-Za-z_-]{35}\b",
        RegexOptions.None,
        TimeoutMilliseconds)]
    public static partial Regex GoogleApiKey();

    [GeneratedRegex(
        @"\b[sr]k_(?:live|test)_[A-Za-z0-9]{16,99}\b",
        RegexOptions.None,
        TimeoutMilliseconds)]
    public static partial Regex StripeSecretKey();

    [GeneratedRegex(
        @"\bgh[pousr]_[A-Za-z0-9]{36,251}\b",
        RegexOptions.None,
        TimeoutMilliseconds)]
    public static partial Regex GitHubToken();

    [GeneratedRegex(
        @"\bxox[abprs]-[A-Za-z0-9-]{10,72}\b",
        RegexOptions.None,
        TimeoutMilliseconds)]
    public static partial Regex SlackToken();

    [GeneratedRegex(
        """\b(?:api[_-]?key|apikey|client[_-]?secret|access[_-]?token|auth[_-]?token)\b["']?[ \t]{0,8}[:=][ \t]{0,8}["']?(?<value>[A-Za-z0-9_\-+/=.]{16,200})""",
        RegexOptions.IgnoreCase,
        TimeoutMilliseconds)]
    public static partial Regex AssignedSecret();

    [GeneratedRegex(
        @"-----BEGIN(?: [A-Z]{1,16}){0,3} PRIVATE KEY-----[\s\S]{0,16384}?-----END(?: [A-Z]{1,16}){0,3} PRIVATE KEY-----",
        RegexOptions.None,
        TimeoutMilliseconds)]
    public static partial Regex PrivateKeyBlock();

    [GeneratedRegex(
        @"-----BEGIN(?: [A-Z]{1,16}){0,3} PRIVATE KEY-----",
        RegexOptions.None,
        TimeoutMilliseconds)]
    public static partial Regex PrivateKeyHeader();

    [GeneratedRegex(
        @"\b(?:Server|Data\s?Source|Host|Initial\s?Catalog)[ \t]{0,8}=[^;\r\n]{1,128};(?:[^;\r\n]{0,128};){0,8}[ \t]{0,8}(?:Password|Pwd)[ \t]{0,8}=[^;\r\n]{1,128}",
        RegexOptions.IgnoreCase,
        TimeoutMilliseconds)]
    public static partial Regex AdoConnectionString();

    [GeneratedRegex(
        @"\b[a-z][a-z0-9+.-]{0,15}://[^\s:/@]{1,64}:[^\s:/@]{1,64}@[^\s/<>""']{1,128}",
        RegexOptions.IgnoreCase,
        TimeoutMilliseconds)]
    public static partial Regex CredentialsInUrl();

    [GeneratedRegex(
        """\b(?:password|passwd|pwd)\b["']?[ \t]{0,8}[:=][ \t]{0,8}["']?(?<value>[^\s;"']{3,128})""",
        RegexOptions.IgnoreCase,
        TimeoutMilliseconds)]
    public static partial Regex AssignedPassword();

    [GeneratedRegex(
        @"\b[a-z][a-z0-9+.-]{0,15}://[^\s<>""'\]\[{}|\\^`]{1,512}",
        RegexOptions.IgnoreCase,
        TimeoutMilliseconds)]
    public static partial Regex Url();

    [GeneratedRegex(
        @"(?<![\w.])(?:(?:25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)\.){3}(?:25[0-5]|2[0-4]\d|1\d{2}|[1-9]?\d)(?![\w.])",
        RegexOptions.None,
        TimeoutMilliseconds)]
    public static partial Regex IpV4Address();

    [GeneratedRegex(
        @"(?<![\w:])(?:[A-Fa-f0-9]{1,4}:){7}[A-Fa-f0-9]{1,4}(?![\w:])",
        RegexOptions.None,
        TimeoutMilliseconds)]
    public static partial Regex IpV6Address();
}
