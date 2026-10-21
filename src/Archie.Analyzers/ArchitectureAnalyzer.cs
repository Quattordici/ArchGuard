using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Archie.Core.Config;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Archie.Analyzers;

/// <summary>Reports architecture rule violations (ARCH001, ARCH002), convention violations (ARCH101–ARCH105) and problems with archie.json (ARCH900–ARCH909).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ArchitectureAnalyzer : DiagnosticAnalyzer
{
    private const string ConfigFileName = "archie.json";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
        Diagnostics.LayerDependency,
        Diagnostics.ForbiddenNamespace,
        Diagnostics.Naming,
        Diagnostics.Placement,
        Diagnostics.Inheritance,
        Diagnostics.Attribute,
        Diagnostics.KindAndModifiers,
        Diagnostics.Unreadable,
        Diagnostics.UnknownKey,
        Diagnostics.InvalidValue,
        Diagnostics.NoConfig);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var properties = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
        if (!IsTrue(properties, "ArchieEnabled", whenMissing: true))
        {
            return;
        }

        var isTestProject = IsTrue(properties, "IsTestProject", whenMissing: false);
        var configs = FindConfigs(context.Options.AdditionalFiles);
        if (configs.Count == 0)
        {
            // Test projects are excluded by default (design §4.4), so a missing config is only worth mentioning for production code.
            if (!isTestProject)
            {
                context.RegisterCompilationEndAction(static end => end.ReportDiagnostic(Diagnostic.Create(Diagnostics.NoConfig, Location.None)));
            }

            return;
        }

        var config = configs[0];
        var text = config.GetText(context.CancellationToken);
        var entry = ConfigCache.Get(text?.ToString() ?? string.Empty);

        // A test project is skipped unless the config says otherwise, and then also stays quiet about config errors: the production projects
        // that share the file report them.
        if (isTestProject && entry.Result.Model?.CheckTestProjects != true)
        {
            return;
        }

        context.RegisterAdditionalFileAction(file => ReportConfigProblems(file, configs, text, entry.Result));
        if (entry.Evaluator is null)
        {
            return; // a config that is not fully understood checks nothing; ARCH9xx says why
        }

        if (entry.Conventions is { } conventions)
        {
            var namespaces = new NamespaceNames();
            context.RegisterSymbolAction(symbol => ConventionRules.Analyze(symbol, conventions, namespaces), SymbolKind.NamedType);
        }

        if (entry.Evaluator.Model.Layers.Count == 0)
        {
            return; // conventions only
        }

        var checker = new DependencyChecker(entry.Evaluator);
        context.RegisterSymbolAction(symbol => DeclarationRules.Analyze(symbol, checker), DeclarationRules.Kinds);
        context.RegisterOperationBlockStartAction(block =>
        {
            if (checker.CheckedSource(block.OwningSymbol) is { } source)
            {
                block.RegisterOperationAction(operation => BodyRules.Analyze(operation, checker, source), BodyRules.Kinds);
            }
        });
    }

    private static void ReportConfigProblems(AdditionalFileAnalysisContext context, List<AdditionalText> configs, SourceText? text, ConfigResult result)
    {
        var file = context.AdditionalFile;
        if (file == configs[0])
        {
            if (text is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.Unreadable, Location.None, $"{file.Path} could not be read."));
                return;
            }

            foreach (var error in result.Errors)
            {
                var span = new TextSpan(error.Start, Math.Min(error.Length, text.Length - error.Start));
                var location = Location.Create(file.Path, span, text.Lines.GetLinePositionSpan(span));
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ForConfigError(error.Code), location, error.Message));
            }
        }
        else if (configs.Contains(file))
        {
            var location = Location.Create(file.Path, default, default);
            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.Unreadable, location,
                $"More than one archie.json is passed to the compiler; only {configs[0].Path} is used. Remove the other AdditionalFiles item, or set ArchieConfig in the project to choose one."));
        }
    }

    private static List<AdditionalText> FindConfigs(ImmutableArray<AdditionalText> files)
    {
        var configs = new List<AdditionalText>(1);
        foreach (var file in files)
        {
            if (string.Equals(Path.GetFileName(file.Path), ConfigFileName, StringComparison.OrdinalIgnoreCase))
            {
                configs.Add(file);
            }
        }

        return configs;
    }

    private static bool IsTrue(AnalyzerConfigOptions options, string property, bool whenMissing) =>
        options.TryGetValue("build_property." + property, out var value) && value.Length > 0
            ? string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            : whenMissing;
}
