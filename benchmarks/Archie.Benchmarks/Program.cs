using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using Archie.Analyzers;
using Archie.Benchmarks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

// Usage: Archie.Benchmarks [--files 400] [--iterations 5] [--budget]    measure in process
//        Archie.Benchmarks gen <folder> [--files 400]                  write the code as a project for benchmarks/build-budget.sh
// Compiles the synthetic codebase in memory and measures Archie on top of an analyzer that only forces operation trees, like the SDK's own
// analyzers do in a real build (without that baseline the trees would be charged to Archie; Phase 0 findings). Allocations are the budget
// here; time is only reported, because in process the compiler's own lazy work lands in whichever analyzer asks first and swings the
// telemetry by a factor of three between runs. With --budget the exit code is 1 when over budget. Prints Markdown, for $GITHUB_STEP_SUMMARY.
var files = int.Parse(Option("files", "400"), CultureInfo.InvariantCulture);
if (args.Length > 1 && args[0] == "gen")
{
    // For benchmarks/build-budget.sh: the same code as a project that references the packed Archie package, measured in a real build.
    var target = args[1];
    foreach (var (path, text) in SyntheticCode.Generate(files))
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(target, path))!);
        File.WriteAllText(Path.Combine(target, path), text);
    }

    File.WriteAllText(Path.Combine(target, "archie.json"), ArchieJson);
    File.WriteAllText(Path.Combine(target, "Synth.csproj"), SynthProject);
    File.WriteAllText(Path.Combine(target, "warnings.globalconfig"),
        "is_global = true\n" + string.Join("", new[] { "001", "002", "101", "102", "103", "104", "105" }.Select(id => $"dotnet_diagnostic.ARCH{id}.severity = warning\n")));
    return 0;
}

var iterations = int.Parse(Option("iterations", "5"), CultureInfo.InvariantCulture);
const double AllocationBudgetMb = 50; // per 100k lines (design §9); the time budget is measured in a real build by build-budget.sh

var sources = SyntheticCode.Generate(files);
var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
var trees = sources.Select(s => CSharpSyntaxTree.ParseText(SourceText.From(s.Text), parseOptions, "/synth/" + s.Path)).ToImmutableArray();
var lines = trees.Sum(t => t.GetText().Lines.Count);
var frameworkDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
var references = Directory.GetFiles(frameworkDirectory, "*.dll")
    .Where(path => Path.GetFileName(path) is var name && (name.StartsWith("System.", StringComparison.Ordinal) || name is "netstandard.dll" or "mscorlib.dll"))
    .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
    .ToImmutableArray();
var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, concurrentBuild: true);

var errors = Create().GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Take(5).ToArray();
if (errors.Length > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, errors.Select(e => e.ToString())));
    return 2;
}

var analyzerOptions = new AnalyzerOptions([new Config("/synth/archie.json", ArchieJson)], new NoOptions());

var compileMs = new List<double>();
var allocatedMb = new List<double>();
var archieMs = new List<double>();
var reported = 0;
for (var i = 0; i <= iterations; i++) // 0 is the warm-up
{
    var (compile, _, _, _) = await Measure(analyzers: null);
    var (_, baseline, _, _) = await Measure([new OperationTreeAnalyzer()]);
    var (_, withArchie, exec, count) = await Measure([new OperationTreeAnalyzer(), new ArchitectureAnalyzer()]);
    if (i > 0)
    {
        compileMs.Add(compile);
        allocatedMb.Add(withArchie - baseline);
        archieMs.Add(exec);
        reported = count;
    }
}

var allocationMb = Median(allocatedMb) * 100_000 / lines;
var ideMs = await PerDocument();

Console.WriteLine($"## In process ({lines:N0} lines, {trees.Length} files, {Environment.ProcessorCount} cores, Roslyn {typeof(Compilation).Assembly.GetName().Version!.ToString(2)})");
Console.WriteLine();
Console.WriteLine("| Metric | Value | Budget |");
Console.WriteLine("|---|---:|---:|");
Console.WriteLine($"| Allocations over the baseline, per 100k lines (median) | {allocationMb:F1} MB | {AllocationBudgetMb:F0} MB |");
Console.WriteLine($"| Archie time, single-threaded (fastest of {iterations}) | {archieMs.Min():F0} ms | |");
Console.WriteLine($"| Compile time, binding only (fastest of {iterations}) | {compileMs.Min():F0} ms | |");
Console.WriteLine($"| Archie time per edited document (median, 8 largest Domain files) | {ideMs:F2} ms | |");
Console.WriteLine($"| Diagnostics reported | {reported} | |");

if (reported == 0)
{
    Console.Error.WriteLine("Archie reported nothing, so it did not run; the numbers mean nothing.");
    return 2;
}

var over = allocationMb > AllocationBudgetMb;
if (over)
{
    Console.WriteLine();
    Console.WriteLine("**Over budget.**");
}

return over && args.Contains("--budget") ? 1 : 0;

CSharpCompilation Create() => CSharpCompilation.Create("Synth", trees, references, options);

// The compilation is bound first (that is the compile time), then the analyzers run on it, one at a time so Archie's time is its own work
// rather than waiting on other threads.
async Task<(double WallMs, double AllocatedMb, double ArchieMs, int Diagnostics)> Measure(DiagnosticAnalyzer[]? analyzers)
{
    var compilation = Create();
    var start = Stopwatch.GetTimestamp();
    compilation.GetDiagnostics();
    if (analyzers is null)
    {
        return (Stopwatch.GetElapsedTime(start).TotalMilliseconds, 0, 0, 0);
    }

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var before = GC.GetTotalAllocatedBytes(precise: true);
    start = Stopwatch.GetTimestamp();
    var withAnalyzers = compilation.WithAnalyzers([.. analyzers],
        new CompilationWithAnalyzersOptions(analyzerOptions, null, concurrentAnalysis: false, logAnalyzerExecutionTime: true));
    var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    var wall = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    var allocated = Mb(before);
    var telemetry = await withAnalyzers.GetAnalysisResultAsync(CancellationToken.None);

    // Roslyn keeps telemetry per analyzer instance across compilations, so every measurement gets new instances.
    var exec = telemetry.AnalyzerTelemetryInfo.Where(t => t.Key is ArchitectureAnalyzer).Sum(t => t.Value.ExecutionTime.TotalMilliseconds);
    return (wall, allocated, exec, diagnostics.Count(d => d.Id.StartsWith("ARCH", StringComparison.Ordinal)));
}

// The IDE proxy from Phase 0: one edit in a large Domain file, then Archie on that document only.
async Task<double> PerDocument()
{
    var compilation = Create();
    compilation.GetDiagnostics();
    var documents = trees.Where(t => t.FilePath.StartsWith("/synth/Domain/", StringComparison.Ordinal)).OrderByDescending(t => t.Length).Take(8).ToArray();
    var samples = new List<double>();
    for (var rep = 0; rep <= 5; rep++)
    {
        foreach (var document in documents)
        {
            var edited = document.WithChangedText(document.GetText().WithChanges(new TextChange(new TextSpan(0, 0), $"// edit {rep}\n")));
            var archie = new ArchitectureAnalyzer();
            var withAnalyzers = compilation.ReplaceSyntaxTree(document, edited).WithAnalyzers([archie],
                new CompilationWithAnalyzersOptions(analyzerOptions, null, concurrentAnalysis: true, logAnalyzerExecutionTime: true));
            var model = withAnalyzers.Compilation.GetSemanticModel(edited);
            await withAnalyzers.GetAnalyzerSemanticDiagnosticsAsync(model, null, [archie], CancellationToken.None);
            var telemetry = await withAnalyzers.GetAnalyzerTelemetryInfoAsync(archie, CancellationToken.None);
            if (rep > 0)
            {
                samples.Add(telemetry.ExecutionTime.TotalMilliseconds);
            }
        }
    }

    return Median(samples);
}

string Option(string name, string fallback)
{
    var index = Array.IndexOf(args, "--" + name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
}

static double Mb(long before) => (GC.GetTotalAllocatedBytes(precise: true) - before) / 1024.0 / 1024.0;

static double Median(List<double> values)
{
    var sorted = values.Order().ToList();
    return sorted.Count == 0 ? double.NaN : sorted[sorted.Count / 2];
}

partial class Program
{
    // Every layer is checked, plus one convention of each kind, so the whole analyzer is measured (Phase 0 measured one rule).
    private const string SynthProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>disable</ImplicitUsings>
            <Nullable>disable</Nullable>
            <NoWarn>$(NoWarn);CS0168;CS0219;CS0169;CS0649;CS0414;CS8321</NoWarn>
            <!-- The 222 violations are the point; as warnings the build finishes and reports the analyzer's time. -->
            <WarningsAsErrors />
            <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Archie" Version="$(ArchieVersion)" PrivateAssets="all" />
            <GlobalAnalyzerConfigFiles Include="warnings.globalconfig" />
          </ItemGroup>
        </Project>
        """;

    private const string ArchieJson = """
        {
          "rootNamespace": "Synth",
          "layers": ["Infrastructure", "Application", "Domain"],
          "mustNotDependOn": { "Domain": ["System.Net.Http"] },
          "conventions": [
            { "types": { "named": "Entity*" }, "mustBeIn": ["Domain", "Application", "Infrastructure"] },
            { "types": { "inherit": "EntityBase" }, "mustNotBe": "abstract", "mustNotHaveAttribute": "Obsolete" },
            { "types": { "named": "I*Store" }, "mustBe": "interface", "mustNotInherit": "IDisposable" },
            { "mustNotBeNamed": ["*Manager", "*Helper"] },
          ],
        }
        """;
}

/// <summary>What the SDK's analyzers do in every real build: ask for every operation tree. Archie is measured on top of it.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class OperationTreeAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Unused = new("BENCH001", "Unused", "Unused", "Bench", DiagnosticSeverity.Hidden, true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Unused];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterOperationBlockStartAction(block => block.RegisterOperationAction(_ => { }, OperationKind.Invocation));
    }
}

internal sealed class Config(string path, string text) : AdditionalText
{
    public override string Path { get; } = path;

    public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
}

internal sealed class NoOptions : AnalyzerConfigOptionsProvider
{
    private static readonly AnalyzerConfigOptions Empty = new EmptyOptions();

    public override AnalyzerConfigOptions GlobalOptions => Empty;

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;

    private sealed class EmptyOptions : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            value = null!;
            return false;
        }
    }
}
