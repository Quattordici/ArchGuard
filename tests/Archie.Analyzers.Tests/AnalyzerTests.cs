using Microsoft.CodeAnalysis;

namespace Archie.Analyzers.Tests;

/// <summary>What the analyzer adds around the rules: messages, properties, config diagnostics, excluded projects and severities.</summary>
public class AnalyzerTests
{
    private const string Config = """{ "rootNamespace": "App", "layers": ["Infrastructure", "Application", "Domain"], "mustNotDependOn": { "Domain": "Vendor" } }""";

    private const string Source = """
        namespace App.Infrastructure { public class Db { } }
        namespace Vendor { public class Orm { } }
        namespace App.Domain.Orders
        {
            public class Order
            {
                public void Save(App.Infrastructure.Db db) { }

                public Vendor.Orm? Orm;
            }
        }
        """;

    [Test]
    public async Task ARCH001_says_what_was_used_which_layers_and_what_is_allowed()
    {
        var diagnostic = (await RunAsync(Source, Config)).Single(d => d.Id == "ARCH001");

        diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).ShouldBe(
            "Domain must not depend on Infrastructure: 'Order.Save' uses 'Db' (App.Infrastructure). Domain may depend on: nothing.");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.Properties.ShouldBe(new Dictionary<string, string?>
        {
            ["from"] = "Domain",
            ["to"] = "Infrastructure",
            ["target"] = "App.Infrastructure",
            ["sourceId"] = "M:App.Domain.Orders.Order.Save(App.Infrastructure.Db)",
            ["targetId"] = "T:App.Infrastructure.Db",
            ["kind"] = "Parameter",
        }, ignoreOrder: true);
    }

    [Test]
    public async Task ARCH002_names_the_forbidden_namespace()
    {
        var diagnostic = (await RunAsync(Source, Config)).Single(d => d.Id == "ARCH002");

        diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).ShouldBe(
            "Domain must not depend on 'Vendor': 'Order.Orm' uses 'Orm'. Forbidden by mustNotDependOn in archie.json.");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.Properties["to"].ShouldBe("");
        diagnostic.Properties["targetId"].ShouldBe("T:Vendor.Orm");
    }

    [Test]
    public async Task The_allowed_list_names_the_layers_below()
    {
        const string source = """
            namespace App.Infrastructure { public class Db { } }
            namespace App.Application { public class Service { public App.Infrastructure.Db? Db; } }
            """;

        var diagnostic = (await RunAsync(source, Config)).Single();

        diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).ShouldEndWith("Application may depend on: Domain.");
    }

    [Test]
    public async Task The_named_map_form_gives_the_same_diagnostics()
    {
        const string map = """
            {
              "layers": {
                "Domain":         { "namespaces": "App.Domain", "mustNotDependOn": "Vendor" },
                "Application":    { "namespaces": "App.Application", "mayDependOn": ["Domain"] },
                "Infrastructure": { "namespaces": "App.Infrastructure", "mayDependOn": ["Application", "Domain"] },
              },
            }
            """;

        var fromList = (await RunAsync(Source, Config)).Select(Key);
        var fromMap = (await RunAsync(Source, map)).Select(Key);

        fromMap.ShouldBe(fromList, ignoreOrder: true);
    }

    [Test]
    public async Task A_convention_violation_says_what_is_required_where_the_type_is_and_which_convention()
    {
        const string config = """
            {
              "rootNamespace": "App",
              "layers": ["Application", "Domain"],
              "conventions": [{ "name": "Handlers", "types": { "inherit": "IHandler<>" }, "mustBeIn": "Application" }]
            }
            """;
        const string source = """
            namespace App.Domain
            {
                public interface IHandler<T> { }
                public sealed class Thing { }
            }
            namespace App.Domain.Orders { public sealed class PlaceOrder : IHandler<Thing> { } }
            """;

        var diagnostic = (await RunAsync(source, config)).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe("ARCH102");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).ShouldBe(
            "'PlaceOrder' must be in Application, but is in Domain (App.Domain.Orders). Convention 'Handlers' in archie.json applies to types that inherit 'IHandler<>'.");
        diagnostic.Properties.ShouldBe(new Dictionary<string, string?>
        {
            ["convention"] = "Handlers",
            ["sourceId"] = "T:App.Domain.Orders.PlaceOrder",
            ["targetId"] = "mustBeIn=Application",
            ["kind"] = "mustBeIn",
        }, ignoreOrder: true);
    }

    [Test]
    public async Task Conventions_work_without_layers()
    {
        const string config = """{ "conventions": [{ "types": { "are": "interface" }, "mustBeNamed": "I*" }] }""";
        const string source = "namespace Anything { public interface Repository { } public interface IGood { } }";

        var diagnostic = (await RunAsync(source, config)).ShouldHaveSingleItem();

        diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).ShouldBe(
            "'Repository' must be named 'I*'. conventions[0] in archie.json applies to types that are interface.");
    }

    [Test]
    public async Task Config_errors_point_at_the_line_in_archie_json_and_check_nothing()
    {
        const string config = """
            {
              "rootNamespace": "App",
              "layers": ["Infrastructure", "Domain"],
              "mayDependsOn": {}
            }
            """;

        var diagnostic = (await RunAsync(Source, config)).ShouldHaveSingleItem();

        diagnostic.Id.ShouldBe("ARCH901");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).ShouldBe(
            "Unknown key 'mayDependsOn' at the top level. Known keys: $schema, rootNamespace, layers, mustNotDependOn, exclude, checkTestProjects, conventions.");
        var span = diagnostic.Location.GetLineSpan();
        span.Path.ShouldBe("/solution/archie.json");
        span.StartLinePosition.ShouldBe(new Microsoft.CodeAnalysis.Text.LinePosition(3, 2));
        span.EndLinePosition.ShouldBe(new Microsoft.CodeAnalysis.Text.LinePosition(3, 16));
    }

    [Test]
    public async Task A_second_archie_json_is_ARCH900()
    {
        var diagnostics = await AnalyzerRunner.RunAsync(
            [("/solution/src/Order.cs", "namespace App.Domain { public class Order { } }")],
            [("/solution/archie.json", Config), ("/solution/src/archie.json", Config)]);

        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("ARCH900");
        diagnostic.Location.GetLineSpan().Path.ShouldBe("/solution/src/archie.json");
        diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).ShouldStartWith(
            "More than one archie.json is passed to the compiler; only /solution/archie.json is used.");
    }

    [Test]
    public async Task Test_projects_are_skipped_unless_the_config_says_otherwise()
    {
        var properties = new Dictionary<string, string> { ["IsTestProject"] = "true" };

        (await RunAsync(Source, Config, properties)).ShouldBeEmpty();
        (await RunAsync(Source, "{ \"checkTestProjects\": true, " + Config[1..], properties))
            .Select(d => d.Id).ShouldBe(["ARCH001", "ARCH002"], ignoreOrder: true);
    }

    [Test]
    public async Task A_test_project_does_not_repeat_config_errors() =>
        (await RunAsync(Source, "{ \"layers\": 1 }", new Dictionary<string, string> { ["IsTestProject"] = "true" })).ShouldBeEmpty();

    [Test]
    public async Task ArchieEnabled_false_turns_the_project_off() =>
        (await RunAsync(Source, Config, new Dictionary<string, string> { ["ArchieEnabled"] = "false" })).ShouldBeEmpty();

    [Test]
    public void Rules_are_errors_and_the_missing_config_is_info_by_default()
    {
        // Pinned: changing a default severity changes whether builds fail (plan §14). Update AnalyzerReleases.Unshipped.md and the README with it.
        Diagnostics.LayerDependency.DefaultSeverity.ShouldBe(DiagnosticSeverity.Error);
        Diagnostics.ForbiddenNamespace.DefaultSeverity.ShouldBe(DiagnosticSeverity.Error);
        Diagnostics.NoConfig.DefaultSeverity.ShouldBe(DiagnosticSeverity.Info);
        new ArchitectureAnalyzer().SupportedDiagnostics.Where(d => d.Id.StartsWith("ARCH90", StringComparison.Ordinal) && d.Id != "ARCH909")
            .ShouldAllBe(d => d.DefaultSeverity == DiagnosticSeverity.Error);
    }

    private static Task<System.Collections.Immutable.ImmutableArray<Diagnostic>> RunAsync(string source, string config,
        IReadOnlyDictionary<string, string>? properties = null) =>
        AnalyzerRunner.RunAsync([("/solution/src/Order.cs", source)], [("/solution/archie.json", config)], properties);

    private static string Key(Diagnostic d) => $"{d.Id} {d.Location.SourceSpan} {d.Properties["sourceId"]} {d.Properties["targetId"]}";
}
