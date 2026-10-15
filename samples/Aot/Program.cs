using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SensitiveDataGateway;
using SensitiveDataGateway.Anonymization;
using SensitiveDataGateway.Detection;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Testing;
using SensitiveDataGateway.Validation;

// Every step below is one that a naive implementation would have done with reflection: binding a
// policy from configuration, composing a container, resolving a keyed registration, and walking a
// sanitized payload. If any of them needed metadata the trimmer removed, this program would fail
// here rather than in production.

Console.WriteLine("== Configuration binding and composition ==");

IConfigurationRoot configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Gateway:Policy:Id"] = "sample.aot",
        ["Gateway:Policy:Version"] = "1.0.0",
        ["Gateway:Policy:DefaultAction"] = "Pseudonymize",
        ["Gateway:Policy:UnknownEntityAction"] = "Redact",
        ["Gateway:Policy:DefaultScope"] = "Bundle",
        ["Gateway:Policy:DefaultThresholds:Transform"] = "0.7",
        ["Gateway:Policy:DefaultThresholds:Flag"] = "0.5",
        ["Gateway:Policy:Rules:0:Match"] = "IBAN",
        ["Gateway:Policy:Rules:0:Action"] = "Pseudonymize",
        ["Gateway:Policy:Rules:1:Match"] = "EMAIL",
        ["Gateway:Policy:Rules:1:Action"] = "Redact",
        ["Gateway:Policy:Limits:MaxBundleCharacters"] = "100000",
    })
    .Build();

ServiceCollection services = new();

// The policy binder is hand written precisely so that this line does not depend on the
// reflection-based configuration binder, which is not trim safe.
services.AddSensitiveDataGateway(gateway => gateway
    .WithPolicy(configuration.GetSection("Gateway:Policy"))
    // A hashing key carries at least 32 bytes. This one is a placeholder: a real deployment
    // reads it from a secret store, never from source.
    .WithHashingKey(HashingKey.Create(Encoding.UTF8.GetBytes("not-a-real-key-not-a-real-key-not-a-real-key"))));

using ServiceProvider container = services.BuildServiceProvider();

ISensitiveDataGateway sanitizer = container.GetRequiredService<ISensitiveDataGateway>();
SanitizationPolicy policy = container.GetRequiredService<SanitizationPolicy>();

Console.WriteLine($"Resolved policy: {policy.Id} {policy.Version}");
Console.WriteLine($"Rules: {policy.Rules.Count}");
Console.WriteLine();

Console.WriteLine("== Sanitization ==");

// The values below are synthetic. They are structurally valid so that the detectors recognise
// them, and they belong to no one.
DocumentBundle bundle = TestBundle.FromText(
    "Invoice for olivia.hartley@example.com, settled from GB71NWBK99999999999999.");

SanitizationResult result = await sanitizer.SanitizeAsync(bundle, policy, CancellationToken.None);

Console.WriteLine($"Allowed: {result.IsAllowed.ToString(CultureInfo.InvariantCulture)}");
Console.WriteLine($"Checks executed: {result.Report.Executed.Count}");

ValidatedPayload payload = result.Payload
    ?? throw new InvalidOperationException("The sample document was expected to pass validation.");

Console.WriteLine($"Segments: {payload.Content.Count}");
Console.WriteLine($"Content hash: {payload.ContentHash}");
Console.WriteLine();

Console.WriteLine("== Egress assertion ==");

// The assertion the whole library exists for. It throws without ever printing the values it
// looked for.
LeakAssert.NoneOf(payload, "olivia.hartley@example.com", "GB71NWBK99999999999999");

Console.WriteLine("No synthetic value survived sanitization.");
Console.WriteLine();

Console.WriteLine("== Inspection ==");

InspectionReport inspection = await sanitizer.InspectAsync(bundle, policy, CancellationToken.None);

Console.WriteLine($"Would be allowed: {inspection.WouldBeAllowed.ToString(CultureInfo.InvariantCulture)}");

foreach (KeyValuePair<EntityType, int> entry in inspection.EntityCounts)
{
    Console.WriteLine($"  {entry.Key}: {entry.Value.ToString(CultureInfo.InvariantCulture)}");
}

Console.WriteLine();
Console.WriteLine("Native AOT smoke test passed.");
