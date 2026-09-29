using System;

namespace SensitiveDataGateway;

/// <summary>
/// The failure raised when transmission is refused.
/// </summary>
/// <remarks>
/// This failure reports a refusal the caller did not ask for, such as an attempt to transmit a payload that was never validated. A policy decision to block is returned as a result instead, so that the safe path never depends on a catch block.
/// </remarks>
public sealed class EgressBlockedException : SensitiveDataGatewayException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EgressBlockedException"/> class.
    /// </summary>
    /// <param name="error">The content-free description of the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="error"/> is not categorized as <see cref="GatewayErrorCategory.EgressBlocked"/>.
    /// </exception>
    public EgressBlockedException(GatewayError error)
        : this(error, innerException: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EgressBlockedException"/> class.
    /// </summary>
    /// <param name="error">The content-free description of the failure.</param>
    /// <param name="innerException">The failure that caused this one, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="error"/> is not categorized as <see cref="GatewayErrorCategory.EgressBlocked"/>.
    /// </exception>
    public EgressBlockedException(GatewayError error, Exception? innerException)
        : base(error, GatewayErrorCategory.EgressBlocked, innerException)
    {
    }
}
