using System;
using SensitiveDataGateway.Detection;

namespace SensitiveDataGateway.Anonymization;

/// <summary>
/// Replaces a value with a marker that names its type and nothing else.
/// </summary>
/// <remarks>
/// Redaction is the only transformation that destroys information outright. Two redacted
/// values of the same type are indistinguishable, which is the point: a redaction that could
/// be told apart from another redaction would be a correlatable token wearing a disguise.
/// </remarks>
public static class Redactor
{
    /// <summary>
    /// Returns the replacement for a redacted value.
    /// </summary>
    /// <param name="type">What kind of thing is being removed.</param>
    /// <returns>The marker, for example <c>[REDACTED_EMAIL]</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="type"/> is the default value.</exception>
    public static string Apply(EntityType type) => MarkerSyntax.Redaction(type);
}
