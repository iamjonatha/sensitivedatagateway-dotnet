# Architecture Decision Records

An ADR records a decision that is expensive to reverse, together with the context that made
it necessary and the consequences accepted along with it. Read them before proposing an
architectural change.

Records are immutable. A decision is changed by adding a new record that supersedes the old
one, never by editing history.

## Writing a new record

1. Copy [`0000-template.md`](0000-template.md) to `NNNN-short-title.md`, using the next free
   number.
2. Write it in English, in the imperative, and keep it short.
3. Open it as a pull request in `Proposed` status; it becomes `Accepted` when merged.
4. Add it to the index below.

A new dependency, a new package that can read raw content, and any change to what may be
logged all require a record before the code is written.

## Index

| # | Title | Status |
| --- | --- | --- |
| [0001](0001-provider-independent-core.md) | Keep the gateway independent of any decision provider | Accepted |
| [0002](0002-target-frameworks.md) | Target net8.0 and net10.0 | Accepted |
| [0003](0003-package-layout.md) | Ship a consolidated package layout, not one package per pipeline stage | Accepted |
| [0004](0004-fail-closed.md) | Fail closed, and make a block a result rather than an exception | Accepted |
| [0005](0005-typed-pipeline-stages.md) | Give every pipeline stage its own type | Accepted |
| [0006](0006-detection-policy-anonymization-separation.md) | Separate detection, policy and anonymization | Accepted |
| [0007](0007-mapping-stays-inside-the-boundary.md) | Keep the pseudonym mapping inside the trust boundary | Accepted |
| [0008](0008-privacy-safe-observability.md) | Make logging, telemetry and errors privacy-safe by default | Accepted |
| [0009](0009-no-network-in-core.md) | Keep the network out of everything that reads raw content | Accepted |
| [0010](0010-document-content-is-untrusted.md) | Treat document content as untrusted data, never as instruction | Accepted |
| [0011](0011-decisionkit-first-integration.md) | Adopt DecisionKit as the first provider integration | Accepted |
| [0012](0012-dependency-policy.md) | Restrict shipping dependencies and classify every one of them | Accepted |
| [0013](0013-token-vocabulary.md) | Give replacement tokens a closed grammar, and escape content that imitates it | Accepted |
| [0014](0014-validator-re-derives-what-it-checks.md) | Give the validator the whole pipeline, not just its output | Accepted |
| [0015](0015-failed-checksum-drops-the-candidate.md) | Drop a candidate whose check digit disagrees, rather than reporting it weakly | Accepted |
