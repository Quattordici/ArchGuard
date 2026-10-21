namespace Archie.Core.Rules;

/// <summary>How the source uses the target. Informational: it does not change the verdict, and backends may not agree on it.</summary>
public enum DependencyKind
{
    /// <summary>The base class.</summary>
    BaseType,

    /// <summary>An implemented interface.</summary>
    Interface,

    /// <summary>The type of a field.</summary>
    Field,

    /// <summary>The type of a property or indexer.</summary>
    Property,

    /// <summary>The type of an event.</summary>
    Event,

    /// <summary>The type of a parameter.</summary>
    Parameter,

    /// <summary>The return type of a method or delegate.</summary>
    ReturnType,

    /// <summary>The explicitly written type of a local variable.</summary>
    Local,

    /// <summary>An explicitly written generic argument of a method call.</summary>
    TypeArgument,

    /// <summary>A generic constraint.</summary>
    Constraint,

    /// <summary>An attribute, or a type or enum value in its arguments.</summary>
    Attribute,

    /// <summary><c>new T()</c>.</summary>
    ObjectCreation,

    /// <summary>A call of a method declared on the type, including extension methods (the static class counts).</summary>
    Invocation,

    /// <summary>A field, property, event or method group of the type.</summary>
    MemberAccess,

    /// <summary><c>typeof(T)</c>.</summary>
    TypeOf,

    /// <summary>An explicit cast or <c>as</c>.</summary>
    Cast,

    /// <summary><c>is T</c> and type patterns.</summary>
    TypeCheck,

    /// <summary><c>catch (T)</c>.</summary>
    Catch,

    /// <summary><c>new T[n]</c>.</summary>
    ArrayCreation,

    /// <summary><c>default(T)</c>.</summary>
    Default,
}
