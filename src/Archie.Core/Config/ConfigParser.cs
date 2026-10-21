using System;
using System.Collections.Generic;
using System.Linq;
using Archie.Core.Json;
using Archie.Core.Rules;

namespace Archie.Core.Config;

/// <summary>Reads archie.json (JSON with comments) into an <see cref="ArchModel"/>. Design: v1-design §4.</summary>
public static class ConfigParser
{
    /// <summary>The file could not be read: JSON syntax, duplicate keys, not an object, more than one config file.</summary>
    public const string Unreadable = "ARCH900";

    /// <summary>A key Archie does not know, or does not support yet.</summary>
    public const string UnknownKey = "ARCH901";

    /// <summary>A value that is not valid: wrong type, bad namespace pattern, unknown or duplicate layer, two layers matching equally well.</summary>
    public const string InvalidValue = "ARCH902";

    private static readonly string[] TopLevelKeys = ["$schema", "rootNamespace", "layers", "mustNotDependOn", "exclude", "checkTestProjects"];
    private static readonly string[] ListItemKeys = ["name", "namespaces", "mustNotDependOn"];
    private static readonly string[] MapItemKeys = ["namespaces", "mayDependOn", "mustNotDependOn"];

    /// <summary>Parses <paramref name="text"/>. Never throws for any input.</summary>
    public static ConfigResult Parse(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        var reader = new Reader(text);
        var model = reader.Read();
        return new ConfigResult(reader.Errors.Count == 0 ? model : null, reader.Errors);
    }

    private sealed class LayerDraft(string name, JsonNode nameNode, string path)
    {
        public string Name { get; } = name;

        /// <summary>Where errors about the layer as a whole go: the name in a list, the key in a map.</summary>
        public JsonNode NameNode { get; } = nameNode;

        /// <summary>For messages: <c>layers.Domain</c>.</summary>
        public string Path { get; } = path;

        public List<(NamespacePattern Pattern, JsonNode Node)> Namespaces { get; } = [];

        public List<JsonScalar> MayDependOn { get; } = [];

        public List<NamespacePattern> MustNotDependOn { get; } = [];
    }

    private sealed class Reader(string text)
    {
        private readonly string _text = text;
        private readonly List<LayerDraft> _layers = [];
        private string? _rootNamespace;
        private bool _rootNamespaceInvalid;
        private bool _listForm;

        public List<ConfigError> Errors { get; } = [];

        public ArchModel? Read()
        {
            JsonNode root;
            try
            {
                root = JsoncReader.Parse(_text);
            }
            catch (JsonSyntaxException e)
            {
                Error(Unreadable, e.Message, e.Start, e.Length);
                return null;
            }

            if (root is not JsonObject top)
            {
                Error(Unreadable, $"archie.json must contain an object, like {{ \"layers\": [...] }}, not {root.Describe()}.", root);
                return null;
            }

            JsonProperty? layers = null;
            JsonProperty? mustNotDependOn = null;
            var exclude = new List<NamespacePattern>();
            var checkTestProjects = false;
            foreach (var property in top.Properties)
            {
                switch (property.Name)
                {
                    case "$schema":
                        ExpectKind(property.Value, "$schema", "a string", JsonKind.String);
                        break;
                    case "rootNamespace":
                        ReadRootNamespace(property.Value);
                        break;
                    case "layers":
                        layers = property;
                        break;
                    case "mustNotDependOn":
                        mustNotDependOn = property;
                        break;
                    case "exclude":
                        foreach (var item in StringList(property.Value, "exclude"))
                        {
                            AddPattern(exclude, item, "exclude");
                        }

                        break;
                    case "checkTestProjects":
                        if (ExpectKind(property.Value, "checkTestProjects", "true or false", JsonKind.True, JsonKind.False))
                        {
                            checkTestProjects = property.Value.Kind == JsonKind.True;
                        }

                        break;
                    case "conventions":
                        Error(UnknownKey, "'conventions' is not supported until Archie 1.x. Remove it; v1 checks layer dependencies only.", property);
                        break;
                    default:
                        UnknownKeyError(property, "at the top level", TopLevelKeys);
                        break;
                }
            }

            // The layers come first: rootNamespace may follow them in the file, and mustNotDependOn names them.
            if (layers is null)
            {
                Error(InvalidValue,
                    "archie.json defines no layers. Add, for example, \"rootNamespace\": \"MyApp\" and \"layers\": [\"Api\", \"Infrastructure\", \"Application\", \"Domain\"].",
                    top.Start, 1);
                return null;
            }

            ReadLayers(layers.Value);
            if (mustNotDependOn is not null)
            {
                ReadTopLevelMustNotDependOn(mustNotDependOn.Value);
            }

            var mayDependOn = ResolveMayDependOn();
            CheckAmbiguousNamespaces();
            if (Errors.Count > 0)
            {
                return null;
            }

            var built = new Layer[_layers.Count];
            for (var i = 0; i < built.Length; i++)
            {
                var draft = _layers[i];
                built[i] = new Layer(i, draft.Name, draft.Namespaces.Select(n => n.Pattern).ToArray(), mayDependOn[i], draft.MustNotDependOn.ToArray());
            }

            return new ArchModel(_rootNamespace, built, exclude.ToArray(), checkTestProjects);
        }

        private void ReadRootNamespace(JsonNode node)
        {
            if (!ExpectKind(node, "rootNamespace", "a string", JsonKind.String))
            {
                _rootNamespaceInvalid = true;
                return;
            }

            var value = ((JsonScalar)node).Value;
            if (NamespacePattern.TryParse(value, allowWildcards: false, out var error) is null)
            {
                Error(InvalidValue, $"'{value}' is not a valid rootNamespace: {error}.", node);
                _rootNamespaceInvalid = true;
                return;
            }

            _rootNamespace = value;
        }

        private void ReadLayers(JsonNode node)
        {
            switch (node)
            {
                case JsonArray list:
                    _listForm = true;
                    for (var i = 0; i < list.Items.Count; i++)
                    {
                        ReadListItem(list.Items[i], i);
                    }

                    break;
                case JsonObject map:
                    foreach (var property in map.Properties)
                    {
                        ReadMapItem(property);
                    }

                    break;
                default:
                    Error(InvalidValue,
                        $"'layers' must be a list of layer names (each may depend on the ones after it) or an object of named layers, not {node.Describe()}.",
                        node);
                    return;
            }

            if (_layers.Count == 0 && Errors.Count == 0)
            {
                Error(InvalidValue, "'layers' is empty. Name at least one layer.", node);
            }
        }

        private void ReadListItem(JsonNode item, int index)
        {
            var path = $"layers[{index}]";
            if (item is JsonScalar { Kind: JsonKind.String } name)
            {
                if (AddLayer(name.Value, name, path) is { } layer)
                {
                    AddDefaultNamespace(layer, name);
                }

                return;
            }

            if (item is not JsonObject definition)
            {
                Error(InvalidValue, $"{path} must be a layer name or an object with \"name\" and \"namespaces\", not {item.Describe()}.", item);
                return;
            }

            var nameProperty = definition.Properties.FirstOrDefault(p => p.Name == "name");
            if (nameProperty is null)
            {
                Error(InvalidValue, $"{path} has no \"name\".", definition.Start, 1);
                return;
            }

            if (!ExpectKind(nameProperty.Value, $"{path}.name", "a string", JsonKind.String))
            {
                return;
            }

            var draft = AddLayer(((JsonScalar)nameProperty.Value).Value, nameProperty.Value, path);
            var hasNamespaces = false;
            foreach (var property in definition.Properties)
            {
                switch (property.Name)
                {
                    case "name":
                        break;
                    case "namespaces":
                        hasNamespaces = true;
                        ReadNamespaces(draft, property.Value, path);
                        break;
                    case "mustNotDependOn":
                        ReadMustNotDependOn(draft, property.Value, $"{path}.mustNotDependOn");
                        break;
                    case "mayDependOn":
                        Error(UnknownKey,
                            $"'mayDependOn' only works when layers is an object of named layers. In a list, each layer may depend on the layers after it.",
                            property);
                        break;
                    default:
                        UnknownKeyError(property, $"in {path}", ListItemKeys);
                        break;
                }
            }

            if (!hasNamespaces && draft is not null)
            {
                AddDefaultNamespace(draft, nameProperty.Value);
            }
        }

        private void ReadMapItem(JsonProperty property)
        {
            var nameNode = new JsonScalar(JsonKind.String, property.NameStart, property.NameLength, property.Name);
            var path = $"layers.{property.Name}";
            var draft = AddLayer(property.Name, nameNode, path);
            switch (property.Value)
            {
                case JsonScalar { Kind: JsonKind.String }:
                case JsonArray:
                    ReadNamespaces(draft, property.Value, path);
                    return;
                case JsonObject definition:
                    var hasNamespaces = false;
                    foreach (var inner in definition.Properties)
                    {
                        switch (inner.Name)
                        {
                            case "namespaces":
                                hasNamespaces = true;
                                ReadNamespaces(draft, inner.Value, path);
                                break;
                            case "mayDependOn":
                                draft?.MayDependOn.AddRange(StringList(inner.Value, $"{path}.mayDependOn"));
                                break;
                            case "mustNotDependOn":
                                ReadMustNotDependOn(draft, inner.Value, $"{path}.mustNotDependOn");
                                break;
                            case "name":
                                Error(UnknownKey, $"Unknown key 'name' in {path}: in an object of named layers, the key is the layer's name.", inner);
                                break;
                            default:
                                UnknownKeyError(inner, $"in {path}", MapItemKeys);
                                break;
                        }
                    }

                    if (!hasNamespaces)
                    {
                        Error(InvalidValue, $"{path} has no \"namespaces\", e.g. \"namespaces\": \"MyApp.{property.Name}\".", nameNode);
                    }

                    return;
                default:
                    Error(InvalidValue,
                        $"{path} must be a namespace, a list of namespaces, or an object with \"namespaces\", not {property.Value.Describe()}.",
                        property.Value);
                    return;
            }
        }

        private LayerDraft? AddLayer(string name, JsonNode nameNode, string path)
        {
            if (!Identifiers.IsIdentifier(name))
            {
                Error(InvalidValue, $"'{name}' is not a valid layer name: use letters, digits and '_', starting with a letter (no dots).", nameNode);
                return null;
            }

            if (_layers.Any(l => l.Name == name))
            {
                Error(InvalidValue, $"Layer '{name}' is defined twice.", nameNode);
                return null;
            }

            var draft = new LayerDraft(name, nameNode, path);
            _layers.Add(draft);
            return draft;
        }

        private void AddDefaultNamespace(LayerDraft layer, JsonNode nameNode)
        {
            if (_rootNamespaceInvalid)
            {
                return; // already reported
            }

            if (_rootNamespace is null)
            {
                Error(InvalidValue,
                    $"Layer '{layer.Name}' needs \"rootNamespace\" to know its namespace. Add \"rootNamespace\": \"MyApp\", or write the layer as {{ \"name\": \"{layer.Name}\", \"namespaces\": \"MyApp.{layer.Name}\" }}.",
                    nameNode);
                return;
            }

            var text = _rootNamespace + "." + layer.Name;
            layer.Namespaces.Add((NamespacePattern.TryParse(text, out _)!, nameNode));
        }

        private void ReadNamespaces(LayerDraft? layer, JsonNode node, string path)
        {
            var items = StringList(node, $"{path}.namespaces");
            if (items.Count == 0 && node is JsonArray)
            {
                Error(InvalidValue, $"{path}.namespaces is empty.", node);
            }

            foreach (var item in items)
            {
                if (ParsePattern(item, $"{path}.namespaces") is { } pattern)
                {
                    layer?.Namespaces.Add((pattern, item));
                }
            }
        }

        private void ReadMustNotDependOn(LayerDraft? layer, JsonNode node, string path)
        {
            foreach (var item in StringList(node, path))
            {
                if (ParsePattern(item, path) is { } pattern)
                {
                    layer?.MustNotDependOn.Add(pattern);
                }
            }
        }

        private void ReadTopLevelMustNotDependOn(JsonNode node)
        {
            if (node is not JsonObject byLayer)
            {
                Error(InvalidValue,
                    $"'mustNotDependOn' must be an object of layer names and namespaces, like {{ \"Domain\": [\"Microsoft.EntityFrameworkCore\"] }}, not {node.Describe()}.",
                    node);
                return;
            }

            foreach (var property in byLayer.Properties)
            {
                var layer = _layers.FirstOrDefault(l => l.Name == property.Name);
                if (layer is null)
                {
                    Error(InvalidValue, $"Unknown layer '{property.Name}' in mustNotDependOn.{SuggestLayer(property.Name)}", property.NameStart, property.NameLength);
                }

                // Merged with the layer's own list (design §4.7), and still validated when the layer is unknown.
                ReadMustNotDependOn(layer, property.Value, $"mustNotDependOn.{property.Name}");
            }
        }

        private IReadOnlyList<int>[] ResolveMayDependOn()
        {
            var result = new IReadOnlyList<int>[_layers.Count];
            for (var i = 0; i < _layers.Count; i++)
            {
                var allowed = new List<int>();
                if (_listForm)
                {
                    for (var j = i + 1; j < _layers.Count; j++)
                    {
                        allowed.Add(j);
                    }
                }
                else
                {
                    foreach (var reference in _layers[i].MayDependOn)
                    {
                        var index = _layers.FindIndex(l => l.Name == reference.Value);
                        if (index < 0)
                        {
                            Error(InvalidValue, $"Unknown layer '{reference.Value}' in {_layers[i].Path}.mayDependOn.{SuggestLayer(reference.Value)}", reference);
                        }
                        else if (index != i && !allowed.Contains(index))
                        {
                            allowed.Add(index);
                        }
                    }
                }

                result[i] = allowed;
            }

            return result;
        }

        private void CheckAmbiguousNamespaces()
        {
            for (var b = 1; b < _layers.Count; b++)
            {
                foreach (var (pattern, node) in _layers[b].Namespaces)
                {
                    for (var a = 0; a < b; a++)
                    {
                        var clash = _layers[a].Namespaces.FirstOrDefault(n => n.Pattern.Specificity == pattern.Specificity && n.Pattern.Overlaps(pattern));
                        if (clash.Pattern is not null)
                        {
                            Error(InvalidValue,
                                $"'{pattern.Text}' (layer {_layers[b].Name}) and '{clash.Pattern.Text}' (layer {_layers[a].Name}) can match the same namespace equally well, so it is unclear which layer it belongs to. Make one of them more specific.",
                                node);
                            break;
                        }
                    }
                }
            }
        }

        private string SuggestLayer(string name)
        {
            var names = _layers.Select(l => l.Name).ToArray();
            var closest = Identifiers.Closest(name, names);
            return closest is not null ? $" Did you mean '{closest}'?" : $" Layers: {(names.Length == 0 ? "none" : string.Join(", ", names))}.";
        }

        private NamespacePattern? ParsePattern(JsonScalar item, string path)
        {
            var pattern = NamespacePattern.TryParse(item.Value, out var error);
            if (pattern is null)
            {
                Error(InvalidValue, $"'{item.Value}' in {path} is not a valid namespace pattern: {error}.", item);
            }

            return pattern;
        }

        private void AddPattern(List<NamespacePattern> patterns, JsonScalar item, string path)
        {
            if (ParsePattern(item, path) is { } pattern)
            {
                patterns.Add(pattern);
            }
        }

        /// <summary>A string or a list of strings; reports anything else and returns what was usable.</summary>
        private List<JsonScalar> StringList(JsonNode node, string path)
        {
            var result = new List<JsonScalar>();
            if (node is JsonScalar { Kind: JsonKind.String } single)
            {
                result.Add(single);
                return result;
            }

            if (node is not JsonArray array)
            {
                Error(InvalidValue, $"{path} must be a string or a list of strings, not {node.Describe()}.", node);
                return result;
            }

            foreach (var item in array.Items)
            {
                if (item is JsonScalar { Kind: JsonKind.String } text)
                {
                    result.Add(text);
                }
                else
                {
                    Error(InvalidValue, $"{path} must contain strings, not {item.Describe()}.", item);
                }
            }

            return result;
        }

        private bool ExpectKind(JsonNode node, string path, string expected, params JsonKind[] kinds)
        {
            if (Array.IndexOf(kinds, node.Kind) >= 0)
            {
                return true;
            }

            Error(InvalidValue, $"{path} must be {expected}, not {node.Describe()}.", node);
            return false;
        }

        private void UnknownKeyError(JsonProperty property, string where, string[] known)
        {
            var closest = Identifiers.Closest(property.Name, known);
            var hint = closest is not null ? $" Did you mean '{closest}'?" : $" Known keys: {string.Join(", ", known)}.";
            Error(UnknownKey, $"Unknown key '{property.Name}' {where}.{hint}", property);
        }

        private void Error(string code, string message, JsonProperty property) => Error(code, message, property.NameStart, property.NameLength);

        private void Error(string code, string message, JsonNode node) => Error(code, message, node.Start, node.Length);

        private void Error(string code, string message, int start, int length)
        {
            var line = 1;
            var lineStart = 0;
            for (var i = 0; i < start && i < _text.Length; i++)
            {
                if (_text[i] == '\n')
                {
                    line++;
                    lineStart = i + 1;
                }
            }

            Errors.Add(new ConfigError(code, message, start, length, line, start - lineStart + 1));
        }
    }
}
