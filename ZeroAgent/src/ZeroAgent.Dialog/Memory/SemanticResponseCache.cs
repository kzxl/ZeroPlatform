using System;
using System.Collections.Generic;
using ZeroVector.Core.Indices;
using ZeroVector.Core.Metrics;

namespace ZeroAgent.Dialog.Memory
{
    /// <summary>
    /// Represents a cached response entry in the semantic cache.
    /// </summary>
    public sealed class SemanticCacheEntry
    {
        public int Id { get; }
        public string Query { get; }
        public string ResponseText { get; }
        public string? IntentName { get; }
        public DateTime CreatedAt { get; }
        public TimeSpan Ttl { get; }
        public float Similarity { get; set; }

        public bool IsExpired => DateTime.UtcNow - CreatedAt > Ttl;

        public SemanticCacheEntry(int id, string query, string responseText, string? intentName, TimeSpan ttl)
        {
            Id = id;
            Query = query ?? string.Empty;
            ResponseText = responseText ?? string.Empty;
            IntentName = intentName;
            CreatedAt = DateTime.UtcNow;
            Ttl = ttl;
        }
    }

    /// <summary>
    /// Sub-millisecond Semantic Response Cache utilizing ZeroVector TwoStageVectorIndex (1-Bit BQ + SIMD Cosine).
    /// Bypasses costly intent classification and LLM reasoning for semantically identical questions.
    /// </summary>
    public sealed class SemanticResponseCache
    {
        private readonly TwoStageVectorIndex _index;
        private readonly Dictionary<int, SemanticCacheEntry> _entries = new Dictionary<int, SemanticCacheEntry>();
        private readonly object _lock = new object();
        private int _idSequence = 0;
        private readonly TimeSpan _defaultTtl;

        public int Count
        {
            get
            {
                lock (_lock) return _entries.Count;
            }
        }

        public SemanticResponseCache(int dimension = 128, TimeSpan? defaultTtl = null)
        {
            _index = new TwoStageVectorIndex(dimension, initialCapacity: 64, defaultMetric: VectorMetricType.Cosine);
            _defaultTtl = defaultTtl ?? TimeSpan.FromMinutes(10);
        }

        /// <summary>
        /// Stores a response associated with a query embedding vector.
        /// </summary>
        public void Store(ReadOnlySpan<float> queryVector, string query, string responseText, string? intentName = null, TimeSpan? ttl = null)
        {
            lock (_lock)
            {
                int id = ++_idSequence;
                var entry = new SemanticCacheEntry(id, query, responseText, intentName, ttl ?? _defaultTtl);
                _entries[id] = entry;
                _index.Add(id, queryVector);
            }
        }

        /// <summary>
        /// Attempts to retrieve a cached response if a semantic match with similarity >= minSimilarity is found.
        /// Validates parameterized entity tokens (such as machine IDs or numbers) to prevent cross-entity collisions.
        /// </summary>
        public bool TryGet(ReadOnlySpan<float> queryVector, string query, float minSimilarity, out SemanticCacheEntry? hit)
        {
            hit = null;
            lock (_lock)
            {
                if (_entries.Count == 0) return false;

                var results = _index.SearchTopK(queryVector, k: 1);
                if (results.Length > 0 && results[0].Score >= minSimilarity)
                {
                    int id = results[0].Id;
                    if (_entries.TryGetValue(id, out var entry))
                    {
                        if (!entry.IsExpired)
                        {
                            if (AreEntitiesCompatible(query, entry.Query))
                            {
                                entry.Similarity = results[0].Score;
                                hit = entry;
                                return true;
                            }
                        }
                    }
                }
                return false;
            }
        }

        public bool TryGet(ReadOnlySpan<float> queryVector, float minSimilarity, out SemanticCacheEntry? hit)
            => TryGet(queryVector, string.Empty, minSimilarity, out hit);

        private static bool AreEntitiesCompatible(string q1, string q2)
        {
            if (string.Equals(q1, q2, StringComparison.OrdinalIgnoreCase)) return true;

            var tokens1 = ExtractEntityTokens(q1);
            var tokens2 = ExtractEntityTokens(q2);

            if (tokens1.Count != tokens2.Count) return false;

            foreach (var t in tokens1)
            {
                if (!tokens2.Contains(t)) return false;
            }
            return true;
        }

        private static HashSet<string> ExtractEntityTokens(string text)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text)) return set;

            var parts = text.Split(new[] { ' ', ',', ';', ':', '?', '!' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                bool hasDigit = false;
                for (int i = 0; i < p.Length; i++)
                {
                    if (char.IsDigit(p[i])) { hasDigit = true; break; }
                }

                if (hasDigit)
                {
                    set.Add(p);
                }
            }
            return set;
        }

        /// <summary>
        /// Evicts all expired cache entries to preserve memory.
        /// </summary>
        public int PruneExpired()
        {
            lock (_lock)
            {
                var expiredIds = new List<int>();
                foreach (var kvp in _entries)
                {
                    if (kvp.Value.IsExpired)
                    {
                        expiredIds.Add(kvp.Key);
                    }
                }

                foreach (var id in expiredIds)
                {
                    _entries.Remove(id);
                }

                return expiredIds.Count;
            }
        }

        /// <summary>
        /// Clears all entries from the semantic cache.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _entries.Clear();
                _index.Clear();
            }
        }

        public IReadOnlyList<SemanticCacheEntry> GetAllEntries()
        {
            lock (_lock)
            {
                return new List<SemanticCacheEntry>(_entries.Values);
            }
        }

        public bool TryGetEmbedding(int id, Span<float> destination)
        {
            lock (_lock)
            {
                return _index.TryGet(id, destination);
            }
        }
    }
}
