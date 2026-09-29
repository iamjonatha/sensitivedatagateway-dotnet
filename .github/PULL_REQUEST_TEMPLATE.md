## Summary

<!-- What does this change and why. One paragraph. -->

## Related issues

<!-- Closes #123 / Refs #123 -->

## Type of change

- [ ] `feat` — new capability
- [ ] `fix` — bug fix
- [ ] `perf` — performance
- [ ] `refactor` — no behavioural change
- [ ] `docs` — documentation only
- [ ] `test` — tests only
- [ ] `build` / `ci` / `chore`

## Boundary impact

<!-- Answer all four. "No" is the expected answer to all four. -->

- Does this let any type other than `ValidatedPayload` reach a provider? **No / explain**
- Does this add network capability outside `SensitiveDataGateway.Providers.*`? **No / explain**
- Does this make the pseudonym map reachable from anywhere new? **No / explain**
- Does this weaken, relax or make configurable a guarantee in
  [`docs/security/egress-guarantees.md`](../docs/security/egress-guarantees.md)?
  **No / link the ADR**

## Checklist

- [ ] Everything is written in English, including commit messages
- [ ] No `Co-authored-by` or tool attribution trailers in any commit
- [ ] Commits follow Conventional Commits
- [ ] `dotnet format --verify-no-changes` passes
- [ ] `dotnet build -c Release` is warning-free on `net8.0` and `net10.0`
- [ ] `dotnet test` passes, including the egress security suite
- [ ] `SensitiveDataGateway.Core` gained no package reference and no I/O
- [ ] No document content can reach a log, a metric, a trace or an exception
- [ ] No fixture I added contains a real name, address, IBAN, tax code, key or document
- [ ] Cancellation is honoured end to end
- [ ] A new failure path blocks rather than continuing
- [ ] Public API additions are intentional, documented and in `PublicAPI.Unshipped.txt`
- [ ] `CHANGELOG.md` updated under `## [Unreleased]`

## Breaking changes

<!-- Describe the break and the migration path, or write "None". -->
