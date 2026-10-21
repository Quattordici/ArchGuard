using System.Collections.Generic;
using Archie.Core.Conventions;

namespace Archie.Core.Rules;

/// <summary>The rules of one archie.json, immutable. Both config forms (ordered list and named map) produce the same model.</summary>
public sealed class ArchModel
{
    private readonly bool[] _allowed;

    internal ArchModel(string? rootNamespace, IReadOnlyList<Layer> layers, IReadOnlyList<NamespacePattern> exclude, bool checkTestProjects,
        IReadOnlyList<Convention> conventions)
    {
        Conventions = conventions;
        RootNamespace = rootNamespace;
        Layers = layers;
        Exclude = exclude;
        CheckTestProjects = checkTestProjects;

        var count = layers.Count;
        _allowed = new bool[count * count];
        foreach (var layer in layers)
        {
            _allowed[(layer.Index * count) + layer.Index] = true;
            foreach (var target in layer.MayDependOn)
            {
                _allowed[(layer.Index * count) + target] = true;
            }
        }
    }

    /// <summary><c>rootNamespace</c>, which the names in the ordered-list form are relative to; <see langword="null"/> when not set.</summary>
    public string? RootNamespace { get; }

    /// <summary>The layers in config order.</summary>
    public IReadOnlyList<Layer> Layers { get; }

    /// <summary>Namespaces not checked as the source of a dependency. They still count as targets.</summary>
    public IReadOnlyList<NamespacePattern> Exclude { get; }

    /// <summary><c>checkTestProjects</c>: test projects (<c>IsTestProject</c>) are skipped unless this is set.</summary>
    public bool CheckTestProjects { get; }

    /// <summary>The conventions (naming, placement, inheritance, attributes, kind and modifiers) in config order. May be empty.</summary>
    public IReadOnlyList<Convention> Conventions { get; }

    /// <summary>Whether code in layer <paramref name="from"/> may depend on layer <paramref name="to"/>; a layer may always depend on itself.</summary>
    public bool MayDependOn(int from, int to) => _allowed[(from * Layers.Count) + to];
}
