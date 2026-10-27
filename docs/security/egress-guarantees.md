# Egress guarantees

What the gateway guarantees, what it does not, and how to tell the difference. This document
is the reference for any claim made about the project anywhere else. A sentence in the
README that is stronger than a sentence here is a bug in the README.

---

## 1. What is guaranteed

These are structural properties. A regression in any of them is a vulnerability, not a
defect. See [`../../SECURITY.md`](../../SECURITY.md).

| # | Guarantee | Enforced by |
| --- | --- | --- |
| G1 | A provider cannot receive anything other than a `ValidatedPayload` | The type system: `IDecisionProvider<,>` takes no other content type |
| G2 | Only the privacy validator can construct a `ValidatedPayload` | No public constructor, the validator is internal, and a test enumerates the public surface for anything that yields one |
| G3 | A provider package cannot reach the raw document, the entity list or the pseudonym map | Package boundaries and an architecture test |
| G4 | A validation failure prevents transmission | No configuration converts it into a warning |
| G5 | A detection failure prevents transmission | Fail-closed evaluation of an incomplete scan, under every policy |
| G6 | An unresolved entity above the tolerated sensitivity prevents transmission | Policy evaluation, then re-checked by the validator |
| G7 | An exceeded limit prevents transmission | Limits block; they never truncate |
| G8 | The pseudonym map is never transmitted | It is not a member of any type that crosses the boundary |
| G9 | No document content is written to a log, metric, trace or exception | Default deny, a fixed field set, and a CI leakage harness |
| G10 | Document content is never executed or acted on as instruction | Content is data at every stage |
| G11 | The same input, policy version and detector versions produce the same output | Determinism is a requirement of every stage |
| G12 | Every operation, including a blocked one, produces an audit record | The façade emits one per operation |

### How the validator enforces them

`PrivacyValidator` runs ten checks, in the order of
[`PrivacyCheck`](../../src/SensitiveDataGateway.Core/Validation/PrivacyCheck.cs). It takes
the detected document, the plan, the sanitized output and the policy, rather than the output
alone: a check that trusts the stage it is checking only confirms that stage agrees with
itself, so each fact is re-derived from the stage before the one that reports it.

| Check | Enforces | Fails when |
| --- | --- | --- |
| `SanitizationStatus` | G4 | the anonymizer did not finish cleanly |
| `ScanCompleteness` | G5 | an extractor or a detector did not complete |
| `UnresolvedEntities` | G6 | a candidate above the tolerated sensitivity is unresolved |
| `PolicyViolations` | G4 | the plan carries a blocking decision |
| `TransformationCoverage` | G4 | a decision other than `Preserve` produced no transformation |
| `AntiLeakScan` | G1, G4 | a detected value, a normalization of it, a fragment of it, a known secret pattern or a high-entropy string survives in the output |
| `TokenResolution` | G8 | a marker body carries the value the marker stands for |
| `MarkerIntegrity` | G4 | the marker count does not match the transformations that produce one |
| `ProvenanceCompleteness` | G11 | the policy, its version or the detector versions are absent or disagree with the plan |
| `PayloadStructure` | G7 | the payload is empty, belongs to another bundle, or exceeds a limit |

Every finding names a check, and optionally a segment, an entity type and a signal. No
finding carries text from the document, which is why a report can be logged whole (G9).

The scan searches for the values of entities the plan decided to transform. Values the
policy decided to `Preserve`, and unresolved candidates the policy tolerates, are in the
output legitimately; flagging them would block documents the policy approved.

---

## 2. What is not guaranteed

Stated as plainly as the guarantees, because a privacy tool that oversells itself is worse
than none.

### N1 — Detection is not complete

An automatic detector has false negatives. A name the model does not recognize, an
identifier in an unexpected format, a confidential project referred to by a nickname: these
pass. The gateway minimizes exposure. It does not eliminate it.

### N2 — The anti-leak scan cannot see what detection missed

The scan searches the output for values the detectors found in the input. A value no
detector found is not in the search set. The scan catches bugs in stages 1 to 4; it does not
catch a gap in stage 2.

### N3 — Indirect identification survives sanitization

Removing every direct identifier does not make a document anonymous. A role, a date, a
location and an amount can identify one person between them. The gateway reduces
linkability; it does not measure or prove it.

### N4 — The gateway governs its own path only

An application can call a vendor SDK directly, in the same process, with the same document.
The gateway makes its own path safe and its own path convenient. It is not a process-wide
egress filter and does not claim to be.

### N5 — This is not compliance

The gateway provides technical mechanisms for data minimization and pseudonymization.
Whether a deployment satisfies a legal obligation depends on purposes, legal bases, data
processing agreements, retention, organizational policy and the operating environment. No
version of this software makes a deployment compliant with anything, and the project makes
no such claim.

### N6 — Pseudonymized data is still personal data

Pseudonymization is not anonymization. A value replaced by a token, with the mapping kept
locally, remains personal data under most frameworks. The gateway is explicit about which
transformations are reversible:

| Action | Reversible | Correlatable |
| --- | --- | --- |
| `Pseudonymize` | yes, locally, through the map | yes |
| `Hash` | no | yes |
| `Mask` | no | partially — the shape survives |
| `Redact` | no | no |

### N7 — The provider is assumed hostile, not verified

The gateway assumes the provider retains and analyses what it receives. It has no way to
verify what the provider actually does, and offers no mechanism that would make the
guarantee depend on provider behaviour.

### N8 — Metadata leaks

Payload size, entity counts, processing duration and request frequency are visible to an
observer, and permit inference. The gateway does not pad, delay or batch to defeat that.

### N9 — The request is not sanitized

`ExecuteAsync` takes a request alongside the bundle, and hands it to the provider untouched.
The gateway does not inspect it, scan it or block on it, because it has no way to tell which
part of a caller-built object is quoted content and which is the caller's own wording.

```csharp
// Safe: the question is a constant.
new ProbabilityQuestion(new QuestionId("risk"), "Is this claim fraudulent?")

// A leak. No stage of the pipeline can see it, and none will stop it.
new ProbabilityQuestion(new QuestionId("risk"), $"Is the claim from {customer.Name} fraudulent?")
```

Build request text from constants. This is the one egress path on the gateway's own route
that the gateway does not govern, and it is the caller's to close. See
[`../providers/custom-provider.md`](../providers/custom-provider.md).

### N10 — A semantic detector reads the document before anything has been removed

A detector registered through `SemanticDetectorHost` is given document text as it was
extracted: nothing has been detected yet, so nothing has been transformed yet. A detector
that is a process, a container or a service therefore receives the unsanitized document.

That is inherent to the job. A stage that finds what the patterns missed has to see what the
patterns saw, and a stage that only ever saw redacted text could not find anything.

What the gateway does about it is make the choice explicit rather than implicit:

- `SemanticDetectorHost.Create` refuses a detector that reports
  `TrustBoundaryLocality.Remote`, at composition time;
- accepting one requires `CreateOutsideTheTrustBoundary`, which is a deliberate name;
- the detector's identity in the audit record carries the locality, so a record says which
  side of the boundary read the document.

None of that verifies the recipient. A self-hosted analyzer on infrastructure you control is
still infrastructure that holds document content, and securing it is the deployment's job,
not the library's. The guarantees above are about what crosses the *egress* boundary towards
a decision provider; they say nothing about a detector you chose to put on a network.

See [`../guides/semantic-detection.md`](../guides/semantic-detection.md) and
[ADR-0023](../adr/0023-semantic-detection-is-a-contract-not-a-model.md).

---

## 3. How a block reaches the caller

A **block** is an expected outcome and is reported as a result:

```csharp
var result = await gateway.ExecuteAsync(bundle, request, policy, cancellationToken);

if (!result.IsAllowed)
{
    // result.Report names every check that failed. It quotes no value.
    return Reject(result.Report);
}

return Handle(result.Value);
```

`result.Value` is `null` whenever `IsAllowed` is `false`, so a caller that forgets the check
gets a null reference rather than a value that was never cleared. `GetValueOrThrow()` is the
same decision taken the other way: it throws `EgressBlockedException` instead of returning
nothing.

A **failure** is an exception. Both prevent egress; neither requires a `catch` block to reach
the safe path. The unsafe path is not reachable by forgetting something.

A provider that throws is reported as `ProviderException`, with its own exception preserved
as the inner one, whatever type it threw. The translation is not decoration: it is what makes
the audit record of an outage say that the *provider* failed rather than that something did.

---

## 4. Verifying the guarantees yourself

Every guarantee above has a test in `tests/SensitiveDataGateway.Security.Tests`. The suite is
part of the project's contract: a change that weakens a guarantee must delete a test, and
deleting one of those tests is a reviewable event. The suite has no access to the internals
of the libraries it tests, so every scenario in it is one a consumer could write.

An application can assert the same property over its own documents:

```csharp
var provider = RecordingDecisionProvider.Returning<ReviewRequest, ReviewDecision>(decision);
var gateway = new DocumentGateway<ReviewRequest, ReviewDecision>(
    DocumentGateway.CreateDefault(),
    provider);

await gateway.ExecuteAsync(bundle, request, policy, cancellationToken);

LeakAssert.NoneOf(provider, expectedSensitiveValues);
```

`SensitiveDataGateway.Testing` ships `LeakAssert`, the recording and fake providers, a
recording audit sink and an annotated synthetic corpus, so that the property can be tested
with no network and no credential. See [`../guides/testing.md`](../guides/testing.md).

A provider is the last place a value could still escape, so each one is tested the same way
in its own suite: the whole corpus through the real pipeline into the adapter, whatever the
adapter was about to transmit flattened back into strings, and every annotated value looked
for in it. `tests/SensitiveDataGateway.Providers.DecisionKit.Tests` does this for both
payload formats, and additionally reads the adapter assembly's type references out of its
metadata to prove it cannot name a type from any stage before validation. Copy both checks
into your own adapter; [`../providers/custom-provider.md`](../providers/custom-provider.md)
shows how.

---

## 5. Reporting a gap

A document that leaks a value the gateway should have caught is a security report, not an
issue. Follow [`../../SECURITY.md`](../../SECURITY.md) and do not open a public issue.
Redact every real value from the report; a synthetic reproduction is always preferred and is
usually sufficient.
