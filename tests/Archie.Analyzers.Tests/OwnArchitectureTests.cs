using System.Collections.Immutable;
using System.Reflection;
using Archie.Tests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace Archie.Analyzers.Tests;

/// <summary>
/// Archie checks its own architecture with src/archie.json: Core has no Roslyn types and no JSON library, the analyzer only depends on Core's
/// rules, and only the config reader reads JSON. Doubles as a test on real code that nobody wrote for a corpus.
/// </summary>
public class OwnArchitectureTests
{
    // What src/ is built against: netstandard2.0 and the Roslyn floor (Directory.Packages.props).
    private static readonly Lazy<Task<ImmutableArray<MetadataReference>>> FloorReferences = new(() => ReferenceAssemblies.NetStandard.NetStandard20
        .AddPackages([new PackageIdentity("Microsoft.CodeAnalysis.CSharp", RoslynFloor)])
        .ResolveAsync(LanguageNames.CSharp, CancellationToken.None));

    private static string RoslynFloor => typeof(OwnArchitectureTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "RoslynFloor").Value!;

    [Test]
    public async Task Archie_follows_its_own_rules()
    {
        var diagnostics = await RunAsync([]);

        diagnostics.Select(d => $"{d.Id} {d.Location.GetLineSpan()}: {d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)}").ShouldBeEmpty();
    }

    [Test]
    [Arguments("Archie.Core.Rules", "using Microsoft.CodeAnalysis; class Leak { ISymbol? S; }", "ARCH002")]
    [Arguments("Archie.Core.Json", "class Leak { Archie.Core.Config.ConfigResult? R; }", "ARCH001")]
    [Arguments("Archie.Analyzers", "class Leak { object R = new Archie.Core.Json.JsonArray(0, 0, []); }", "ARCH001")]
    [Arguments("Archie.Core.Json", "public class Leak { }", "ARCH105")]
    public async Task The_rules_are_live(string ns, string code, string expected)
    {
        // Without this, an archie.json that matches nothing (a renamed namespace) would pass the test above for ever.
        var diagnostics = await RunAsync([("/src/Leak.cs", $"#nullable enable\nnamespace {ns} {{ {code} }}")]);

        diagnostics.Select(d => d.Id).ShouldContain(expected);
    }

    [Test]
    public void Core_references_nothing_but_netstandard() =>
        References(typeof(Archie.Core.Config.ConfigParser).Assembly).ShouldBe(["netstandard 2.0"]);

    [Test]
    public void The_analyzer_references_only_Core_and_the_Roslyn_floor()
    {
        // Anything else would have to be loadable in every host; a newer Roslyn would not load in the oldest supported one (CS9057).
        var floor = new Version(RoslynFloor).ToString(2);

        References(typeof(ArchitectureAnalyzer).Assembly).ShouldBe(
            ["Archie.Core 1.0", $"Microsoft.CodeAnalysis {floor}", $"Microsoft.CodeAnalysis.CSharp {floor}", "netstandard 2.0", "System.Collections.Immutable 7.0"],
            ignoreOrder: true);
    }

    private static string[] References(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies().Select(name => $"{name.Name} {name.Version!.ToString(2)}")];

    private static async Task<IReadOnlyList<Diagnostic>> RunAsync(IEnumerable<(string Path, string Text)> extra)
    {
        var sources = new[] { "src/Archie.Core", "src/Archie.Analyzers" }
            .SelectMany(project => Directory.GetFiles(Repository.PathOf(project), "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .Concat(extra)
            .ToArray();
        sources.Length.ShouldBeGreaterThan(30);

        // One compilation for both projects: the rules are about namespaces, and the analyzer uses only Core's public API.
        return await AnalyzerRunner.RunAsync(sources, [("/src/archie.json", Repository.Read("src/archie.json"))], references: await FloorReferences.Value);
    }
}
