using System.Collections.Generic;
using Archie.Core.Rules;

namespace Archie.Core.Conventions;

/// <summary>
/// The <c>types</c> of a convention: which types it applies to. Every key given must match; within a key a list means any of its values,
/// except <c>are</c> (all of the traits) and <c>areNot</c> (none of them). Without keys it selects every type that is checked.
/// </summary>
public sealed class TypeSelector
{
    internal TypeSelector(PlaceSet? @in, IReadOnlyList<NamePattern>? named, TypeTraits are, TypeTraits areNot, IReadOnlyList<TypeReference>? inherit,
        IReadOnlyList<TypeReference>? haveAttribute)
    {
        In = @in;
        Named = named;
        Are = are;
        AreNot = areNot;
        Inherit = inherit;
        HaveAttribute = haveAttribute;
        Description = Describe();
    }

    /// <summary><c>in</c>: layers or namespaces; <see langword="null"/> when not given.</summary>
    public PlaceSet? In { get; }

    /// <summary><c>named</c>: name patterns; <see langword="null"/> when not given.</summary>
    public IReadOnlyList<NamePattern>? Named { get; }

    /// <summary><c>are</c>: traits the type must all have.</summary>
    public TypeTraits Are { get; }

    /// <summary><c>areNot</c>: traits the type must have none of.</summary>
    public TypeTraits AreNot { get; }

    /// <summary><c>inherit</c>: base classes or interfaces; <see langword="null"/> when not given.</summary>
    public IReadOnlyList<TypeReference>? Inherit { get; }

    /// <summary><c>haveAttribute</c>: attributes; <see langword="null"/> when not given.</summary>
    public IReadOnlyList<TypeReference>? HaveAttribute { get; }

    /// <summary>For messages: "types in Domain named '*Event' that are not abstract and inherit 'IDomainEvent'", or "all types".</summary>
    public string Description { get; }

    /// <summary>Whether the convention applies to <paramref name="subject"/>. The cheap questions come first.</summary>
    internal bool Matches(IConventionSubject subject, NamespaceInfo ns)
    {
        var traits = subject.Traits;
        if ((traits & Are) != Are || (traits & AreNot) != 0)
        {
            return false;
        }

        if (Named is not null && !AnyName(Named, subject.Name))
        {
            return false;
        }

        if (In is not null && !In.Contains(ns))
        {
            return false;
        }

        if (Inherit is not null && !AnyType(Inherit, subject, attributes: false))
        {
            return false;
        }

        return HaveAttribute is null || AnyType(HaveAttribute, subject, attributes: true);
    }

    private static bool AnyName(IReadOnlyList<NamePattern> patterns, string name)
    {
        foreach (var pattern in patterns)
        {
            if (pattern.Matches(name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AnyType(IReadOnlyList<TypeReference> types, IConventionSubject subject, bool attributes)
    {
        foreach (var type in types)
        {
            if (attributes ? subject.HasAttribute(type) : subject.Inherits(type))
            {
                return true;
            }
        }

        return false;
    }

    private string Describe()
    {
        var text = "types";
        if (In is not null)
        {
            text += " in " + In.Description;
        }

        if (Named is not null)
        {
            text += " named " + JoinQuoted(Named, n => n.Text);
        }

        var clauses = new List<string>();
        if (Are != TypeTraits.None)
        {
            clauses.Add("are " + TypeTraitNames.Describe(Are, " and "));
        }

        if (AreNot != TypeTraits.None)
        {
            clauses.Add("are not " + TypeTraitNames.Describe(AreNot, " or "));
        }

        if (Inherit is not null)
        {
            clauses.Add("inherit " + JoinQuoted(Inherit, t => t.Text));
        }

        if (HaveAttribute is not null)
        {
            clauses.Add("have attribute " + JoinQuoted(HaveAttribute, t => t.Text));
        }

        if (clauses.Count > 0)
        {
            text += " that " + string.Join(" and ", clauses);
        }

        return text == "types" ? "all types" : text;
    }

    private static string JoinQuoted<T>(IReadOnlyList<T> items, System.Func<T, string> text)
    {
        var result = string.Empty;
        foreach (var item in items)
        {
            result = result.Length == 0 ? $"'{text(item)}'" : $"{result} or '{text(item)}'";
        }

        return result;
    }
}
