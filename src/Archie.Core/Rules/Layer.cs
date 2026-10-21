using System.Collections.Generic;

namespace Archie.Core.Rules;

/// <summary>A named group of namespaces, with what it may and must not depend on.</summary>
public sealed class Layer
{
    internal Layer(int index, string name, IReadOnlyList<NamespacePattern> namespaces, IReadOnlyList<int> mayDependOn,
        IReadOnlyList<NamespacePattern> mustNotDependOn)
    {
        Index = index;
        Name = name;
        Namespaces = namespaces;
        MayDependOn = mayDependOn;
        MustNotDependOn = mustNotDependOn;
    }

    /// <summary>Position in <see cref="ArchModel.Layers"/>.</summary>
    public int Index { get; }

    /// <summary>The name used in archie.json and in messages.</summary>
    public string Name { get; }

    /// <summary>The namespaces that belong to the layer.</summary>
    public IReadOnlyList<NamespacePattern> Namespaces { get; }

    /// <summary>Indexes of the other layers this one may depend on, in config order. Its own layer is always allowed and not listed.</summary>
    public IReadOnlyList<int> MayDependOn { get; }

    /// <summary>Outside namespaces this layer must not use (top-level and in-layer entries merged).</summary>
    public IReadOnlyList<NamespacePattern> MustNotDependOn { get; }

    /// <inheritdoc />
    public override string ToString() => Name;
}
