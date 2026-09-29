# 0008. Make logging, telemetry and errors privacy-safe by default

- **Status:** Accepted
- **Date:** 2026-09-29
- **Deciders:** @iamjonatha

## Context

A gateway that prevents a document from reaching a provider, and then writes the document to
a log aggregator, has moved the leak rather than closed it. Logs are usually retained longer
than requests, are readable by more people, and are frequently shipped to a third-party
platform that nobody classified as a data processor.

The pressure to log content is real and comes from a good place: when a document is blocked,
the first question is "what was in it?" A debug log that answers that question is the most
useful thing a developer could ask for, and it is a leak with a log level.

Exception messages are the same problem with a shorter path: an interpolated message quoting
the input reaches an error tracker within seconds of the failure.

## Decision

Content is denied on every observability surface, by default and by construction.

**Allowed:**

| Field | Example |
| --- | --- |
| Identifiers | `BundleId`, `PartId`, `SegmentId` |
| Sizes and counts | part count, extracted characters, entity count by type and by sensitivity |
| Versions | policy identifier and version, detector identifiers and versions, gateway version |
| Outcomes | sanitization status, validation outcome, the identifier of the check that blocked |
| Timing | stage durations, total duration |
| Provider | provider name, transmission status |

**Forbidden, everywhere, at every log level:**

- document content, in whole or in part, including a single detected value;
- an entity's matched text, or the text surrounding it;
- the pseudonym map, or any entry of it;
- raw provider request or response bodies;
- any credential;
- an exception message or property that quotes input.

Mechanically:

- logging is source-generated with a fixed field set, so adding a field is a visible diff;
- no exception type has a property capable of holding content, and messages name the stage,
  the segment identifier and the failed check;
- `DetectionEvidence` explains a match — pattern name, checksum result, match length, model
  score — without reproducing the value;
- `InspectionReport` is the privacy-safe projection and is the type intended for logging;
- hashes in audit records are opt-in and salted, because an unsalted hash of a short value is
  a lookup table away from being the value;
- a CI harness runs the annotated synthetic corpus through the whole pipeline and fails the
  build if any emitted log line, metric tag, trace attribute or exception message contains an
  annotated value.

If a raw-capture facility is ever added, it is separately named, off by default, documented
as a debugging facility that must not be enabled in production, and it logs the fact that it
is enabled.

## Consequences

### Positive

- The observability stack does not become a second copy of the documents.
- Logs stay useful: counts, versions and outcomes are what operational questions are actually
  asked in.
- A leak through logging is caught by CI rather than by an auditor.
- Log volume stays bounded regardless of document size.

### Negative

- Diagnosing a false positive requires `InspectAsync` inside the trust boundary rather than a
  log search. That is more work, and it is the point.
- A contributor cannot add a quick diagnostic log line without thinking about it.
- The CI harness is infrastructure to maintain.

### Neutral

- The rule extends unchanged to metrics and traces: an `ActivitySource` tag is a log field
  with better tooling and the same retention problem.

## Alternatives considered

### Redaction middleware over free-form logs

Rejected as the primary mechanism. It is a detector applied to a log line, so it inherits
every false negative of detection, at a point where there is no second barrier. Denying
content by construction is stronger than scrubbing it afterwards.

### A debug level that logs content

Rejected. Debug levels are enabled in production during incidents, which is exactly when the
documents are most interesting and the decision is least considered.
