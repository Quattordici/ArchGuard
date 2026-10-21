using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;

namespace Archie.Analyzers.Tests;

/// <summary>
/// Runs every case of tests/Corpus/Dependencies through the analyzer and compares the violations with the expectations written in the case
/// (tests/Corpus/README.md). The corpus is data so the Phase 4 metadata backend can run the same cases.
/// </summary>
public partial class DependencyCorpusTests
{
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Corpus", "Dependencies");

    public static IEnumerable<string> Cases() =>
        Directory.GetFiles(Folder, "*.cs").Select(Path.GetFileName).Where(name => !name!.StartsWith('_')).Order(StringComparer.Ordinal)!;

    [Test]
    [MethodDataSource(nameof(Cases))]
    public async Task Case_produces_exactly_its_expected_violations(string file)
    {
        var shared = Directory.GetFiles(Folder, "_*.cs").Order(StringComparer.Ordinal);
        var sources = shared.Append(Path.Combine(Folder, file)).Select(path => (Path: path, Text: File.ReadAllText(path))).ToArray();

        var diagnostics = await AnalyzerRunner.RunAsync(
            sources,
            [("/solution/archie.json", await File.ReadAllTextAsync(Path.Combine(Folder, "archie.json")))]);

        var actual = diagnostics.Select(Describe).Order(StringComparer.Ordinal).ToArray();
        var expected = sources.SelectMany(source => Expectations(source.Path, source.Text)).Order(StringComparer.Ordinal).ToArray();
        actual.ShouldBe(expected);
    }

    [Test]
    public void The_corpus_has_cases() => Cases().Count().ShouldBeGreaterThan(5);

    private static string Describe(Diagnostic diagnostic)
    {
        var line = diagnostic.Location.GetLineSpan();
        var text = diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);
        return Key(Path.GetFileName(line.Path), line.StartLinePosition.Line + 1, diagnostic.Id, diagnostic.Properties["sourceId"]!,
            diagnostic.Properties["targetId"]!, text);
    }

    private static IEnumerable<string> Expectations(string path, string text)
    {
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var comment = lines[i].IndexOf("// ARCH", StringComparison.Ordinal);
            if (comment < 0)
            {
                continue;
            }

            foreach (Match match in Expectation().Matches(lines[i][comment..]))
            {
                yield return Key(Path.GetFileName(path), i + 1, match.Groups["id"].Value, match.Groups["source"].Value, match.Groups["target"].Value,
                    match.Groups["text"].Value);
            }
        }
    }

    private static string Key(string file, int line, string id, string sourceId, string targetId, string text) =>
        $"{file}({line}): {id} {sourceId} {targetId} \"{text}\"";

    [GeneratedRegex(@"(?<id>ARCH\d{3}) (?<source>\S+) (?<target>\S+) ""(?<text>[^""]*)""")]
    private static partial Regex Expectation();
}

/// <summary>Compiles sources with the analyzer the way csc does, without the verifier's location matching.</summary>
internal static class AnalyzerRunner
{
    private static readonly Lazy<Task<ImmutableArray<MetadataReference>>> References =
        new(() => ReferenceAssemblies.Net.Net80.ResolveAsync(LanguageNames.CSharp, CancellationToken.None));

    public static async Task<ImmutableArray<Diagnostic>> RunAsync(IEnumerable<(string Path, string Text)> sources,
        IEnumerable<(string Path, string Text)> additionalFiles, IReadOnlyDictionary<string, string>? buildProperties = null)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Diagnose);
        var trees = sources.Select(source => CSharpSyntaxTree.ParseText(source.Text, parseOptions, source.Path)).ToArray();
        var compilation = CSharpCompilation.Create(
            "Cases",
            trees,
            await References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        errors.ShouldBeEmpty("The case must compile.");

        var exceptions = new List<Exception>();
        var options = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions(
                additionalFiles.Select(file => (AdditionalText)new InMemoryAdditionalText(file.Path, file.Text)).ToImmutableArray(),
                new GlobalOptionsProvider(buildProperties ?? new Dictionary<string, string>())),
            onAnalyzerException: (exception, _, _) => exceptions.Add(exception),
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false);

        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ArchitectureAnalyzer()), options)
            .GetAnalyzerDiagnosticsAsync();

        exceptions.ShouldBeEmpty();
        return diagnostics;
    }

    private sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }

    private sealed class GlobalOptionsProvider(IReadOnlyDictionary<string, string> buildProperties) : AnalyzerConfigOptionsProvider
    {
        private static readonly AnalyzerConfigOptions Empty = new Options(new Dictionary<string, string>());

        public override AnalyzerConfigOptions GlobalOptions { get; } =
            new Options(buildProperties.ToDictionary(p => "build_property." + p.Key, p => p.Value));

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;
    }

    private sealed class Options(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
    }
}
