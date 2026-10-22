# Contributing to Archie

Contributions are welcome through pull requests from forks.

## How to contribute

1. **Open an issue first** for anything beyond a small fix, so we can agree on the approach before you spend time on it.
2. **Fork** the repository and create a branch from `main`.
3. Make your change, with tests where it makes sense.
4. Open a **pull request** against `main`.

## Building and testing

```bash
dotnet build -c Release
dotnet test -c Release --no-build                  # tests on the newest Roslyn
dotnet test -c Release -p:TestAgainstFloors=true   # the same tests on Roslyn 4.8, the oldest compiler Archie supports
```

The analyzer targets `netstandard2.0` and compiles against Roslyn 4.8, so it loads in the .NET 8 SDK and Visual Studio 17.8 and later.
CI also packs the package and builds a plain project with it on the .NET 8.0.1xx SDK (Roslyn 4.8) and the current SDK
(`tests/HostSmoke/run.sh`), which fails if the analyzer can't be loaded or doesn't run, and builds the sample solution
(`samples/Shop/check.sh`), which must report exactly the errors its README lists.

## Performance

The analyzer runs on every keystroke in the IDE and in every build, so it has a budget (from the Phase 0 spike): at most 5% of csc's
time and 50 MB of allocations per 100k lines. The weekly `Performance` workflow measures both on a synthetic 114k-line project; run them
locally before and after a change to a hot path. The time budget applies to a config with nothing to report; the same code with 222
violations is reported next to it. Timing swings by up to 2x between invocations on a busy machine, so compare interleaved runs.

Keep syntax out of the common path: look a symbol's syntax or a location up only once there is a violation. Asking a lambda parameter for
its `DeclaringSyntaxReferences` and building every member access's location up front was half of Archie's time.

```bash
dotnet run -c Release --project benchmarks/Archie.Benchmarks             # allocations and per-document time, in process
dotnet pack src/Archie.Analyzers -c Release -o artifacts/packages -p:Version=0.0.0-smoke
benchmarks/build-budget.sh 0.0.0-smoke artifacts/packages                # Archie's share of csc time in a real build
```

## Pull request expectations

- Keep pull requests focused; unrelated changes belong in separate PRs.
- All CI checks must pass.
- Pull requests are **squash-merged**, so the PR title becomes the commit message. Please make it clear and descriptive.

## About CI on pull requests

CI workflows on pull requests from forks do not start automatically. A maintainer approves the run first, and changes to
workflow files, project files (`*.csproj`, `Directory.Build.*`, `*.targets`) and `tests/HostSmoke` are reviewed with extra care
since they execute during the build.

## Releasing (maintainers)

A tag `v<version>` on a commit on `main` packs the `Archie` package with that version and, after all CI jobs pass, pushes it to
nuget.org through [trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) (no stored API key):

```bash
git tag v0.0.1-alpha.1 && git push origin v0.0.1-alpha.1
```

- Use SemVer 2 pre-release labels with a number (`-alpha.1`, `-alpha.2`) so versions sort correctly; anything without a label is a stable release.
- A pushed version can be unlisted but never deleted, replaced or reused. Metadata (description, readme, icon) belongs to the version and can
  only be changed by pushing a new one. The pack job checks the licence, readme and repository metadata before anything is pushed.
- The push job runs in the GitHub environment `nuget` (give it a required reviewer and limit it to `v*` tags) and needs the secret
  `NUGET_USER` (the nuget.org profile name) and a trusted publishing policy on nuget.org for owner `Quattordici`, repository `Archie`,
  workflow `ci.yml`, environment `nuget`.

## Reporting security issues

Please do not use public issues for security problems. See [SECURITY.md](SECURITY.md).
