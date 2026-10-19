# 0018. Let a format adapter take one parser dependency, under stated criteria

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** @iamjonatha

## Context

[ADR-0012](0012-dependency-policy.md) restricts shipping packages to Microsoft-maintained
framework packages, with one carve-out: a `Providers.*` package may reference the integration
library it exists to adapt. It also records that the restriction is known to be inconvenient
in exactly one place — Phase 10 needs MIME, PDF, DOCX and XLSX readers, and no
Microsoft-maintained package covers all of them.

That phase has arrived. The options are three.

Writing the parsers here is not a saving. A MIME parser is RFC 5322 headers, RFC 2045
transfer encodings, RFC 2047 encoded words, charset fallbacks and twenty years of
malformed-message behaviour. A PDF text extractor is cross-reference tables, cross-reference
streams, object streams, filters, font encodings and `ToUnicode` maps. Each one is a parser
of hostile input, written by a project that has no fuzzing corpus for it. A privacy gateway
whose own parser can be made to read the wrong bytes has failed at the one thing it exists
to do, and it fails silently: a value the parser never emitted is a value the scan never saw.

Not shipping adapters at all is not an option either. The reference scenario in
[`../architecture/overview.md`](../architecture/overview.md) is an email with attachments.
Without adapters, every application writes its own extraction step, outside the boundary and
outside the audit record, which is strictly worse than one reviewed dependency.

The third option is to take a parser, and to constrain which one.

## Decision

A `SensitiveDataGateway.Documents.*` package may reference **one** parser for the format it
adapts, and nothing else beyond `SensitiveDataGateway.Core`. The carve-out is the same shape
as the one `Providers.*` already has: one named library, in one named package, that an
application installs only when it needs that format.

A parser is eligible only if all of the following hold, and the ADR that takes it states how
each was checked:

1. **No network.** Not for fonts, not for schemas, not for telemetry, not for update checks.
   The capability must be absent from the package graph, not merely unused by default.
2. **No external entity resolution and no reference following.** No DTD, no XInclude, no
   remote schema, no linked file, no URL. Where the parser exposes the choice, the adapter
   sets it, and a test proves the setting holds.
3. **Streaming, or at least bounded.** The adapter must be able to stop at
   `ProcessingLimits` rather than after materializing an attacker-sized document.
4. **Maintained.** Recent releases, a visible security response, and a real user base that
   finds the malformed-input bugs before this project does.
5. **Permissively licensed.** MIT, Apache-2.0 or BSD. No copyleft, no commercial terms.
6. **No plugin or code-loading surface** reachable from parsing input.

The parser is classified `Sensitive-data processing` in
[`../security/dependency-classification.md`](../security/dependency-classification.md). It
reads raw content, so it stays out of `Core` and out of the pipeline package, and the
dependency graph keeps it there.

Three constraints survive unchanged:

- **`Core` still has zero package references**, and the pipeline package still references
  only `Pure` packages. An adapter is referenced by the application, never by the pipeline.
- **An adapter still does no network I/O of its own**, by [ADR-0009](0009-no-network-in-core.md).
  Taking a parser does not relax that; it is what criterion 1 exists to preserve.
- **Each parser still gets its own ADR**, as ADR-0012 requires. This record states the rule;
  [0019](0019-mimekitlite-for-mime.md), [0020](0020-pdfpig-for-pdf.md) and
  [0021](0021-open-xml-sdk-for-docx-and-xlsx.md) take the individual decisions.

An adapter that cannot read something is not allowed to approximate it. A construct that
carries content the adapter cannot extract in place — an encrypted part, an embedded file, an
alternative-format chunk, an external workbook link — fails the part, and a failed part
blocks. That is the exchange that makes a third-party parser acceptable: the parser decides
what the bytes say, and this project decides what happens when it cannot say.

## Consequences

### Positive

- The reference scenario becomes reachable with parsers that have been fuzzed by their own
  projects for years.
- The capability classification still answers "what can reach the network?" by reading the
  package graph: nothing added here can.
- An application that only sanitizes strings installs none of this.

### Negative

- The supply chain of a consuming application grows by one package per format, each with its
  own transitive graph, its own CVE feed and its own release cadence.
- Trimming and Native AOT are no longer guaranteed for the adapter packages. The guarantee
  stands for `Core`, the pipeline, `Extensions` and `Providers.*`; an adapter states its own
  position, and `samples/Aot` continues to cover only the packages that promise it.
- A parser bug is now a privacy bug in this project's bug tracker, and triaging it means
  reading someone else's parser.

### Neutral

- The criteria are written down, so the next format is an argument about evidence rather than
  about taste.

## Alternatives considered

### Write every parser in this repository

Rejected. See the context: the failure mode of a home-grown parser of hostile input is
silent under-extraction, which is exactly the failure this library is built to prevent. The
effort would also be measured in years, not sprints.

### Vendor the parser sources into the repository

Rejected, as in ADR-0012. It transfers maintenance and security response here without
removing any of the original code's capabilities, and it hides upstream fixes.

### Put all four formats behind one `Documents` package

Rejected. It would force an application that reads email to install a PDF parser and a
spreadsheet parser, which contradicts [ADR-0003](0003-package-layout.md) and makes the
capability classification coarser than it needs to be.

### Run the parsers out of process, in a sandbox

Deferred rather than rejected. It is the right answer for a hostile-input parser, and it is
a much larger change: process lifetime, serialization of the extracted text, and a second
deployment artefact. It is recorded in [`../roadmap.md`](../roadmap.md) as a candidate for
after 1.0, and nothing decided here prevents it.
