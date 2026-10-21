namespace Archie.Core.Rules;

/// <summary>What the rules know about one namespace: its layer, whether it is excluded, and which layers must not use it.</summary>
public sealed class NamespaceInfo
{
    private readonly bool[]? _forbiddenFor;

    internal NamespaceInfo(string name, string[] segments, int layer, bool isExcluded, bool[]? forbiddenFor)
    {
        Name = name;
        Segments = segments;
        Layer = layer;
        IsExcluded = isExcluded;
        _forbiddenFor = forbiddenFor;
    }

    /// <summary>The namespace; empty for the global namespace.</summary>
    public string Name { get; }

    /// <summary>The namespace split at its dots; empty for the global namespace.</summary>
    internal string[] Segments { get; }

    /// <summary>Index of the layer the namespace belongs to, or -1.</summary>
    public int Layer { get; }

    /// <summary>Matched by <c>exclude</c>: code here is not checked as a source.</summary>
    public bool IsExcluded { get; }

    /// <summary>Whether code here is checked at all. When it is not, nothing it uses can be a violation, so front-ends can skip it early.</summary>
    public bool IsCheckedSource => Layer >= 0 && !IsExcluded;

    /// <summary>Whether <c>mustNotDependOn</c> of <paramref name="layer"/> names this namespace.</summary>
    public bool IsForbiddenFor(int layer) => _forbiddenFor is not null && _forbiddenFor[layer];
}
