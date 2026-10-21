using System;
using System.Collections.Generic;
using Archie.Core.Config;
using Archie.Core.Rules;

namespace Archie.Analyzers;

/// <summary>
/// Parsed configs keyed by the file's content. The IDE starts a compilation for every project on every edit, and they all read the same
/// archie.json, so it is parsed once per content rather than once per compilation. Small and bounded: a session sees a handful of configs.
/// </summary>
internal static class ConfigCache
{
    private const int Capacity = 8;

    private static readonly object Gate = new();
    private static readonly LinkedList<Entry> Entries = new();

    public static Entry Get(string text)
    {
        lock (Gate)
        {
            for (var node = Entries.First; node is not null; node = node.Next)
            {
                if (string.Equals(node.Value.Text, text, StringComparison.Ordinal))
                {
                    Entries.Remove(node);
                    Entries.AddFirst(node);
                    return node.Value;
                }
            }
        }

        // Parsed outside the lock; two threads may parse the same text, and either result is fine.
        var entry = new Entry(text, ConfigParser.Parse(text));
        lock (Gate)
        {
            Entries.AddFirst(entry);
            if (Entries.Count > Capacity)
            {
                Entries.RemoveLast();
            }
        }

        return entry;
    }

    internal sealed class Entry
    {
        public Entry(string text, ConfigResult result)
        {
            Text = text;
            Result = result;
            Evaluator = result.Model is null ? null : new ArchEvaluator(result.Model);
        }

        public string Text { get; }

        public ConfigResult Result { get; }

        /// <summary>Shared by every compilation with this config; its namespace cache is keyed by name, so that is safe.</summary>
        public ArchEvaluator? Evaluator { get; }
    }
}
