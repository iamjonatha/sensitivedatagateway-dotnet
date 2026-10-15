# Samples

One runnable project for now. It needs nothing but a .NET SDK and a C++ toolchain, and it is
part of `SensitiveDataGateway.slnx`, so a broken sample is a broken build.

| Sample | What it demonstrates |
| --- | --- |
| [`Aot`](Aot) | Native AOT publish with zero trim warnings: configuration binding, container composition, sanitization and the anti-leak assertion |

## Running it

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
