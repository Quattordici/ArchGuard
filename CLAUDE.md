# CLAUDE.md

Archie enforces architecture rules (layers, forbidden dependencies) in .NET as Roslyn analyzers. The v1 design lives outside the repository for now; the code follows it.

## Commands

```bash
dotnet build -c Release                                  # warnings are errors (Directory.Build.props)
dotnet test -c Release                                   # TUnit + Shouldly + Microsoft.CodeAnalysis.Testing, on the newest Roslyn
dotnet test -c Release -p:TestAgainstFloors=true         # the same tests on Roslyn 4.8
dotnet pack src/Archie.Analyzers -c Release -o artifacts/packages -p:Version=0.0.0-smoke
tests/HostSmoke/run.sh 0.0.0-smoke artifacts/packages    # the package in a plain project, built by the active SDK's compiler
samples/Shop/check.sh 0.0.0-smoke artifacts/packages     # the sample solution reports exactly the errors its README lists
dotnet run -c Release --project benchmarks/Archie.Benchmarks -- --budget   # allocations and per-document time on 114k synthetic lines
benchmarks/build-budget.sh 0.0.0-smoke artifacts/packages            # Archie's share of csc time in a real build (budget 5%)
```

`.github/workflows/perf.yml` runs both benchmarks weekly (never on pull requests, timing on shared runners is too noisy to gate). To run the host smoke test on another SDK, put a `global.json` in `tests/HostSmoke/` (ignored by git; CI writes one per host).

## Constraints

- **Roslyn floor.** `src/` targets `netstandard2.0` and compiles against `Microsoft.CodeAnalysis.CSharp` 4.8 (`RoslynFloor` in `Directory.Packages.props`), the oldest compiler that must load the analyzer (.NET 8.0.1xx SDK, VS 17.8). Don't raise it for convenience: it drops every older SDK and IDE. Tests run on `RoslynLatest` by default and on the floor with `-p:TestAgainstFloors=true`; both must pass, because hosts hand analyzers different operation trees (Roslyn 5.x exposes attributes as operation blocks, 4.8 doesn't).
- **No runtime dependencies in the analyzer.** Whatever it references has to be loadable in every host (VS on .NET Framework, Rider, `dotnet build`), which is why config is read by an own JSONC reader rather than System.Text.Json.
- **Diagnostics.** IDs are `ARCH001`… (rules, default error: `Diagnostics.RuleSeverity`), `ARCH1xx` reserved for conventions, `ARCH9xx` configuration. Register every new one in `AnalyzerReleases.Unshipped.md`, the README table and `docs/diagnostics/` (`DiagnosticCatalogTests` checks the last two against the descriptors). A diagnostic without a source location (like ARCH909) can only have its severity changed in a global analyzer config, not in a `[*.cs]` section of `.editorconfig`.
- **Rule logic lives in Core.** `Archie.Core` (netstandard2.0, no references) has the config reader (`ConfigParser`, own JSONC reader) and the rules (`ArchEvaluator.Check(DependencyEdge)`). The analyzer only turns symbols and operations into edges and reports; the Phase 4 metadata backend will reuse Core unchanged. Archie checks this about itself: `src/archie.json` is its own architecture and `OwnArchitectureTests` compiles `src/` against netstandard2.0 and the Roslyn floor and expects no violations; update the file when a namespace is added. The violation key is `(ID, sourceId, targetId)` with doc-comment IDs, in `Diagnostic.Properties`.
- **Rule tests are data.** `tests/Corpus/Dependencies` and `tests/Corpus/Conventions` (see the README) hold one case per kind of dependency or convention with the expected violations as comments; `CorpusTests` runs them. Add a case there for a new kind of dependency or requirement rather than an inline test.
- **Conventions** (ARCH101–105, one ID per kind of requirement) are decided in `Archie.Core/Conventions` (`ConventionEvaluator`, which also builds the message) against an `IConventionSubject`; the analyzer only wraps a symbol (`ConventionRules.TypeSubject`). `inherit` covers base classes and interfaces (no `implement` key); type names may omit the namespace. Config error messages are pinned in `ConfigParserTests`; `DocumentationTests` keeps `schema/v1.json` and every config example in the README and `docs/diagnostics/` in step with the parser.
- **Package layout.** One package, `Archie`: `analyzers/dotnet/cs/Archie.Analyzers.dll`, `analyzers/dotnet/cs/Archie.Core.dll` and `build/Archie.targets` (finds the nearest `archie.json` with `GetPathOfFileAbove` and makes `IsTestProject`/`ArchieEnabled` visible to the analyzer). Nothing in `lib/`; the pack job checks this.
