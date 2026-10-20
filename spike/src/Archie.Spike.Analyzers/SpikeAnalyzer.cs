using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Archie.Spike.Analyzers;

/// <summary>
/// Phase 0 spike analyzer. The registration strategy is picked per compilation from the MSBuild
/// property <c>ArchieSpikeMode</c> so one DLL can be measured several ways:
/// <list type="bullet">
/// <item><c>block</c> (default): symbol actions + OperationBlockStart that registers operation actions
/// only for members whose type is in the source layer.</item>
/// <item><c>operation</c>: symbol actions + flat RegisterOperationAction; the layer check runs per operation.</item>
/// <item><c>syntax</c>: one SyntaxNodeAction on every simple name, bound with GetSymbolInfo.</item>
/// <item><c>touch</c>: same registrations as <c>block</c> with empty callbacks, to separate the cost of
/// building IOperation trees from the rule's own work.</item>
/// <item><c>noop</c>: loads and registers nothing, to isolate the cost of having an analyzer at all.</item>
/// </list>
/// <c>ArchieSpikeFrom</c> / <c>ArchieSpikeTo</c> are semicolon lists of namespace segments
/// (default Domain / Infrastructure).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SpikeAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ARCH001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Layer dependency not allowed",
        "{0} must not depend on {1}: '{2}' uses '{3}'",
        "Archie",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly ImmutableArray<OperationKind> OperationKinds = ImmutableArray.Create(
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
        OperationKind.VariableDeclarator,
        OperationKind.CatchClause,
        OperationKind.ArrayCreation);

    private static readonly ImmutableArray<SymbolKind> SymbolKinds = ImmutableArray.Create(
        SymbolKind.NamedType, SymbolKind.Method, SymbolKind.Field, SymbolKind.Property, SymbolKind.Event);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var options = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
        var mode = Read(options, "build_property.ArchieSpikeMode", "block").ToLowerInvariant();
        var rule = new LayerRule(
            Split(Read(options, "build_property.ArchieSpikeFrom", "Domain")),
            Split(Read(options, "build_property.ArchieSpikeTo", "Infrastructure")));

        switch (mode)
        {
            case "noop":
                return;
            case "touch":
                context.RegisterSymbolAction(static _ => { }, SymbolKinds);
                context.RegisterOperationBlockStartAction(block =>
                {
                    if (rule.ResolveOwner(block.OwningSymbol) == LayerRule.Source)
                        block.RegisterOperationAction(static _ => { }, OperationKinds);
                });
                return;
            case "syntax":
                context.RegisterSyntaxNodeAction(c => AnalyzeName(c, rule), SyntaxKind.IdentifierName, SyntaxKind.GenericName);
                return;
            case "operation":
                context.RegisterSymbolAction(c => AnalyzeSymbol(c, rule), SymbolKinds);
                context.RegisterOperationAction(c =>
                {
                    if (rule.ResolveOwner(c.ContainingSymbol) == LayerRule.Source)
                        AnalyzeOperation(c, rule);
                }, OperationKinds);
                return;
            default:
                context.RegisterSymbolAction(c => AnalyzeSymbol(c, rule), SymbolKinds);
                context.RegisterOperationBlockStartAction(block =>
                {
                    if (rule.ResolveOwner(block.OwningSymbol) != LayerRule.Source)
                        return;
                    block.RegisterOperationAction(c => AnalyzeOperation(c, rule), OperationKinds);
                });
                return;
        }
    }

    // ---- declarations -------------------------------------------------------------------------

    private static void AnalyzeSymbol(SymbolAnalysisContext context, LayerRule rule)
    {
        var symbol = context.Symbol;
        if (symbol.IsImplicitlyDeclared || rule.ResolveOwner(symbol) != LayerRule.Source)
            return;

        switch (symbol)
        {
            case INamedTypeSymbol type:
                Check(context, rule, type.BaseType, "BaseType");
                foreach (var i in type.Interfaces)
                    Check(context, rule, i, "Interface");
                CheckConstraints(context, rule, type.TypeParameters);
                break;
            case IMethodSymbol method:
                if (method.AssociatedSymbol is not null)
                    return; // accessors: the property or event reports
                Check(context, rule, method.ReturnType, "ReturnType");
                foreach (var p in method.Parameters)
                    Check(context, rule, p.Type, "Parameter");
                CheckConstraints(context, rule, method.TypeParameters);
                break;
            case IFieldSymbol field:
                Check(context, rule, field.Type, "Field");
                break;
            case IPropertySymbol property:
                Check(context, rule, property.Type, "Property");
                foreach (var p in property.Parameters)
                    Check(context, rule, p.Type, "Parameter");
                break;
            case IEventSymbol @event:
                Check(context, rule, @event.Type, "Event");
                break;
        }

        foreach (var attribute in symbol.GetAttributes())
            Check(context, rule, attribute.AttributeClass, "Attribute");
    }

    private static void CheckConstraints(SymbolAnalysisContext context, LayerRule rule, ImmutableArray<ITypeParameterSymbol> parameters)
    {
        foreach (var tp in parameters)
        {
            foreach (var c in tp.ConstraintTypes)
                Check(context, rule, c, "Constraint");
        }
    }

    private static void Check(SymbolAnalysisContext context, LayerRule rule, ITypeSymbol? type, string via)
    {
        var hit = rule.FindForbidden(type);
        if (hit is null)
            return;
        // Spike: squiggle on the declaration. v1 locates the type syntax; that cost is only paid on a hit.
        var location = context.Symbol.Locations.Length > 0 ? context.Symbol.Locations[0] : Location.None;
        context.ReportDiagnostic(Create(rule, location, context.Symbol, hit, via));
    }

    // ---- bodies -------------------------------------------------------------------------------

    private static void AnalyzeOperation(OperationAnalysisContext context, LayerRule rule)
    {
        var operation = context.Operation;
        switch (operation)
        {
            case IInvocationOperation invocation:
                var method = invocation.TargetMethod;
                if (Report(context, rule, method.ContainingType, "Invocation"))
                    return;
                foreach (var arg in method.TypeArguments)
                {
                    if (Report(context, rule, arg, "TypeArgument"))
                        return;
                }
                break;
            case IObjectCreationOperation creation:
                // Newer hosts (not 4.8) expose attributes as operation blocks; the symbol action already reports them.
                if (creation.Syntax is not AttributeSyntax)
                    Report(context, rule, creation.Type, "ObjectCreation");
                break;
            case IArrayCreationOperation array:
                Report(context, rule, array.Type, "ArrayCreation");
                break;
            case IMemberReferenceOperation member:
                Report(context, rule, member.Member.ContainingType, "MemberReference");
                break;
            case ITypeOfOperation typeOf:
                Report(context, rule, typeOf.TypeOperand, "TypeOf");
                break;
            case IConversionOperation conversion:
                if (!conversion.IsImplicit)
                    Report(context, rule, conversion.Type, "Cast");
                break;
            case IIsTypeOperation isType:
                Report(context, rule, isType.TypeOperand, "IsType");
                break;
            case IDeclarationPatternOperation declarationPattern:
                Report(context, rule, declarationPattern.MatchedType, "Pattern");
                break;
            case ITypePatternOperation typePattern:
                Report(context, rule, typePattern.MatchedType, "Pattern");
                break;
            case IVariableDeclaratorOperation declarator:
                // Only explicitly written types count; `var` is reported where the value is created or touched.
                if (declarator.Syntax.Parent is VariableDeclarationSyntax { Type.IsVar: false })
                    Report(context, rule, declarator.Symbol.Type, "Local");
                break;
            case ICatchClauseOperation catchClause:
                Report(context, rule, catchClause.ExceptionType, "Catch");
                break;
        }
    }

    private static bool Report(OperationAnalysisContext context, LayerRule rule, ITypeSymbol? type, string via)
    {
        var hit = rule.FindForbidden(type);
        if (hit is null)
            return false;
        context.ReportDiagnostic(Create(rule, context.Operation.Syntax.GetLocation(), context.ContainingSymbol, hit, via));
        return true;
    }

    // ---- syntax walk --------------------------------------------------------------------------

    private static void AnalyzeName(SyntaxNodeAnalysisContext context, LayerRule rule)
    {
        var name = (SimpleNameSyntax)context.Node;
        if (name is IdentifierNameSyntax { IsVar: true })
            return;
        // Usings and namespace names have no containing type, so they fall out here too.
        if (rule.ResolveOwner(context.ContainingSymbol) != LayerRule.Source)
            return;

        var info = context.SemanticModel.GetSymbolInfo(name, context.CancellationToken);
        var symbol = info.Symbol ?? (info.CandidateSymbols.Length > 0 ? info.CandidateSymbols[0] : null);

        ITypeSymbol? target;
        string via;
        switch (symbol)
        {
            case ITypeSymbol type:
                target = type;
                via = "TypeName";
                break;
            case IAliasSymbol alias:
                target = alias.Target as ITypeSymbol;
                via = "TypeName";
                break;
            case IMethodSymbol method:
                target = (method.ReducedFrom ?? method).ContainingType;
                via = "MemberName";
                break;
            case IFieldSymbol or IPropertySymbol or IEventSymbol:
                target = symbol.ContainingType;
                via = "MemberName";
                break;
            default:
                return;
        }

        if (!rule.IsForbiddenNamedType(target))
            return;

        // `Infra.Util.Do()`: the type name already reported; only pay for the extra bind on a hit.
        if (via == "MemberName"
            && name.Parent is MemberAccessExpressionSyntax access
            && access.Name == name
            && context.SemanticModel.GetSymbolInfo(access.Expression, context.CancellationToken).Symbol is ITypeSymbol)
        {
            return;
        }

        context.ReportDiagnostic(Create(rule, name.GetLocation(), context.ContainingSymbol!, target!, via));
    }

    // ---- shared -------------------------------------------------------------------------------

    private static Diagnostic Create(LayerRule rule, Location location, ISymbol source, ITypeSymbol target, string via) =>
        Diagnostic.Create(
            Rule,
            location,
            ImmutableDictionary<string, string?>.Empty.Add("via", via),
            rule.FromName,
            rule.ToName,
            source.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat),
            target.ToDisplayString());

    private static string Read(AnalyzerConfigOptions options, string key, string fallback) =>
        options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;

    private static string[] Split(string value) =>
        value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
}
