# Performance

Numbers, not adjectives. Everything below was measured by the project in
[`benchmarks/`](../benchmarks/) and can be reproduced with one command.

```bash
dotnet run -c Release --project benchmarks/SensitiveDataGateway.Benchmarks -- --filter '*'
```

---

## 1. What was measured

Three document shapes, each at two sizes, through the five pipeline stages and through
`DocumentGateway.SanitizeAsync` end to end.

| Shape | What it is | Why it is here |
| --- | --- | --- |
| `Clean` | Prose with no sensitive value in it | The floor. What you pay to find nothing |
| `Realistic` | Synthetic corpus samples, blank line between them | A document that looks like a document |
| `Dense` | One synthetic value per line, no prose | The adversarial shape: every line is a hit |

The policy is permissive: it pseudonymizes everything and blocks nothing. That is the
expensive path on purpose. A blocking policy stops earlier and would produce a flattering
number that no one who needs the output would ever see.

The size parameter is characters of extracted text — 200,000 is a tenth of the default
`ProcessingLimits.MaxExtractedCharacters`.

---

## 2. End to end

`DocumentGateway.CreateDefault().SanitizeAsync`, one text part.

| Shape | Characters | Mean | Allocated |
| --- | ---: | ---: | ---: |
| `Clean` | 10,000 | 0.93 ms | 263 KB |
| `Clean` | 200,000 | 12.3 ms | 4.4 MB |
| `Realistic` | 10,000 | 3.3 ms | 987 KB |
| `Realistic` | 200,000 | 212 ms | 31.6 MB |
| `Dense` | 10,000 | 2.0 ms | 927 KB |
| `Dense` | 200,000 | 177 ms | 19.0 MB |

Read the first row as the cost of the guarantee itself. A document with nothing in it still
gets extracted, scanned by every detector, planned, rewritten and re-scanned, because a
pipeline that skips work on a document it believes is clean is trusting the belief it was
built to check.

---

## 3. Per stage

At 200,000 characters:

| Stage | `Clean` | `Realistic` | `Dense` |
| --- | ---: | ---: | ---: |
| Extract | 0.26 ms | 1.1 ms | 0.62 ms |
| Detect | 4.1 ms | 33.6 ms | 51.3 ms |
| Evaluate policy | 0.00007 ms | 0.31 ms | 0.57 ms |
| Anonymize | 0.30 ms | 1.8 ms | 57.1 ms |
| Validate | 5.8 ms | 152.0 ms | 50.6 ms |

Three things to take from that table.

**Detection and validation are the pipeline.** Together they are over 90% of every column.
Extraction is a decode and a split; the policy engine is a decision per entity over data
already in memory, and at `Clean` it is 73 *nanoseconds*.

**Validation is not a cheap afterthought.** It costs about as much as detection, sometimes
more, because it is a second independent implementation of the same question —
[ADR-0014](adr/0014-validator-re-derives-what-it-checks.md) says why it re-derives rather
than reading the answer from the stage it is checking. Paying twice is the design, not an
inefficiency in it.

**The worst shape is not the one with the most values.** `Dense` has far more entities than
`Realistic` and validates three times faster, because `Dense` is one paragraph and
`Realistic` is two thousand. See the next section.

---

## 4. The scaling rule you need to know

The anti-leak scan compares **every candidate value against every output segment**. It has
to: a value anonymized in one paragraph must not reappear in another, and a scan that only
looked at the paragraph a value came from would miss exactly the bug it exists to catch.

So the dominant term is `segments × entities`, not characters. A document split into many
small paragraphs each carrying a value is the expensive case, and `Realistic` at 200,000
characters is that case: roughly 2,000 paragraphs, and the validator's 152 ms is the whole
cross-product.

Practical consequences:

- Byte size alone does not predict cost. A 200 KB document can be 6 ms or 152 ms.
- The default `ProcessingLimits` bound both terms — `MaxExtractedCharacters` and
  `MaxEntities` — which is what keeps the cross-product finite. They are a denial-of-service
  control as much as a memory one.
- If you raise those limits, raise them knowing the scan is quadratic in their product.

---

## 5. Memory

The pipeline holds the whole extracted text of a bundle in memory, deliberately, and bounds
it instead of streaming it. [ADR-0024](adr/0024-bounded-buffering-not-streaming.md) has the
reasoning: three stages cannot answer their question from a window.

The practical figure is the `Allocated` column above: tens of megabytes of short-lived
allocation for a 200,000-character bundle, against the default 2,000,000-character ceiling.
Most of it is generation 0. The one place where the garbage collector is visibly involved is
the 400 KB string a 200,000-character part produces, which lands on the large object heap —
visible as the Gen2 counts in the raw report.

---

## 6. What these numbers do not say

- **They are not a service-level objective.** They are one laptop, one run, five iterations
  per case. The confidence intervals in the raw reports under
  `BenchmarkDotNet.Artifacts/results/` are wide, and some are very wide. Treat a figure here
  as an order of magnitude, not a budget.
- **They exclude every format adapter.** Extraction here is plain text. A PDF or an Open XML
  document costs what its parser costs, and that is PdfPig's number or the Open XML SDK's,
  not this library's.
- **They exclude semantic detection.** A model dominates anything measured here by orders of
  magnitude, and what it costs is a property of the model and where it runs. Measure your
  own.
- **They exclude the provider call.** The network is somebody else's latency.
- **They say nothing about concurrency.** Every measurement is one document at a time. The
  gateway holds no shared mutable state across operations, but that is a correctness claim,
  not a throughput one.

---

## 7. If you need it faster

In the order worth trying:

1. **Tighten the policy.** Preserving a type is free; the entity is still found, but nothing
   is rewritten and nothing is re-scanned for it.
2. **Narrow the detector set.** A detector you do not register is a pass over the text you do
   not make. Weigh that against what it would have found — this is a safety trade, not a
   tuning knob.
3. **Lower `MaxExtractedCharacters` and split the work upstream.** Smaller bundles keep the
   cross-product in section 4 small, at the cost of losing cross-document correlation unless
   you widen the pseudonym scope.
4. **Measure before and after.** The benchmark project takes a `--filter`; add your own shape
   to `Corpus` and run the two stages you care about.

What is not on the list: turning off the validator. There is no such setting, by design.
