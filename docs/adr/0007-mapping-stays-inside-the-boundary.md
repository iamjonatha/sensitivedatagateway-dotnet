# 0007. Keep the pseudonym mapping inside the trust boundary

- **Status:** Accepted
- **Date:** 2026-09-29
- **Deciders:** @iamjonatha

## Context

Pseudonymization replaces `Mario Rossi` with `[PERSON_001]` and keeps a mapping so that the
application can read the answer in its own terms. The mapping is more concentrated than the
document it came from: it is a list of exactly the sensitive values, indexed and stripped of
the surrounding prose that made them hard to find.

It is therefore the single most dangerous artifact the gateway produces, and the temptations
around it are obvious. Sharing it across operations makes tokens stable, which makes results
more useful. Persisting it makes re-identification possible later. Sending a token back to
the provider for clarification would make some workflows easier.

Each of those turns the mapping into a durable, reachable re-identification key.

## Decision

The mapping never crosses the boundary, and nothing in the design makes it possible to try.

1. It is not a member of `SanitizedDocument` as passed to validation, and not a member of
   `ValidatedPayload`.
2. A `Providers.*` package cannot name `PseudonymMap`, because it does not reference the
   types. This is verified by an architecture test.
3. Its default lifetime is the operation. It is volatile, and it is released when the
   operation ends.
4. Persistence is opt-in through `IPseudonymStore`, and a persisting implementation must
   encrypt at rest, scope the entries and expire them. No persisting store ships as a
   default.
5. Re-identification is a local operation of the application, invoked by an explicitly named
   method. The gateway exposes no channel through which a provider could request the value
   behind a token.
6. The default correlation scope is `Bundle`: a token means one thing across an email and its
   attachments, and nothing outside that bundle. `Session` and `Tenant` are opt-in, and the
   correlation they create is documented next to the switch that enables them.

## Consequences

### Positive

- A compromised provider gains tokens, not identities.
- The most dangerous artifact has the shortest life by default.
- Cross-document correlation is a decision somebody made, not a side effect of a default.
- The package boundary makes the rule checkable rather than aspirational.

### Negative

- Tokens are not stable across bundles by default, so an application that wants to correlate
  two emails about the same person has to opt in and accept the consequence.
- Re-identification after the process ends requires the application to have persisted the
  map itself, under its own controls.
- Workflows that would want the provider to ask "who is `PERSON_001`?" are not supported, by
  design.

### Neutral

- This is the boundary between pseudonymization and anonymization made concrete.
  Pseudonymized data remains personal data; the project says so in
  [`../security/egress-guarantees.md`](../security/egress-guarantees.md).

## Alternatives considered

### A shared mapping store across operations

Rejected as a default. It maximizes token usefulness and maximizes blast radius at the same
time, and it converts a per-operation secret into permanent infrastructure.

### Letting the provider request re-identification

Rejected outright. It is a re-identification API on the wrong side of the boundary; the
existence of the endpoint is the vulnerability, independently of who is authorized to call
it.

### Deterministic tokens derived from the value

Rejected as a default. A token derived from the value is a hash of the value, and a hash of a
short value — a phone number, a tax code — is reversible by enumeration. It is available as
`Hash`, with its correlation and reversal properties documented, for callers who need
cross-operation stability and have accepted the trade.
