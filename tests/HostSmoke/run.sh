#!/usr/bin/env bash
# Builds two projects against a packed Archie package with whichever SDK is active and checks that the analyzer loads and runs:
# NoConfig must report ARCH909; WithConfig (archie.json next to it) must not, and must report the one ARCH001 and the one ARCH105 in Order.cs. Neither may fail
# to load the analyzer or Archie.Core.dll next to it.
# Usage: tests/HostSmoke/run.sh <package version> <folder with Archie.<version>.nupkg>
set -euo pipefail
version=$1
feed=$(cd "$2" && pwd)
here=$(cd "$(dirname "$0")" && pwd)
packages=$(mktemp -d) # a fresh cache, so a repacked version with the same number is never served stale
cd "$here" # the SDK is chosen by the global.json nearest the working directory

echo "SDK $(dotnet --version)"
build() { # project directory
  dotnet build "$1" -c Release -p:ArchieVersion="$version" -p:RestoreAdditionalProjectSources="$feed" -p:RestorePackagesPath="$packages" \
    -p:RestoreNoHttpCache=true 2>&1 | tee "$packages/$1.log"
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

echo "Host smoke test passed."
