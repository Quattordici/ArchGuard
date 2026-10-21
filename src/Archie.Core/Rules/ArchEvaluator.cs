using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Archie.Core.Rules;

/// <summary>
/// Decides whether a dependency is allowed. All rule logic (layers, <c>exclude</c>, <c>mustNotDependOn</c>) lives here, so every front-end
/// gives the same answer. Thread-safe.
/// </summary>
public sealed class ArchEvaluator
{
    private readonly ConcurrentDictionary<string, NamespaceInfo> _namespaces = new(StringComparer.Ordinal);
    private readonly Func<string, NamespaceInfo> _classify;
    private readonly string[] _allowedLists;

    /// <summary>Creates an evaluator for <paramref name="model"/>.</summary>
    public ArchEvaluator(ArchModel model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _classify = ClassifyUncached;
        _allowedLists = new string[model.Layers.Count];
        for (var i = 0; i < _allowedLists.Length; i++)
        {
            _allowedLists[i] = BuildAllowedList(model.Layers[i]);
        }
    }

    /// <summary>The rules being applied.</summary>
    public ArchModel Model { get; }

    /// <summary>Checks one dependency. The same as <c>Check(Classify(edge.SourceNamespace), Classify(edge.TargetNamespace))</c>.</summary>
    public Verdict Check(in DependencyEdge edge) => Check(Classify(edge.SourceNamespace), Classify(edge.TargetNamespace));

    /// <summary>
    /// Checks a dependency between two classified namespaces. Front-ends that cache <see cref="NamespaceInfo"/> per namespace symbol use this
    /// on the hot path and build the <see cref="DependencyEdge"/> (whose IDs cost allocations) only for violations.
    /// </summary>
    public Verdict Check(NamespaceInfo source, NamespaceInfo target)
    {
        if (!source.IsCheckedSource)
        {
            return Verdict.None;
        }

        if (target.Layer >= 0 && !Model.MayDependOn(source.Layer, target.Layer))
        {
            return new Verdict(VerdictKind.LayerNotAllowed, source.Layer, target.Layer);
        }

        return target.IsForbiddenFor(source.Layer) ? new Verdict(VerdictKind.ForbiddenNamespace, source.Layer, target.Layer) : Verdict.None;
    }

    /// <summary>Classifies a namespace (<c>""</c> is the global namespace). Cached.</summary>
    public NamespaceInfo Classify(string @namespace) => _namespaces.GetOrAdd(@namespace ?? throw new ArgumentNullException(nameof(@namespace)), _classify);

    /// <summary>The layers <paramref name="layer"/> may depend on, for messages: "Application, Domain", or "nothing".</summary>
    public string DescribeAllowed(int layer) => _allowedLists[layer];

    private NamespaceInfo ClassifyUncached(string @namespace)
    {
        var segments = @namespace.Length == 0 ? Array.Empty<string>() : @namespace.Split('.');

        // Most specific pattern wins. Config validation (ARCH902) rules out ties between layers, so the first best match is the only one.
        var layer = -1;
        var best = -1;
        foreach (var candidate in Model.Layers)
        {
            foreach (var pattern in candidate.Namespaces)
            {
                if (pattern.Specificity > best && pattern.Matches(segments))
                {
                    layer = candidate.Index;
                    best = pattern.Specificity;
                }
            }
        }

        bool[]? forbiddenFor = null;
        foreach (var candidate in Model.Layers)
        {
            if (AnyMatches(candidate.MustNotDependOn, segments))
            {
                forbiddenFor ??= new bool[Model.Layers.Count];
                forbiddenFor[candidate.Index] = true;
            }
        }

        return new NamespaceInfo(@namespace, segments, layer, AnyMatches(Model.Exclude, segments), forbiddenFor);
    }

    private static bool AnyMatches(IReadOnlyList<NamespacePattern> patterns, string[] segments)
    {
        foreach (var pattern in patterns)
        {
            if (pattern.Matches(segments))
            {
                return true;
            }
        }

        return false;
    }

    private string BuildAllowedList(Layer layer)
    {
        if (layer.MayDependOn.Count == 0)
        {
            return "nothing";
        }

        var builder = new StringBuilder();
        foreach (var index in layer.MayDependOn)
        {
            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(Model.Layers[index].Name);
        }

        return builder.ToString();
    }
}
