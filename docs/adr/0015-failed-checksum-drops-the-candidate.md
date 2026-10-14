# 0015. A failed check digit drops the candidate

- **Status:** Accepted
- **Date:** 2026-10-06
- **Deciders:** @iamjonatha

## Context

Several of the things the gateway looks for carry their own arithmetic. An IBAN has a mod-97
remainder, a payment card has a Luhn digit, an Italian tax code has a check character. The
pattern that finds them is weak on its own — `[A-Z]{2}\d{2}[A-Z0-9]{11,30}` matches a great
many strings that are not bank accounts — and the arithmetic is what makes the detector
usable.

What happens when the arithmetic disagrees is a design decision, and the obvious answer is
the wrong one.

The obvious answer is to report the candidate anyway with a reduced confidence, on the
grounds that the gateway exists to be cautious and that a transcription error in a real IBAN
would otherwise be missed. It keeps information rather than discarding it, and information
discarded at stage 2 cannot be recovered at stage 3.

The difficulty is that a confidence is a number the policy compares against a threshold. For
the number to mean anything, it has to answer one question: how likely is it that this is
what I say it is. A candidate that fails its checksum is not a less likely IBAN. It is a
string of the right length that is not an IBAN, and the categories are different in kind
rather than in degree. Reporting it at 0.4 does not express doubt; it mixes a different
question into a scale that already had one, and every threshold downstream becomes harder to
set because the number now means two things.

The transcription-error argument also does not survive contact with the numbers. The strict
policy transforms at 0.90 and treats anything below as unresolved, which blocks. A degraded
candidate would therefore block the document, not redact it, so the behaviour it buys is not
"we caught the damaged IBAN" but "we refused the document because something was the right
length".

## Decision

A detector that applies a structural check reports a candidate only when the check passes. A
candidate that fails is not reported at all — not at a lower confidence, not with a warning,
not as a different type.

This applies to `Iban()` (mod-97), `CreditCard()` (Luhn) and `ItalianTaxId()` (check
character and structural validity, including the homocode substitutions and a plausible day
of birth).

Confidence is reserved for what it already means: how likely the detector thinks it is that
the shape it matched is the thing it is looking for. `Iban()` and `ItalianTaxId()` report
certainty because their checks are strong. `CreditCard()` reports 0.95 rather than certainty
because Luhn is weak — roughly one in ten arbitrary digit sequences passes it.

## Consequences

### Positive

- A confidence remains comparable across detectors, so a policy threshold means the same
  thing everywhere. This is what makes the strict policy's 0.90 a decision rather than a
  guess.
- Precision stays high enough to publish. On the annotated corpus the detector set reports
  one false positive in fifteen claims, and that one is a digit sequence that passes Luhn by
  coincidence — exactly the residue this decision predicts.
- The failure mode is the quiet one. A dropped candidate costs a false negative on a
  malformed value; a degraded candidate costs a blocked document on every number of the right
  length, which is the kind of noise that gets a gateway switched off.

### Negative

- A genuinely mistyped IBAN or card number in a document is not found by these detectors. If
  it is long and dense enough, `HighEntropy()` may still catch it; usually it will not.
- The checks are themselves code that can be wrong, and a bug in one silently removes a whole
  class of detection rather than loudly breaking something. The checksum implementations are
  therefore tested directly, against values verified independently of this repository, rather
  than only through the detectors that use them.
- Adding a national identifier means implementing its check digit before the detector is
  worth shipping, which is more work than writing a pattern.

### Neutral

- Nothing downstream needs to know whether a check was applied, but the evidence records it
  anyway: `DetectionEvidence.ChecksumValidated` is true on exactly these candidates. An audit
  can distinguish a pattern match from a verified one without re-running detection.

## Alternatives considered

### Reporting the failed candidate at a reduced confidence

Rejected, for the reason above: it overloads a scale that has one question on it, and under
the shipped thresholds it produces blocks rather than redactions.

### Reporting the failed candidate as a different, weaker entity type

Rejected. It moves the problem into the policy, which would then need a rule for
`POSSIBLE_IBAN`, and a deployment that forgets the rule blocks on it — an unknown type is
refused by both shipped policies. The gateway would be creating work for every operator in
order to preserve a signal that is nearly always noise.

### Making the behaviour configurable

Rejected. A switch that turns a check off is a switch that makes the gateway report values
that are not values, and the cost of the resulting false positives is paid by whoever reviews
redactions, not by whoever sets the switch. It also conflicts with
[ADR 0004](0004-fail-closed.md): the configurable option that gets chosen under pressure is
always the permissive one.

### Validating the checksum in the policy rather than in the detector

Rejected. The policy would then need to know the internal structure of every identifier it
has a rule for, which is the coupling [ADR 0006](0006-detection-policy-anonymization-separation.md)
exists to prevent. Structure is what a detector knows; consequence is what a policy knows.
