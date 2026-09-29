# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

While the major version is `0`, the public API may change in any minor release.

## [Unreleased]

### Added

- Architecture overview, the normative description of the layers, the fundamental
  requirement and the trust boundary.
- The pipeline contract: what each stage does, what it is forbidden to do, and what it must
  produce for the next one to accept it.
- Domain model: the identity, document, detection, policy, sanitization, validation, audit
  and error types, and the rules they follow.
- Threat model covering fourteen threats, their mitigations and the residual risk of each.
- Egress guarantees: twelve guaranteed properties, eight explicit non-guarantees, and how to
  verify both.
- Dependency classification: the five capability classes, where each may appear, and the
  registry every future dependency is added to.
- Roadmap, and the list of what is deliberately not planned.
- Decision records 0001 to 0012: provider independence, target frameworks, package layout,
  fail-closed semantics, the typed pipeline, the separation of detection from policy from
  anonymization, the pseudonym mapping boundary, privacy-safe observability, the network
  exclusion, untrusted document content, DecisionKit as the first integration, and the
  dependency policy.
- Contribution rules, agent rules, security policy, code of conduct and MIT licence.
- Issue and pull request templates, including the boundary questions every pull request
  must answer.
- Repository foundation: `SensitiveDataGateway.slnx`, central package management, the shared
  build properties, `.editorconfig`, a `NuGet.config` pinned to `nuget.org`, and NuGet
  auditing at `low`. Shipping projects multi-target `net8.0` and `net10.0`, treat warnings as
  errors and track their public surface.
- Continuous integration on Linux and Windows — format check, build, test, pack — and a
  release workflow driven by a `v*.*.*` tag.
- `SensitiveDataGateway.Core` with `BundleId`, the correlation identifier of a processing
  operation: bounded, trimmed, ordinal, and unable to quote a rejected value in its
  diagnostics.
