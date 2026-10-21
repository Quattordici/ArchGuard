using System;
using System.Collections.Generic;

namespace Archie.Core.Rules;

internal static class Identifiers
{
    /// <summary>A C# identifier without <c>@</c>: a letter or '_' first, then letters, digits or '_'. Layer names and namespace segments.</summary>
    public static bool IsIdentifier(string text)
    {
        if (text.Length == 0 || !(char.IsLetter(text[0]) || text[0] == '_'))
        {
            return false;
        }

        for (var i = 1; i < text.Length; i++)
        {
            if (!(char.IsLetterOrDigit(text[i]) || text[i] == '_'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The closest candidate within a typo's distance (one edit per three characters, at least one), or <see langword="null"/>.</summary>
    public static string? Closest(string text, IEnumerable<string> candidates)
    {
        string? best = null;
        var bestDistance = int.MaxValue;
        foreach (var candidate in candidates)
        {
            var distance = Distance(text, candidate);
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        var allowed = Math.Max(1, text.Length / 3);
        return bestDistance <= allowed ? best : null;
    }

    /// <summary>Levenshtein distance, ignoring case so "MayDependOn" suggests "mayDependOn".</summary>
    private static int Distance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = char.ToUpperInvariant(a[i - 1]) == char.ToUpperInvariant(b[j - 1]) ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
