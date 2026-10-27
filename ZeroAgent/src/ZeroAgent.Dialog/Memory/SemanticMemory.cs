using System;
using System.Collections.Generic;
using ZeroVector.Core.Indices;
using ZeroVector.Core.Metrics;
using ZeroVector.Core.Results;

namespace ZeroAgent.Dialog.Memory
{
    public sealed class SemanticKnowledgeItem
    {
        public int Id { get; }
        public string Title { get; }
        public string Content { get; }
        public string Category { get; }
        public DateTime CreatedAtUtc { get; } = DateTime.UtcNow;
        public DateTime? ValidFromUtc { get; set; }
        public DateTime? ValidUntilUtc { get; set; }

        public SemanticKnowledgeItem(int id, string title, string content, string category, DateTime? validFromUtc = null, DateTime? validUntilUtc = null)
        {
            Id = id;
            Title = title;
            Content = content;
            Category = category;
            ValidFromUtc = validFromUtc;
            ValidUntilUtc = validUntilUtc;
        }

        public bool IsValidAt(DateTime? asOfUtc = null)
        {
            var now = asOfUtc ?? DateTime.UtcNow;
            if (ValidFromUtc.HasValue && now < ValidFromUtc.Value) return false;
            if (ValidUntilUtc.HasValue && now > ValidUntilUtc.Value) return false;
            return true;
        }
    }

    /// <summary>
    /// Semantic Memory (Domain Knowledge, SOP Manuals & Technical Specifications).
    /// Indexed via ZeroVector with bi-temporal validation to prevent obsolete SOPs from surfacing.
    /// </summary>
    public sealed class SemanticMemory
    {
        private readonly IVectorIndex _vectorIndex;
        private readonly Dictionary<int, SemanticKnowledgeItem> _items = new Dictionary<int, SemanticKnowledgeItem>();
        private int _nextId = 1;
        private readonly object _lock = new object();

        public int Count => _items.Count;

        public SemanticMemory(int dimension = 128) : this(new TwoStageVectorIndex(dimension, 64, VectorMetricType.Cosine, QuantizationStorageMode.ExactFp32, oversampleFactor: 4))
        {
        }

        public SemanticMemory(IVectorIndex vectorIndex)
        {
            _vectorIndex = vectorIndex ?? throw new ArgumentNullException(nameof(vectorIndex));
        }

        public SemanticKnowledgeItem Add(
            string title, 
            string content, 
            ReadOnlySpan<float> embedding, 
            string category = "SOP",
            DateTime? validFromUtc = null,
            DateTime? validUntilUtc = null)
        {
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentNullException(nameof(title));

            lock (_lock)
            {
                int id = _nextId++;
                _vectorIndex.Add(id, embedding);
                var item = new SemanticKnowledgeItem(id, title, content, category, validFromUtc, validUntilUtc);
                _items[id] = item;
                return item;
            }
        }

        public List<(SemanticKnowledgeItem Item, float Similarity)> Query(
            ReadOnlySpan<float> queryEmbedding, 
            int topK = 3, 
            float minScore = 0.45f,
            DateTime? asOfUtc = null,
            bool includeExpired = false)
        {
            VectorSearchResult[] matches;
            lock (_lock)
            {
                matches = _vectorIndex.SearchTopK(queryEmbedding, topK);
            }

            var results = new List<(SemanticKnowledgeItem, float)>(matches.Length);
            lock (_lock)
            {
                for (int i = 0; i < matches.Length; i++)
                {
                    var m = matches[i];
                    if (m.Score >= minScore && _items.TryGetValue(m.Id, out var item))
                    {
                        if (includeExpired || item.IsValidAt(asOfUtc))
                        {
                            results.Add((item, m.Score));
                        }
                    }
                }
            }

            return results;
        }

        public IReadOnlyList<SemanticKnowledgeItem> GetAllItems()
        {
            lock (_lock)
            {
                return new List<SemanticKnowledgeItem>(_items.Values);
            }
        }

        public bool TryGetEmbedding(int id, Span<float> destination)
        {
            lock (_lock)
            {
                return _vectorIndex.TryGet(id, destination);
            }
        }
    }
}
