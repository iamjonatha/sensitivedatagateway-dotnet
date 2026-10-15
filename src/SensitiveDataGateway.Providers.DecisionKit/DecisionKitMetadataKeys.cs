namespace SensitiveDataGateway.Providers.DecisionKit;

/// <summary>
/// The metadata keys this package puts on a DecisionKit request.
/// </summary>
/// <remarks>
/// <para>
/// Every key is prefixed <c>sdg.</c>, which this package reserves. An application is free to
/// put its own metadata on the questions it builds; it should not use this prefix, because a
/// future version may add keys under it.
/// </para>
/// <para>
/// Every value is a string. None of them is derived from document content: they describe how
/// the content was sanitized, never what it said. The content hash is the hash of the
/// sanitized text, so it identifies a payload without revealing one.
/// </para>
/// </remarks>
public static class DecisionKitMetadataKeys
{
    /// <summary>
    /// The identifier of the policy the payload was sanitized under.
    /// </summary>
    public const string PolicyId = "sdg.policy.id";

    /// <summary>
    /// The version of the policy the payload was sanitized under.
    /// </summary>
    public const string PolicyVersion = "sdg.policy.version";

    /// <summary>
    /// The version of the gateway that produced the payload.
    /// </summary>
    public const string GatewayVersion = "sdg.gateway.version";

    /// <summary>
    /// The hash of the sanitized content.
    /// </summary>
    public const string ContentHash = "sdg.content.hash";

    /// <summary>
    /// When the payload was sanitized, in round-trip format.
    /// </summary>
    public const string SanitizedAt = "sdg.sanitized.at";
}
