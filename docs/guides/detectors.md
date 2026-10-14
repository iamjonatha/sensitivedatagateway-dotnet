# Detectors

A detector finds candidates. It does not decide what happens to them: that is the policy's
job, and keeping the two apart is what lets you change what you redact without changing what
you can find.

Every built-in detector is deterministic. The same text produces the same candidates, on
every machine and in every run. None of them reads a file, opens a socket or consults a
model, so none of them can leak the document it is scanning, and none of them can fail
because something elsewhere is down.

## The built-in set

`DeterministicDetectors.All()` is the set a deployment starts from. Removing one is a
decision about what the gateway is allowed to miss, and it belongs in the policy review
rather than in the wiring.

| Detector | Finds | Type reported | Confidence | Structural check |
| --- | --- | --- | --- | --- |
| `Email()` | Addresses | `EMAIL` | 0.95 | — |
| `Phone()` | International numbers, and numbers a label introduces | `PHONE` | 0.92 / 0.95 | — |
| `Iban()` | Bank account numbers | `IBAN` | certain | mod-97 |
| `CreditCard()` | Payment cards, grouped or plain | `CREDIT_CARD` | 0.95 | Luhn |
| `ItalianTaxId()` | Italian tax codes, including homocoded ones | `TAX_ID` | certain | check character |
| `Jwt()` | JSON web tokens | `JWT` | certain | — |
| `ApiKey()` | AWS, Google, Stripe, GitHub and Slack shapes, plus labelled assignments | `API_KEY`, `ACCESS_TOKEN` | certain / 0.85 | — |
| `PrivateKey()` | PEM blocks, and headers whose block is truncated | `PRIVATE_KEY` | certain | — |
| `ConnectionString()` | ADO strings carrying a password, and credentials written into a locator | `CONNECTION_STRING` | 0.95 / certain | — |
| `Password()` | Values assigned to something called a password | `PASSWORD` | 0.85 | — |
| `Url()` | Locators | `URL` | certain | — |
| `IpAddress()` | Version four, and version six in full form | `IP_ADDRESS` | 0.90 | — |
| `HighEntropy()` | Runs of text too dense to be prose | `API_KEY` | 0.75 | Shannon entropy |

Confidences are calibrated against the thresholds in the shipped policies. The strict policy
transforms at 0.90 and treats anything below as unresolved, which blocks; a detector reporting
0.85 for a shape that is only probably a key is saying that a human should look at it.

### A failed check digit drops the candidate

An IBAN whose mod-97 disagrees, a card number that fails Luhn and a tax code whose check
character does not match are not reported at all. They are not reported with a lower
confidence either.

This is deliberate. A number that fails its own checksum is not a damaged instance of that
kind of number; it is some other number that happens to have the right length. Reporting it
weakly would push the decision into the policy, where the only information available is a
confidence score that no longer means anything.

### What the reported span covers

For most patterns the span covers the whole match. Where a pattern anchors on a label — a
password assignment, an API key assignment, a labelled phone number — the span covers only
the value, so that a redacted document still reads as a configuration file with a password in
it rather than a file with a hole in it.

`ConnectionString()` covers the scheme, the credentials, the host and the port of a locator,
but not its path. The path is usually the database name, and it is not a credential.

## Measured behaviour

Against the annotated corpus in `DetectionCorpus` — fourteen documents, fourteen annotated
values, and a deliberate supply of near misses:

| Measure | Value |
| --- | --- |
| Recall | 100% |
| Precision | 93.3% |

The single false positive is `1000 2000 3000 4000`, which passes Luhn by coincidence.

Recall is held at 100% by a test, and precision at 90%. The two mistakes are not comparable:
a value the gateway misses is a value that reaches a model, while a value it reports in error
is a redaction someone has to review. Widening a pattern to raise precision at the cost of
recall is a change that needs an argument, not a commit message.

## What the built-in set does not find

These are limitations, not bugs. They are listed so that a deployment knows what it still has
to do.

- **Bare national telephone numbers.** `0212345678` is indistinguishable from any other run
  of digits. International form and labelled form are found; the rest is left to a pattern
  that knows the country's formats.
- **Version numbers that look like addresses.** `10.0.0.1` is reported as an IPv4 address.
  Release notes and build logs will produce false positives here.
- **Abbreviated IPv6.** `::1` and `2001:db8::1` are not matched. The abbreviated forms are
  indistinguishable from a time, a ratio or a chapter reference, and a detector that redacts
  timestamps is a detector an operator switches off.
- **Names, addresses and free-text personal data.** No deterministic pattern finds a person's
  name. That is what a semantic detector is for, and it is not in this phase.
- **Tax identification outside Italy.** Tax identification is national by nature.
  `ItalianTaxId()` is the first of a family, not a general detector; another country's code
  is another detector, with its own structure, its own check and its own version.
- **Vendor key formats that are not in the list.** `HighEntropy()` is the net under this, and
  it is a coarse one.

## Entropy

`HighEntropy()` reports any run of at least twenty characters, drawn from more than one
character class, whose Shannon entropy is at least 4.5 bits per character.

```csharp
var sensitive = DeterministicDetectors.HighEntropy(
    EntropyDetectorOptions.Default
        .WithMinimumLength(32)
        .WithMinimumBitsPerCharacter(5.0));
```

Raising either threshold finds less and complains less. Lowering either one starts reporting
base64-encoded prose, checksums and identifiers. It reports `API_KEY` with a confidence of
0.75, which the shipped policies transform but which is low enough to say plainly that the
detector does not know what it has found.

## Adding your own

The built-in detectors cover the shapes that look the same everywhere. An order reference, a
customer code, a case number or an employee identifier look the way one organization decided
they would look, and only that organization can write the pattern.

```csharp
[GeneratedRegex(@"\bORD-\d{5}\b", RegexOptions.None, 250)]
private static partial Regex OrderReference();

var detector = PatternDetector.Create(
    DetectorVersion.Create(DetectorId.Create("order-reference"), "1.0.0"),
    patternName: "order-reference",
    type: EntityType.Create("ORDER_REFERENCE"),
    sensitivity: SensitivityLevel.Low,
    confidence: DetectionConfidence.Create(0.9),
    pattern: OrderReference());
```

Four things are worth knowing before you write one.

**The expression must carry a match timeout.** `PatternDetector.Create` refuses one that does
not. An expression that can be made to take forever is a document that never gets refused, and
the guard is at the type rather than in a review checklist for that reason.

**Use bounded quantifiers.** `{1,64}` rather than `+`. An unbounded quantifier next to another
one is how a regular expression turns a long line into minutes of backtracking. The timeout
catches it, but a timeout is a blocked document, not a successful scan.

**Name your value group `value`** when the pattern has to anchor on a label. The reported span
then covers the group instead of the whole match.

**A new entity type needs a rule in every policy you use.** A policy that does not mention
`ORDER_REFERENCE` applies its `UnknownEntityAction`, and in both shipped policies that is to
block. See [the policy guide](policies.md).

## Running them

```csharp
var pipeline = new DetectionPipeline(DeterministicDetectors.All());

var detected = await pipeline.DetectAsync(extracted, cancellationToken);
```

Detectors run concurrently. The pipeline reconciles the spans they claim over the same
characters: the most damaging claim wins, then the more confident one, then the longer one,
and finally the one whose detector sorts first — not because that is meaningful, but because
the result has to be the same on every run and on every machine.

The claim that survives keeps the detector that made it. The full set of detectors that
examined the document is on `DetectedDocument.Detectors`.

### When a detector does not finish

A detector that throws, exceeds its time budget, or reports more candidates than
`MaxCandidatesPerDetector` allows is recorded as a `DetectionFailure`, and everything it
reported is discarded rather than truncated. `DetectedDocument.IsComplete` is then false, and
every later stage treats that as a reason to refuse.

```csharp
var options = DetectionPipelineOptions.Default
    .WithDetectorTimeout(TimeSpan.FromSeconds(5))
    .WithMaxCandidatesPerDetector(2_000);
```

Neither bound can be set to a value that makes the stage scan less and say nothing about it.
Exceeding either is a gap, and a gap blocks.

A detector is expected to honour the cancellation token it is given. The stage cannot
interrupt one that does not, which is the practical reason a detector must be a pure function
of the document: there is nothing else it could be waiting for.
