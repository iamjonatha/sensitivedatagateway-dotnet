# 0005. Give every pipeline stage its own type

- **Status:** Accepted
- **Date:** 2026-09-29
- **Deciders:** @iamjonatha

## Context

The requirement is that a raw document must not be transmissible to an external decision
service. An API of the shape

```csharp
provider.Send(document);
```

cannot express that. Its correctness depends entirely on the caller passing the right string,
and in a codebase of any size somebody eventually passes the wrong one — usually while
debugging, usually in a commit that is not reviewed closely.

A configuration flag does not fix it either. The unsafe call still compiles.

## Decision

Each stage of the pipeline has a distinct input type and a distinct output type, and the
provider abstraction accepts only the last of them.

```text
DocumentBundle -> ExtractedDocument -> DetectedDocument -> TransformationPlan
    -> SanitizedDocument -> ValidatedPayload -> provider
```

`ValidatedPayload` has no public constructor. It is produced by the privacy validator and by
nothing else. A provider package does not reference the types of the earlier stages, so it
cannot name them even accidentally.

Every stage type is immutable, carries its own provenance — policy identifier and version,
detector versions, gateway version — and is a new object rather than a mutation of its input.
The raw document and the sanitized document therefore coexist, which is what makes the
anti-leak comparison possible at all.

Two architecture tests keep this honest:

1. `IDecisionProvider<,>` exposes no member accepting `DocumentBundle`, `ExtractedDocument`,
   `DetectedDocument` or `SanitizedDocument`.
2. `ValidatedPayload` cannot be constructed from an assembly that is not the validator's.

## Consequences

### Positive

- Skipping a stage is a compile error, not a code review finding.
- The unsafe call is not expressible, so it cannot be introduced by a late-night patch.
- Each stage is independently testable, with a hand-built instance of its input type.
- Input and output can be compared, which the anti-leak scan depends on.
- The types document the pipeline; a reader can see the order from the signatures.

### Negative

- Five types where a naive design has one string. More code, more documentation, more
  allocation.
- An application that only wants to redact a string still goes through the bundle model.
- Memory cost: several representations of the same document exist during an operation. The
  limits in `ProcessingLimits` exist partly because of this.

### Neutral

- This is a compile-time property, not a runtime proof. Reflection defeats it. It removes the
  accidental path, which is the one that actually occurs.

## Alternatives considered

### One document type with a status flag

Rejected. `document.IsSanitized` is checked by the code that remembers to check it, and the
compiler helps with nothing.

### A runtime guard inside the provider

Rejected as the only mechanism. It is a useful second barrier — and the validator is exactly
that — but as a first line it means the unsafe call compiles, ships and fails in production
rather than in the editor.

### Marker interfaces on a single type

Rejected. It expresses the states but still allows one object to be passed along the whole
chain, so nothing prevents the provider from receiving the object that also holds the raw
content.
