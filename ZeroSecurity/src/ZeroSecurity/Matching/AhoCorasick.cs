using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroSecurity.Matching;

/// <summary>
/// Result of an Aho-Corasick pattern match.
/// </summary>
public readonly struct PatternMatch
{
    public string Keyword { get; }
    public int Index { get; }

    public PatternMatch(string keyword, int index)
    {
        Keyword = keyword;
        Index = index;
    }

    public override string ToString() => $"{Keyword} at {Index}";
}

/// <summary>
/// Pure C# Aho-Corasick string matching automaton.
/// Finds multiple keywords or signature strings simultaneously in a single linear-time pass O(N + M).
/// </summary>
public sealed class AhoCorasick
{
    private sealed class Node
    {
        public Dictionary<byte, Node> Transitions { get; } = new();
        public Node? Failure { get; set; }
        public List<string> Output { get; } = new();
    }

    private readonly Node _root;

    public AhoCorasick(IEnumerable<string> keywords)
    {
        if (keywords == null) throw new ArgumentNullException(nameof(keywords));

        _root = new Node();

        // 1. Build Trie
        foreach (var kw in keywords)
        {
            if (string.IsNullOrEmpty(kw)) continue;

            byte[] bytes = Encoding.UTF8.GetBytes(kw);
            var current = _root;
            foreach (byte b in bytes)
            {
                if (!current.Transitions.TryGetValue(b, out var next))
                {
                    next = new Node();
                    current.Transitions[b] = next;
                }
                current = next;
            }
            current.Output.Add(kw);
        }

        // 2. Build Failure Links with BFS
        var queue = new Queue<Node>();
        foreach (var kvp in _root.Transitions)
        {
            kvp.Value.Failure = _root;
            queue.Enqueue(kvp.Value);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var kvp in current.Transitions)
            {
                byte b = kvp.Key;
                var child = kvp.Value;

                var fallback = current.Failure;
                while (fallback != null && !fallback.Transitions.ContainsKey(b))
                {
                    fallback = fallback.Failure;
                }

                child.Failure = fallback != null ? fallback.Transitions[b] : _root;

                // Inherit outputs from failure link
                if (child.Failure != null && child.Failure.Output.Count > 0)
                {
                    child.Output.AddRange(child.Failure.Output);
                }

                queue.Enqueue(child);
            }
        }
    }

    /// <summary>
    /// Searches the input byte span for any configured patterns in a single pass.
    /// </summary>
    public List<PatternMatch> Search(ReadOnlySpan<byte> text)
    {
        var matches = new List<PatternMatch>();
        var current = _root;

        for (int i = 0; i < text.Length; i++)
        {
            byte b = text[i];

            while (current != _root && !current.Transitions.ContainsKey(b))
            {
                current = current.Failure ?? _root;
            }

            if (current.Transitions.TryGetValue(b, out var next))
            {
                current = next;
            }
            else
            {
                current = _root;
            }

            if (current.Output.Count > 0)
            {
                foreach (var match in current.Output)
                {
                    int matchLen = Encoding.UTF8.GetByteCount(match);
                    matches.Add(new PatternMatch(match, i - matchLen + 1));
                }
            }
        }

        return matches;
    }

    /// <summary>
    /// Searches text string for any configured patterns.
    /// </summary>
    public List<PatternMatch> Search(string text)
    {
        if (string.IsNullOrEmpty(text)) return new List<PatternMatch>();
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        return Search(bytes);
    }
}
