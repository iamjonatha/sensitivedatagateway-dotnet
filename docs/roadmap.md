# Roadmap

What exists today, and what is being considered next. This is a direction, not a commitment,
and there are no dates.

## Today

**Nothing is released.** The repository holds the architecture, the threat model and the
decision records, and the pipeline is built end to end. Everything below the egress security
suite is still a plan.

| Area | State |
| --- | --- |
| Architecture and decision records | Written |
| Threat model and egress guarantees | Written |
| Repository foundation | Built |
| Typed pipeline boundary | Built |
| Policy engine | Built |
| Anonymization and pseudonym mapping | Built |
| Final privacy validation | Built |
| Deterministic detection | Built |
| Gateway façade, audit, logging | Built |
| Testing package and egress security suite | Built |
| DecisionKit integration | Built |
| Dependency injection | Built |
| Document adapters | Phase 10 |
| Semantic detection | Phase 11 |
| Mutation testing of the boundary | Phase 13, last |

Target frameworks: `net8.0` and `net10.0`, with an identical public surface on both.

## Stability

The public API is unstable until `1.0.0`: it may change in any minor release while the major
version is `0`. Every public symbol is tracked in `PublicAPI.Shipped.txt` and
`PublicAPI.Unshipped.txt`, so a change to the surface is visible in the diff of the pull
request that makes it.

## Next, after 1.0

In rough priority order. Each item is gated by its own decision record before any code is
written, and each one that lands must leave the existing API working.

1. **More document adapters**, driven by what applications actually receive — RTF, HTML,
   ODF, images with a local OCR engine.
2. **A Presidio adapter**, documented as requiring a local deployment.
3. **Pseudonym scopes beyond the bundle**, with the correlation cost documented next to the
   switch that enables them.
4. **Human review.** `RequireReview` as a full workflow: suspend, hand to a reviewer inside
   the boundary, resume with the reviewer's decisions recorded in the audit trail.
5. **Policy composition.** Layering an organizational policy over a domain policy, with a
   defined conflict resolution.
6. **Telemetry.** An optional package with an `ActivitySource` and a `Meter`, emitting only
   the fields listed in [ADR-0008](adr/0008-privacy-safe-observability.md).
7. **Streaming extraction**, so that a large bundle never has to be fully resident.
8. **Analyzers.** Diagnostics for a policy that leaves a known entity type unhandled, for a
   detector registered without a version, and for a log statement that interpolates content.
9. **Additional provider integrations**, which is also the real test of whether
   [ADR-0001](adr/0001-provider-independent-core.md) held.
10. **Mutation testing of the boundary**, last of all. The suite proves the code passes; it
    does not prove a test would fail if a fail-closed branch stopped closing. A mutation tool
    answers that by rewriting the source one small change at a time and checking that
    something turns red. It is held to the end because it judges every test written before
    it, and because it needs a decision record first: the only mature tool for .NET is not
    Microsoft-maintained, and [ADR-0012](adr/0012-dependency-policy.md) does not yet
    distinguish a development tool from a shipped dependency.

## What is deliberately not planned

- **A hosted gateway or SaaS control plane.** This is a library.
- **A central, shared pseudonym database.** It converts the project's most dangerous artifact
  into a permanent, network-reachable one.
- **Cloud detection of raw content.** It would make the boundary a fiction; see
  [ADR-0009](adr/0009-no-network-in-core.md).
- **A compliance certification, or any claim of one.** See
  [`security/egress-guarantees.md`](security/egress-guarantees.md).
- **A prompt-building surface.** The gateway produces sanitized state. Decision semantics
  belong to the decision engine.
- **Broad format coverage as a goal in itself.** Twenty parsers around a weak boundary is the
  wrong project.
- **Optional parameters on public API.** Overloads or an options object, always.

## Influencing it

Open an issue describing the problem before proposing the API. An item moves because someone
has a use case it blocks, not because it is next in the numbering.

Anything that would let content reach a provider without passing the validator is out of
scope by construction.
