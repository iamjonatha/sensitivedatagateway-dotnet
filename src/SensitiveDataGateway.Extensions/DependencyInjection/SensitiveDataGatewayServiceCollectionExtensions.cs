using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SensitiveDataGateway;
using SensitiveDataGateway.Extensions;
using SensitiveDataGateway.Policies;

// AddSensitiveDataGateway() is reachable without a using directive. The folder cannot be named after it.
#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Registers a sanitization gateway and the policy that governs it.
/// </summary>
/// <remarks>
/// <para>
/// Each overload publishes three services: the <see cref="DocumentGateway"/> itself, the
/// <see cref="ISensitiveDataGateway"/> that forwards to it, and the
/// <see cref="SanitizationPolicy"/> the registration was configured with. The policy is
/// published rather than hidden because the gateway takes one on every call; a caller injects
/// both and hands the policy over at the point of use, where a reviewer can see which policy
/// governed which operation.
/// </para>
/// <para>
/// Registration is eager. The policy is built here, so a configuration file that no longer
/// denotes a usable policy throws on the line that registers it, long before a request
/// arrives.
/// </para>
/// </remarks>
public static class SensitiveDataGatewayServiceCollectionExtensions
{
    /// <summary>
    /// Registers a gateway over the built-in stages, governed by
    /// <see cref="SanitizationPolicy.Strict"/>.
    /// </summary>
    /// <param name="services">The collection.</param>
    /// <returns>The same collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">An unnamed gateway is already registered.</exception>
    public static IServiceCollection AddSensitiveDataGateway(this IServiceCollection services) =>
        services.AddSensitiveDataGateway(static _ => { });

    /// <summary>
    /// Registers a gateway.
    /// </summary>
    /// <param name="services">The collection.</param>
    /// <param name="configure">States what the gateway is made of.</param>
    /// <returns>The same collection.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">An unnamed gateway is already registered.</exception>
    /// <exception cref="PolicyConfigurationException">
    /// The registration states a policy that cannot be built.
    /// </exception>
    public static IServiceCollection AddSensitiveDataGateway(
        this IServiceCollection services,
        Action<SensitiveDataGatewayBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = Compose(services, name: null, configure);

        services.AddSingleton(builder.Policy);
        services.AddSingleton(builder.Build);
        services.AddSingleton<ISensitiveDataGateway>(
            static provider => provider.GetRequiredService<DocumentGateway>());

        return services;
    }

    /// <summary>
    /// Registers a named gateway, resolved as a keyed service.
    /// </summary>
    /// <param name="services">The collection.</param>
    /// <param name="name">The key the gateway, and its policy, are resolved under.</param>
    /// <param name="configure">States what the gateway is made of.</param>
    /// <returns>The same collection.</returns>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or blank.</exception>
    /// <exception cref="InvalidOperationException">A gateway of that name is already registered.</exception>
    /// <exception cref="PolicyConfigurationException">
    /// The registration states a policy that cannot be built.
    /// </exception>
    /// <remarks>
    /// An application that sanitizes for more than one destination registers one gateway per
    /// destination, and injects the one it means with
    /// <see cref="FromKeyedServicesAttribute"/>. Nothing resolves a gateway by name at runtime
    /// out of a catalogue, because a name chosen at runtime is a name nobody reviewed.
    /// </remarks>
    public static IServiceCollection AddSensitiveDataGateway(
        this IServiceCollection services,
        string name,
        Action<SensitiveDataGatewayBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = Compose(services, name, configure);

        services.AddKeyedSingleton(name, builder.Policy);
        services.AddKeyedSingleton(name, (provider, _) => builder.Build(provider));
        services.AddKeyedSingleton<ISensitiveDataGateway>(
            name,
            static (provider, key) => provider.GetRequiredKeyedService<DocumentGateway>(key));

        return services;
    }

    private static SensitiveDataGatewayBuilder Compose(
        IServiceCollection services,
        string? name,
        Action<SensitiveDataGatewayBuilder> configure)
    {
        if (services.Any(descriptor => IsGateway(descriptor, name)))
        {
            throw new InvalidOperationException(
                $"{Describe(name)} is already registered. Register each gateway once, under a name of its own.");
        }

        var builder = new SensitiveDataGatewayBuilder(services, name);
        configure(builder);

        return builder;
    }

    private static bool IsGateway(ServiceDescriptor descriptor, string? name) =>
        descriptor.ServiceType == typeof(DocumentGateway)
        && string.Equals(descriptor.ServiceKey as string, name, StringComparison.Ordinal);

    private static string Describe(string? name) =>
        name is null
            ? "The unnamed sensitive data gateway"
            : $"The sensitive data gateway named \"{name}\"";
}
