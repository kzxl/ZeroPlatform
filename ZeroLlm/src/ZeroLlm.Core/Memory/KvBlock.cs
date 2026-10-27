using System;

namespace ZeroLlm.Core.Memory
{
    /// <summary>
    /// A single fixed-size page block in the Paged KV-Cache holding Key and Value states across layers.
    /// </summary>
    public sealed class KvBlock
    {
        public int BlockId { get; }
        public int BlockSize { get; }
        public int LayerCount { get; }
        public int KvHeadCount { get; }
        public int HeadDim { get; }

        // Flat storage: [LayerCount * BlockSize * KvHeadCount * HeadDim]
        private readonly float[] _keyStorage;
        private readonly float[] _valStorage;
        private readonly int _layerStride;
        private readonly int _tokenStride;

        public int RefCount { get; set; }

        public KvBlock(int blockId, int blockSize, int layerCount, int kvHeadCount, int headDim)
        {
            BlockId = blockId;
            BlockSize = blockSize;
            LayerCount = layerCount;
            KvHeadCount = kvHeadCount;
            HeadDim = headDim;

            _tokenStride = kvHeadCount * headDim;
            _layerStride = blockSize * _tokenStride;
            int totalFloats = layerCount * _layerStride;

            _keyStorage = new float[totalFloats];
            _valStorage = new float[totalFloats];
            RefCount = 0;
        }

        public Span<float> GetKeySpan(int layer, int slotIndex)
        {
            int offset = layer * _layerStride + slotIndex * _tokenStride;
            return new Span<float>(_keyStorage, offset, _tokenStride);
        }

        public Span<float> GetValueSpan(int layer, int slotIndex)
        {
            int offset = layer * _layerStride + slotIndex * _tokenStride;
            return new Span<float>(_valStorage, offset, _tokenStride);
        }

        public void Reset()
        {
            RefCount = 0;
            Array.Clear(_keyStorage, 0, _keyStorage.Length);
            Array.Clear(_valStorage, 0, _valStorage.Length);
        }
    }
}
