# 0016. A refusal is a result; a broken pipeline is an exception

- **Status:** Accepted
- **Date:** 2026-10-07
- **Deciders:** @iamjonatha

## Context

The gateway façade has to decide, for every way an operation can end badly, whether the
caller gets a value back or a stack unwind. The decision is easy to make badly, and it is
made once for the whole library because inconsistency here is worse than either answer.

Two failure modes look similar from inside and are opposite from outside.

The first is the gateway working correctly and concluding that this document must not leave.
An unreadable attachment under a strict policy, an entity nobody could resolve, an anti-leak
check that found a value surviving in the sanitized text. Nothing malfunctioned. The gateway
was asked whether this document may be sent, and the answer is no.

The second is the gateway unable to answer at all. A bundle that produced neither text nor a
failure to report, a document with more entities than the policy's limit allows, a caller who
cancelled. There is no answer to return, because the question was never fully asked.

Treating the first as an exception is the tempting choice: a refusal feels exceptional, and
an exception is hard to ignore. It is wrong for three reasons. A blocked document is the
single most common non-trivial outcome in a system that exists to block documents, and
routine control flow through exceptions is slow and unreadable. The refusal carries a report
of which checks failed and why, and that report is the most useful thing the operation
produced — an exception type would either lose it or smuggle it through a property, which is
a result with extra steps. And callers write `catch` blocks that are wider than they intend;
a refusal expressed as an exception will eventually be caught by a handler that meant to
catch something else, and the document will be sent anyway.

Treating the second as a result is equally wrong. A caller that receives "not allowed"
because the bundle was malformed will log it alongside genuine policy refusals and never
learn that its own input is broken.

There is a third case that forced the shape of the pipeline: a plan that blocks is never
applied. The anonymizer refuses a blocked plan, deliberately, because applying a plan nobody
intends to send is work that can only produce a sanitized document that must be thrown away.
So a blocked plan has no sanitized document, and the validator — which is the only type
allowed to decide what leaves — was written to take one.

## Decision

**A refusal is a result.** `SanitizeAsync` and `ExecuteAsync` return a value with
`IsAllowed` false, a report naming the failed checks, and no payload. `ExecuteAsync` does not
call the provider.

**A pipeline that cannot answer throws.** `ProcessingLimitExceededException` when a limit is
exceeded, `ExtractionException` when a bundle yields nothing and reports nothing,
`OperationCanceledException` when the caller cancels or the processing budget runs out.

**Stage failures flow rather than throw.** An unreadable part becomes an extraction failure,
a detector that fails becomes a detection failure, and both travel to the validator as
evidence. The validator decides whether the scan was complete enough for this policy. The
stage that failed does not get to decide, because a stage that can suppress its own failure
is a stage that can be made to.

**The validator owns refusal too.** `IPrivacyValidator` gains
`Reject(detected, plan, policy)`, which runs the checks that do not need a sanitized
document — scan completeness, unresolved entities, policy violations — and returns a blocked
result. The gateway never constructs a refusal itself.

## Consequences

The validator is still the only producer of a `ValidatedPayload` and now also the only
producer of a refusal, so "what may leave" has exactly one implementation to audit.

A blocked operation skips anonymization entirely, which means the pseudonym store is not
touched and no tokens are minted for a document that is not going anywhere.

`PipelineRun.Sanitized` is nullable and `InspectionReport.Status` is nullable, both meaning
"anonymization did not run". A consumer that wants a non-null status is asking about a
document that was never going to be sent.

`Reject` can be called with a plan that does not block, which is a programming error, so it
throws `ArgumentException`. The check is cheap and the alternative is a silent path that
produces a refusal nobody can explain.

Callers must distinguish two things rather than one. That is the point: a blocked document is
an answer, and a failed operation is a bug or an attack, and conflating them is how a system
stops noticing that its input has been broken for a week.

## Alternatives rejected

**Throw on refusal.** Rejected above: common, loses the report, and gets caught by accident.

**Return a result for everything, including limits and cancellation.** A single `IsAllowed`
flag covering both "the policy said no" and "the bundle was malformed" produces dashboards
where the two are indistinguishable, and callers that retry a malformed bundle forever.

**Let the gateway build the refusal when the plan blocks.** It is three lines and it puts a
second author of refusals next to the validator. The next person to add a check would have to
know to add it in both places, and the one they forgot would be the one that mattered.

**Anonymize anyway, then validate, then discard.** It keeps one code path, at the cost of
minting pseudonyms for documents that are never sent and doing the most expensive stage for
no reason. It also makes the anonymizer's refusal of a blocked plan meaningless, and that
refusal is a useful assertion.
