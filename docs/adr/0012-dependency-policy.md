# 0012. Restrict shipping dependencies and classify every one of them

- **Status:** Accepted
- **Date:** 2026-09-29
- **Deciders:** @iamjonatha

## Context

This library handles confidential documents in memory. Every package it references runs in
the same process, with the same access to the same data, and with whatever capabilities its
own transitive graph brings along.

A package that can open a socket, write a file or load a plugin is a different kind of risk
here than it is in a web application. It does not have to be malicious: a diagnostic
uploader, an auto-update check, a telemetry default or a cached temporary file is enough to
put document content somewhere the threat model did not account for.

The usual arguments for taking a dependency — it is popular, it is maintained, it saves
work — do not address that.

## Decision

Shipping packages may reference only Microsoft-maintained framework and
`Microsoft.Extensions.*` packages, plus, in a `Providers.*` package, the integration library
that package exists to adapt.

- `SensitiveDataGateway.Core` has **zero** package references.
- The pipeline package may reference only packages classified `Pure`.
- Third-party packages are permitted in test and benchmark projects, for the test framework,
  coverage collection and BenchmarkDotNet.
- Versions are declared centrally in `Directory.Packages.props`; no inline `Version=` in a
  project file. Transitive pinning is on, NuGet auditing is on at `low`, and Dependabot
  watches the graph.
- Every dependency is classified — `Pure`, sensitive-data processing, network-capable,
  AI-capable, persistence-capable — in
  [`../security/dependency-classification.md`](../security/dependency-classification.md),
  before it is referenced.
- Adding any dependency requires an ADR that names the class, states what the package can do
  that the project does not want it to do, and argues why taking it is better than writing
  the part that is needed.

Two categories are constrained beyond this by other records: network capability is confined
to `Providers.*` by [ADR-0009](0009-no-network-in-core.md), and the document adapters that
must read raw content are isolated into their own packages by
[ADR-0003](0003-package-layout.md).

The rule is deliberately inconvenient in one known place: Phase 10 needs PDF, DOCX and XLSX
readers, and no Microsoft-maintained package covers all of them. Each will be taken through
its own ADR, into its own package, against stated criteria — no network, no external entity
resolution, streaming, maintained, permissively licensed — so that an application installs
the parser it needs and no more.

## Consequences

### Positive

- The supply-chain surface of the core library is essentially the framework.
- "What can reach the network?" is answered by reading the package graph.
- Version conflicts in consuming applications stay rare.
- Native AOT and trimming remain achievable, because the dependencies are AOT-clean.

### Negative

- More code is written here that a package would have provided, and that code has to be
  tested and maintained.
- Document format support is slower to arrive, and each format costs an architectural
  decision rather than a package reference.
- Contributors used to reaching for a package will find this frustrating.

### Neutral

- The policy is the same one DecisionKit applies, so the two repositories are consistent for
  anyone working across both.

## Alternatives considered

### Allow any well-maintained package

Rejected. Maintenance quality is not the property that matters here; capability is. A
well-maintained HTTP-capable package inside the sanitization pipeline is still an egress path
that the package graph no longer rules out.

### Vendor the code instead of referencing it

Rejected as a general policy. It transfers the maintenance and the security response to this
project without removing the original code's capabilities, and it makes upstream fixes
invisible.
