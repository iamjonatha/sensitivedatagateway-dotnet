using System;

namespace SensitiveDataGateway;

/// <summary>
/// The failure raised when a detector does not complete.
/// </summary>
/// <remarks>
/// An incomplete scan is an unknown, and the pipeline blocks on unknowns. The host converts this failure into a recorded gap so that the document is refused rather than transmitted as if it had been scanned.
/// </remarks>
public sealed class DetectionException : SensitiveDataGatewayException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DetectionException"/> class.
    /// </summary>
    /// <param name="error">The content-free description of the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="error"/> is not categorized as <see cref="GatewayErrorCategory.DetectionFailed"/>.
    /// </exception>
    public DetectionException(GatewayError error)
        : this(error, innerException: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DetectionException"/> class.
    /// </summary>
    /// <param name="error">The content-free description of the failure.</param>
    /// <param name="innerException">The failure that caused this one, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="error"/> is not categorized as <see cref="GatewayErrorCategory.DetectionFailed"/>.
    /// </exception>
    public DetectionException(GatewayError error, Exception? innerException)
        : base(error, GatewayErrorCategory.DetectionFailed, innerException)
    {
    }
}
