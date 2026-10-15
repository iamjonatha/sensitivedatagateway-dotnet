# Writing your own provider

A provider is the last thing between the gateway and the outside world. It is also the only
stage of the pipeline this repository does not control, so the contract it has to honour is
short, and every clause of it exists because breaking it would leak a document.

## The contract

```csharp
namespace SensitiveDataGateway;

public interface IDecisionProvider<TRequest, TResult>
{
    string Name { get; }

    Task<TResult> DecideAsync(
        ValidatedPayload payload,
        TRequest request,
        CancellationToken cancellationToken);
}
```

`ValidatedPayload` is the only thing the gateway hands over, and it is the only thing you
may send. It cannot be constructed outside the pipeline — its factory is internal — so a
payload you hold is one the validator approved.

```csharp
public sealed class MyProvider : IDecisionProvider<MyRequest, MyDecision>
{
    public string Name => "my-engine";

    public async Task<MyDecision> DecideAsync(
        ValidatedPayload payload,
        MyRequest request,
        CancellationToken cancellationToken)
    {
        var text = string.Join("\n\n", payload.Content.Select(segment => segment.Text));

        return await _client.DecideAsync(text, request.Purpose, cancellationToken);
    }
}
```

What a payload carries, and nothing else:

| Member | What it is |
| --- | --- |
| `BundleId` | Correlates the decision with the audit record |
| `Content` | The sanitized segments, in reading order |
| `Provenance` | Policy, detectors, extractors, gateway version, timestamp |
| `ContentHash` | A hash of the sanitized content |

## The rules

**Send the payload, and only the payload.** Do not accept a `DocumentBundle`, a
`SanitizedDocument` or an entity list through a constructor, a property or a second method.
A provider that can see a pre-validation type is a provider that can send one.

**Do not log content.** `payload.Content` is sanitized, not public. Log `BundleId`,
`ContentHash` and `Provenance`; never a segment's text, not at debug level and not behind a
flag. See [logging and audit](../guides/logging-and-audit.md).

**Do not sanitize anything yourself.** If a value survived the pipeline, re-masking it in the
provider hides a defect that belongs in a detector, and the next provider will not have the
fix.

**Pass the cancellation token down**, to every call, without exception.

**Name yourself once, and constantly.** `Name` is read when the provider is composed into
`DocumentGateway<TRequest, TResult>` — before any document exists — and it becomes the
`ProviderName` on every audit record of a payload you received. Return a fixed identifier such
as `my-engine` or `my-engine:eu-west`. A name built from the payload would put content into
the one record that is supposed to be safe to keep, and an empty name is refused at
composition. See [logging and audit](../guides/logging-and-audit.md).

**Let failures through.** Catch nothing you do not handle. The gateway wraps whatever you
throw in a `ProviderException` and keeps the original as `InnerException`, so an application
can still read the transport's own error. Swallowing it turns a failed decision into a
plausible-looking one.

**Everything in `TRequest` is yours to keep clean.** This is the one hole the gateway cannot
close: the request travels beside the payload and is never inspected, so a request field
built from the document sends that document unsanitized. Build request text from constants,
not from content. This is stated as a non-guarantee in
[the egress guarantees](../security/egress-guarantees.md).

## Proving it

The [testing package](../guides/testing.md) exists for exactly this. The two checks worth
copying from `SensitiveDataGateway.Providers.DecisionKit.Tests`:

**A leak test over the corpus.** Run every synthetic document through the full pipeline into
your provider, flatten whatever the provider was about to transmit back into strings, and
hand it to `LeakAssert.NoneIn` together with the document's known sensitive values. Use a
fake transport; a provider test that opens a socket is not a test.

```csharp
LeakAssert.NoneIn(Flatten(transport.LastRequest), document.Values.Select(v => v.Text));
```

**A boundary test over the compiled assembly.** Read your provider assembly's type
references with `System.Reflection.Metadata` and assert that no pre-validation type name
appears in them. Source review catches this until the day it does not.

```csharp
using var reader = new PEReader(File.OpenRead(typeof(MyProvider).Assembly.Location));
var metadata = reader.GetMetadataReader();

var referenced = metadata.TypeReferences
    .Select(handle => metadata.GetString(metadata.GetTypeReference(handle).Name))
    .ToHashSet();

Assert.DoesNotContain(referenced, Forbidden.Contains);
```

Assert that the payload type *is* referenced too, or the test also passes on an assembly
that was never read.

## Packaging

A provider ships as its own package, named `SensitiveDataGateway.Providers.*`, referencing
`SensitiveDataGateway.Core` and the library it adapts — and nothing else. Keeping it
separate is what keeps network capability out of the packages that read raw content, which
[ADR-0012](../adr/0012-dependency-policy.md) requires and
[the dependency registry](../security/dependency-classification.md) records.

[`SensitiveDataGateway.Providers.DecisionKit`](decisionkit.md) is the reference
implementation. It is about a hundred lines; yours should not need to be much larger.
