using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Policies;

namespace SensitiveDataGateway.Extensions;

/// <summary>
/// Reads a <see cref="SanitizationPolicyDescription"/> out of configuration.
/// </summary>
/// <remarks>
/// <para>
/// The reading is written out by hand, key by key. Nothing here reflects over a policy type,
/// so what configuration can express is exactly what this file names, the schema cannot drift
/// away from the documentation by accident, and the whole package stays trimmable.
/// </para>
/// <para>
/// This class only moves text into a description. Whether that description denotes a usable
/// policy is <see cref="SanitizationPolicyDescription.ToPolicy"/>'s question, and it stays
/// the single place where an answer is given.
/// </para>
/// </remarks>
public static class PolicyConfiguration
{
    /// <summary>
    /// Reads a policy out of configuration.
    /// </summary>
    /// <param name="configuration">The section the policy is written in.</param>
    /// <returns>The policy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is <see langword="null"/>.</exception>
    /// <exception cref="PolicyConfigurationException">
    /// A value is not of the type the schema states, or the description it forms does not
    /// denote a usable policy.
    /// </exception>
    public static SanitizationPolicy ReadPolicy(IConfiguration configuration) =>
        ReadDescription(configuration).ToPolicy();

    /// <summary>
    /// Reads a policy description out of configuration, without building the policy.
    /// </summary>
    /// <param name="configuration">The section the policy is written in.</param>
    /// <returns>The description.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is <see langword="null"/>.</exception>
    /// <exception cref="PolicyConfigurationException">
    /// A value is not of the type the schema states.
    /// </exception>
    public static SanitizationPolicyDescription ReadDescription(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var description = new SanitizationPolicyDescription
        {
            Id = configuration["Id"],
            Version = configuration["Version"],
            DefaultAction = ReadEnum<TransformationAction>(configuration, "DefaultAction"),
            UnknownEntityAction = ReadEnum<TransformationAction>(configuration, "UnknownEntityAction"),
            DefaultScope = ReadEnum<PseudonymScope>(configuration, "DefaultScope"),
            UnresolvedTolerance = configuration["UnresolvedTolerance"],
            DefaultThresholds = ReadThresholds(configuration.GetSection("DefaultThresholds")),
            Limits = ReadLimits(configuration.GetSection("Limits")),
        };

        ReadRules(configuration.GetSection("Rules"), description.Rules);
        ReadThresholdsByType(configuration.GetSection("Thresholds"), description.Thresholds);

        return description;
    }

    private static void ReadRules(IConfigurationSection section, IList<PolicyRuleDescription> rules)
    {
        foreach (var entry in section.GetChildren())
        {
            rules.Add(new PolicyRuleDescription
            {
                Match = entry["Match"],
                Action = ReadEnum<TransformationAction>(entry, "Action"),
                Scope = ReadEnum<PseudonymScope>(entry, "Scope"),
                Sensitivity = ReadEnum<SensitivityLevel>(entry, "Sensitivity"),
            });
        }
    }

    private static void ReadThresholdsByType(
        IConfigurationSection section,
        IDictionary<string, ConfidenceThresholdsDescription> thresholds)
    {
        foreach (var entry in section.GetChildren())
        {
            thresholds[entry.Key] = ReadThresholds(entry) ?? new ConfidenceThresholdsDescription();
        }
    }

    private static ConfidenceThresholdsDescription? ReadThresholds(IConfigurationSection section)
    {
        if (!section.Exists())
        {
            return null;
        }

        return new ConfidenceThresholdsDescription
        {
            Transform = ReadDouble(section, "Transform"),
            Flag = ReadDouble(section, "Flag"),
        };
    }

    private static ProcessingLimitsDescription? ReadLimits(IConfigurationSection section)
    {
        if (!section.Exists())
        {
            return null;
        }

        return new ProcessingLimitsDescription
        {
            MaxBundleBytes = ReadInt64(section, "MaxBundleBytes"),
            MaxPartBytes = ReadInt64(section, "MaxPartBytes"),
            MaxPartCount = ReadInt32(section, "MaxPartCount"),
            MaxExtractedCharacters = ReadInt32(section, "MaxExtractedCharacters"),
            MaxEntities = ReadInt32(section, "MaxEntities"),
            MaxProcessingTime = ReadTimeSpan(section, "MaxProcessingTime"),
        };
    }

    private static T? ReadEnum<T>(IConfiguration configuration, string key)
        where T : struct, Enum
    {
        if (Text(configuration, key) is not { } text)
        {
            return null;
        }

        return Enum.TryParse<T>(text, ignoreCase: true, out var value) ? value : throw Malformed(key);
    }

    private static double? ReadDouble(IConfiguration configuration, string key)
    {
        if (Text(configuration, key) is not { } text)
        {
            return null;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw Malformed(key);
    }

    private static long? ReadInt64(IConfiguration configuration, string key)
    {
        if (Text(configuration, key) is not { } text)
        {
            return null;
        }

        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw Malformed(key);
    }

    private static int? ReadInt32(IConfiguration configuration, string key)
    {
        if (Text(configuration, key) is not { } text)
        {
            return null;
        }

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw Malformed(key);
    }

    private static TimeSpan? ReadTimeSpan(IConfiguration configuration, string key)
    {
        if (Text(configuration, key) is not { } text)
        {
            return null;
        }

        return TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw Malformed(key);
    }

    private static string? Text(IConfiguration configuration, string key)
    {
        var value = configuration[key];

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static PolicyConfigurationException Malformed(string key) =>
        new(GatewayError.Create(
            GatewayErrorCategory.ConfigurationInvalid,
            "policy.configuration_value_malformed",
            string.Create(
                CultureInfo.InvariantCulture,
                $"The policy configuration key \"{key}\" does not hold a value of the type the policy schema states.")));
}
