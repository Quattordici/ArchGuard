using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Archie.Analyzers;

/// <summary>Puts the squiggle on the type's name rather than the whole declaration. Only runs for violations, so it may walk syntax.</summary>
internal static class Locations
{
    /// <summary>
    /// The name of <paramref name="type"/> inside <paramref name="root"/> (a type, base list or attribute), matched by text: the first
    /// <c>Repository</c> in <c>List&lt;Repository&gt;</c>. Falls back to <paramref name="root"/> when the name is not written there (an alias).
    /// </summary>
    public static Location? Of(SyntaxNode? root, ITypeSymbol type)
    {
        if (root is null)
        {
            return null;
        }

        var name = type.Name;
        var shortAttributeName = name.EndsWith("Attribute", System.StringComparison.Ordinal) ? name.Substring(0, name.Length - "Attribute".Length) : null;
        foreach (var node in root.DescendantNodesAndSelf())
        {
            if (node is SimpleNameSyntax simple
                && (simple.Identifier.ValueText == name || (shortAttributeName is not null && simple.Identifier.ValueText == shortAttributeName)))
            {
                return simple.Identifier.GetLocation();
            }
        }

        // `DbAlias?`: the alias without the `?`.
        return (root is NullableTypeSyntax nullable ? nullable.ElementType : root).GetLocation();
    }

    /// <summary>The member name of <c>a.b.Name</c> or <c>Name</c>: where an access or call is squiggled.</summary>
    public static Location NameOf(SyntaxNode syntax) => syntax switch
    {
        MemberAccessExpressionSyntax access => access.Name.GetLocation(),
        MemberBindingExpressionSyntax binding => binding.Name.GetLocation(),
        _ => syntax.GetLocation(),
    };
}
