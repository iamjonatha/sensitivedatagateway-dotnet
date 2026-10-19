# 0020. Read PDF with PdfPig

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** @iamjonatha

## Context

An attachment to the reference email in
[`../architecture/overview.md`](../architecture/overview.md) is a PDF. It is also the format in
which the documents this library exists to protect actually arrive: an identity document, a pay
slip, a bank statement, a medical report. An adapter that reads PDF badly is worse than no
adapter, because a page whose text it fails to recover is a page the pipeline scans, finds
clean, and forwards.

PDF is not a text format with decoration. A page is a content stream of operators; the text is
a sequence of glyph codes positioned by matrix, and recovering a *string* from it means
resolving the font, reversing its encoding to Unicode, and then deciding which glyph follows
which — because the order the operators appear in is the order the producer chose to paint,
not the order a human reads. The file around it is a graph of indirect objects reached through
a cross-reference table, with streams that may be compressed, encrypted, or both.

[ADR-0018](0018-format-adapters-may-take-a-parser.md) permits one parser per format adapter,
against six criteria. This record takes PDF against them.

The .NET framework offers nothing. There is no Microsoft-maintained PDF reader, so
[ADR-0012](0012-dependency-policy.md)'s preference for a framework package has no candidate to
express.

## Decision

`SensitiveDataGateway.Documents.Pdf` references **PdfPig** 0.1.16.

The six criteria, and how each was checked:

1. **No network.** The seven assemblies PdfPig ships — `UglyToad.PdfPig`, `.Core`,
   `.DocumentLayoutAnalysis`, `.Fonts`, `.Package`, `.Tokenization` and `.Tokens` — were
   disassembled and every method body scanned for calls into `System.Net.Http`,
   `System.Net.Sockets`, `System.Net.Security`, `System.Net.WebSockets` and `System.Net.Quic`,
   and for the individual transmitting types that live elsewhere — `Dns`, `WebClient`,
   `WebRequest`, `SmtpClient`, `Ping`. There are none, in any of the seven. The scan is kept as
   a test, `NetworkCapabilityTests`, so a future PdfPig that gains the capability fails the
   build rather than shipping.
2. **No external entity resolution and no reference following.** PDF has several ways to point
   out of the file and PdfPig follows none of them: a remote go-to action, a launch action and
   a submit-form action are each surfaced as data on an `Annotation` and never acted on. The
   adapter reads their target as *text*, because a URL in a document is a string a detector
   should see, not an address anything should resolve.

   The one place a parsed XML document could appear is `XmpMetadata`, whose `GetXDocument()`
   would hand the file's metadata packet to `System.Xml.Linq`. **The adapter never calls it.**
   It calls `GetXmlBytes()` and decodes the bytes as text, with BOM detection and strict UTF-8.
   XMP is RDF/XML, so it can carry a `DOCTYPE` with an external entity; decoding it as text
   means there is no resolver to configure, no `XmlReaderSettings` to get wrong, and nothing
   that a later refactor can quietly re-enable. The metadata is scanned as a string, which is
   all the pipeline needs from it.

   XFA — a form defined as an XML payload inside the document rather than as AcroForm fields —
   is refused outright rather than parsed, by looking for the `XFA` key in the form dictionary.
   An embedded file is refused the same way: a document carrying another document is a bundle,
   and an extractor cannot produce parts.
3. **Streaming, or at least bounded.** PdfPig is a random-access parser, not a streaming one:
   it needs the cross-reference table, which is at the end. So the bound is the adapter's. The
   byte ceiling is enforced *during* the copy of the part's stream, before the parser is handed
   anything, because a declared `Stream.Length` can lie and a `DocumentContent` built from a
   factory frequently has none. The page ceiling is checked against `NumberOfPages` before any
   page is touched. The form-field walk is bounded at 32 levels, because the field tree is a
   graph the file controls. Each bound has a test.
4. **Maintained.** Eliot Jones and contributors, continuously since 2017, under the
   `UglyToad` organization; 0.1.16 is from October 2026. The project's own corpus of real and
   malformed documents is large, and it is the reader most .NET projects that need text out of
   a PDF end up on.
5. **Permissively licensed.** Apache-2.0.
6. **No plugin or code-loading surface** reachable from parsing. `ParsingOptions` exposes a
   `FilterProvider` and a `Logger`; the adapter sets the logger to a sink that discards, leaves
   the filter provider at its default, and sets neither from input. There is no reflection over
   document content and no type loading driven by anything in the file.

PdfPig has **no package dependencies** on `net8.0` or `net10.0`. The seven assemblies are the
whole of what it adds to a consuming application.

### Parsing is strict

`UseLenientParsing` is `false` and `SkipMissingFonts` is `false`. This is the opposite of what
a document viewer wants and the right setting here. Lenient parsing exists so that a damaged
file still renders *something*; a reader that guesses at a broken object produces a page whose
text is partly recovered and silently partly absent. For this library, partly absent is the
failure. A document PdfPig cannot read strictly is refused, and a refused part blocks the
operation.

For the same reason text comes out through `ContentOrderTextExtractor` rather than
`Page.Text`. `Page.Text` concatenates glyphs in the order the content stream paints them,
which for a two-column layout interleaves the columns. An IBAN split across that interleaving
is an IBAN no detector matches.

### A page of pixels is refused, not accepted as empty

A page with no extractable text and at least one image is a scan. Returning it as an empty
segment would mean reporting that a page of a pay slip contains no sensitive data, which is
false in the only way that matters. It is refused with `extraction.unreadable_construct`,
which is a reason code added for this adapter. The way out is OCR, which is a later step and a
different package.

## Consequences

### Positive

- The pipeline can read the format the library's own reference scenario attaches.
- Every way this parser could reach out of the file — remote action, launch action, embedded
  file, XFA, XMP as XML — is either absent from the library or refused by the adapter, and each
  refusal has a test rather than a comment.
- The dependency graph stays closed: two format adapters so far, zero transitive packages
  between them.

### Negative

- Scanned documents cannot be read, only refused, until OCR exists. For an organization whose
  inbound PDFs are scans this is a blocked operation on arrival. It is the correct outcome and
  it is a real limitation, stated in the package description.
- The package is neither trimmable nor AOT-compatible, and says so in its own metadata; both
  flags are off in the project file. An application that publishes AOT can use the gateway and
  the MIME adapter, but not this one. That is visible at publish time rather than at runtime.
- Strict parsing refuses documents a viewer would open. A damaged file from a legitimate sender
  blocks rather than degrades. Loosening it is not a configuration option, by hard rule 6.

### Neutral

- PdfPig can also *write* PDF, through `UglyToad.PdfPig.Writer`. The adapter never touches it,
  and the writer is where the only `System.Xml` use in the library lives, alongside
  `DocumentLayoutAnalysis`'s exporters. Neither is on the read path.

## Alternatives considered

### iText 7

Rejected on criterion 5. It is AGPL-3.0 or a commercial licence, and an AGPL dependency in an
MIT library imposes the AGPL on anyone who deploys a service built on it. That is a licensing
decision this project cannot make on a consumer's behalf. Its PDF handling is excellent and the
licence is the whole of the objection.

### PDFsharp / MigraDoc

Rejected. It is a composition and manipulation library; text extraction is not a goal of the
project and what exists of it does not resolve font encodings well enough to be trusted with
"did we see everything on this page?".

### Docnet, PDFium-based wrappers

Rejected on criteria 1 and 6 together. A native PDFium binary is a large C++ parser whose IL
cannot be scanned, which means criterion 1 cannot be *checked* rather than that it fails. It
also adds per-RID native assets to every consuming application. The scan test that makes hard
rule 7 enforceable stops at the managed boundary, and a dependency that defeats the test is
worse than one that merely fails it.

### Write the parser here

Rejected in [ADR-0018](0018-format-adapters-may-take-a-parser.md) for all formats. PDF is the
clearest case: font encoding and reading-order recovery are where the bugs are, they are
invisible in testing with documents produced by one tool, and their failure mode here is
unscanned text rather than a crash.

### Accept an image-only page as empty

Rejected. It is the single most dangerous thing this adapter could do, and it is what every
PDF-to-text tool does by default.
