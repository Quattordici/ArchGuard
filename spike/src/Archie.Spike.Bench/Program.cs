using System.Collections.Immutable;
using System.Diagnostics;
using Archie.Spike.Analyzers;
using Archie.Spike.Bench;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

// Usage:
//   bench gen <outDir> [files=400]
//   bench run <csc-log> <projectDir> [--from Domain] [--to Infrastructure] [--iterations 5]
//             [--modes compile,project,noop,touch,block,operation,syntax] [--ide-docs 8] [--ide-reps 5]
//             [--baseline project|none]
// compile = compiler only; project = the project's own analyzers (what csc runs anyway);
// every other mode = baseline analyzers + the spike analyzer in that mode.
var argv = args.ToList();
if (argv.Count == 0)
{
    Console.Error.WriteLine("bench gen <outDir> [files] | bench run <csc-log> <projectDir> [options]");
    return 1;
}

if (argv[0] == "gen")
{
    var files = argv.Count > 2 ? int.Parse(argv[2]) : 400;
    var expected = SyntheticSolution.Generate(argv[1], files);
    Console.WriteLine($"Generated {files} files in {argv[1]}; expected ARCH001 per construct: {expected.PerConstruct} x {expected.ViolatingFiles} files");
    return 0;
}

string Opt(string name, string fallback)
{
    var i = argv.IndexOf("--" + name);
    return i >= 0 && i + 1 < argv.Count ? argv[i + 1] : fallback;
}

var log = argv[1];
var projectDir = argv[2];
var from = Opt("from", "Domain");
var to = Opt("to", "Infrastructure");
var iterations = int.Parse(Opt("iterations", "5"));
var modes = Opt("modes", "compile,project,noop,touch,block,operation,syntax").Split(',');
var baseline = Opt("baseline", "project");
var ideDocs = int.Parse(Opt("ide-docs", "8"));
var ideReps = int.Parse(Opt("ide-reps", "5"));

var sw = Stopwatch.StartNew();
var project = CscProject.Load(log, projectDir);
var loc = project.Trees.Sum(t => t.GetText().Lines.Count);
Console.WriteLine($"# {project.Name}: {project.Trees.Length} trees ({project.GeneratedTrees} generated), {loc:N0} lines, {project.References.Length} references, loaded in {sw.Elapsed.TotalSeconds:F1}s");
Console.WriteLine($"# rule: {from} must not depend on {to}; {Environment.ProcessorCount} cores; server GC off");

var analyzer = new SpikeAnalyzer();
var baselineAnalyzers = baseline == "project" ? project.ProjectAnalyzers : ImmutableArray<DiagnosticAnalyzer>.Empty;
Console.WriteLine($"# baseline: {baselineAnalyzers.Length} project analyzers");
ImmutableArray<DiagnosticAnalyzer> AnalyzersFor(string mode) =>
    mode == "project" ? baselineAnalyzers : baselineAnalyzers.Add(analyzer);
AnalyzerOptions OptionsFor(string mode) => new(
    ImmutableArray<AdditionalText>.Empty,
    new SpikeOptionsProvider(project.ConfigSet, ImmutableDictionary<string, string>.Empty
        .Add("build_property.ArchieSpikeMode", mode)
        .Add("build_property.ArchieSpikeFrom", from)
        .Add("build_property.ArchieSpikeTo", to)));

// ---- whole compilation --------------------------------------------------------------------------

var results = modes.ToDictionary(m => m, _ => new List<Sample>());
for (var i = 0; i <= iterations; i++) // iteration 0 is warm-up
{
    foreach (var mode in modes)
    {
        var sample = await MeasureCompilation(mode);
        if (i > 0)
            results[mode].Add(sample);
    }
}

Console.WriteLine();
Console.WriteLine("## Whole compilation (median of iterations)");
Console.WriteLine("| mode | wall ms | allocated MB | Δ alloc vs noop MB | spike exec ms | all analyzers exec ms | ARCH001 |");
Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|");
var noopAlloc = results.TryGetValue("noop", out var n) ? Median(n.Select(s => s.AllocatedMb)) : double.NaN;
foreach (var mode in modes)
{
    var s = results[mode];
    var alloc = Median(s.Select(x => x.AllocatedMb));
    var delta = mode is "compile" or "project" or "noop" ? "" : (alloc - noopAlloc).ToString("F1");
    Console.WriteLine($"| {mode} | {Median(s.Select(x => x.WallMs)):F0} | {alloc:F1} | {delta} | {Median(s.Select(x => x.AnalyzerMs)):F0} | {Median(s.Select(x => x.AllAnalyzersMs)):F0} | {s[0].Diagnostics} |");
}

foreach (var mode in modes.Where(m => m is "block" or "operation" or "syntax"))
{
    var byVia = results[mode][0].ByVia;
    Console.WriteLine($"# {mode} by construct: " + string.Join(", ", byVia.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}")));
}

// ---- IDE proxy: re-analyse one edited document --------------------------------------------------

var docs = project.Trees
    .Take(project.Trees.Length - project.GeneratedTrees)
    .Where(t => IsSourceLayer(t, from))
    .OrderByDescending(t => t.Length)
    .Take(ideDocs)
    .ToList();

if (docs.Count > 0)
{
    var baseCompilation = project.CreateCompilation();
    baseCompilation.GetDiagnostics(); // warm declarations, like an open solution

    Console.WriteLine();
    Console.WriteLine($"## Per-document re-analysis after an edit ({docs.Count} largest {from} documents, {ideReps} edits each, median)");
    Console.WriteLine("| mode | compiler semantic diags ms | analyzer wall ms | analyzer exec ms | p95 analyzer wall ms |");
    Console.WriteLine("|---|---:|---:|---:|---:|");
    foreach (var mode in modes.Where(m => m is not ("compile" or "project")))
    {
        var compilerMs = new List<double>();
        var wallMs = new List<double>();
        var execMs = new List<double>();
        for (var rep = 0; rep <= ideReps; rep++)
        {
            foreach (var doc in docs)
            {
                var text = doc.GetText();
                var edited = doc.WithChangedText(text.WithChanges(new TextChange(new TextSpan(0, 0), $"// edit {rep}\n")));
                var compilation = baseCompilation.ReplaceSyntaxTree(doc, edited);

                var t0 = Stopwatch.GetTimestamp();
                compilation.GetSemanticModel(edited).GetDiagnostics();
                var compilerElapsed = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;

                // Only the spike analyzer here: the IDE runs analyzers per document, and the question is our latency.
                var single = ImmutableArray.Create<DiagnosticAnalyzer>(analyzer);
                var cwa = compilation.WithAnalyzers(single,
                    new CompilationWithAnalyzersOptions(OptionsFor(mode), null, concurrentAnalysis: true, logAnalyzerExecutionTime: true));
                var model = cwa.Compilation.GetSemanticModel(edited);
                t0 = Stopwatch.GetTimestamp();
                await cwa.GetAnalyzerSemanticDiagnosticsAsync(model, null, single, CancellationToken.None);
                var wall = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
                var telemetry = await cwa.GetAnalyzerTelemetryInfoAsync(analyzer, CancellationToken.None);

                if (rep == 0)
                    continue;
                compilerMs.Add(compilerElapsed);
                wallMs.Add(wall);
                execMs.Add(telemetry.ExecutionTime.TotalMilliseconds);
            }
        }
        Console.WriteLine($"| {mode} | {Median(compilerMs):F1} | {Median(wallMs):F1} | {Median(execMs):F2} | {Percentile(wallMs, 0.95):F1} |");
    }
}

return 0;

async Task<Sample> MeasureCompilation(string mode)
{
    var compilation = project.CreateCompilation();
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var before = GC.GetTotalAllocatedBytes(precise: true);
    var t0 = Stopwatch.GetTimestamp();

    ImmutableArray<Diagnostic> diagnostics;
    AnalysisResult? result = null;
    if (mode == "compile")
    {
        diagnostics = compilation.GetDiagnostics();
    }
    else
    {
        var cwa = compilation.WithAnalyzers(AnalyzersFor(mode),
            new CompilationWithAnalyzersOptions(OptionsFor(mode), null, concurrentAnalysis: true, logAnalyzerExecutionTime: true));
        diagnostics = await cwa.GetAllDiagnosticsAsync();
        result = await cwa.GetAnalysisResultAsync(CancellationToken.None); // cached; carries telemetry
    }

    var wall = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
    var allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
    double analyzerMs = 0, allMs = 0;
    if (result is not null)
    {
        allMs = result.AnalyzerTelemetryInfo.Values.Sum(t => t.ExecutionTime.TotalMilliseconds);
        if (result.AnalyzerTelemetryInfo.TryGetValue(analyzer, out var mine))
            analyzerMs = mine.ExecutionTime.TotalMilliseconds;
    }
    var arch = diagnostics.Where(d => d.Id == SpikeAnalyzer.DiagnosticId).ToList();
    var byVia = arch.GroupBy(d => d.Properties.TryGetValue("via", out var v) ? v ?? "?" : "?")
        .ToDictionary(g => g.Key, g => g.Count());
    return new Sample(wall, allocated / 1024.0 / 1024.0, analyzerMs, allMs, arch.Count, byVia);
}

static bool IsSourceLayer(SyntaxTree tree, string from)
{
    var segments = from.Split(';', ',');
    var root = tree.GetRoot();
    var ns = root.DescendantNodes(n => n is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
        .OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
    return ns is not null && ns.Name.ToString().Split('.').Any(s => segments.Contains(s));
}

static double Median(IEnumerable<double> values) => Percentile(values.ToList(), 0.5);

static double Percentile(List<double> values, double p)
{
    if (values.Count == 0)
        return double.NaN;
    var sorted = values.OrderBy(v => v).ToList();
    return sorted[(int)Math.Min(sorted.Count - 1, Math.Round(p * (sorted.Count - 1)))];
}

internal sealed record Sample(double WallMs, double AllocatedMb, double AnalyzerMs, double AllAnalyzersMs, int Diagnostics, Dictionary<string, int> ByVia);
