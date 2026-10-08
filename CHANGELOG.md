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
- The typed pipeline boundary. Every stage of the pipeline now has a type, and the unsafe
  path has none:
  - Identity: `BundleId`, `PartId`, `SegmentId`, `EntityId`, `PolicyId`, `PolicyVersion`,
    `DetectorId`, `ExtractorId` — bounded value objects with ordinal equality, whose
    diagnostics never quote the value they rejected.
  - Input: `DocumentBundle`, `DocumentPart`, `PartKind`, `DocumentContent`,
    `DocumentMetadata`. A bundle rejects duplicate part identifiers, a part whose parent is
    not in the bundle, and a primary document that is nested inside another part.
  - Extraction: `ExtractedDocument`, `TextSegment`, `SegmentOrigin`, `SourceSpan`,
    `ExtractionFailure`, `ExtractorVersion`, `IDocumentExtractor`.
  - Detection: `SensitiveEntity`, `EntityType` with thirty-one well-known values,
    `SensitivityLevel`, `DetectionConfidence`, `DetectionEvidence`, `DetectorVersion`,
    `DetectionFailure`, `DetectedDocument`, `ISensitiveDataDetector`. A detected document
    rejects an entity whose span falls outside the segment it claims.
  - Anonymization: `SanitizedDocument`, `SanitizedSegment`, `SanitizationStatus`,
    `TransformationAction`, `AppliedTransformation`, `SanitizationWarning`,
    `SanitizationProvenance`.
  - Egress: `ValidatedPayload`, which has no public constructor and no public factory, and
    `IDecisionProvider<TRequest, TResult>`, which accepts nothing else.
  - Errors: `GatewayError`, `GatewayErrorCategory` and the eight exceptions below
    `SensitiveDataGatewayException`. `PolicyViolation` has no exception type, because a
    block is a result and not a fault.
  - Limits: `ProcessingLimits`, with a default profile and validation that keeps the part
    bound inside the bundle bound.
- Architecture tests that assert the boundary properties by reflection rather than by
  review: the decision provider accepts no document type, `ValidatedPayload` cannot be
  constructed from a test assembly, no exception carries document content, and
  `PolicyViolation` still has no exception.
- The policy model in `SensitiveDataGateway.Core`: `SanitizationPolicy` and its builder,
  `PolicyRule`, `ConfidenceThresholds`, `ConfidenceOutcome`, `PseudonymScope`,
  `PolicyDecision`, `PolicyRationale`, `PolicyViolation`, `TransformationPlan` and
  `IPolicyEngine`. A policy is built through `SanitizationPolicy.CreateBuilder`, which
  starts closed: block everything, tolerate nothing, require a complete scan.
- `SensitiveDataGateway` with `PolicyEngine`, which turns a `DetectedDocument` and a policy
  into a `TransformationPlan`. It reads candidates and produces decisions; it never touches
  text.
- Two shipped policies, `SanitizationPolicy.Strict` and `SanitizationPolicy.Balanced`, both
  covering every well-known entity type, because a type a policy does not mention is
  blocked.
- `SanitizationPolicyDescription` and the rule, threshold and limit descriptions it holds:
  a policy binds from configuration and becomes a policy through `ToPolicy()`, with no
  reflection over policy types. Omitting a value never loosens the result.
- Policy validation at construction, never at evaluation: an unreachable rule, an undefined
  action, a scope on an action that produces no correlatable token, a flag threshold above
  the transform threshold and a per-part limit above the bundle limit are all configuration
  failures.
- `EntityType.WellKnown`, the list of the thirty-one well-known types, so a policy can be
  checked for coverage.
- Guide: [policies](docs/guides/policies.md).
- The anonymization stage. `DocumentAnonymizer` applies a `TransformationPlan` to a
  `DetectedDocument` and produces a `SanitizedDocument`, rewriting each segment forward
  against unmutated source text so every recorded offset still refers to what detection saw.
- The five transformations: `Preserve`, `Mask` (length-preserving, prefix and suffix only),
  `Redact`, `Pseudonymize` and `Hash` (keyed HMAC-SHA256 through `HashingKey`, which refuses
  key material under 32 bytes and hands none of it back).
- A closed token grammar in `MarkerSyntax`, and escaping of any document text shaped like a
  token, so a document cannot forge a replacement the validator would trust. An escaped
  segment carries a `MarkerSyntaxEscaped` warning and stays valid.
- Correlation scopes: `PseudonymScopeKey` for `Document`, `Bundle`, `Session` and `Tenant`,
  with the two wider scopes usable only when the application names them through
  `AnonymizationOptions`.
- `PseudonymMap`, `IPseudonymStore` and the volatile `InMemoryPseudonymStore`, all outside
  `SensitiveDataGateway.Core` so that no provider package can name them, and
  `LocalReidentifier` as the only, explicitly local way back from a token to a value.
- Overlapping spans are resolved strongest action first: a span wholly covered by an accepted
  one is reported and dropped, a partial overlap leaves the document `Incomplete`. An
  incomplete extraction, an incomplete detection, an unresolved candidate or a planned entity
  missing from the document do the same.
- Architecture tests asserting that the mapping stays inside the boundary: no mapping type
  lives in the assembly a provider references, no public member of `SanitizedDocument` or
  `ValidatedPayload` has a path to one, and `HashingKey` exposes no key material.
- Decision record 0013: the token grammar, the escaping rule, the keyed hash and the
  synchronous store.
- Guide: [anonymization](docs/guides/anonymization.md).
