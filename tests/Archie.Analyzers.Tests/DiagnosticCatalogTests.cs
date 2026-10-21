using System.Text.RegularExpressions;
using Archie.Tests;
using Microsoft.CodeAnalysis;

namespace Archie.Analyzers.Tests;

/// <summary>
/// Every diagnostic the analyzer can report is documented where users look (docs/diagnostics, the README table, the help link) with the
/// title and default severity it really has, and its ID is in the range CLAUDE.md gives it. Release tracking (RS2000–RS2008) already
/// checks AnalyzerReleases.*.md at build time.
/// </summary>
public partial class DiagnosticCatalogTests
{
    private static readonly DiagnosticDescriptor[] Descriptors = [.. new ArchitectureAnalyzer().SupportedDiagnostics];

    public static IEnumerable<string> Ids() => Descriptors.Select(d => d.Id);

    [Test]
    [MethodDataSource(nameof(Ids))]
    public void Has_a_page_in_docs_diagnostics_that_starts_with_its_title(string id)
    {
        var descriptor = Descriptor(id);
        var page = Repository.PathOf($"docs/diagnostics/{id}.md");

        File.Exists(page).ShouldBeTrue($"{id} has no docs/diagnostics/{id}.md.");
        File.ReadLines(page).First().ShouldBe($"# {id}: {descriptor.Title}");
        descriptor.HelpLinkUri.ShouldBe($"https://github.com/Quattordici/Archie/blob/main/docs/diagnostics/{id}.md");
    }

    [Test]
    [MethodDataSource(nameof(Ids))]
    public void Is_in_the_README_table_with_its_title_and_default_severity(string id)
    {
        var descriptor = Descriptor(id);
        var severity = descriptor.DefaultSeverity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            DiagnosticSeverity.Info => "info",
            _ => "hidden",
        };

        ReadmeRows().ShouldContain($"| [{id}](docs/diagnostics/{id}.md) | {descriptor.Title} | {severity} |");
    }

    [Test]
    public void Documents_nothing_the_analyzer_does_not_report()
    {
        var pages = Directory.GetFiles(Repository.PathOf("docs/diagnostics"), "*.md").Select(Path.GetFileNameWithoutExtension);
        var rows = ReadmeRows().Select(row => DiagnosticRow().Match(row).Groups["id"].Value);

        pages.Order(StringComparer.Ordinal).ShouldBe(Ids().Order(StringComparer.Ordinal));
        rows.ShouldBe(Ids(), "The README table lists every diagnostic once, in ID order.");
    }

    [Test]
    [MethodDataSource(nameof(Ids))]
    public void Is_in_its_range_with_the_severity_the_range_has(string id)
    {
        // ARCH0xx rules and ARCH1xx conventions share Diagnostics.RuleSeverity; ARCH9xx config errors are errors, ARCH909 is info.
        var descriptor = Descriptor(id);
        descriptor.Category.ShouldBe("Archie");
        descriptor.IsEnabledByDefault.ShouldBeTrue();
        Regex.IsMatch(id, "^ARCH(0[0-9]{2}|1[0-9]{2}|9[0-9]{2})$").ShouldBeTrue($"{id} is outside ARCH0xx, ARCH1xx and ARCH9xx.");
        descriptor.DefaultSeverity.ShouldBe(id switch
        {
            "ARCH909" => DiagnosticSeverity.Info,
            _ when id.StartsWith("ARCH9", StringComparison.Ordinal) => DiagnosticSeverity.Error,
            _ => Diagnostics.RuleSeverity,
        });
    }

    private static DiagnosticDescriptor Descriptor(string id) => Descriptors.Single(d => d.Id == id);

    private static string[] ReadmeRows() =>
        [.. File.ReadLines(Repository.PathOf("README.md")).Where(line => DiagnosticRow().IsMatch(line))];

    [GeneratedRegex(@"^\| \[(?<id>ARCH\d{3})\]")]
    private static partial Regex DiagnosticRow();
}
