# 0014. Give the validator the whole pipeline, not just its output

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** @iamjonatha

## Context

Stage 5 is the last barrier before anything leaves the trust boundary
([ADR 0004](0004-fail-closed.md)). Every earlier stage already records what it did: the
anonymizer sets a status, the plan names its decisions, the sanitized document lists the
transformations that were applied and the warnings that were raised.

The obvious design is therefore to hand the validator the `SanitizedDocument` alone and let
it read those records. It is the smallest signature, it keeps the stages decoupled, and it
is what the pipeline document originally specified.

It is also worth very little. The records the validator would read are written by the stage
it is checking. A validator that reads `SanitizedDocument.Status` and stops there confirms
that the anonymizer believes it succeeded. A validator that counts
`SanitizedDocument.Applied` confirms that the anonymizer believes it applied what it applied.
Neither is a check; both are a stage agreeing with itself, which is the one thing a stage can
be relied upon to do even when it is wrong.

The failures worth catching are exactly the ones where a stage is wrong without knowing it: a
transformation that silently no-ops, a plan decision that is dropped between evaluation and
application, a span computed against the wrong offset, a payload assembled from a different
bundle. None of these produce an error in the stage that caused them. All of them are visible
by comparing two stages that were written independently.

## Decision

`IPrivacyValidator.Validate` takes four inputs: the `DetectedDocument`, the
`TransformationPlan`, the `SanitizedDocument` and the `SanitizationPolicy`.

Each check re-derives its fact from the stage *before* the one that reports it:

- the entities and the scan failures come from detection, not from the output's warnings;
- the decisions and the blocking violations come from the plan, not from the applied list;
- the limits and the tolerated sensitivity come from the policy, not from the plan;
- the anti-leak scan compares detected values against the output text, which no single stage
  can do on its own.

The output is used for what only the output can answer: what the provider would actually
receive.

Redundancy with the earlier stages is accepted deliberately. Two independent implementations
of "this must not happen" fail independently; one implementation checked against its own
notes does not fail at all.

## Consequences

### Positive

- Every check has a failure mode that a single-input validator cannot see. The test suite
  proves it: each of the ten checks has a test that breaks that check and nothing else, and
  five of them break a stage without that stage reporting an error.
- The validator can verify that the payload belongs to the plan and the plan to the policy,
  which closes a class of mix-up that the type system alone does not.
- The anti-leak scan is possible at all. It needs the original values, and the original
  values exist only in the detected document.

### Negative

- The signature is wide, and a wide signature invites an overload that takes less. Such an
  overload would be a silent downgrade of every check, so the interface has one method and
  the test suite asserts the result types cannot be constructed from outside.
- The caller must keep the earlier stages alive until validation completes. That is already
  true for the audit record, but it means a streaming implementation of the pipeline is
  constrained by this decision.
- The validator knows the shape of four types, so a change to any of them reaches it. This is
  a real coupling cost, paid in exchange for the only checks that have teeth.

### Neutral

- The validator does no I/O and holds no state between calls, so the wider input does not
  make it harder to test or to reason about concurrently.

## Alternatives considered

### Passing only the sanitized document

Rejected, for the reason above: it would confirm the anonymizer's self-report and nothing
else. It is the design that looks cleanest and catches least.

### Carrying a signed provenance record through the stages

Rejected for now. Signing the plan and verifying the signature at stage 5 would detect
tampering between stages, but tampering is not the failure this check exists for — bugs are,
and a bug produces a correctly signed wrong answer. It remains available if the pipeline ever
crosses a process boundary, where tampering becomes the realistic threat.

### Re-running detection inside the validator

Rejected. A second detection pass over the sanitized text would find values the first pass
missed, which sounds strictly better, but it moves the completeness of the scan into the
barrier that is supposed to be simple enough to audit by reading. It also makes stage 5
depend on detector availability, so a model outage would turn into a block of every document
rather than a block of the documents that need one.
