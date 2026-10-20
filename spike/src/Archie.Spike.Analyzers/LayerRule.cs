using System;
using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;

namespace Archie.Spike.Analyzers;

/// <summary>
/// The hardcoded rule: code in a namespace with a segment from <c>From</c> must not use types
/// in a namespace with a segment from <c>To</c>. Stands in for the v1 LayerResolver: string work
/// runs once per namespace, every later lookup is a dictionary hit.
/// </summary>
internal sealed class LayerRule
{
    public const int None = 0;
    public const int Source = 1;
    public const int Target = 2;

    private readonly string[] _from;
    private readonly string[] _to;
    private readonly ConcurrentDictionary<INamespaceSymbol, int> _cache = new(SymbolEqualityComparer.Default);
    private readonly Func<INamespaceSymbol, int> _compute;

    public LayerRule(string[] from, string[] to)
    {
        _from = from;
        _to = to;
        _compute = Compute;
    }

    public string FromName => string.Join("|", _from);
    public string ToName => string.Join("|", _to);

    public int Resolve(INamespaceSymbol? ns) =>
        ns is null || ns.IsGlobalNamespace ? None : _cache.GetOrAdd(ns, _compute);

    /// <summary>Layer of the type that owns <paramref name="symbol"/> (the symbol itself for a type).</summary>
    public int ResolveOwner(ISymbol? symbol)
    {
        var type = symbol as INamedTypeSymbol ?? symbol?.ContainingType;
        return type is null ? None : Resolve(type.ContainingNamespace);
    }

    /// <summary>
    /// Finds a forbidden type inside <paramref name="type"/>, unwrapping arrays, pointers and generic
    /// arguments (tuples and nullables are generic types).
    /// </summary>
    public ITypeSymbol? FindForbidden(ITypeSymbol? type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return FindForbidden(array.ElementType);
            case IPointerTypeSymbol pointer:
                return FindForbidden(pointer.PointedAtType);
            case INamedTypeSymbol named:
                if (Resolve(named.ContainingNamespace) == Target)
                    return named;
                if (named.IsGenericType)
                {
                    foreach (var arg in named.TypeArguments)
                    {
                        var hit = FindForbidden(arg);
                        if (hit is not null)
                            return hit;
                    }
                }
                return null;
            default:
                return null;
        }
    }

    /// <summary>Non-recursive variant for the syntax walk, where every generic argument is its own name node.</summary>
    public bool IsForbiddenNamedType(ITypeSymbol? type) =>
        type is INamedTypeSymbol named && Resolve(named.ContainingNamespace) == Target;

    private int Compute(INamespaceSymbol ns)
    {
        // Innermost segment wins: Nop.Core.Domain.Infrastructure would be Target.
        for (var n = ns; n is not null && !n.IsGlobalNamespace; n = n.ContainingNamespace)
        {
            if (Matches(_to, n.Name))
                return Target;
            if (Matches(_from, n.Name))
                return Source;
        }
        return None;
    }

    private static bool Matches(string[] names, string segment)
    {
        foreach (var name in names)
        {
            if (string.Equals(name, segment, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
