# 0001. Keep the gateway independent of any decision provider

- **Status:** Accepted
- **Date:** 2026-09-29
- **Deciders:** @iamjonatha

## Context

The project starts from a concrete need: send documents to TypeSafe JEV for classification
without sending the confidential parts. The shortest path to that need is a JEV client with
a sanitization step bolted on before the request is built.

That path has two problems. The sanitization step becomes a feature of a vendor client, so
it is only as trustworthy as the client's own discipline about which method the caller
reaches for. And the security boundary — the part with real value — becomes unavailable to
anyone using a different provider, which is most people.

The boundary is also the part that generalizes. "What may I send to an external AI service?"
is not a JEV question.

## Decision

`SensitiveDataGateway.Core` models documents, entities, policy, sanitization and validation,
and knows no provider. It contains no vendor URL, header, credential, DTO, endpoint name or
protocol type, and it has zero package references.

Egress is expressed as a generic abstraction:

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

The gateway contributes the payload. The application contributes the request, shaped by
whatever decision engine it uses. Every provider integration lives in its own
`SensitiveDataGateway.Providers.*` package, depends on `Core`, and is the only place where
network egress is allowed to exist.

The gateway does not model decision semantics — questions, choices, scores, confidences — and
does not build prompts. Its job ends when the payload is validated.

## Consequences

### Positive

- The boundary is reusable with any decision engine, and with a local rules engine or a mock.
- `Core` is testable with no network, no credentials and no vendor account.
- The threat model is small: exactly one package class can reach the network.
- A provider change does not touch the policy, the detectors or the validator.

### Negative

- Two abstractions exist where a direct client would have none, which is more code and more
  tests.
- An application must compose two things — the gateway and its decision engine — instead of
  one.
- A provider capability with no neutral expression is reachable only through the provider's
  own API, so "swap the provider" has documented limits.

### Neutral

- The project is positioned as privacy infrastructure rather than as a smarter SDK, which
  changes how the README and the package descriptions are written.

## Alternatives considered

### A JEV client with sanitization built in

Rejected. It makes the security boundary a feature of a vendor client, and it restricts the
boundary to the users of that one vendor. It also puts raw document types and vendor
transport types in the same assembly, which is precisely the adjacency the project exists to
remove.

### Reusing another library's provider abstraction as the gateway's own

Rejected. It would make `Core` depend on that library's release cadence and its model of a
decision, and it would put a network-capable package one reference away from the types that
hold raw content. The gateway's own abstraction is four lines; the coupling would not have
been.
