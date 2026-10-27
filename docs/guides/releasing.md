# Releasing

How a version of SensitiveDataGateway becomes packages on nuget.org.

A release here is a promise about a public API and about a set of security guarantees. The
mechanics below exist so that neither is made by accident.

---

## What a version number means

[Semantic Versioning 2.0.0](https://semver.org/), applied to the public API **and** to the
guarantees in [`../security/egress-guarantees.md`](../security/egress-guarantees.md).

| Change | Bump |
| --- | --- |
| A new type, member or overload | Minor |
| A new detector, transformation or adapter | Minor |
| A behavioural change that lets something through that used to be blocked | **Major** |
| A behavioural change that blocks something that used to pass | Minor, with a changelog note |
| A removed or renamed public member | Major |
| A tightened default | Minor, with a changelog note |
| A fix with no API change | Patch |

The third row is the one that matters. Weakening a guarantee is a breaking change even when
the API is identical, because the thing a consumer depended on was the guarantee.

Before `1.0.0` the public API may still move, and a minor bump may break a consumer. After
`1.0.0` it may not.

All packages share one version. They are released together, and
`Directory.Build.props` → `VersionPrefix` is the single place it is written.

---

## Before you tag

Run the checks in order. Each one has caught a release.

```bash
dotnet format SensitiveDataGateway.slnx --verify-no-changes
dotnet build SensitiveDataGateway.slnx -c Release
dotnet test --solution SensitiveDataGateway.slnx -c Release
```

Then:

1. **Shipped public API.** Every entry in each `PublicAPI.Unshipped.txt` moves to the
   matching `PublicAPI.Shipped.txt`, and the unshipped file is left with nothing but
   `#nullable enable`. The analyzer will not do this for you, and a release that ships with
   entries still unshipped has no baseline to compare the next one against.

   ```bash
   # From the repository root, for each project under src/
   cat src/<Project>/PublicAPI.Unshipped.txt >> src/<Project>/PublicAPI.Shipped.txt
   printf '#nullable enable\n' > src/<Project>/PublicAPI.Unshipped.txt
   ```

   Sort the shipped file afterwards and drop the duplicate `#nullable enable` line.

2. **Changelog.** `## [Unreleased]` becomes `## [x.y.z] - YYYY-MM-DD`, and a fresh empty
   `## [Unreleased]` goes above it. Every user-visible change has an entry; a release note
   generated from commit subjects is not a changelog.

3. **Version.** `VersionPrefix` in `Directory.Build.props` matches the tag you are about to
   push.

4. **Documentation.** Every example in `docs/guides/` compiles against the code you are about
   to publish. The full set is reviewed against the code at each minor release, not only at
   `1.0.0`.

5. **Security suite.** `tests/SensitiveDataGateway.Security.Tests` passes with nothing
   skipped. It is part of the contract, not part of the coverage.

---

## Tagging

```bash
git tag -a v0.2.0 -m "0.2.0"
git push origin v0.2.0
```

The tag triggers [`.github/workflows/release.yml`](../../.github/workflows/release.yml),
which builds, tests, packs, authenticates to NuGet with a short-lived token and pushes every
package and symbol package, then opens a GitHub release.

`workflow_dispatch` runs the same job with a version typed by hand. Use it only to recover a
failed publish, never to publish something that has no tag.

---

## After the release

Two things, immediately, in one commit:

1. Set `SensitiveDataGatewayBaselineVersion` in `Directory.Build.props` to the version you
   just published. This turns on package validation against the previous release: from the
   next build onwards, removing or changing a public member fails the build rather than a
   consumer's.

2. Bump `VersionPrefix` to the next patch version, so that a build from `main` is never
   confusable with a published package.

---

## Yanking

A package that leaked something is unlisted, not deleted. Deleting it breaks restore for
everyone who already depends on it, and does not remove it from their caches.

1. Unlist the affected versions on nuget.org.
2. Publish the fix as a new patch version.
3. Open a security advisory on the repository, following
   [`../../SECURITY.md`](../../SECURITY.md).
4. Add a changelog entry naming the versions affected and what a consumer should do.

A leak is an unlist-and-advise event even when nobody is known to have been affected. The
whole premise of this library is that the caller does not have to find out for themselves.

---

## See also

- [`../../CONTRIBUTING.md`](../../CONTRIBUTING.md) — what a change has to satisfy before it
  reaches a release at all.
- [`../security/egress-guarantees.md`](../security/egress-guarantees.md) — the promises a
  major version is measured against.
- [`../adr/README.md`](../adr/README.md) — where a decision that changes one of them is
  recorded.
