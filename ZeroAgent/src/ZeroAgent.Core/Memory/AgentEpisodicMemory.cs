using System;
using System.Collections.Generic;
using ZeroAgent.Core.Embedding;
using ZeroVector.Core.Indices;
using ZeroVector.Core.Metrics;
using ZeroVector.Core.Results;

namespace ZeroAgent.Core.Memory
{
    /// <summary>
    /// Long-term episodic and semantic memory engine for AI Agents backed by ZeroVector.
    /// Provides sub-millisecond retrieval of relevant past experiences, documents, and rules.
    /// </summary>
    public sealed class AgentEpisodicMemory
    {
        private readonly IVectorIndex _index;
        private readonly Dictionary<int, string> _memoryStore = new Dictionary<int, string>();
        private int _nextId = 1;
        private readonly object _lock = new object();

        public int Count => _index.Count;
        public int Dimension => _index.Dimension;

        public AgentEpisodicMemory(int dimension, bool useHnsw = false)
        {
            if (useHnsw)
            {
                _index = new HnswVectorIndex(dimension, m: 16, efConstruction: 100, efSearch: 50, metric: VectorMetricType.Cosine);
            }
            else
            {
                _index = new FlatVectorIndex(dimension, initialCapacity: 128, defaultMetric: VectorMetricType.Cosine);
            }
        }

        public int Remember(string text, ReadOnlySpan<float> embedding)
        {
            if (string.IsNullOrWhiteSpace(text)) return -1;

            lock (_lock)
            {
                int id = _nextId++;
                _index.Add(id, embedding);
                _memoryStore[id] = text;
                return id;
            }
        }

        public List<(string Memory, float SimilarityScore)> Recall(ReadOnlySpan<float> queryEmbedding, int topK = 5)
        {
            if (topK <= 0) return new List<(string, float)>();

            VectorSearchResult[] matches;
            lock (_lock)
            {
                matches = _index.SearchTopK(queryEmbedding, topK);
            }

            var results = new List<(string, float)>(matches.Length);
            lock (_lock)
            {
                foreach (var match in matches)
                {
                    if (_memoryStore.TryGetValue(match.Id, out string? text))
                    {
                        results.Add((text, match.Score));
                    }
                }
            }

            return results;
        }

        public int Remember(string text, ITextEmbedder embedder)
        {
            if (embedder == null) throw new ArgumentNullException(nameof(embedder));
            if (string.IsNullOrWhiteSpace(text)) return -1;
            var vec = embedder.Embed(text);
            return Remember(text, vec);
        }

        public List<(string Memory, float SimilarityScore)> Recall(string query, ITextEmbedder embedder, int topK = 5)
        {
            if (embedder == null) throw new ArgumentNullException(nameof(embedder));
            if (string.IsNullOrWhiteSpace(query) || topK <= 0) return new List<(string, float)>();
            var vec = embedder.Embed(query);
            return Recall(vec, topK);
        }

        public void Clear()
        {
            lock (_lock)
            {
                _index.Clear();
                _memoryStore.Clear();
                _nextId = 1;
            }
        }
    }
}
