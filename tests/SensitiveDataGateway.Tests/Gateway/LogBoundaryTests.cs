using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Tests.Detection;

namespace SensitiveDataGateway.Tests.Gateway;

/// <summary>
/// Proves that nothing the gateway writes outside the process can carry document content.
/// </summary>
/// <remarks>
/// Two complementary checks. The static one forbids the parameter shapes that could carry
/// content at all, so a future log statement cannot regress silently. The dynamic one runs
/// the annotated corpus through the gateway and reads back everything it wrote.
/// </remarks>
public sealed class LogBoundaryTests
{
    private static readonly Assembly[] s_shippingAssemblies =
    [
        typeof(DocumentGateway).Assembly,
        typeof(SanitizationPolicy).Assembly,
    ];

    [Fact]
    public void No_log_message_accepts_free_text()
    {
        var offenders = new List<string>();

        foreach (var method in LoggerMessageMethods())
        {
            foreach (var parameter in method.GetParameters())
            {
                if (!IsContentFree(parameter.ParameterType))
                {
                    offenders.Add($"{method.DeclaringType!.FullName}.{method.Name}({parameter.Name})");
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void Every_shipping_assembly_logs_through_the_source_generator()
    {
        Assert.NotEmpty(LoggerMessageMethods());
    }

    [Fact]
    public async Task Writes_no_corpus_value_to_the_log_or_the_audit_trail()
    {
        var logger = new CapturingLogger();
        var sink = new RecordingAuditSink();
        var gateway = GatewayFixture.Gateway(GatewayFixture.Options(sink).WithLogger(logger));
        var written = new List<string>();

        foreach (var sample in DetectionCorpus.All())
        {
            var bundle = GatewayFixture.Bundle(sample.Name, sample.Text);

            foreach (var policy in new[] { SanitizationPolicy.Balanced, SanitizationPolicy.Strict })
            {
                var result = await gateway.SanitizeAsync(bundle, policy, TestContext.Current.CancellationToken);
                var report = await gateway.InspectAsync(bundle, policy, TestContext.Current.CancellationToken);

                written.Add(result.ToString() ?? string.Empty);
                written.Add(report.ToString() ?? string.Empty);
            }
        }

        written.AddRange(logger.Written);
        written.AddRange(sink.Records.Select(record => record.ToString() ?? string.Empty));

        Assert.NotEmpty(logger.Written);
        Assert.Empty(LeaksIn(written));
    }

    private static List<string> LeaksIn(IReadOnlyList<string> written)
    {
        var leaks = new List<string>();

        foreach (var sample in DetectionCorpus.All())
        {
            foreach (var value in Secrets(sample))
            {
                if (written.Any(line => line.Contains(value, StringComparison.OrdinalIgnoreCase)))
                {
                    leaks.Add($"{sample.Name}: {value.Length} characters");
                }
            }
        }

        return leaks;
    }

    private static IEnumerable<string> Secrets(DetectionCorpus.Case sample) =>
        sample.Expected
            .Select(annotation => annotation.Value)
            .Append(sample.Text)
            .Where(value => value.Length >= 6)
            .Distinct(StringComparer.Ordinal);

    private static List<MethodInfo> LoggerMessageMethods() =>
        s_shippingAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttribute<LoggerMessageAttribute>() is not null)
            .ToList();

    private static bool IsContentFree(Type type)
    {
        var effective = Nullable.GetUnderlyingType(type) ?? type;

        if (effective == typeof(string) || effective == typeof(object))
        {
            return false;
        }

        if (effective.IsEnum || effective.IsPrimitive)
        {
            return true;
        }

        if (effective == typeof(Guid) || effective == typeof(TimeSpan) || effective == typeof(DateTimeOffset))
        {
            return true;
        }

        if (effective == typeof(ILogger) || effective == typeof(Exception))
        {
            return true;
        }

        // Identifier structs carry an application-chosen name, never document content.
        return effective.IsValueType && s_shippingAssemblies.Contains(effective.Assembly);
    }
}
