# 0021. Read Word and Excel with the Open XML SDK

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** @iamjonatha

## Context

After MIME and PDF, the formats left in the reference scenario of
[`../architecture/overview.md`](../architecture/overview.md) are the ones an office sends: a
contract as `.docx`, a customer list as `.xlsx`. They differ from PDF in a way that matters
here. A PDF is a rendering, so what it holds is roughly what a reader sees. An Open XML package
is an authoring format, and it holds things a reader never sees: text a reviewer deleted with
revisions on, a comment nobody resolved, the name of whoever last saved the file, a formula
that assembles a string the cell only shows the result of. Each of those is a place a personal
detail outlives the edit that was meant to remove it, which makes them the most valuable text
in the file for this library and the easiest to miss.

[ADR-0018](0018-format-adapters-may-take-a-parser.md) permits one parser per format adapter,
against six criteria. This record takes Open XML against them.

Unlike PDF, this format has a Microsoft-maintained reader, so
[ADR-0012](0012-dependency-policy.md)'s preference for a framework package has a candidate to
express.

## Decision

`SensitiveDataGateway.Documents.OpenXml` references **DocumentFormat.OpenXml** 3.5.1 and
provides both `DocxExtractor` and `XlsxExtractor`.

The six criteria, and how each was checked:

1. **No network.** `DocumentFormat.OpenXml.dll`, `DocumentFormat.OpenXml.Framework.dll` and
   `System.IO.Packaging.dll` were disassembled and every method body scanned for calls into
   `System.Net.Http`, `System.Net.Sockets`, `System.Net.Security`, `System.Net.WebSockets` and
   `System.Net.Quic`, and for the individual transmitting types that live elsewhere — `Dns`,
   `WebClient`, `WebRequest`, `SmtpClient`, `Ping`. There are none, in any of the three. The
   scan is kept as a test, `NetworkCapabilityTests`, so a future version that gains the
   capability fails the build rather than shipping.
2. **No external entity resolution and no reference following.** This is the criterion Open XML
   is most exposed on, because a package part *is* an XML document and the format's own
   extension points point at other files.

   The reader prohibits document type declarations outright. The three assemblies hold no
   reference to `XmlUrlResolver`, `XmlResolver`, `XmlPreloadedResolver`, `XmlDocument` or
   `XmlTextReader`, and a package whose `document.xml` opens with a `DOCTYPE` — an internal
   entity, an external `file://` entity, or an external DTD — is rejected with *"For security
   reasons DTD is prohibited in this XML document."* before any content is read. That is the
   XXE case and it fails closed by construction rather than by configuration. Three tests, one
   per shape of declaration, assert the refusal so a change of reader cannot quietly take the
   prohibition away.

   The format's own pointers out of the file are refused by the adapter, not followed:
   an `altChunk` (a chunk of HTML, RTF or another format a word processor imports on open), a
   `subDoc` (a master document assembled from separate files), and an `externalLink` (a
   workbook whose formulas read cells of another workbook). Each is `unreadable_construct`
   rather than a best-effort read, because what they carry is text no part of this adapter
   scanned.

   Hyperlinks are read as *text*. A `mailto:` target is an address written down, which a
   detector must see; it is a string here and nothing opens it.
3. **Streaming, or at least bounded.** A package is a zip archive with a central directory at
   the end, so it is random-access by construction and the bound is the adapter's. The byte
   ceiling is enforced *during* the copy of the part's stream, before the reader is handed
   anything, because a declared `Stream.Length` can lie and a `DocumentContent` built from a
   factory frequently has none. The segment ceiling is enforced as segments accumulate. Against
   the zip bomb — a part that is small compressed and enormous expanded — the SDK's own
   `OpenSettings.MaxCharactersInPart` is set from the adapter's options and enforced while
   reading. Each bound has a test.
4. **Maintained.** Microsoft, in the open at `dotnet/Open-XML-SDK`, continuously since 2014;
   3.5.1 is current. It is the reference implementation of ECMA-376 for .NET and the reader the
   format's own publisher uses.
5. **Permissively licensed.** MIT.
6. **No plugin or code-loading surface** reachable from parsing. `OpenSettings` exposes
   `AutoSave`, `CompatibilityLevel`, `MarkupCompatibilityProcessSettings` and
   `MaxCharactersInPart` — no provider, no resolver, no logger, nothing settable from input.
   Element types are resolved from a compiled table of the standard's names, not by reflecting
   over what the file says.

The SDK's package dependencies are `DocumentFormat.OpenXml.Framework` and
`System.IO.Packaging`, both Microsoft and both MIT. Three assemblies in total is the whole of
what it adds to a consuming application.

### One package for both formats

`DocumentFormat.OpenXml.dll` is a single assembly covering wordprocessing, spreadsheet and
presentation markup; it cannot be referenced in part. Shipping
`SensitiveDataGateway.Documents.Docx` and `.Xlsx` separately would install the same megabytes
twice for anyone who reads both, and would buy an application that reads only one of them
nothing at all. The two extractors are separate classes in one package, registered
independently.

### Reading is in document order, and a row is one segment

A paragraph is one segment. A table row is one segment with its cells joined by a single space,
and so is a spreadsheet row. The alternative — a segment per cell — is what the markup suggests
and it is wrong here: an author who puts an account number in one column and its holder in the
next has written one fact across two cells, and a detector handed `IT60` and `X0542811101`
separately matches neither. Joining at the row is the smallest unit that keeps a value
contiguous without duplicating text between segments.

### What a reader cannot see is read anyway

Text in a tracked deletion, the text of a formula rather than only its cached result, the
shared string table a cell points into, a comment and the name on it, the package properties a
person typed. None of these is on screen when the document is opened normally, and all of them
are in the file that would be transmitted. A reader that takes only the visible layer reports a
document clean on the strength of what was hidden from it.

The package property *dates* are deliberately left out. They are timestamps in a fixed syntax,
and the only thing a detector can make of one is a date of birth that is not there.

### An encrypted document is told apart from a damaged one

An Open XML file protected with a password is not an encrypted zip archive: it is a compound
file holding the encrypted package, and handing one to the reader produces the same
*"File contains corrupted data"* as a truncated file. The adapter checks for the compound file
signature before the reader is handed anything, and reports `password_protected`. Reporting it
as malformed would send an operator looking for a broken file instead of a password.

### Refusals are matched on the markup, not on the reader's typing

`altChunk` and `subDoc` are found by element name and namespace rather than by the type the SDK
assigned. An element the reader did not expect in the position it found it is handed back
untyped, and a refusal that depends on the reader's typing is a refusal that a misplaced
element walks straight past. This was not a theoretical concern: a `w:subDoc` written as a
direct child of `w:body` is exactly such an element, and the name-based check is what catches
it.

## Consequences

### Positive

- The two formats an office actually sends can be read, including the layers of them a reader
  does not see.
- Criterion 2, the one this format is most exposed on, is satisfied by the reader's own
  construction rather than by settings the adapter has to remember to apply — and the tests
  assert it anyway.
- The dependency graph stays closed: three format adapters, and no package among them depends
  on anything but Microsoft.

### Negative

- The package is neither trimmable nor AOT-compatible; both flags are off in the project file,
  as for the other adapters. An application that publishes AOT can use the gateway and the MIME
  adapter but not this one, and that is visible at publish time rather than at runtime.
- A document carrying an `altChunk` is refused rather than partly read. Word writes one when
  content is pasted from HTML, so this will block real documents from legitimate senders. It is
  the correct outcome: the chunk's text is not in the markup, and hard rule 6 forbids turning
  the block into a warning.
- An image inside a document is not read, exactly as a scanned PDF page is not. Unlike the PDF
  case the adapter cannot tell a decorative logo from a photographed identity card, so it does
  not refuse on their account. A document whose sensitive content is entirely in a picture
  passes as clean until OCR exists.

### Neutral

- The SDK also reads PowerPoint, and can write all three formats. The adapter uses neither:
  `AutoSave` is off so nothing can flush a change back into the caller's file, and presentation
  support is a later step rather than a missing one.

## Alternatives considered

### NPOI

Rejected on criteria 1 and 6. It is a port of Apache POI and carries the Java design with it,
including a much larger surface than reading text requires. It also reads the legacy binary
`.doc` and `.xls` formats, which is the feature it is usually chosen for and is not wanted
here: a compound file is a filesystem inside a file, and accepting one would mean accepting
every stream in it as something to account for.

### ClosedXML, DocX and the other convenience wrappers

Rejected. They sit on top of the Open XML SDK, so they add the SDK's surface plus their own
dependencies, and they are built for authoring documents rather than for reading everything a
document holds. Comments, tracked deletions and formula text are precisely what a convenience
API abstracts away.

### Read the zip and parse the XML here

Rejected in [ADR-0018](0018-format-adapters-may-take-a-parser.md) for all formats, and the
reasoning holds twice over here. Parsing the markup directly would mean owning the `XmlReader`
settings that make the DTD prohibition work — the single most security-sensitive line in this
adapter — rather than inheriting it from Microsoft's reader. Owning it means being able to get
it wrong.

### Two packages, one per format

Rejected above: the SDK assembly is monolithic, so two packages ship the same bytes twice and
isolate nothing.

### Accept a document with an `altChunk` and read what is in the markup

Rejected. It is the Open XML equivalent of accepting an image-only PDF page as empty: a
document reported clean on the strength of the part nothing looked at.
