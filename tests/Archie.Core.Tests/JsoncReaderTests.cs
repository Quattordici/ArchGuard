using Archie.Core.Json;

namespace Archie.Core.Tests;

public class JsoncReaderTests
{
    [Test]
    public void Reads_comments_and_trailing_commas()
    {
        var node = JsoncReader.Parse("""
            // line comment
            {
              /* block
                 comment */
              "layers": ["Api", "Domain",], // trailing comma in an array
              "flag": true,
            }
            """);

        var root = node.ShouldBeOfType<JsonObject>();
        root.Properties.Select(p => p.Name).ShouldBe(["layers", "flag"]);
        root.Properties[0].Value.ShouldBeOfType<JsonArray>().Items.Cast<JsonScalar>().Select(i => i.Value).ShouldBe(["Api", "Domain"]);
        root.Properties[1].Value.Kind.ShouldBe(JsonKind.True);
    }

    [Test]
    public void Keeps_the_span_of_every_key_and_value()
    {
        const string text = "{ \"key\": \"value\" }";
        var property = JsoncReader.Parse(text).ShouldBeOfType<JsonObject>().Properties[0];

        text.Substring(property.NameStart, property.NameLength).ShouldBe("\"key\"");
        text.Substring(property.Value.Start, property.Value.Length).ShouldBe("\"value\"");
    }

    [Test]
    public void Unescapes_strings()
    {
        var value = JsoncReader.Parse("""["a\"b\\c\/d\n\u00e9"]""").ShouldBeOfType<JsonArray>().Items[0].ShouldBeOfType<JsonScalar>();
        value.Value.ShouldBe("a\"b\\c/d\né");
    }

    [Test]
    public void Skips_a_byte_order_mark() => JsoncReader.Parse("\uFEFF{}").ShouldBeOfType<JsonObject>();

    [Test]
    public void Reads_numbers_and_literals()
    {
        var items = JsoncReader.Parse("[-1.5e3, 0, null, false]").ShouldBeOfType<JsonArray>().Items;
        items.Select(i => i.Kind).ShouldBe([JsonKind.Number, JsonKind.Number, JsonKind.Null, JsonKind.False]);
        ((JsonScalar)items[0]).Value.ShouldBe("-1.5e3");
    }

    [Test]
    [Arguments("", 0, "archie.json is empty. It needs at least { \"layers\": [...] }.")]
    [Arguments("{ \"a\": 1 \"b\": 2 }", 9, "',' or '}' was expected.")]
    [Arguments("{ \"a\": 1, \"a\": 2 }", 10, "Duplicate key 'a'.")]
    [Arguments("{ \"a\": \"open }", 7, "The string is not closed with '\"'.")]
    [Arguments("{ 'a': 1 }", 2, "Keys use double quotes in JSON.")]
    [Arguments("{ \"a\" 1 }", 6, "':' was expected after the key.")]
    [Arguments("[1, 2", 0, "Unexpected end of file; ']' was expected.")]
    [Arguments("{} {}", 3, "Unexpected text after the end of the JSON value.")]
    [Arguments("/* open", 0, "The comment is not closed with '*/'.")]
    [Arguments("[tru]", 1, "Unexpected character 't'; a value was expected.")]
    [Arguments("[\"\\x\"]", 2, "Unknown escape '\\x'.")]
    public void Reports_the_first_syntax_error_with_its_position(string text, int start, string message)
    {
        var error = Should.Throw<JsonSyntaxException>(() => JsoncReader.Parse(text));

        error.Message.ShouldBe(message);
        error.Start.ShouldBe(start);
    }

    [Test]
    public void Rejects_deep_nesting_instead_of_overflowing_the_stack()
    {
        var text = new string('[', 10_000) + new string(']', 10_000);

        Should.Throw<JsonSyntaxException>(() => JsoncReader.Parse(text)).Message.ShouldBe("Nested more than 64 levels deep.");
    }
}
