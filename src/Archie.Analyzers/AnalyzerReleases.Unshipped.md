; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ARCH001 | Archie | Error | ArchitectureAnalyzer, layer dependency not allowed
ARCH002 | Archie | Error | ArchitectureAnalyzer, forbidden namespace used
ARCH900 | Archie | Error | ArchitectureAnalyzer, archie.json could not be read
ARCH901 | Archie | Error | ArchitectureAnalyzer, unknown or unsupported key in archie.json
ARCH902 | Archie | Error | ArchitectureAnalyzer, invalid value in archie.json
ARCH909 | Archie | Info | ArchitectureAnalyzer, no archie.json found
