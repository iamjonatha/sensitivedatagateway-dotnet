# Frequently asked questions

Short answers, each with a link to the long one.

---

## What it is

### What problem does this solve?

An application wants a decision — a fraud score, a classification, a routing hint — about a
document that contains personal data. The thing that makes the decision is somewhere the
document is not allowed to go. The gateway sits between them: it reads the document, finds
what is sensitive, transforms it according to a policy, proves the transformed text no longer
carries it, and only then hands a typed payload to a provider.

See [`../architecture/overview.md`](../architecture/overview.md).

### Is it an AI library?

No. It is the thing you put in front of one. It ships no model and makes no inference call of
its own. A semantic detector is a contract an application implements against a model it
already runs — see [ADR-0023](../adr/0023-semantic-detection-is-a-contract-not-a-model.md).

### Does it make me GDPR compliant?

No. No library does. It gives you pseudonymization and redaction you can point at, an audit
record per operation, and a policy that is versioned so you can say which rules applied on a
given day. Whether your processing is lawful is a question about your purpose, your basis and
your retention, and this library knows none of those.

---

## Using it

### What is the smallest thing that works?

```csharp
var gateway = DocumentGateway.CreateDefault();
var result = await gateway.SanitizeAsync(bundle, SanitizationPolicy.Balanced, cancellationToken);
```

See [`getting-started.md`](getting-started.md).

### Which policy should I start with?

`SanitizationPolicy.Strict` if you are sending anything outside your own infrastructure.
`Balanced` if the recipient is inside it. Then write your own: the shipped policies are
starting points, and a policy that was never read is a policy nobody agreed to.

See [`policies.md`](policies.md).

### Why did my document get blocked?

A block is a result, not an exception — read `result.Report`. The usual causes are an
unresolved entity above the policy's tolerance, a detector that failed, a processing limit
exceeded, or the validator finding something in the sanitized text that should not have
survived.

See [`../security/egress-guarantees.md`](../security/egress-guarantees.md) for the full list,
and [ADR-0016](../adr/0016-refusal-is-a-result.md) for why it is a result.

### Can I turn off the block and just get a warning?

No, and there is no setting for it. Fail-closed is not configurable — see
[ADR-0004](../adr/0004-fail-closed.md). If you want to see what *would* happen without
transmitting anything, call `InspectAsync`.

### How do I get the original values back?

With the pseudonym map, through `LocalReidentifier`, inside your own process. The map never
leaves the trust boundary, is never transmitted, and is unreachable from a provider package
by construction — see [ADR-0007](../adr/0007-mapping-stays-inside-the-boundary.md).

### Can I send a PDF, a Word document or an email?

Yes, with the matching adapter package: `Documents.Pdf`, `Documents.OpenXml`,
`Documents.Mime`. An email's attachments are read as parts of the same bundle and sanitized
with it. A scanned PDF needs an OCR engine, which this repository defines and does not ship
— see [ADR-0022](../adr/0022-an-ocr-abstraction-with-no-implementation.md).

---

## Detection

### What does it find out of the box?

The deterministic detectors: structured identifiers with a checkable shape — card numbers
(Luhn), IBANs, Italian tax codes, email addresses, phone numbers, and high-entropy strings
that look like credentials. A candidate whose check digit disagrees is dropped, not reported
weakly ([ADR-0015](../adr/0015-failed-checksum-drops-the-candidate.md)).

See [`detectors.md`](detectors.md).

### It missed a name. Why?

A name has no shape. No pattern finds "Giulia" in "ask Giulia about it" without also finding
every other capitalized word. Names, addresses, health context and the other meaning-bearing
types need a model, which is what a semantic detector is for.

See [`semantic-detection.md`](semantic-detection.md).

### Can I use a cloud model for detection?

You can, and the API makes you say so out loud: `SemanticDetectorHost.Create` refuses a
remote detector, and you have to call `CreateOutsideTheTrustBoundary` instead. Think about
what that means before you type it — a detector sees the document before anything has been
removed. See **N10** in
[`../security/egress-guarantees.md`](../security/egress-guarantees.md).

### A hostile document told the model to ignore its instructions. Does that work?

No. Document content is data everywhere, including in the one stage that reads it for
meaning: the rendered instruction is a constant, content sits inside a delimited block whose
marker a document cannot close, and a finding that does not fit the text it claims to
describe is rejected — which blocks. The hostile corpus in
`tests/SensitiveDataGateway.Security.Tests` proves it.

See [ADR-0010](../adr/0010-document-content-is-untrusted.md).

---

## Operations

### Is any document content ever logged?

No. Not at debug level, not in an exception message, not in a metric tag or a trace
attribute. Logs carry counts, identifiers, hashes and outcomes. The audit record carries a
keyed hash of the document and of the payload, never either of them.

See [ADR-0008](../adr/0008-privacy-safe-observability.md) and
[`logging-and-audit.md`](logging-and-audit.md).

### How big a document can it handle?

As big as the policy's `ProcessingLimits` allow — 2,000,000 extracted characters by default.
Beyond that the operation is refused, never truncated. The pipeline holds the whole extracted
text in memory on purpose; see [ADR-0024](../adr/0024-bounded-buffering-not-streaming.md) and
the measured cost in [`../performance.md`](../performance.md).

### How fast is it?

Published numbers, measured rather than claimed, are in
[`../performance.md`](../performance.md).

### Does it need the network?

Only a `Providers.*` package does, and only to call the provider. `Core`, the pipeline and
every document adapter have no network capability at all, and a test asserts it from the
assembly references. The one deliberate exception is `Detection.Presidio`, which talks to an
analyzer the deployment runs itself — documented in
[`../security/dependency-classification.md`](../security/dependency-classification.md).

### Does the whole test suite run offline?

Yes. No credential, no network, no real clock, no real delay.

---

## Contributing

### Why can I not add that package?

Because every package runs in the same process as the document. The rule, and the way to
argue for an exception, are in [ADR-0012](../adr/0012-dependency-policy.md).

### Why is everything in English when the maintainer is Italian?

So that the next contributor can read it. The analysis the project started from is in
Italian and stays out of the published repository.

### Where do I report a vulnerability?

[`../../SECURITY.md`](../../SECURITY.md). Not in a public issue.
