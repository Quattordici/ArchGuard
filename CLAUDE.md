# CLAUDE.md

Archie enforces architecture rules (layers, forbidden dependencies) in .NET as Roslyn analyzers. The v1 design lives outside the repository for now; the code follows it.

## Commands

```bash
dotnet build -c Release                                  # warnings are errors (Directory.Build.props)
dotnet test -c Release                                   # TUnit + Shouldly + Microsoft.CodeAnalysis.Testing, on the newest Roslyn
dotnet test -c Release -p:TestAgainstFloors=true         # the same tests on Roslyn 4.8
dotnet pack src/Archie.Analyzers -c Release -o artifacts/packages -p:Version=0.0.0-smoke
tests/HostSmoke/run.sh 0.0.0-smoke artifacts/packages    # the package in a plain project, built by the active SDK's compiler
```

To run the host smoke test on another SDK, put a `global.json` in `tests/HostSmoke/` (ignored by git; CI writes one per host).

## Constraints

- **Roslyn floor.** `src/` targets `netstandard2.0` and compiles against `Microsoft.CodeAnalysis.CSharp` 4.8 (`RoslynFloor` in `Directory.Packages.props`), the oldest compiler that must load the analyzer (.NET 8.0.1xx SDK, VS 17.8). Don't raise it for convenience: it drops every older SDK and IDE. Tests run on `RoslynLatest` by default and on the floor with `-p:TestAgainstFloors=true`; both must pass, because hosts hand analyzers different operation trees (Roslyn 5.x exposes attributes as operation blocks, 4.8 doesn't).
- **No runtime dependencies in the analyzer.** Whatever it references has to be loadable in every host (VS on .NET Framework, Rider, `dotnet build`), which is why config is read by an own JSONC reader rather than System.Text.Json.
- **Diagnostics.** IDs are `ARCH001`… (rules), `ARCH1xx` reserved for conventions, `ARCH9xx` configuration. Register every new one in `AnalyzerReleases.Unshipped.md`. A diagnostic without a source location (like ARCH909) can only have its severity changed in a global analyzer config, not in a `[*.cs]` section of `.editorconfig`.
- **Package layout.** One package, `Archie`: `analyzers/dotnet/cs/Archie.Analyzers.dll` and `build/Archie.targets` (finds the nearest `archie.json` with `GetPathOfFileAbove` and makes `IsTestProject`/`ArchieEnabled` visible to the analyzer). Nothing in `lib/`; the pack job checks this.
