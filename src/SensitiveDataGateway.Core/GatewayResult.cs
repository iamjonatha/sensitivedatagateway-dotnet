using System;
using SensitiveDataGateway.Validation;

namespace SensitiveDataGateway;

/// <summary>
/// The outcome of an operation that was allowed to reach a decision provider, or the reasons
/// it was not.
/// </summary>
/// <typeparam name="TResult">The provider's result.</typeparam>
/// <remarks>
/// A blocked operation is a result the caller reads, not an exception the caller catches. A
/// provider failure is the opposite: it is a fault, and it is thrown.
/// </remarks>
public sealed class GatewayResult<TResult>
{
    internal GatewayResult(BundleId bundleId, bool isAllowed, TResult? value, PrivacyValidationReport report)
    {
        BundleId = bundleId;
        IsAllowed = isAllowed;
        Value = value;
        Report = report;
    }

    /// <summary>
    /// Gets the processing operation this result concerns.
    /// </summary>
    public BundleId BundleId { get; }

    /// <summary>
    /// Gets a value indicating whether the operation reached the provider.
    /// </summary>
    public bool IsAllowed { get; }

    /// <summary>
    /// Gets the provider's result, or <see langword="default"/> when the operation was blocked.
    /// </summary>
    public TResult? Value { get; }

    /// <summary>
    /// Gets what the validator checked, and what it found.
    /// </summary>
    /// <remarks>
    /// The report is present whether the operation was allowed or blocked. An allowed
    /// operation that cannot say what was checked is indistinguishable from one that checked
    /// nothing.
    /// </remarks>
    public PrivacyValidationReport Report { get; }

    /// <summary>
    /// Returns the provider's result, or throws when the operation was blocked.
    /// </summary>
    /// <returns>The provider's result.</returns>
    /// <exception cref="EgressBlockedException">The operation was blocked.</exception>
    /// <remarks>
    /// This is the unsafe convenience: it turns a result the caller is meant to read into a
    /// failure the caller may ignore with a catch. Prefer reading <see cref="IsAllowed"/>.
    /// </remarks>
    public TResult GetValueOrThrow() =>
        IsAllowed
            ? Value!
            : throw new EgressBlockedException(
                GatewayError.Create(
                    GatewayErrorCategory.EgressBlocked,
                    "egress.blocked",
                    "The operation was blocked before egress. The report lists every finding.")
                    .InBundle(BundleId));

    /// <summary>
    /// Returns a description of the result.
    /// </summary>
    /// <returns>Whether the operation was allowed, and the report. It carries no content.</returns>
    public override string ToString() => IsAllowed ? $"Allowed: {Report}" : $"Blocked: {Report}";
}
