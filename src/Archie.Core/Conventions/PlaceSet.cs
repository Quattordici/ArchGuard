using System.Collections.Generic;
using Archie.Core.Rules;

namespace Archie.Core.Conventions;

/// <summary>
/// Where a type is, as named in <c>in</c>, <c>mustBeIn</c> and <c>mustNotBeIn</c>: layer names and namespace patterns. A bare name is a layer;
/// anything with a dot or a wildcard is a namespace, so the two can never be confused.
/// </summary>
public sealed class PlaceSet
{
    internal PlaceSet(IReadOnlyList<string> texts, IReadOnlyList<int> layers, IReadOnlyList<NamespacePattern> namespaces)
    {
        Texts = texts;
        Layers = layers;
        Namespaces = namespaces;
        Description = Join(texts, " or ", quoteNamespaces: true);
    }

    /// <summary>The values as written, in config order.</summary>
    public IReadOnlyList<string> Texts { get; }

    /// <summary>Indexes of the layers named.</summary>
    public IReadOnlyList<int> Layers { get; }

    /// <summary>The namespace patterns named.</summary>
    public IReadOnlyList<NamespacePattern> Namespaces { get; }

    /// <summary>For messages: <c>Application or 'MyApp.Shared'</c>.</summary>
    public string Description { get; }

    /// <summary>Whether a type in <paramref name="ns"/> is in one of the places.</summary>
    public bool Contains(NamespaceInfo ns)
    {
        foreach (var layer in Layers)
        {
            if (ns.Layer == layer)
            {
                return true;
            }
        }

        foreach (var pattern in Namespaces)
        {
            if (pattern.Matches(ns.Segments))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsLayerName(string text) => Identifiers.IsIdentifier(text);

    internal static string Join(IReadOnlyList<string> texts, string separator, bool quoteNamespaces)
    {
        var result = string.Empty;
        foreach (var text in texts)
        {
            var shown = quoteNamespaces && !IsLayerName(text) ? $"'{text}'" : text;
            result = result.Length == 0 ? shown : result + separator + shown;
        }

        return result;
    }
}
