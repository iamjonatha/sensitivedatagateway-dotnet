# SensitiveDataGateway

**A fail-closed privacy boundary between your documents and an external AI service.**

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Your application wants an AI service to answer a question about a document: is this an
invoice, which team should handle it, how urgent is it. The document is an email with
attachments, and it contains names, addresses, bank details, contract references and
occasionally a password somebody pasted into a signature.

The decision does not need any of that. SensitiveDataGateway removes it first, checks that it
is gone, and only then lets anything travel.

```text
Raw document
     |
     v
extraction -> detection -> policy -> anonymization -> validation
     |
     v
ValidatedPayload        <- the only type a decision provider can accept
     |
     v
Decision provider
```

> This is **not** an SDK for any AI service, and not a PII regular-expression scanner. It is
> the boundary: what may I afford to send, and can I prove it left nothing behind?

---

## Status

**Pre-implementation.** The architecture, the threat model and the decision records are
written; the code starts at the repository foundation. Nothing is released, and the public
API is unstable until `1.0.0`.

Start with the [architecture overview](docs/architecture/overview.md), then the
[roadmap](docs/roadmap.md).

---

## Why

The usual integration looks like this:

```csharp
var answer = await ai.Classify(email.Body);   // the whole thing, original values included
```

It works, and it sends everything. The safety of that line depends entirely on the caller
having thought about it, in every place it is written, forever.

SensitiveDataGateway makes the unsafe version not compile. Each stage of the pipeline has its
own type, a provider accepts only a `ValidatedPayload`, and only the privacy validator can
produce one:

```csharp
var result = await gateway.ExecuteAsync(bundle, questions, cancellationToken);

if (result.Status is GatewayStatus.Blocked)
{
    return Reject(result.Report);   // it says which check refused, and quotes no value
}

return Handle(result.Value);
```

A block is a result, not an exception. Reaching the safe path never depends on remembering
to write a `catch`.

---

## What it does

| Stage | What happens |
| --- | --- |
| **Extraction** | Email, PDF, DOCX, XLSX and plain text become ordered text segments, each traceable to the page, paragraph or cell it came from |
| **Detection** | Deterministic detectors first — IBAN with its checksum, credit cards with Luhn, tax codes, tokens, keys — then, optionally, a local semantic detector |
| **Policy** | An explicit, versioned policy decides per entity type: preserve, mask, redact, pseudonymize, hash, encrypt, block or hand to a human |
| **Anonymization** | `Mario Rossi` becomes `[PERSON_001]`, stable across an email and its attachments. The mapping stays with you |
| **Validation** | Ten checks, including a scan of the output for every value found in the input. Failure blocks |
| **Egress** | The provider receives sanitized content, a policy version and nothing else |

The original:

```text
Buongiorno Mario Rossi, la fattura 2026/123 relativa al contratto con
ACME S.p.A. verrà saldata sul conto IT60X0542811101000000123456.
```

What the provider sees:

```text
Buongiorno [PERSON_001], la fattura [INVOICE_001] relativa al contratto con
[ORGANIZATION_001] verrà saldata sul conto [IBAN_001].
```

Enough to answer "is this an invoice?". Not enough to know whose.

---

## Design principles

1. **Privacy by architecture.** The safe path is the only one the types allow.
2. **Least data.** Send the minimum the decision needs.
3. **Fail closed.** Unknown, unresolved and error all mean block. There is no warning-only
   mode. [ADR-0004](docs/adr/0004-fail-closed.md)
4. **Deterministic before semantic.** Do not call a model to find an IBAN.
5. **AI detects, policy decides.** A model proposes; it never authorizes an egress.
6. **The network stays out.** Nothing that reads raw content can open a socket.
   [ADR-0009](docs/adr/0009-no-network-in-core.md)
7. **No raw data in telemetry.** Logs, metrics, traces and exceptions are a second egress
   channel, and they are treated as one. [ADR-0008](docs/adr/0008-privacy-safe-observability.md)
8. **The mapping never leaves.** Re-identification is local, always.
   [ADR-0007](docs/adr/0007-mapping-stays-inside-the-boundary.md)
9. **Provider agnostic.** The core knows no vendor. [ADR-0001](docs/adr/0001-provider-independent-core.md)
10. **Composability.** Every component is replaceable without touching the boundary.

---

## Packages

| Package | Purpose | Dependencies |
| --- | --- | --- |
| `SensitiveDataGateway.Core` | Document model, pipeline stage types, policy model, every abstraction | none |
| `SensitiveDataGateway` | The pipeline: detectors, policy engine, anonymizers, validator, façade | `Core` |
| `SensitiveDataGateway.Extensions` | Dependency injection, configuration, logging | `Core`, pipeline |
| `SensitiveDataGateway.Testing` | Fake providers, leakage assertions, synthetic corpora | `Core` |
| `SensitiveDataGateway.Providers.*` | One per decision integration. The only place egress exists | `Core` |
| `SensitiveDataGateway.Documents.*` | One per document format | `Core` |

Target frameworks: `net8.0` and `net10.0`. Windows, Linux and macOS.

---

## Relationship to DecisionKit

[DecisionKit](https://github.com/iamjonatha/decisionkit-dotnet) is a typed,
provider-independent decision engine, with a TypeSafe JEV provider. It answers *how do I ask
the question*. This project answers *what am I allowed to send*.

```text
SensitiveDataGateway -> SensitiveDataGateway.Providers.DecisionKit -> DecisionKit -> TypeSafe JEV
```

DecisionKit is the first implemented integration, not a privileged one. The gateway
implements no vendor protocol, holds no vendor credential and opens no socket. See
[ADR-0011](docs/adr/0011-decisionkit-first-integration.md).

---

## What this does not promise

No automatic detector is complete. A name it does not recognize, an identifier in an
unexpected format, a project referred to by a nickname: those pass, and no combination of
detectors removes that.

So the project does not claim that no sensitive data can escape. It claims this:

1. exposure is minimized rather than accepted;
2. several independent detectors run, and deterministic ones are preferred;
3. the transformation follows an explicit, versioned policy;
4. every payload is validated after sanitization and before egress;
5. uncertainty blocks;
6. the safe path is the only convenient one;
7. leakage is tested systematically, and those tests are part of the contract.

It is also **not a compliance product**, and pseudonymized data is still personal data. The
full statement of what is and is not guaranteed is in
[`docs/security/egress-guarantees.md`](docs/security/egress-guarantees.md). Any claim
anywhere that is stronger than that document is a bug.

---

## Documentation

| Document | Contents |
| --- | --- |
| [Roadmap](docs/roadmap.md) | What exists today, and what is being considered next |
| [Architecture overview](docs/architecture/overview.md) | Layers, boundaries, the fundamental requirement. Normative |
| [The pipeline](docs/architecture/pipeline.md) | What each stage does, and what it is forbidden to do |
| [Domain model](docs/architecture/domain-model.md) | The types, and the rules they follow |
| [Threat model](docs/security/threat-model.md) | Fourteen threats, their mitigations, and the residual risk |
| [Egress guarantees](docs/security/egress-guarantees.md) | What is guaranteed, what is not, and how to verify it |
| [Dependency classification](docs/security/dependency-classification.md) | What a package is allowed to be able to do |
| [Decision records](docs/adr/) | Why the architecture is what it is, and what was rejected |
| [Contributing](CONTRIBUTING.md) | Development rules, commit conventions, review gates |
| [Security policy](SECURITY.md) | Reporting a vulnerability, and the properties treated as security requirements |

---

## Contributing

Read [`CONTRIBUTING.md`](CONTRIBUTING.md) and, if you are an AI coding agent,
[`AGENTS.md`](AGENTS.md). The project is English-only, including issues and commit messages.

A pull request that weakens a guarantee in
[`docs/security/egress-guarantees.md`](docs/security/egress-guarantees.md) is rejected
regardless of its quality.

---

## Security

Do not open a public issue for a security problem, including a document that leaks a value
the gateway should have caught. See [`SECURITY.md`](SECURITY.md).

---

## License

[MIT](LICENSE) © Jonatha Panni
