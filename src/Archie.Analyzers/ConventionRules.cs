using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Archie.Core.Conventions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Archie.Analyzers;

/// <summary>Checks each declared type against the conventions (ARCH101–ARCH105). The decisions are <see cref="ConventionEvaluator"/>'s.</summary>
internal static class ConventionRules
{
    private static readonly SymbolDisplayFormat ShortFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

    public static void Analyze(SymbolAnalysisContext context, ConventionEvaluator evaluator, NamespaceNames namespaces)
    {
        if (context.Symbol is not INamedTypeSymbol type || type.IsImplicitlyDeclared || type.Locations.IsEmpty)
        {
            return;
        }

        var violations = evaluator.Check(new TypeSubject(type, namespaces));
        if (violations.Count == 0)
        {
            return;
        }

        string? sourceId = null;
        foreach (var violation in violations)
        {
            sourceId ??= type.GetDocumentationCommentId() ?? type.ToDisplayString();
            var convention = violation.Convention;
            var properties = ImmutableDictionary.CreateRange(new[]
            {
                new KeyValuePair<string, string?>("convention", convention.Name ?? $"conventions[{convention.Index}]"),
                new KeyValuePair<string, string?>("sourceId", sourceId),
                new KeyValuePair<string, string?>("targetId", violation.Requirement.RuleKey),
                new KeyValuePair<string, string?>("kind", violation.Requirement.Key),
            });

            // On the name of the first declaration; the other parts of a partial type are listed as additional locations.
            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ForConvention(violation.Id), type.Locations[0], type.Locations.RemoveAt(0),
                properties, violation.Message));
        }
    }

    private sealed class TypeSubject(INamedTypeSymbol type, NamespaceNames namespaces) : IConventionSubject
    {
        private string? _displayName;

        public string Name => type.Name;

        public string DisplayName => _displayName ??= type.ToDisplayString(ShortFormat);

        public string Namespace => namespaces.Of(type.ContainingNamespace);

        public TypeTraits Traits { get; } = TraitsOf(type);

        public bool Inherits(TypeReference reference)
        {
            for (var current = type.BaseType; current is not null; current = current.BaseType)
            {
                if (Matches(reference, current))
                {
                    return true;
                }
            }

            foreach (var @interface in type.AllInterfaces)
            {
                if (Matches(reference, @interface))
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasAttribute(TypeReference reference)
        {
            foreach (var attribute in type.GetAttributes())
            {
                for (var current = attribute.AttributeClass; current is not null; current = current.BaseType)
                {
                    if (Matches(reference, current))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool Matches(TypeReference reference, INamedTypeSymbol candidate) =>
            reference.MatchesName(candidate.Name) && reference.Matches(candidate.Name, candidate.Arity, namespaces.Of(candidate.ContainingNamespace));

        private static TypeTraits TraitsOf(INamedTypeSymbol type)
        {
            var traits = type.TypeKind switch
            {
                TypeKind.Class => TypeTraits.Class,
                TypeKind.Interface => TypeTraits.Interface,
                TypeKind.Struct => TypeTraits.Struct,
                TypeKind.Enum => TypeTraits.Enum,
                TypeKind.Delegate => TypeTraits.Delegate,
                _ => TypeTraits.None,
            };

            if (type.IsRecord)
            {
                traits |= TypeTraits.Record;
            }

            if (type.TypeKind == TypeKind.Class)
            {
                if (type.IsStatic)
                {
                    traits |= TypeTraits.Static;
                }
                else if (type.IsAbstract)
                {
                    traits |= TypeTraits.Abstract;
                }
                else if (type.IsSealed)
                {
                    traits |= TypeTraits.Sealed;
                }
            }

            traits |= type.DeclaredAccessibility switch
            {
                Accessibility.Public => TypeTraits.Public,
                Accessibility.Internal => TypeTraits.Internal,
                _ => TypeTraits.None,
            };

            return traits;
        }
    }
}

/// <summary>Namespace names per symbol, per compilation, so a type's namespace and its base types' are turned into strings once.</summary>
internal sealed class NamespaceNames
{
    private readonly ConcurrentDictionary<INamespaceSymbol, string> _names = new(SymbolEqualityComparer.Default);
    private readonly Func<INamespaceSymbol, string> _name = ns => ns.IsGlobalNamespace ? string.Empty : ns.ToDisplayString();

    public string Of(INamespaceSymbol? ns) => ns is null ? string.Empty : _names.GetOrAdd(ns, _name);
}
