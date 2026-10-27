using System;
using System.Collections.Generic;

namespace ZeroNetwork.PubSub
{
    /// <summary>
    /// High-performance, zero-allocation topic tokenizer operating directly on spans.
    /// Replaces BCL string.Split('/') on hot paths.
    /// </summary>
    public ref struct TopicSpanTokenizer
    {
        private ReadOnlySpan<char> _remaining;

        /// <summary>
        /// Initializes a new instance with the specified topic character span.
        /// </summary>
        public TopicSpanTokenizer(ReadOnlySpan<char> span)
        {
            _remaining = span;
        }

        /// <summary>
        /// Attempts to extract the next hierarchical topic segment.
        /// </summary>
        /// <param name="segment">Extracted segment span.</param>
        /// <returns><c>true</c> if a segment was extracted; <c>false</c> if end reached.</returns>
        public bool TryGetNext(out ReadOnlySpan<char> segment)
        {
            if (_remaining.IsEmpty)
            {
                segment = default;
                return false;
            }

            int slashIndex = _remaining.IndexOf('/');
            if (slashIndex >= 0)
            {
                segment = _remaining.Slice(0, slashIndex);
                _remaining = _remaining.Slice(slashIndex + 1);
            }
            else
            {
                segment = _remaining;
                _remaining = default;
            }
            return true;
        }
    }

    /// <summary>
    /// Thread-safe Trie data structure for MQTT-style topic routing.
    /// Supports exact matching, single-level wildcards ('+'), and multi-level wildcards ('#').
    /// Optimized with Copy-On-Write flat child arrays and zero-allocation span traversal on hot paths.
    /// </summary>
    /// <typeparam name="TSubscriber">Type of the subscriber token/handler.</typeparam>
    public sealed class TopicTrie<TSubscriber>
    {
        private sealed class TrieNode
        {
            public struct ChildEntry
            {
                public string Segment;
                public TrieNode Node;
            }

            // Copy-On-Write contiguous array for cache-friendly, lock-free span lookup
            public volatile ChildEntry[] FlatChildren = Array.Empty<ChildEntry>();
            public TrieNode? SingleWildcardChild; // '+'
            public TrieNode? MultiWildcardChild;  // '#'

            // Copy-On-Write array for lock-free read iteration
            public volatile TSubscriber[] Subscribers = Array.Empty<TSubscriber>();
            public readonly object SyncRoot = new object();

            public TrieNode GetOrAddChild(string segment)
            {
                lock (SyncRoot)
                {
                    for (int i = 0; i < FlatChildren.Length; i++)
                    {
                        if (string.Equals(FlatChildren[i].Segment, segment, StringComparison.Ordinal))
                        {
                            return FlatChildren[i].Node;
                        }
                    }

                    var newNode = new TrieNode();
                    var list = new List<ChildEntry>(FlatChildren)
                    {
                        new ChildEntry { Segment = segment, Node = newNode }
                    };
                    FlatChildren = list.ToArray();
                    return newNode;
                }
            }

            public bool TryGetChild(ReadOnlySpan<char> segment, out TrieNode? node)
            {
                var children = FlatChildren;
                for (int i = 0; i < children.Length; i++)
                {
                    if (segment.SequenceEqual(children[i].Segment.AsSpan()))
                    {
                        node = children[i].Node;
                        return true;
                    }
                }
                node = null;
                return false;
            }

            public bool RemoveChild(string segment)
            {
                lock (SyncRoot)
                {
                    var list = new List<ChildEntry>(FlatChildren);
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (string.Equals(list[i].Segment, segment, StringComparison.Ordinal))
                        {
                            list.RemoveAt(i);
                            FlatChildren = list.ToArray();
                            return true;
                        }
                    }
                    return false;
                }
            }
        }

        private readonly TrieNode _root = new TrieNode();

        /// <summary>
        /// Registers a subscriber for the specified topic pattern.
        /// </summary>
        /// <param name="pattern">Topic pattern, e.g. "sensors/+/temperature" or "factory/#".</param>
        /// <param name="subscriber">Subscriber instance.</param>
        public void Add(string pattern, TSubscriber subscriber)
        {
            if (string.IsNullOrEmpty(pattern))
                throw new ArgumentNullException(nameof(pattern));
            if (subscriber == null)
                throw new ArgumentNullException(nameof(subscriber));

            string[] segments = pattern.Split('/');
            ValidatePattern(segments);

            TrieNode current = _root;

            for (int i = 0; i < segments.Length; i++)
            {
                string seg = segments[i];

                if (seg == "+")
                {
                    if (current.SingleWildcardChild == null)
                    {
                        lock (current.SyncRoot)
                        {
                            if (current.SingleWildcardChild == null)
                                current.SingleWildcardChild = new TrieNode();
                        }
                    }
                    current = current.SingleWildcardChild;
                }
                else if (seg == "#")
                {
                    if (current.MultiWildcardChild == null)
                    {
                        lock (current.SyncRoot)
                        {
                            if (current.MultiWildcardChild == null)
                                current.MultiWildcardChild = new TrieNode();
                        }
                    }
                    current = current.MultiWildcardChild;
                    break; // '#' must be the last segment
                }
                else
                {
                    current = current.GetOrAddChild(seg);
                }
            }

            lock (current.SyncRoot)
            {
                var list = new List<TSubscriber>(current.Subscribers) { subscriber };
                current.Subscribers = list.ToArray();
            }
        }

        /// <summary>
        /// Removes a subscriber from the specified topic pattern.
        /// </summary>
        /// <param name="pattern">Topic pattern.</param>
        /// <param name="subscriber">Subscriber instance to remove.</param>
        /// <returns><c>true</c> if removed; otherwise <c>false</c>.</returns>
        public bool Remove(string pattern, TSubscriber subscriber)
        {
            if (string.IsNullOrEmpty(pattern) || subscriber == null)
                return false;

            string[] segments = pattern.Split('/');
            TrieNode? current = _root;

            for (int i = 0; i < segments.Length; i++)
            {
                string seg = segments[i];
                if (seg == "+")
                {
                    current = current.SingleWildcardChild;
                }
                else if (seg == "#")
                {
                    current = current.MultiWildcardChild;
                    break;
                }
                else
                {
                    if (!current.TryGetChild(seg.AsSpan(), out current))
                        return false;
                }

                if (current == null)
                    return false;
            }

            if (current == null) return false;

            lock (current.SyncRoot)
            {
                var list = new List<TSubscriber>(current.Subscribers);
                bool removed = list.Remove(subscriber);
                if (removed)
                {
                    current.Subscribers = list.ToArray();
                }
                return removed;
            }
        }

        /// <summary>
        /// Finds all subscribers whose patterns match the concrete published topic.
        /// Zero heap allocation on lookup.
        /// </summary>
        /// <param name="topic">Concrete topic, e.g. "sensors/line1/temperature".</param>
        /// <param name="results">List to append matched subscribers into.</param>
        public void GetMatches(string topic, List<TSubscriber> results)
        {
            if (string.IsNullOrEmpty(topic) || results == null)
                return;

            GetMatches(topic.AsSpan(), results);
        }

        /// <summary>
        /// Finds all subscribers matching the concrete topic span without heap allocations.
        /// </summary>
        public void GetMatches(ReadOnlySpan<char> topic, List<TSubscriber> results)
        {
            if (topic.IsEmpty || results == null)
                return;

            CollectMatches(_root, topic, results);
        }

        private static void CollectMatches(TrieNode current, ReadOnlySpan<char> remainingTopic, List<TSubscriber> results)
        {
            // 1. If this node has a '#' wildcard child, it matches this segment and all subsequent segments
            if (current.MultiWildcardChild != null)
            {
                var multiSubs = current.MultiWildcardChild.Subscribers;
                if (multiSubs.Length > 0)
                {
                    results.AddRange(multiSubs);
                }
            }

            // 2. If we reached the end of the topic, collect exact subscribers at this node
            if (remainingTopic.IsEmpty)
            {
                var exactSubs = current.Subscribers;
                if (exactSubs.Length > 0)
                {
                    results.AddRange(exactSubs);
                }
                return;
            }

            // Extract next segment using zero-alloc slice
            int slashIndex = remainingTopic.IndexOf('/');
            ReadOnlySpan<char> segment;
            ReadOnlySpan<char> nextRemaining;

            if (slashIndex >= 0)
            {
                segment = remainingTopic.Slice(0, slashIndex);
                nextRemaining = remainingTopic.Slice(slashIndex + 1);
            }
            else
            {
                segment = remainingTopic;
                nextRemaining = default;
            }

            // 3. Exact literal segment match via flat child array (zero-alloc SequenceEqual)
            if (current.TryGetChild(segment, out var literalChild) && literalChild != null)
            {
                CollectMatches(literalChild, nextRemaining, results);
            }

            // 4. Single-level '+' wildcard match (matches exactly 1 segment)
            if (current.SingleWildcardChild != null)
            {
                CollectMatches(current.SingleWildcardChild, nextRemaining, results);
            }
        }

        /// <summary>
        /// Tests if a topic pattern matches a concrete topic without heap allocation.
        /// </summary>
        public static bool IsMatch(string pattern, string topic)
        {
            if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(topic))
                return false;

            return IsMatch(pattern.AsSpan(), topic.AsSpan());
        }

        /// <summary>
        /// Tests if a topic pattern span matches a concrete topic span without heap allocation.
        /// </summary>
        public static bool IsMatch(ReadOnlySpan<char> pattern, ReadOnlySpan<char> topic)
        {
            var pTok = new TopicSpanTokenizer(pattern);
            var tTok = new TopicSpanTokenizer(topic);

            while (pTok.TryGetNext(out var pSeg))
            {
                if (pSeg.Length == 1 && pSeg[0] == '#')
                {
                    return true; // '#' at end matches everything remaining
                }

                if (!tTok.TryGetNext(out var tSeg))
                {
                    return false; // Topic ended prematurely
                }

                if (!(pSeg.Length == 1 && pSeg[0] == '+') && !pSeg.SequenceEqual(tSeg))
                {
                    return false;
                }
            }

            // Both must be exhausted (unless pattern ended with '#')
            return !tTok.TryGetNext(out _);
        }

        private static void ValidatePattern(string[] segments)
        {
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == "#" && i != segments.Length - 1)
                {
                    throw new ArgumentException("Multi-level wildcard '#' can only appear as the last segment of the pattern.", nameof(segments));
                }
            }
        }
    }
}
