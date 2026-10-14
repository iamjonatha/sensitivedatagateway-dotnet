# Getting started

Five minutes to a document that cannot leave with a name in it.

## Install

```bash
dotnet add package SensitiveDataGateway
```

`SensitiveDataGateway.Core` comes with it. You reference `Core` directly only when you write
a detector, a policy source or a provider in a separate assembly.

## Sanitize a document

```csharp
using SensitiveDataGateway;
using SensitiveDataGateway.Documents;
using SensitiveDataGateway.Policies;

var gateway = DocumentGateway.CreateDefault();

var bundle = DocumentBundle.Create(
    BundleId.Create("claim-4821"),
    DocumentPart.Create(
        PartId.Create("body"),
        PartKind.Body,
        "text/plain",
        DocumentContent.FromText("Contact anna.verdi@example.invalid about the renewal.")));

var result = await gateway.SanitizeAsync(bundle, SanitizationPolicy.Balanced, cancellationToken);

if (!result.IsAllowed)
{
    // result.Report says which checks failed. There is no payload to inspect.
    return;
}

foreach (var segment in result.Payload!.Content)
{
    Console.WriteLine(segment.Text);
}
```

The address is gone, replaced by a token such as `[[EMAIL:7f3c1a92]]`. `result.Payload` is
the only type in the library a provider accepts, and the validator is the only type that
produces one.

## Send it somewhere

A gateway that knows where the document is going takes a provider and hands you a decision:

```csharp
var gateway = new DocumentGateway<ClaimRequest, ClaimDecision>(
    DocumentGateway.CreateDefault(),
    myProvider);

var result = await gateway.ExecuteAsync(bundle, request, SanitizationPolicy.Balanced, cancellationToken);
```

When the document is blocked, `result.IsAllowed` is `false` and the provider is never called.
Not called late, not called with a redacted copy: not called. That is the whole point of the
split between `ISensitiveDataGateway` and `ISensitiveDataGateway<TRequest, TResult>` — only
the generic one can reach a provider, and it can only reach it with a `ValidatedPayload`.

## Look before you send

`InspectAsync` runs the same five stages and throws the sanitized document away:

```csharp
var report = await gateway.InspectAsync(bundle, SanitizationPolicy.Strict, cancellationToken);

Console.WriteLine($"{report.EntityCounts.Count} kinds of entity, allowed: {report.WouldBeAllowed}");
```

`InspectionReport` holds counts, statuses and versions. It holds no text, no entity value and
no offset, so it is safe to log, store and show in a console. `report.Status` is `null` when
the policy blocked the document and anonymization never ran.

## Choose a policy

Two policies ship with the library:

| Policy | Posture |
| --- | --- |
| `SanitizationPolicy.Balanced` | Pseudonymizes what it recognizes, tolerates low-sensitivity doubt |
| `SanitizationPolicy.Strict` | Transforms only at high confidence, blocks on anything unresolved |

Write your own when neither fits. [Policies](policies.md) explains the rules, the thresholds
and what each failure code means.

## Configure the gateway

`GatewayOptions` is immutable; each `With` returns a new instance.

```csharp
var options = GatewayOptions.Default
    .WithAuditSink(mySink)
    .WithLogger(loggerFactory.CreateLogger("SensitiveDataGateway"))
    .WithHashing(HashingKey.Create(secret))
    .WithGatewayVersion("claims-api 2.4");

var gateway = DocumentGateway.CreateDefault(options);
```

[Logging and audit](logging-and-audit.md) covers the sink, the logger and the key.

## What the default gateway can read

`DocumentGateway.CreateDefault()` reads `text/*` and nothing else. A part with any other
media type produces an extraction failure, which `Strict` treats as an incomplete scan and
blocks. Document adapters for PDF, Office and email formats arrive in phase 10; until then,
convert the document yourself or supply your own `IDocumentExtractor`.

## When it throws

Most problems are a blocked result, not an exception. The gateway throws when it cannot
produce a result at all:

| Exception | When |
| --- | --- |
| `ProcessingLimitExceededException` | The bundle, or the entities found in it, exceeded `policy.Limits` |
| `ExtractionException` | A bundle produced neither text nor a failure to report |
| `OperationCanceledException` | The caller cancelled, or the processing budget ran out |

A blocked document is a normal outcome and does not throw. Treat an exception as a bug in the
caller or an attack on the gateway, not as a routine refusal.
