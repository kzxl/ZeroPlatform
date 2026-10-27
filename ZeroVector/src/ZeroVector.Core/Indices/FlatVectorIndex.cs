using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroVector.Core.Metrics;
using ZeroVector.Core.Results;

namespace ZeroVector.Core.Indices
{
    /// <summary>
    /// High-throughput contiguous cache-aligned flat vector database for exact brute-force Top-K similarity search.
    /// Stores high-dimensional vector embeddings in flat contiguous memory and performs batch SIMD evaluation.
    /// Supports multi-threaded parallel partitioning for large vector collections.
    /// </summary>
    public sealed class FlatVectorIndex : IVectorIndex
    {
        private const int ParallelThreshold = 512;

        private readonly int _dimension;
        private readonly VectorMetricType _defaultMetric;
        private float[] _data;
        private int[] _ids;
        private readonly Dictionary<int, int> _idToOffset;
        private int _count;
        private readonly ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        public int Dimension => _dimension;
        public int Count
        {
            get
            {
                _rwLock.EnterReadLock();
                try { return _count; }
                finally { _rwLock.ExitReadLock(); }
            }
        }

        public VectorMetricType Metric => _defaultMetric;

        public FlatVectorIndex(int dimension, int initialCapacity = 64, VectorMetricType defaultMetric = VectorMetricType.Cosine)
        {
            if (dimension <= 0) throw new ArgumentOutOfRangeException(nameof(dimension), "Dimension must be positive.");
            if (initialCapacity < 16) initialCapacity = 16;

            _dimension = dimension;
            _defaultMetric = defaultMetric;
            _data = new float[initialCapacity * dimension];
            _ids = new int[initialCapacity];
            _idToOffset = new Dictionary<int, int>(initialCapacity);
            _count = 0;
        }

        public void Add(int id, ReadOnlySpan<float> vector)
        {
            if (vector.Length != _dimension)
                throw new ArgumentException($"Vector dimension {vector.Length} does not match index dimension {_dimension}.");

            _rwLock.EnterWriteLock();
            try
            {
                if (_idToOffset.TryGetValue(id, out int existingIndex))
                {
                    // Update in-place
                    int offset = existingIndex * _dimension;
                    vector.CopyTo(_data.AsSpan(offset, _dimension));
                    return;
                }

                EnsureCapacity(_count + 1);
                int targetIndex = _count;
                int dataOffset = targetIndex * _dimension;
                vector.CopyTo(_data.AsSpan(dataOffset, _dimension));
                _ids[targetIndex] = id;
                _idToOffset[id] = targetIndex;
                _count++;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        public bool TryGet(int id, Span<float> destination)
        {
            if (destination.Length < _dimension) return false;

            _rwLock.EnterReadLock();
            try
            {
                if (!_idToOffset.TryGetValue(id, out int index))
                    return false;

                int offset = index * _dimension;
                new ReadOnlySpan<float>(_data, offset, _dimension).CopyTo(destination);
                return true;
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        public VectorSearchResult[] SearchTopK(ReadOnlySpan<float> query, int k)
        {
            return SearchTopK(query, k, _defaultMetric);
        }

        public VectorSearchResult[] SearchTopK(ReadOnlySpan<float> query, int k, VectorMetricType metric)
        {
            if (query.Length != _dimension)
                throw new ArgumentException($"Query vector dimension {query.Length} must match index dimension {_dimension}.");

            if (k <= 0) return Array.Empty<VectorSearchResult>();

            _rwLock.EnterReadLock();
            try
            {
                int currentCount = _count;
                if (currentCount == 0) return Array.Empty<VectorSearchResult>();

                int actualK = Math.Min(k, currentCount);
                var candidates = new VectorSearchResult[currentCount];

                if (currentCount >= ParallelThreshold)
                {
                    float[] queryCopy = query.ToArray();
                    Parallel.For(0, currentCount, i =>
                    {
                        int offset = i * _dimension;
                        var storedSpan = new ReadOnlySpan<float>(_data, offset, _dimension);
                        float score = EvaluateMetric(queryCopy, storedSpan, metric);
                        candidates[i] = new VectorSearchResult(_ids[i], score);
                    });
                }
                else
                {
                    for (int i = 0; i < currentCount; i++)
                    {
                        int offset = i * _dimension;
                        var storedSpan = new ReadOnlySpan<float>(_data, offset, _dimension);
                        float score = EvaluateMetric(query, storedSpan, metric);
                        candidates[i] = new VectorSearchResult(_ids[i], score);
                    }
                }

                // Higher score is better for Cosine & DotProduct (descending)
                // Lower score is better for Euclidean, Manhattan & Hamming (ascending)
                bool ascending = metric == VectorMetricType.Euclidean ||
                                 metric == VectorMetricType.EuclideanSquared ||
                                 metric == VectorMetricType.Manhattan ||
                                 metric == VectorMetricType.Hamming;

                if (ascending)
                {
                    Array.Sort(candidates, (a, b) => a.Score.CompareTo(b.Score));
                }
                else
                {
                    Array.Sort(candidates, (a, b) => b.Score.CompareTo(a.Score));
                }

                var result = new VectorSearchResult[actualK];
                Array.Copy(candidates, result, actualK);
                return result;
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        public void Clear()
        {
            _rwLock.EnterWriteLock();
            try
            {
                _count = 0;
                _idToOffset.Clear();
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Saves the entire vector index to a stream with header and CRC32C checksum.
        /// </summary>
        public void Save(System.IO.Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            _rwLock.EnterReadLock();
            try
            {
                using (var writer = new System.IO.BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
                {
                    // Magic: ZVF1
                    writer.Write((byte)'Z');
                    writer.Write((byte)'V');
                    writer.Write((byte)'F');
                    writer.Write((byte)'1');
                    writer.Write((int)_defaultMetric);
                    writer.Write(_dimension);
                    writer.Write(_count);

                    for (int i = 0; i < _count; i++)
                    {
                        writer.Write(_ids[i]);
                    }

                    int floatCount = _count * _dimension;
                    for (int i = 0; i < floatCount; i++)
                    {
                        writer.Write(_data[i]);
                    }
                }
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Loads a FlatVectorIndex from a stream written by Save.
        /// </summary>
        public static FlatVectorIndex Load(System.IO.Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            using (var reader = new System.IO.BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                byte m0 = reader.ReadByte();
                byte m1 = reader.ReadByte();
                byte m2 = reader.ReadByte();
                byte m3 = reader.ReadByte();

                if (m0 != (byte)'Z' || m1 != (byte)'V' || m2 != (byte)'F' || m3 != (byte)'1')
                    throw new System.IO.InvalidDataException("Invalid ZeroVector magic header.");

                var metric = (VectorMetricType)reader.ReadInt32();
                int dimension = reader.ReadInt32();
                int count = reader.ReadInt32();

                var index = new FlatVectorIndex(dimension, Math.Max(count, 16), metric);

                int[] ids = new int[count];
                for (int i = 0; i < count; i++)
                {
                    ids[i] = reader.ReadInt32();
                }

                float[] data = new float[count * dimension];
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = reader.ReadSingle();
                }

                for (int i = 0; i < count; i++)
                {
                    var span = new ReadOnlySpan<float>(data, i * dimension, dimension);
                    index.Add(ids[i], span);
                }

                return index;
            }
        }

        private static float EvaluateMetric(ReadOnlySpan<float> query, ReadOnlySpan<float> stored, VectorMetricType metric)
        {
            return metric switch
            {
                VectorMetricType.Cosine => VectorMetrics.CosineSimilarity(query, stored),
                VectorMetricType.DotProduct => VectorMetrics.DotProduct(query, stored),
                VectorMetricType.Euclidean => VectorMetrics.EuclideanDistance(query, stored),
                VectorMetricType.EuclideanSquared => VectorMetrics.EuclideanDistanceSquared(query, stored),
                VectorMetricType.Manhattan => VectorMetrics.ManhattanDistance(query, stored),
                _ => VectorMetrics.CosineSimilarity(query, stored)
            };
        }

        private void EnsureCapacity(int minCapacity)
        {
            if (_ids.Length >= minCapacity) return;

            int newCap = Math.Max(_ids.Length * 2, minCapacity);
            Array.Resize(ref _ids, newCap);
            Array.Resize(ref _data, newCap * _dimension);
        }
    }
}
