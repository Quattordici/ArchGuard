using System;
using Archie.Core.Rules;

namespace Archie.Core.Conventions;

/// <summary>
/// A type named in a convention: <c>MediatR.IRequestHandler&lt;,&gt;</c>, <c>IRequestHandler`2</c>, <c>EntityBase</c>. Without a namespace it matches
/// the name in any namespace; without type arguments it matches any arity. Type arguments only count: <c>IHandler&lt;T&gt;</c> is
/// <c>IHandler&lt;&gt;</c>.
/// </summary>
public sealed class TypeReference
{
    private TypeReference(string text, string? @namespace, string name, int arity, bool isAttribute)
    {
        Text = text;
        Namespace = @namespace;
        Name = name;
        Arity = arity;
        IsAttribute = isAttribute;
    }

    /// <summary>As written in archie.json.</summary>
    public string Text { get; }

    /// <summary>The namespace, or <see langword="null"/> when the name was written without one.</summary>
    public string? Namespace { get; }

    /// <summary>The type name without namespace and type arguments.</summary>
    public string Name { get; }

    /// <summary>The number of type parameters, or -1 for any.</summary>
    public int Arity { get; }

    /// <summary>An attribute name: like in C#, <c>Serializable</c> also matches <c>SerializableAttribute</c>.</summary>
    public bool IsAttribute { get; }

    /// <summary>Parses <paramref name="text"/>, or returns <see langword="null"/> and a reason that completes "'X' is not a valid type name: …".</summary>
    public static TypeReference? TryParse(string text, bool isAttribute, out string? error)
    {
        var name = text.Trim();
        var arity = -1;
        var open = name.IndexOf('<');
        var tick = name.IndexOf('`');
        if (open >= 0)
        {
            if (name[name.Length - 1] != '>' || name.IndexOf('<', open + 1) >= 0)
            {
                error = "write type arguments as <> or <,> at the end, e.g. IRequestHandler<,>";
                return null;
            }

            var arguments = name.Substring(open + 1, name.Length - open - 2);
            arity = 1;
            foreach (var argument in arguments.Split(','))
            {
                var trimmed = argument.Trim();
                if (trimmed.Length > 0 && !Identifiers.IsIdentifier(trimmed))
                {
                    error = "only the number of type arguments counts; write IRequestHandler<,> or IRequestHandler<TRequest, TResponse>";
                    return null;
                }
            }

            arity += Count(arguments, ',');
            name = name.Substring(0, open);
        }
        else if (tick >= 0)
        {
            if (!int.TryParse(name.Substring(tick + 1), out arity) || arity < 1)
            {
                error = "after '`' comes the number of type parameters, e.g. IRequestHandler`2";
                return null;
            }

            name = name.Substring(0, tick);
        }

        var dot = name.LastIndexOf('.');
        var simple = dot >= 0 ? name.Substring(dot + 1) : name;
        string? @namespace = null;
        if (dot >= 0)
        {
            @namespace = name.Substring(0, dot);
            if (NamespacePattern.TryParse(@namespace, allowWildcards: false, out var namespaceError) is null)
            {
                error = $"the namespace '{@namespace}' is not valid: {namespaceError}";
                return null;
            }
        }

        if (!Identifiers.IsIdentifier(simple))
        {
            error = simple.Length == 0 ? "it has no type name" : $"'{simple}' is not a type name";
            return null;
        }

        error = null;
        return new TypeReference(text, @namespace, simple, arity, isAttribute);
    }

    /// <summary>
    /// Whether the type <paramref name="namespace"/>.<paramref name="name"/> with <paramref name="arity"/> type parameters is the one named.
    /// <paramref name="namespace"/> is only read when the reference has one, so front-ends can pass a cached string.
    /// </summary>
    public bool Matches(string name, int arity, string @namespace) =>
        MatchesName(name) && (Arity < 0 || Arity == arity) && (Namespace is null || string.Equals(Namespace, @namespace, StringComparison.Ordinal));

    /// <summary>Whether <paramref name="name"/> could match, before the front-end works out arity and namespace.</summary>
    public bool MatchesName(string name) =>
        string.Equals(name, Name, StringComparison.Ordinal)
        || (IsAttribute && name.Length == Name.Length + 9 && name.StartsWith(Name, StringComparison.Ordinal) && name.EndsWith("Attribute", StringComparison.Ordinal));

    /// <inheritdoc />
    public override string ToString() => Text;

    private static int Count(string text, char c)
    {
        var count = 0;
        foreach (var x in text)
        {
            if (x == c)
            {
                count++;
            }
        }

        return count;
    }
}
