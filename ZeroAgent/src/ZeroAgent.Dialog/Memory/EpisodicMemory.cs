using System;
using System.Collections.Generic;
using ZeroVector.Core.Indices;
using ZeroVector.Core.Metrics;
using ZeroVector.Core.Results;

namespace ZeroAgent.Dialog.Memory
{
    public sealed class IncidentEpisode
    {
        public int Id { get; }
        public string Issue { get; }
        public string Resolution { get; }
        public bool Success { get; }
        public DateTime TimestampUtc { get; }
        public DateTime LastAccessedUtc { get; set; }
        public int AccessCount { get; set; }
        public double HalfLifeHours { get; set; }

        public IncidentEpisode(int id, string issue, string resolution, bool success, double halfLifeHours = 72.0)
        {
            Id = id;
            Issue = issue;
            Resolution = resolution;
            Success = success;
            TimestampUtc = DateTime.UtcNow;
            LastAccessedUtc = TimestampUtc;
            AccessCount = 1;
            HalfLifeHours = halfLifeHours;
        }

        /// <summary>
        /// Calculates the continuous Ebbinghaus memory retention score in [0.0, 1.0].
        /// Frequency reinforcement: Repeated accesses extend memory half-life dynamically.
        /// </summary>
        public float ComputeRetention(DateTime nowUtc)
        {
            double deltaHours = (nowUtc - LastAccessedUtc).TotalHours;
            if (deltaHours <= 0) return 1.0f;

            // Frequency boost: Access count extends retention half-life
            double freqBoost = 1.0 + 0.5 * Math.Log(1.0 + AccessCount, 2);
            double tau = HalfLifeHours * freqBoost;

            double retention = Math.Exp(-deltaHours / tau);
            return (float)Math.Max(0.0, Math.Min(1.0, retention));
        }
    }

    /// <summary>
    /// Episodic Memory (Experience & Historical Incidents Memory).
    /// Stores past diagnostics, equipment faults, and human-verified resolutions backed by TwoStageVectorIndex
    /// and continuous Ebbinghaus cognitive decay.
    /// </summary>
    public sealed class EpisodicMemory
    {
        private readonly IVectorIndex _vectorIndex;
        private readonly Dictionary<int, IncidentEpisode> _episodes = new Dictionary<int, IncidentEpisode>();
        private int _nextId = 1;
        private readonly object _lock = new object();

        public int Count => _episodes.Count;

        public EpisodicMemory(int dimension = 128) : this(new TwoStageVectorIndex(dimension, 64, VectorMetricType.Cosine, QuantizationStorageMode.ExactFp32, oversampleFactor: 4))
        {
        }

        public EpisodicMemory(IVectorIndex vectorIndex)
        {
            _vectorIndex = vectorIndex ?? throw new ArgumentNullException(nameof(vectorIndex));
        }

        public IncidentEpisode Record(string issue, string resolution, ReadOnlySpan<float> embedding, bool success = true, double halfLifeHours = 72.0)
        {
            if (string.IsNullOrWhiteSpace(issue)) throw new ArgumentNullException(nameof(issue));

            lock (_lock)
            {
                int id = _nextId++;
                _vectorIndex.Add(id, embedding);
                var ep = new IncidentEpisode(id, issue, resolution, success, halfLifeHours);
                _episodes[id] = ep;
                return ep;
            }
        }

        /// <summary>
        /// Recalls past episodes scoring with fused vector similarity, Ebbinghaus recency decay, and frequency boost.
        /// </summary>
        public List<(IncidentEpisode Episode, float Similarity)> Recall(
            ReadOnlySpan<float> queryEmbedding,
            int topK = 3,
            float minScore = 0.35f,
            bool updateAccess = true)
        {
            VectorSearchResult[] matches;
            lock (_lock)
            {
                matches = _vectorIndex.SearchTopK(queryEmbedding, topK * 2);
            }

            var now = DateTime.UtcNow;
            var results = new List<(IncidentEpisode Episode, float Similarity)>();

            lock (_lock)
            {
                for (int i = 0; i < matches.Length; i++)
                {
                    var m = matches[i];
                    if (_episodes.TryGetValue(m.Id, out var ep))
                    {
                        float retention = ep.ComputeRetention(now);
                        float freqBoost = 1.0f + 0.15f * (float)Math.Log(1.0 + ep.AccessCount, 2);
                        float cognitiveScore = m.Score * retention * freqBoost;

                        if (cognitiveScore >= minScore)
                        {
                            if (updateAccess)
                            {
                                ep.AccessCount++;
                                ep.LastAccessedUtc = now;
                            }
                            results.Add((ep, cognitiveScore));
                        }
                    }
                }
            }

            results.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            if (results.Count > topK)
            {
                results.RemoveRange(topK, results.Count - topK);
            }

            return results;
        }

        /// <summary>
        /// Prunes decayed episodes whose Ebbinghaus retention falls below the minimum threshold.
        /// </summary>
        public int PruneExpiredEpisodes(DateTime nowUtc, float minRetentionThreshold = 0.05f)
        {
            lock (_lock)
            {
                var toRemove = new List<int>();
                foreach (var kvp in _episodes)
                {
                    if (kvp.Value.ComputeRetention(nowUtc) < minRetentionThreshold)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }

                for (int i = 0; i < toRemove.Count; i++)
                {
                    _episodes.Remove(toRemove[i]);
                }

                return toRemove.Count;
            }
        }

        public IReadOnlyList<IncidentEpisode> GetAllEpisodes()
        {
            lock (_lock)
            {
                return new List<IncidentEpisode>(_episodes.Values);
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
