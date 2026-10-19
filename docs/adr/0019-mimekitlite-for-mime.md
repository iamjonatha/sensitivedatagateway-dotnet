# 0019. Read MIME with MimeKitLite

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** @iamjonatha

## Context

Phase 10 needs to turn an RFC 5322 message into a `DocumentBundle`. The reference scenario in
[`../architecture/overview.md`](../architecture/overview.md) is an email with attachments, so
this is the format the library most needs to read correctly.

Reading it correctly is not a weekend's work. A message is RFC 5322 headers with RFC 2047
encoded words, RFC 2045 transfer encodings, a charset label that is frequently wrong, and
thirty years of what mail clients actually emit as opposed to what the RFCs say. Under-reading
any of that is a privacy failure here specifically: a body this library fails to decode is a
body it never scans, and an unscanned body is forwarded to a decision engine intact.

[ADR-0018](0018-format-adapters-may-take-a-parser.md) permits one parser per format adapter,
against six criteria. This record takes MIME against them.

The .NET framework offers no candidate. `System.Net.Mail` composes messages; it cannot parse
one. `MailKit` parses, but it is a mail *client*: IMAP, POP3 and SMTP are the package. That
leaves MimeKit, which is the parser MailKit is built on and is published separately.

MimeKit ships in two packages built from the same sources:

- **MimeKit** — the parser plus S/MIME and OpenPGP.
- **MimeKitLite** — the parser alone.

## Decision

`SensitiveDataGateway.Documents.Mime` references **MimeKitLite**, and not MimeKit.

The difference is the whole reason for this record. MimeKit reaches the network in exactly two
places, both in `MimeKit.Cryptography`, and both found by disassembling the shipped assembly
rather than by reading its documentation:

| Method | Call |
| --- | --- |
| `BouncyCastleSecureMimeContext.DownloadCrlOverHttp` | `HttpClient.GetAsync` |
| `OpenPgpContext.RetrievePublicKeyRingAsync` | `HttpClient.GetAsync` |

Neither is on the parse path. Both are reached only by an application that constructs a
cryptography context and asks it to decrypt or verify, which this adapter never does. Under
criterion 1 that is not enough: the capability must be *absent*, not unused. MimeKitLite
removes it, so the adapter takes MimeKitLite and the question does not arise.

The six criteria, and how each was checked:

1. **No network.** `MimeKitLite.dll` was disassembled and every method body scanned for calls
   into `System.Net.Http`, `System.Net.Sockets`, `System.Net.Security`, `System.Net.WebSockets`
   and `System.Net.Quic`, and for calls to the individual transmitting types that live in
   otherwise harmless namespaces — `Dns`, `WebClient`, `WebRequest`, `SmtpClient`, `Ping`.
   There are none. The `System.Net.*` references that do remain are in-memory objects:
   `System.Net.Mime.ContentType` for a header, `System.Net.Mail.MailMessage` and its
   `Attachment`/`AlternateView` for conversion to and from the framework's own mail types, and
   `IPAddress.TryParse` to validate an address literal in a `Received` line. None of them opens
   anything. One method, `MimeUtils.GenerateMessageId`, reads the local host name through
   `IPGlobalProperties`; it generates no traffic, and it is reached only when *composing* a
   message, which this adapter never does. The scan is kept as a test — `NetworkCapabilityTests`
   — so a future MimeKitLite that gains the capability fails the build rather than shipping.
2. **No external entity resolution and no reference following.** A MIME message has three ways
   to point out of itself, and the adapter refuses all three rather than configuring them:
   `message/external-body` is a URL the parser does not fetch and the adapter does not claim, so
   the part blocks; `message/partial` is a fragment that is never reassembled; an encrypted or
   signed part is never decrypted, which with MimeKitLite is true because the code to do it is
   not present. Each has a test.
3. **Streaming, or at least bounded.** `MimeParser` is a streaming parser. The adapter bounds
   each part against `ProcessingLimits` as it flattens, using the encoded length before
   decoding, and then the decoded length. Nesting is bounded by the adapter's own walk;
   `ParserOptions.MaxMimeDepth` is set above that bound deliberately, because the parser does
   not fail when its own ceiling is reached — it hands back the remainder of the message as one
   unparsed part. The adapter refuses such a part outright. Both behaviours have a test.
4. **Maintained.** One author, Jeffrey Stedfast, continuously since 2013, releasing roughly
   monthly; 4.18.1 is from September 2026. It is the parser underneath MailKit, so the
   malformed-message corpus exercising it is very large.
5. **Permissively licensed.** MIT.
6. **No plugin or code-loading surface** reachable from parsing. The extension points MimeKit
   exposes — `ParserOptions`, `MimeEntityConstructor` — are set by the adapter, and the adapter
   sets neither from input. There is no reflection over message content and no type loading
   driven by a `Content-Type`.

MimeKitLite has **no package dependencies at all** on `net8.0` and `net10.0`. This is not a
rounding of the supply-chain cost but a removal of it: MimeKit would have brought
`BouncyCastle.Cryptography` and `System.Security.Cryptography.Pkcs`, the second of which was
already failing the build's NuGet audit on GHSA-2fmv-rv2c-59x7 and GHSA-846f-f764-76mx and had
to be pinned forward. That pin is gone with the dependency.

## Consequences

### Positive

- An application that reads email installs one package with a closed dependency graph.
- The "does an adapter reach the network?" question is answered by a test over the shipped IL,
  not by a promise in a document. That test is the enforcement of hard rule 7 for this package.
- The NuGet audit finding disappears along with the transitive dependency that carried it.

### Negative

- S/MIME and OpenPGP messages cannot be read, only refused. An organization whose mail is
  routinely encrypted gets a blocked operation rather than a scan. That is the correct outcome
  — scanning a payload this library could not decrypt would report "no sensitive data found"
  about bytes it never read — but it is a real limitation and it is stated in the package
  description, not buried here.
- Should decryption ever be wanted, it cannot be a flag on this package. It would be a separate
  package with its own ADR, because it changes what the dependency graph can do.

### Neutral

- The adapter's source does not depend on which of the two packages is referenced; they share
  an assembly name and namespace. The choice is a line in `Directory.Packages.props`, which is
  exactly why it needs a record: nothing in the code would reveal that it had been changed.

## Alternatives considered

### MimeKit

Rejected, on criterion 1. See the table above: the capability is in the package, and criterion
1 is about the package rather than about the call path. The adapter would have been correct
and the dependency graph would still have said that something in it can open an HTTP
connection, which is precisely the question the classification in
[`../security/dependency-classification.md`](../security/dependency-classification.md) exists
to answer cheaply.

### MailKit

Rejected without reaching the criteria. It is a mail client; IMAP, POP3 and SMTP come with it.

### `System.Net.Mail`

Rejected. It builds messages and does not parse them. There is no Microsoft-maintained MIME
parser to prefer under [ADR-0012](0012-dependency-policy.md).

### Write the parser here

Rejected in [ADR-0018](0018-format-adapters-may-take-a-parser.md) for all formats, and MIME is
the strongest case for that rejection rather than the weakest. The failure mode of a
home-grown MIME parser is a body that decodes to nothing and is silently not scanned.

### Reference MimeKit but trim the cryptography away

Rejected. Trimming is a property of how a consuming application publishes, not of what this
package depends on. An application that does not trim, or that trims with the wrong roots,
would still ship the capability, and the dependency graph would advertise it either way.
