; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ARCH001 | Archie | Error | ArchitectureAnalyzer, layer dependency not allowed
ARCH002 | Archie | Error | ArchitectureAnalyzer, forbidden namespace used
ARCH101 | Archie | Error | ArchitectureAnalyzer, type name breaks a convention
ARCH102 | Archie | Error | ArchitectureAnalyzer, type is in the wrong place
ARCH103 | Archie | Error | ArchitectureAnalyzer, type's base types break a convention
ARCH104 | Archie | Error | ArchitectureAnalyzer, type's attributes break a convention
ARCH105 | Archie | Error | ArchitectureAnalyzer, type's kind or modifiers break a convention
ARCH900 | Archie | Error | ArchitectureAnalyzer, archie.json could not be read
ARCH901 | Archie | Error | ArchitectureAnalyzer, unknown or unsupported key in archie.json
ARCH902 | Archie | Error | ArchitectureAnalyzer, invalid value in archie.json
ARCH909 | Archie | Info | ArchitectureAnalyzer, no archie.json found
