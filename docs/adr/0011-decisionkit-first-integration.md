# 0011. Adopt DecisionKit as the first provider integration

- **Status:** Accepted
- **Date:** 2026-09-29
- **Deciders:** @iamjonatha

## Context

The gateway's first target is TypeSafe JEV. Reaching JEV means authentication, HTTP
transport, JSON serialization, retry semantics, idempotency, error mapping and forward
compatibility with question and answer types the client does not yet model.

None of that is privacy work, and all of it already exists.
[DecisionKit](https://github.com/iamjonatha/decisionkit-dotnet) is a typed,
provider-independent decision engine built in a separate repository: `DecisionKit.Core`
models questions, answers, scores and probabilities and knows no provider; `DecisionKit.Jev`
implements the JEV protocol behind that abstraction.

The original design anticipated a `SensitiveDataGateway.Provider.Jev` that adapts to a JEV
SDK. DecisionKit is that SDK, one layer better: adapting to DecisionKit means the gateway
never sees a vendor protocol at all, and every provider DecisionKit gains afterwards works
through the same integration.

The question this record settles is the shape of the coupling, because putting DecisionKit
types into the gateway's own abstraction would make the gateway depend on another library's
model of a decision.

## Decision

The gateway keeps its own provider abstraction, as decided in
[ADR-0001](0001-provider-independent-core.md). DecisionKit is the first implementation of it,
in `SensitiveDataGateway.Providers.DecisionKit`.

```text
Application
    |
    v
SensitiveDataGateway            ValidatedPayload
    |
    v
SensitiveDataGateway.Providers.DecisionKit
    |
    v
DecisionKit.Core                IDecisionProvider, questions, answers
    |
    v
DecisionKit.Jev                 protocol, HTTP, auth, retry
    |
    v
TypeSafe JEV
```

The integration package implements
`IDecisionProvider<QuestionSet, DecisionResult>`, and its whole job is mapping:

- a `ValidatedPayload`'s segments become a `DecisionInput`, as text or as structured
  properties;
- the `BundleId` becomes the client request identifier, so an operation is correlatable
  across both libraries;
- the sanitization provenance — policy identifier and version, gateway version — becomes
  request metadata;
- the caller's `CancellationToken` is propagated to the transport;
- DecisionKit's failures surface as they are, without being flattened into a gateway error.

`SensitiveDataGateway.Core` does not reference DecisionKit. The integration package
references `DecisionKit.Core`; the application composes `DecisionKit.Jev` itself, which keeps
the JEV transport out of the gateway's dependency graph entirely.

The gateway ships no JEV package of its own, and models no decision semantics: questions,
choices, scores, probabilities and prompt construction stay in DecisionKit.

## Consequences

### Positive

- No duplication of protocol, transport, authentication, retry or serialization.
- The gateway never holds a vendor credential and never opens a socket to a vendor.
- Every provider DecisionKit supports later is reachable through the same integration.
- Two independently testable boundaries: "may this be sent" and "how is it sent".
- Both libraries stay releasable on their own schedule.

### Negative

- An application composes two libraries rather than one, and has to understand where each
  one's responsibility ends.
- The integration tracks DecisionKit's pre-`1.0.0` public API, which may change in a minor
  release.
- A JEV feature with no DecisionKit expression is reachable only through DecisionKit's own
  provider-specific API, not through the gateway.

### Neutral

- DecisionKit is the first integration, not a privileged one. Its package sits beside any
  other `Providers.*` package and gets no special access.

## Alternatives considered

### Reuse DecisionKit's `IDecisionProvider` as the gateway's own abstraction

Rejected. It would put a third-party model of a decision into `SensitiveDataGateway.Core`,
tie the gateway's release cadence to DecisionKit's, and place a network-capable package one
reference away from the types that hold raw documents. The gateway's own abstraction is a
handful of lines; the coupling would not have been.

### Implement a JEV client inside the gateway

Rejected. It duplicates a solved problem, doubles the protocol surface to maintain, and puts
HTTP and credentials in a project whose selling point is that it does neither.

### Have the application call DecisionKit directly with the sanitized text

Rejected. It reintroduces the failure this project exists to prevent: an entry point that
accepts a string, where nothing stops the caller from passing the original.
