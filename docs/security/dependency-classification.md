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
7. **A `Documents.*` package may reference one parser**, for the format it adapts, against
   the six criteria in [ADR-0018](../adr/0018-format-adapters-may-take-a-parser.md): no
   network, no external entity resolution, bounded reads, maintained, permissively licensed,
   no code-loading surface. Nothing else travels with it.

---

## Where each class is expected to appear

```text
Core                     Pure only, and in practice nothing at all
  ^
SensitiveDataGateway     Pure
  ^
Documents.*              Sensitive-data processing
Detection.*              AI-capable; network-capable when the model is a service
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
| `Microsoft.Extensions.Configuration.Abstractions` | Pure | `Extensions` | [0017](../adr/0017-di-composes-the-gateway-the-caller-passes-the-policy.md) | Read key by key; the reflection binder is never used |
| `Microsoft.Extensions.Logging.Abstractions` | Pure | `Extensions`, pipeline | [0008](../adr/0008-privacy-safe-observability.md) | Source-generated logging only |
| `DecisionKit.Core` | Pure | `Providers.DecisionKit` | [0011](../adr/0011-decisionkit-first-integration.md) | Abstractions only; the network lives in `DecisionKit.Jev`, which the application composes |
| `MimeKitLite` | Sensitive-data processing | `Documents.Mime` | [0019](../adr/0019-mimekitlite-for-mime.md) | The parser without S/MIME and OpenPGP, which is where MimeKit reaches the network. No package dependencies on `net8.0` or `net10.0`. Enforced by `NetworkCapabilityTests` |
| `PdfPig` | Sensitive-data processing | `Documents.Pdf` | [0020](../adr/0020-pdfpig-for-pdf.md) | Seven assemblies, no package dependencies. XMP is read as bytes, never as XML. Neither trimmable nor AOT-compatible. Enforced by `NetworkCapabilityTests` |
| `DocumentFormat.OpenXml` | Sensitive-data processing | `Documents.OpenXml` | [0021](../adr/0021-openxml-sdk-for-office-documents.md) | Three assemblies, all Microsoft and MIT. Document type declarations are prohibited by the reader, so XXE fails closed by construction. Neither trimmable nor AOT-compatible. Enforced by `NetworkCapabilityTests` |

---

## A capability taken without a package

`SensitiveDataGateway.Detection.Presidio` adds no row above, because it adds no reference: it
references `Core` and the framework, and speaks the analyzer's HTTP API itself. Nothing is
installed, and no third-party code runs in the host process.

It is listed here anyway, because the classification is about capability rather than about
package count. The package is **network-capable and reads raw content**, which is the one
combination [ADR-0009](../adr/0009-no-network-in-core.md) otherwise forbids, and point 3 of
that record is what permits it: a model the deployment runs itself is inside the boundary.
`NetworkCapabilityTests` does not vouch for this assembly, deliberately — the whole point of
it is to transmit. What constrains it instead is
[ADR-0023](../adr/0023-semantic-detection-is-a-contract-not-a-model.md): `ForHostedService`
produces a detector the strict composition path refuses, and the audit record names the side
of the boundary that read the document. The residual risk is written down as N10 in
[the egress guarantees](egress-guarantees.md).

---

## Candidates, and why they are not taken yet

| Package | Class | Status |
| --- | --- | --- |
| A local model runtime | AI-capable | Behind `ISemanticDetector`, if a runtime ever earns its weight in the package |
| A cloud document-intelligence service | Network-capable, sensitive-data processing | Never on the strict path. If it ships at all, it ships as an explicitly named out-of-boundary adapter |
| An OCR engine | Sensitive-data processing, frequently network-capable | Never. `ITextRecognizer` is the shape, and the host brings the engine. [0022](../adr/0022-an-ocr-abstraction-with-no-implementation.md) records why no engine ships, and why a remote one is reachable only through `RecognizingExtractor.CreateOutsideTheTrustBoundary` |
