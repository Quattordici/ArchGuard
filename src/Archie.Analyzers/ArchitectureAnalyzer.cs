using System;
using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Archie.Analyzers;

/// <summary>Reports architecture rule violations. For now only finds the configuration (ARCH909 when there is none).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ArchitectureAnalyzer : DiagnosticAnalyzer
{
    private const string ConfigFileName = "archie.json";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Diagnostics.NoConfig);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationAction(OnCompilation);
    }

    private static void OnCompilation(CompilationAnalysisContext context)
    {
        var properties = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
        if (!IsTrue(properties, "ArchieEnabled", whenMissing: true))
        {
            return;
        }

        // Test projects are excluded by default (design §4.4), so a missing config is only worth mentioning for production code.
        if (FindConfig(context.Options.AdditionalFiles) is null && !IsTrue(properties, "IsTestProject", whenMissing: false))
        {
            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.NoConfig, Location.None));
        }
    }

    private static AdditionalText? FindConfig(ImmutableArray<AdditionalText> files)
    {
        foreach (var file in files)
        {
            if (string.Equals(Path.GetFileName(file.Path), ConfigFileName, StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }

        return null;
    }

    private static bool IsTrue(AnalyzerConfigOptions options, string property, bool whenMissing) =>
        options.TryGetValue("build_property." + property, out var value) && value.Length > 0
            ? string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            : whenMissing;
}
