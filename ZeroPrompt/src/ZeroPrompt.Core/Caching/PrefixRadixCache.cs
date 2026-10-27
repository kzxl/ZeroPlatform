using System;
using System.Collections.Generic;

namespace ZeroPrompt.Core.Caching
{
    /// <summary>
    /// High-performance Prefix Radix Tree (Trie) for caching KV states, pre-tokenized chunks,
    /// or intermediate prompt evaluations based on token ID prefixes.
    /// Operates with sub-microsecond prefix match lookups.
    /// </summary>
    /// <typeparam name="TPayload">The cached data type (e.g. KV-cache handle, tensor buffer, or response template).</typeparam>
    public sealed class PrefixRadixCache<TPayload> where TPayload : class
    {
        private sealed class RadixNode
        {
            public int TokenId { get; }
            public TPayload? Payload { get; set; }
            public long LastAccessedTicks { get; set; }
            public Dictionary<int, RadixNode> Children { get; } = new Dictionary<int, RadixNode>();

            public RadixNode(int tokenId)
            {
                TokenId = tokenId;
                LastAccessedTicks = DateTime.UtcNow.Ticks;
            }
        }

        private readonly RadixNode _root = new RadixNode(-1);
        private readonly object _syncLock = new object();
        private readonly int _maxEntries;
        private int _entryCount = 0;

        public int Count => _entryCount;

        public PrefixRadixCache(int maxEntries = 10000)
        {
            _maxEntries = Math.Max(16, maxEntries);
        }

        /// <summary>
        /// Inserts or updates a payload associated with a specific token sequence.
        /// </summary>
        public void Insert(IReadOnlyList<int> tokens, TPayload payload)
        {
            if (tokens == null || tokens.Count == 0 || payload == null) return;

            lock (_syncLock)
            {
                var current = _root;
                for (int i = 0; i < tokens.Count; i++)
                {
                    int token = tokens[i];
                    if (!current.Children.TryGetValue(token, out var next))
                    {
                        next = new RadixNode(token);
                        current.Children[token] = next;
                    }
                    current = next;
                }

                if (current.Payload == null)
                {
                    _entryCount++;
                }

                current.Payload = payload;
                current.LastAccessedTicks = DateTime.UtcNow.Ticks;
            }
        }

        /// <summary>
        /// Finds the longest matching prefix for the given token sequence.
        /// Returns the number of matched tokens and the deepest cached payload found.
        /// </summary>
        public (int MatchedTokens, TPayload? Payload) MatchLongestPrefix(IReadOnlyList<int> tokens)
        {
            if (tokens == null || tokens.Count == 0) return (0, null);

            lock (_syncLock)
            {
                var current = _root;
                int matchedCount = 0;
                TPayload? deepestPayload = null;
                int deepestMatchLength = 0;

                for (int i = 0; i < tokens.Count; i++)
                {
                    int token = tokens[i];
                    if (!current.Children.TryGetValue(token, out var next))
                    {
                        break;
                    }

                    current = next;
                    matchedCount++;
                    current.LastAccessedTicks = DateTime.UtcNow.Ticks;

                    if (current.Payload != null)
                    {
                        deepestPayload = current.Payload;
                        deepestMatchLength = matchedCount;
                    }
                }

                return (deepestMatchLength, deepestPayload);
            }
        }

        /// <summary>
        /// Clears all entries from the Radix tree.
        /// </summary>
        public void Clear()
        {
            lock (_syncLock)
            {
                _root.Children.Clear();
                _entryCount = 0;
            }
        }
    }
}
