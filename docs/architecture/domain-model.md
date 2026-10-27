# Domain model

The types the gateway is built from, and the rules they follow. This document is a design
target for the phases that implement it; where it and the code disagree, the code is wrong
or this document needs an ADR.

Read [`overview.md`](overview.md) first. The stage semantics are in
[`pipeline.md`](pipeline.md).

---

## 1. Identity

Value objects, not bare strings, so that correlation is deterministic and a typo is a
compile-time or construction-time error rather than a silent mismatch.

| Type | Purpose |
| --- | --- |
| `BundleId` | Identifies one processing operation end to end. Appears in every log line, audit record and report |
| `PartId` | Identifies one part of a bundle — the body, an attachment, a sheet |
| `SegmentId` | Identifies one canonical text segment produced by extraction |
| `EntityId` | Identifies one detected entity within an operation |
| `PolicyId` | Names a policy. Combined with `PolicyVersion` it makes a sanitization reproducible |
| `DetectorId` | Names a detector. Combined with its version it explains why an entity was found |

Identifiers are non-empty, trimmed, bounded in length, and never derived from content. A
`BundleId` that embeds a subject line is a leak with an identifier's name.

---

## 2. Input: the document bundle

The input is not a string. The reference case is an email with attachments, and flattening
it to text before the gateway sees it destroys the provenance the pipeline needs.

```text
DocumentBundle
  Id           BundleId
  Primary      DocumentPart
  Attachments  IReadOnlyList<DocumentPart>
  Metadata     DocumentMetadata
  CreatedAt    DateTimeOffset

DocumentPart
  Id           PartId
  Kind         PartKind          Body | Subject | Header | Attachment | Embedded
  MediaType    string            "text/plain", "application/pdf", ...
  Content      DocumentContent   text, bytes or a stream factory
  Parent       PartId?           an attachment inside an attachment keeps its lineage
  Metadata     DocumentMetadata
```

`DocumentMetadata` is a bounded, typed property bag. It carries what the application knows
about the part — file name, declared language, declared origin. It is **content**: metadata
is scanned like everything else, because a file name is frequently the most sensitive string
in a bundle.

`DocumentBundle` is the untrusted boundary type. It is accepted by the gateway façade and by
nothing else. No provider package can name it, and no logging surface accepts it.

---

## 3. Extraction: canonical content

```text
ExtractedDocument
  BundleId     BundleId
  Segments     IReadOnlyList<TextSegment>
  Failures     IReadOnlyList<ExtractionFailure>
  Extractors   IReadOnlyList<ExtractorVersion>

TextSegment
  Id           SegmentId
  PartId       PartId
  Text         string
  Origin       SegmentOrigin     page, paragraph, sheet and cell, header name
  Order        int
```

Extraction converts every part into ordered text segments while preserving provenance. A
transformation applied to a segment can always be traced back to the page, paragraph or cell
it came from, which is what makes the audit record meaningful and the human-review workflow
possible later.

An extractor that cannot handle a part records an `ExtractionFailure` rather than returning
empty text. Silence and failure are different facts, and the policy treats them differently:
an unreadable attachment is not an attachment with nothing in it.

**Rule:** an extractor never classifies. It does not know what a person's name is, and it
must not try.

---

## 4. Detection: candidates

```text
DetectedDocument
  Extracted    ExtractedDocument
  Entities     IReadOnlyList<SensitiveEntity>
  Detectors    IReadOnlyList<DetectorVersion>
  Failures     IReadOnlyList<DetectionFailure>

SensitiveEntity
  Id           EntityId
  Type         EntityType
  Span         SourceSpan         SegmentId + start + length
  Confidence   DetectionConfidence  0.0 .. 1.0
  Detector     DetectorId
  Sensitivity  SensitivityLevel
  Evidence     DetectionEvidence   why, without repeating the value
```

### `EntityType` is open, not an enum

The taxonomy must be extensible per domain, so `EntityType` is a validated value object with
well-known instances rather than a closed enumeration:

```csharp
EntityType.Person
EntityType.Email
EntityType.Iban
EntityType.ApiKey
new EntityType("CLAIM_NUMBER")      // domain-specific, first-class
```

Well-known types are grouped for policy convenience:

| Group | Types |
| --- | --- |
| Personal identifiers | `PERSON`, `EMAIL`, `PHONE`, `ADDRESS`, `TAX_ID`, `NATIONAL_ID`, `PASSPORT`, `DRIVER_LICENSE`, `DATE_OF_BIRTH` |
| Financial | `IBAN`, `CREDIT_CARD`, `BANK_ACCOUNT`, `FINANCIAL_REFERENCE` |
| Secrets | `PASSWORD`, `API_KEY`, `ACCESS_TOKEN`, `JWT`, `PRIVATE_KEY`, `CONNECTION_STRING` |
| Business confidential | `CUSTOMER`, `PROJECT`, `CONTRACT`, `INTERNAL_ID`, `CONFIDENTIAL_CODE`, `INTERNAL_SYSTEM` |
| Semantic categories | `HEALTH`, `LEGAL`, `HR`, `FINANCIAL_CONTEXT`, `SECURITY`, `TRADE_SECRET` |

An entity type the policy does not mention is governed by the policy's `UnknownEntityAction`,
whose safe default is `Block`.

### `SensitivityLevel`

`Low`, `Moderate`, `High`, `Critical`. It is a property of the *type* in a given policy, not
of the individual value, and it drives what happens when detection is uncertain: an
unresolved `Critical` entity blocks, an unresolved `Low` entity may be transformed
defensively.

### `DetectionEvidence` never quotes the value

Evidence explains the match — the pattern name, whether a checksum passed, the model score,
the length of the match — so that a false positive can be diagnosed without a log line
containing the IBAN that triggered it.

**Rule:** a detector never decides what happens to what it found. It emits candidates and
stops.

---

## 5. Policy: decisions

```text
SanitizationPolicy
  Id                   PolicyId
  Version              PolicyVersion
  DefaultAction        TransformationAction
  UnknownEntityAction  TransformationAction        default: Block
  DefaultThresholds    ConfidenceThresholds        used when Thresholds has no entry
  DefaultScope         PseudonymScope              used when a rule states no scope
  Rules                IReadOnlyList<PolicyRule>
  Thresholds           IReadOnlyDictionary<EntityType, ConfidenceThresholds>
  Limits               ProcessingLimits
  UnresolvedTolerance  SensitivityLevel?           null: tolerate nothing

PolicyRule
  Match       EntityType
  Action      TransformationAction
  Scope       PseudonymScope?
  Sensitivity SensitivityLevel?
```

A policy is built through `SanitizationPolicy.CreateBuilder(id, version)`, never through a
constructor with eleven parameters. The builder starts from the closed position — block
everything, tolerate nothing — so an incompletely configured policy is a restrictive policy,
not an open one.

`PolicyRule.Sensitivity` **selects**, it does not assign. A rule with a sensitivity matches
only entities the detector classified at that level; the level itself comes from detection.
The first matching rule wins, and `Build()` rejects a rule that an earlier rule has already
made unreachable, because a policy that silently ignores one of its own lines cannot be
audited.

`Scope` is accepted only for `Pseudonymize` and `Hash` — the two actions that produce a
correlatable token. For every other action a scope would be a value nothing reads.

### What is tolerated, and what blocks

| Condition | Outcome |
| --- | --- |
| Entity type mentioned by no rule and no threshold | `UnknownEntityAction`, which ships as `Block` |
| Confidence below the flag threshold | The entity is unresolved; no decision is produced |
| Unresolved entity above `UnresolvedTolerance` | `PolicyViolation` — the operation blocks |
| Rule action is `Block` or `RequireReview` | `PolicyViolation` — the operation blocks |
| Scan incomplete | `PolicyViolation` — the operation blocks, under every policy |

An unresolved entity never receives a `PolicyDecision`. The engine will not act on something
it could not identify, so the choice is between tolerating it and refusing the document — and
that choice belongs to the policy, not to the engine.

### `TransformationAction`

| Action | Meaning |
| --- | --- |
| `Preserve` | Leave the value as it is. An explicit, auditable decision |
| `Mask` | Keep the shape, remove the content: `IT60****3456` |
| `Redact` | Replace with a type marker that carries no identity: `[REDACTED]` |
| `Pseudonymize` | Replace with a stable, correlatable token: `[PERSON_001]` |
| `Hash` | Replace with a keyed digest. Correlatable across operations, not reversible |
| `Block` | Refuse the whole operation |
| `RequireReview` | Suspend the operation and hand it to a human inside the trust boundary |

### `ConfidenceThresholds`

```text
Transform   >= 0.90   apply the rule's action
Flag        >= 0.70   apply the action and record a warning
Unresolved   < 0.70   the entity was not resolved
```

The numbers are the shipped default for a strict policy, not a constant. They are
configurable per entity type, because the correct threshold for `API_KEY` and for
`ORGANIZATION` are not the same number.

### Output

```text
TransformationPlan
  PolicyId     PolicyId
  PolicyVersion PolicyVersion
  Decisions    IReadOnlyList<PolicyDecision>
  Unresolved   IReadOnlyList<SensitiveEntity>
  Blocking     IReadOnlyList<PolicyViolation>

PolicyDecision
  Entity       EntityId
  Action       TransformationAction
  Rationale    PolicyRationale     which rule, which threshold
```

The plan is data. It can be inspected, logged in aggregate, serialized into an audit record
and asserted against in a test, before a single character of text has been rewritten.

**Rule:** the policy engine never touches text.

---

## 6. Anonymization: the rewritten document

```text
SanitizedDocument
  BundleId     BundleId
  Segments     IReadOnlyList<SanitizedSegment>
  Status       SanitizationStatus     Valid | Incomplete | Failed
  Applied      IReadOnlyList<AppliedTransformation>
  Warnings     IReadOnlyList<SanitizationWarning>
  Provenance   SanitizationProvenance  policy, detectors, gateway version, timestamp
```

`SanitizedDocument` is a new object. The original is never mutated, so the two can be
compared — which is exactly what the anti-leak scan does.

### The pseudonym map

```text
PseudonymMap
  Scope        PseudonymScope    Document | Bundle | Session | Tenant
  Entries      token -> original value
```

`Bundle` is the default: a token means one thing across an email and its attachments, and
nothing outside that bundle. Wider scopes increase correlatability across documents and are
an explicit, documented choice.

The map is:

- never part of a `SanitizedDocument` that is passed to validation for egress;
- never part of a `ValidatedPayload`;
- unreachable from any `Providers.*` package, by package boundary and by type;
- volatile by default, with persistence being opt-in, encrypted, scoped and expiring.

Reversibility is a policy property, and the three cases are distinct:

| | Reversible | Correlatable |
| --- | --- | --- |
| `Pseudonymize` | yes, through the map, locally | yes |
| `Hash` | no | yes |
| `Redact` | no | no |

**Rule:** the anonymizer applies the plan. It does not re-classify, and it does not decide.

---

## 7. Validation: the only door

```text
PrivacyValidationReport
  Outcome      ValidationOutcome     Passed | Blocked
  Checks       IReadOnlyList<ValidationCheck>
  Blocks       IReadOnlyList<ValidationBlock>

ValidatedPayload
  BundleId     BundleId
  Content      IReadOnlyList<SanitizedSegment>
  Provenance   SanitizationProvenance
  ContentHash  string
```

`ValidatedPayload` has no public constructor. It is produced by the validator and by nothing
else, which is what makes "a provider accepts only a validated payload" a statement about
the type system rather than a convention.

It carries no pseudonym map, no entity list, no raw content and no reference to the
`DocumentBundle` it came from.

---

## 8. Result types

```text
SanitizationResult
  Status       Completed | Blocked
  Payload      ValidatedPayload?        present only when Completed
  Report       PrivacyValidationReport
  Summary      InspectionReport

InspectionReport
  BundleId          BundleId
  EntityCounts      EntityType -> int
  SensitivityCounts SensitivityLevel -> int
  Unresolved        int
  HighRiskUnresolved int
  TransmissionAllowed bool
  PolicyId, PolicyVersion, DetectorVersions
```

`InspectionReport` is the privacy-safe view: counts, statuses and versions, no values. It is
what may be logged, exported to telemetry and shown in an operational dashboard. An
application that is inside the trust boundary and needs the values reaches for the detailed
result explicitly, and that call is named so that it is visible in review.

---

## 9. Audit

```text
AuditRecord
  BundleId, Timestamp
  PolicyId, PolicyVersion
  DetectorVersions, GatewayVersion
  EntityCounts, UnresolvedCount
  SanitizationStatus, ValidationOutcome
  ProviderName, TransmissionStatus
  DocumentHash?, PayloadHash?         opt-in, salted
```

The audit record documents that an operation happened and under which rules. It never
reproduces the document. Hashes are optional and salted, because an unsalted hash of a short
value — a phone number, a tax code — is a lookup table away from being the value.

---

## 10. Errors

Following the same shape as the rest of the stack: a shallow exception hierarchy over a
category that keeps every distinction.

| Category | Meaning |
| --- | --- |
| `ExtractionFailed` | A part could not be read |
| `DetectionFailed` | A detector failed to complete |
| `PolicyViolation` | The policy forbids this document |
| `SanitizationFailed` | A transformation could not be applied |
| `ValidationFailed` | The final validator rejected the payload |
| `EgressBlocked` | Transmission was refused |
| `LimitExceeded` | A size, count or time limit was reached |
| `ProviderFailed` | The downstream provider failed |
| `ConfigurationInvalid` | The policy or the options are not usable |

Rules:

- A **block** is a `SanitizationResult` with `Status = Blocked`, not an exception. It is an
  expected outcome.
- A **failure** is an exception. It reaches the caller.
- `ValidationFailed` is never converted into "continue".
- **No exception message, and no exception property, contains document content.** An
  exception says which segment and which check, never what was in it.

---

## 11. Limits

```text
ProcessingLimits
  MaxBundleBytes, MaxPartBytes, MaxPartCount
  MaxExtractedCharacters
  MaxEntities
  MaxProcessingTime
```

Exceeding a limit blocks. It never truncates and sends: a truncated document is a document
whose unexamined tail was silently dropped, and an unexamined tail is exactly where an
attacker would put the payload.
