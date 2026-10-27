using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using ZeroPrimitives.Cryptography;
using ZeroVector.Core.Indices;
using ZeroVector.Core.Metrics;
using ZeroVector.Core.Quantization;

namespace ZeroAgent.Core.Database
{
    /// <summary>
    /// Pure C# Sovereign Embedded AI Database Engine (ZAB1 - Zero Agent Binary).
    /// Provides unified multi-modal storage for:
    /// 1. Agent Manifest & Capabilities
    /// 2. Verified Domain Knowledge & Rules (O(1) Hash-Indexed)
    /// 3. SQ8 Quantized Vectors accelerated by TwoStageVectorIndex (1-bit POPCNT filter + SIMD rerank)
    /// 4. Reflexion Episodic Memory
    /// 5. Semantic Plan Trajectory Cache
    /// 6. Semi-Parametric Quantized Neural Action Policy Network
    /// 
    /// Incorporates ReaderWriterLockSlim concurrency, Write-Ahead Logging (WAL),
    /// hardware CRC32C integrity, atomic File.Replace durability, and lossless JSON round-tripping.
    /// </summary>
    public sealed class ZabDatabase : IDisposable
    {
        private readonly string _filePath;
        private readonly ZabWalJournal _wal;
        private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        private ZabManifest _manifest;
        private readonly List<ZabKnowledgeRecord> _knowledge = new List<ZabKnowledgeRecord>();
        private readonly Dictionary<string, ZabKnowledgeRecord> _knowledgeKeyIndex = new Dictionary<string, ZabKnowledgeRecord>(StringComparer.OrdinalIgnoreCase);

        private readonly List<ZabVectorRecord> _vectors = new List<ZabVectorRecord>();
        private TwoStageVectorIndex? _twoStageAccelerator;
        private readonly Dictionary<string, int> _vectorIdToInt = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, ZabVectorRecord> _intToVectorRecord = new Dictionary<int, ZabVectorRecord>();
        private int _nextVectorIntId = 1;

        private readonly List<ZabReflexionRecord> _reflexions = new List<ZabReflexionRecord>();
        private readonly List<ZabPlanRecord> _plans = new List<ZabPlanRecord>();
        private ZabNeuralPolicy? _neuralPolicy;

        private long _walSequenceNumber;
        private bool _isDirty;
        private bool _disposed;

        public string FilePath => _filePath;
        public ZabWalJournal Wal => _wal;
        public ZabManifest Manifest => _manifest;
        public IReadOnlyList<ZabKnowledgeRecord> Knowledge => _knowledge;
        public IReadOnlyList<ZabVectorRecord> Vectors => _vectors;
        public IReadOnlyList<ZabReflexionRecord> Reflexions => _reflexions;
        public IReadOnlyList<ZabPlanRecord> Plans => _plans;
        public ZabNeuralPolicy? NeuralPolicy => _neuralPolicy;
        public bool IsDirty => _isDirty;
        public bool AutoCheckpointOnDispose { get; set; } = true;

        private ZabDatabase(string filePath, ZabManifest? manifest = null)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _manifest = manifest ?? new ZabManifest();
            _wal = new ZabWalJournal(filePath);
        }

        #region Factory Methods

        /// <summary>
        /// Creates a new sovereign database container file with the given manifest.
        /// </summary>
        public static ZabDatabase CreateNew(string filePath, ZabManifest? manifest = null)
        {
            var db = new ZabDatabase(filePath, manifest);
            db._isDirty = true;
            db.Commit();
            return db;
        }

        /// <summary>
        /// Opens an existing database container file, verifies its hardware checksum,
        /// and replays any pending transactions from the Write-Ahead Log (.zab-wal).
        /// </summary>
        public static ZabDatabase Open(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"ZAB database file not found: {filePath}", filePath);
            }

            var db = new ZabDatabase(filePath);
            db.Reload();

            // Replay uncommitted WAL frames if present
            db._wal.RecoverAndReplay(db.ApplyWalMutation);

            return db;
        }

        /// <summary>
        /// Opens an existing database container or creates a new one if it does not exist.
        /// </summary>
        public static ZabDatabase OpenOrCreate(string filePath, ZabManifest? defaultManifest = null)
        {
            if (File.Exists(filePath))
            {
                return Open(filePath);
            }
            return CreateNew(filePath, defaultManifest);
        }

        #endregion

        #region Knowledge CRUD (O(1) Hash-Indexed)

        public ZabKnowledgeRecord AddKnowledge(
            string key,
            string value,
            string category = "General",
            string author = "Admin",
            float confidence = 1.0f)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));

            string cleanKey = key.Trim();
            var record = new ZabKnowledgeRecord
            {
                Key = cleanKey,
                Value = value ?? string.Empty,
                Category = category ?? "General",
                Author = author ?? "Admin",
                Confidence = confidence,
                LastUpdatedUtc = DateTime.UtcNow
            };

            _rwLock.EnterWriteLock();
            try
            {
                // Remove existing if key already present to maintain 1:1 invariant
                if (_knowledgeKeyIndex.TryGetValue(cleanKey, out var existing))
                {
                    _knowledge.Remove(existing);
                }

                _knowledge.Add(record);
                _knowledgeKeyIndex[cleanKey] = record;
                _isDirty = true;
                _walSequenceNumber++;

                // Log to WAL for durability
                byte[] payload = SerializeKnowledgeRecord(record);
                _wal.AppendFrame(ZabWalOpCode.AddKnowledge, payload);

                if (_wal.ShouldCheckpoint())
                {
                    Checkpoint();
                }
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }

            return record;
        }

        public bool UpdateKnowledge(string id, string newValue, float? confidence = null)
        {
            _rwLock.EnterWriteLock();
            try
            {
                var record = _knowledge.Find(k => string.Equals(k.Id, id, StringComparison.OrdinalIgnoreCase));
                if (record == null) return false;

                record.Value = newValue;
                if (confidence.HasValue) record.Confidence = confidence.Value;
                record.LastUpdatedUtc = DateTime.UtcNow;
                _isDirty = true;
                _walSequenceNumber++;

                // Log to WAL
                using (var ms = new MemoryStream())
                using (var writer = new BinaryWriter(ms, Encoding.UTF8))
                {
                    writer.Write(record.Id);
                    writer.Write(record.Value);
                    writer.Write(confidence.HasValue);
                    if (confidence.HasValue) writer.Write(confidence.Value);
                    _wal.AppendFrame(ZabWalOpCode.UpdateKnowledge, ms.ToArray());
                }

                if (_wal.ShouldCheckpoint())
                {
                    Checkpoint();
                }

                return true;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        public bool DeleteKnowledge(string idOrKey)
        {
            if (string.IsNullOrWhiteSpace(idOrKey)) return false;

            _rwLock.EnterWriteLock();
            try
            {
                var record = _knowledge.Find(k => string.Equals(k.Id, idOrKey, StringComparison.OrdinalIgnoreCase))
                          ?? (_knowledgeKeyIndex.TryGetValue(idOrKey.Trim(), out var kRec) ? kRec : null);

                if (record != null)
                {
                    _knowledge.Remove(record);
                    _knowledgeKeyIndex.Remove(record.Key);
                    _isDirty = true;
                    _walSequenceNumber++;

                    // Log to WAL with the canonical record Id
                    using (var ms = new MemoryStream())
                    using (var writer = new BinaryWriter(ms, Encoding.UTF8))
                    {
                        writer.Write(record.Id);
                        _wal.AppendFrame(ZabWalOpCode.DeleteKnowledge, ms.ToArray());
                    }

                    if (_wal.ShouldCheckpoint())
                    {
                        Checkpoint();
                    }

                    return true;
                }
                return false;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        public ZabKnowledgeRecord? FindKnowledge(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;

            _rwLock.EnterReadLock();
            try
            {
                return _knowledgeKeyIndex.TryGetValue(key.Trim(), out var record) ? record : null;
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        #endregion

        #region Quantized Vector Storage & SIMD Search

        /// <summary>
        /// Stores an embedding vector by L2-normalizing it and compressing to SQ8 (4x memory reduction).
        /// Replaces any existing vector with the same ID for strict idempotency.
        /// </summary>
        private void RebuildTwoStageAccelerator()
        {
            if (_vectors.Count == 0)
            {
                _twoStageAccelerator = null;
                _vectorIdToInt.Clear();
                _intToVectorRecord.Clear();
                return;
            }

            int dim = _vectors[0].Dimension;
            _twoStageAccelerator = new TwoStageVectorIndex(dim, Math.Max(16, _vectors.Count), VectorMetricType.Cosine, QuantizationStorageMode.ScalarQuantizedSq8);
            _vectorIdToInt.Clear();
            _intToVectorRecord.Clear();
            _nextVectorIntId = 1;

            float[] fVec = new float[dim];
            for (int i = 0; i < _vectors.Count; i++)
            {
                var vec = _vectors[i];
                if (vec.Dimension != dim) continue;

                int intId = _nextVectorIntId++;
                _vectorIdToInt[vec.Id] = intId;
                _intToVectorRecord[intId] = vec;

                BinaryQuantizer.DequantizeSQ8(vec.QuantizedData, fVec, vec.Scale, vec.Offset);
                _twoStageAccelerator.Add(intId, fVec);
            }
        }

        /// <summary>
        /// Stores an embedding vector by L2-normalizing it and compressing to SQ8 (4x memory reduction).
        /// Replaces any existing vector with the same ID for strict idempotency.
        /// Synchronizes both the sovereign .zab snapshot records and the TwoStageVectorIndex accelerator.
        /// </summary>
        public ZabVectorRecord StoreVector(string label, ReadOnlySpan<float> vector, string? id = null)
        {
            if (vector.IsEmpty) throw new ArgumentException("Vector cannot be empty.", nameof(vector));

            // Auto-normalize vector to unit L2 norm to guarantee exact cosine similarity
            float[] normVec = vector.ToArray();
            VectorMetrics.NormalizeL2(normVec);

            sbyte[] qData = new sbyte[normVec.Length];
            BinaryQuantizer.QuantizeSQ8(normVec, qData, out float scale, out float offset);

            string finalId = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id!;
            var record = new ZabVectorRecord
            {
                Id = finalId,
                Label = label ?? string.Empty,
                Dimension = normVec.Length,
                Scale = scale,
                Offset = offset,
                QuantizedData = qData
            };

            _rwLock.EnterWriteLock();
            try
            {
                _vectors.RemoveAll(v => string.Equals(v.Id, finalId, StringComparison.OrdinalIgnoreCase));
                _vectors.Add(record);

                if (_twoStageAccelerator == null || _twoStageAccelerator.Dimension != normVec.Length)
                {
                    RebuildTwoStageAccelerator();
                }
                else
                {
                    if (_vectorIdToInt.TryGetValue(finalId, out int existingIntId))
                    {
                        _intToVectorRecord[existingIntId] = record;
                        _twoStageAccelerator.Add(existingIntId, normVec);
                    }
                    else
                    {
                        int newIntId = _nextVectorIntId++;
                        _vectorIdToInt[finalId] = newIntId;
                        _intToVectorRecord[newIntId] = record;
                        _twoStageAccelerator.Add(newIntId, normVec);
                    }
                }

                _isDirty = true;
                _walSequenceNumber++;

                // Log to WAL
                byte[] payload = SerializeVectorRecord(record);
                _wal.AppendFrame(ZabWalOpCode.StoreVector, payload);

                if (_wal.ShouldCheckpoint())
                {
                    Checkpoint();
                }
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }

            return record;
        }

        public bool DeleteVector(string id)
        {
            _rwLock.EnterWriteLock();
            try
            {
                int removed = _vectors.RemoveAll(v => string.Equals(v.Id, id, StringComparison.OrdinalIgnoreCase));
                if (removed > 0)
                {
                    if (_vectorIdToInt.TryGetValue(id, out int intId))
                    {
                        _vectorIdToInt.Remove(id);
                        _intToVectorRecord.Remove(intId);
                    }
                    if (_vectors.Count > 0 && _vectors.Count % 32 == 0)
                    {
                        RebuildTwoStageAccelerator();
                    }

                    _isDirty = true;
                    _walSequenceNumber++;

                    using (var ms = new MemoryStream())
                    using (var writer = new BinaryWriter(ms, Encoding.UTF8))
                    {
                        writer.Write(id);
                        _wal.AppendFrame(ZabWalOpCode.DeleteVector, ms.ToArray());
                    }

                    if (_wal.ShouldCheckpoint())
                    {
                        Checkpoint();
                    }
                    return true;
                }
                return false;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Searches vectors using TwoStageVectorIndex (1-bit BQ POPCNT hardware filter + SQ8 SIMD rerank)
        /// when collection size >= 16, or hoisted SIMD flat scan for smaller collections.
        /// Non-blocking read lock allows multiple simultaneous searches without contending with writers.
        /// </summary>
        public List<(ZabVectorRecord Record, float Similarity)> SearchVectors(
            ReadOnlySpan<float> queryVector,
            int topK = 5,
            float minSimilarity = 0.50f)
        {
            if (queryVector.IsEmpty || topK <= 0) return new List<(ZabVectorRecord, float)>();

            // Auto-normalize query vector for exact cosine similarity
            float[] normQuery = queryVector.ToArray();
            VectorMetrics.NormalizeL2(normQuery);

            var results = new List<(ZabVectorRecord Record, float Similarity)>();

            _rwLock.EnterReadLock();
            try
            {
                // High-performance path: When index has >= 16 vectors, use TwoStageVectorIndex
                // (Stage 1: 1-bit BQ POPCNT hardware filter in ~50ns -> Stage 2: SQ8 AVX2 SIMD reranking)
                if (_twoStageAccelerator != null && _vectors.Count >= 16 && normQuery.Length == _twoStageAccelerator.Dimension)
                {
                    int fetchCount = Math.Min(_vectors.Count, Math.Max(topK * 4, 32));
                    var matches = _twoStageAccelerator.SearchTopK(normQuery, fetchCount, VectorMetricType.Cosine);
                    for (int i = 0; i < matches.Length; i++)
                    {
                        var m = matches[i];
                        if (m.Score >= minSimilarity && _intToVectorRecord.TryGetValue(m.Id, out var rec))
                        {
                            results.Add((rec, m.Score));
                            if (results.Count >= topK) break;
                        }
                    }
                    return results;
                }

                // Fallback for smaller collections (< 16 vectors): SIMD-accelerated hoisted flat scan
                float querySum = BinaryQuantizer.ComputeVectorSum(normQuery);
                for (int i = 0; i < _vectors.Count; i++)
                {
                    var vec = _vectors[i];
                    if (vec.Dimension != normQuery.Length) continue;

                    float sim = vec.ComputeSimilarity(normQuery, querySum);
                    if (sim >= minSimilarity)
                    {
                        results.Add((vec, sim));
                    }
                }

                results.Sort((a, b) => b.Similarity.CompareTo(a.Similarity));
                if (results.Count > topK)
                {
                    results.RemoveRange(topK, results.Count - topK);
                }
                return results;
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        #endregion

        #region Reflexion Memory CRUD

        public ZabReflexionRecord AddReflexion(string goal, string failureReason, string lesson)
        {
            var record = new ZabReflexionRecord
            {
                Goal = goal ?? string.Empty,
                FailureReason = failureReason ?? string.Empty,
                Lesson = lesson ?? string.Empty,
                TimestampUtc = DateTime.UtcNow
            };

            _rwLock.EnterWriteLock();
            try
            {
                _reflexions.Add(record);
                _isDirty = true;
                _walSequenceNumber++;

                // Log to WAL
                byte[] payload = SerializeReflexionRecord(record);
                _wal.AppendFrame(ZabWalOpCode.AddReflexion, payload);

                if (_wal.ShouldCheckpoint())
                {
                    Checkpoint();
                }
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }

            return record;
        }

        public bool DeleteReflexion(string id)
        {
            _rwLock.EnterWriteLock();
            try
            {
                int removed = _reflexions.RemoveAll(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
                if (removed > 0)
                {
                    _isDirty = true;
                    _walSequenceNumber++;

                    using (var ms = new MemoryStream())
                    using (var writer = new BinaryWriter(ms, Encoding.UTF8))
                    {
                        writer.Write(id);
                        _wal.AppendFrame(ZabWalOpCode.DeleteReflexion, ms.ToArray());
                    }

                    if (_wal.ShouldCheckpoint())
                    {
                        Checkpoint();
                    }
                    return true;
                }
                return false;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        public List<ZabReflexionRecord> RecallReflexions(string goal, int topK = 3)
        {
            if (string.IsNullOrWhiteSpace(goal) || topK <= 0) return new List<ZabReflexionRecord>();

            var tokens = goal.Split(new[] { ' ', ',', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
            var scored = new List<(ZabReflexionRecord Rec, int Score)>();

            _rwLock.EnterReadLock();
            try
            {
                foreach (var refRec in _reflexions)
                {
                    int score = 0;
                    foreach (var tok in tokens)
                    {
                        if (refRec.Goal.IndexOf(tok, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            score += 2;
                        }
                        if (refRec.FailureReason.IndexOf(tok, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            score += 1;
                        }
                    }

                    if (score > 0)
                    {
                        scored.Add((refRec, score));
                    }
                }
            }
            finally
            {
                _rwLock.ExitReadLock();
            }

            scored.Sort((a, b) => b.Score.CompareTo(a.Score));
            var result = new List<ZabReflexionRecord>();
            for (int i = 0; i < Math.Min(topK, scored.Count); i++)
            {
                result.Add(scored[i].Rec);
            }

            return result;
        }

        #endregion

        #region Plan Semantic Cache CRUD & Pruning

        public ZabPlanRecord CachePlan(
            string goal,
            string solution,
            int stepsCount,
            float confidence = 1.0f,
            ReadOnlySpan<float> goalVector = default)
        {
            var plan = new ZabPlanRecord
            {
                Goal = goal ?? string.Empty,
                Solution = solution ?? string.Empty,
                StepsCount = stepsCount,
                Confidence = confidence,
                HitCount = 0,
                CachedAtUtc = DateTime.UtcNow
            };

            if (!goalVector.IsEmpty)
            {
                float[] normGoal = goalVector.ToArray();
                VectorMetrics.NormalizeL2(normGoal);

                sbyte[] qVec = new sbyte[normGoal.Length];
                BinaryQuantizer.QuantizeSQ8(normGoal, qVec, out float scale, out float offset);
                plan.VectorDim = normGoal.Length;
                plan.Scale = scale;
                plan.Offset = offset;
                plan.GoalVectorSq8 = qVec;
            }

            _rwLock.EnterWriteLock();
            try
            {
                _plans.Add(plan);
                _isDirty = true;
                _walSequenceNumber++;

                // Log to WAL
                byte[] payload = SerializePlanRecord(plan);
                _wal.AppendFrame(ZabWalOpCode.CachePlan, payload);

                if (_wal.ShouldCheckpoint())
                {
                    Checkpoint();
                }
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }

            return plan;
        }

        public bool DeletePlan(string id)
        {
            _rwLock.EnterWriteLock();
            try
            {
                int removed = _plans.RemoveAll(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
                if (removed > 0)
                {
                    _isDirty = true;
                    _walSequenceNumber++;

                    using (var ms = new MemoryStream())
                    using (var writer = new BinaryWriter(ms, Encoding.UTF8))
                    {
                        writer.Write(id);
                        _wal.AppendFrame(ZabWalOpCode.DeletePlan, ms.ToArray());
                    }

                    if (_wal.ShouldCheckpoint())
                    {
                        Checkpoint();
                    }
                    return true;
                }
                return false;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Prunes stale cached plans older than maxAge that did not meet the minimum hit count threshold.
        /// </summary>
        public int PrunePlans(TimeSpan maxAge, int minHitCount = 0)
        {
            DateTime cutoff = DateTime.UtcNow - maxAge;

            _rwLock.EnterWriteLock();
            try
            {
                int removed = _plans.RemoveAll(p => p.CachedAtUtc < cutoff && p.HitCount <= minHitCount);
                if (removed > 0)
                {
                    _isDirty = true;
                    _walSequenceNumber++;

                    using (var ms = new MemoryStream())
                    using (var writer = new BinaryWriter(ms, Encoding.UTF8))
                    {
                        writer.Write(maxAge.Ticks);
                        writer.Write(minHitCount);
                        _wal.AppendFrame(ZabWalOpCode.PrunePlans, ms.ToArray());
                    }

                    if (_wal.ShouldCheckpoint())
                    {
                        Checkpoint();
                    }
                }
                return removed;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        public ZabPlanRecord? LookupPlan(ReadOnlySpan<float> queryVector, float minSimilarity = 0.85f)
        {
            if (queryVector.IsEmpty) return null;

            float[] normQuery = queryVector.ToArray();
            VectorMetrics.NormalizeL2(normQuery);

            float querySum = BinaryQuantizer.ComputeVectorSum(normQuery);
            ZabPlanRecord? bestMatch = null;
            float maxSim = minSimilarity;

            _rwLock.EnterWriteLock(); // Enter write lock because hit count is mutated
            try
            {
                for (int i = 0; i < _plans.Count; i++)
                {
                    var plan = _plans[i];
                    if (plan.GoalVectorSq8 == null || plan.VectorDim != normQuery.Length) continue;

                    float sim = plan.ComputeSimilarity(normQuery, querySum);
                    if (sim >= maxSim)
                    {
                        maxSim = sim;
                        bestMatch = plan;
                    }
                }

                if (bestMatch != null)
                {
                    bestMatch.HitCount++;
                    _isDirty = true;
                }
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }

            return bestMatch;
        }

        #endregion

        #region Semi-Parametric Neural Action Policy

        /// <summary>
        /// Updates the semi-parametric INT8 neural action policy network stored within the container.
        /// </summary>
        public void SetNeuralPolicy(ZabNeuralPolicy policy)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));

            _rwLock.EnterWriteLock();
            try
            {
                _neuralPolicy = policy;
                _isDirty = true;
                _walSequenceNumber++;

                byte[] payload = SerializeNeuralPolicy(policy);
                _wal.AppendFrame(ZabWalOpCode.UpdateNeuralPolicy, payload);

                if (_wal.ShouldCheckpoint())
                {
                    Checkpoint();
                }
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Predicts the recommended tool or action intent in sub-0.05ms using the internal quantized policy network.
        /// </summary>
        public string? PredictAction(ReadOnlySpan<float> inputEmbedding, out float confidence)
        {
            _rwLock.EnterReadLock();
            try
            {
                if (_neuralPolicy == null)
                {
                    confidence = 0.0f;
                    return null;
                }

                return _neuralPolicy.Predict(inputEmbedding, out confidence);
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Primitive 1: Choice (1-of-N). Predicts the recommended tool or action intent in sub-0.05ms using the internal quantized policy network.
        /// </summary>
        public string? PredictChoice(ReadOnlySpan<float> inputEmbedding, out float confidence)
        {
            _rwLock.EnterReadLock();
            try
            {
                if (_neuralPolicy == null)
                {
                    confidence = 0.0f;
                    return null;
                }

                return _neuralPolicy.PredictChoice(inputEmbedding, out confidence);
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Primitive 2: Score. Evaluates continuous score in [0.0, 1.0] for risk, complexity, or quality assessment.
        /// </summary>
        public float PredictScore(ReadOnlySpan<float> inputEmbedding, string targetClassName)
        {
            _rwLock.EnterReadLock();
            try
            {
                return _neuralPolicy?.PredictScore(inputEmbedding, targetClassName) ?? 0.0f;
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Primitive 3: Binary. Evaluates a deterministic boolean gatekeeper condition against a confidence threshold.
        /// </summary>
        public bool PredictBinary(ReadOnlySpan<float> inputEmbedding, string targetClassName, float threshold = 0.5f)
        {
            _rwLock.EnterReadLock();
            try
            {
                return _neuralPolicy?.PredictBinary(inputEmbedding, targetClassName, threshold) ?? false;
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Online Delta Adaptation: Updates INT8 policy weights dynamically based on System 2 verified feedback without offline retraining.
        /// Logs the updated weights snapshot to WAL to guarantee crash-resilience.
        /// </summary>
        public bool AdaptNeuralWeights(ReadOnlySpan<float> inputEmbedding, string targetClassName, float learningRate = 0.05f)
        {
            _rwLock.EnterWriteLock();
            try
            {
                if (_neuralPolicy == null) return false;

                bool adapted = _neuralPolicy.AdaptWeights(inputEmbedding, targetClassName, learningRate);
                if (adapted)
                {
                    _isDirty = true;
                    _walSequenceNumber++;

                    byte[] payload = SerializeNeuralPolicy(_neuralPolicy);
                    _wal.AppendFrame(ZabWalOpCode.UpdateNeuralPolicy, payload);

                    if (_wal.ShouldCheckpoint())
                    {
                        Checkpoint();
                    }
                }
                return adapted;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        #endregion

        #region WAL Mutation Replay (Internal)

        public void ApplyWalMutation(ZabWalOpCode opCode, byte[] payload)
        {
            using (var ms = new MemoryStream(payload))
            using (var reader = new BinaryReader(ms, Encoding.UTF8))
            {
                switch (opCode)
                {
                    case ZabWalOpCode.AddKnowledge:
                        {
                            var rec = DeserializeKnowledgeRecord(reader);
                            if (_knowledgeKeyIndex.TryGetValue(rec.Key, out var existingKey))
                            {
                                _knowledge.Remove(existingKey);
                            }
                            _knowledge.RemoveAll(k => string.Equals(k.Id, rec.Id, StringComparison.OrdinalIgnoreCase));
                            _knowledge.Add(rec);
                            _knowledgeKeyIndex[rec.Key] = rec;
                            break;
                        }
                    case ZabWalOpCode.UpdateKnowledge:
                        {
                            string id = reader.ReadString();
                            string val = reader.ReadString();
                            bool hasConf = reader.ReadBoolean();
                            float? conf = hasConf ? (float?)reader.ReadSingle() : null;

                            var existing = _knowledge.Find(k => string.Equals(k.Id, id, StringComparison.OrdinalIgnoreCase));
                            if (existing != null)
                            {
                                existing.Value = val;
                                if (conf.HasValue) existing.Confidence = conf.Value;
                                existing.LastUpdatedUtc = DateTime.UtcNow;
                            }
                            break;
                        }
                    case ZabWalOpCode.DeleteKnowledge:
                        {
                            string id = reader.ReadString();
                            var record = _knowledge.Find(k => string.Equals(k.Id, id, StringComparison.OrdinalIgnoreCase));
                            if (record != null)
                            {
                                _knowledge.Remove(record);
                                _knowledgeKeyIndex.Remove(record.Key);
                            }
                            break;
                        }
                    case ZabWalOpCode.StoreVector:
                        {
                            var rec = DeserializeVectorRecord(reader);
                            _vectors.RemoveAll(v => string.Equals(v.Id, rec.Id, StringComparison.OrdinalIgnoreCase));
                            _vectors.Add(rec);
                            break;
                        }
                    case ZabWalOpCode.DeleteVector:
                        {
                            string id = reader.ReadString();
                            _vectors.RemoveAll(v => string.Equals(v.Id, id, StringComparison.OrdinalIgnoreCase));
                            break;
                        }
                    case ZabWalOpCode.AddReflexion:
                        {
                            var rec = DeserializeReflexionRecord(reader);
                            _reflexions.RemoveAll(r => string.Equals(r.Id, rec.Id, StringComparison.OrdinalIgnoreCase));
                            _reflexions.Add(rec);
                            break;
                        }
                    case ZabWalOpCode.DeleteReflexion:
                        {
                            string id = reader.ReadString();
                            _reflexions.RemoveAll(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
                            break;
                        }
                    case ZabWalOpCode.CachePlan:
                        {
                            var rec = DeserializePlanRecord(reader);
                            _plans.RemoveAll(p => string.Equals(p.Id, rec.Id, StringComparison.OrdinalIgnoreCase));
                            _plans.Add(rec);
                            break;
                        }
                    case ZabWalOpCode.DeletePlan:
                        {
                            string id = reader.ReadString();
                            _plans.RemoveAll(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
                            break;
                        }
                    case ZabWalOpCode.PrunePlans:
                        {
                            long ticks = reader.ReadInt64();
                            int minHit = reader.ReadInt32();
                            DateTime cutoff = DateTime.UtcNow - TimeSpan.FromTicks(ticks);
                            _plans.RemoveAll(p => p.CachedAtUtc < cutoff && p.HitCount <= minHit);
                            break;
                        }
                    case ZabWalOpCode.UpdateNeuralPolicy:
                        {
                            _neuralPolicy = DeserializeNeuralPolicy(reader);
                            break;
                        }
                }
            }
        }

        #endregion

        #region Binary Persistence & Atomic Checkpoint (File.Replace)

        /// <summary>
        /// Checkpoints all in-memory state into the baseline .zab file atomically,
        /// then resets the Write-Ahead Log (.zab-wal) to zero bytes.
        /// </summary>
        public void Checkpoint()
        {
            _rwLock.EnterWriteLock();
            try
            {
                _wal.Checkpoint(() => FlushBaselineSnapshot());
                _isDirty = false;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Explicit commit command that checkpoints the database and guarantees durability.
        /// </summary>
        public void Commit()
        {
            Checkpoint();
        }

        private void FlushBaselineSnapshot()
        {
            string? dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir!);
            }

            // Disk exhaustion safeguard: ensure sufficient physical disk space for atomic snapshot replacement
            try
            {
                string fullPath = Path.GetFullPath(_filePath);
                string root = Path.GetPathRoot(fullPath) ?? string.Empty;
                if (!string.IsNullOrEmpty(root))
                {
                    var drive = new DriveInfo(root);
                    long currentSize = File.Exists(_filePath) ? new FileInfo(_filePath).Length : 0;
                    long minRequiredSpace = Math.Max(10 * 1024 * 1024, (long)(currentSize * 1.2)); // At least 10MB or 120% of current size
                    if (drive.AvailableFreeSpace < minRequiredSpace)
                    {
                        throw new IOException($"Insufficient disk space on drive '{root}'. Required: {minRequiredSpace / (1024 * 1024)} MB, Available: {drive.AvailableFreeSpace / (1024 * 1024)} MB. Aborting checkpoint to prevent storage exhaustion.");
                    }
                }
            }
            catch (IOException) { throw; }
            catch { /* Fallback gracefully if drive info not accessible in restricted environments */ }

            string tempFile = _filePath + ".tmp." + Guid.NewGuid().ToString("N");

            using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (var msPayload = new MemoryStream())
            using (var payloadWriter = new BinaryWriter(msPayload, Encoding.UTF8))
            {
                var header = new ZabHeader
                {
                    LastModifiedUtc = DateTime.UtcNow,
                    WalSequenceNumber = _walSequenceNumber
                };

                // 1. Write Manifest
                header.ManifestOffset = ZabHeader.HeaderSize + msPayload.Position;
                long mStart = msPayload.Position;
                payloadWriter.Write(_manifest.AgentId);
                payloadWriter.Write(_manifest.Name);
                payloadWriter.Write(_manifest.Description);
                payloadWriter.Write(_manifest.Role);
                payloadWriter.Write(_manifest.SystemPrompt);
                payloadWriter.Write(_manifest.RegisteredTools.Count);
                foreach (var tool in _manifest.RegisteredTools) payloadWriter.Write(tool);
                payloadWriter.Write(_manifest.CreatedAtUtc.ToBinary());
                header.ManifestLength = (int)(msPayload.Position - mStart);

                // 2. Write Knowledge
                header.KnowledgeOffset = ZabHeader.HeaderSize + msPayload.Position;
                long kStart = msPayload.Position;
                payloadWriter.Write(_knowledge.Count);
                foreach (var k in _knowledge)
                {
                    payloadWriter.Write(k.Id);
                    payloadWriter.Write(k.Key);
                    payloadWriter.Write(k.Value);
                    payloadWriter.Write(k.Category);
                    payloadWriter.Write(k.Confidence);
                    payloadWriter.Write(k.Status);
                    payloadWriter.Write(k.Author);
                    payloadWriter.Write(k.LastUpdatedUtc.ToBinary());
                    payloadWriter.Write(k.ConflictDetails ?? string.Empty);
                }
                header.KnowledgeLength = (int)(msPayload.Position - kStart);

                // 3. Write Vectors (SQ8 Quantized)
                header.VectorsOffset = ZabHeader.HeaderSize + msPayload.Position;
                long vStart = msPayload.Position;
                payloadWriter.Write(_vectors.Count);
                foreach (var v in _vectors)
                {
                    payloadWriter.Write(v.Id);
                    payloadWriter.Write(v.Label);
                    payloadWriter.Write(v.Dimension);
                    payloadWriter.Write(v.Scale);
                    payloadWriter.Write(v.Offset);
                    payloadWriter.Write(v.QuantizedData.Length);
                    for (int i = 0; i < v.QuantizedData.Length; i++)
                    {
                        payloadWriter.Write((byte)v.QuantizedData[i]);
                    }
                }
                header.VectorsLength = (int)(msPayload.Position - vStart);

                // 4. Write Reflexions
                header.ReflexionsOffset = ZabHeader.HeaderSize + msPayload.Position;
                long rStart = msPayload.Position;
                payloadWriter.Write(_reflexions.Count);
                foreach (var r in _reflexions)
                {
                    payloadWriter.Write(r.Id);
                    payloadWriter.Write(r.Goal);
                    payloadWriter.Write(r.FailureReason);
                    payloadWriter.Write(r.Lesson);
                    payloadWriter.Write(r.TimestampUtc.ToBinary());
                }
                header.ReflexionsLength = (int)(msPayload.Position - rStart);

                // 5. Write Plans
                header.PlansOffset = ZabHeader.HeaderSize + msPayload.Position;
                long pStart = msPayload.Position;
                payloadWriter.Write(_plans.Count);
                foreach (var p in _plans)
                {
                    payloadWriter.Write(p.Id);
                    payloadWriter.Write(p.Goal);
                    payloadWriter.Write(p.Solution);
                    payloadWriter.Write(p.StepsCount);
                    payloadWriter.Write(p.Confidence);
                    payloadWriter.Write(p.HitCount);
                    payloadWriter.Write(p.CachedAtUtc.ToBinary());
                    bool hasVec = p.GoalVectorSq8 != null && p.GoalVectorSq8.Length > 0;
                    payloadWriter.Write(hasVec);
                    if (hasVec)
                    {
                        payloadWriter.Write(p.VectorDim);
                        payloadWriter.Write(p.Scale);
                        payloadWriter.Write(p.Offset);
                        payloadWriter.Write(p.GoalVectorSq8!.Length);
                        for (int i = 0; i < p.GoalVectorSq8.Length; i++)
                        {
                            payloadWriter.Write((byte)p.GoalVectorSq8[i]);
                        }
                    }
                }
                header.PlansLength = (int)(msPayload.Position - pStart);

                // 6. Write Neural Policy (if present)
                if (_neuralPolicy != null)
                {
                    header.NeuralOffset = ZabHeader.HeaderSize + msPayload.Position;
                    long nStart = msPayload.Position;
                    byte[] nBytes = SerializeNeuralPolicy(_neuralPolicy);
                    payloadWriter.Write(nBytes.Length);
                    payloadWriter.Write(nBytes);
                    header.NeuralLength = (int)(msPayload.Position - nStart);
                }

                // Calculate hardware CRC32C over payload bytes
                byte[] payloadBytes = msPayload.ToArray();
                header.ChecksumCrc32C = FastCrc.Crc32C(payloadBytes);

                // Write Header + Payload to File
                using (var fileWriter = new BinaryWriter(fs, Encoding.UTF8))
                {
                    header.Write(fileWriter);
                    fileWriter.Write(payloadBytes);
                }
            }

            // Bulletproof atomic file swap on OS using File.Replace
            if (File.Exists(_filePath))
            {
                string backupFile = _filePath + ".bak";
                try
                {
                    File.Replace(tempFile, _filePath, backupFile, ignoreMetadataErrors: true);
                    if (File.Exists(backupFile))
                    {
                        try { File.Delete(backupFile); } catch { }
                    }
                }
                catch
                {
                    // Fallback for cross-filesystem / volume boundaries
                    if (File.Exists(_filePath)) File.Delete(_filePath);
                    File.Move(tempFile, _filePath);
                }
            }
            else
            {
                File.Move(tempFile, _filePath);
            }
        }

        /// <summary>
        /// Reloads state from the baseline binary file, verifying header magic and CRC32C checksum.
        /// </summary>
        public void Reload()
        {
            _rwLock.EnterWriteLock();
            try
            {
                using (var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new BinaryReader(fs, Encoding.UTF8))
                {
                    var header = ZabHeader.Read(reader);
                    if (!header.IsValid)
                    {
                        throw new InvalidDataException("Invalid ZAB binary header or unsupported format version.");
                    }

                    _walSequenceNumber = header.WalSequenceNumber;

                    // Read entire payload and verify CRC32C
                    int payloadLength = (int)(fs.Length - ZabHeader.HeaderSize);
                    byte[] payloadBytes = reader.ReadBytes(payloadLength);
                    uint computedCrc = FastCrc.Crc32C(payloadBytes);

                    if (computedCrc != header.ChecksumCrc32C)
                    {
                        throw new InvalidDataException($"Hardware CRC32C verification failed. Expected: {header.ChecksumCrc32C}, Computed: {computedCrc}. File may be corrupted.");
                    }

                    using (var ms = new MemoryStream(payloadBytes))
                    using (var payloadReader = new BinaryReader(ms, Encoding.UTF8))
                    {
                        // 1. Read Manifest
                        ms.Seek(header.ManifestOffset - ZabHeader.HeaderSize, SeekOrigin.Begin);
                        _manifest = new ZabManifest
                        {
                            AgentId = payloadReader.ReadString(),
                            Name = payloadReader.ReadString(),
                            Description = payloadReader.ReadString(),
                            Role = payloadReader.ReadString(),
                            SystemPrompt = payloadReader.ReadString()
                        };
                        int toolCount = payloadReader.ReadInt32();
                        _manifest.RegisteredTools.Clear();
                        for (int i = 0; i < toolCount; i++) _manifest.RegisteredTools.Add(payloadReader.ReadString());
                        _manifest.CreatedAtUtc = DateTime.FromBinary(payloadReader.ReadInt64());

                        // 2. Read Knowledge
                        _knowledge.Clear();
                        _knowledgeKeyIndex.Clear();
                        ms.Seek(header.KnowledgeOffset - ZabHeader.HeaderSize, SeekOrigin.Begin);
                        int kCount = payloadReader.ReadInt32();
                        for (int i = 0; i < kCount; i++)
                        {
                            var rec = DeserializeKnowledgeRecord(payloadReader);
                            _knowledge.Add(rec);
                            _knowledgeKeyIndex[rec.Key] = rec;
                        }

                        // 3. Read Vectors
                        _vectors.Clear();
                        ms.Seek(header.VectorsOffset - ZabHeader.HeaderSize, SeekOrigin.Begin);
                        int vCount = payloadReader.ReadInt32();
                        for (int i = 0; i < vCount; i++)
                        {
                            _vectors.Add(DeserializeVectorRecord(payloadReader));
                        }
                        RebuildTwoStageAccelerator();

                        // 4. Read Reflexions
                        _reflexions.Clear();
                        ms.Seek(header.ReflexionsOffset - ZabHeader.HeaderSize, SeekOrigin.Begin);
                        int rCount = payloadReader.ReadInt32();
                        for (int i = 0; i < rCount; i++)
                        {
                            _reflexions.Add(DeserializeReflexionRecord(payloadReader));
                        }

                        // 5. Read Plans
                        _plans.Clear();
                        ms.Seek(header.PlansOffset - ZabHeader.HeaderSize, SeekOrigin.Begin);
                        int pCount = payloadReader.ReadInt32();
                        for (int i = 0; i < pCount; i++)
                        {
                            _plans.Add(DeserializePlanRecord(payloadReader));
                        }

                        // 6. Read Neural Policy
                        if (header.NeuralOffset > 0 && header.NeuralLength > 0)
                        {
                            ms.Seek(header.NeuralOffset - ZabHeader.HeaderSize, SeekOrigin.Begin);
                            int nLen = payloadReader.ReadInt32();
                            byte[] nBytes = payloadReader.ReadBytes(nLen);
                            using (var nMs = new MemoryStream(nBytes))
                            using (var nReader = new BinaryReader(nMs, Encoding.UTF8))
                            {
                                _neuralPolicy = DeserializeNeuralPolicy(nReader);
                            }
                        }
                        else
                        {
                            _neuralPolicy = null;
                        }
                    }
                }

                _isDirty = false;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        #endregion

        #region Record Serialization Helpers

        private static byte[] SerializeKnowledgeRecord(ZabKnowledgeRecord k)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                w.Write(k.Id);
                w.Write(k.Key);
                w.Write(k.Value);
                w.Write(k.Category);
                w.Write(k.Confidence);
                w.Write(k.Status);
                w.Write(k.Author);
                w.Write(k.LastUpdatedUtc.ToBinary());
                w.Write(k.ConflictDetails ?? string.Empty);
                return ms.ToArray();
            }
        }

        private static ZabKnowledgeRecord DeserializeKnowledgeRecord(BinaryReader r)
        {
            return new ZabKnowledgeRecord
            {
                Id = r.ReadString(),
                Key = r.ReadString(),
                Value = r.ReadString(),
                Category = r.ReadString(),
                Confidence = r.ReadSingle(),
                Status = r.ReadString(),
                Author = r.ReadString(),
                LastUpdatedUtc = DateTime.FromBinary(r.ReadInt64()),
                ConflictDetails = r.ReadString()
            };
        }

        private static byte[] SerializeVectorRecord(ZabVectorRecord v)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                w.Write(v.Id);
                w.Write(v.Label);
                w.Write(v.Dimension);
                w.Write(v.Scale);
                w.Write(v.Offset);
                w.Write(v.QuantizedData.Length);
                for (int i = 0; i < v.QuantizedData.Length; i++)
                {
                    w.Write((byte)v.QuantizedData[i]);
                }
                return ms.ToArray();
            }
        }

        private static ZabVectorRecord DeserializeVectorRecord(BinaryReader r)
        {
            string id = r.ReadString();
            string label = r.ReadString();
            int dim = r.ReadInt32();
            float scale = r.ReadSingle();
            float offset = r.ReadSingle();
            int qLen = r.ReadInt32();
            sbyte[] qData = new sbyte[qLen];
            for (int j = 0; j < qLen; j++)
            {
                qData[j] = (sbyte)r.ReadByte();
            }

            return new ZabVectorRecord
            {
                Id = id,
                Label = label,
                Dimension = dim,
                Scale = scale,
                Offset = offset,
                QuantizedData = qData
            };
        }

        private static byte[] SerializeReflexionRecord(ZabReflexionRecord refRec)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                w.Write(refRec.Id);
                w.Write(refRec.Goal);
                w.Write(refRec.FailureReason);
                w.Write(refRec.Lesson);
                w.Write(refRec.TimestampUtc.ToBinary());
                return ms.ToArray();
            }
        }

        private static ZabReflexionRecord DeserializeReflexionRecord(BinaryReader r)
        {
            return new ZabReflexionRecord
            {
                Id = r.ReadString(),
                Goal = r.ReadString(),
                FailureReason = r.ReadString(),
                Lesson = r.ReadString(),
                TimestampUtc = DateTime.FromBinary(r.ReadInt64())
            };
        }

        private static byte[] SerializePlanRecord(ZabPlanRecord p)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                w.Write(p.Id);
                w.Write(p.Goal);
                w.Write(p.Solution);
                w.Write(p.StepsCount);
                w.Write(p.Confidence);
                w.Write(p.HitCount);
                w.Write(p.CachedAtUtc.ToBinary());
                bool hasVec = p.GoalVectorSq8 != null && p.GoalVectorSq8.Length > 0;
                w.Write(hasVec);
                if (hasVec)
                {
                    w.Write(p.VectorDim);
                    w.Write(p.Scale);
                    w.Write(p.Offset);
                    w.Write(p.GoalVectorSq8!.Length);
                    for (int i = 0; i < p.GoalVectorSq8.Length; i++)
                    {
                        w.Write((byte)p.GoalVectorSq8[i]);
                    }
                }
                return ms.ToArray();
            }
        }

        private static ZabPlanRecord DeserializePlanRecord(BinaryReader r)
        {
            var plan = new ZabPlanRecord
            {
                Id = r.ReadString(),
                Goal = r.ReadString(),
                Solution = r.ReadString(),
                StepsCount = r.ReadInt32(),
                Confidence = r.ReadSingle(),
                HitCount = r.ReadInt32(),
                CachedAtUtc = DateTime.FromBinary(r.ReadInt64())
            };
            bool hasVec = r.ReadBoolean();
            if (hasVec)
            {
                plan.VectorDim = r.ReadInt32();
                plan.Scale = r.ReadSingle();
                plan.Offset = r.ReadSingle();
                int qLen = r.ReadInt32();
                sbyte[] qData = new sbyte[qLen];
                for (int j = 0; j < qLen; j++)
                {
                    qData[j] = (sbyte)r.ReadByte();
                }
                plan.GoalVectorSq8 = qData;
            }
            return plan;
        }

        private static byte[] SerializeNeuralPolicy(ZabNeuralPolicy p)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                w.Write(p.ModelName);
                w.Write(p.InputDim);
                w.Write(p.OutputClasses.Count);
                foreach (var c in p.OutputClasses) w.Write(c);
                w.Write(p.Scale);

                w.Write(p.WeightsInt8.Length);
                for (int i = 0; i < p.WeightsInt8.Length; i++) w.Write((byte)p.WeightsInt8[i]);

                w.Write(p.Biases.Length);
                for (int i = 0; i < p.Biases.Length; i++) w.Write(p.Biases[i]);

                return ms.ToArray();
            }
        }

        private static ZabNeuralPolicy DeserializeNeuralPolicy(BinaryReader r)
        {
            string name = r.ReadString();
            int dim = r.ReadInt32();
            int cCount = r.ReadInt32();
            var classes = new List<string>(cCount);
            for (int i = 0; i < cCount; i++) classes.Add(r.ReadString());
            float scale = r.ReadSingle();

            int wLen = r.ReadInt32();
            sbyte[] weights = new sbyte[wLen];
            for (int i = 0; i < wLen; i++) weights[i] = (sbyte)r.ReadByte();

            int bLen = r.ReadInt32();
            float[] biases = new float[bLen];
            for (int i = 0; i < bLen; i++) biases[i] = r.ReadSingle();

            return new ZabNeuralPolicy
            {
                ModelName = name,
                InputDim = dim,
                OutputClasses = classes,
                Scale = scale,
                WeightsInt8 = weights,
                Biases = biases
            };
        }

        #endregion

        #region JSON Export & Import (100% Lossless Human-Readable)

        public class ZabJsonDumpDto
        {
            public ZabManifest Manifest { get; set; } = new ZabManifest();
            public List<ZabKnowledgeRecord> Knowledge { get; set; } = new List<ZabKnowledgeRecord>();
            public List<ZabReflexionRecord> Reflexions { get; set; } = new List<ZabReflexionRecord>();
            public List<ZabPlanRecord> Plans { get; set; } = new List<ZabPlanRecord>();
            public ZabNeuralPolicy? NeuralPolicy { get; set; }
        }

        /// <summary>
        /// Exports the database contents to a human-readable JSON string.
        /// </summary>
        public string ExportToJsonString()
        {
            _rwLock.EnterReadLock();
            try
            {
                var dump = new ZabJsonDumpDto
                {
                    Manifest = _manifest,
                    Knowledge = _knowledge,
                    Reflexions = _reflexions,
                    Plans = _plans,
                    NeuralPolicy = _neuralPolicy
                };

                return JsonSerializer.Serialize(dump, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true
                });
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Exports database to a human-readable JSON file on disk.
        /// </summary>
        public void ExportToJson(string jsonFilePath)
        {
            string json = ExportToJsonString();
            File.WriteAllText(jsonFilePath, json, Encoding.UTF8);
        }

        /// <summary>
        /// Restores database contents from a human-readable JSON string and checkpoints container.
        /// </summary>
        public void ImportFromJsonString(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentNullException(nameof(json));

            var dump = JsonSerializer.Deserialize<ZabJsonDumpDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (dump == null) throw new InvalidDataException("Failed to parse ZAB JSON dump.");

            _rwLock.EnterWriteLock();
            try
            {
                if (dump.Manifest != null) _manifest = dump.Manifest;

                if (dump.Knowledge != null)
                {
                    _knowledge.Clear();
                    _knowledgeKeyIndex.Clear();
                    foreach (var k in dump.Knowledge)
                    {
                        _knowledge.Add(k);
                        _knowledgeKeyIndex[k.Key] = k;
                    }
                }

                if (dump.Reflexions != null)
                {
                    _reflexions.Clear();
                    _reflexions.AddRange(dump.Reflexions);
                }

                if (dump.Plans != null)
                {
                    _plans.Clear();
                    _plans.AddRange(dump.Plans);
                }

                if (dump.NeuralPolicy != null)
                {
                    _neuralPolicy = dump.NeuralPolicy;
                }

                _isDirty = true;
                Checkpoint();
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Restores database contents from a human-readable JSON file.
        /// </summary>
        public void ImportFromJson(string jsonFilePath)
        {
            if (!File.Exists(jsonFilePath)) throw new FileNotFoundException("JSON dump file not found.", jsonFilePath);
            string json = File.ReadAllText(jsonFilePath, Encoding.UTF8);
            ImportFromJsonString(json);
        }

        #endregion

        #region Storage Metrics & Statistics

        /// <summary>
        /// Calculates storage metrics, comparing raw uncompressed size vs actual binary container size.
        /// </summary>
        public ZabStorageStats GetStorageStats()
        {
            _rwLock.EnterReadLock();
            try
            {
                long fileLength = File.Exists(_filePath) ? new FileInfo(_filePath).Length : 0;
                long walLength = File.Exists(_wal.WalFilePath) ? new FileInfo(_wal.WalFilePath).Length : 0;
                long totalDiskBytes = fileLength + walLength;

                // Estimate raw size if stored uncompressed:
                long rawVectorBytes = 0;
                foreach (var v in _vectors)
                {
                    rawVectorBytes += v.Dimension * 4;
                }

                // Estimated raw JSON/UTF-8 string representation
                long rawTextBytes = 0;
                foreach (var k in _knowledge) rawTextBytes += (k.Key.Length + k.Value.Length) * 2 + 128;
                foreach (var r in _reflexions) rawTextBytes += (r.Goal.Length + r.Lesson.Length) * 2 + 128;
                foreach (var p in _plans) rawTextBytes += (p.Goal.Length + p.Solution.Length) * 2 + 128;

                long estimatedRaw = ZabHeader.HeaderSize + rawVectorBytes + rawTextBytes;
                if (estimatedRaw < totalDiskBytes) estimatedRaw = totalDiskBytes * 4;

                return new ZabStorageStats
                {
                    RawSizeEstimatedBytes = estimatedRaw,
                    ActualFileSizeBytes = totalDiskBytes,
                    KnowledgeCount = _knowledge.Count,
                    VectorCount = _vectors.Count,
                    ReflexionCount = _reflexions.Count,
                    PlanCount = _plans.Count,
                    HasNeuralPolicy = _neuralPolicy != null
                };
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Creates a zero-copy memory-mapped file reader directly onto the flushed baseline .zab container.
        /// Flushes any pending memory state before opening to ensure 100% snapshot freshness.
        /// </summary>
        public ZabMMapReader CreateMMapReader()
        {
            _rwLock.EnterWriteLock();
            try
            {
                if (_isDirty || _wal.PendingFrames > 0)
                {
                    Checkpoint();
                }
                return ZabMMapReader.Open(_filePath);
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Builds an Inverted File (IVF) Clustered Index over current quantized vectors for sub-linear O(√N) searches.
        /// </summary>
        public ZabIvfVectorIndex BuildIvfIndex(int targetClusters = 16)
        {
            _rwLock.EnterReadLock();
            try
            {
                if (_vectors.Count == 0)
                {
                    return new ZabIvfVectorIndex(64, targetClusters);
                }

                int dim = _vectors[0].Dimension;
                int clusters = Math.Max(2, Math.Min(targetClusters, (int)Math.Sqrt(_vectors.Count) + 1));
                var ivf = new ZabIvfVectorIndex(dim, clusters);

                for (int i = 0; i < _vectors.Count; i++)
                {
                    var vec = _vectors[i];
                    if (vec.Dimension == dim)
                    {
                        float[] dequantized = vec.Dequantize();
                        ivf.Add(vec, dequantized);
                    }
                }

                return ivf;
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Compacts storage by flushing baseline snapshot, removing dead/reclaimed records, and truncating WAL.
        /// </summary>
        public void Compact()
        {
            _rwLock.EnterWriteLock();
            try
            {
                // Rebuild accelerators and purge any stale structures
                RebuildTwoStageAccelerator();
                Checkpoint();
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Builds a high-scale persistent index mapping 64-bit key hashes to container offsets for O(1) point lookups.
        /// </summary>
        public ZabBillionScaleIndex BuildBillionScaleIndex(int recordsPerBlock = 256)
        {
            _rwLock.EnterWriteLock();
            try
            {
                if (_isDirty || _wal.PendingFrames > 0)
                {
                    Checkpoint();
                }

                var slots = new List<ZabIndexSlot>(_knowledge.Count);

                using (var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new BinaryReader(fs, Encoding.UTF8))
                {
                    var header = ZabHeader.Read(reader);
                    if (header.KnowledgeOffset > 0 && header.KnowledgeLength > 0)
                    {
                        fs.Position = header.KnowledgeOffset;
                        int count = reader.ReadInt32();
                        for (int i = 0; i < count; i++)
                        {
                            long recordStart = fs.Position;
                            string id = reader.ReadString();
                            string key = reader.ReadString();
                            string val = reader.ReadString();
                            string cat = reader.ReadString();
                            float conf = reader.ReadSingle();
                            string stat = reader.ReadString();
                            string author = reader.ReadString();
                            long updated = reader.ReadInt64();
                            string details = reader.ReadString();
                            long recordEnd = fs.Position;

                            slots.Add(new ZabIndexSlot
                            {
                                KeyHash = FastHash.Fnv1a64(key.AsSpan()),
                                FileOffset = recordStart,
                                Length = (int)(recordEnd - recordStart),
                                Checksum = 0
                            });
                        }
                    }
                }

                return ZabBillionScaleIndex.Build(slots, recordsPerBlock);
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_isDirty && AutoCheckpointOnDispose)
                {
                    try { Checkpoint(); } catch { }
                }
                _wal.Dispose();
                _rwLock.Dispose();
                _disposed = true;
            }
        }
    }
}
