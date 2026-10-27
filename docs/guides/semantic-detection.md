# Semantic detection

A pattern finds what it was written to find. A name it has never seen, an address written in
a shape no regular expression anticipated, a sentence that reveals a diagnosis without naming
a disease — these are the gaps a semantic detector exists to close.

Closing them has a cost. A semantic detector is the one stage in this library that hands
document text to something that reads it for meaning, which makes it the one stage where an
instruction hidden in a document has anything to act on, and the one stage that might be
tempted to make a network call. This guide is mostly about how the design keeps both of those
from becoming a leak.

No cloud implementation ships. [ADR 0023](../adr/0023-semantic-detection-is-a-contract-not-a-model.md)
records why, and what would have to change for one to.

## The shape of it

Three types, in three places:

| Type | Package | What it is |
| --- | --- | --- |
| `SemanticDetectionContext` | `SensitiveDataGateway.Core` | The question, frozen |
| `ISemanticDetector` | `SensitiveDataGateway.Core` | What a model adapter implements |
| `SemanticDetectorHost` | `SensitiveDataGateway` | The `ISensitiveDataDetector` the pipeline sees |

You implement the middle one. The host wraps it, checks what it returns, and presents it to
the detection pipeline as an ordinary detector — which means a semantic detector that
misbehaves fails exactly the way a deterministic one would, and fails closed.

## Wiring one up

```csharp
var host = SemanticDetectorHost.Create(myDetector);

var gateway = new DocumentGateway(
    new ExtractionPipeline([new PlainTextExtractor()]),
    new DetectionPipeline([.. DeterministicDetectors.All(), host]),
    new PolicyEngine(),
    new DocumentAnonymizer(new InMemoryPseudonymStore()),
    GatewayOptions.Default);
```

A semantic detector is added to the deterministic set, never substituted for it. The
deterministic detectors are the floor: they do not depend on a model being available,
correct, or honest. A model is there to find what they cannot, not to be trusted with what
they already find.

To narrow what is asked for, pass the types:

```csharp
var host = SemanticDetectorHost.Create(myDetector, [EntityType.Person, EntityType.Health]);
```

The default set is the one a pattern cannot cover: `PERSON`, `ADDRESS`, `DATE_OF_BIRTH`,
`HEALTH`, `LEGAL`, `HR`, `FINANCIAL_CONTEXT` and `TRADE_SECRET`. Asking a model for an IBAN
is asking it to be worse at a job `Iban()` already does with a checksum.

## The trust boundary is in the method name

```csharp
SemanticDetectorHost.Create(detector);                        // in-process, self-hosted
SemanticDetectorHost.CreateOutsideTheTrustBoundary(detector); // anything else
```

`Create` refuses a detector whose `Locality` is `TrustBoundaryLocality.Remote`. It throws at
composition time, not at scan time, so a deployment that would have sent raw document text to
somebody else's API fails to start rather than failing quietly in production at three in the
morning.

The second factory exists because "remote" is not always "wrong" — a model on a machine you
own, across a network segment you control, is a legitimate deployment. What is not legitimate
is reaching that configuration by accident. The method is named the way it is so that the
decision is visible in the diff, in the code review, and in a search for the string.

The choice is recorded downstream too. The host composes the detector's identity as
`semantic/{locality}/{id}` — `semantic/self-hosted/presidio`, `semantic/remote/internal-ner` —
so the audit record of every document says which side of the boundary scanned it.

## What the context does

`SemanticDetectionContext.Render()` produces the text to send to a model. Three properties
hold, for every document, including a hostile one:

**The instructions are a constant.** They are compiled into the library and are identical for
every document. Nothing about a document — its content, its name, its language, its size —
changes a single byte of them. The security suite asserts this across the whole hostile
corpus.

**The content sits inside a fence the document cannot write.** The block is delimited by a
marker generated per context from sixteen random bytes. A document cannot contain it, because
it did not exist when the document was written, and the context refuses to build if a
collision somehow occurs. A document therefore cannot end the content block early and append
instructions of its own: whatever it writes stays inside.

**The content is verbatim.** No escaping, no filtering, no stripping of text that looks like
an instruction. Removing "ignore all previous instructions" from a document would be a
judgement about meaning, and a gateway that judges meaning will be wrong about it. The
document is data all the way to the model; it is the model's prompt discipline, and the
host's checking, that make that safe.

[ADR 0010](../adr/0010-document-content-is-untrusted.md) is the decision these three
implement.

## What the host checks

The host does not trust what comes back. A finding is rejected — and the whole scan fails —
when it:

- is attributed to a detector other than the one that was asked;
- reports a type that was not requested;
- points at a segment that is not in the document;
- runs past the end of the segment it points at.

A `null` result is rejected too. Each of these raises a `DetectionException`, which the
detection pipeline turns into a gap, and a gap blocks under every shipped policy.

That is deliberate and it is the point of the stage. A model that has been talked into
reporting spans that do not exist does not get to produce a document that is "mostly"
sanitized. It gets the document blocked.

The host does not impose a timeout. `DetectionPipeline` already does, and a second budget
would be a second number to keep aligned with the first.

## What a failure costs

| The model | The scan | The document |
| --- | --- | --- |
| Reports nothing | Succeeds | Sanitized by the deterministic detectors alone |
| Throws | Fails | Blocked |
| Never answers | Times out | Blocked |
| Reports a bad span | Fails | Blocked |

The first row is why the deterministic set stays in place. A model that has been argued out
of reporting anything is a model that found nothing, and finding nothing is not an error — it
is just an unhelpful scan. The values a pattern can find are still found, still transformed,
and still checked at the boundary.

## Implementing one

```csharp
public sealed class MyDetector : ISemanticDetector
{
    public DetectorVersion Version { get; } =
        DetectorVersion.Create(DetectorId.Create("my-model"), "1.0.0");

    public TrustBoundaryLocality Locality => TrustBoundaryLocality.SelfHosted;

    public async ValueTask<IReadOnlyList<SensitiveEntity>> ExamineAsync(
        SemanticDetectionContext context,
        CancellationToken cancellationToken)
    {
        // Either send context.Render() to a model, or walk context.Segments yourself.
        // Attribute every entity to context.DetectorId, and keep every span inside the
        // segment it belongs to.
    }
}
```

Four obligations:

1. **Attribute findings to `context.DetectorId`**, not to `Version.DetectorId`. The host
   composes the first from the second plus the locality.
2. **Report spans against `context.Segments`**, using the `SegmentId` of the segment the text
   came from and offsets within that segment's text.
3. **Report only requested types.** `context.EntityTypes` is the list.
4. **Pass the cancellation token down.** The pipeline's timeout depends on it.

An adapter for a model that answers in prose has one more job: parse the answer, and treat a
reply that does not parse as a failure rather than as "no findings". Returning an empty list
because the response was unreadable turns a broken scan into a silently incomplete one.

## Presidio

`SensitiveDataGateway.Detection.Presidio` adapts
[Microsoft Presidio](https://microsoft.github.io/presidio/), which your deployment runs
itself.

```csharp
var http = new HttpClient();
var options = PresidioOptions.ForSelfHosted(new Uri("http://presidio-analyzer:3000"));
var detector = PresidioDetector.Create(http, options);
var host = SemanticDetectorHost.Create(detector);
```

The package takes no dependency beyond `SensitiveDataGateway.Core` and the framework. It
speaks the analyzer's HTTP API directly, one `POST /analyze` per segment, because Presidio
reports offsets within the text it was given.

Presidio is a named-entity recognizer rather than a language model, so it does not consume
`Render()`. It reads `context.Segments` and `context.EntityTypes`. The prompt discipline
costs nothing there and is kept anyway.

### Deployment

The analyzer is a Python service you host. The project publishes a container image; the
gateway assumes nothing about how you run it beyond reachability at the URI you configure.

- `ForSelfHosted(uri)` — a service on infrastructure you control. Reports `SelfHosted`, and
  `SemanticDetectorHost.Create` accepts it. An overload takes the analyzer's language code,
  which defaults to `en`. `http` is accepted here: a container on a network you own is
  reached over a loopback or a bridge, and a certificate there buys nothing.
- `ForHostedService(uri, language)` — a service somebody else runs. Reports `Remote`, and
  `Create` refuses it. Use `CreateOutsideTheTrustBoundary` if that is genuinely what you
  want. The address must be `https`: the content is unsanitized and the network is not
  yours, so cleartext would hand every document to anyone on the path.

Raw document text leaves the process in both cases. The distinction is who receives it.

### What reaches the policy

The analyzer answers with whatever its recognizers found. The detector keeps only the
findings whose type the host asked for, and drops the rest — an `EMAIL_ADDRESS` from
Presidio is discarded, because `Email()` already found it with a pattern and the host would
treat an unrequested type as a malformed answer and block the document.

So the types you pass to `SemanticDetectorHost.Create` narrow what Presidio contributes, and
never produce a failed scan.

### Tuning

```csharp
var options = PresidioOptions.ForSelfHosted(uri)
    .WithMinimumScore(0.6)
    .WithEntityTypes(myMap);
```

`WithMinimumScore` discards findings the analyzer scores below the threshold, before they
reach the policy. Raising it trades recall for noise; the policy thresholds in
[Policies](policies.md) are the other end of the same dial.

The default map covers twenty-three Presidio entity names. One of them deserves a note:
`DATE_TIME` maps to `DATE_OF_BIRTH`, which means every date in a document is treated as a
date of birth. That is the cautious direction and it produces false positives. Narrow the map
with `WithEntityTypes` if your documents are full of dates that are not birthdays.

An entity name the map does not know becomes an `EntityType` built from its lowercased name,
so a custom recognizer you added to your Presidio deployment reaches the policy under a type
you can write a rule against.

## What this stage does not do

- It does not run a model in-process. No ONNX, no inference engine, no model weights in a
  NuGet package.
- It does not ship a cloud adapter, a prompt template for one, or a client for one.
- It does not treat a model's answer as authoritative over a deterministic detector.
- It does not reach the pseudonym map, the audit record, or anything downstream of
  detection.

## See also

- [Detectors](detectors.md) — the deterministic set a semantic detector is added to
- [Policies](policies.md) — what happens to what a detector finds
- [ADR 0010](../adr/0010-document-content-is-untrusted.md) — why content is never obeyed
- [ADR 0023](../adr/0023-semantic-detection-is-a-contract-not-a-model.md) — why no model ships
