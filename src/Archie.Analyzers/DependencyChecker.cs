using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Archie.Core.Rules;
using Microsoft.CodeAnalysis;

namespace Archie.Analyzers;

/// <summary>
/// The per-compilation side of the rules: classifies namespaces once per symbol, finds the first type in a type expression that breaks a rule
/// and turns violations into diagnostics. The decisions themselves are <see cref="ArchEvaluator"/>'s.
/// </summary>
internal sealed class DependencyChecker
{
    // `Order.Save`, `Outer.Inner`: containing types, no namespace, no parameters (the message names the target's namespace separately).
    private static readonly SymbolDisplayFormat ShortFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType);

    private readonly ArchEvaluator _evaluator;
    private readonly ConcurrentDictionary<INamespaceSymbol, NamespaceInfo> _namespaces = new(SymbolEqualityComparer.Default);
    private readonly Func<INamespaceSymbol, NamespaceInfo> _classify;

    public DependencyChecker(ArchEvaluator evaluator)
    {
        _evaluator = evaluator;
        _classify = ns => _evaluator.Classify(ns.IsGlobalNamespace ? string.Empty : ns.ToDisplayString());
    }

    /// <summary>The source's namespace when code in <paramref name="symbol"/> is checked, otherwise <see langword="null"/> (no layer, excluded).</summary>
    public NamespaceInfo? CheckedSource(ISymbol symbol)
    {
        var type = symbol as INamedTypeSymbol ?? symbol.ContainingType;
        if (type?.ContainingNamespace is not { } ns)
        {
            return null;
        }

        var info = Classify(ns);
        return info.IsCheckedSource ? info : null;
    }

    /// <summary>
    /// The first type in <paramref name="type"/> that <paramref name="source"/> may not use, looking through arrays, pointers, nullables,
    /// tuples and generic arguments; <see langword="null"/> if there is none.
    /// </summary>
    public Violation? Find(NamespaceInfo source, ITypeSymbol? type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return Find(source, array.ElementType);
            case IPointerTypeSymbol pointer:
                return Find(source, pointer.PointedAtType);
            case INamedTypeSymbol named when named.TypeKind != TypeKind.Error:
                if (named.ContainingNamespace is { } ns)
                {
                    var target = Classify(ns);
                    var verdict = _evaluator.Check(source, target);
                    if (verdict.IsViolation)
                    {
                        return new Violation(named, target, verdict);
                    }
                }

                for (var current = named; current is not null; current = current.ContainingType)
                {
                    foreach (var argument in current.TypeArguments)
                    {
                        if (Find(source, argument) is { } hit)
                        {
                            return hit;
                        }
                    }
                }

                return null;
            default:
                return null; // type parameters, dynamic, error types, function pointers
        }
    }

    /// <summary>Reports <paramref name="violation"/> by <paramref name="source"/> at <paramref name="location"/>.</summary>
    public Diagnostic Create(Violation violation, ISymbol source, NamespaceInfo sourceNamespace, DependencyKind kind, Location location)
    {
        var target = violation.Type;
        var edge = new DependencyEdge(
            SourceId(source),
            sourceNamespace.Name,
            DocumentationId(target.OriginalDefinition),
            violation.Target.Name,
            kind);

        var verdict = violation.Verdict;
        var layers = _evaluator.Model.Layers;
        var from = layers[verdict.SourceLayer].Name;
        var to = verdict.TargetLayer >= 0 ? layers[verdict.TargetLayer].Name : string.Empty;
        var properties = ImmutableDictionary.CreateRange(new[]
        {
            new KeyValuePair<string, string?>("from", from),
            new KeyValuePair<string, string?>("to", to),
            new KeyValuePair<string, string?>("target", edge.TargetNamespace),
            new KeyValuePair<string, string?>("sourceId", edge.SourceId),
            new KeyValuePair<string, string?>("targetId", edge.TargetTypeId),
            new KeyValuePair<string, string?>("kind", kind.ToString()),
        });

        var sourceText = source.ToDisplayString(ShortFormat);
        var targetText = target.OriginalDefinition.ToDisplayString(ShortFormat);
        return verdict.Kind == VerdictKind.LayerNotAllowed
            ? Diagnostic.Create(Diagnostics.LayerDependency, location, properties,
                from, to, sourceText, targetText, edge.TargetNamespace, _evaluator.DescribeAllowed(verdict.SourceLayer))
            : Diagnostic.Create(Diagnostics.ForbiddenNamespace, location, properties, from, edge.TargetNamespace, sourceText, targetText);
    }

    private NamespaceInfo Classify(INamespaceSymbol ns) => _namespaces.GetOrAdd(ns, _classify);

    /// <summary>
    /// The member that has the dependency, as metadata knows it: a parameter or local function belongs to its method, a type's own
    /// declaration-level uses (base type, attributes) to the type.
    /// </summary>
    private static string SourceId(ISymbol source)
    {
        var symbol = source;
        while (symbol is IParameterSymbol or ITypeParameterSymbol or ILocalSymbol || symbol is IMethodSymbol { MethodKind: MethodKind.LocalFunction or MethodKind.AnonymousFunction })
        {
            symbol = symbol.ContainingSymbol;
        }

        return DocumentationId(symbol.OriginalDefinition);
    }

    private static string DocumentationId(ISymbol symbol) =>
        symbol.GetDocumentationCommentId() ?? symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
}

/// <summary>A type that may not be used, the namespace it is in, and the rule it breaks.</summary>
internal sealed class Violation(INamedTypeSymbol type, NamespaceInfo target, Verdict verdict)
{
    public INamedTypeSymbol Type { get; } = type;

    public NamespaceInfo Target { get; } = target;

    public Verdict Verdict { get; } = verdict;
}
