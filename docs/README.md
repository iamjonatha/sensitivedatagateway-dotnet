# Documentation

| Document | Purpose |
| --- | --- |
| [Roadmap](roadmap.md) | What exists today, and what is being considered next |
| [Architecture overview](architecture/overview.md) | Layers, boundaries, the fundamental requirement, non-functional constraints |
| [The pipeline](architecture/pipeline.md) | Each stage's contract: what it does, what it must not do, when it blocks |
| [Domain model](architecture/domain-model.md) | The types the gateway is built from, and the rules they follow |
| [Policies](guides/policies.md) | Writing a policy, the two shipped policies, and what each failure code means |
| [Anonymization](guides/anonymization.md) | The five transformations, the token grammar, correlation scopes and the mapping |
| [Detectors](guides/detectors.md) | What each built-in detector finds, what it misses, and how to add your own |
| [Getting started](guides/getting-started.md) | Sanitizing, inspecting and executing a document in five minutes |
| [Logging and audit](guides/logging-and-audit.md) | The ten log events, the audit record, and why neither can carry content |
| [Testing](guides/testing.md) | The testing package, the synthetic corpus, and which tests may go red |
| [Threat model](security/threat-model.md) | The trust boundary, the assets, fourteen threats and the residual risk |
| [Egress guarantees](security/egress-guarantees.md) | What is guaranteed, what is explicitly not, and how to verify both |
| [Dependency classification](security/dependency-classification.md) | What a referenced package is allowed to be capable of |
| [Decision records](adr/) | Why the architecture is what it is, and what was rejected |

## Planned

Written by the phase that creates the behaviour. The phases are listed in
[the roadmap](roadmap.md). These documents do not exist yet.

| Document | Phase |
| --- | --- |
| `providers/decisionkit.md`, `providers/custom-provider.md` | 8 |
| `guides/dependency-injection.md` | 9 |
| `guides/semantic-detection.md` | 11 |
| `guides/releasing.md`, `guides/aot-and-trimming.md`, `guides/faq.md` | 0 and 12 |

## Rules

- Documentation is written in English, like everything else in this repository.
- A change is not finished until the documentation matches it.
- `architecture/overview.md` is normative. Contradicting it requires an ADR, not a pull
  request comment.
- `security/egress-guarantees.md` bounds every claim made anywhere else. A stronger statement
  in the README, in a package description or in a guide is a bug.
- `adr/` is append-only. A decision is superseded by a new record, never edited into a
  different decision.
- No document contains a real value. Every example is synthetic.
