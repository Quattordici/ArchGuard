using System;
using System.Collections.Generic;

namespace Archie.Core.Rules;

/// <summary>
/// A namespace glob from archie.json, matched segment by segment: <c>*</c> is one segment, <c>**</c> zero or more. Every pattern covers the
/// namespaces under what it matches (<c>MyApp.Domain</c> is <c>MyApp.Domain.**</c>), so users write the short form.
/// </summary>
public sealed class NamespacePattern
{
    private const string AnySegment = "*";
    private const string AnySegments = "**";

    private readonly string[] _segments;

    private NamespacePattern(string text, string[] segments, int specificity)
    {
        Text = text;
        _segments = segments;
        Specificity = specificity;
    }

    /// <summary>The pattern as written in archie.json.</summary>
    public string Text { get; }

    /// <summary>Number of literal segments. When several layers match a namespace, the most specific pattern wins.</summary>
    public int Specificity { get; }

    /// <summary>Parses <paramref name="text"/>, or returns <see langword="null"/> and a reason that completes "'X' is not a valid namespace pattern: …".</summary>
    public static NamespacePattern? TryParse(string text, out string? error) => TryParse(text, allowWildcards: true, out error);

    internal static NamespacePattern? TryParse(string text, bool allowWildcards, out string? error)
    {
        if (text.Length == 0)
        {
            error = "it is empty";
            return null;
        }

        var parts = text.Split('.');
        var segments = new List<string>(parts.Length + 1);
        var specificity = 0;
        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                error = "it has an empty segment (two dots, or a dot at the start or end)";
                return null;
            }

            if (part is AnySegment or AnySegments)
            {
                if (!allowWildcards)
                {
                    error = "wildcards are not allowed here";
                    return null;
                }

                // `**.**` is the same as `**`.
                if (part == AnySegments && segments.Count > 0 && segments[segments.Count - 1] == AnySegments)
                {
                    continue;
                }

                segments.Add(part);
                continue;
            }

            if (!Identifiers.IsIdentifier(part))
            {
                error = part.IndexOf('*') >= 0
                    ? $"'{part}' mixes a name and '*'; a segment is either a name, '*' (one segment) or '**' (any number)"
                    : $"'{part}' is not a valid namespace name";
                return null;
            }

            segments.Add(part);
            specificity++;
        }

        if (segments[segments.Count - 1] != AnySegments)
        {
            segments.Add(AnySegments);
        }

        error = null;
        return new NamespacePattern(text, segments.ToArray(), specificity);
    }

    /// <summary>Whether <paramref name="namespaceSegments"/> (the global namespace is empty) is matched.</summary>
    public bool Matches(IReadOnlyList<string> namespaceSegments) => Matches(0, namespaceSegments, 0);

    /// <summary>Whether some namespace is matched by both patterns.</summary>
    public bool Overlaps(NamespacePattern other) => Overlaps(other, 0, 0);

    /// <inheritdoc />
    public override string ToString() => Text;

    private bool Matches(int p, IReadOnlyList<string> name, int n)
    {
        while (p < _segments.Length)
        {
            var segment = _segments[p];
            if (segment == AnySegments)
            {
                // Patterns are short and rarely have more than the trailing `**`, so plain backtracking is fine.
                for (var skip = n; skip <= name.Count; skip++)
                {
                    if (Matches(p + 1, name, skip))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (n >= name.Count || (segment != AnySegment && !string.Equals(segment, name[n], StringComparison.Ordinal)))
            {
                return false;
            }

            p++;
            n++;
        }

        return n == name.Count;
    }

    private bool Overlaps(NamespacePattern other, int i, int j)
    {
        var a = _segments;
        var b = other._segments;
        if (i == a.Length && j == b.Length)
        {
            return true;
        }

        if (i < a.Length && a[i] == AnySegments)
        {
            // `**` matches nothing, or takes the segment b produces next.
            return Overlaps(other, i + 1, j) || (j < b.Length && Overlaps(other, i, j + 1));
        }

        if (j < b.Length && b[j] == AnySegments)
        {
            return Overlaps(other, i, j + 1) || (i < a.Length && Overlaps(other, i + 1, j));
        }

        if (i == a.Length || j == b.Length)
        {
            return false;
        }

        var compatible = a[i] == AnySegment || b[j] == AnySegment || string.Equals(a[i], b[j], StringComparison.Ordinal);
        return compatible && Overlaps(other, i + 1, j + 1);
    }
}
