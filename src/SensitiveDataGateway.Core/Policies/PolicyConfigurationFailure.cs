namespace SensitiveDataGateway.Policies;

/// <summary>
/// Builds the failures raised when a declarative policy description is not usable.
/// </summary>
/// <remarks>
/// The codes live together so that the set stays small and stable: they are part of what an
/// operator matches on when a deployment refuses to start.
/// </remarks>
internal static class PolicyConfigurationFailure
{
    internal static PolicyConfigurationException Invalid(string code, string description) =>
        new(GatewayError.Create(GatewayErrorCategory.ConfigurationInvalid, code, description));
}
