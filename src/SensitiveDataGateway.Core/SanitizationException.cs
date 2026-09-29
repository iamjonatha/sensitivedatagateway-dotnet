using System;

namespace SensitiveDataGateway;

/// <summary>
/// The failure raised when a planned transformation cannot be applied.
/// </summary>
/// <remarks>
/// A document whose plan was only partly applied still contains the values the plan meant to remove. It is refused, never transmitted with the transformations that did succeed.
/// </remarks>
public sealed class SanitizationException : SensitiveDataGatewayException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SanitizationException"/> class.
    /// </summary>
    /// <param name="error">The content-free description of the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="error"/> is not categorized as <see cref="GatewayErrorCategory.SanitizationFailed"/>.
    /// </exception>
    public SanitizationException(GatewayError error)
        : this(error, innerException: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SanitizationException"/> class.
    /// </summary>
    /// <param name="error">The content-free description of the failure.</param>
    /// <param name="innerException">The failure that caused this one, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="error"/> is not categorized as <see cref="GatewayErrorCategory.SanitizationFailed"/>.
    /// </exception>
    public SanitizationException(GatewayError error, Exception? innerException)
        : base(error, GatewayErrorCategory.SanitizationFailed, innerException)
    {
    }
}
