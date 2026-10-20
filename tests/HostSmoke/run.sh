#!/usr/bin/env bash
# Builds two projects against a packed Archie package with whichever SDK is active and checks that the analyzer loads and runs:
# NoConfig must report ARCH909, WithConfig (archie.json next to it) must not, and neither may fail to load the analyzer.
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

echo "Host smoke test passed."
