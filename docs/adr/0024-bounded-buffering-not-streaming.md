# 0024. Bound the whole document in memory rather than stream the pipeline

- **Status:** Accepted
- **Date:** 2026-10-27
- **Deciders:** @iamjonatha

## Context

Phase 12 asked for "streaming and memory bounds for large bundles". The two words name two
different things, and only one of them turned out to be available.

The bounds exist: [`ProcessingLimits`](../../src/SensitiveDataGateway.Core/ProcessingLimits.cs)
caps bundle bytes, part bytes, part count, extracted characters, detected entities and
elapsed time, every one of them by refusing the operation rather than by truncating it.
A part whose size the application does not declare is refused before it is read, because a
bound checked after the read has already cost what it was meant to prevent.

Streaming is the other half, and it is the half this pipeline cannot have. Three stages need
the whole text at once:

- **Detection** reports a `SourceSpan` into a segment. A detector that saw a window would
  find whatever straddles the window boundary only by accident, and an attacker who knows the
  window size chooses where to put the value.
- **The policy engine and the reconciler** decide per entity across the whole document:
  overlapping findings from different detectors are merged, and the decision for one entity
  depends on what else was found.
- **The validator** re-derives what the sanitized text must not contain and scans the whole
  payload for it ([ADR-0014](0014-validator-re-derives-what-it-checks.md)). A streamed
  validator would be checking a window against findings derived from a different window.

Ingress is already streamed where it can be:
[`DocumentContent.FromStream`](../../src/SensitiveDataGateway.Core/Documents/DocumentContent.cs)
lets an application keep a 10 MB attachment on disk until the limits say it may be opened,
and the document adapters read from that stream. What cannot be streamed is everything after
the text exists.

## Decision

The pipeline holds the extracted text of one bundle in memory, and bounds it.

- `ProcessingLimits` is part of the policy, so the same policy version always means the same
  peak and a sanitization stays reproducible.
- `MaxExtractedCharacters` is the limit that matters. Bytes bound what arrives; characters
  bound what the pipeline works on, which is where a decompression bomb shows up and where
  the byte limits do not see it.
- The default — 2,000,000 characters — is a starting point for the reference case, not a
  recommendation. An application that knows what it receives tightens it.
- Published benchmarks state the measured peak for a document at the default limit, so an
  application can size a host instead of guessing. See [`../performance.md`](../performance.md).
- No streaming variant of the pipeline is offered, and no `IAsyncEnumerable<TextSegment>`
  appears on the public surface. Offering one would publish an API whose guarantees are
  weaker than the ones this library exists to make, and the difference would not be visible
  at the call site.

## Consequences

### Positive

- The peak is a function of a number in the policy, not of what arrived.
- Detection, reconciliation and validation all see the same text, which is what makes
  re-derivation in the validator meaningful.
- A hostile bundle costs a bounded amount before it is refused.

### Negative

- A document larger than the limit cannot be processed at all. It is refused, not split. An
  application that needs to handle one splits it itself, and accepts that a value spanning
  the split is a value neither half will find.
- Peak memory is a multiple of the document size, not a constant. Extraction, detection
  evidence and the sanitized copy coexist.
- A host sizing exercise is needed before deployment. The benchmarks make it possible; they
  do not make it unnecessary.

### Neutral

- Ingress stays streamable, so the limit applies to text rather than to what an application
  must hold open.

## Alternatives considered

### Stream the pipeline, window the detectors

Rejected. A windowed detector has a boundary, and a boundary is a place to hide a value. The
cost would be paid in the one property the library sells.

### Stream only extraction, buffer the rest

This is what the design already does. Recording it as a decision rather than as an accident
is the point of this record.

### Spill the extracted text to disk above a threshold

Rejected for now. It trades a memory bound for a new place where document content exists at
rest, outside the process, with its own lifetime and its own deletion problem. That is a
larger decision than the one it solves, and it needs its own record if it is ever taken.
