namespace SensitiveDataGateway;

/// <summary>
/// The failures the gateway itself reports, as opposed to the ones a stage reports.
/// </summary>
internal static class GatewayErrors
{
    internal const string TooManyEntitiesCode = "limits.too-many-entities";

    internal static GatewayError TooManyEntities { get; } = GatewayError.Create(
        GatewayErrorCategory.LimitExceeded,
        TooManyEntitiesCode,
        "Detection found more candidates than the policy allows.");
}
