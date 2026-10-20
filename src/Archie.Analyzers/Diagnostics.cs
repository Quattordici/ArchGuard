using Microsoft.CodeAnalysis;

namespace Archie.Analyzers;

internal static class Diagnostics
{
    public const string Category = "Archie";

    public static readonly DiagnosticDescriptor NoConfig = new(
        id: "ARCH909",
        title: "No archie.json found",
        messageFormat: "Archie is installed but found no archie.json; add one next to your .sln",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.CompilationEnd);
}
