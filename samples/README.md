# Samples

Two runnable projects. They need nothing but a .NET SDK — the AOT one also needs a C++
toolchain — and both are part of `SensitiveDataGateway.slnx`, so a broken sample is a broken
build.

| Sample | What it demonstrates |
| --- | --- |
| [`Aot`](Aot) | Native AOT publish with zero trim warnings: configuration binding, container composition, sanitization and the anti-leak assertion |
| [`EmailClassification`](EmailClassification) | The reference scenario end to end: a `.eml` with PDF, Word and Excel attachments, read, sanitized and handed to a provider |

## Email classification

```bash
dotnet run --project samples/EmailClassification
```

An email is a bundle of parts, and an attachment is one of them. Nothing in the sample treats
an attachment as a special case, because the library does not: `MimeBundleReader` flattens the
message, each part is offered to the registered extractors by media type, and a part nothing
reads blocks the operation rather than passing as empty.

The arrangement of the synthetic message is the point. The body carries one address; the bank
details are in the PDF, a home address is in the Word document, and a second customer is in the
workbook. Four of the six detected values appear nowhere but inside an attachment, so a gateway
that read only what a mail client displays would fail the egress assertion.

It also shows the two things the library does *not* do. A fourth attachment in a format no
extractor claims refuses the whole message, because an unscanned part is content nobody
accounted for. And the postal address in the Word document is transmitted, because no
deterministic detector can find an address written in prose — the sample asserts that the gap
is still open, so the day semantic detection ships, this sample fails and has to be told.

The sample exits non-zero when an assertion fails, so it doubles as a smoke test.

## Native AOT

```bash
dotnet run --project samples/Aot
```

The point, though, is the publish:

```bash
dotnet publish samples/Aot -c Release -r win-x64
./artifacts/publish/SensitiveDataGateway.Samples.Aot/release_win-x64/SensitiveDataGateway.Samples.Aot
```

Warnings are errors here too, so any reflection introduced in a shipping package fails this
publish before it reaches a user. On Linux use `-r linux-x64`. On Windows the native link step
needs the Visual Studio C++ toolchain, and `vswhere.exe` must be on `PATH`:

```powershell
$env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\Installer;$env:PATH"
dotnet publish samples/Aot -c Release -r win-x64
```

The sample exits non-zero when the anti-leak assertion fails, so it doubles as a smoke test.

## Conventions

Samples target `net10.0` only, have `ImplicitUsings` enabled and are never packed. They are
written to be read: each one prints what it is doing, and the interesting part is the source,
not the output.

Every value a sample sanitizes is synthetic. They are structurally valid, so the detectors
recognise them, and they belong to no one.
