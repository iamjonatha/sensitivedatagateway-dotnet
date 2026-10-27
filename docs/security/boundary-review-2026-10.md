# Boundary review — October 2026

An internal review of the egress boundary, carried out before the first release.

**This is not an external audit.** It was performed by the people who wrote the code, which
is the weakest kind of review there is: it can find a defect nobody thought about, but it
cannot find a defect everybody involved shares an assumption about. No independent party has
examined this library. Treat the result as evidence that the boundary was looked at on
purpose, not as assurance that it holds.

An external review remains open; it is listed in [`../roadmap.md`](../roadmap.md).

---

## 1. Scope

What the review tried to break, stated as questions rather than areas:

1. Can a document be blocked and still reach a provider?
2. Can content reach a log, a metric, a trace attribute or an exception message?
3. Can the pseudonym map be reached from anything that leaves the boundary?
4. Can a document instruct the gateway, rather than merely be read by it?
5. Can a marker be forged, so that a sanitized document appears to contain a pseudonym it
   does not?
6. Can a `ValidatedPayload` be obtained without running the validator?
7. Can a hostile document make the gateway spend unbounded time or memory?
8. Is the cryptography used for what it claims?

Questions 1 to 5 and 8 found nothing. Questions 6 and 7 did.

## 2. Findings

| # | Severity | Where | What | Status |
|---|----------|-------|------|--------|
| 1 | High | `PrivacyValidator`, `DocumentGateway` | A payload could be manufactured without running the pipeline | Fixed |
| 2 | Medium | `EntityReconciler` | Resolution was quadratic and ignored cancellation | Fixed |
| 3 | Medium | `PresidioOptions` | A hosted analyzer could be addressed over cleartext | Fixed |

### Finding 1 — a payload could be manufactured

`PrivacyValidator` was public, and so were the factories for all four of its arguments. A
caller could therefore build a `SanitizedDocument` holding arbitrary text, pass a plan with
no decisions in it, and receive a genuine `ValidatedPayload` — together with a report that
truthfully recorded ten passed checks, because every check compares the sanitized text
against the decisions, and there were none to compare it against.

This defeated G2 without touching the private constructor that was supposed to enforce it.

The fix closes the producer rather than the data types. `PrivacyValidator` and
`IPrivacyValidator` are now internal, and the gateway constructs the validator itself instead
of accepting one. The XML documentation already said the checking "is not optional and not
configurable"; the code now agrees with it.

`SanitizedDocument.Create` stays public, because a sanitized document is not a payload and no
provider accepts one. Injecting a validator was never the hole either: an external
implementation could not produce a payload regardless, because `SanitizationResult.Allowed`
is internal. The public, unimplementable interface was the visible edge of the real defect.

`PayloadProvenanceTests` now enumerates the public surface of every shipping assembly and
fails if any member other than three named readers yields a payload.

### Finding 2 — resolution was quadratic and uninterruptible

`EntityReconciler` compared each candidate against every candidate it had already accepted,
and never looked at the cancellation token. The detection stage admits up to
`MaxCandidatesPerDetector` from *each* detector, so a document crafted to saturate thirteen
detectors could present enough candidates to occupy a core for hours, and cancelling the
request would not stop it.

The fix keeps the accepted spans ordered per segment and binary-searches them, which is
correct for the same reason the loop was: acceptances are pairwise disjoint, so only the two
spans nearest the candidate can collide with it. The token is now passed in and observed.

No bound was added. Bounding the input would have hidden the defect rather than removed it,
and would have widened the public options surface to do so.

### Finding 3 — a hosted analyzer could be addressed over cleartext

`PresidioOptions.ForHostedService` accepted `http`. A hosted analyzer receives the document
in full, before anything has been removed from it — that is N9 — so cleartext there exposes
every document to anyone on the path. `ForSelfHosted` still accepts `http`, because a
container reached over a loopback or a bridge the deployment owns gains nothing from a
certificate.

## 3. What the review did not cover

- The decision providers' own transport, retention and logging. N4 says the gateway governs
  its own path only, and this review did not extend past it.
- The correctness of the detectors. A missed entity is N1, not a boundary defect, and the
  review assumed detection is incomplete rather than trying to measure how incomplete.
- The third-party parsers, beyond confirming that none of them can reach the network.
- Timing and other side channels.
- The published packages, as opposed to the source. Nothing here verifies the build.

## 4. Reproducing it

The structural claims are tests, not prose. The suite in
`tests/SensitiveDataGateway.Security.Tests` fails if any of them stops holding:

```bash
dotnet test --solution SensitiveDataGateway.slnx -c Release
```

Section 4 of [`egress-guarantees.md`](egress-guarantees.md) says which test proves which
guarantee.

## 5. Reporting something this missed

See [`../../SECURITY.md`](../../SECURITY.md).
