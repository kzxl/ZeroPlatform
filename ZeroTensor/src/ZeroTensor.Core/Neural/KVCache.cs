using System;

namespace ZeroTensor.Core.Neural
{
    /// <summary>
    /// High-performance pre-allocated Key-Value Cache for autoregressive LLM and Transformer inference.
    /// Eliminates dynamic memory allocation and GC pressure on every token generation step.
    /// Internal tensor layout: [BatchSize, NumHeads, MaxSeqLength, HeadDim].
    /// </summary>
    public sealed class KVCache<T> : IDisposable where T : unmanaged, IEquatable<T>
    {
        private readonly Tensor<T> _keys;
        private readonly Tensor<T> _values;
        private int _currentLength;
        private readonly int _maxSeqLen;
        private readonly int _batchSize;
        private readonly int _numHeads;
        private readonly int _headDim;

        /// <summary>
        /// Gets the current number of cached tokens along the sequence dimension.
        /// </summary>
        public int CurrentLength => _currentLength;

        /// <summary>
        /// Gets the maximum sequence capacity of the cache.
        /// </summary>
        public int MaxSeqLength => _maxSeqLen;

        /// <summary>
        /// Gets the batch size dimension.
        /// </summary>
        public int BatchSize => _batchSize;

        /// <summary>
        /// Gets the number of attention heads (or KV heads for Grouped Query Attention).
        /// </summary>
        public int NumHeads => _numHeads;

        /// <summary>
        /// Gets the dimension of each attention head.
        /// </summary>
        public int HeadDim => _headDim;

        /// <summary>
        /// Gets the underlying full pre-allocated Key cache tensor: [BatchSize, NumHeads, MaxSeqLength, HeadDim].
        /// </summary>
        public Tensor<T> KeyCache => _keys;

        /// <summary>
        /// Gets the underlying full pre-allocated Value cache tensor: [BatchSize, NumHeads, MaxSeqLength, HeadDim].
        /// </summary>
        public Tensor<T> ValueCache => _values;

        /// <summary>
        /// Initializes a new Key-Value cache with pre-allocated buffer memory.
        /// </summary>
        public KVCache(int batchSize, int numHeads, int maxSeqLength, int headDim)
        {
            if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize));
            if (numHeads <= 0) throw new ArgumentOutOfRangeException(nameof(numHeads));
            if (maxSeqLength <= 0) throw new ArgumentOutOfRangeException(nameof(maxSeqLength));
            if (headDim <= 0) throw new ArgumentOutOfRangeException(nameof(headDim));

            _batchSize = batchSize;
            _numHeads = numHeads;
            _maxSeqLen = maxSeqLength;
            _headDim = headDim;
            _currentLength = 0;

            // Shape: [batchSize, numHeads, maxSeqLength, headDim]
            _keys = new Tensor<T>(batchSize, numHeads, maxSeqLength, headDim);
            _values = new Tensor<T>(batchSize, numHeads, maxSeqLength, headDim);
        }

        /// <summary>
        /// Appends new keys and values into the cache at the current token position.
        /// Uses ultra-fast contiguous block memory copies per head, eliminating delegate iteration overhead.
        /// </summary>
        /// <param name="newKeys">Tensor of shape [batchSize, numHeads, newSeqLen, headDim]</param>
        /// <param name="newValues">Tensor of shape [batchSize, numHeads, newSeqLen, headDim]</param>
        public void Append(Tensor<T> newKeys, Tensor<T> newValues)
        {
            if (newKeys == null) throw new ArgumentNullException(nameof(newKeys));
            if (newValues == null) throw new ArgumentNullException(nameof(newValues));

            if (newKeys.Rank != 4 || newValues.Rank != 4)
            {
                throw new ArgumentException("Input newKeys and newValues must have rank 4 [batchSize, numHeads, newSeqLen, headDim].");
            }

            int newLen = newKeys.Shape[2];
            if (_currentLength + newLen > _maxSeqLen)
            {
                throw new InvalidOperationException($"Cache capacity exceeded: current={_currentLength}, new={newLen}, max={_maxSeqLen}.");
            }

            CopyHeadSlices(newKeys, _keys, _currentLength, newLen, _batchSize, _numHeads, _maxSeqLen, _headDim);
            CopyHeadSlices(newValues, _values, _currentLength, newLen, _batchSize, _numHeads, _maxSeqLen, _headDim);

            _currentLength += newLen;
        }

        private static unsafe void CopyHeadSlices(Tensor<T> src, Tensor<T> dstCache, int startSeqPos, int newSeqLen, int batchSize, int numHeads, int maxSeqLen, int headDim)
        {
            var srcContig = src.IsContiguous ? src : src.ToContiguous();
            int copyElements = newSeqLen * headDim;

            int srcBatchStride = numHeads * newSeqLen * headDim;
            int srcHeadStride = newSeqLen * headDim;

            int dstBatchStride = numHeads * maxSeqLen * headDim;
            int dstHeadStride = maxSeqLen * headDim;

            fixed (T* pSrcBase = &srcContig.Storage.GetPinnableReference(srcContig.Offset))
            fixed (T* pDstBase = &dstCache.Storage.GetPinnableReference(dstCache.Offset))
            {
                byte* byteSrc = (byte*)pSrcBase;
                byte* byteDst = (byte*)pDstBase;
                int typeSize = sizeof(T);
                int copyBytes = copyElements * typeSize;

                for (int b = 0; b < batchSize; b++)
                {
                    for (int h = 0; h < numHeads; h++)
                    {
                        int srcOffset = (b * srcBatchStride + h * srcHeadStride) * typeSize;
                        int dstOffset = (b * dstBatchStride + h * dstHeadStride + startSeqPos * headDim) * typeSize;

                        System.Runtime.CompilerServices.Unsafe.CopyBlockUnaligned(byteDst + dstOffset, byteSrc + srcOffset, (uint)copyBytes);
                    }
                }
            }
        }

        /// <summary>
        /// Returns an O(1) zero-copy view of valid cached keys up to the current length: [batchSize, numHeads, CurrentLength, headDim].
        /// </summary>
        public Tensor<T> GetValidKeys()
        {
            if (_currentLength == 0) return _keys.Slice(2, 0, 0);
            return _keys.Slice(2, 0, _currentLength);
        }

        /// <summary>
        /// Returns an O(1) zero-copy view of valid cached values up to the current length: [batchSize, numHeads, CurrentLength, headDim].
        /// </summary>
        public Tensor<T> GetValidValues()
        {
            if (_currentLength == 0) return _values.Slice(2, 0, 0);
            return _values.Slice(2, 0, _currentLength);
        }

        /// <summary>
        /// Resets the cache length to 0 for a new sequence without freeing memory.
        /// </summary>
        public void Reset()
        {
            _currentLength = 0;
        }

        /// <summary>
        /// Releases all underlying tensor resources.
        /// </summary>
        public void Dispose()
        {
            _keys.Dispose();
            _values.Dispose();
        }
    }
}
