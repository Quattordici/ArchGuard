using System.Collections.Immutable;
using Archie.Core.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Archie.Analyzers;

/// <summary>
/// Dependencies in code: creation, calls, member access, typeof, casts, type checks, catch, explicitly typed locals (design §5). Registered
/// per operation block only when the owning member is checked (Phase 0: the cheapest strategy).
/// </summary>
internal static class BodyRules
{
    public static readonly ImmutableArray<OperationKind> Kinds = ImmutableArray.Create(
        OperationKind.Invocation,
        OperationKind.ObjectCreation,
        OperationKind.FieldReference,
        OperationKind.PropertyReference,
        OperationKind.EventReference,
        OperationKind.MethodReference,
        OperationKind.TypeOf,
        OperationKind.Conversion,
        OperationKind.IsType,
        OperationKind.DeclarationPattern,
        OperationKind.TypePattern,
        OperationKind.RecursivePattern,
        OperationKind.VariableDeclarator,
        OperationKind.Loop,
        OperationKind.DeclarationExpression,
        OperationKind.CatchClause,
        OperationKind.ArrayCreation,
        OperationKind.DefaultValue,
        OperationKind.LocalFunction,
        OperationKind.AnonymousFunction);

    public static void Analyze(OperationAnalysisContext context, DependencyChecker checker, NamespaceInfo source)
    {
        var operation = context.Operation;
        switch (operation)
        {
            case IInvocationOperation invocation when !invocation.IsImplicit && invocation.Syntax is InvocationExpressionSyntax call:
                var method = invocation.TargetMethod;
                if (!Check(context, checker, source, method.ContainingType, DependencyKind.Invocation, Locations.NameOf(call.Expression))
                    && GenericName(call.Expression) is { } generic)
                {
                    foreach (var argument in method.TypeArguments)
                    {
                        if (CheckIn(context, checker, source, argument, DependencyKind.TypeArgument, generic.TypeArgumentList))
                        {
                            break;
                        }
                    }
                }

                break;
            case IObjectCreationOperation creation when !creation.IsImplicit:
                CheckIn(context, checker, source, creation.Type, DependencyKind.ObjectCreation,
                    creation.Syntax is ObjectCreationExpressionSyntax written ? written.Type : creation.Syntax);
                break;
            case IMemberReferenceOperation member when !member.IsImplicit:
                Check(context, checker, source, member.Member.ContainingType, DependencyKind.MemberAccess, Locations.NameOf(member.Syntax));
                break;
            case ITypeOfOperation typeOf:
                CheckIn(context, checker, source, typeOf.TypeOperand, DependencyKind.TypeOf, (typeOf.Syntax as TypeOfExpressionSyntax)?.Type);
                break;
            case IConversionOperation conversion:
                // Only conversions that are written: `(T)x` and `x as T`. Implicit ones are inferred.
                var castType = conversion.Syntax switch
                {
                    CastExpressionSyntax cast => cast.Type,
                    BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AsExpression) => binary.Right,
                    _ => null,
                };

                if (castType is not null)
                {
                    CheckIn(context, checker, source, conversion.Type, DependencyKind.Cast, castType);
                }

                break;
            case IIsTypeOperation isType:
                CheckIn(context, checker, source, isType.TypeOperand, DependencyKind.TypeCheck, (isType.Syntax as BinaryExpressionSyntax)?.Right);
                break;
            case IDeclarationPatternOperation { Syntax: DeclarationPatternSyntax pattern } declaration:
                CheckIn(context, checker, source, declaration.MatchedType, DependencyKind.TypeCheck, pattern.Type);
                break;
            case ITypePatternOperation typePattern:
                CheckIn(context, checker, source, typePattern.MatchedType, DependencyKind.TypeCheck, typePattern.Syntax);
                break;
            case IRecursivePatternOperation { Syntax: RecursivePatternSyntax { Type: { } patternType } } recursive:
                CheckIn(context, checker, source, recursive.MatchedType, DependencyKind.TypeCheck, patternType);
                break;
            case IVariableDeclaratorOperation declarator:
                // `Db a, b;` is one written type, so one violation.
                if (declarator.Syntax is VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Type.IsVar: false } variables } variable
                    && variables.Variables[0] == variable)
                {
                    CheckIn(context, checker, source, declarator.Symbol.Type, DependencyKind.Local, variables.Type);
                }

                break;
            case IForEachLoopOperation { Syntax: ForEachStatementSyntax { Type.IsVar: false } loop } forEach:
                foreach (var local in forEach.Locals)
                {
                    CheckIn(context, checker, source, local.Type, DependencyKind.Local, loop.Type);
                }

                break;
            case IDeclarationExpressionOperation { Syntax: DeclarationExpressionSyntax { Type.IsVar: false } expression } outVariable:
                CheckIn(context, checker, source, outVariable.Type, DependencyKind.Local, expression.Type);
                break;
            case ICatchClauseOperation { Syntax: CatchClauseSyntax { Declaration: { } catchDeclaration } } catchClause:
                CheckIn(context, checker, source, catchClause.ExceptionType, DependencyKind.Catch, catchDeclaration.Type);
                break;
            case IArrayCreationOperation { Syntax: ArrayCreationExpressionSyntax array } arrayCreation:
                CheckIn(context, checker, source, arrayCreation.Type, DependencyKind.ArrayCreation, array.Type.ElementType);
                break;
            case IDefaultValueOperation { Syntax: DefaultExpressionSyntax defaultExpression } defaultValue:
                CheckIn(context, checker, source, defaultValue.Type, DependencyKind.Default, defaultExpression.Type);
                break;
            case ILocalFunctionOperation { Syntax: LocalFunctionStatementSyntax localFunction } local:
                if (!local.Symbol.ReturnsVoid)
                {
                    CheckIn(context, checker, source, local.Symbol.ReturnType, DependencyKind.ReturnType, localFunction.ReturnType);
                }

                CheckParameters(context, checker, source, local.Symbol);
                break;
            case IAnonymousFunctionOperation lambda:
                if (lambda.Syntax is ParenthesizedLambdaExpressionSyntax { ReturnType: { } lambdaReturn })
                {
                    CheckIn(context, checker, source, lambda.Symbol.ReturnType, DependencyKind.ReturnType, lambdaReturn);
                }

                CheckParameters(context, checker, source, lambda.Symbol);
                break;
        }
    }

    private static void CheckParameters(OperationAnalysisContext context, DependencyChecker checker, NamespaceInfo source, IMethodSymbol method)
    {
        foreach (var parameter in method.Parameters)
        {
            // Only written types: `x => …` infers its parameter's type.
            if (parameter.DeclaringSyntaxReferences.Length > 0
                && parameter.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken) is ParameterSyntax { Type: { } type })
            {
                CheckIn(context, checker, source, parameter.Type, DependencyKind.Parameter, type);
            }
        }
    }

    /// <summary>Checks <paramref name="type"/> and squiggles its name inside <paramref name="syntax"/>.</summary>
    private static bool CheckIn(OperationAnalysisContext context, DependencyChecker checker, NamespaceInfo source, ITypeSymbol? type,
        DependencyKind kind, SyntaxNode? syntax)
    {
        if (checker.Find(source, type) is not { } hit)
        {
            return false;
        }

        Report(context, checker, source, hit, kind, Locations.Of(syntax, hit.Type));
        return true;
    }

    /// <summary>Checks <paramref name="type"/> and squiggles <paramref name="syntax"/> as a whole.</summary>
    private static bool Check(OperationAnalysisContext context, DependencyChecker checker, NamespaceInfo source, ITypeSymbol? type,
        DependencyKind kind, SyntaxNode syntax) =>
        Check(context, checker, source, type, kind, syntax.GetLocation());

    private static bool Check(OperationAnalysisContext context, DependencyChecker checker, NamespaceInfo source, ITypeSymbol? type,
        DependencyKind kind, Location location)
    {
        if (checker.Find(source, type) is not { } hit)
        {
            return false;
        }

        Report(context, checker, source, hit, kind, location);
        return true;
    }

    private static void Report(OperationAnalysisContext context, DependencyChecker checker, NamespaceInfo source, Violation hit, DependencyKind kind,
        Location? location)
    {
        // Attributes are operation blocks on newer hosts only (Roslyn 5.x, not 4.8); the declaration rules report them on every host.
        // Only checked for violations, so the walk costs nothing on the common path.
        if (InAttributeOrNameOf(context.Operation))
        {
            return;
        }

        context.ReportDiagnostic(checker.Create(hit, context.ContainingSymbol, source, kind, location ?? context.Operation.Syntax.GetLocation()));
    }

    private static bool InAttributeOrNameOf(IOperation operation)
    {
        for (var current = operation; current is not null; current = current.Parent)
        {
            if (current is INameOfOperation)
            {
                return true;
            }
        }

        for (var node = operation.Syntax; node is not null; node = node.Parent)
        {
            if (node is AttributeSyntax)
            {
                return true;
            }

            if (node is MemberDeclarationSyntax or StatementSyntax)
            {
                return false;
            }
        }

        return false;
    }

    private static GenericNameSyntax? GenericName(ExpressionSyntax expression) => expression switch
    {
        GenericNameSyntax generic => generic,
        MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } => generic,
        MemberBindingExpressionSyntax { Name: GenericNameSyntax generic } => generic,
        _ => null,
    };
}
