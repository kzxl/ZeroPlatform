using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace ZeroData.Core
{
    /// <summary>
    /// Represents a search result containing row index and similarity score.
    /// </summary>
    public readonly struct VectorMatch
    {
        public int RowIndex { get; }
        public float Score { get; }

        public VectorMatch(int rowIndex, float score)
        {
            RowIndex = rowIndex;
            Score = score;
        }

        public override string ToString() => $"[Row {RowIndex}: {Score:F4}]";
    }

    /// <summary>
    /// High-throughput contiguous columnar storage for high-dimensional vector embeddings.
    /// Eliminates per-row array allocations and enables fused hybrid search with SelectionMask filters
    /// and 1-Bit Binary Quantization (BQ) hardware POPCNT coarse acceleration.
    /// </summary>
    public sealed unsafe class VectorColumn : IDataColumn
    {
        private readonly string _name;
        private readonly int _dimension;
        private readonly int _bqUlongs;
        private float[] _flatData;
        private ulong[]? _bqData;
        private byte[]? _nullBitmap;
        private int _length;
        private bool _hasNulls;
        private bool _bqValid;

        public string Name => _name;
        public Type DataType => typeof(float[]);
        public int Length => _length;
        public int Dimension => _dimension;
        public int BqUlongsPerVector => _bqUlongs;
        public bool HasNulls => _hasNulls;
        public ReadOnlySpan<byte> NullBitmap => _nullBitmap != null ? new ReadOnlySpan<byte>(_nullBitmap, 0, (_length + 7) >> 3) : ReadOnlySpan<byte>.Empty;

        public VectorColumn(string name, int dimension, int initialCapacity = 0)
        {
            if (dimension <= 0) throw new ArgumentOutOfRangeException(nameof(dimension), "Dimension must be positive.");
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _dimension = dimension;
            _bqUlongs = (dimension + 63) >> 6;
            _flatData = new float[Math.Max(initialCapacity, 0) * dimension];
            _length = initialCapacity;
            _hasNulls = false;
            _bqValid = false;
        }

        public VectorColumn(string name, int dimension, float[] flatData, int length)
        {
            if (dimension <= 0) throw new ArgumentOutOfRangeException(nameof(dimension), "Dimension must be positive.");
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _dimension = dimension;
            _bqUlongs = (dimension + 63) >> 6;
            _flatData = flatData ?? throw new ArgumentNullException(nameof(flatData));
            _length = length;
            _hasNulls = false;
            _bqValid = false;
        }

        public VectorColumn(string name, int dimension, IEnumerable<float[]> vectors)
        {
            if (dimension <= 0) throw new ArgumentOutOfRangeException(nameof(dimension), "Dimension must be positive.");
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _dimension = dimension;
            _bqUlongs = (dimension + 63) >> 6;

            var list = new List<float[]>(vectors);
            _length = list.Count;
            _flatData = new float[_length * dimension];

            for (int i = 0; i < _length; i++)
            {
                var vec = list[i];
                if (vec != null)
                {
                    if (vec.Length != dimension)
                        throw new ArgumentException($"Vector at row {i} has dimension {vec.Length}, expected {dimension}.");
                    Array.Copy(vec, 0, _flatData, i * dimension, dimension);
                }
                else
                {
                    SetNull(i);
                }
            }

            _bqValid = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<float> GetVectorSpan(int rowIndex)
        {
            if ((uint)rowIndex >= (uint)_length) throw new ArgumentOutOfRangeException(nameof(rowIndex));
            return new ReadOnlySpan<float>(_flatData, rowIndex * _dimension, _dimension);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<float> GetVectorSpanWritable(int rowIndex)
        {
            if ((uint)rowIndex >= (uint)_length) throw new ArgumentOutOfRangeException(nameof(rowIndex));
            _bqValid = false;
            return new Span<float>(_flatData, rowIndex * _dimension, _dimension);
        }

        public float[] GetVector(int rowIndex)
        {
            if ((uint)rowIndex >= (uint)_length) throw new ArgumentOutOfRangeException(nameof(rowIndex));
            if (IsNull(rowIndex)) return Array.Empty<float>();

            var result = new float[_dimension];
            Array.Copy(_flatData, rowIndex * _dimension, result, 0, _dimension);
            return result;
        }

        public void SetVector(int rowIndex, ReadOnlySpan<float> vector)
        {
            if ((uint)rowIndex >= (uint)_length) throw new ArgumentOutOfRangeException(nameof(rowIndex));
            if (vector.Length != _dimension)
                throw new ArgumentException($"Vector dimension {vector.Length} does not match column dimension {_dimension}.");

            int offset = rowIndex * _dimension;
            vector.CopyTo(_flatData.AsSpan(offset, _dimension));

            if (_nullBitmap != null)
            {
                _nullBitmap[rowIndex >> 3] |= (byte)(1 << (rowIndex & 7));
            }

            if (_bqData != null)
            {
                int bqOffset = rowIndex * _bqUlongs;
                Quantize1Bit(vector, _bqData.AsSpan(bqOffset, _bqUlongs));
            }
        }

        public void AddVector(ReadOnlySpan<float> vector)
        {
            if (vector.Length != _dimension)
                throw new ArgumentException($"Vector dimension {vector.Length} does not match column dimension {_dimension}.");

            EnsureCapacity(_length + 1);
            int targetRow = _length;
            int offset = targetRow * _dimension;
            vector.CopyTo(_flatData.AsSpan(offset, _dimension));

            if (_bqData != null)
            {
                int bqOffset = targetRow * _bqUlongs;
                Quantize1Bit(vector, _bqData.AsSpan(bqOffset, _bqUlongs));
            }

            _length++;
        }

        public bool IsNull(int index)
        {
            if (!_hasNulls || _nullBitmap == null) return false;
            if ((uint)index >= (uint)_length) throw new ArgumentOutOfRangeException(nameof(index));
            return (_nullBitmap[index >> 3] & (1 << (index & 7))) == 0;
        }

        public void SetNull(int index)
        {
            if ((uint)index >= (uint)_length) throw new ArgumentOutOfRangeException(nameof(index));
            EnsureNullBitmap();
            _nullBitmap![index >> 3] &= (byte)~(1 << (index & 7));
            _hasNulls = true;
            _bqValid = false;
        }

        public object? GetValue(int index)
        {
            if (IsNull(index)) return null;
            return GetVector(index);
        }

        public void SetValue(int index, object? value)
        {
            if (value == null)
            {
                SetNull(index);
                return;
            }

            if (value is float[] arr)
            {
                SetVector(index, arr);
            }
            else if (value is ReadOnlyMemory<float> mem)
            {
                SetVector(index, mem.Span);
            }
            else
            {
                throw new ArgumentException($"Unsupported vector value type: {value.GetType().Name}");
            }
        }

        public IDataColumn Slice(int start, int length)
        {
            if (start < 0 || length < 0 || start + length > _length)
                throw new ArgumentOutOfRangeException();

            var slicedFlat = new float[length * _dimension];
            Array.Copy(_flatData, start * _dimension, slicedFlat, 0, length * _dimension);

            var col = new VectorColumn(_name, _dimension, slicedFlat, length);
            if (_hasNulls && _nullBitmap != null)
            {
                for (int i = 0; i < length; i++)
                {
                    if (IsNull(start + i)) col.SetNull(i);
                }
            }
            return col;
        }

        public IDataColumn Clone()
        {
            var clonedFlat = new float[_length * _dimension];
            Array.Copy(_flatData, 0, clonedFlat, 0, _length * _dimension);

            var col = new VectorColumn(_name, _dimension, clonedFlat, _length);
            if (_hasNulls && _nullBitmap != null)
            {
                col.EnsureNullBitmap();
                Array.Copy(_nullBitmap, col._nullBitmap!, (_length + 7) >> 3);
                col._hasNulls = true;
            }
            return col;
        }

        public IDataColumn Filter(int[] indices)
        {
            if (indices == null) throw new ArgumentNullException(nameof(indices));
            int newLen = indices.Length;
            var filteredFlat = new float[newLen * _dimension];

            var col = new VectorColumn(_name, _dimension, filteredFlat, newLen);
            for (int i = 0; i < newLen; i++)
            {
                int srcIdx = indices[i];
                if (IsNull(srcIdx))
                {
                    col.SetNull(i);
                }
                else
                {
                    Array.Copy(_flatData, srcIdx * _dimension, filteredFlat, i * _dimension, _dimension);
                }
            }
            return col;
        }

        /// <summary>
        /// Ensures the 1-Bit Binary Quantization (BQ) index is built and ready for hardware POPCNT filtering.
        /// </summary>
        public void EnsureBqIndex()
        {
            if (_bqValid && _bqData != null && _bqData.Length >= _length * _bqUlongs)
                return;

            if (_bqData == null || _bqData.Length < _length * _bqUlongs)
            {
                _bqData = new ulong[Math.Max(_length, 16) * _bqUlongs];
            }

            for (int i = 0; i < _length; i++)
            {
                if (IsNull(i)) continue;
                int bqOffset = i * _bqUlongs;
                int dataOffset = i * _dimension;
                var src = new ReadOnlySpan<float>(_flatData, dataOffset, _dimension);
                var dest = _bqData.AsSpan(bqOffset, _bqUlongs);
                Quantize1Bit(src, dest);
            }

            _bqValid = true;
        }

        /// <summary>
        /// Executes fused hybrid similarity retrieval directly over columnar memory.
        /// Fuses Apache Arrow SelectionMask filters with Stage-1 1-Bit BQ Hamming scanning and Stage-2 SIMD Cosine reranking.
        /// </summary>
        public VectorMatch[] SearchNearest(
            ReadOnlySpan<float> query,
            int topK,
            SelectionMask? selectionMask = null,
            int oversampleFactor = 4)
        {
            if (query.Length != _dimension)
                throw new ArgumentException($"Query vector dimension {query.Length} must match column dimension {_dimension}.");

            if (topK <= 0 || _length == 0)
                return Array.Empty<VectorMatch>();

            EnsureBqIndex();

            int candidateTarget = Math.Max(topK * Math.Max(oversampleFactor, 2), 32);
            int maxCandidates = Math.Min(candidateTarget, _length);

            // Quantize query to 1-Bit BQ on stack
            Span<ulong> queryBq = stackalloc ulong[_bqUlongs];
            Quantize1Bit(query, queryBq);

            var poolInt = ArrayPool<int>.Shared;
            int[] candRows = poolInt.Rent(_length);
            int[] candDists = poolInt.Rent(_length);
            int validCount = 0;

            try
            {
                fixed (ulong* pBq = _bqData)
                {
                    for (int r = 0; r < _length; r++)
                    {
                        // Fused SelectionMask check: skip unselected rows immediately with zero overhead
                        if (selectionMask != null && !selectionMask.IsSelected(r))
                            continue;

                        if (IsNull(r))
                            continue;

                        ulong* pEntry = pBq + (r * _bqUlongs);
                        var entrySpan = new ReadOnlySpan<ulong>(pEntry, _bqUlongs);
                        int dist = ComputeHammingDistance(queryBq, entrySpan);

                        candRows[validCount] = r;
                        candDists[validCount] = dist;
                        validCount++;
                    }
                }

                if (validCount == 0)
                    return Array.Empty<VectorMatch>();

                int stage2Count = Math.Min(maxCandidates, validCount);
                QuickSelectTopSmallest(candRows, candDists, 0, validCount - 1, stage2Count);

                // Stage 2: Exact SIMD Cosine Reranking
                var matches = new VectorMatch[stage2Count];
                for (int c = 0; c < stage2Count; c++)
                {
                    int row = candRows[c];
                    int offset = row * _dimension;
                    var storedVec = new ReadOnlySpan<float>(_flatData, offset, _dimension);
                    float score = ComputeCosineSimilarity(query, storedVec);
                    matches[c] = new VectorMatch(row, score);
                }

                // Sort descending by score
                Array.Sort(matches, (a, b) => b.Score.CompareTo(a.Score));

                int finalK = Math.Min(topK, matches.Length);
                if (finalK == matches.Length)
                    return matches;

                var result = new VectorMatch[finalK];
                Array.Copy(matches, result, finalK);
                return result;
            }
            finally
            {
                poolInt.Return(candRows);
                poolInt.Return(candDists);
            }
        }

        #region Private Engine Helpers

        private void EnsureCapacity(int required)
        {
            if (required * _dimension <= _flatData.Length) return;

            int newCapacity = Math.Max(_length * 2, required);
            Array.Resize(ref _flatData, newCapacity * _dimension);

            if (_bqData != null)
            {
                Array.Resize(ref _bqData, newCapacity * _bqUlongs);
            }
        }

        private void EnsureNullBitmap()
        {
            if (_nullBitmap == null)
            {
                int bytes = (_length + 7) >> 3;
                _nullBitmap = new byte[Math.Max(bytes, 4)];
                for (int i = 0; i < bytes; i++) _nullBitmap[i] = 0xFF;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Quantize1Bit(ReadOnlySpan<float> source, Span<ulong> destination)
        {
            destination.Clear();
            int len = source.Length;
            for (int i = 0; i < len; i++)
            {
                if (source[i] >= 0.0f)
                {
                    destination[i >> 6] |= (1UL << (i & 63));
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ComputeHammingDistance(ReadOnlySpan<ulong> a, ReadOnlySpan<ulong> b)
        {
            int dist = 0;
            int len = a.Length;
            for (int i = 0; i < len; i++)
            {
                dist += PopCount64(a[i] ^ b[i]);
            }
            return dist;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PopCount64(ulong value)
        {
#if NET8_0_OR_GREATER
            return BitOperations.PopCount(value);
#else
            value -= (value >> 1) & 0x5555555555555555UL;
            value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
            value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((value * 0x0101010101010101UL) >> 56);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ComputeCosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            int len = a.Length;
            if (len == 0) return 0f;

            fixed (float* pA = a, pB = b)
            {
#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && len >= 8)
                {
                    int i = 0;
                    int limit = len - 7;
                    var dotVec = Vector256<float>.Zero;
                    var normAVec = Vector256<float>.Zero;
                    var normBVec = Vector256<float>.Zero;

                    if (Fma.IsSupported)
                    {
                        for (; i < limit; i += 8)
                        {
                            var va = Vector256.Load(pA + i);
                            var vb = Vector256.Load(pB + i);
                            dotVec = Fma.MultiplyAdd(va, vb, dotVec);
                            normAVec = Fma.MultiplyAdd(va, va, normAVec);
                            normBVec = Fma.MultiplyAdd(vb, vb, normBVec);
                        }
                    }
                    else
                    {
                        for (; i < limit; i += 8)
                        {
                            var va = Vector256.Load(pA + i);
                            var vb = Vector256.Load(pB + i);
                            dotVec += va * vb;
                            normAVec += va * va;
                            normBVec += vb * vb;
                        }
                    }

                    float dot = Vector256.Sum(dotVec);
                    float normA = Vector256.Sum(normAVec);
                    float normB = Vector256.Sum(normBVec);

                    for (; i < len; i++)
                    {
                        float fa = pA[i];
                        float fb = pB[i];
                        dot += fa * fb;
                        normA += fa * fa;
                        normB += fb * fb;
                    }

                    if (normA <= 1e-12f || normB <= 1e-12f) return 0f;
                    float denom = (float)Math.Sqrt(normA * normB);
                    float cos = dot / denom;
                    return cos > 1.0f ? 1.0f : (cos < -1.0f ? -1.0f : cos);
                }
#endif
                float dotS = 0f;
                float normAS = 0f;
                float normBS = 0f;

                for (int i = 0; i < len; i++)
                {
                    float fa = pA[i];
                    float fb = pB[i];
                    dotS += fa * fb;
                    normAS += fa * fa;
                    normBS += fb * fb;
                }

                if (normAS <= 1e-12f || normBS <= 1e-12f) return 0f;
                float denomS = (float)Math.Sqrt(normAS * normBS);
                float cosS = dotS / denomS;
                return cosS > 1.0f ? 1.0f : (cosS < -1.0f ? -1.0f : cosS);
            }
        }

        private static void QuickSelectTopSmallest(int[] indices, int[] distances, int left, int right, int k)
        {
            if (left >= right) return;

            int pivotIdx = Partition(indices, distances, left, right);
            if (pivotIdx == k - 1) return;

            if (pivotIdx > k - 1)
            {
                QuickSelectTopSmallest(indices, distances, left, pivotIdx - 1, k);
            }
            else
            {
                QuickSelectTopSmallest(indices, distances, pivotIdx + 1, right, k);
            }
        }

        private static int Partition(int[] indices, int[] distances, int left, int right)
        {
            int pivot = distances[right];
            int i = left - 1;

            for (int j = left; j < right; j++)
            {
                if (distances[j] <= pivot)
                {
                    i++;
                    int tempIdx = indices[i];
                    indices[i] = indices[j];
                    indices[j] = tempIdx;

                    int tempDist = distances[i];
                    distances[i] = distances[j];
                    distances[j] = tempDist;
                }
            }

            int tIdx = indices[i + 1];
            indices[i + 1] = indices[right];
            indices[right] = tIdx;

            int tDist = distances[i + 1];
            distances[i + 1] = distances[right];
            distances[right] = tDist;

            return i + 1;
        }

        #endregion
    }
}
