using System;

namespace SensitiveDataGateway;

/// <summary>
/// The failure raised when a size, count or time limit is reached.
/// </summary>
/// <remarks>
/// Every limit refuses the operation instead of truncating it. A truncated document is a document whose unexamined tail was dropped, and the tail is where a payload would be hidden.
/// </remarks>
public sealed class ProcessingLimitExceededException : SensitiveDataGatewayException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingLimitExceededException"/> class.
    /// </summary>
    /// <param name="error">The content-free description of the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="error"/> is not categorized as <see cref="GatewayErrorCategory.LimitExceeded"/>.
    /// </exception>
    public ProcessingLimitExceededException(GatewayError error)
        : this(error, innerException: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingLimitExceededException"/> class.
    /// </summary>
    /// <param name="error">The content-free description of the failure.</param>
    /// <param name="innerException">The failure that caused this one, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="error"/> is not categorized as <see cref="GatewayErrorCategory.LimitExceeded"/>.
    /// </exception>
    public ProcessingLimitExceededException(GatewayError error, Exception? innerException)
        : base(error, GatewayErrorCategory.LimitExceeded, innerException)
    {
    }
}
