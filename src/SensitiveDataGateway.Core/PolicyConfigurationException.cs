using System;

namespace SensitiveDataGateway;

/// <summary>
/// The failure raised when a policy, a limit or an option is not usable.
/// </summary>
/// <remarks>
/// Configuration is validated when it is built. An unusable configuration fails loudly at that point rather than resolving to a permissive default that is discovered halfway through processing a real document.
/// </remarks>
public sealed class PolicyConfigurationException : SensitiveDataGatewayException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyConfigurationException"/> class.
    /// </summary>
    /// <param name="error">The content-free description of the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="error"/> is not categorized as <see cref="GatewayErrorCategory.ConfigurationInvalid"/>.
    /// </exception>
    public PolicyConfigurationException(GatewayError error)
        : this(error, innerException: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PolicyConfigurationException"/> class.
    /// </summary>
    /// <param name="error">The content-free description of the failure.</param>
    /// <param name="innerException">The failure that caused this one, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="error"/> is not categorized as <see cref="GatewayErrorCategory.ConfigurationInvalid"/>.
    /// </exception>
    public PolicyConfigurationException(GatewayError error, Exception? innerException)
        : base(error, GatewayErrorCategory.ConfigurationInvalid, innerException)
    {
    }
}
