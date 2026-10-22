#!/usr/bin/env bash
# Builds the synthetic 114k-line project with a packed Archie package, the way Phase 0 measured (design §9): Archie's time in
# -p:ReportAnalyzer=true as a share of csc's wall time, median of the runs after a warm-up. Two configs check the same code: a clean one with
# nothing to report (what a codebase that follows its rules pays on every build; the 5% budget) and one with 222 violations (reported, not
# budgeted: each file with a diagnostic also pays Roslyn's generated-code check). Prints Markdown; exit 1 when the clean config is over 5%.
# Usage: benchmarks/build-budget.sh <package version> <folder with Archie.<version>.nupkg> [runs=5]
set -euo pipefail
version=$1
feed=$(cd "$2" && pwd)
runs=${3:-5}
here=$(cd "$(dirname "$0")" && pwd)
work=$(mktemp -d)
packages="$work/packages"

median() { printf '%s\n' "$@" | sort -n | awk '{ v[NR] = $1 } END { print v[int((NR + 1) / 2)] }'; }

# measure <name> [gen options]: sets share, archie, csc and diagnostics.
measure() {
  local name=$1; shift
  local project="$work/$name"
  # A compiler server that has loaded other analyzer versions or projects measured up to twice as slow; run 0 warms the new one.
  dotnet build-server shutdown > /dev/null 2>&1 || true
  dotnet run -c Release --project "$here/Archie.Benchmarks" -- gen "$project" "$@" > /dev/null
  dotnet restore "$project" -p:ArchieVersion="$version" -p:RestoreAdditionalProjectSources="$feed" -p:RestorePackagesPath="$packages" \
    -p:RestoreNoHttpCache=true > /dev/null

  local csc_ms=() archie_ms=()
  diagnostics=0
  for run in $(seq 0 "$runs"); do # run 0 warms the compiler server and is not counted
    local log="$work/$name-$run.log"
    dotnet build "$project" --no-restore -c Release -t:Rebuild -p:ArchieVersion="$version" -p:RestorePackagesPath="$packages" \
      -p:ReportAnalyzer=true "-flp:v=d;PerformanceSummary" -flp:logfile="$log" -v:q -nologo > /dev/null
    [[ $run == 0 ]] && continue
    local c a
    c=$(grep -E '^\s+[0-9]+ ms\s+Csc\s+[0-9]+ calls' "$log" | awk '{print $1}' | head -1)
    a=$(grep -E '^\s+[0-9.<]+\s+[0-9]+\s+Archie.Analyzers, Version' "$log" | awk '{print $1}' | head -1)
    [[ -n $c && -n $a ]] || { echo "::error::No csc or Archie time in $log; did the analyzer run?"; exit 2; }
    csc_ms+=("$c")
    archie_ms+=("$(awk -v s="${a#<}" 'BEGIN { printf "%.0f", s * 1000 }')")
    diagnostics=$( (grep -oE 'warning ARCH[0-9]+: .*' "$log" || true) | sort -u | wc -l)
  done

  csc=$(median "${csc_ms[@]}")
  archie=$(median "${archie_ms[@]}")
  share=$(awk -v a="$archie" -v c="$csc" 'BEGIN { printf "%.1f", 100 * a / c }')
}

measure clean --clean
clean_share=$share; clean_row="| Clean (nothing to report) | $share % | $archie / $csc ms | $diagnostics | 5 % |"
[[ $diagnostics -eq 0 ]] || { echo "::error::The clean config reported $diagnostics diagnostics."; exit 2; }
measure violations
violations_row="| With violations | $share % | $archie / $csc ms | $diagnostics | |"
[[ $diagnostics -gt 0 ]] || { echo "::error::Archie reported nothing, so it did not run."; exit 2; }

echo "## Real build (SDK $(dotnet --version), median of $runs)"
echo
echo "Every layer checked, plus four conventions."
echo
echo "| Config | Archie time in ReportAnalyzer / csc wall time | Archie / csc | Diagnostics | Budget |"
echo "|---|---:|---:|---:|---:|"
echo "$clean_row"
echo "$violations_row"
echo
echo "Archie's time is summed over threads and csc's is wall time, so the share is conservative on several cores (Phase 0 findings)."
awk -v s="$clean_share" 'BEGIN { exit !(s > 5) }' && { echo; echo "**Over budget.**"; exit 1; }
exit 0
