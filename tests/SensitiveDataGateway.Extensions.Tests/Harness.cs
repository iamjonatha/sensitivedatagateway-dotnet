using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SensitiveDataGateway.Policies;

namespace SensitiveDataGateway.Extensions.Tests;

/// <summary>
/// Builds the inputs the registration tests share.
/// </summary>
internal static class Harness
{
    public static IConfiguration Configuration(IEnumerable<KeyValuePair<string, string?>> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    public static IConfigurationSection Section(params (string Key, string? Value)[] values)
    {
        var entries = new List<KeyValuePair<string, string?>>(values.Length);

        foreach (var (key, value) in values)
        {
            entries.Add(new KeyValuePair<string, string?>($"Policy:{key}", value));
        }

        return Configuration(entries).GetSection("Policy");
    }

    /// <summary>
    /// Gets the keys and values of a policy that is complete enough to build.
    /// </summary>
    public static (string Key, string? Value)[] MinimalPolicy { get; } =
    [
        ("Id", "test.policy"),
        ("Version", "1"),
    ];

    public static IConfigurationSection MinimalSection(params (string Key, string? Value)[] extra)
    {
        var values = new List<(string, string?)>(MinimalPolicy);
        values.AddRange(extra);

        return Section([.. values]);
    }

    public static SanitizationPolicy Policy(string id) =>
        SanitizationPolicy.CreateBuilder(PolicyId.Create(id), PolicyVersion.Create("1")).Build();

    public static ServiceProvider Build(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);

        return services.BuildServiceProvider();
    }
}
