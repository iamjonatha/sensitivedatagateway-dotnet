# 0004. Fail closed, and make a block a result rather than an exception

- **Status:** Accepted
- **Date:** 2026-09-29
- **Deciders:** @iamjonatha

## Context

Every stage of the pipeline can end in a state that is neither "safe" nor "definitely
unsafe": a detector timed out, an entity was recognized with a confidence of 0.62, an
attachment could not be parsed, a transformation could not be applied to one of four
segments.

A library has two ways to handle that. It can transmit and warn, which is what most scanning
tools do because it keeps the happy path working. Or it can refuse, which means a real
document will occasionally be refused for a reason the operator finds unconvincing.

There is a second, independent question: how the refusal reaches the caller. If a block is
an exception, then the caller reaches the safe path only by writing a `catch`, and the code
that forgets to write one gets the unsafe behaviour. That is the wrong default in a security
boundary.

## Decision

Uncertainty blocks.

```text
SAFE        -> transmit
UNSAFE      -> block
UNKNOWN     -> block
ERROR       -> block
UNRESOLVED  -> block
```

There is no "warning only" mode, no configuration switch that converts a validation failure
into a warning, and no permissive fallback when a policy is invalid. An invalid policy is a
configuration failure at construction time, not an empty rule set at evaluation time.

A **block is a result**:

```csharp
var result = await gateway.ExecuteAsync(bundle, request, cancellationToken);

if (result.Status is GatewayStatus.Blocked)
{
    return Reject(result.Report);
}

return Handle(result.Value);
```

`result.Value` is available only when the status is `Completed`; reading it otherwise throws.
A block is an expected outcome of a correctly functioning gateway, it carries a report naming
every check that failed, and it does not require exception handling.

A **failure is an exception**: a detector threw, an extractor crashed, the configuration is
unusable. It propagates to the boundary that can act on it.

Both prevent egress. Neither requires the caller to have remembered anything.

A bypass, if one is ever justified, must be a separate type in a separate package with a name
that is obvious in a pull request diff. It must never be a boolean on an options object.

## Consequences

### Positive

- The safe path is the path a caller gets by writing the obvious code.
- Refusals are visible and diagnosable, rather than being a warning nobody reads.
- The behaviour is the same in development and in production; there is no permissive mode to
  leave switched on.
- "Did this leak?" has a deterministic answer.

### Negative

- Documents will be blocked that a human would have allowed. Tuning the policy is real
  operational work, and `InspectAsync` exists because of it.
- A detector outage stops processing instead of degrading it. That is intended and it is a
  real availability cost.
- A caller who wants best-effort behaviour cannot get it from this library.

### Neutral

- `InspectAsync` becomes load-bearing: it is how an operator finds out why a document would
  be blocked without transmitting it.

## Alternatives considered

### Transmit with a warning when uncertain

Rejected. The warning is emitted after the data has left. It is a record of a leak, not a
prevention of one.

### A configurable enforcement level

Rejected. A switch that relaxes a security boundary is set once, during an incident, and
never set back. If the boundary is negotiable it is not a boundary.

### A block as an exception

Rejected. It makes `catch` a prerequisite for correctness and turns an expected outcome into
control flow through the exception system.
