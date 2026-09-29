# 0009. Keep the network out of everything that reads raw content

- **Status:** Accepted
- **Date:** 2026-09-29
- **Deciders:** @iamjonatha

## Context

The best available detectors for names, organizations and semantic categories are large
models, and the easiest of those to adopt are hosted APIs. The same is true of OCR and of
document-intelligence services: the cloud versions are better and are one HTTP call away.

Adopting one of them for detection produces a specific absurdity. To decide whether a
document may be sent to an external AI service, the document is sent to an external AI
service. The boundary still exists in the code, and it protects nothing, because the data
left at stage 2.

This is not a hypothetical failure mode. It is the most likely way this project could be
built wrong, because at every individual step it looks like an improvement in detection
quality.

## Decision

Network capability is confined to `SensitiveDataGateway.Providers.*`, and nothing that reads
raw content may reach the network.

1. `SensitiveDataGateway.Core` performs no I/O of any kind and has zero package references.
2. The pipeline package — extraction host, detection host, policy engine, anonymizers,
   validator — performs no network I/O. It may reference only packages classified `Pure`; see
   [`../security/dependency-classification.md`](../security/dependency-classification.md).
3. A detector on the strict path must run inside the trust boundary: in-process, local, or
   self-hosted within the deployment. Registering a network-capable detector on the strict
   path is refused at composition time, not warned about at runtime.
4. Remote OCR and remote document intelligence are external processors of raw content. They
   are never selected automatically, must be registered by name, and their registration
   documents that the strict guarantee no longer holds for the parts they handle.
5. Only a `Providers.*` package may open a socket, and it receives only a `ValidatedPayload`.

Self-hosted is inside the boundary; "private endpoint of a vendor's cloud" is not, unless the
deployment's own risk assessment says it is — and that assessment is the application's, made
explicitly, not a default this library ships.

## Consequences

### Positive

- The threat model has exactly one egress point, and it is the one the validator guards.
- The claim "raw content does not leave your boundary" is checkable by reading the package
  graph.
- `Core` and the pipeline are testable with the network disabled entirely.
- A reviewer can audit egress by looking at which packages are referenced, not by reading
  every method.

### Negative

- The best hosted detection services cannot be used on the strict path, so out-of-the-box
  semantic detection quality is lower than it could be.
- Users who want cloud OCR must accept an explicitly documented weakening, or pre-process
  outside the gateway.
- A local model is more work to deploy than an API key, and that cost lands on the adopter.

### Neutral

- The rule constrains the project's own dependency choices more than it constrains users.
  That is intentional: the users' choices are theirs to make, and this library's job is to
  make the safe one the default.

## Alternatives considered

### A cloud semantic detector with a warning in the documentation

Rejected. It makes the default configuration a false boundary, and the warning is read after
the integration is written.

### A "trusted network" configuration switch

Rejected. Every deployment considers its own network trusted. The switch would be on
everywhere, and the property it protects would be unverifiable from the outside.

### Allowing network access in the pipeline for OCR only

Rejected. Once the pipeline package can open a socket, the guarantee stops being a property
of the package graph and becomes a property of the code inside it, which is much harder to
review and much easier to erode.
