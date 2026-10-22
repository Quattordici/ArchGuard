using System.Collections.Immutable;
using Archie.Core.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Archie.Analyzers;

/// <summary>Dependencies in declarations: base types, interfaces, member and parameter types, constraints and attributes (design §5).</summary>
/// <remarks>The symbol is checked first and its syntax is only looked up for a violation: a symbol's declaring syntax costs more than the check.</remarks>
internal static class DeclarationRules
{
    public static readonly ImmutableArray<SymbolKind> Kinds =
        ImmutableArray.Create(SymbolKind.NamedType, SymbolKind.Method, SymbolKind.Field, SymbolKind.Property, SymbolKind.Event);

    public static void Analyze(SymbolAnalysisContext context, DependencyChecker checker)
    {
        var symbol = context.Symbol;
        if (symbol.IsImplicitlyDeclared || checker.CheckedSource(symbol) is not { } source)
        {
            return;
        }

        var scope = new Scope(context, checker, source, symbol);
        switch (symbol)
        {
            case INamedTypeSymbol type:
                AnalyzeType(scope, type);
                break;
            case IMethodSymbol method:
                // Accessors belong to their property or event, which reports the type once.
                if (method.AssociatedSymbol is not null)
                {
                    return;
                }

                AnalyzeSignature(scope, method);
                foreach (var attribute in method.GetReturnTypeAttributes())
                {
                    CheckAttribute(scope, attribute);
                }

                break;
            case IFieldSymbol field:
                if (scope.Find(field.Type) is { } fieldHit && FirstDeclarator(field) is { } fieldType)
                {
                    scope.Report(fieldHit, DependencyKind.Field, Locations.Of(fieldType, fieldHit.Type));
                }

                break;
            case IPropertySymbol property:
                if (property.ContainingType.IsRecord && DeclaringSyntax(property) is ParameterSyntax)
                {
                    return; // a record's positional property: its constructor parameter reports
                }

                if (scope.Find(property.Type) is { } propertyHit)
                {
                    scope.Report(propertyHit, DependencyKind.Property,
                        Locations.Of((DeclaringSyntax(property) as BasePropertyDeclarationSyntax)?.Type, propertyHit.Type));
                }

                CheckParameters(scope, property.Parameters);
                break;
            case IEventSymbol @event:
                switch (DeclaringSyntax(@event))
                {
                    case EventDeclarationSyntax declaration:
                        scope.Check(@event.Type, DependencyKind.Event, declaration.Type);
                        break;
                    case VariableDeclaratorSyntax declarator when FirstDeclaratorType(declarator) is { } eventType:
                        scope.Check(@event.Type, DependencyKind.Event, eventType);
                        break;
                }

                break;
        }

        foreach (var attribute in symbol.GetAttributes())
        {
            CheckAttribute(scope, attribute);
        }
    }

    private static void AnalyzeType(Scope scope, INamedTypeSymbol type)
    {
        if (type.BaseType is { } baseType && scope.Find(baseType) is { } baseHit)
        {
            scope.Report(baseHit, DependencyKind.BaseType, BaseListLocation(type, baseHit.Type));
        }

        foreach (var @interface in type.Interfaces)
        {
            if (scope.Find(@interface) is { } hit)
            {
                scope.Report(hit, DependencyKind.Interface, BaseListLocation(type, hit.Type));
            }
        }

        CheckTypeParameters(scope, type.TypeParameters);
        if (type.DelegateInvokeMethod is { } invoke)
        {
            AnalyzeSignature(scope, invoke);
        }
    }

    private static void AnalyzeSignature(Scope scope, IMethodSymbol method)
    {
        if (!method.ReturnsVoid && scope.Find(method.ReturnType) is { } hit)
        {
            var returnType = DeclaringSyntax(method) switch
            {
                MethodDeclarationSyntax m => m.ReturnType,
                OperatorDeclarationSyntax o => o.ReturnType,
                ConversionOperatorDeclarationSyntax c => c.Type,
                DelegateDeclarationSyntax d => d.ReturnType,
                _ => null,
            };

            scope.Report(hit, DependencyKind.ReturnType, Locations.Of(returnType, hit.Type));
        }

        CheckParameters(scope, method.Parameters);
        CheckTypeParameters(scope, method.TypeParameters);
    }

    public static void CheckParameters(Scope scope, ImmutableArray<IParameterSymbol> parameters)
    {
        foreach (var parameter in parameters)
        {
            if (scope.Find(parameter.Type) is { } hit)
            {
                // A parameter without a written type is inferred, not a dependency.
                var syntax = DeclaringSyntax(parameter) as ParameterSyntax;
                if (syntax is not { Type: null })
                {
                    scope.Report(hit, DependencyKind.Parameter, Locations.Of(syntax?.Type, hit.Type));
                }
            }

            foreach (var attribute in parameter.GetAttributes())
            {
                CheckAttribute(scope, attribute);
            }
        }
    }

    private static void CheckTypeParameters(Scope scope, ImmutableArray<ITypeParameterSymbol> parameters)
    {
        foreach (var parameter in parameters)
        {
            foreach (var constraint in parameter.ConstraintTypes)
            {
                if (scope.Find(constraint) is { } hit)
                {
                    scope.Report(hit, DependencyKind.Constraint, Locations.Of(ConstraintClause(parameter), hit.Type));
                }
            }

            foreach (var attribute in parameter.GetAttributes())
            {
                CheckAttribute(scope, attribute);
            }
        }
    }

    private static void CheckAttribute(Scope scope, AttributeData attribute)
    {
        var hit = scope.Find(attribute.AttributeClass);
        if (hit is null)
        {
            foreach (var argument in attribute.ConstructorArguments)
            {
                hit ??= FindInArgument(scope, argument);
            }

            foreach (var argument in attribute.NamedArguments)
            {
                hit ??= FindInArgument(scope, argument.Value);
            }
        }

        if (hit is not null)
        {
            scope.Report(hit, DependencyKind.Attribute, Locations.Of(attribute.ApplicationSyntaxReference?.GetSyntax(scope.CancellationToken), hit.Type));
        }
    }

    /// <summary><c>typeof(T)</c> and enum values in attribute arguments, which are not operations on every host.</summary>
    private static Violation? FindInArgument(Scope scope, TypedConstant argument) => argument.Kind switch
    {
        TypedConstantKind.Type => scope.Find(argument.Value as ITypeSymbol),
        TypedConstantKind.Enum => scope.Find(argument.Type),
        TypedConstantKind.Array when !argument.IsNull => FindInArray(scope, argument.Values),
        _ => null,
    };

    private static Violation? FindInArray(Scope scope, ImmutableArray<TypedConstant> values)
    {
        foreach (var value in values)
        {
            if (FindInArgument(scope, value) is { } hit)
            {
                return hit;
            }
        }

        return null;
    }

    /// <summary>The type syntax of a field or event field, only for the first of <c>Db a, b;</c> so one written type is one violation.</summary>
    private static TypeSyntax? FirstDeclarator(IFieldSymbol field) =>
        DeclaringSyntax(field) is VariableDeclaratorSyntax declarator ? FirstDeclaratorType(declarator) : null;

    private static TypeSyntax? FirstDeclaratorType(VariableDeclaratorSyntax declarator) =>
        declarator.Parent is VariableDeclarationSyntax declaration && declaration.Variables[0] == declarator ? declaration.Type : null;

    private static Location? BaseListLocation(INamedTypeSymbol type, ITypeSymbol hit)
    {
        // Partial types: the part whose base list names the type.
        Location? fallback = null;
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is TypeDeclarationSyntax { BaseList: { } baseList })
            {
                var location = Locations.Of(baseList, hit);
                if (location is not null && location.SourceSpan != baseList.Span)
                {
                    return location;
                }

                fallback ??= location;
            }
        }

        return fallback;
    }

    private static SyntaxNode? ConstraintClause(ITypeParameterSymbol parameter)
    {
        var declaration = DeclaringSyntax(parameter)?.Parent?.Parent;
        var clauses = declaration switch
        {
            TypeDeclarationSyntax type => type.ConstraintClauses,
            MethodDeclarationSyntax method => method.ConstraintClauses,
            DelegateDeclarationSyntax @delegate => @delegate.ConstraintClauses,
            LocalFunctionStatementSyntax local => local.ConstraintClauses,
            _ => default,
        };

        foreach (var clause in clauses)
        {
            if (clause.Name.Identifier.ValueText == parameter.Name)
            {
                return clause;
            }
        }

        return null;
    }

    private static SyntaxNode? DeclaringSyntax(ISymbol symbol) =>
        symbol.DeclaringSyntaxReferences.Length > 0 ? symbol.DeclaringSyntaxReferences[0].GetSyntax() : null;

    /// <summary>What one symbol's checks share.</summary>
    internal readonly struct Scope(SymbolAnalysisContext context, DependencyChecker checker, NamespaceInfo source, ISymbol symbol)
    {
        public System.Threading.CancellationToken CancellationToken => context.CancellationToken;

        public Violation? Find(ITypeSymbol? type) => checker.Find(source, type);

        public void Check(ITypeSymbol type, DependencyKind kind, SyntaxNode? typeSyntax)
        {
            if (Find(type) is { } hit)
            {
                Report(hit, kind, Locations.Of(typeSyntax, hit.Type));
            }
        }

        public void Report(Violation hit, DependencyKind kind, Location? location) =>
            context.ReportDiagnostic(checker.Create(hit, symbol, source, kind, location ?? FirstLocation()));

        private Location FirstLocation() => symbol.Locations.Length > 0 ? symbol.Locations[0] : Location.None;
    }
}
