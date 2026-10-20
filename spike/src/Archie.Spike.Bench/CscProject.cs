using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Archie.Spike.Bench;

/// <summary>
/// Rebuilds a project's compilation from the csc command line MSBuild logged, so the harness
/// analyses exactly what the build compiled: same sources, references, options, .globalconfig
/// and source generator output.
/// </summary>
internal sealed class CscProject
{
    public required string Name { get; init; }
    public required ImmutableArray<SyntaxTree> Trees { get; init; }
    public required ImmutableArray<MetadataReference> References { get; init; }
    public required CSharpCompilationOptions Options { get; init; }
    public required AnalyzerConfigSet ConfigSet { get; init; }
    public required int GeneratedTrees { get; init; }
    /// <summary>The project's own analyzers (NetAnalyzers, ASP.NET, package analyzers): the baseline csc already runs.</summary>
    public required ImmutableArray<DiagnosticAnalyzer> ProjectAnalyzers { get; init; }

    public CSharpCompilation CreateCompilation() =>
        CSharpCompilation.Create(Name, Trees, References, Options);

    public static CscProject Load(string logPath, string projectDir)
    {
        var line = File.ReadLines(logPath).FirstOrDefault(l => l.Contains("/bincore/csc ", StringComparison.Ordinal))
                   ?? throw new InvalidOperationException($"No csc command line in {logPath}. Build with -v:n or more.");
        var commandLine = line[(line.IndexOf("/bincore/csc ", StringComparison.Ordinal) + "/bincore/csc ".Length)..];
        var rawArgs = CommandLineParser.SplitCommandLineIntoArguments(commandLine, removeHashComments: false).ToArray();
        var args = CSharpCommandLineParser.Default.Parse(rawArgs, projectDir, sdkDirectory: null);
        foreach (var d in args.Errors)
            Console.Error.WriteLine($"  csc args: {d}");

        var parseOptions = args.ParseOptions;
        var trees = args.SourceFiles
            .AsParallel().AsOrdered()
            .Select(f => CSharpSyntaxTree.ParseText(SourceText.From(File.ReadAllText(f.Path), System.Text.Encoding.UTF8), parseOptions, f.Path))
            .ToImmutableArray();

        var references = args.MetadataReferences
            .Select(r => (MetadataReference)MetadataReference.CreateFromFile(r.Reference, r.Properties))
            .ToImmutableArray();

        var configs = args.AnalyzerConfigPaths
            .Select(p => AnalyzerConfig.Parse(SourceText.From(File.ReadAllText(p)), p))
            .ToImmutableArray();
        var configSet = AnalyzerConfigSet.Create(configs);

        // Severities from the SDK's analysislevel .globalconfig, so disabled CA rules stay disabled.
        var options = args.CompilationOptions
            .WithConcurrentBuild(true)
            .WithSyntaxTreeOptionsProvider(null);
        options = options.WithSpecificDiagnosticOptions(
            configSet.GlobalConfigOptions.TreeOptions.SetItems(options.SpecificDiagnosticOptions));

        var name = Path.GetFileNameWithoutExtension(args.OutputFileName ?? "Project");
        var compilation = CSharpCompilation.Create(name, trees, references, options);

        // Run the project's source generators once and keep their output as ordinary trees.
        var loader = new SimpleLoader();
        var analyzerReferences = args.AnalyzerReferences
            .Select(r => new AnalyzerFileReference(Path.GetFullPath(r.FilePath, projectDir), loader))
            .ToList();
        var generators = analyzerReferences.SelectMany(r => r.GetGenerators(LanguageNames.CSharp)).ToImmutableArray();
        var projectAnalyzers = analyzerReferences.SelectMany(r => r.GetAnalyzers(LanguageNames.CSharp)).ToImmutableArray();
        var provider = new SpikeOptionsProvider(configSet, ImmutableDictionary<string, string>.Empty);
        var driver = CSharpGeneratorDriver.Create(generators, parseOptions: parseOptions, optionsProvider: provider);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var withGenerated, out _);
        var generated = withGenerated.SyntaxTrees.Skip(trees.Length).ToImmutableArray();

        return new CscProject
        {
            Name = name,
            Trees = trees.AddRange(generated),
            References = references,
            Options = options,
            ConfigSet = configSet,
            GeneratedTrees = generated.Length,
            ProjectAnalyzers = projectAnalyzers,
        };
    }

    private sealed class SimpleLoader : IAnalyzerAssemblyLoader
    {
        private readonly AssemblyLoadContext _context = new("generators");
        private readonly Dictionary<string, string> _byName = new(StringComparer.OrdinalIgnoreCase);

        public SimpleLoader() => _context.Resolving += (ctx, name) =>
            _byName.TryGetValue(name.Name!, out var path) ? ctx.LoadFromAssemblyPath(path) : null;

        public void AddDependencyLocation(string fullPath) =>
            _byName.TryAdd(Path.GetFileNameWithoutExtension(fullPath), fullPath);

        public Assembly LoadFromPath(string fullPath)
        {
            // Roslyn itself comes from the host so generators see the same Microsoft.CodeAnalysis types.
            var name = AssemblyName.GetAssemblyName(fullPath);
            if (name.Name!.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal))
                return AssemblyLoadContext.Default.LoadFromAssemblyName(name);
            return _context.LoadFromAssemblyPath(fullPath);
        }
    }
}

/// <summary>Analyzer options built from the project's .globalconfig/.editorconfig plus spike overrides.</summary>
internal sealed class SpikeOptionsProvider(AnalyzerConfigSet configSet, ImmutableDictionary<string, string> overrides)
    : AnalyzerConfigOptionsProvider
{
    private readonly DictionaryOptions _global = new(configSet.GlobalConfigOptions.AnalyzerOptions.SetItems(overrides));

    public override AnalyzerConfigOptions GlobalOptions => _global;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) =>
        new DictionaryOptions(configSet.GetOptionsForSourcePath(tree.FilePath).AnalyzerOptions);

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
        new DictionaryOptions(configSet.GetOptionsForSourcePath(textFile.Path).AnalyzerOptions);

    private sealed class DictionaryOptions(ImmutableDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
    }
}
