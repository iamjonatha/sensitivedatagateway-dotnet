# Native AOT and trimming

Every shipping package in this repository is `IsTrimmable`, `IsAotCompatible`, and built with
the trim and AOT analyzers on and warnings as errors. This guide says what that buys you,
which packages the claim covers, and where it stops.

---

## Why the library cares

Native AOT is not only a startup-time feature here. A trimmed, ahead-of-time compiled binary
has no runtime code generation and a package graph that was proved closed at build time. For
a library whose job is to be the last thing that sees a document in clear, "nothing can load
a type that was not there when we shipped" is a security property as much as a performance
one.

The dependency policy in [ADR-0012](../adr/0012-dependency-policy.md) and this one support
each other: the packages are AOT-clean largely because there are so few of them.

---

## What is covered

| Package | AOT | Note |
| --- | --- | --- |
| `SensitiveDataGateway.Core` | Yes | No dependency at all. |
| `SensitiveDataGateway` | Yes | The pipeline, including the deterministic detectors. |
| `SensitiveDataGateway.Extensions` | Yes | Configuration binding is hand-written, not reflective. See below. |
| `SensitiveDataGateway.Testing` | Yes | |
| `SensitiveDataGateway.Providers.DecisionKit` | Inherits | AOT-clean as far as this package goes; DecisionKit's own surface is DecisionKit's claim. |
| `SensitiveDataGateway.Documents.Mime` | Inherits | MimeKitLite is trim-friendly; verify against your own trim settings. |
| `SensitiveDataGateway.Documents.Pdf` | Inherits | PdfPig is not annotated for trimming. Expect warnings. |
| `SensitiveDataGateway.Documents.OpenXml` | Inherits | The Open XML SDK uses reflection over its generated model. Expect warnings. |
| `SensitiveDataGateway.Detection.Presidio` | Inherits | Uses `System.Text.Json` with a source-generated context. |

"Inherits" means this repository's code in that package is clean and the parser it adapts is
the thing to check. That is the practical consequence of
[ADR-0018](../adr/0018-format-adapters-may-take-a-parser.md): one format, one package, one
parser, so a trim warning names exactly which format you are paying for.

---

## The smoke test

[`samples/Aot`](../../samples/Aot) exists for one reason: it publishes native, with
`TrimMode=full` and `TrimmerSingleWarn=false`, and the build fails on any trim or AOT
warning. An unannotated reflection call added to a shipping package breaks that sample before
it reaches a release.

```bash
dotnet publish samples/Aot -c Release -r linux-x64
```

It is a sample, not a test project, so it is not in the test run. It is in the solution, so
it is in the build.

---

## Configuration binding

`SensitiveDataGateway.Extensions` binds a policy from `IConfiguration` without
`Microsoft.Extensions.Configuration.Binder` and without reflection. The binding is written by
hand in `PolicyConfiguration`, reading named sections and typed values.

This is more code than `configuration.Get<T>()`, and it is the reason the extensions package
is AOT-clean. It also means a misspelled key is a configuration failure with a message rather
than a silently defaulted value — which, for a policy, is the difference between a block and
a leak.

---

## What AOT does not change

- **No guarantee moves.** Trimming removes code that is not reachable; it does not remove a
  check. If a trimmed build behaved differently from a JIT build at the boundary, that would
  be a defect, and the security suite runs on both target frameworks to notice it.
- **Reflection over your own types.** The gateway never reflects over the request or result
  type of `IDecisionProvider<TRequest, TResult>`. Serializing them is the provider's
  business; if it uses `System.Text.Json`, you supply a source-generated context.
- **Analyzer coverage is not proof.** `EnableTrimAnalyzer` catches what it can see. A
  dependency that builds a type name from a string defeats it, which is one more reason the
  dependency list is short.

---

## If you hit a warning

1. Find out which assembly it came from. `TrimmerSingleWarn=false` is on in the sample for
   exactly this.
2. If it is one of this repository's assemblies, it is a bug — open an issue.
3. If it is a document parser, you have three options: keep the format and accept the
   warning, drop the package for that format, or extract the text yourself and feed the
   gateway plain text.
4. Do not silence it with `TrimmerRootAssembly` on a parser. Rooting an assembly to make a
   warning go away keeps every reflective path in it, which is the opposite of what the
   build was telling you.

---

## See also

- [`getting-started.md`](getting-started.md) — the smallest working gateway.
- [`dependency-injection.md`](dependency-injection.md) — composition, which is where an AOT
  application does its wiring.
- [`../security/dependency-classification.md`](../security/dependency-classification.md) —
  what each package is allowed to do.
