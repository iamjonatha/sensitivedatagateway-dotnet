# Contributing to SensitiveDataGateway

Thanks for considering a contribution. This document is the contract between contributors and
maintainers. Everything here is enforced by review, and where possible by tooling.

This project is a security boundary. Some of the rules below would be pedantic in an ordinary
library and are not negotiable here.

---

## 1. Language policy

**English is the only language of this project.** No exceptions.

| Artifact | Language |
| --- | --- |
| Source code, identifiers, XML documentation | English |
| README, documentation, ADRs, samples | English |
| Commit messages | English |
| Pull request titles, descriptions, review comments | English |
| Issue titles and descriptions | English |
| Log messages, exception messages, diagnostics | English |
| Code comments | English |

Non-English contributions are closed with a request to resubmit in English.

---

## 2. Commit conventions

Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/):

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types:** `feat`, `fix`, `perf`, `refactor`, `docs`, `test`, `build`, `ci`, `chore`, `revert`.

**Scopes:** `core`, `detection`, `policy`, `anonymization`, `validation`, `providers`,
`documents`, `extensions`, `testing`, `docs`, `build`, `ci`, `samples`.

**Rules:**

- Subject in English, imperative mood, lower case, no trailing period, max 72 characters.
- Body explains *why*, not *what*. The diff already says what.
- Breaking changes use `!` after the scope and a `BREAKING CHANGE:` footer.
- Reference issues with `Closes #123` / `Refs #123`.
- **Never add `Co-authored-by` trailers.** Not for pair programming, not for AI assistants,
  not for rebased work. Authorship is the commit author field and nothing else.
- Never add tool attribution footers of any kind.
- **Never include a real document, a real value or a real credential in a commit**, including
  in a test fixture. Fixtures are synthetic.

Good:

```
feat(validation): scan sanitized output for normalized entity values

An exact-match scan missed values that survived with different casing or
spacing, which is the shape a partially applied transformation leaves behind.

Closes #42
```

Bad:

```
Aggiunto validatore
fix leak
feat(validation): added the scanner.

Co-authored-by: Someone <someone@example.com>
```

---

## 3. Branching and pull requests

- `main` is always releasable. Direct pushes are disabled.
- Work happens on `feat/<short-slug>`, `fix/<short-slug>`, `docs/<short-slug>`.
- One logical change per pull request. Split unrelated refactors out.
- Rebase onto `main` before requesting review.
- Pull requests are squash-merged; the squash subject must itself be a valid Conventional
  Commit.

---

## 4. Architectural rules

Non-negotiable. A pull request that violates one is rejected regardless of quality.

1. **`SensitiveDataGateway.Core` has zero package references** and performs no I/O.
2. **Dependency direction is one-way**, always towards `Core`. Never the reverse.
3. **A provider accepts only `ValidatedPayload`**, and only the privacy validator constructs
   one.
4. **Nothing that reads raw content may reach the network.** Network capability exists only
   in `SensitiveDataGateway.Providers.*`. See [ADR-0009](docs/adr/0009-no-network-in-core.md).
5. **Fail closed.** Unknown, unresolved and error all block. No configuration converts a
   block into a warning. See [ADR-0004](docs/adr/0004-fail-closed.md).
6. **Detection, policy and anonymization stay separate.** A detector never decides; the
   policy never rewrites text; the anonymizer never classifies.
7. **The pseudonym map never crosses the boundary**, and no provider package can name it.
8. **No document content reaches a log, a metric, a trace or an exception**, at any level.
   See [ADR-0008](docs/adr/0008-privacy-safe-observability.md).
9. **Document content is data, never instruction.** No stage branches on content in a way
   that changes a security decision.
10. **Every public asynchronous method accepts a `CancellationToken`** and propagates it.
11. **No synchronous API performs I/O.**
12. **A type is `public` only if a real consumer must use it.** Everything else is `internal`.
13. **A limit blocks; it never truncates.**

---

## 5. Coding standards

- Target frameworks: `net8.0` and `net10.0`. Code compiles cleanly on both.
- Nullable reference types enabled everywhere. Warnings are errors.
- File-scoped namespaces. Namespace matches folder structure.
- `using` directives outside the namespace, `System` first.
- Naming: PascalCase for types and members, `camelCase` for locals and parameters,
  `_camelCase` for private instance fields, `s_camelCase` for private static fields,
  `TPascalCase` for type parameters, `I` prefix for interfaces, `Async` suffix for
  asynchronous methods. Enforced by `.editorconfig`.
- Prefer immutable types: `readonly record struct`, `sealed` classes, `readonly` fields.
  Every pipeline stage type is immutable.
- Keep cyclomatic complexity low. If a method needs a comment to explain its flow, split it.
- Comment only non-obvious logic, format quirks and deliberate workarounds. Do not narrate
  code.
- Every public member that is not self-evident carries XML documentation.
- No `try`/`catch` that only rethrows or swallows. Let errors reach the boundary that can
  decide — and in this project, that boundary blocks.
- No optional parameters on public API. Use overloads or an options object.
- Regular expressions are source-generated, have an explicit timeout, and come with a
  backtracking test.

Run before pushing:

```bash
dotnet format SensitiveDataGateway.slnx --verify-no-changes
dotnet build SensitiveDataGateway.slnx -c Release
dotnet test --solution SensitiveDataGateway.slnx -c Release
```

---

## 6. Dependency policy

- Shipping packages may reference **only** Microsoft-maintained framework and
  `Microsoft.Extensions.*` packages, plus — in a `Providers.*` package — the integration
  library that package exists to adapt.
- `SensitiveDataGateway.Core` targets **zero** package references.
- The pipeline package may reference only packages classified `Pure`.
- Third-party packages are allowed in test and benchmark projects, for the test framework,
  coverage collection and BenchmarkDotNet.
- All versions are declared centrally in `Directory.Packages.props`. No inline `Version=`.
- **Adding any dependency requires an ADR**, and a row in
  [`docs/security/dependency-classification.md`](docs/security/dependency-classification.md)
  naming its class and what it is capable of. See
  [ADR-0012](docs/adr/0012-dependency-policy.md).

---

## 7. Testing requirements

- Every behavioural change ships with tests. Bug fixes start with a failing test.
- Tests are deterministic. No real clocks, no real network, no real delays.
- **No test fixture contains a real value.** No real name, address, IBAN, tax code, key or
  document. The synthetic corpus in `SensitiveDataGateway.Testing` is the source for all of
  it, and it is annotated with ground truth.
- A change to detection, policy, anonymization or validation also runs the egress security
  suite in `tests/SensitiveDataGateway.Security.Tests`.
- A change that weakens a guarantee in
  [`docs/security/egress-guarantees.md`](docs/security/egress-guarantees.md) must delete a
  test from that suite. That deletion is a reviewable event and needs an ADR.
- Test naming: `MethodOrScenario_ExpectedBehaviour`.
- Arrange/Act/Assert, one behaviour per test.

See [`docs/guides/testing.md`](docs/guides/testing.md) for what the testing package offers,
what `LeakAssert` does and does not prove, and how the security suite is organized.

---

## 8. Documentation requirements

A change is not done until the documentation matches it:

- New or changed public API → XML documentation.
- New capability → a guide in `docs/guides/`.
- Architectural decision, new dependency, or a change to what may be logged → an ADR in
  `docs/adr/`.
- New component that can read raw content, write a log or reach the network → a row in
  [`docs/security/threat-model.md`](docs/security/threat-model.md).
- User-visible change → a `CHANGELOG.md` entry under `## [Unreleased]`.

`docs/architecture/overview.md` is normative. Contradicting it requires an ADR, not a review
comment. `docs/adr/` is append-only.

---

## 9. Pull request checklist

- [ ] Everything is in English, including the commit messages.
- [ ] No `Co-authored-by` or tool attribution trailers.
- [ ] Commits follow Conventional Commits.
- [ ] `dotnet format --verify-no-changes` passes.
- [ ] `dotnet build -c Release` is warning-free on both target frameworks.
- [ ] `dotnet test -c Release` passes, including the egress security suite.
- [ ] `Core` has no package reference and no I/O.
- [ ] No new network capability outside `Providers.*`.
- [ ] No document content reaches a log, a metric, a trace or an exception.
- [ ] No fixture contains a real value.
- [ ] Cancellation is honoured end to end.
- [ ] Public API additions are documented, intentional and recorded in `PublicAPI.Unshipped.txt`.
- [ ] `CHANGELOG.md` is updated.

---

## 10. Releasing

See `docs/guides/releasing.md` once Phase 0 has produced it.

---

## 11. Code of conduct

Participation is governed by [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md).
