using System;

namespace SensitiveDataGateway;

/// <summary>
/// The base class for every failure raised by the gateway.
/// </summary>
/// <remarks>
/// <para>
/// The hierarchy is one level deep. A caller that needs to tell two failures apart reads
/// <see cref="Error"/> and switches on <see cref="GatewayError.Category"/> or on
/// <see cref="GatewayError.Code"/>, rather than adding another <see langword="catch"/>
/// block. Deep hierarchies push callers into catching a base type they do not understand.
/// </para>
/// <para>
/// No exception in this hierarchy exposes document content. Everything an exception carries
/// is in <see cref="Error"/>, which is content-free by construction, so a log or an audit
/// record built from a caught exception cannot leak the document that caused it.
/// </para>
/// <para>
/// A policy decision to block is not a failure and is not raised as an exception. It is
/// returned as a result, so that refusing to transmit never depends on a caller writing a
/// <see langword="catch"/> block.
/// </para>
/// </remarks>
public abstract class SensitiveDataGatewayException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SensitiveDataGatewayException"/> class.
    /// </summary>
    /// <param name="error">The content-free description of the failure.</param>
    /// <param name="expectedCategory">The category the concrete exception type stands for.</param>
    /// <param name="innerException">The failure that caused this one, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="error"/> does not have the category the exception type stands for.
    /// </exception>
    protected SensitiveDataGatewayException(
        GatewayError error,
        GatewayErrorCategory expectedCategory,
        Exception? innerException)
        : base(Describe(error), innerException)
    {
        if (error.Category != expectedCategory)
        {
            throw new ArgumentException(
                $"The error describes {error.Category} and cannot be raised as {expectedCategory}.",
                nameof(error));
        }

        Error = error;
    }

    /// <summary>
    /// Gets the content-free description of the failure.
    /// </summary>
    public GatewayError Error { get; }

    private static string Describe(GatewayError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.ToString();
    }
}
