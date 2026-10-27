using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ZeroLlm.Core.Memory
{
    /// <summary>
    /// Direct zero-lock sequence accessor for GQA attention, eliminating per-token monitor lock contention.
    /// </summary>
    public readonly struct KvSequenceView
    {
        private readonly KvBlock[] _allBlocks;
        private readonly int[] _blockIndices;
        private readonly int _blockSize;
        private readonly int _layer;
        public int SequenceLength { get; }

        public KvSequenceView(KvBlock[] allBlocks, int[] blockIndices, int blockSize, int layer, int sequenceLength)
        {
            _allBlocks = allBlocks;
            _blockIndices = blockIndices;
            _blockSize = blockSize;
            _layer = layer;
            SequenceLength = sequenceLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<float> GetKeySpan(int tokenPos)
        {
            int blockIdx = tokenPos / _blockSize;
            int slotIdx = tokenPos % _blockSize;
            return _allBlocks[_blockIndices[blockIdx]].GetKeySpan(_layer, slotIdx);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<float> GetValueSpan(int tokenPos)
        {
            int blockIdx = tokenPos / _blockSize;
            int slotIdx = tokenPos % _blockSize;
            return _allBlocks[_blockIndices[blockIdx]].GetValueSpan(_layer, slotIdx);
        }
    }

    /// <summary>
    /// High-throughput Paged KV-Cache block allocator preventing memory fragmentation in LLM inference.
    /// </summary>
    public sealed class PagedKvCache : IDisposable
    {
        public int BlockSize { get; }
        public int LayerCount { get; }
        public int KvHeadCount { get; }
        public int HeadDim { get; }
        public int TotalBlocks { get; }

        private readonly KvBlock[] _allBlocks;
        private readonly Stack<int> _freeBlocks;
        private readonly Dictionary<int, List<int>> _blockTables = new Dictionary<int, List<int>>();
        private readonly Dictionary<int, int> _seqLengths = new Dictionary<int, int>();
        private readonly object _lock = new object();

        public PagedKvCache(int totalBlocks, int blockSize, int layerCount, int kvHeadCount, int headDim)
        {
            if (totalBlocks <= 0) throw new ArgumentOutOfRangeException(nameof(totalBlocks));
            if (blockSize <= 0) throw new ArgumentOutOfRangeException(nameof(blockSize));

            TotalBlocks = totalBlocks;
            BlockSize = blockSize;
            LayerCount = layerCount;
            KvHeadCount = kvHeadCount;
            HeadDim = headDim;

            _allBlocks = new KvBlock[totalBlocks];
            _freeBlocks = new Stack<int>(totalBlocks);

            for (int i = 0; i < totalBlocks; i++)
            {
                _allBlocks[i] = new KvBlock(i, blockSize, layerCount, kvHeadCount, headDim);
                _freeBlocks.Push(i);
            }
        }

        public int FreeBlockCount
        {
            get
            {
                lock (_lock)
                {
                    return _freeBlocks.Count;
                }
            }
        }

        public int GetSequenceLength(int seqId)
        {
            lock (_lock)
            {
                return _seqLengths.TryGetValue(seqId, out int len) ? len : 0;
            }
        }

        /// <summary>
        /// Appends key and value states for a specific layer at the current sequence token position.
        /// When layer == LayerCount - 1, the sequence length advances.
        /// </summary>
        public void AppendToken(int seqId, int layer, ReadOnlySpan<float> key, ReadOnlySpan<float> val)
        {
            lock (_lock)
            {
                if (!_blockTables.TryGetValue(seqId, out var table))
                {
                    table = new List<int>();
                    _blockTables[seqId] = table;
                    _seqLengths[seqId] = 0;
                }

                int currentLen = _seqLengths[seqId];
                int tokenIdx = currentLen;
                int blockIndexInSeq = tokenIdx / BlockSize;
                int slotIndex = tokenIdx % BlockSize;

                // Ensure physical block is allocated
                while (table.Count <= blockIndexInSeq)
                {
                    if (_freeBlocks.Count == 0)
                    {
                        throw new InvalidOperationException($"Paged KV-cache exhausted! (All {TotalBlocks} blocks in use)");
                    }

                    int newBlockId = _freeBlocks.Pop();
                    _allBlocks[newBlockId].RefCount = 1;
                    table.Add(newBlockId);
                }

                var block = _allBlocks[table[blockIndexInSeq]];
                key.CopyTo(block.GetKeySpan(layer, slotIndex));
                val.CopyTo(block.GetValueSpan(layer, slotIndex));

                // If this is the last layer in the Transformer block, advance token counter
                if (layer == LayerCount - 1)
                {
                    _seqLengths[seqId] = currentLen + 1;
                }
            }
        }

        /// <summary>
        /// Writes key and value states for a specific layer at an explicit token position during batched prompt prefill.
        /// </summary>
        public void AppendTokenAt(int seqId, int layer, int tokenIdx, ReadOnlySpan<float> key, ReadOnlySpan<float> val)
        {
            lock (_lock)
            {
                if (!_blockTables.TryGetValue(seqId, out var table))
                {
                    table = new List<int>();
                    _blockTables[seqId] = table;
                    _seqLengths[seqId] = 0;
                }

                int blockIndexInSeq = tokenIdx / BlockSize;
                int slotIndex = tokenIdx % BlockSize;

                while (table.Count <= blockIndexInSeq)
                {
                    if (_freeBlocks.Count == 0)
                    {
                        throw new InvalidOperationException($"Paged KV-cache exhausted! (All {TotalBlocks} blocks in use)");
                    }

                    int newBlockId = _freeBlocks.Pop();
                    _allBlocks[newBlockId].RefCount = 1;
                    table.Add(newBlockId);
                }

                var block = _allBlocks[table[blockIndexInSeq]];
                key.CopyTo(block.GetKeySpan(layer, slotIndex));
                val.CopyTo(block.GetValueSpan(layer, slotIndex));

                if (layer == LayerCount - 1 && tokenIdx + 1 > _seqLengths[seqId])
                {
                    _seqLengths[seqId] = tokenIdx + 1;
                }
            }
        }

        public ReadOnlySpan<float> GetKey(int seqId, int layer, int tokenPos)
        {
            lock (_lock)
            {
                if (!_blockTables.TryGetValue(seqId, out var table))
                {
                    throw new ArgumentException($"Sequence {seqId} does not exist.");
                }

                int blockIdx = tokenPos / BlockSize;
                int slotIdx = tokenPos % BlockSize;
                return _allBlocks[table[blockIdx]].GetKeySpan(layer, slotIdx);
            }
        }

        public ReadOnlySpan<float> GetValue(int seqId, int layer, int tokenPos)
        {
            lock (_lock)
            {
                if (!_blockTables.TryGetValue(seqId, out var table))
                {
                    throw new ArgumentException($"Sequence {seqId} does not exist.");
                }

                int blockIdx = tokenPos / BlockSize;
                int slotIdx = tokenPos % BlockSize;
                return _allBlocks[table[blockIdx]].GetValueSpan(layer, slotIdx);
            }
        }

        /// <summary>
        /// Obtains a zero-lock, direct-indexed view of Key and Value vectors for a sequence and layer.
        /// </summary>
        public KvSequenceView GetSequenceView(int seqId, int layer)
        {
            lock (_lock)
            {
                if (!_blockTables.TryGetValue(seqId, out var table) || !_seqLengths.TryGetValue(seqId, out int seqLen))
                {
                    return new KvSequenceView(_allBlocks, Array.Empty<int>(), BlockSize, layer, 0);
                }

                int[] indices = table.ToArray();
                return new KvSequenceView(_allBlocks, indices, BlockSize, layer, seqLen);
            }
        }

        /// <summary>
        /// Obtains a zero-lock, direct-indexed view of Key and Value vectors with an explicit causal sequence length.
        /// </summary>
        public KvSequenceView GetSequenceView(int seqId, int layer, int causalSequenceLength)
        {
            lock (_lock)
            {
                if (!_blockTables.TryGetValue(seqId, out var table))
                {
                    return new KvSequenceView(_allBlocks, Array.Empty<int>(), BlockSize, layer, 0);
                }

                int[] indices = table.ToArray();
                return new KvSequenceView(_allBlocks, indices, BlockSize, layer, causalSequenceLength);
            }
        }

        public void FreeSequence(int seqId)
        {
            lock (_lock)
            {
                if (_blockTables.TryGetValue(seqId, out var table))
                {
                    foreach (int blockId in table)
                    {
                        var block = _allBlocks[blockId];
                        block.RefCount--;
                        if (block.RefCount <= 0)
                        {
                            block.Reset();
                            _freeBlocks.Push(blockId);
                        }
                    }

                    _blockTables.Remove(seqId);
                    _seqLengths.Remove(seqId);
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _blockTables.Clear();
                _seqLengths.Clear();
                _freeBlocks.Clear();
                for (int i = 0; i < _allBlocks.Length; i++)
                {
                    _allBlocks[i].Reset();
                    _freeBlocks.Push(i);
                }
            }
        }
    }
}
