using System;

namespace SensitiveDataGateway;

/// <summary>
/// The failure raised when a part of the bundle cannot be read.
/// </summary>
/// <remarks>
/// Extraction stops at the part that failed. A part that could not be read is recorded as a failure and never treated as an empty part, because an empty part would be scanned, found clean, and transmitted.
/// </remarks>
public sealed class ExtractionException : SensitiveDataGatewayException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractionException"/> class.
    /// </summary>
    /// <param name="error">The content-free description of the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="error"/> is not categorized as <see cref="GatewayErrorCategory.ExtractionFailed"/>.
    /// </exception>
    public ExtractionException(GatewayError error)
        : this(error, innerException: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtractionException"/> class.
    /// </summary>
    /// <param name="error">The content-free description of the failure.</param>
    /// <param name="innerException">The failure that caused this one, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="error"/> is not categorized as <see cref="GatewayErrorCategory.ExtractionFailed"/>.
    /// </exception>
    public ExtractionException(GatewayError error, Exception? innerException)
        : base(error, GatewayErrorCategory.ExtractionFailed, innerException)
    {
    }
}
