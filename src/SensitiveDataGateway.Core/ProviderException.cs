using System;

namespace SensitiveDataGateway;

/// <summary>
/// The failure raised when the downstream decision provider fails.
/// </summary>
/// <remarks>
/// This is the only failure that originates outside the trust boundary. The provider's own exception is kept as the inner exception rather than flattened, and nothing the provider returns is allowed to reach the document model.
/// </remarks>
public sealed class ProviderException : SensitiveDataGatewayException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderException"/> class.
    /// </summary>
    /// <param name="error">The content-free description of the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="error"/> is not categorized as <see cref="GatewayErrorCategory.ProviderFailed"/>.
    /// </exception>
    public ProviderException(GatewayError error)
        : this(error, innerException: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderException"/> class.
    /// </summary>
    /// <param name="error">The content-free description of the failure.</param>
    /// <param name="innerException">The failure that caused this one, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="error"/> is not categorized as <see cref="GatewayErrorCategory.ProviderFailed"/>.
    /// </exception>
    public ProviderException(GatewayError error, Exception? innerException)
        : base(error, GatewayErrorCategory.ProviderFailed, innerException)
    {
    }
}
