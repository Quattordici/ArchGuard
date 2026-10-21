using System.Text.Json;
using System.Text.RegularExpressions;
using Archie.Core.Config;
using Archie.Core.Conventions;
using Archie.Tests;

namespace Archie.Core.Tests;

/// <summary>
/// The schema (completion and inline docs in the editor) and the examples in the README and docs/diagnostics (the getting-started story)
/// stay in step with what <see cref="ConfigParser"/> accepts, so a renamed key can't leave either of them behind.
/// </summary>
public partial class DocumentationTests
{
    // Fragments like `"conventions": [...]` are read inside this config, which defines every layer the examples name.
    private const string Base = "\"rootNamespace\": \"MyApp\", \"layers\": [\"Api\", \"Web\", \"Infrastructure\", \"Application\", \"Domain\"]";

    private static readonly string[] Documents =
        [.. new[] { "README.md" }.Concat(Directory.GetFiles(Repository.PathOf("docs/diagnostics"), "*.md").Select(path => "docs/diagnostics/" + Path.GetFileName(path)))];

    [Test]
    public void The_schema_knows_exactly_the_keys_the_parser_knows()
    {
        using var schema = JsonDocument.Parse(Repository.Read("schema/v1.json"));
        var root = schema.RootElement;
        var definitions = root.GetProperty("definitions");
        var layers = root.GetProperty("properties").GetProperty("layers").GetProperty("oneOf");

        Keys(root).ShouldBe(ConfigParser.TopLevelKeys, ignoreOrder: true);
        Keys(layers[0].GetProperty("items").GetProperty("oneOf")[1]).ShouldBe(ConfigParser.ListItemKeys, ignoreOrder: true);
        Keys(layers[1].GetProperty("additionalProperties").GetProperty("oneOf")[1]).ShouldBe(ConfigParser.MapItemKeys, ignoreOrder: true);
        Keys(definitions.GetProperty("convention")).ShouldBe(ConfigParser.ConventionKeys, ignoreOrder: true);
        Keys(definitions.GetProperty("selector")).ShouldBe(ConfigParser.SelectorKeys, ignoreOrder: true);
        definitions.GetProperty("trait").GetProperty("enum").EnumerateArray().Select(e => e.GetString()!).ShouldBe(TypeTraitNames.Names);
    }

    [Test]
    public void The_schema_rejects_keys_it_does_not_know()
    {
        // additionalProperties: false is what turns a typo into a squiggle in the editor, like ARCH901 does in the build.
        using var schema = JsonDocument.Parse(Repository.Read("schema/v1.json"));
        var definitions = schema.RootElement.GetProperty("definitions");

        schema.RootElement.GetProperty("additionalProperties").GetBoolean().ShouldBeFalse();
        definitions.GetProperty("convention").GetProperty("additionalProperties").GetBoolean().ShouldBeFalse();
        definitions.GetProperty("selector").GetProperty("additionalProperties").GetBoolean().ShouldBeFalse();
    }

    public static IEnumerable<string> Examples() =>
        Documents.SelectMany(document => JsoncBlock().Matches(Repository.Read(document)).Select((match, i) => $"{document}#{i + 1}"));

    [Test]
    [MethodDataSource(nameof(Examples))]
    public void Every_config_example_is_valid(string example)
    {
        var document = example[..example.IndexOf('#')];
        var index = int.Parse(example[(example.IndexOf('#') + 1)..], System.Globalization.CultureInfo.InvariantCulture) - 1;
        var block = JsoncBlock().Matches(Repository.Read(document))[index].Groups["body"].Value;

        // A whole file starts with `{` (after comments); a fragment is one or more keys of one.
        var whole = Regex.Replace(block, @"^\s*(//[^\n]*\n\s*)*", "").StartsWith('{');
        var text = whole ? block
            : block.Contains("\"layers\"", StringComparison.Ordinal) ? $"{{ \"rootNamespace\": \"MyApp\", {block} }}"
            : $"{{ {Base}, {block} }}";

        ConfigParser.Parse(text).Errors.Select(e => $"{e.Code}: {e.Message}").ShouldBeEmpty(text);
    }

    [Test]
    public void The_documents_have_examples() => Examples().Count().ShouldBeGreaterThan(8);

    [Test]
    public void Every_archie_json_in_the_repository_is_valid()
    {
        var files = Directory.GetFiles(Repository.Root, "archie.json", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();

        files.Length.ShouldBeGreaterThan(3);
        foreach (var file in files)
        {
            ConfigParser.Parse(File.ReadAllText(file)).Errors.Select(e => $"{e.Code}: {e.Message}").ShouldBeEmpty(file);
        }
    }

    private static string[] Keys(JsonElement element) =>
        [.. element.GetProperty("properties").EnumerateObject().Select(p => p.Name)];

    [GeneratedRegex(@"```jsonc?\r?\n(?<body>.*?)```", RegexOptions.Singleline)]
    private static partial Regex JsoncBlock();
}
