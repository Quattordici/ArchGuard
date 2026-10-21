#!/usr/bin/env bash
# Builds the synthetic 114k-line project with a packed Archie package, the way Phase 0 measured (design §9): Archie's time in
# -p:ReportAnalyzer=true as a share of csc's wall time, median of the runs after a warm-up. Prints Markdown; exit 1 over the 5% budget.
# Usage: benchmarks/build-budget.sh <package version> <folder with Archie.<version>.nupkg> [runs=5]
set -euo pipefail
version=$1
feed=$(cd "$2" && pwd)
runs=${3:-5}
here=$(cd "$(dirname "$0")" && pwd)
work=$(mktemp -d)
packages="$work/packages"

dotnet run -c Release --project "$here/Archie.Benchmarks" -- gen "$work/Synth" > /dev/null
cd "$work/Synth"
dotnet restore -p:ArchieVersion="$version" -p:RestoreAdditionalProjectSources="$feed" -p:RestorePackagesPath="$packages" \
  -p:RestoreNoHttpCache=true > /dev/null

csc_ms=(); archie_ms=(); diagnostics=0
for run in $(seq 0 "$runs"); do # run 0 warms the compiler server and is not counted
  log="$work/build-$run.log"
  dotnet build --no-restore -c Release -t:Rebuild -p:ArchieVersion="$version" -p:RestorePackagesPath="$packages" -p:ReportAnalyzer=true \
    "-flp:v=d;PerformanceSummary" -flp:logfile="$log" -v:q -nologo > /dev/null
  [[ $run == 0 ]] && continue
  csc=$(grep -E '^\s+[0-9]+ ms\s+Csc\s+[0-9]+ calls' "$log" | awk '{print $1}' | head -1)
  archie=$(grep -E '^\s+[0-9.<]+\s+[0-9]+\s+Archie.Analyzers, Version' "$log" | awk '{print $1}' | head -1)
  [[ -n $csc && -n $archie ]] || { echo "::error::No csc or Archie time in $log; did the analyzer run?"; exit 2; }
  csc_ms+=("$csc")
  archie_ms+=("$(awk -v s="${archie#<}" 'BEGIN { printf "%.0f", s * 1000 }')")
  diagnostics=$(grep -oE 'warning ARCH[0-9]+: .*' "$log" | sort -u | wc -l)
done

median() { printf '%s\n' "$@" | sort -n | awk '{ v[NR] = $1 } END { print v[int((NR + 1) / 2)] }'; }
csc=$(median "${csc_ms[@]}")
archie=$(median "${archie_ms[@]}")
share=$(awk -v a="$archie" -v c="$csc" 'BEGIN { printf "%.1f", 100 * a / c }')

echo "## Real build (SDK $(dotnet --version), median of $runs)"
echo
echo "| Metric | Value | Budget |"
echo "|---|---:|---:|"
echo "| Archie time in ReportAnalyzer / csc wall time | $share % | 5 % |"
echo "| Archie time / csc wall time | $archie / $csc ms | |"
echo "| Diagnostics reported | $diagnostics | |"
echo
echo "Archie's time is summed over threads and csc's is wall time, so the share is conservative on several cores (Phase 0 findings)."
[[ $diagnostics -gt 0 ]] || { echo "::error::Archie reported nothing, so it did not run."; exit 2; }
awk -v s="$share" 'BEGIN { exit !(s > 5) }' && { echo; echo "**Over budget.**"; exit 1; }
exit 0
