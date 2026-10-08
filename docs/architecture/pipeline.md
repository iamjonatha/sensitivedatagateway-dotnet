# The pipeline

What each stage does, what it is forbidden to do, and what it must produce for the next one
to accept it. [`domain-model.md`](domain-model.md) describes the types; this document
describes the contract between them.

```text
DocumentBundle -> ExtractedDocument -> DetectedDocument -> TransformationPlan
    -> SanitizedDocument -> ValidatedPayload -> provider
```

A stage is a pure function of its input, the policy and the registered components. Given the
same bundle, the same policy version and the same detector versions, the output is the same.
Nondeterminism in a security boundary is a defect, not a performance trade-off.

---

## Stage 1 — Extraction

**In:** `DocumentBundle` · **Out:** `ExtractedDocument`

Turns every part of the bundle into ordered, provenance-carrying text segments.

**Does**

- Select an `IDocumentExtractor` per part by media type, with the plain-text extractor as the
  terminal fallback.
- Preserve structure that matters to provenance: page for a PDF, paragraph and table cell for
  a DOCX, sheet and cell for an XLSX, header name for an email.
- Extract the metadata that is content — file names, subjects, sender and recipient headers —
  as segments, so that they are scanned like everything else.
- Record an `ExtractionFailure` for a part it cannot read.
- Enforce `MaxPartBytes`, `MaxPartCount` and `MaxExtractedCharacters`.

**Must not**

- Classify anything. An extractor has no opinion about what is sensitive.
- Truncate silently. A part over the limit blocks the operation.
- Follow a reference out of the bundle. No URL is fetched, no linked file is opened, no
  external entity is resolved. XXE and SSRF are extraction-stage vulnerabilities.
- Call the network. OCR and cloud document intelligence are *remote processors of raw
  content*: they are outside the strict path, registered explicitly, and never selected
  automatically.

**Blocks when**

- a part exceeds a limit;
- a required part fails to extract and the policy does not allow a partial bundle;
- the bundle produces no segments at all.

---

## Stage 2 — Detection

**In:** `ExtractedDocument` · **Out:** `DetectedDocument`

Runs every registered `ISensitiveDataDetector` and collects candidate entities.

```text
                 +--> deterministic detectors  (parallel, no I/O)
ExtractedDocument +--> structured detectors
                 +--> semantic detector        (local only, opt-in, last)
                          |
                          v
                   merge and reconcile
                          |
                          v
                   DetectedDocument
```

**Does**

- Run deterministic detectors first and in parallel. They are fast, offline, testable and
  free; there is no reason to reach for a model to find an IBAN.
- Run a semantic detector only when the policy asks for one, and only if it is local.
- Reconcile overlapping spans: when two detectors claim the same range, the higher
  sensitivity wins, and the surviving entity records both detectors.
- Record every detector identifier and version in the output, so that a result can be
  reproduced and a regression attributed.
- Record a `DetectionFailure` when a detector throws or times out.

**Must not**

- Decide anything. It produces candidates with confidences.
- Send raw content anywhere. A detector that needs the network is not admissible on the
  strict path; see [ADR-0009](../adr/0009-no-network-in-core.md).
- Interpret content as instruction. Document text reaching a model is data, framed so that
  it cannot be read as a directive; see
  [ADR-0010](../adr/0010-document-content-is-untrusted.md).

**Blocks when**

- a detector fails and the policy requires a complete detection pass — which the strict
  policy does. A partial scan is an unknown, and unknown blocks.

---

## Stage 3 — Policy evaluation

**In:** `DetectedDocument` + `SanitizationPolicy` · **Out:** `TransformationPlan`

Decides what happens to each candidate. This is the only stage that makes a judgement.

**Does**

- Resolve each entity against the policy rules, falling back to `DefaultAction`, and to
  `UnknownEntityAction` for a type the policy does not mention.
- Apply the per-type confidence thresholds, producing transformed, flagged and unresolved
  sets.
- Raise a `PolicyViolation` for any entity whose action is `Block`, and for any unresolved
  entity above the sensitivity level the policy tolerates.
- Attach a rationale to every decision: which rule matched, which threshold applied.
- Stamp the plan with the policy identifier and version.

**Must not**

- Rewrite text.
- Invent a rule for an unknown type. Unknown is governed by `UnknownEntityAction`, and the
  safe default is `Block`.
- Downgrade a `Block` into a warning. There is no configuration that does this.

**Blocks when**

- any decision is `Block`;
- an unresolved entity exceeds the tolerated sensitivity;
- the policy itself is invalid — an invalid policy is a configuration failure, not a
  permissive default.

---

## Stage 4 — Anonymization

**In:** `DetectedDocument` + `TransformationPlan` · **Out:** `SanitizedDocument` (+ a
`PseudonymMap` that stays behind)

Applies the plan to the text.

**Does**

- Rewrite each segment forward against the original text, copying the text between planned
  spans and appending the replacement in place of each one. The source is never mutated, so
  every recorded offset still refers to the document detection saw.
- Resolve overlapping spans before rewriting, strongest action first. A span wholly covered
  by an accepted one is reported as `OverlappingEntities` and dropped; a partial overlap is
  a transformation that could not be applied, and the document becomes `Incomplete`.
- Allocate pseudonyms through the scope the policy selected, reusing the token for a value
  already seen within that scope. `Bundle` is the default.
- Produce tokens that cannot be confused with content: a fixed, documented marker syntax, and
  a check that the marker syntax does not already occur in the source. If it does, the
  markers are escaped — otherwise a document can forge a token and defeat the validator. The
  grammar and the escaping rule are [ADR 0013](../adr/0013-token-vocabulary.md).
- Record an `AppliedTransformation` per rewritten span for the audit trail.
- Set `Status` to `Incomplete` if any planned transformation could not be applied.

**Must not**

- Re-classify. It applies decisions; it does not make them.
- Mutate the input. A new document is produced.
- Put the map anywhere near the output.

**Blocks when**

- a planned transformation fails, leaving the document partially sanitized. Partial is
  unknown, and unknown blocks.

---

## Stage 5 — Final privacy validation

**In:** `SanitizedDocument` · **Out:** `ValidatedPayload` or a block

The last barrier, and the only producer of `ValidatedPayload`. It assumes every previous
stage may have failed.

Checks, all of which must pass:

| # | Check |
| --- | --- |
| 1 | Sanitization status is `Valid` |
| 2 | Every detector completed; no `DetectionFailure` is present |
| 3 | No unresolved entity above the tolerated sensitivity remains |
| 4 | No policy violation is outstanding |
| 5 | Every planned transformation was applied |
| 6 | **Anti-leak scan:** no original sensitive value appears in the output |
| 7 | No pseudonym token resolves to its original inside the payload |
| 8 | No marker syntax was forged by the source document |
| 9 | Policy identifier, version and detector versions are present |
| 10 | The payload structure is well formed and within limits |

### The anti-leak scan

Check 6 is the one that catches a bug in stages 1–4. It compares the sanitized output
against the values the detectors found in the original, looking for:

- exact matches;
- normalized matches — case, whitespace, punctuation, diacritics, common separators removed;
- fragment matches above a configured length, so that `Mario Rossi` is not smuggled through
  as `Mario` plus `Rossi`;
- known-secret patterns, re-run on the output;
- high-entropy strings that survived.

It is deliberately redundant with detection. Redundancy is the point: two independent
implementations of "this value must not be here" fail independently.

It is also not free, and it is not sufficient. It can only look for what detection found, so
a value no detector saw is invisible to it too. It is the second barrier, not the first.

**Blocks when** any check fails. There is no partial pass, and no override.

---

## Stage 6 — Egress

**In:** `ValidatedPayload` + a provider-shaped request · **Out:** the provider's result

```csharp
public interface IDecisionProvider<TRequest, TResult>
{
    string Name { get; }

    Task<TResult> DecideAsync(
        ValidatedPayload payload,
        TRequest request,
        CancellationToken cancellationToken);
}
```

**Does**

- Translate the payload into whatever the downstream engine expects.
- Propagate the caller's `CancellationToken` all the way to the transport.
- Report the provider's own failures without flattening them.

**Must not**

- Accept anything other than a `ValidatedPayload` as content.
- Reach the `DocumentBundle`, the entity list or the pseudonym map. It cannot: no package
  reference gives it the types.
- Re-identify. The gateway offers no mechanism by which a provider could ask for the original
  behind a token.

---

## The two entry points

### The safe path

```csharp
GatewayResult<TResult> result = await gateway.ExecuteAsync(bundle, request, cancellationToken);
```

Runs every stage. There is no way to skip one, because the caller never holds an
intermediate type.

### The advanced path

```csharp
InspectionReport   report  = await gateway.InspectAsync(bundle, cancellationToken);
SanitizationResult result  = await gateway.SanitizeAsync(bundle, cancellationToken);
```

Lets an application see what would happen, or sanitize now and transmit later. It changes
nothing about the boundary: a provider still accepts only a `ValidatedPayload`, and only the
validator makes one. The advanced path gives a caller more visibility, never more
permission.

`InspectAsync` is the intended development and operations tool: it answers "what is in this
document and would it be allowed through" without transmitting anything and without writing
a value to a log.
