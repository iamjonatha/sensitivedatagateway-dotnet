# 0006. Separate detection, policy and anonymization

- **Status:** Accepted
- **Date:** 2026-09-29
- **Deciders:** @iamjonatha

## Context

The obvious implementation of a redactor is one component: a regular expression matches an
IBAN and writes `[REDACTED]` in its place. It is short, fast and easy to understand.

It is also unauditable. When a document comes out wrong there is no way to tell whether the
detector matched the wrong span, whether the rule chose the wrong action, or whether the
rewrite was applied incorrectly — because none of those three things produced an artifact.
And every new policy requirement, such as "pseudonymize people but preserve organizations",
means editing the detector.

There is a stronger argument. The detection step is where a model may be involved, and a
model must not be able to decide that a document can be transmitted. If detection and
decision are the same component, the model decides.

## Decision

Three components, three artifacts, one responsibility each.

| Component | Consumes | Produces | Forbidden to |
| --- | --- | --- | --- |
| Detection | `ExtractedDocument` | `SensitiveEntity` candidates with confidence, sensitivity and evidence | Decide anything |
| Policy | Candidates plus a versioned policy | `TransformationPlan`: one decided action per entity, with a rationale | Rewrite text |
| Anonymization | The plan | `SanitizedDocument` | Re-classify or decide |

The `TransformationPlan` is data. It can be inspected, asserted against in a test, summarized
into an audit record and diffed between policy versions, all before any text is rewritten.

A confidence is an input to the policy, never a verdict. `confidence = 0.72` means "this
looks like a person's name", and what happens next is the policy's business. Thresholds are
per entity type, because a false negative on an API key and a false negative on an
organization name do not cost the same.

## Consequences

### Positive

- A model proposes; it never authorizes. The authorization path is deterministic and
  auditable.
- A policy change requires no detector change, and vice versa.
- Each component is unit-testable in isolation: the policy engine is tested with hand-built
  entities and needs no detector at all, which is why Phases 2 to 4 precede Phase 5.
- A wrong output is attributable to exactly one of the three.
- Detectors are replaceable, including with domain-specific ones, without touching the
  boundary.

### Negative

- Three components and an intermediate artifact where one would have worked.
- The plan is an allocation per operation.
- A contributor adding "one small regex that redacts" has to touch two components and a
  policy.

### Neutral

- The separation follows the wider convention of separating recognition from anonymization,
  so the design is legible to anyone who has used a comparable tool.

## Alternatives considered

### A detector that redacts as it matches

Rejected. It is unauditable, it hardcodes policy in pattern code, and it lets whatever
performs detection — potentially a model — make the egress-relevant decision.

### Policy expressed as a callback on the detector

Rejected. It keeps the coupling while making it harder to see: the policy becomes code
scattered across detector registrations rather than a versioned, serializable document.
