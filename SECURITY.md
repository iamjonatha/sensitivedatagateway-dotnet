# Security Policy

## Supported versions

The project is pre-release. Until `1.0.0` is published, only the latest released version
receives security fixes.

| Version | Supported |
| --- | --- |
| latest `0.x` | yes |
| older `0.x` | no |

## Reporting a vulnerability

**Do not open a public issue for a security problem.**

Report privately through
[GitHub Security Advisories](https://github.com/iamjonatha/sensitivedatagateway-dotnet/security/advisories/new).

A document that leaks a value the gateway should have caught is a security report, not a bug
report.

Please include:

- the affected package and version;
- a description of the impact;
- a minimal reproduction — **synthetic, never a real document**. Redact every real value. A
  synthetic reproduction is always preferred and is almost always sufficient;
- the policy and detector versions in use;
- any suggested mitigation.

You can expect an acknowledgement within 5 working days and an assessment within 15 working
days. Fixes are released as soon as practical, and the advisory is published once a fixed
version is available. Reporters are credited unless they ask not to be.

## Security properties this project commits to

These are treated as security requirements, not as best-effort behaviour. A regression in any
of them is a vulnerability. The full statement, including the residual risks, is in
[`docs/security/egress-guarantees.md`](docs/security/egress-guarantees.md).

1. **A provider cannot receive anything but a validated payload.** `IDecisionProvider<,>`
   accepts only `ValidatedPayload`, and only the privacy validator can construct one.
2. **Uncertainty blocks.** Unknown, unresolved, incomplete detection, a failed check and an
   exceeded limit all prevent transmission. There is no configuration that converts a block
   into a warning.
3. **The pseudonym map never crosses the boundary.** It is not a member of any type that
   leaves, it is unreachable from any provider package, it is volatile by default, and no
   re-identification channel is exposed to a provider.
4. **No document content reaches a log, a metric, a trace or an exception**, at any level.
   This is verified in CI by running an annotated corpus through the pipeline and scanning
   everything the library emitted.
5. **Nothing that reads raw content reaches the network.** `Core`, the pipeline and the
   document adapters perform no network I/O. Only `Providers.*` may, and it sees only a
   validated payload.
6. **Document content is never executed or acted on as instruction**, including inside a
   semantic detector.
7. **A limit blocks rather than truncates.** A truncated document is a document whose
   unexamined tail was silently dropped.
8. **Dependencies are auditable and classified.** Shipping packages reference only
   Microsoft-maintained framework packages, pinned centrally, with NuGet auditing enabled at
   build time, and every one is classified in
   [`docs/security/dependency-classification.md`](docs/security/dependency-classification.md).

## What is out of scope

- **Detector completeness.** An automatic detector has false negatives. A value that no
  detector recognizes is not a vulnerability in this library; it is the documented limit of
  the approach, and a detection improvement is a feature request. A value that a detector
  *did* find and that reached the output anyway **is** a vulnerability.
- **Indirect identification.** Removing every direct identifier does not make a document
  anonymous.
- **A compromised host**, a malicious operator inside the trust boundary, or an attacker with
  debugger access to the process.
- **Vulnerabilities in a downstream decision service.** Report those to its vendor.
- **Data you deliberately transmit** under a `Preserve` rule. The gateway documents what it
  transmits; deciding what is acceptable is the application's responsibility.
- **Side channels.** Payload size, entity counts, durations and request frequency are
  observable and permit inference. The gateway does not pad, delay or batch to defeat that.
- **Compliance conclusions.** The project provides mechanisms, not compliance. See
  [`docs/security/egress-guarantees.md`](docs/security/egress-guarantees.md).
