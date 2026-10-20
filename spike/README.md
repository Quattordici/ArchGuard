# Phase 0 spike

Throwaway code that answers one question before the rule model is built: **what does a layer-dependency
analyzer cost, and which Roslyn registration strategy is cheapest?** Not the product; nothing here ships.
Findings live outside the repo in the project's `spike/phase0-findings.md`.

## What is here

| Path | What it is |
|---|---|
| `src/Archie.Spike.Analyzers` | One hardcoded rule (`ARCH001`, "From must not depend on To", by namespace segment) with five registration modes. `netstandard2.0`, compiled against `Microsoft.CodeAnalysis.CSharp` 4.8. |
| `src/Archie.Spike.Bench` | Harness: rebuilds a project's exact compilation from the csc command line in an MSBuild log, then measures allocations, analyzer time and per-document re-analysis latency per mode. Also generates the synthetic solution. Hosts Roslyn 5.9 (the .NET 10.0.4xx compiler) so SDK generators load. |
| `build/Archie.Spike.Inject.targets` | Adds the analyzer to any project without editing it (`CustomAfterMicrosoftCommonTargets`). |
| `scripts/measure-build.sh` | Rebuilds one project per mode N times with `-p:ReportAnalyzer=true` and prints CSV. |

### Modes (`ArchieSpikeMode`)

| Mode | Registrations |
|---|---|
| `off` | Analyzer not referenced (build script only). |
| `noop` | Analyzer loads, registers nothing. Cost of being present. |
| `touch` | Same registrations as `block`, empty callbacks. Cost of IOperation trees and dispatch alone. |
| `block` | Symbol actions + `OperationBlockStart` that registers operation actions only when the owning type is in the source layer. The v1 candidate. |
| `operation` | Symbol actions + flat `RegisterOperationAction`; layer check per operation. |
| `syntax` | One `SyntaxNodeAction` on every `IdentifierName`/`GenericName`, bound with `GetSymbolInfo`. |

`ArchieSpikeFrom` / `ArchieSpikeTo` are semicolon lists of namespace segments (default `Domain` / `Infrastructure`).

## Reproduce

```bash
cd spike
dotnet build -c Release Archie.Spike.slnx

# Synthetic 114k LOC single project, 10 files with 22 known violations each
dotnet run --project src/Archie.Spike.Bench -c Release -- gen ../../synth 400

# Build matrix (run 0 is warm-up and not reported)
cd ../../synth && dotnet build -c Release            # once, so references exist
LOGDIR=/tmp/logs /path/to/spike/scripts/measure-build.sh Synth.csproj synth Domain Infrastructure 5

# Harness: point it at any detailed build log that contains the csc command line
dotnet run --project src/Archie.Spike.Bench -c Release -- run /tmp/logs/synth-off-1.log ../../synth \
  --from Domain --to Infrastructure --iterations 5 --ide-docs 8 --ide-reps 5
```

nopCommerce (`develop` at c8cf35e, .NET 10) was measured with `Libraries/Nop.Services` and
`Libraries/Nop.Core` the same way.

## Rider (run locally; cannot be measured from the cloud)

The cloud container has no IDE. These four checks need a person in Rider on a large solution:

1. **Wire it in.** Build `src/Archie.Spike.Analyzers` in Release. In the target solution's root add a
   `Directory.Build.targets` (or extend the existing one) with
   `<Import Project="/abs/path/to/spike/build/Archie.Spike.Inject.targets" />` and set
   `<ArchieSpikeMode>block</ArchieSpikeMode>` plus `ArchieSpikeFrom`/`ArchieSpikeTo` to a rule that matches
   most of the solution (worst case). Reload the solution.
2. **Diagnostics show up.** ARCH001 squiggles in the editor and in Problems > Current File; Solution-Wide
   Analysis lists them. Note whether Rider needs "Roslyn analyzers" enabled under
   Settings > Editor > Inspection Settings > Roslyn Analyzers (it is on by default).
3. **Typing latency.** Open the largest file in the source layer, type continuously for ~30 s, and watch for
   lag. Then compare against `ArchieSpikeMode=noop`. For numbers: Help > Diagnostic Tools > Profile (or
   `dotnet-trace` on the Rider backend process) while typing, and look for `Archie.Spike.Analyzers` frames.
   Pass: no perceptible difference from `noop`; the harness predicts single-digit ms per edited file.
4. **Analyzer switch-off.** Toggle "Run Roslyn analyzers" off/on in Rider settings and confirm the rule
   behaves (this is the escape hatch users will reach for if it is slow).

Record results in the findings file under "Rider".
