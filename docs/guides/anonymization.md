# Anonymization

Anonymization is stage 4: it applies a `TransformationPlan` to a `DetectedDocument` and
produces a `SanitizedDocument`. It is the only stage that rewrites text, and the only one
that holds the mapping from a token back to the value it replaced.

This guide covers the five transformations, the token grammar, correlation scopes, and what
happens to the mapping afterwards.

## Applying a plan

```csharp
var store = new InMemoryPseudonymStore();
var anonymizer = new DocumentAnonymizer(store);

var sanitized = anonymizer.Anonymize(detected, plan, provenance);

if (sanitized.Status != SanitizationStatus.Valid)
{
    // Something planned could not be applied. Partial sanitization is not sanitization.
    return;
}
```

The anonymizer refuses a plan it cannot honour, rather than doing its best:

| Condition | Result |
| --- | --- |
| The plan was built for another bundle | `ArgumentException` |
| `plan.IsBlocked` | `InvalidOperationException` |
| `Hash` with no key configured | `InvalidOperationException` |
| `Session` or `Tenant` scope with no name configured | `InvalidOperationException` |
| `Encrypt`, `Block` or `RequireReview` | `NotSupportedException` |

The first two are programming errors: the policy engine already decided, and ignoring its
verdict here would make the block advisory. The rest are configuration errors, and a
configuration error that silently degrades to a weaker transformation is the failure this
project exists to prevent.

Anything the anonymizer could not complete — a planned entity missing from the document, an
incomplete extraction, an incomplete detection, an unresolved candidate, a partial overlap —
sets `Status` to `Incomplete` and adds a `SanitizationWarning`. Validation refuses an
`Incomplete` document.

## The five transformations

| Action | Produces | Reversible | Correlates |
| --- | --- | --- | --- |
| `Preserve` | the original text | — | — |
| `Mask` | `12****78` | no | no |
| `Redact` | `[REDACTED_EMAIL]` | no | no |
| `Pseudonymize` | `[PERSON_001]` | through the map, locally | within the scope |
| `Hash` | `[IBAN_H3F9A2C71B4D5E6F7]` | no | everywhere the key is the same |

**Masking** preserves length and keeps a prefix and a suffix: under 8 characters nothing is
visible, otherwise `min(4, length / 4)` characters on each side. It leaves a shape, which is
useful to a human reading the document and useless for identification.

**Redaction** says only that something of that type was there. It is the strongest
transformation and the one that survives any later mistake.

**Pseudonymization** is the only action that can be undone, and only by the application that
holds the map. Pseudonymized data is still personal data — see
[egress guarantees](../security/egress-guarantees.md).

**Hashing** needs a key:

```csharp
var options = AnonymizationOptions.Default.WithHashingKey(HashingKey.Create(keyMaterial));
var anonymizer = new DocumentAnonymizer(store, options);
```

The key must be at least 32 bytes and comes from the application; `HashingKey` never hands it
back. Two runs with the same key produce the same token for the same value, which is the
point and also the risk: it correlates across every document the key ever touched.
`HashingKey.CreateEphemeral()` exists for tests and for callers who want hashing without that
property.

## Token grammar

```text
marker    := '[' body ']'
body      := [A-Z0-9_]+
pseudonym := [PERSON_001]
hash      := [IBAN_H3F9A2C71B4D5E6F7]
redaction := [REDACTED_EMAIL]
```

Text in the document that matches `marker` is escaped by doubling the opening bracket, so a
document containing the literal `[PERSON_001]` reaches the provider as `[[PERSON_001]` and
cannot be mistaken for a token the gateway wrote. The segment carries one
`MarkerSyntaxEscaped` warning; the document stays `Valid`, because the forgery was
neutralized.

Escaping is by shape, so `[OK]` is escaped too. [ADR 0013](../adr/0013-token-vocabulary.md)
explains why the error is deliberately in that direction.

## Correlation scopes

A scope decides how far a pseudonym means the same thing.

| Scope | A token is stable across | Configure with |
| --- | --- | --- |
| `Document` | one part of the bundle | — |
| `Bundle` (default) | the whole bundle: an email and its attachments | — |
| `Session` | every bundle of a named session | `WithSession(name)` |
| `Tenant` | every bundle of a named tenant | `WithTenant(name)` |

```csharp
var options = AnonymizationOptions.Default.WithSession("case-4471");
```

Widening the scope widens the mapping: a `Tenant` map is a standing index of that tenant's
people. Name it explicitly or the anonymizer refuses to use it, because a scope that wide
should never be reached by a default.

## The mapping, and getting a value back

The map lives in the `IPseudonymStore` the application passed in. It is not a member of
`SanitizedDocument`, it is not reachable from `ValidatedPayload`, and a provider package
cannot name its type — see
[ADR 0007](../adr/0007-mapping-stays-inside-the-boundary.md). `InMemoryPseudonymStore` is
volatile and is the only implementation that ships.

Re-identification is an explicit local call:

```csharp
var restored = new LocalReidentifier(store)
    .Restore(answer, PseudonymScopeKey.ForBundle(bundleId));
```

`Restore` replaces the tokens of that scope with the values they stand for and unescapes
anything that was escaped on the way out. A token of another scope, a token of a released
map, a hash and a redaction all stay as they are: there is nothing behind them to restore.

Release the map as soon as the operation is over:

```csharp
store.Release(PseudonymScopeKey.ForBundle(bundleId));
```

## Related

- [Pipeline](../architecture/pipeline.md) — where stage 4 sits, and what blocks it
- [Policies](policies.md) — how the plan it applies is produced
- [Egress guarantees](../security/egress-guarantees.md) — what the output is promised to be
