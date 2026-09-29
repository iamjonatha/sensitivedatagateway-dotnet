# 0003. Ship a consolidated package layout, not one package per pipeline stage

- **Status:** Accepted
- **Date:** 2026-09-29
- **Deciders:** @iamjonatha

## Context

The design this project started from proposes nine packages: `Core`, `Policies`, `Detection`,
`Anonymization`, `Validation`, `Documents.Abstractions`, `Providers.Abstractions`,
`Provider.Jev` and `Extensions`, with format adapters on top.

The motivation is sound — a consumer should not install a parser stack to redact a string,
and every component should be replaceable. But three of those packages would contain only
interfaces, and four more would contain code whose only dependency is `Core` and which every
consumer installs together anyway. Splitting them costs a project, a package identity, a
version to keep aligned and a `PublicAPI` pair each, and buys separation that the dependency
rules already provide.

There is also a boundary argument against the split. `Validation` produces `ValidatedPayload`
and is the only thing allowed to. The narrower the assembly that can construct it, the
stronger the claim — but the pipeline stages are useless apart, so the practical assembly
boundary that matters is "the pipeline" against "a provider", not "validation" against
"anonymization".

## Decision

Ship six packages.

| Package | Contents |
| --- | --- |
| `SensitiveDataGateway.Core` | Document model, pipeline stage types, `SensitiveEntity`, policy model, audit model, every abstraction, error contracts. Zero package references |
| `SensitiveDataGateway` | The pipeline: detection host, built-in deterministic detectors, policy engine, anonymizers, privacy validator, the gateway façade |
| `SensitiveDataGateway.Extensions` | Dependency injection, options binding, logging integration |
| `SensitiveDataGateway.Testing` | Fake and recording providers, leakage assertions, synthetic corpora |
| `SensitiveDataGateway.Providers.*` | One package per decision integration. The only place network egress exists |
| `SensitiveDataGateway.Documents.*` | One package per document format |

Abstractions live in `Core` rather than in separate `*.Abstractions` packages, because `Core`
already is the abstractions package: it has zero dependencies and no behaviour that reaches
outside the process.

`ValidatedPayload` is constructible only from within the assembly that contains the
validator, and the architecture test that proves it is written against that assembly
boundary.

Splitting a package later is a source-compatible operation if the namespaces are right, so
the namespaces are laid out as if that split had already happened: `Detection`, `Policies`,
`Anonymization` and `Validation` are namespaces from day one. A package is extracted when a
consumer can demonstrably use the rest without it.

## Consequences

### Positive

- Six package identities to version, document and release instead of nine or more.
- A consumer installs one package and gets a working pipeline.
- The namespace layout keeps the option of splitting open at no cost.
- The boundaries that carry security weight — `Core` against the pipeline, the pipeline
  against `Providers.*` — are still real assembly boundaries.

### Negative

- A consumer who wants only the policy engine also gets the detectors. They are small and
  have no dependencies, but they are there.
- Replacing the built-in validator with a different implementation means implementing the
  abstraction rather than not installing a package.
- The layout differs from the nine-package design this project started from, which is why
  that design is recorded here rather than assumed.

### Neutral

- Format adapters and provider integrations are still separate packages, which is where the
  dependency weight and the network capability actually live.

## Alternatives considered

### The nine packages described above

Rejected for an MVP. It is the right shape for a mature project with independent consumers
for each stage, and nothing here prevents arriving at it. Starting there pays the cost before
there is evidence anyone wants the separation.

### A single package

Rejected. It would put the provider integration, and therefore the network, in the same
assembly as the raw document types. That adjacency is the one the project exists to prevent.
