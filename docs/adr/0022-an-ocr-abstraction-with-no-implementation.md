# 0022. Give optical character recognition a shape, and ship no engine

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** @iamjonatha

## Context

Three format adapters now read the formats an office sends, and all three stop at the same
place. A PDF page that is a photograph of a contract is refused as an `unreadable_construct`
([ADR-0020](0020-pdfpig-for-pdf.md)); an image pasted into a Word document is not read at all
([ADR-0021](0021-openxml-sdk-for-office-documents.md)). The identity card someone scanned and
attached is, in practice, the single most sensitive thing a document in the reference scenario
carries, and it is exactly the thing none of the readers can see.

Closing that gap means recognising text in a picture, and that is where this library's
guarantees are easiest to lose. [ADR-0009](0009-no-network-in-core.md) says that nothing which
reads raw content may do network I/O, and point 4 of it says that when a decision weakens a
guarantee it must be made in code that a reviewer reads, not in a configuration file a
deployment edits. T10 of [`../security/threat-model.md`](../security/threat-model.md) names the
exact failure: a recognition service reached over the network is a boundary in name only,
because the raw page — the un-anonymised one, by definition, since recognition is what comes
*before* detection — leaves the process to reach it. A gateway that sends an identity card to a
vendor endpoint to find out what it says has already done the thing it exists to prevent.

Every usable OCR engine is also a native dependency, a model file, or a hosted service.
[ADR-0012](0012-dependency-policy.md) and the no-network rule together mean this library cannot
pick one.

## Decision

`SensitiveDataGateway.Core` defines `ITextRecognizer` and the value types around it.
`SensitiveDataGateway` provides `RecognizingExtractor`, which adapts a recognizer to
`IDocumentExtractor`. **No package in this repository implements `ITextRecognizer`.**

An application that does not construct a `RecognizingExtractor` has no recognition path at all,
and a scanned page keeps failing closed exactly as it does today. Nothing is enabled by
upgrading.

### The only property of an engine this library reasons about is where it runs

`RecognizerLocality` is `InProcess`, `SelfHosted` or `Remote`, and it is part of the
recognizer's version, which is part of the extractor's identity, which is what the audit record
carries. Accuracy, language coverage and speed are the host's problem. Locality is not, because
it is the only property that changes what the gateway can promise about content it has not yet
anonymised.

The declaration is **a claim under review, not a measurement**. This library cannot prove that
an engine stays local; what it can do is make the claim explicit, record it, and refuse the one
value that contradicts the guarantee unless a reviewer accepted it by name.

### Accepting a remote engine is a different method, not a flag

`RecognizingExtractor.Create` throws on a recognizer that declares `Remote`.
`RecognizingExtractor.CreateOutsideTheTrustBoundary` accepts it.

The length of the second name is the point. It appears in a diff, it is searchable across a
codebase, and it cannot be reached by editing a settings file after review — which is point 4
of [ADR-0009](0009-no-network-in-core.md) applied to the first case that needed it. An
application that genuinely accepts the trade — an air-gapped appliance whose "remote" is a
process on the same host, a deployment with a signed agreement covering the vendor — can state
so once, in code, where it is read.

`SelfHosted` is accepted by `Create` because an engine under the host's own control is a
deployment fact this library has no standing to refuse. It is recorded all the same.

### The audit records where the content went, not what was in it

The composed extractor identifies itself as `recognizer/{locality}/{recognizer id}` at version
`{recognizer version}`. An operator reading `recognizer/remote/vendor-ocr@1.0` learns that a
page left the process and which engine took it, which is what an audit trail is for, without
the record holding a single recognised character.

The locality is in the *identifier* rather than in the version string because
`ExtractorVersion` bounds a version at 64 characters and an engine's own version already fills
it. A composed identity too long to record is refused at composition, not truncated: an
identifier that does not round-trip is an audit entry that cannot be matched.

### Confidence is judged on the worst line, and one bad line fails the part

`RecognizedText.LowestConfidence` is the minimum across lines, and `RecognitionOptions`
defaults to refusing a part below `0.80`.

A page of forty confident lines and one illegible one is a page that was not read. The mean is
the arithmetic by which that fact disappears, and it disappears in the direction that reports
the document clean. Dropping the uncertain line instead is worse still: it produces a page that
scans clean precisely where the reader could not make out what was written, which is where an
account number photographed at an angle lives.

So the whole part is refused, and an operator is told a part could not be read with enough
certainty — never which line, and never what it nearly said.

### A refusal names no recognised text

Every error this stage raises carries the part identifier, the engine identity and a threshold.
None carries a line, a fragment, or a count that correlates with content, under hard rule 4 of
[`../../AGENTS.md`](../../AGENTS.md). `RecognizedLine.ToString()` is overridden for the same
reason: the default would have put the text into any log line that formatted one.

## Consequences

### Positive

- The gap the three adapters leave has a named shape, so an application that must close it has
  one place to do so and one contract to satisfy.
- The decision to send raw content off the host is visible in source, recorded in the audit
  trail, and impossible to reach by configuration.
- The library's dependency graph is unchanged: no native binary, no model file, no service
  client, and nothing new to scan in `NetworkCapabilityTests`.
- A recognizer is `ITextRecognizer`, not an extractor, so the fail-closed rules — empty result,
  threshold, line bound, null, thrown — are enforced once here rather than re-implemented by
  every host that integrates an engine.

### Negative

- Out of the box, a scanned document is still refused. The library ships the hole, not its
  filling, and an application that needs OCR has work to do that this package does not spare it.
- The locality is self-declared. A recognizer that claims `InProcess` and opens a socket is not
  caught by this design, and nothing short of running the engine under a sandbox would catch
  it. What the design buys is that the claim is written down and reviewed.
- The default threshold of `0.80` will refuse pages that a lenient reading would have passed.
  That is the intended direction of the error, and it is tunable per host — downwards only to
  `0.05`, so that "effectively no threshold" is not reachable by a typo.

### Neutral

- Engine selection, model management, language packs and GPU usage are entirely the host's.
  This library never sees them.
- `SensitiveDataGateway.Testing` does not expose a fake recognizer yet. The test doubles live
  in the test projects until an integrating application asks for one.

## Alternatives considered

### Ship a Tesseract-based recognizer

Rejected. Tesseract means a native binary per platform and a trained-data file per language,
which is a supply-chain surface an order of magnitude larger than anything else in this
repository and is not a NuGet package in the sense [ADR-0012](0012-dependency-policy.md) means.
It would also tie the library's release cadence to a native dependency it cannot audit by
disassembly the way the three format parsers were audited.

### A configuration flag that allows a remote recognizer

Rejected by point 4 of [ADR-0009](0009-no-network-in-core.md). A setting that lets raw content
leave the process is a setting that gets turned on in an environment nobody reviewed, by
someone solving an unrelated problem, and the resulting deployment looks identical to a
compliant one from the source alone.

### Average the confidence across lines

Rejected above. The mean is the number that makes one unreadable line vanish into thirty-nine
readable ones.

### Drop lines below the threshold and extract the rest

Rejected above, and more dangerous than averaging: it returns a successful extraction of a page
whose hardest-to-read portion — the portion most likely to be a handwritten or photographed
identifier — was silently discarded before any detector saw it.

### Let a recognizer be an `IDocumentExtractor` directly

Rejected. An extractor returns segments, so everything this record is about — the threshold,
the worst-line rule, the locality refusal, the identity that records where content went — would
be each implementer's to remember. The narrower contract is what makes the rules enforceable in
one place.

### Make the remote case a runtime check rather than a composition one

Rejected. A refusal raised when the first scanned page arrives is a refusal that reaches
production, because the composition it comes from is valid everywhere except where it matters.
Refusing at `Create` turns it into a failure during startup, in the test suite, at the line that
made the choice.
