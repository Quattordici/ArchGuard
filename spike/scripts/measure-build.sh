#!/usr/bin/env bash
# Rebuilds ONE project (references already built) per analyzer mode and records csc time,
# the spike analyzer's ReportAnalyzer time and the diagnostic count.
#
#   measure-build.sh <project.csproj> <label> <from-segments> <to-segments> [runs] [modes]
#
# Output: CSV on stdout: label,mode,run,csc_ms,archie_s,all_analyzers_s,arch001
# Build the project once normally first so references are up to date.
set -eu
proj=$1; label=$2; from=$3; to=$4; runs=${5:-5}; modes=${6:-"off noop block operation syntax"}
here=$(cd "$(dirname "$0")/.." && pwd)
inject="$here/build/Archie.Spike.Inject.targets"
logdir=${LOGDIR:-$(mktemp -d)}
echo "label,mode,run,csc_ms,archie_s,all_analyzers_s,arch001"
for run in $(seq 0 "$runs"); do          # run 0 is warm-up and not reported
  for mode in $modes; do
    log="$logdir/$label-$mode-$run.log"
    dotnet build "$proj" --no-restore -c Release -t:Rebuild -p:BuildProjectReferences=false \
      -p:CustomAfterMicrosoftCommonTargets="$inject" -p:ArchieSpikeMode="$mode" \
      -p:ArchieSpikeFrom="$from" -p:ArchieSpikeTo="$to" -p:ReportAnalyzer=true \
      "-flp:v=d;PerformanceSummary" -flp:logfile="$log" -v:q -nologo > /dev/null
    [ "$run" = 0 ] && continue
    csc=$(grep -E '^\s+[0-9]+ ms\s+Csc\s+[0-9]+ calls' "$log" | awk '{print $1}' | head -1 || true)
    archie=$(grep -E 'Archie.Spike.Analyzers, Version' "$log" | awk '{print $1}' | head -1 || true)
    total=$(grep -E 'Total analyzer execution time' "$log" | grep -oE '[0-9.]+' | head -1 || true)
    diags=$(grep -oE 'warning ARCH001: .*' "$log" | sort -u | wc -l || true)
    echo "$label,$mode,$run,${csc:-},${archie:-0},${total:-},$diags"
  done
done
