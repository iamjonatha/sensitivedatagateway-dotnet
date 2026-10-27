# Testing

How to prove, over your own documents, that the gateway did what it says it did — and how
the project proves it over its own.

The audience is two people: the application developer who wants a test that fails when a
customer's name reaches a vendor, and the contributor who is about to change a stage and
needs to know which tests are allowed to go red.

---

## 1. The testing package

`SensitiveDataGateway.Testing` is a shipped package, not a test helper that leaked out of
the repository. It is for consumers first.

| Type | For |
| --- | --- |
| `LeakAssert` | Asserting that a set of values is not in a payload, a provider call or a written line |
| `RecordingDecisionProvider<TRequest, TResult>` | Standing in for a provider and keeping every payload it received |
| `FakeDecisionProvider<TRequest, TResult>` | Returning a fixed decision, deciding from the payload, or failing on purpose |
| `RecordingAuditSink` | Keeping every audit record, so a test can scan them |
| `SyntheticCorpus` | Nine annotated documents in five languages, with the sensitive values listed |
| `SyntheticDocument`, `SensitiveValue` | One document and one annotated value in it |
| `TestBundle` | Building a bundle from text, from a body and an attachment, or of a given size |
| `TestPolicy` | A permissive policy, a blocking one, a builder, and one bounded by limits |
| `Leak`, `LeakDetectedException` | What a failed assertion reports |

It has no test-framework dependency. It works under xUnit, NUnit, MSTest or a console
program, because an assertion that drags a framework in cannot be used by a consumer who
chose a different one.

**Nothing in it can forge a payload.** `ValidatedPayload` has no public constructor, and the
testing package does not have internal access to the libraries. A fake provider receives a
payload the validator produced, or it receives nothing.

---

## 2. The one test an application should write

The question worth asking is not "does the gateway work" — that is this project's problem.
It is "does *my* document, under *my* policy, stop leaking what I think it leaks".

```csharp
using SensitiveDataGateway;
using SensitiveDataGateway.Testing;

[Fact]
public async Task The_claim_form_never_carries_the_claimant()
{
    var provider = RecordingDecisionProvider.Returning<ReviewRequest, ReviewDecision>(
        new ReviewDecision("approved"));

    var gateway = new DocumentGateway<ReviewRequest, ReviewDecision>(
        DocumentGateway.CreateDefault(),
        provider);

    await gateway.ExecuteAsync(
        TestBundle.FromText(ClaimForm),
        new ReviewRequest("fraud-review"),
        SanitizationPolicy.Balanced,
        cancellationToken);

    LeakAssert.NoneOf(provider, ["Ada Lovelace", "ada.lovelace@example.com"]);
}
```

Use a document shaped like yours, with invented values. A test fixture containing a real
claimant is a breach whose blast radius is your git history.

### What `LeakAssert` is, exactly

A case-insensitive substring search over two forms of the text: as written, and with all
white space removed. That second form matters because wrapping and column alignment split
account numbers across lines.

It is deliberately blunt. It does not know what a marker is, and it will not notice a value
that was encoded, split across segments or re-spelled. A test that passes has not proven the
gateway correct. **A test that fails has proven it broken**, and that asymmetry is the whole
point.

Values shorter than four characters are refused rather than skipped: a two-character search
term appears in almost any prose, and a silently skipped value is a test that reports success
over something it never looked at.

A failure throws `LeakDetectedException`, which reports where the value was found and how
long it was, and **never repeats the value**. A failure message ends up in a console, a log
file and a CI artifact; printing the value there would publish it in three more places than
the test was complaining about.

### Asserting over logs and audit records

`NoneIn` takes any sequence of lines, so the same assertion covers what you logged:

```csharp
var sink = new RecordingAuditSink();
var gateway = DocumentGateway.CreateDefault(GatewayOptions.Default.WithAuditSink(sink));

// ... run the document through ...

LeakAssert.NoneIn(sink.Records.Select(record => record.ToString()), sensitiveValues);
```

---

## 3. The synthetic corpus

`SyntheticCorpus.All()` returns nine documents — English, Italian, German, French and
Spanish — covering a claim, a complaint, a case file, a financial file, a supplier contract,
a dump of credentials, and two documents that try to give the reader instructions. Each one
lists the sensitive values it contains, by entity type.

```csharp
foreach (var document in SyntheticCorpus.All())
{
    var result = await gateway.SanitizeAsync(document.ToBundle(), policy, cancellationToken);

    LeakAssert.NoneOf(result.Payload!, document.Values.Select(value => value.Text));
}
```

Every value in it is invented: the IBANs carry valid check digits and belong to no account,
the tax codes are well-formed and belong to no person, and the credentials are syntactically
plausible and authenticate against nothing.

The corpus is annotated, which makes it usable in the other direction too. The project's own
suite asserts that every annotated value is actually detected — if the corpus listed a value
no detector finds, the leak tests around it would pass for the wrong reason.

---

## 4. The security suite

`tests/SensitiveDataGateway.Security.Tests` is where the guarantees in
[`../security/egress-guarantees.md`](../security/egress-guarantees.md) are enforced.

It is built under one rule: **no internal access**. The project grants it no
`InternalsVisibleTo`, so every scenario in it is one a consumer of the published packages
could write. A guarantee that only holds from inside the assembly is not a guarantee.

| Area | What it establishes |
| --- | --- |
| Corpus egress | Every annotated value is detected, transformed, and absent from the payload, the provider call and the audit record |
| Fail closed | A detector that throws, an anonymizer that rewrites nothing, an unresolved entity the policy will not tolerate, and an oversized part each stop transmission — under the most permissive policy the gateway accepts |
| Prompt injection | A document that issues instructions is handled like any other, leaks nothing, and has its instruction carried through as content |
| Forged markers | A marker written in the source is escaped rather than believed, and does not shield a value beside it |
| Exception surface | Every failure path is driven to failure, and the whole exception is scanned against the corpus |
| `LeakAssert` | The instrument itself fails when it should, and its report never repeats the value |

Two conventions in it are load-bearing:

- **Each scenario breaks exactly one stage** and leaves the rest honest, so the outcome is
  attributable to the stage that was broken.
- **Every scenario uses the permissive policy.** A block under a strict policy proves the
  policy. A block under a policy that was given every reason to allow the document proves
  the pipeline.

---

## 5. Which tests may change

| Suite | When it may go red |
| --- | --- |
| `SensitiveDataGateway.Tests` | Freely, when behaviour changes deliberately |
| `SensitiveDataGateway.Security.Tests` | Effectively never |

A change that makes a security test fail is a change that weakened a guarantee. The fix is
the change, not the test. If a guarantee genuinely should be narrower, that is an ADR and a
rewritten row in `egress-guarantees.md` — and the ADR comes first.

Deleting a security test is a reviewable event. It is the one way the suite can be made to
pass while the property it protected is gone, so it gets read as if it were a change to the
threat model, because it is one.

---

## 6. Writing a test in this repository

The rules that trip people up:

- **A bug fix starts from a failing test.** Not a test written after the fix that would have
  failed; one that actually ran red first. The regression you are fixing is only pinned down
  by a test that demonstrably detects it.
- **No real clock, no real network, no real delay.** `GatewayOptions` takes a `TimeProvider`;
  use `TimeProvider.System` nowhere in a test.
- **No real value in a fixture.** Not a name, not an address, not an account number — see
  [`../../AGENTS.md`](../../AGENTS.md).
- **Assert on the guarantee, not on the wording.** `Assert.Contains(PrivacyCheck.AntiLeakScan,
  …)` survives a rephrased message; asserting on the message does not.
- **A new detector, transformation or check ships with the test that proves it fails closed
  when it fails.** The happy path is the easy half.

### Running them

```bash
dotnet test --solution SensitiveDataGateway.slnx -c Release
```

Everything runs on both `net8.0` and `net10.0`. A test that passes on one and not the other
is a defect in the code under test far more often than in the test.

---

## 7. What the suite still does not prove

Every test above establishes that the code passes. None of them establishes that a test would
*fail* if a fail-closed branch stopped closing. Those are different claims, and for three
components the difference is the whole product: the validator, the policy engine and the
egress guard all fail silently when they fail. A validator that returns `Allowed` because its
loop never executed looks exactly like a validator that checked and found nothing.

Mutation testing is the instrument for that. It rewrites the source one small change at a
time — flips a comparison, empties a collection, inverts a condition — and asks whether
anything turns red. A surviving mutant is a behaviour change no assertion noticed.

**It is not running today, and the reason is recorded in
[ADR-0025](../adr/0025-development-tools-and-deferred-mutation-testing.md).** In short: this
suite runs on Microsoft.Testing.Platform, Stryker.NET drives tests through VSTest, and the
two have not met yet. Bridged onto VSTest it does produce a report — a mutation score of
0.00%, with every mutant in `SourceSpan.cs` surviving although `SourceSpanTests.cs` asserts
that type directly. The tests are discovered and are not deciding anything. A number that
confident and that wrong is worse than no number at all, which is why none is published.

Until that changes, treat the following as the substitute, and as a weaker one:

- the egress suite, which asserts the guarantee rather than the implementation, and which may
  effectively never go red (§5);
- the rule that a new detector, transformation or check ships with the test that proves it
  fails closed (§6);
- the requirement that a bug fix starts from a test that actually ran red (§6), which is the
  one habit that gives a kill-like signal by hand.

When the tool can run here, the first run should be scoped to those three components rather
than to the whole repository, and a surviving mutant in any of them should be read as a
missing assertion until someone demonstrates otherwise.
