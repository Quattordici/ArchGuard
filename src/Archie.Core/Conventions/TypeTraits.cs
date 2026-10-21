using System;

namespace Archie.Core.Conventions;

/// <summary>What kind of type it is and its modifiers, as named in <c>are</c>, <c>mustBe</c> and <c>mustNotBe</c>.</summary>
[Flags]
public enum TypeTraits
{
    /// <summary>Nothing.</summary>
    None = 0,

    /// <summary><c>class</c>, including record classes.</summary>
    Class = 1 << 0,

    /// <summary><c>interface</c>.</summary>
    Interface = 1 << 1,

    /// <summary><c>struct</c>, including record structs.</summary>
    Struct = 1 << 2,

    /// <summary><c>enum</c>.</summary>
    Enum = 1 << 3,

    /// <summary><c>delegate</c>.</summary>
    Delegate = 1 << 4,

    /// <summary><c>record</c> or <c>record struct</c>.</summary>
    Record = 1 << 5,

    /// <summary>An <c>abstract</c> class (not an interface).</summary>
    Abstract = 1 << 6,

    /// <summary>A <c>sealed</c> class (structs and enums are always sealed and do not count).</summary>
    Sealed = 1 << 7,

    /// <summary>A <c>static</c> class.</summary>
    Static = 1 << 8,

    /// <summary>Declared <c>public</c>.</summary>
    Public = 1 << 9,

    /// <summary>Declared <c>internal</c>.</summary>
    Internal = 1 << 10,
}

/// <summary>The words for <see cref="TypeTraits"/> in archie.json.</summary>
public static class TypeTraitNames
{
    /// <summary>The kinds; a type is exactly one of them.</summary>
    public const TypeTraits Kinds = TypeTraits.Class | TypeTraits.Interface | TypeTraits.Struct | TypeTraits.Enum | TypeTraits.Delegate;

    private static readonly (string Name, TypeTraits Trait)[] All =
    [
        // In C# declaration order, so descriptions read like code: "public sealed record".
        ("public", TypeTraits.Public),
        ("internal", TypeTraits.Internal),
        ("abstract", TypeTraits.Abstract),
        ("sealed", TypeTraits.Sealed),
        ("static", TypeTraits.Static),
        ("record", TypeTraits.Record),
        ("class", TypeTraits.Class),
        ("interface", TypeTraits.Interface),
        ("struct", TypeTraits.Struct),
        ("enum", TypeTraits.Enum),
        ("delegate", TypeTraits.Delegate),
    ];

    /// <summary>Every word, in the order the schema and messages list them.</summary>
    public static string[] Names { get; } = Array.ConvertAll(All, t => t.Name);

    /// <summary>The trait for <paramref name="name"/>, or <see cref="TypeTraits.None"/>.</summary>
    public static TypeTraits Parse(string name)
    {
        foreach (var (text, trait) in All)
        {
            if (string.Equals(text, name, StringComparison.Ordinal))
            {
                return trait;
            }
        }

        return TypeTraits.None;
    }

    /// <summary>The words of <paramref name="traits"/> in a fixed order, joined with <paramref name="separator"/>: "sealed and internal".</summary>
    public static string Describe(TypeTraits traits, string separator)
    {
        var text = string.Empty;
        foreach (var (name, trait) in All)
        {
            if ((traits & trait) != 0)
            {
                text = text.Length == 0 ? name : text + separator + name;
            }
        }

        return text;
    }
}
