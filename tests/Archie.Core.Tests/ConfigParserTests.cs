using Archie.Core.Config;
using Archie.Core.Rules;

namespace Archie.Core.Tests;

public class ConfigParserTests
{
    [Test]
    public void Ordered_list_each_layer_may_depend_on_the_layers_after_it()
    {
        var model = Valid("""{ "rootNamespace": "MyApp", "layers": ["Api", "Infrastructure", "Application", "Domain"] }""");

        model.Layers.Select(l => l.Name).ShouldBe(["Api", "Infrastructure", "Application", "Domain"]);
        model.Layers[0].Namespaces.Single().Text.ShouldBe("MyApp.Api");
        Allowed(model, "Api").ShouldBe(["Infrastructure", "Application", "Domain"]);
        Allowed(model, "Application").ShouldBe(["Domain"]);
        Allowed(model, "Domain").ShouldBeEmpty();
        model.MayDependOn(3, 3).ShouldBeTrue();
        model.MayDependOn(3, 0).ShouldBeFalse();
    }

    [Test]
    public void Ordered_list_items_can_be_objects_with_their_own_namespaces()
    {
        var model = Valid("""
            {
              "rootNamespace": "Contoso.Shop",
              "layers": [
                "Web",
                { "name": "Infrastructure", "namespaces": ["Contoso.Shop.Infrastructure.**", "Contoso.Shop.Persistence.**"] },
                { "name": "Domain", "mustNotDependOn": "Microsoft.EntityFrameworkCore" },
              ],
            }
            """);

        model.Layers[1].Namespaces.Select(n => n.Text).ShouldBe(["Contoso.Shop.Infrastructure.**", "Contoso.Shop.Persistence.**"]);
        model.Layers[2].Namespaces.Single().Text.ShouldBe("Contoso.Shop.Domain");
        model.Layers[2].MustNotDependOn.Single().Text.ShouldBe("Microsoft.EntityFrameworkCore");
    }

    [Test]
    public void Named_map_missing_mayDependOn_means_nothing()
    {
        var model = Valid("""
            {
              "layers": {
                "Domain":         "MyApp.Domain.**",
                "Application":    { "namespaces": "MyApp.Application.**", "mayDependOn": ["Domain"] },
                "Infrastructure": { "namespaces": ["MyApp.Infrastructure"], "mayDependOn": ["Application", "Domain"] },
              }
            }
            """);

        Allowed(model, "Domain").ShouldBeEmpty();
        Allowed(model, "Application").ShouldBe(["Domain"]);
        Allowed(model, "Infrastructure").ShouldBe(["Application", "Domain"]);
        model.RootNamespace.ShouldBeNull();
    }

    [Test]
    public void Mutual_mayDependOn_is_allowed()
    {
        var model = Valid("""{ "layers": { "A": { "namespaces": "X.A", "mayDependOn": "B" }, "B": { "namespaces": "X.B", "mayDependOn": "A" } } }""");

        model.MayDependOn(0, 1).ShouldBeTrue();
        model.MayDependOn(1, 0).ShouldBeTrue();
    }

    [Test]
    public void MustNotDependOn_at_top_level_and_in_the_layer_are_merged()
    {
        var model = Valid("""
            {
              "layers": { "Domain": { "namespaces": "MyApp.Domain", "mustNotDependOn": ["System.Net.Http"] } },
              "mustNotDependOn": { "Domain": ["Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore.**"] },
            }
            """);

        model.Layers[0].MustNotDependOn.Select(p => p.Text).ShouldBe(["System.Net.Http", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore.**"]);
    }

    [Test]
    public void Reads_exclude_checkTestProjects_and_schema()
    {
        var model = Valid("""
            {
              "$schema": "https://example.com/v1.json",
              "rootNamespace": "MyApp",
              "layers": ["Domain"],
              "exclude": ["MyApp.Domain.Legacy", "MyApp.*.Generated"],
              "checkTestProjects": true,
            }
            """);

        model.Exclude.Select(p => p.Text).ShouldBe(["MyApp.Domain.Legacy", "MyApp.*.Generated"]);
        model.CheckTestProjects.ShouldBeTrue();
    }

    [Test]
    public void Test_projects_are_not_checked_by_default() =>
        Valid("""{ "rootNamespace": "MyApp", "layers": ["Domain"] }""").CheckTestProjects.ShouldBeFalse();

    [Test]
    public void RootNamespace_may_follow_the_layers() =>
        Valid("""{ "layers": ["Domain"], "rootNamespace": "MyApp" }""").Layers[0].Namespaces[0].Text.ShouldBe("MyApp.Domain");

    [Test]
    public void Never_throws_and_reports_line_and_column()
    {
        var result = ConfigParser.Parse("{\n  \"rootNamespace\": \"MyApp\",\n  \"layer\": []\n}");

        result.Model.ShouldBeNull();
        result.Errors.Select(e => e.ToString()).ShouldBe([
            "(3,3): ARCH901: Unknown key 'layer' at the top level. Did you mean 'layers'?",
            "(1,1): ARCH902: archie.json defines no layers. Add, for example, \"rootNamespace\": \"MyApp\" and \"layers\": [\"Api\", \"Infrastructure\", \"Application\", \"Domain\"].",
        ]);
        result.Errors[0].Start.ShouldBe(32);
        result.Errors[0].Length.ShouldBe("\"layer\"".Length);
    }

    /// <summary>Every ARCH9xx message, pinned so a change of wording is deliberate. The marked text is where the error is reported.</summary>
    [Test]
    [Arguments("«{» \"rootNamespace\": \"MyApp\", \"layers\": [\"Domain\"] ", "ARCH900: Unexpected end of file; '}' was expected.")]
    [Arguments("«[\"Domain\"]»", "ARCH900: archie.json must contain an object, like { \"layers\": [...] }, not an array.")]
    [Arguments("{ \"rootNamespace\": \"MyApp\", \"layers\": [\"Domain\"], «\"layers\"»: [] }", "ARCH900: Duplicate key 'layers'.")]
    [Arguments("{ \"rootNamespace\": \"MyApp\", \"layers\": [\"Domain\"], «\"exlude\"»: [] }", "ARCH901: Unknown key 'exlude' at the top level. Did you mean 'exclude'?")]
    [Arguments("{ \"rootNamespace\": \"MyApp\", \"layers\": [\"Domain\"], «\"conventions\"»: [] }", "ARCH901: 'conventions' is not supported until Archie 1.x. Remove it; v1 checks layer dependencies only.")]
    [Arguments("{ \"layers\": { \"Domain\": { \"namespaces\": \"A\", «\"mayDependsOn\"»: [] } } }", "ARCH901: Unknown key 'mayDependsOn' in layers.Domain. Did you mean 'mayDependOn'?")]
    [Arguments("{ \"layers\": { \"Domain\": { \"namespaces\": \"A\", «\"name\"»: \"Domain\" } } }", "ARCH901: Unknown key 'name' in layers.Domain: in an object of named layers, the key is the layer's name.")]
    [Arguments("{ \"rootNamespace\": \"A\", \"layers\": [{ \"name\": \"Domain\", «\"mayDependOn\"»: [] }] }", "ARCH901: 'mayDependOn' only works when layers is an object of named layers. In a list, each layer may depend on the layers after it.")]
    [Arguments("{ \"rootNamespace\": \"A\", \"layers\": [{ \"name\": \"Domain\", «\"colour\"»: 1 }] }", "ARCH901: Unknown key 'colour' in layers[0]. Known keys: name, namespaces, mustNotDependOn.")]
    [Arguments("«{» }", "ARCH902: archie.json defines no layers. Add, for example, \"rootNamespace\": \"MyApp\" and \"layers\": [\"Api\", \"Infrastructure\", \"Application\", \"Domain\"].")]
    [Arguments("{ \"layers\": «[]» }", "ARCH902: 'layers' is empty. Name at least one layer.")]
    [Arguments("{ \"layers\": «42» }", "ARCH902: 'layers' must be a list of layer names (each may depend on the ones after it) or an object of named layers, not a number.")]
    [Arguments("{ \"layers\": [«\"Domain\"»] }", "ARCH902: Layer 'Domain' needs \"rootNamespace\" to know its namespace. Add \"rootNamespace\": \"MyApp\", or write the layer as { \"name\": \"Domain\", \"namespaces\": \"MyApp.Domain\" }.")]
    [Arguments("{ \"rootNamespace\": \"A\", \"layers\": [\"Domain\", «\"Domain\"»] }", "ARCH902: Layer 'Domain' is defined twice.")]
    [Arguments("{ \"rootNamespace\": \"A\", \"layers\": [«\"My.Domain\"»] }", "ARCH902: 'My.Domain' is not a valid layer name: use letters, digits and '_', starting with a letter (no dots).")]
    [Arguments("{ \"rootNamespace\": «\"A.*\"», \"layers\": [\"Domain\"] }", "ARCH902: 'A.*' is not a valid rootNamespace: wildcards are not allowed here.")]
    [Arguments("{ \"rootNamespace\": \"A\", \"layers\": [«{» \"namespaces\": \"A.B\" }] }", "ARCH902: layers[0] has no \"name\".")]
    [Arguments("{ \"layers\": { «\"Domain\"»: { \"mayDependOn\": [] } } }", "ARCH902: layers.Domain has no \"namespaces\", e.g. \"namespaces\": \"MyApp.Domain\".")]
    [Arguments("{ \"layers\": { \"Domain\": «true» } }", "ARCH902: layers.Domain must be a namespace, a list of namespaces, or an object with \"namespaces\", not a boolean.")]
    [Arguments("{ \"layers\": { \"Domain\": «\"A..B\"» } }", "ARCH902: 'A..B' in layers.Domain.namespaces is not a valid namespace pattern: it has an empty segment (two dots, or a dot at the start or end).")]
    [Arguments("{ \"layers\": { \"Domain\": \"A.Domain\", \"App\": { \"namespaces\": \"A.App\", \"mayDependOn\": [«\"Domian\"»] } } }", "ARCH902: Unknown layer 'Domian' in layers.App.mayDependOn. Did you mean 'Domain'?")]
    [Arguments("{ \"layers\": { \"Domain\": \"A.Domain\", \"App\": { \"namespaces\": \"A.App\", \"mayDependOn\": [«\"Web\"»] } } }", "ARCH902: Unknown layer 'Web' in layers.App.mayDependOn. Layers: Domain, App.")]
    [Arguments("{ \"rootNamespace\": \"A\", \"layers\": [\"Domain\"], \"mustNotDependOn\": { «\"Domian\"»: [\"X\"] } }", "ARCH902: Unknown layer 'Domian' in mustNotDependOn. Did you mean 'Domain'?")]
    [Arguments("{ \"rootNamespace\": \"A\", \"layers\": [\"Domain\"], \"mustNotDependOn\": «[\"X\"]» }", "ARCH902: 'mustNotDependOn' must be an object of layer names and namespaces, like { \"Domain\": [\"Microsoft.EntityFrameworkCore\"] }, not an array.")]
    [Arguments("{ \"rootNamespace\": \"A\", \"layers\": [\"Domain\"], \"exclude\": [«1»] }", "ARCH902: exclude must contain strings, not a number.")]
    [Arguments("{ \"rootNamespace\": \"A\", \"layers\": [\"Domain\"], \"checkTestProjects\": «\"yes\"» }", "ARCH902: checkTestProjects must be true or false, not a string.")]
    [Arguments("{ \"layers\": { \"Orders\": \"A.*.Orders\", \"Domain\": «\"A.Domain.*\"» } }", "ARCH902: 'A.Domain.*' (layer Domain) and 'A.*.Orders' (layer Orders) can match the same namespace equally well, so it is unclear which layer it belongs to. Make one of them more specific.")]
    public void Reports_each_config_error_at_its_location(string marked, string expected)
    {
        var start = marked.IndexOf('«', StringComparison.Ordinal);
        var end = marked.IndexOf('»', StringComparison.Ordinal) - 1;
        var text = marked.Replace("«", "", StringComparison.Ordinal).Replace("»", "", StringComparison.Ordinal);

        var result = ConfigParser.Parse(text);

        result.Model.ShouldBeNull();
        var error = result.Errors.ShouldHaveSingleItem();
        $"{error.Code}: {error.Message}".ShouldBe(expected);
        text.Substring(error.Start, error.Length).ShouldBe(text.Substring(start, end - start));
    }

    [Test]
    public void Equal_specificity_in_the_same_layer_is_not_ambiguous() =>
        Valid("""{ "layers": { "Domain": ["A.*.Orders", "A.Domain.*"] } }""").Layers.ShouldHaveSingleItem();

    [Test]
    public void A_more_specific_pattern_in_another_layer_is_not_ambiguous() =>
        Valid("""{ "layers": { "Domain": "A.Domain", "Legacy": "A.Domain.Legacy" } }""").Layers.Count.ShouldBe(2);

    private static ArchModel Valid(string text)
    {
        var result = ConfigParser.Parse(text);
        result.Errors.ShouldBeEmpty();
        return result.Model.ShouldNotBeNull();
    }

    private static IEnumerable<string> Allowed(ArchModel model, string layer) =>
        model.Layers.Single(l => l.Name == layer).MayDependOn.Select(i => model.Layers[i].Name);
}
