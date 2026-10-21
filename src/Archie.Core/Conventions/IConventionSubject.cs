namespace Archie.Core.Conventions;

/// <summary>
/// A type checked against the conventions, as a front-end sees it: the analyzer wraps a symbol, the metadata runner (Phase 4) a type
/// definition. The questions are narrow so a front-end answers them without building lists: the evaluator asks only what a convention needs.
/// </summary>
public interface IConventionSubject
{
    /// <summary>The name without namespace and type parameters: <c>Repository</c> for <c>Repository&lt;T&gt;</c>.</summary>
    string Name { get; }

    /// <summary>How messages name the type: <c>Outer.Repository&lt;T&gt;</c>.</summary>
    string DisplayName { get; }

    /// <summary>The namespace; empty for the global namespace.</summary>
    string Namespace { get; }

    /// <summary>Its kind and modifiers.</summary>
    TypeTraits Traits { get; }

    /// <summary>
    /// Whether <paramref name="type"/> is a base class (at any depth) or an interface the type implements (directly or through a base or
    /// another interface). The type itself does not count. Match each candidate with <see cref="TypeReference.Matches"/>.
    /// </summary>
    bool Inherits(TypeReference type);

    /// <summary>Whether the type's own declaration has an attribute whose class is, or derives from, <paramref name="attribute"/>.</summary>
    bool HasAttribute(TypeReference attribute);
}
