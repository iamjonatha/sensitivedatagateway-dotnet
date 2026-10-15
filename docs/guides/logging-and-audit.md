# Logging and audit

The gateway writes two things outside the process, and neither can carry document content.

| Artefact | Who reads it | What it answers |
| --- | --- | --- |
| Log | An engineer looking at a live system | What is the pipeline doing right now |
| Audit record | An operator, a regulator, an incident review | What happened to this document, and why |

They are separate because the questions are separate. A log is sampled, truncated, shipped to
whatever the platform provides and routinely too verbose to keep. An audit record is exactly
one per operation, structured, and worth retaining. Collapsing them would mean either losing
records to a sampling rule or retaining debug noise forever.

## Logging

Supply an `ILogger` and the gateway uses it. Supply none and it writes nothing at all —
`GatewayOptions.Default.Logger` discards.

```csharp
var options = GatewayOptions.Default.WithLogger(loggerFactory.CreateLogger("SensitiveDataGateway"));
```

Ten events, with stable identifiers:

| Id | Level | Event |
| --- | --- | --- |
| 1 | Debug | Operation started |
| 2 | Debug | Extraction finished |
| 3 | Debug | Detection finished |
| 4 | Debug | Policy evaluation finished |
| 5 | Debug | Anonymization finished |
| 6 | Debug | Validation finished |
| 7 | Information | Operation allowed |
| 8 | Warning | Operation blocked |
| 9 | Error | Operation failed |
| 10 | Warning | Operation cancelled |

Every message is source-generated, so a disabled level costs nothing and nothing is formatted
until a sink asks for it.

### No message takes a string

Not "no message takes document text" — no message takes a `string` at all. The distinction
matters, because the first rule is a judgement someone has to make correctly every time and
the second is a property a machine can check.

A string parameter is how content reaches a log. Someone needs one more detail while
diagnosing an incident, the nearest string is the segment text, and the log file becomes a
copy of the document with none of the document's protections. Forbidding the parameter type
removes the opportunity rather than relying on the next author to resist it.

What is left is counts, durations, enumerations, `Guid`s, and the identifier structs the
application itself chose — `BundleId`, `PolicyId`, `PolicyVersion`. It is enough to follow an
operation through the pipeline and never enough to learn what the document said.

One consequence is deliberate: the log carries `GatewayErrorCategory` and not the error code,
because a code is a string. An operator who needs the code reads the audit record, which is
the artefact built for that question.

Two tests enforce this, and both run in CI:

- a reflection test over every `[LoggerMessage]` method in every shipping assembly, which
  fails if any parameter is a `string` or an `object`;
- a corpus harness that runs the annotated detection corpus through the gateway with a
  capturing logger and asserts no annotated value appears in any formatted message, any
  structured field or any scope.

Supply your own logger and the first test still protects you. The second only covers what the
library writes; a log statement in your own code is yours to check.

## Audit

Implement `IAuditSink` and the gateway calls it once per operation:

```csharp
public sealed class DatabaseAuditSink : IAuditSink
{
    public async ValueTask WriteAsync(AuditRecord record, CancellationToken cancellationToken) =>
        await _store.AppendAsync(record, cancellationToken);
}
```

### Exactly one record, always

Allowed, blocked, failed with an exception, cancelled mid-flight: one record, every time. The
write happens in a `finally`, and it uses `CancellationToken.None` rather than the caller's
token — a cancelled operation that left no trace is precisely the operation an incident
review needs to see.

| `AuditOutcome` | Meaning |
| --- | --- |
| `Allowed` | A payload was produced |
| `Blocked` | The policy or the validator refused; no payload exists |
| `Failed` | An exception ended the operation |
| `Cancelled` | The caller cancelled, or the processing budget ran out |

A sink must not throw. If it does, the exception surfaces from the gateway call and, when it
happens inside the `finally` of a failing operation, it replaces the original exception. The
gateway does not swallow it: a sanitization that silently lost its audit trail is worse than
one that failed loudly. Catch inside your sink, buffer, and fail your own way.

### What a record holds

Identifiers, versions, timings and counts: `OperationId`, `Operation`, `Outcome`, `BundleId`,
`PolicyId`, `PolicyVersion`, `GatewayVersion`, `StartedAt`, `Duration`, `EntityCount`,
`TransformedCount`, `GapCount`, `FailedChecks`, `ErrorCategory`, `ErrorCode`, `ProviderName`,
and two optional digests.

It holds no entity value, no offset, no segment text and no pseudonym mapping. `ToString()`
is safe to write anywhere, and the corpus harness asserts that it is.

### Where the payload went

`ProviderName` is the only field that says who received content, and it is set if and only if
content left the gateway:

| Situation | `ProviderName` |
| --- | --- |
| `Execute` that reached the provider | The provider's name |
| `Execute` that reached the provider and then failed | The provider's name |
| `Execute` blocked before the provider | `null` |
| `Sanitize` or `Inspect` | `null` |

A failed call still names the provider, because the payload crossed the boundary whether or
not a decision came back. That record is exactly the one an auditor needs attributed.

The name is read once, when the provider is composed into
`DocumentGateway<TRequest, TResult>`, before any document exists. A provider that built its
name out of the content it was handed would have nothing to build it from, so the field cannot
become a back channel into the one record that is meant to be safe to keep. A provider that
returns an empty name is refused at composition rather than producing records nobody can
attribute.

### Digests

Off by default. Supply a key and every record carries a `DocumentHash`, and an `Execute` or
`Sanitize` record that produced a payload also carries a `PayloadHash`:

```csharp
var options = GatewayOptions.Default.WithHashing(HashingKey.Create(secret));
```

The digests answer "have we seen this document before?" without keeping the document.
Whoever holds the key can ask that question; whoever does not cannot test a guess against a
record, which is why the hash is keyed rather than plain.

An `Inspect` record never carries a `PayloadHash`, because inspection never produces a
payload.

The same key also keys the hashing transformations a policy asks for, so a value hashes to
the same token across processes. Without a key, the gateway still hashes — a policy that asks
for one gets one — but with a per-instance secret, so the token is stable inside that gateway
and reproducible nowhere else. That is the safe default: correlation across processes is a
capability, and a capability nobody asked for should not be on.

## What the gateway never does

- It never logs or audits document text, an entity value or an offset.
- It never writes a pseudonym mapping anywhere. The mapping is reachable from the
  reidentifier and from nothing else.
- It never degrades a block into a warning because a sink or a logger was unavailable.

[Egress guarantees](../security/egress-guarantees.md) states the full set and how to verify
each one.
