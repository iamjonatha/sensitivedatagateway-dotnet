# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

While the major version is `0`, the public API may change in any minor release.

## [Unreleased]

### Added

- `SensitiveDataGateway.Detection.Presidio`: a detector that asks a Presidio analyzer the
  deployment operates. The endpoint has no default, `ForHostedService` produces a detector the
  strict path refuses, an entity name the map does not know is kept rather than dropped, and
  the package adds no NuGet dependency — the JSON contract is source-generated so it survives
  trimming and Native AOT.
- `ISemanticDetector` and `SemanticDetectionContext`: the contract for a detector backed by a
  model, and the question it is asked. The instructions are a constant of the library, the
  document sits inside a block whose marker is chosen at random once the content is known, and
  the text is written verbatim so that reported offsets still point at it.
- `SemanticDetectorHost`, the only way a semantic detector reaches the pipeline. It refuses a
  detector that declares itself remote unless the application says so in code, records where
  the model ran in the identifier the audit keeps, and rejects a finding that points at a
  segment the document does not have, reaches past the end of one, names an entity type the
  detector was not asked about, or is attributed to somebody else.
- `TrustBoundaryLocality`, the one question the library asks about a component that reads raw
  content: does it run in the process, elsewhere in the deployment, or at a third party. A
  text recognizer and a semantic detector are classified by the same type, so the two stages
  cannot drift into disagreeing about what "self-hosted" means.
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
  starts closed: block everything, tolerate nothing.
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
- The final privacy validation. `PrivacyValidator` runs ten checks over the detected
  document, the plan, the sanitized output and the policy, and is the only producer of a
  `ValidatedPayload`:
  - `SanitizationStatus`, `ScanCompleteness`, `UnresolvedEntities`, `PolicyViolations`,
    `TransformationCoverage`, `AntiLeakScan`, `TokenResolution`, `MarkerIntegrity`,
    `ProvenanceCompleteness` and `PayloadStructure`. Every check must pass; there is no
    partial pass, no severity and no override.
  - The anti-leak scan looks for an exact match, a normalized match, a fragment above a
    configured length, a known secret pattern and a high-entropy string, over the output text
    with the gateway's own markers removed.
  - `TokenResolution` catches a marker whose body carries the value it stands for, which the
    token grammar permits and which would otherwise pass every other check.
- `PrivacyCheck`, `LeakSignal`, `PrivacyFinding`, `PrivacyValidationReport`,
  `SanitizationResult`, `GatewayResult<TResult>` and `IPrivacyValidator` in
  `SensitiveDataGateway.Core`. A finding names a check, a segment, an entity type and a
  signal, and carries no text from the document, so a report can be logged whole.
- `ValidationOptions`, with a fragment length, an entropy length and an entropy threshold.
  None of the three can be set to a value that turns a check off, and the type carries no
  boolean at all — asserted by a test, so that an option to downgrade a block cannot be added
  without deleting it.
- Decision record 0014: the validator takes four inputs and re-derives each fact from the
  stage before the one that reports it.
- Stage 2, deterministic detection. `DetectionPipeline` runs a set of detectors concurrently,
  reconciles the spans they claim over the same characters, and records a `DetectionFailure`
  for any detector that throws, exceeds its time budget or floods the stage. The claims of a
  failed detector are discarded rather than truncated, so the document carries the evidence
  that part of it was never examined.
  - Reconciliation prefers the more damaging claim, then the more confident, then the longer,
    and breaks a remaining tie by detector identifier so that the result is the same on every
    run and on every machine.
- `DeterministicDetectors`, with thirteen built-in detectors: email addresses, telephone
  numbers, IBANs, payment cards, Italian tax codes, JSON web tokens, API keys and access
  tokens, private keys, connection strings, passwords, locators, IP addresses and runs of
  text too dense to be prose. None of them reads a file, opens a socket or consults a model.
  - `Iban()`, `CreditCard()` and `ItalianTaxId()` verify mod-97, Luhn and the Italian check
    character respectively. A candidate whose arithmetic disagrees is dropped, not degraded.
  - Measured against the annotated corpus: 100% recall and 93.3% precision, both asserted by
    a test rather than only published.
- `PatternDetector.Create`, which builds a detector from an expression a deployment supplies.
  It refuses an expression that carries no match timeout, and narrows the reported span to a
  group named `value` when the expression declares one.
- `EntropyDetectorOptions` and `DetectionPipelineOptions`. Neither can be set to a value that
  makes the stage scan less without saying so: exceeding a bound is a recorded gap, and a gap
  blocks.
- `EntityType.Url` and `EntityType.IpAddress`, with rules in both shipped policies — the
  strict policy redacts them, the balanced policy pseudonymizes them.
- Decision record 0015: a failed check digit drops the candidate rather than reducing its
  confidence.
- The detector guide, covering what each built-in detector finds, the measured precision and
  recall, the six classes of value the built-in set does not find, and how to add a pattern
  of your own.
- Stage 1 of the pipeline. `IDocumentExtractor`, `ExtractionPipeline`, `ExtractedDocument`,
  `TextSegment`, `ExtractionFailure` and a `PlainTextExtractor` that accepts `text/*` and
  nothing else. A part no extractor accepts becomes a recorded failure rather than a silent
  best-effort read, and a bundle-wide overage is a limit exception rather than a truncation.
- The gateway façade. `ISensitiveDataGateway` sanitizes and inspects;
  `ISensitiveDataGateway<TRequest, TResult>` adds `ExecuteAsync` and is the only way to reach
  a provider, which it can only reach with a `ValidatedPayload`. `DocumentGateway` and
  `DocumentGateway<TRequest, TResult>` implement them over one shared pipeline, so the two
  cannot drift.
- `InspectionReport`, what `InspectAsync` returns: counts, statuses and versions, with no
  text, no entity value and no offset. Nothing is transmitted and no payload is produced.
- Audit. `IAuditSink`, `AuditRecord`, `AuditOperation`, `AuditOutcome` and a `NullAuditSink`
  default. Every operation writes exactly one record — allowed, blocked, failed or
  cancelled — and the write uses `CancellationToken.None`, because a cancelled operation that
  left no trace is the one an incident review needs.
- `GatewayOptions`, immutable with `With` methods: the audit sink, the logger, the hashing
  key, the clock the processing budget is enforced against, and the gateway version stamped
  into provenance.
- Ten source-generated log events, none of which takes a `string` parameter. A reflection
  test over every shipping assembly enforces the parameter rule, and a corpus harness runs
  the annotated detection corpus through the gateway and asserts that no annotated value
  reaches a message, a structured field, a scope or an audit record.
- Optional keyed digests of the document and the payload in an audit record, off unless the
  application supplies a key. An `Inspect` record never carries a payload digest.
- `IPrivacyValidator.Reject`, which produces a refusal for a plan the policy engine already
  blocked. The validator remains the only producer of a `ValidatedPayload` and is now also
  the only producer of a refusal.
- Decision record 0016: a refusal is a result, a pipeline that cannot answer throws, and
  stage failures flow to the validator rather than being decided by the stage that failed.
- The getting-started guide and the logging-and-audit guide.
- `SensitiveDataGateway.Testing`, a shipped package rather than an internal helper:
  `LeakAssert`, `RecordingDecisionProvider`, `FakeDecisionProvider`, `RecordingAuditSink`,
  `SyntheticCorpus` with nine annotated documents in five languages, `SyntheticDocument`,
  `SensitiveValue`, `TestBundle` and `TestPolicy`. It carries no test-framework dependency,
  so it is usable from xUnit, NUnit, MSTest or a console program, and it has no internal
  access to the libraries — nothing in it can forge a `ValidatedPayload`.
  - `LeakAssert` searches the text as written and with all white space removed, refuses a
    value shorter than four characters rather than skipping it, and reports a leak without
    ever repeating the value it found.
- The egress security suite, `tests/SensitiveDataGateway.Security.Tests`, which is granted no
  internal access either: every scenario in it is one a consumer of the published packages
  could write. It establishes that every annotated value is detected and absent from the
  payload, the provider call and the audit record; that a detector that throws, an anonymizer
  that rewrites nothing, an unresolved entity the policy will not tolerate and an oversized
  part each stop transmission under the most permissive policy the gateway accepts; that a
  document issuing instructions is handled like any other and has its instruction carried
  through as content; that a marker written in the source is escaped rather than believed;
  that no failure path renders a value into an exception; and that `LeakAssert` itself fails
  when it should.
- The testing guide.
- `SensitiveDataGateway.Providers.DecisionKit`, the first decision integration and the
  reference implementation of `IDecisionProvider<TRequest, TResult>`. `DecisionKitProvider`
  maps a `ValidatedPayload` onto a DecisionKit `DecisionRequest` and returns the result
  untouched: it interprets no answer, retries nothing, and performs no network I/O of its
  own, because the transport belongs to the DecisionKit provider the application composes
  behind it.
  - `DecisionKitPayloadFormat` chooses between one block of text and one property per
    segment. The choice is about shape, never about safety: both carry the same sanitized
    content and neither can reach anything else.
  - `DecisionKitMetadataKeys` names the five provenance keys put on every request — policy,
    policy version, gateway version, content hash and sanitization timestamp. All are
    strings, none is derived from document content, and the `sdg.` prefix is reserved.
  - The bundle identifier travels as the DecisionKit `ClientRequestId`, so a decision can be
    correlated with its audit record without carrying content.
  - A `DecisionException` propagates as thrown, so the category and retry hint survive to
    the application as the inner exception of the gateway's `ProviderException`.
- A boundary test that reads the adapter assembly's type references out of its metadata and
  fails if it names a type from any stage before validation. Those types are reachable from
  the adapter at compile time, so the check is mechanical rather than a review convention.
- The DecisionKit provider guide and the guide to writing your own provider.
- A ninth explicit non-guarantee in the egress guarantees: the request passed alongside the
  bundle is handed to the provider untouched, so content quoted into it leaves unsanitized.
  The gateway cannot tell a caller's own wording from quoted content, and says so rather
  than implying a guarantee it does not make.
- `SensitiveDataGateway.Extensions`, the composition package.
  `AddSensitiveDataGateway(...)` publishes the gateway, its interface and the
  `SanitizationPolicy` the registration was configured with. The policy stays an argument of
  every call: the container composes the gateway, the caller passes the policy, and a call
  site still says which rules governed it. A registration that states no policy publishes
  `SanitizationPolicy.Strict`, so forgetting to choose blocks rather than leaks.
  - `SensitiveDataGatewayBuilder` states extractors, detectors, the hashing key, the
    pseudonym store, the audit sink, the gateway version and the clock, each as a value or a
    factory over the service provider. There is no overload that takes a type and constructs
    it, because that is the one thing that would stop the package publishing under native
    ahead-of-time compilation.
  - `PolicyConfiguration` reads a policy out of `IConfiguration` key by key, with no
    reflection. What configuration can express is exactly what that file names.
  - Registration is eager: the builder runs and the policy is built inside
    `AddSensitiveDataGateway`, so a malformed section throws on the line that registers it
    rather than on the first request. No hosting dependency, no `ValidateOnStart`.
  - Registering the same name twice throws, naming the registration and nothing else.
  - `AddSensitiveDataGateway(name, configure)` registers a keyed gateway and a keyed policy,
    so several destinations coexist in one host and the choice between them is made at
    compile time.
- `DocumentGateway.Create(extractors, detectors, pseudonymStore, options)`, which builds a
  gateway over stated stages while keeping the one definition of the ephemeral hashing-key
  fallback inside the pipeline.
- `samples/Aot`, the native AOT smoke test. It binds a policy from configuration, composes a
  container, sanitizes a document and asserts that no synthetic value survived, publishing
  with `PublishAot`, `TrimMode=full` and warnings as errors.
- The dependency injection guide, with the full configuration schema.
- A carve-out for format adapters: a package whose only purpose is to turn one file format
  into text may take one parser dependency, against six stated criteria. Decision record
  0018, and rule 7 in the dependency classification.
- `SensitiveDataGateway.Documents.Mime`, the adapter for internet messages, built on
  MimeKitLite.
  - `MimeBundleReader` turns a `message/rfc822` stream into a `DocumentBundle`: one part per
    body, per attachment and per forwarded message, each carrying its lineage, its media
    type and its file name, and a set of header segments on the primary part.
  - Identifiers are content-free. Nothing in a part identifier, a segment identifier or an
    error message repeats what the message said.
  - It fails closed on what it cannot read whole: a `message/external-body` it would have to
    fetch, a `message/partial` it cannot reassemble, an encrypted or signed part it cannot
    open, a message deeper, larger or wider than its limits, and a message the parser
    truncated rather than parsed.
  - `MimeStructureExtractor` reads the headers the caller names, unfolding continuations and
    decoding RFC 2047 encoded words, and `MimeReaderOptions` states the limits and the header
    list.
  - Decision record 0019 records why the adapter references MimeKitLite rather than MimeKit:
    MimeKit reaches the network in two places, both in its cryptography, and the criterion is
    absence of the capability rather than disuse of it. MimeKitLite has no package
    dependencies on either target framework.
- `NetworkCapabilityTests`, which disassembles every assembly that reads document content and
  fails if any method body calls into a network namespace or one of the transmitting types
  that live in otherwise harmless ones. Hard rule 7 now has a test.
- `SensitiveDataGateway.Documents.Pdf`, the extractor for `application/pdf`, built on PdfPig.
  - `PdfExtractor` produces one segment per page, the document information entries, the XMP
    metadata packet, every annotation, and the value of every AcroForm field. Page text comes
    out in reading order rather than painting order, because a value split across an
    interleaved two-column layout is a value no detector matches.
  - Parsing is strict. A document the parser cannot read without guessing is refused, because
    a guessed object yields a page whose text is partly recovered and silently partly absent.
  - The XMP packet is decoded as text and never handed to an XML parser, so there is no
    external entity to resolve and no resolver to configure.
  - It fails closed on what it cannot read whole: a password-protected document, an embedded
    file, an XFA form, a page of pixels with no extractable text, a document past the page or
    byte bound, and a field tree deeper than the walk descends. The byte bound is enforced
    while the content is copied, because a declared stream length can lie.
  - `PdfExtractorOptions` states the page and byte bounds and is immutable.
  - Decision record 0020 records the choice of PdfPig against the six criteria in 0018, and
    why iText, PDFsharp and a PDFium wrapper were rejected.
- `SensitiveDataGateway.Documents.OpenXml`, the extractors for Word and Excel, built on the
  Open XML SDK.
  - `DocxExtractor` produces one segment per paragraph, one per table row, one per header and
    footer, one per footnote and endnote, one per comment together with the name on it, one
    per external link target, and one per package property a person can type into.
  - `XlsxExtractor` produces one segment per row of each sheet, one per sheet name, one per
    defined name, one per comment, and one per package property. A cell is resolved through
    the shared string table and carries the text of its formula as well as its value, because
    a name assembled from literals appears nowhere else in the file.
  - A table row and a spreadsheet row are each one segment, with the cells joined by a space.
    An account number in one column and its holder in the next are one fact, and a detector
    handed the cells separately matches neither.
  - Text a reviewer deleted with revisions on is read. It is invisible to a reader and fully
    present in the file that would be transmitted.
  - It fails closed on what it cannot read whole: a document encrypted with a password, told
    apart from a damaged one by the compound file signature; an `altChunk` importing another
    format; a `subDoc` assembling the document from separate files; a workbook whose formulas
    read another workbook; a package with no main part; and a document past the byte or
    segment bound. The byte bound is enforced while the content is copied, because a declared
    stream length can lie, and `OpenSettings.MaxCharactersInPart` bounds expansion while the
    markup is read.
  - A package whose markup carries a document type declaration is refused by the reader before
    any content is read, which is the XXE case closed by construction. Three tests assert it.
  - `OpenXmlExtractorOptions` states the byte, segment and character bounds and is immutable.
  - Decision record 0021 records the choice of the Open XML SDK against the six criteria in
    0018, why both formats ship in one package, and why NPOI, the convenience wrappers and
    parsing the markup directly were rejected.
- An optical character recognition abstraction, with no engine shipped in this repository.
  - `ITextRecognizer` is what an engine satisfies, and `RecognizedText`, `RecognizedLine`,
    `RecognizerVersion`, `RecognizerId` and `TrustBoundaryLocality` are the values it
    exchanges.
  - `RecognizingExtractor` is the only thing that puts a recognizer into the extraction stage.
    An application that does not construct one has no recognition path at all, and a scanned
    page keeps failing closed exactly as before.
  - `RecognizingExtractor.Create` refuses an engine that declares itself remote.
    `CreateOutsideTheTrustBoundary` accepts it. The decision to send content that detection
    has not yet seen off the host is a line of source a reviewer reads, not a setting.
  - The locality becomes part of the composed extractor identity, so an audit record says a
    page went to `recognizer/remote/vendor-ocr@1.0` without saying what was on it.
  - A part is refused unless every line reaches the confidence threshold, which defaults to
    0.80. `RecognizedText.LowestConfidence` is the minimum rather than the mean, because the
    mean is the arithmetic by which one illegible line disappears into forty readable ones.
  - It fails closed on an engine that returns nothing, returns null, throws, or returns more
    lines than `RecognitionOptions.MaxLines` allows. No refusal names a recognised line, a
    fragment of one, or anything that correlates with content.
  - Decision record 0022 records why no engine ships, why locality is the only property of an
    engine the library reasons about, and why a configuration flag, averaging the confidence
    and dropping the uncertain line were all rejected.
- `samples/EmailClassification`, the reference scenario end to end: an email with PDF, Word and
  Excel attachments, read into a bundle, sanitized, and handed to a recording provider.
  - It demonstrates that an attachment is a part and nothing more. Four of the six detected
    values sit nowhere but inside an attachment, so a gateway that read only the body of the
    message would fail the sample's egress assertion.
  - A further attachment in a format no extractor claims refuses the whole message, rather than
    transmitting the three that were read.
  - It asserts that a postal address written in prose is *still transmitted*, because no
    deterministic detector can find one. The day semantic detection ships, the sample fails.
- `ExtractionFailureReason.UnreadableConstruct` and `ExtractionErrorCodes.UnreadableConstruct`,
  for a part whose bytes parse but whose content cannot be recovered as text — a scanned page,
  an embedded document, a form defined as a payload the reader refuses to open.

### Changed

- The default gateway supplies the anonymizer with a hashing key whether or not the
  application configured one. A shipped policy may ask for a hashing transformation, and the
  anonymizer refuses to produce an unkeyed hash because a short value hashed without a secret
  is reversible; without this, `SanitizationPolicy.Balanced` on the default gateway threw at
  the first value it was asked to hash. The fallback key is per-instance, so the token is
  stable inside that gateway and reproducible nowhere else.
- A blocked plan now skips anonymization entirely. The pseudonym store is not touched and no
  token is minted for a document that is not going anywhere, so
  `InspectionReport.Status` is `null` when the policy blocked the document.
- `ExecuteAsync` now reports a provider that throws as `ProviderException`, whatever type it
  threw, keeping the original as the inner exception. A provider is third-party code and
  cannot be relied on to honour the contract `IDecisionProvider<,>` documents; until now its
  own exception escaped unchanged, and the audit record of an outage carried no error
  category at all — nothing downstream could tell a provider failure apart from a failure of
  the gateway itself. Cancellation and failures the provider already reported correctly are
  passed through untouched.

### Removed

- `SanitizationPolicy.RequiresCompleteScan`, `SanitizationPolicyBuilder.RequireCompleteScan`
  and the `RequireCompleteScan` key of a policy description. An incomplete scan blocks under
  every policy: the final privacy validation refuses one unconditionally, as the rule against
  downgrading a block requires, so the lever could only ever read as a way to turn that
  refusal off. It had no effect downstream, and an option that appears to relax a guarantee
  without relaxing it is worse than no option.

### Fixed

- The audit record now carries `ProviderName`, the name of the decision provider the payload
  was handed to. `IDecisionProvider<,>.Name` documented itself as being for the audit record,
  but no field carried it and nothing in the gateway read it, so a record said that an
  operation happened and nothing about where the content went. The field is set if and only
  if the payload crossed the boundary — including when the provider was reached and then
  failed, and excluding a blocked `Execute`, a `Sanitize` and an `Inspect` — because the one
  record of an outage is exactly the one an auditor needs attributed. The name is read once,
  when the provider is composed into the gateway and before any document exists, so a
  provider that derived its name from content would have nothing to derive it from; a
  provider that returns an empty name is now refused at composition rather than producing
  records nobody can attribute.

- Raised the match timeout on every built-in expression, in both deterministic detection and
  the final anti-leak scan, from 250 and 100 milliseconds to one second. The timeout is the
  fallback behind bounded quantifiers, not the protection itself, and a value that tight
  measured contention for the processor rather than a runaway pattern — turning a scan that
  was working into a recorded gap, and a gap into a blocked document.

- The anonymizer no longer degrades a document to `Incomplete` for an unresolved candidate
  the policy explicitly tolerates. A blocked plan never reaches the anonymizer, so every
  unresolved candidate that does is one `TolerateUnresolvedUpTo` accepted; reporting it as a
  failure made that policy lever inert.

- The IBAN expression no longer reaches past the end of the number it is matching. Its final
  group was mandatory and could span the separator, so an account number whose body divides
  evenly into four-character groups — every Spanish IBAN, among others — swallowed the next
  word of the sentence when that word was one to four characters long. The widened candidate
  then failed its mod-97 check and was discarded, so the account number was not reported at
  all: a missed detection rather than a noisy one. The separator is now captured once and
  required to be the same throughout, which is what the format actually specifies.
