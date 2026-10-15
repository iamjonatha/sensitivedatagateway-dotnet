# DecisionKit

`SensitiveDataGateway.Providers.DecisionKit` adapts the gateway to
[DecisionKit](https://www.nuget.org/packages/DecisionKit.Core), the first decision engine
integrated. It is the reference implementation of
[`IDecisionProvider<TRequest, TResult>`](custom-provider.md): everything here is something
your own adapter can do too.

The package is deliberately thin. It maps a `ValidatedPayload` onto a DecisionKit
`DecisionRequest`, calls the DecisionKit provider you composed, and returns what came back.
It interprets no answer, retries nothing, and opens no socket: the transport belongs to the
DecisionKit provider behind it, which this repository neither references nor configures.

## Composing it

```csharp
using DecisionKit.Identifiers;
using DecisionKit.Questions;
using DecisionKit.Results;
using SensitiveDataGateway;
using SensitiveDataGateway.Policies;
using SensitiveDataGateway.Providers.DecisionKit;

var risk = new ProbabilityQuestion(new QuestionId("risk"), "Is this claim fraudulent?");

// engine is any DecisionKit.Providers.IDecisionProvider: a model-backed one, a rule set,
// a fake in a test. The gateway does not care which, and never sees it directly.
var gateway = new DocumentGateway<QuestionSet, DecisionResult>(
    DocumentGateway.CreateDefault(),
    new DecisionKitProvider(engine));

var result = await gateway.ExecuteAsync(
    bundle,
    QuestionSet.Create(risk),
    SanitizationPolicy.Strict,
    cancellationToken);

if (result.IsAllowed)
{
    var probability = result.GetValueOrThrow().Get(risk);
}
```

`TRequest` is `QuestionSet` and `TResult` is `DecisionResult`. The gateway sanitizes the
bundle, validates it, and only then does the adapter build a request.

## What crosses

| DecisionKit | Source | Note |
| --- | --- | --- |
| `Questions` | your `QuestionSet` | Passed through untouched. **Not sanitized** — see below. |
| `Input` | `ValidatedPayload.Content` | The sanitized segments, in one of two shapes. |
| `ClientRequestId` | `ValidatedPayload.BundleId` | For correlating the decision with the audit record. |
| `Metadata` | `ValidatedPayload.Provenance` | Five keys, all strings, listed below. |

Nothing else. The adapter cannot name a `DocumentBundle`, an `ExtractedDocument`, a
`SensitiveEntity` or the pseudonym map — not because it chooses not to, but because
`BoundaryTests` reads the compiled assembly's type references and fails if one of those
names appears in it.

### The two payload formats

```csharp
new DecisionKitProvider(engine, DecisionKitPayloadFormat.Text)      // the default
new DecisionKitProvider(engine, DecisionKitPayloadFormat.Segments)
```

`Text` joins the segments in reading order, separated by a blank line, into
`DecisionInput.Text`. This is the right shape when the decision is made from prose.

`Segments` puts one property per segment into `DecisionInput.Properties`, keyed by segment
identifier. This is the right shape when the decision is made field by field, or when the
answer has to point back at a part of the document.

Segment identifiers are derived from the part they came from, so they are only as readable
as the part identifiers you chose. A bundle whose parts are created with
`PartId.Create("body")` yields keys such as `body:p1`; one that uses `PartId.New()` yields a
GUID. Name your parts if you intend to use this format.

The choice is about shape, never about safety. Both formats carry exactly the same sanitized
text, and neither can reach anything the other cannot.

### The metadata keys

The `sdg.` prefix is reserved by this package. Put your own metadata on the questions
instead; a future version may add keys under this prefix.

| Key | Value |
| --- | --- |
| `sdg.policy.id` | The policy the payload was sanitized under |
| `sdg.policy.version` | That policy's version |
| `sdg.gateway.version` | The gateway that produced the payload |
| `sdg.content.hash` | The hash of the sanitized content |
| `sdg.sanitized.at` | When it was sanitized, in round-trip (`O`) format |

Every value is a string, and none is derived from document content. The content hash is a
hash of the *sanitized* text, so it identifies a payload without revealing one.

The metadata is attached on every request, regardless of what the DecisionKit provider
reports in `Capabilities.SupportsRequestMetadata`. A provider that cannot use metadata
ignores it, which is a better failure than silently losing the only thing that ties a
decision back to the policy that allowed it.

## What does not cross, and what the gateway cannot stop

The gateway sanitizes **the document**. It does not sanitize the `QuestionSet` you pass
alongside it, because a question is yours and the gateway has no idea which parts of it are
quoted content.

```csharp
// Safe: the question text is a constant.
new ProbabilityQuestion(new QuestionId("risk"), "Is this claim fraudulent?")

// A leak. Nothing in this pipeline can see it, and nothing in it will block it.
new ProbabilityQuestion(new QuestionId("risk"), $"Is the claim from {customer.Name} fraudulent?")
```

This is the leak path named in
[the egress guarantees](../security/egress-guarantees.md) and it is the caller's to close.
Build question text from constants.

## Failures

A failed decision stays a failed decision. The adapter catches nothing: a
`DecisionKit.Errors.DecisionException` carries the category and retry hint your application
needs in order to decide what to do next, so it propagates as thrown. The gateway wraps it
in a `ProviderException` without flattening it, so the original is always
`ProviderException.InnerException`.

```csharp
catch (ProviderException ex) when (ex.InnerException is DecisionException decision)
{
    if (decision.Category == DecisionErrorCategory.RateLimit)
    {
        // back off and retry
    }
}
```

A decision never runs at all when the gateway blocks: `result.IsAllowed` is `false`, the
engine was not called, and `result.Report` says why.

## Dependencies

The package references `DecisionKit.Core`, which is classified `Pure` in
[the dependency registry](../security/dependency-classification.md). It does not reference
`DecisionKit.Jev`, or any other transport-carrying package: choosing and configuring the
engine is the application's job, and keeping that choice out of this repository is what
keeps the adapter free of network capability.

See [ADR-0011](../adr/0011-decisionkit-first-integration.md) for why DecisionKit is the
first integration, and what being first does and does not mean.
