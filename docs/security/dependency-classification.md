# Dependency classification

A library that handles confidential documents cannot add a dependency because it is
convenient. Every package referenced by a shipping project is classified here before it is
referenced, and the classification is what the ADR approving it argues about.

The general dependency policy — Microsoft-maintained packages only, central versioning, an
ADR per addition — is in [`../../CONTRIBUTING.md`](../../CONTRIBUTING.md#6-dependency-policy).
This document is the security half of that decision.

---

## The classes

| Class | Meaning | Where it may be referenced |
| --- | --- | --- |
| **Pure** | Computes. No I/O, no ambient state, no reflection over consumer types | Anywhere, including `Core` |
| **Sensitive-data processing** | Reads raw document content | The pipeline and document adapters only. Never `Core` |
| **Network-capable** | Can open a socket, by design or incidentally | `Providers.*` only |
| **AI-capable** | Runs or calls a model | A named detector package only, and only local or self-hosted on the strict path |
| **Persistence-capable** | Can write to disk, a cache or a database | A named store package only, opt-in, never a default |

A package takes the strongest class that applies. A document parser that can also fetch a
remote schema is network-capable, not sensitive-data processing.

---

## The rules

1. **`SensitiveDataGateway.Core` has zero package references.** Not "few". Zero.
2. **The pipeline package may reference only Pure packages.** It reads raw content, so it
   must not be able to send it anywhere.
3. **Network capability is confined to `Providers.*`.** That is the only place egress is
   allowed to exist, which is what makes the threat model small enough to reason about.
4. **A transitive dependency is classified too.** A Pure package that pulls in an HTTP client
   is not Pure.
5. **Convenience is never a justification.** "It saves fifty lines" does not outrank "it can
   open a socket in a package that reads raw email".
6. **Every addition needs an ADR** recording the class, what the package can do that the
   project does not want, and why writing it is worse than taking it.

---

## Where each class is expected to appear

```text
Core                     Pure only, and in practice nothing at all
  ^
SensitiveDataGateway     Pure
  ^
Documents.*              Sensitive-data processing
Detection.Semantic.*     AI-capable, local or self-hosted
Anonymization.Stores.*   Persistence-capable
  ^
Providers.*              Network-capable
Extensions               Pure (Microsoft.Extensions.*)
Testing                  Pure
```

The arrow points at what is depended upon. Capability increases downward in that list, and
never travels back up.

---

## Registry

Filled in as dependencies are taken. A row is added by the pull request that adds the
reference, together with its ADR.

| Package | Class | Referenced by | ADR | Notes |
| --- | --- | --- | --- | --- |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | Pure | `Extensions` | [0012](../adr/0012-dependency-policy.md) | Registration only |
| `Microsoft.Extensions.Options` | Pure | `Extensions` | [0012](../adr/0012-dependency-policy.md) | Configuration binding without reflection |
| `Microsoft.Extensions.Logging.Abstractions` | Pure | `Extensions`, pipeline | [0008](../adr/0008-privacy-safe-observability.md) | Source-generated logging only |
| `DecisionKit.Core` | Pure | `Providers.DecisionKit` | [0011](../adr/0011-decisionkit-first-integration.md) | Abstractions only; the network lives in `DecisionKit.Jev`, which the application composes |

---

## Candidates, and why they are not taken yet

| Package | Class | Status |
| --- | --- | --- |
| A PDF text extractor | Sensitive-data processing | Needed in Phase 10. Selection criteria: no network, no external entity resolution, streaming, maintained, permissive licence |
| A DOCX/XLSX reader | Sensitive-data processing | Same, Phase 10 |
| Microsoft Presidio | AI-capable, network-capable as deployed | Phase 11, as a separate package, documented as requiring a local deployment |
| A local model runtime | AI-capable | Phase 11, behind `ISemanticDetector` |
| A cloud document-intelligence service | Network-capable, sensitive-data processing | Never on the strict path. If it ships at all, it ships as an explicitly named out-of-boundary adapter |
