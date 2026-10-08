# 0013. Give replacement tokens a closed grammar, and escape content that imitates it

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** @iamjonatha

## Context

Anonymization replaces a span of text with a token. The document the token is written into is
untrusted ([ADR 0010](0010-document-content-is-untrusted.md)), and the validator that guards
egress ([ADR 0004](0004-fail-closed.md)) reads the sanitized text as a string. Everything
downstream — the validator, the audit trail, the re-identification step, a human reading the
payload — has to tell a token the gateway wrote apart from text the document happened to
contain.

Nothing in the sanitized text says where it came from. If a document contains the literal
`[PERSON_001]` and the gateway also emits `[PERSON_001]`, then re-identification resolves a
value the document never carried, the validator attributes a transformation that never
happened, and an attacker who knows the token shape can spend one line of a PDF to make the
gateway lie about its own output.

The replacement also has to survive being read by a decision provider. A token that varies in
shape between runs, or that differs per entity type in an undocumented way, becomes a pattern
that every consumer re-derives by inspection, and that every consumer gets slightly wrong.

## Decision

Replacement tokens have one grammar, it is closed, and content that imitates it is escaped
before it reaches the output.

1. The grammar is fixed and documented:

   ```text
   marker    := '[' body ']'
   body      := [A-Z0-9_]+
   pseudonym := [PERSON_001]              ; type, then a sequence of at least three digits
   hash      := [IBAN_H3F9A2C71B4D5E6F7]  ; type, then 'H', then 16 uppercase hex digits
   redaction := [REDACTED_EMAIL]
   ```

2. Masking produces no marker. It rewrites the value in place, preserving its length, and is
   therefore outside this vocabulary by construction.

3. Before any residual text is written to the output, every sequence that matches `marker` is
   escaped by doubling the opening bracket: `[PERSON_001]` becomes `[[PERSON_001]`. Escaping
   is applied to residual text only, never to a token the gateway itself emitted.

4. Escaping is driven by shape alone. A document containing `[OK]` is escaped too. The error
   is in the safe direction: an escape that was not needed is visible and reversible, a
   missing escape is a forged token.

5. A segment in which anything was escaped carries one `MarkerSyntaxEscaped` warning. The
   document stays `Valid`: the forgery attempt was neutralized, not merely detected.

6. Re-identification unescapes. `Unescape(Escape(text)) == text` for every input, so the
   application reading a restored document sees what the document actually said.

7. A hashed token is a keyed HMAC-SHA256 of the value, truncated to 8 bytes and rendered as
   16 uppercase hex digits. The key is supplied by the application through `HashingKey`,
   must be at least 32 bytes, and is never exposed by any member of the type. Hashing with
   no key configured throws; it does not silently fall back to pseudonymization.

8. `IPseudonymStore` is synchronous. The map is an in-memory artifact of the operation
   ([ADR 0007](0007-mapping-stays-inside-the-boundary.md)); an asynchronous signature would
   imply an out-of-process store as the normal case and invite exactly the durable,
   shared mapping that ADR rejects.

## Consequences

### Positive

- A token in sanitized text is a token the gateway wrote. The property holds against a
  document authored to break it, not only against accidents.
- The validator, the re-identifier and a human reader all use the same rule, and the rule is
  one page long.
- An unkeyed hash is impossible, so the reversible-by-enumeration failure mode that
  [ADR 0007](0007-mapping-stays-inside-the-boundary.md) warns about cannot be reached by
  omission.

### Negative

- Bracketed text that was never sensitive — `[OK]`, `[1]`, a citation marker — is escaped and
  reaches the provider in escaped form. Prompts and parsers downstream see it.
- `Escape` is not idempotent: escaping twice yields `[[[X]`. It is applied exactly once, at
  the one place that writes residual text, and that is a constraint on the implementation
  rather than a property of the function.
- The grammar is public API in everything but name. Widening it later is a breaking change
  for anyone who parses tokens, so the reserved shape is deliberately narrow.

### Neutral

- Truncating the HMAC to 8 bytes trades collision resistance for readability. Collisions
  within a document are a correlation artifact, not a disclosure: the token reveals nothing
  about the value either way.

## Alternatives considered

### Carrying the transformation as structured metadata instead of in the text

Rejected. It makes the text unforgeable at the cost of making the sanitized document
unusable as text, which is the one thing every decision provider needs it to be. The
metadata would then be duplicated into the text by every caller, badly.

### Rejecting a document that contains marker syntax

Rejected. It is fail-closed in the letter and useless in the spirit: any document with a
bracketed abbreviation would be blocked, and the blocking would be read as a bug by the
operator, who would then look for the switch that turns it off.

### A random delimiter chosen per operation

Rejected. It defeats forgery by unpredictability, but the delimiter then has to travel with
the document to every consumer, and a consumer that mislays it produces output that cannot be
validated at all. A fixed grammar plus escaping gets the same guarantee without the channel.
