#!/usr/bin/env bash
# Builds the sample with a given Archie package and checks it reports exactly the errors its README lists.
# Usage: samples/Shop/check.sh <version> <feed folder>. CI runs it on every host the smoke test runs on.
set -euo pipefail
version=$1
feed=$(cd "$2" && pwd)
here=$(cd "$(dirname "$0")" && pwd)
packages=$(mktemp -d)
trap 'rm -rf "$packages"' EXIT

errors() {
  # Build output repeats each error in the summary; keep "File(line,col): error ID" once.
  dotnet build "$here/Shop.slnx" --no-incremental -p:ArchieVersion="$version" -p:RestoreAdditionalProjectSources="$feed" \
    -p:RestorePackagesPath="$packages" "$@" 2>&1 \
    | grep -oE '[A-Za-z.]+/[A-Za-z]+/[A-Za-z]+\.cs\([0-9]+,[0-9]+\): (error|warning) [A-Z]+[0-9]+' | sort -u || true
}

expect() {
  local name=$1 expected=$2 actual=$3
  if [[ "$actual" != "$expected" ]]; then
    echo "::error::$name: expected"; echo "$expected"; echo "but the build reported"; echo "$actual"; exit 1
  fi
  echo "$name: OK"
}

expect "Default build" "$(printf '%s\n' \
  'Shop.Domain/Orders/Order.cs(12,22): error ARCH001' \
  'Shop.Domain/Orders/Order.cs(12,45): error ARCH001' \
  'Shop.Domain/Orders/Order.cs(15,41): error ARCH002' \
  'Shop.Domain/Orders/Order.cs(15,70): error ARCH002')" "$(errors)"

# Shop.Application is only compiled once Domain builds.
expect "Build without ARCH001/002" "$(printf '%s\n' \
  'Shop.Application/Orders/PlaceOrder.cs(14,21): error ARCH101' \
  'Shop.Application/Orders/PlaceOrder.cs(20,18): error ARCH102')" "$(errors -p:NoWarn=ARCH001%3BARCH002)"
