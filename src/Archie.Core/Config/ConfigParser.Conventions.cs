using System.Collections.Generic;
using System.Linq;
using Archie.Core.Conventions;
using Archie.Core.Json;
using Archie.Core.Rules;

namespace Archie.Core.Config;

public static partial class ConfigParser
{
    internal static readonly string[] ConventionKeys =
    [
        "name", "types", "mustBeNamed", "mustNotBeNamed", "mustBeIn", "mustNotBeIn", "mustInherit", "mustNotInherit", "mustHaveAttribute",
        "mustNotHaveAttribute", "mustBe", "mustNotBe",
    ];

    internal static readonly string[] SelectorKeys = ["in", "named", "inherit", "haveAttribute", "are", "areNot"];

    private sealed partial class Reader
    {
        /// <summary>Reads <c>conventions</c> (design: phase2-conventions.md). Runs after the layers, which <c>in</c> and <c>mustBeIn</c> name.</summary>
        private List<Convention> ReadConventions(JsonNode node)
        {
            var result = new List<Convention>();
            if (node is not JsonArray list)
            {
                Error(InvalidValue,
                    $"'conventions' must be a list of conventions, like [{{ \"types\": {{ \"inherit\": \"IRequestHandler<,>\" }}, \"mustBeNamed\": \"*Handler\" }}], not {node.Describe()}.",
                    node);
                return result;
            }

            var names = new HashSet<string>();
            for (var i = 0; i < list.Items.Count; i++)
            {
                var path = $"conventions[{i}]";
                if (list.Items[i] is not JsonObject item)
                {
                    Error(InvalidValue, $"{path} must be an object with \"types\" and a requirement such as \"mustBeNamed\", not {list.Items[i].Describe()}.", list.Items[i]);
                    continue;
                }

                if (ReadConvention(item, i, path, names) is { } convention)
                {
                    result.Add(convention);
                }
            }

            return result;
        }

        private Convention? ReadConvention(JsonObject item, int index, string path, HashSet<string> names)
        {
            var errors = Errors.Count;
            string? name = null;
            TypeSelector? selector = null;
            var requirements = new List<Requirement>();
            foreach (var property in item.Properties)
            {
                var key = $"{path}.{property.Name}";
                switch (property.Name)
                {
                    case "name":
                        if (ExpectKind(property.Value, key, "a string", JsonKind.String))
                        {
                            name = ((JsonScalar)property.Value).Value;
                            if (name.Trim().Length == 0)
                            {
                                Error(InvalidValue, $"{key} is empty.", property.Value);
                            }
                            else if (!names.Add(name))
                            {
                                Error(InvalidValue, $"Convention '{name}' is defined twice; give each convention its own name.", property.Value);
                            }
                        }

                        break;
                    case "types":
                        selector = ReadSelector(property.Value, key);
                        break;
                    case "mustBeNamed" or "mustNotBeNamed":
                        if (NamePatterns(property.Value, key) is { } patterns)
                        {
                            requirements.Add(new NameRequirement(property.Name == "mustNotBeNamed", patterns));
                        }

                        break;
                    case "mustBeIn" or "mustNotBeIn":
                        if (Places(property.Value, key) is { } places)
                        {
                            requirements.Add(new PlaceRequirement(property.Name == "mustNotBeIn", places, _layers.Select(l => l.Name).ToArray()));
                        }

                        break;
                    case "mustInherit" or "mustNotInherit" or "mustHaveAttribute" or "mustNotHaveAttribute":
                        var attributes = property.Name.EndsWith("Attribute", System.StringComparison.Ordinal);
                        if (Types(property.Value, key, attributes) is { } types)
                        {
                            requirements.Add(new TypeListRequirement(attributes, property.Name.StartsWith("mustNot", System.StringComparison.Ordinal), types,
                                types.Select(t => t.Text).ToArray()));
                        }

                        break;
                    case "mustBe" or "mustNotBe":
                        if (Traits(property.Value, key, out var texts) is var traits && traits != TypeTraits.None)
                        {
                            requirements.Add(new TraitRequirement(property.Name == "mustNotBe", traits, texts));
                        }

                        break;
                    case "mustImplement" or "mustNotImplement":
                        Error(UnknownKey,
                            $"Unknown key '{property.Name}' in {path}. Use '{property.Name.Replace("Implement", "Inherit")}': it covers base classes and interfaces.",
                            property);
                        break;
                    default:
                        UnknownKeyError(property, $"in {path}", ConventionKeys);
                        break;
                }
            }

            if (requirements.Count == 0 && Errors.Count == errors)
            {
                Error(InvalidValue,
                    $"{path} has no requirement, so it checks nothing. Add one of mustBeNamed, mustBeIn, mustInherit, mustHaveAttribute, mustBe (or their mustNot… forms).",
                    item.Start, 1);
            }

            return Errors.Count == errors
                ? new Convention(index, name, selector ?? new TypeSelector(null, null, TypeTraits.None, TypeTraits.None, null, null), requirements)
                : null;
        }

        private TypeSelector? ReadSelector(JsonNode node, string path)
        {
            if (node is not JsonObject selector)
            {
                Error(InvalidValue,
                    $"{path} must be an object that says which types the convention applies to, like {{ \"in\": \"Domain\", \"named\": \"*Repository\" }}, not {node.Describe()}.",
                    node);
                return null;
            }

            PlaceSet? @in = null;
            NamePattern[]? named = null;
            TypeReference[]? inherit = null;
            TypeReference[]? haveAttribute = null;
            var are = TypeTraits.None;
            var areNot = TypeTraits.None;
            foreach (var property in selector.Properties)
            {
                var key = $"{path}.{property.Name}";
                switch (property.Name)
                {
                    case "in":
                        @in = Places(property.Value, key);
                        break;
                    case "named":
                        named = NamePatterns(property.Value, key);
                        break;
                    case "inherit":
                        inherit = Types(property.Value, key, attributes: false);
                        break;
                    case "haveAttribute":
                        haveAttribute = Types(property.Value, key, attributes: true);
                        break;
                    case "are":
                        are = Traits(property.Value, key, out _);
                        if (CountKinds(are) > 1)
                        {
                            Error(InvalidValue,
                                $"{key} lists traits that must all hold, and a type cannot be {TypeTraitNames.Describe(are & TypeTraitNames.Kinds, " and ")} at once. Use one convention per kind.",
                                property.Value);
                        }

                        break;
                    case "areNot":
                        areNot = Traits(property.Value, key, out _);
                        break;
                    case "implement":
                        Error(UnknownKey, $"Unknown key 'implement' in {path}. Use 'inherit': it covers base classes and interfaces.", property);
                        break;
                    default:
                        UnknownKeyError(property, $"in {path}", SelectorKeys);
                        break;
                }
            }

            if ((are & areNot) != 0)
            {
                Error(InvalidValue, $"{path} has '{TypeTraitNames.Describe(are & areNot, "', '")}' in both 'are' and 'areNot', so it selects nothing.", node);
            }

            return new TypeSelector(@in, named, are, areNot, inherit, haveAttribute);
        }

        private NamePattern[]? NamePatterns(JsonNode node, string path)
        {
            var result = new List<NamePattern>();
            foreach (var item in NonEmptyStringList(node, path))
            {
                var pattern = NamePattern.TryParse(item.Value, out var error);
                if (pattern is null)
                {
                    Error(InvalidValue, $"'{item.Value}' in {path} is not a valid name pattern: {error}.", item);
                }
                else
                {
                    result.Add(pattern);
                }
            }

            return result.Count > 0 ? result.ToArray() : null;
        }

        private PlaceSet? Places(JsonNode node, string path)
        {
            var texts = new List<string>();
            var layers = new List<int>();
            var namespaces = new List<NamespacePattern>();
            foreach (var item in NonEmptyStringList(node, path))
            {
                if (PlaceSet.IsLayerName(item.Value))
                {
                    var index = _layers.FindIndex(l => l.Name == item.Value);
                    if (index < 0)
                    {
                        Error(InvalidValue,
                            $"Unknown layer '{item.Value}' in {path}.{SuggestLayer(item.Value)} For a namespace, write '{item.Value}.**'.",
                            item);
                        continue;
                    }

                    layers.Add(index);
                }
                else if (ParsePattern(item, path) is { } pattern)
                {
                    namespaces.Add(pattern);
                }
                else
                {
                    continue;
                }

                texts.Add(item.Value);
            }

            return texts.Count > 0 ? new PlaceSet(texts, layers, namespaces) : null;
        }

        private TypeReference[]? Types(JsonNode node, string path, bool attributes)
        {
            var result = new List<TypeReference>();
            foreach (var item in NonEmptyStringList(node, path))
            {
                var type = TypeReference.TryParse(item.Value, attributes, out var error);
                if (type is null)
                {
                    Error(InvalidValue, $"'{item.Value}' in {path} is not a valid type name: {error}.", item);
                }
                else
                {
                    result.Add(type);
                }
            }

            return result.Count > 0 ? result.ToArray() : null;
        }

        private TypeTraits Traits(JsonNode node, string path, out string[] texts)
        {
            var traits = TypeTraits.None;
            var written = new List<string>();
            foreach (var item in NonEmptyStringList(node, path))
            {
                var trait = TypeTraitNames.Parse(item.Value);
                if (trait == TypeTraits.None)
                {
                    var closest = Identifiers.Closest(item.Value, TypeTraitNames.Names);
                    var hint = closest is not null ? $" Did you mean '{closest}'?" : $" Use: {string.Join(", ", TypeTraitNames.Names)}.";
                    Error(InvalidValue, $"Unknown value '{item.Value}' in {path}.{hint}", item);
                    continue;
                }

                traits |= trait;
                written.Add(item.Value);
            }

            texts = written.ToArray();
            return traits;
        }

        private List<JsonScalar> NonEmptyStringList(JsonNode node, string path)
        {
            var items = StringList(node, path);
            if (items.Count == 0 && node is JsonArray)
            {
                Error(InvalidValue, $"{path} is empty.", node);
            }

            return items;
        }

        private static int CountKinds(TypeTraits traits)
        {
            var kinds = (int)(traits & TypeTraitNames.Kinds);
            var count = 0;
            while (kinds != 0)
            {
                kinds &= kinds - 1;
                count++;
            }

            return count;
        }
    }
}
