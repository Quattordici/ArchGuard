#!/usr/bin/env bash
# Builds projects against a packed Archie package with whichever SDK is active and checks that the analyzer loads and runs:
# NoConfig must report ARCH909; WithConfig (archie.json next to it) must not, and must report the one ARCH001 and the one ARCH105 in Order.cs,
# and without the smoke test's severity downgrade its build must fail on that ARCH001 (the default is error, plan §14.1). CustomConfig chooses
# a config that is not named archie.json with <ArchieConfig>. None may fail to load the analyzer or Archie.Core.dll next to it.
# Usage: tests/HostSmoke/run.sh <package version> <folder with Archie.<version>.nupkg>
set -euo pipefail
version=$1
feed=$(cd "$2" && pwd)
here=$(cd "$(dirname "$0")" && pwd)
packages=$(mktemp -d) # a fresh cache, so a repacked version with the same number is never served stale
cd "$here" # the SDK is chosen by the global.json nearest the working directory

echo "SDK $(dotnet --version)"
build() { # project directory, extra build arguments
  local project=$1
  shift
  dotnet build "$project" -c Release -p:ArchieVersion="$version" -p:RestoreAdditionalProjectSources="$feed" -p:RestorePackagesPath="$packages" \
    -p:RestoreNoHttpCache=true "$@" 2>&1 | tee "$packages/$project.log"
}

build NoConfig
grep -q "warning ARCH909" "$packages/NoConfig.log" || { echo "::error::NoConfig: ARCH909 was not reported, so the analyzer did not run."; exit 1; }

build WithConfig
if grep -q "ARCH909" "$packages/WithConfig.log"; then echo "::error::WithConfig: ARCH909 was reported, so build/Archie.targets did not add archie.json."; exit 1; fi
grep -q "Order.cs(12,31): warning ARCH001: Domain must not depend on Infrastructure" "$packages/WithConfig.log" \
  || { echo "::error::WithConfig: ARCH001 was not reported at Order.cs(12,31), so the rules did not run."; exit 1; }
grep -q "Order.cs(3,25): warning ARCH105: 'Db' must not be public" "$packages/WithConfig.log" \
  || { echo "::error::WithConfig: ARCH105 was not reported at Order.cs(3,25), so the conventions did not run."; exit 1; }
if grep -qE "ARCH9[0-9][0-9]" "$packages/WithConfig.log"; then echo "::error::WithConfig: archie.json was reported as invalid."; exit 1; fi

# Rebuilt (the compiler would otherwise be skipped as up to date) with the default severities: the violation must fail the build.
if build WithConfig --no-incremental -p:ArchieSmokeDefaults=true; then
  echo "::error::WithConfig: the build succeeded with default severities, so a violation would not fail CI."; exit 1
fi
grep -q "Order.cs(12,31): error ARCH001" "$packages/WithConfig.log" \
  || { echo "::error::WithConfig: the build failed, but not with error ARCH001 at Order.cs(12,31)."; exit 1; }

build CustomConfig
if grep -q "ARCH909" "$packages/CustomConfig.log"; then echo "::error::CustomConfig: ARCH909 was reported, so the file <ArchieConfig> names was not used."; exit 1; fi
grep -q "Order.cs(12,31): warning ARCH001" "$packages/CustomConfig.log" \
  || { echo "::error::CustomConfig: ARCH001 was not reported, so the rules in rules/architecture.jsonc did not run."; exit 1; }

echo "Host smoke test passed."
