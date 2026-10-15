# Dependency injection

`SensitiveDataGateway.Extensions` turns the four hand-assembled stages into one registration,
reads a policy out of configuration without reflection, and publishes both so that a call site
still says which policy governed it.

The package has no network, no reflection and no dependency beyond
`Microsoft.Extensions.Configuration.Abstractions`,
`Microsoft.Extensions.DependencyInjection.Abstractions` and
`Microsoft.Extensions.Logging.Abstractions`. [ADR 0017](../adr/0017-di-composes-the-gateway-the-caller-passes-the-policy.md)
records why it is shaped this way.

## The shortest registration

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddSensitiveDataGateway();
```

That publishes three singletons:

| Service | What it is |
| --- | --- |
| `DocumentGateway` | The gateway itself |
| `ISensitiveDataGateway` | The same instance, behind its interface |
| `SanitizationPolicy` | The policy this registration was configured with |

With nothing stated, the registration uses the plain-text extractor, every deterministic
detector, an in-process pseudonym store and `SanitizationPolicy.Strict`. **The default policy
is the strict one**, so a registration that forgets to choose blocks rather than leaks.

## Using it

The policy is a parameter of every operation, not a dependency of the gateway. Inject both and
pass it:

```csharp
public sealed class TicketTriage(ISensitiveDataGateway gateway, SanitizationPolicy policy)
{
    public async Task<SanitizationResult> PrepareAsync(DocumentBundle bundle, CancellationToken ct) =>
        await gateway.SanitizeAsync(bundle, policy, ct);
}
```

This is two injections where one would have done. It buys a call site that states which rules
governed it, which is the whole point of the library and is not worth trading for a shorter
constructor.

## Stating what the gateway is made of

```csharp
services.AddSensitiveDataGateway(gateway => gateway
    .WithPolicy(configuration.GetSection("Gateway:Policy"))
    .WithHashingKey(HashingKey.Create(keyMaterial))
    .WithPseudonymStore(provider => provider.GetRequiredService<RedisPseudonymStore>())
    .WithAuditSink(provider => provider.GetRequiredService<SqlAuditSink>()));
```

| Method | Effect when omitted |
| --- | --- |
| `WithPolicy(SanitizationPolicy)` | `SanitizationPolicy.Strict` |
| `WithPolicy(IConfiguration)` | — reads the section below |
| `WithExtractor(IDocumentExtractor)` | `PlainTextExtractor` alone |
| `WithDetector(ISensitiveDataDetector)` | every deterministic detector |
| `WithHashingKey(HashingKey)` | a key that dies with the process |
| `WithPseudonymStore(Func<IServiceProvider, IPseudonymStore>)` | an in-process store |
| `WithAuditSink(Func<IServiceProvider, IAuditSink>)` | the container's `IAuditSink`, else records are discarded |
| `WithGatewayVersion(string)` | the assembly's informational version |
| `WithTimeProvider(TimeProvider)` | `TimeProvider.System` |

Two of those defaults are fine on one machine and wrong on two. A hashing key that dies with
the process means two instances of the same application disagree about every hash, and an
in-process pseudonym store means they disagree about every pseudonym. State both in anything
that scales out.

Stating **any** detector replaces the deterministic set wholesale. Adding one to the built-in
set means listing them:

```csharp
services.AddSensitiveDataGateway(gateway =>
{
    foreach (var detector in DeterministicDetectors.All())
    {
        gateway.WithDetector(detector);
    }

    gateway.WithDetector(new EmployeeNumberDetector());
});
```

There is no `WithDetector<T>()` that constructs `T` for you. Constructing a type the compiler
was never shown is the one thing that would stop this package publishing under native AOT.

## Logging

If an `ILoggerFactory` is registered, the gateway logs through it. If not, it logs nowhere.
Nothing in those events can carry document content — see
[Logging and audit](logging-and-audit.md).

## More than one gateway

An application that sanitizes for more than one destination registers one gateway per
destination:

```csharp
services.AddSensitiveDataGateway("analytics", gateway => gateway
    .WithPolicy(configuration.GetSection("Gateway:Analytics")));

services.AddSensitiveDataGateway("support", gateway => gateway
    .WithPolicy(configuration.GetSection("Gateway:Support")));
```

Both the gateway and its policy are keyed under the name:

```csharp
public sealed class AnalyticsExport(
    [FromKeyedServices("analytics")] ISensitiveDataGateway gateway,
    [FromKeyedServices("analytics")] SanitizationPolicy policy);
```

There is no factory that resolves a gateway from a name computed at run time. A name assembled
from a request is a policy chosen by a request.

Registering the same name twice throws `InvalidOperationException` naming the registration. The
message names the registration and nothing else: no file, no key, no value.

## Reading a policy from configuration

`WithPolicy(IConfiguration)` reads the section key by key. Every key below is one that
`PolicyConfiguration` names explicitly; a key it does not name is ignored, and a key it does
name holding a value of the wrong shape throws `PolicyConfigurationException` with code
`policy.configuration_value_malformed`.

### Schema

| Key | Type | Meaning |
| --- | --- | --- |
| `Id` | string | Policy identifier, reported in audit records |
| `Version` | string | Policy version, reported in audit records |
| `DefaultAction` | `TransformationAction` | What happens to a detected entity with no rule of its own |
| `UnknownEntityAction` | `TransformationAction` | What happens to an entity type the policy never mentions |
| `DefaultScope` | `PseudonymScope` | How far a pseudonym stays stable: `Document`, `Bundle`, `Session`, `Tenant` |
| `UnresolvedTolerance` | string | A `SensitivityLevel` name, or `None`. Absent tolerates nothing |
| `DefaultThresholds:Transform` | double | Confidence at or above which an entity is transformed |
| `DefaultThresholds:Flag` | double | Confidence at or above which an entity is reported but not transformed |
| `Thresholds:<ENTITY_TYPE>:Transform` | double | Per-type override |
| `Thresholds:<ENTITY_TYPE>:Flag` | double | Per-type override |
| `Rules:<n>:Match` | string | The entity type this rule governs |
| `Rules:<n>:Action` | `TransformationAction` | What happens to it |
| `Rules:<n>:Scope` | `PseudonymScope` | Correlation scope for this rule |
| `Rules:<n>:Sensitivity` | `SensitivityLevel` | `Low`, `Moderate`, `High`, `Critical` |
| `Limits:MaxBundleBytes` | long | |
| `Limits:MaxPartBytes` | long | |
| `Limits:MaxPartCount` | int | |
| `Limits:MaxExtractedCharacters` | int | |
| `Limits:MaxEntities` | int | |
| `Limits:MaxProcessingTime` | `TimeSpan` | `hh:mm:ss` |

`TransformationAction` is `Preserve`, `Mask`, `Redact`, `Pseudonymize`, `Hash`, `Encrypt`,
`Block` or `RequireReview`. Names are matched case-insensitively. Entity type names are the
canonical upper-case ones — `IBAN`, `EMAIL`, `TAX_ID` — listed in
[Detectors](detectors.md).

Rules are applied in the order they are written, so `Rules:0` wins over `Rules:1` when both
match.

### Example

```json
{
  "Gateway": {
    "Policy": {
      "Id": "support.outbound",
      "Version": "2.1.0",
      "DefaultAction": "Pseudonymize",
      "UnknownEntityAction": "Redact",
      "DefaultScope": "Bundle",
      "UnresolvedTolerance": "Low",
      "DefaultThresholds": { "Transform": 0.7, "Flag": 0.5 },
      "Thresholds": {
        "IBAN": { "Transform": 0.95 },
        "PERSON": { "Transform": 0.6, "Flag": 0.4 }
      },
      "Rules": [
        { "Match": "IBAN", "Action": "Block", "Sensitivity": "Critical" },
        { "Match": "EMAIL", "Action": "Pseudonymize", "Scope": "Tenant" },
        { "Match": "PROJECT", "Action": "Preserve" }
      ],
      "Limits": {
        "MaxBundleBytes": 10485760,
        "MaxPartCount": 25,
        "MaxEntities": 5000,
        "MaxProcessingTime": "00:00:30"
      }
    }
  }
}
```

### When a bad policy is noticed

At registration, not at the first request. `AddSensitiveDataGateway` runs the configure
callback and builds the policy on the spot, so a file that no longer parses throws on the line
in `Program.cs` that registers it, before `builder.Build()` is reached:

```
PolicyConfigurationException: The policy configuration key "DefaultThresholds:Transform" does
not hold a value of the type the policy schema states.
```

There is no `ValidateOnStart` and no hosted service, because the check happens earlier than
either would run and the stack trace points at the registration rather than at a framework.

## Native AOT and trimming

The package is trim- and AOT-clean, and `samples/Aot` is the proof: it composes a container,
binds a policy from configuration and sanitizes a document, publishing with `PublishAot`,
`TrimMode=full` and warnings as errors. Run it yourself:

```bash
dotnet publish samples/Aot -c Release -r win-x64
./artifacts/publish/SensitiveDataGateway.Samples.Aot/release_win-x64/SensitiveDataGateway.Samples.Aot
```

Do not reach for `configuration.Get<SanitizationPolicyDescription>()`. It works in a JIT build
and fails in a published one, which is the worst place to find out.
