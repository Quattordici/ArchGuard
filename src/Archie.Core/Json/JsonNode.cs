using System.Collections.Generic;

namespace Archie.Core.Json;

internal enum JsonKind
{
    Object,
    Array,
    String,
    Number,
    True,
    False,
    Null,
}

/// <summary>A JSON value with the span of its text, so config errors can point at it.</summary>
internal abstract class JsonNode(JsonKind kind, int start, int length)
{
    public JsonKind Kind { get; } = kind;

    public int Start { get; } = start;

    public int Length { get; } = length;

    /// <summary>How a value of this kind is named in messages: "a string", "an array".</summary>
    public string Describe() => Kind switch
    {
        JsonKind.Object => "an object",
        JsonKind.Array => "an array",
        JsonKind.String => "a string",
        JsonKind.Number => "a number",
        JsonKind.True or JsonKind.False => "a boolean",
        _ => "null",
    };
}

internal sealed class JsonObject(int start, int length, IReadOnlyList<JsonProperty> properties) : JsonNode(JsonKind.Object, start, length)
{
    /// <summary>In the order they appear in the file. Keys are unique: the reader rejects duplicates.</summary>
    public IReadOnlyList<JsonProperty> Properties { get; } = properties;
}

internal sealed class JsonProperty(string name, int nameStart, int nameLength, JsonNode value)
{
    public string Name { get; } = name;

    /// <summary>Start of the key including its quotes.</summary>
    public int NameStart { get; } = nameStart;

    public int NameLength { get; } = nameLength;

    public JsonNode Value { get; } = value;
}

internal sealed class JsonArray(int start, int length, IReadOnlyList<JsonNode> items) : JsonNode(JsonKind.Array, start, length)
{
    public IReadOnlyList<JsonNode> Items { get; } = items;
}

/// <summary>A string, number or literal. <see cref="Value"/> is the unescaped string, or the literal text for the others.</summary>
internal sealed class JsonScalar(JsonKind kind, int start, int length, string value) : JsonNode(kind, start, length)
{
    public string Value { get; } = value;
}
