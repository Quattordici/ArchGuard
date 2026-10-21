using System;

namespace Archie.Core.Conventions;

/// <summary>A type name with <c>*</c> for any run of characters: <c>*Handler</c>, <c>I*</c>, <c>*Dto*</c>. Matched against the name without namespace or type arguments.</summary>
public sealed class NamePattern
{
    private readonly string[] _parts;
    private readonly bool _anchoredStart;
    private readonly bool _anchoredEnd;

    private NamePattern(string text)
    {
        Text = text;
        _parts = text.Split(['*'], StringSplitOptions.RemoveEmptyEntries);
        _anchoredStart = text[0] != '*';
        _anchoredEnd = text[text.Length - 1] != '*';
    }

    /// <summary>The pattern as written in archie.json.</summary>
    public string Text { get; }

    /// <summary>Parses <paramref name="text"/>, or returns <see langword="null"/> and a reason that completes "'X' is not a valid name pattern: …".</summary>
    public static NamePattern? TryParse(string text, out string? error)
    {
        if (text.Length == 0)
        {
            error = "it is empty";
            return null;
        }

        foreach (var c in text)
        {
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '*'))
            {
                error = c == '.'
                    ? "it is matched against the type name only, without the namespace; use 'in' or 'mustBeIn' for namespaces"
                    : $"'{c}' cannot be part of a type name; use letters, digits, '_' and '*' (any characters)";
                return null;
            }
        }

        error = null;
        return new NamePattern(text);
    }

    /// <summary>Whether <paramref name="name"/> matches. Case-sensitive, like C#.</summary>
    public bool Matches(string name)
    {
        if (_parts.Length == 0)
        {
            return true; // "*"
        }

        var position = 0;
        for (var i = 0; i < _parts.Length; i++)
        {
            var part = _parts[i];
            if (i == 0 && _anchoredStart)
            {
                if (!name.StartsWith(part, StringComparison.Ordinal))
                {
                    return false;
                }

                position = part.Length;
                continue;
            }

            if (i == _parts.Length - 1 && _anchoredEnd)
            {
                return name.Length - part.Length >= position && name.EndsWith(part, StringComparison.Ordinal);
            }

            var found = name.IndexOf(part, position, StringComparison.Ordinal);
            if (found < 0)
            {
                return false;
            }

            position = found + part.Length;
        }

        return !_anchoredEnd || position == name.Length;
    }

    /// <inheritdoc />
    public override string ToString() => Text;
}
