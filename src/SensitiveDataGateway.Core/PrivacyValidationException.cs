using System;

namespace SensitiveDataGateway;

/// <summary>
/// The failure raised when the final privacy validator rejects a payload.
/// </summary>
/// <remarks>
/// This is the last check before the trust boundary. Reaching it means an earlier stage produced something the invariants forbid, which is a defect in the pipeline rather than a property of the document.
/// </remarks>
public sealed class PrivacyValidationException : SensitiveDataGatewayException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PrivacyValidationException"/> class.
    /// </summary>
    /// <param name="error">The content-free description of the failure.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="error"/> is not categorized as <see cref="GatewayErrorCategory.ValidationFailed"/>.
    /// </exception>
    public PrivacyValidationException(GatewayError error)
        : this(error, innerException: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivacyValidationException"/> class.
    /// </summary>
    /// <param name="error">The content-free description of the failure.</param>
    /// <param name="innerException">The failure that caused this one, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="error"/> is not categorized as <see cref="GatewayErrorCategory.ValidationFailed"/>.
    /// </exception>
    public PrivacyValidationException(GatewayError error, Exception? innerException)
        : base(error, GatewayErrorCategory.ValidationFailed, innerException)
    {
    }
}
