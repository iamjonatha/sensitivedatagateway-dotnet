# 0025. Separate development tools from shipping dependencies, and defer mutation testing

- **Status:** Accepted
- **Date:** 2026-10-27
- **Deciders:** @iamjonatha

## Context

Phase 13 asked for mutation testing over the three components whose failure is a privacy
incident rather than a bug: the validator, the policy engine and the egress guard. Line
coverage says those lines ran. It does not say an assertion would have noticed had they
behaved differently, and for a fail-closed boundary that distinction is the whole point.

The request raised a policy question first.
[ADR-0012](0012-dependency-policy.md) forbids non-Microsoft runtime dependencies and already
permits third-party packages in test and benchmark projects, "for the test framework,
coverage collection and BenchmarkDotNet". A mutation testing tool is neither of those: it is
a `dotnet tool` that is never referenced by a project and never appears in any package graph
we publish. ADR-0012 does not say what to do with it, and the gap should not be closed by
whoever happens to need a tool next.

The second question was whether the chosen tool works here, and that one has a measured
answer. This repository runs its tests on Microsoft.Testing.Platform: `global.json` declares
`"test": { "runner": "Microsoft.Testing.Platform" }`, the test projects are xunit.v3
executables, and `dotnet test --solution` drives them through that host.

## Decision

**Admit development tools as a class, under conditions.** The dependency ban in ADR-0012 is
about what reaches a consumer. A tool that is invoked from a command line, produces a report
a human reads, and leaves no trace in a shipped assembly or a published package graph is not
a dependency in that sense. Such a tool is admissible when all of the following hold:

- it is not referenced by any project, shipped or test, and does not enter the package graph
  of anything published;
- it is pinned to an exact version in a committed tool manifest, so two runs on two machines
  mean the same thing;
- no build, test or release step fails because the tool is absent;
- its output informs a human decision and never, by itself, gates a merge.

This is a narrower permission than it looks. It admits a report; it does not admit a tool
that writes source, rewrites a build, or needs the network during a build.

**Do not adopt Stryker.NET today.** It is admissible under the rule above and still not
adopted, because it cannot measure this suite honestly. Two probes, both run against
`dotnet-stryker` 5.0.0:

- Run as-is, against the platform the project actually uses, it reports
  `Number of tests found: 0` and exits 1. Stryker drives tests through VSTest, and upstream
  support for Microsoft.Testing.Platform is still open work
  ([stryker-net#3094](https://github.com/stryker-mutator/stryker-net/issues/3094), open since
  2024-11-04, four of seven sub-issues done at the time of writing).
- Bridged onto VSTest — by adding `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio`
  to the test projects — it builds, discovers 402 tests in
  `SensitiveDataGateway.Core.Tests`, runs to completion, and reports a mutation score of
  **0.00%**. Restricted to `SourceSpan.cs`, all 51 mutants survived, although
  `SourceSpanTests.cs` asserts that type's behaviour directly. The tests were discovered but
  were not deciding anything about the mutated assembly.

The second result is the reason for the decision, and it is worse than the first. A tool that
refuses to start is an inconvenience. A tool that produces a number which is not the truth is
a hazard, and a repository that publishes guarantees about failing closed is the last place
that should carry one. Adopting the bridge would also mean maintaining a second test host
whose results CI never consults, so the mutation run would exercise a path the project does
not otherwise use.

Mutation testing stays on the [roadmap](../roadmap.md) as open work, to be revisited when the
tool can run on the platform the suite already runs on. This phase therefore closes with no
change to shipped code.

## Consequences

### Positive

- The dependency rule now distinguishes what ships from what a developer runs, so the next
  tool is argued against written criteria instead of against precedent.
- No second test host, no second runner configuration, and no divergence between what CI
  verifies and what a tool measures.
- The reason for not adopting the tool is recorded with the evidence, so revisiting it means
  re-running two probes rather than re-litigating the question.

### Negative

- The assertion quality of the validator, the policy engine and the egress guard remains
  unmeasured by mutation testing. The egress suite and the fail-closed tests are the current
  substitute, and they are an argument, not a measurement.
- The gap persists until upstream closes it, on a schedule this project does not control.

### Neutral

- Nothing in the repository changes. No tool manifest is committed, because committing one
  for a tool that cannot be used would be a claim that it can.

## Alternatives considered

**Adopt the VSTest bridge and publish the score.** Rejected. The measured score is 0.00% on
code that is demonstrably asserted, so the number would be false. Publishing it in a project
that documents egress guarantees would corrode the credibility of the documents that are
accurate.

**Keep the bridge but use it only locally, undocumented.** Rejected for the same reason, with
the aggravation that the next reader would have no way to know the number is meaningless.

**Migrate the suite back to VSTest.** Rejected. Microsoft.Testing.Platform was chosen for the
test host; reversing a platform decision to accommodate a reporting tool inverts the
priority, and it would cost the suite the native-executable test model that xunit.v3 provides.

**Write a minimal mutation harness.** Rejected. It would be new, untested infrastructure
whose own correctness nothing checks, inside a repository whose point is that unchecked
correctness claims are the defect.

**Drop mutation testing from the roadmap.** Rejected. The need is real and specific: these
three components fail silently when they fail, which is exactly the failure mode mutation
testing detects. The obstacle is a tool, and tools change.
