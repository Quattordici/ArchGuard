using System.Collections.Generic;
using Archie.Core.Rules;

namespace Archie.Core.Conventions;

/// <summary>The diagnostic IDs of the conventions, one per kind of requirement, so each can get its own severity in .editorconfig.</summary>
public static class ConventionIds
{
    /// <summary><c>mustBeNamed</c>, <c>mustNotBeNamed</c>.</summary>
    public const string Naming = "ARCH101";

    /// <summary><c>mustBeIn</c>, <c>mustNotBeIn</c>.</summary>
    public const string Placement = "ARCH102";

    /// <summary><c>mustInherit</c>, <c>mustNotInherit</c>.</summary>
    public const string Inheritance = "ARCH103";

    /// <summary><c>mustHaveAttribute</c>, <c>mustNotHaveAttribute</c>.</summary>
    public const string Attribute = "ARCH104";

    /// <summary><c>mustBe</c>, <c>mustNotBe</c>.</summary>
    public const string KindAndModifiers = "ARCH105";
}

/// <summary>One <c>must…</c> key of a convention: what every selected type must (or must not) be.</summary>
public abstract class Requirement
{
    private protected Requirement(string key, bool negated, string id, IReadOnlyList<string> values)
    {
        Key = key;
        Negated = negated;
        Id = id;
        RuleKey = key + "=" + string.Join(",", values);
    }

    /// <summary>The key in archie.json: <c>mustBeNamed</c>.</summary>
    public string Key { get; }

    /// <summary>A <c>mustNot…</c> key.</summary>
    public bool Negated { get; }

    /// <summary>The diagnostic ID, from <see cref="ConventionIds"/>.</summary>
    public string Id { get; }

    /// <summary>
    /// <c>mustBeNamed=*Handler</c>: the key and its values as written. With the type's doc-comment ID it identifies a violation (the
    /// <c>targetId</c> of the shared violation key, plan §14.2), and it changes when the rule does.
    /// </summary>
    public string RuleKey { get; }

    /// <summary>For messages: "must be named '*Handler'".</summary>
    public abstract string Describe();

    /// <summary>Whether <paramref name="subject"/> meets the requirement; otherwise <paramref name="detail"/> completes the message (may be empty).</summary>
    internal abstract bool IsMet(IConventionSubject subject, NamespaceInfo ns, out string detail);

    private protected static string Quote(IReadOnlyList<string> values, string separator)
    {
        var result = string.Empty;
        foreach (var value in values)
        {
            result = result.Length == 0 ? $"'{value}'" : $"{result}{separator}'{value}'";
        }

        return result;
    }
}

internal sealed class NameRequirement(bool negated, IReadOnlyList<NamePattern> patterns)
    : Requirement(negated ? "mustNotBeNamed" : "mustBeNamed", negated, ConventionIds.Naming, Texts(patterns))
{
    public override string Describe() => (Negated ? "must not be named " : "must be named ") + Quote(Texts(patterns), " or ");

    private static string[] Texts(IReadOnlyList<NamePattern> patterns)
    {
        var texts = new string[patterns.Count];
        for (var i = 0; i < texts.Length; i++)
        {
            texts[i] = patterns[i].Text;
        }

        return texts;
    }

    internal override bool IsMet(IConventionSubject subject, NamespaceInfo ns, out string detail)
    {
        detail = string.Empty;
        foreach (var pattern in patterns)
        {
            if (pattern.Matches(subject.Name))
            {
                return !Negated;
            }
        }

        return Negated;
    }
}

internal sealed class PlaceRequirement(bool negated, PlaceSet places, IReadOnlyList<string> layerNames)
    : Requirement(negated ? "mustNotBeIn" : "mustBeIn", negated, ConventionIds.Placement, places.Texts)
{
    public override string Describe() => (Negated ? "must not be in " : "must be in ") + places.Description;

    internal override bool IsMet(IConventionSubject subject, NamespaceInfo ns, out string detail)
    {
        if (places.Contains(ns) != Negated)
        {
            detail = string.Empty;
            return true;
        }

        var where = ns.Name.Length == 0 ? "the global namespace" : ns.Name;
        detail = ns.Layer >= 0 ? $", but is in {layerNames[ns.Layer]} ({where})" : $", but is in {where}";
        return false;
    }
}

internal sealed class TypeListRequirement : Requirement
{
    private readonly IReadOnlyList<TypeReference> _types;
    private readonly bool _attributes;
    private readonly string _description;

    public TypeListRequirement(bool attributes, bool negated, IReadOnlyList<TypeReference> types, IReadOnlyList<string> texts)
        : base(Keys[(attributes ? 2 : 0) + (negated ? 1 : 0)], negated, attributes ? ConventionIds.Attribute : ConventionIds.Inheritance, texts)
    {
        _types = types;
        _attributes = attributes;
        var verb = attributes ? "have attribute " : "inherit ";
        _description = (negated ? "must not " : "must ") + verb + Quote(texts, negated ? " or " : " and ");
    }

    private static string[] Keys { get; } = ["mustInherit", "mustNotInherit", "mustHaveAttribute", "mustNotHaveAttribute"];

    public override string Describe() => _description;

    internal override bool IsMet(IConventionSubject subject, NamespaceInfo ns, out string detail)
    {
        // mustInherit/mustHaveAttribute need all of them, the mustNot… keys none. With one value the description says it all.
        List<string>? offending = null;
        foreach (var type in _types)
        {
            var has = _attributes ? subject.HasAttribute(type) : subject.Inherits(type);
            if (has == Negated)
            {
                (offending ??= []).Add(type.Text);
            }
        }

        if (offending is null)
        {
            detail = string.Empty;
            return true;
        }

        detail = _types.Count == 1 ? string.Empty : (Negated ? " (it does: " : " (missing: ") + Quote(offending, ", ") + ")";
        return false;
    }
}

internal sealed class TraitRequirement(bool negated, TypeTraits traits, IReadOnlyList<string> texts)
    : Requirement(negated ? "mustNotBe" : "mustBe", negated, ConventionIds.KindAndModifiers, texts)
{
    public override string Describe() => (Negated ? "must not be " : "must be ") + TypeTraitNames.Describe(traits, Negated ? " or " : " and ");

    internal override bool IsMet(IConventionSubject subject, NamespaceInfo ns, out string detail)
    {
        // mustBe needs all of them, mustNotBe none.
        var offending = Negated ? subject.Traits & traits : traits & ~subject.Traits;
        if (offending == TypeTraits.None)
        {
            detail = string.Empty;
            return true;
        }

        var single = (traits & (traits - 1)) == 0;
        detail = single ? string.Empty : (Negated ? " (it is " : " (it is not ") + TypeTraitNames.Describe(offending, Negated ? " and " : " or ") + ")";
        return false;
    }
}
