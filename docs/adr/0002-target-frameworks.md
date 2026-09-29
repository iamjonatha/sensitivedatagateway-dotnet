# 0002. Target net8.0 and net10.0

- **Status:** Accepted
- **Date:** 2026-09-29
- **Deciders:** @iamjonatha

## Context

The library must be usable by applications that are in long-term support and by applications
on the current release. `.NET Standard 2.0` would widen reach to .NET Framework, at the cost
of losing nullable reference types as a build gate, source-generated regular expressions,
`TimeProvider`, spans in the shapes the hot paths want, and trimming and AOT analysis.

Sanitization runs over every document an application processes, so the hot paths matter. The
correctness gates matter more: a security boundary built without nullable analysis and
without warnings as errors is a worse boundary.

## Decision

Shipping packages multi-target `net8.0` and `net10.0`, with an identical public surface on
both. The code compiles cleanly on both, and CI builds and tests both on Linux and Windows.

`Microsoft.Extensions.*` dependencies are pinned per target framework: the `net8.0` assets
take the 8.0 line, the `net10.0` assets the 10.0 line, so a .NET 8 application is never
dragged onto the .NET 10 extension stack by one of our references.

Packages are designed for trimming and Native AOT: `IsTrimmable`, `IsAotCompatible`, the trim
and AOT analyzers on, reflection kept out of configuration binding, and a Native AOT sample
published in CI with `TrimMode=full` so that any reflection introduced in a shipping package
fails the build.

No shipping package uses a platform-specific API. Windows, Linux and macOS are supported
equally.

## Consequences

### Positive

- Nullable reference types, warnings as errors and modern analyzers are available as build
  gates.
- Source-generated regular expressions and source-generated logging, both of which this
  project needs, are available.
- Trimming and AOT are enforced rather than hoped for.
- A .NET 8 consumer keeps its own extension stack.

### Negative

- .NET Framework applications cannot use the library. That is a real population, and it is
  excluded deliberately.
- Every behavioural change is tested twice.
- Two dependency version lines to keep current.

### Neutral

- The target list will move as support windows close. Dropping a target framework is a
  breaking change and needs a new record.

## Alternatives considered

### `netstandard2.0`

Rejected. It costs the correctness gates and the performance primitives the project depends
on, in exchange for reaching a platform that is not the project's audience.

### `net10.0` only

Rejected. It excludes applications in long-term support, which is most of the audience for a
library that is adopted for compliance and risk reasons.
