using System;
using ZeroTensor.Core;

namespace ZeroInference.Core.Layers
{
    /// <summary>
    /// Rotary Position Embedding (RoPE) for Transformer query/key sequence tensors.
    /// Pure C# implementation with zero external dependencies.
    /// </summary>
    public sealed class RotaryEmbedding
    {
        public int HeadDim { get; }
        public int MaxSeqLen { get; }
        public float ThetaBase { get; }

        private readonly float[] _cosTable;
        private readonly float[] _sinTable;

        public RotaryEmbedding(int headDim, int maxSeqLen = 4096, float thetaBase = 10000.0f)
        {
            if (headDim <= 0 || headDim % 2 != 0)
                throw new ArgumentException("HeadDim must be a positive even integer.", nameof(headDim));
            if (maxSeqLen <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxSeqLen));

            HeadDim = headDim;
            MaxSeqLen = maxSeqLen;
            ThetaBase = thetaBase;

            int halfDim = headDim / 2;
            _cosTable = new float[maxSeqLen * halfDim];
            _sinTable = new float[maxSeqLen * halfDim];

            PrecomputeFrequencies(halfDim);
        }

        private void PrecomputeFrequencies(int halfDim)
        {
            for (int i = 0; i < halfDim; i++)
            {
                double exponent = 2.0 * i / HeadDim;
                double freq = 1.0 / Math.Pow(ThetaBase, exponent);

                for (int pos = 0; pos < MaxSeqLen; pos++)
                {
                    double angle = pos * freq;
                    int idx = pos * halfDim + i;
                    _cosTable[idx] = (float)Math.Cos(angle);
                    _sinTable[idx] = (float)Math.Sin(angle);
                }
            }
        }

        /// <summary>
        /// Applies Rotary Position Embedding in-place on a 1D span representing a single head vector [HeadDim] at position 'pos'.
        /// </summary>
        public void Apply(Span<float> headVector, int pos)
        {
            if (headVector.Length < HeadDim)
                throw new ArgumentException("Vector length must be at least HeadDim.", nameof(headVector));
            if (pos < 0 || pos >= MaxSeqLen)
                throw new ArgumentOutOfRangeException(nameof(pos), $"Position must be between 0 and {MaxSeqLen - 1}.");

            int halfDim = HeadDim / 2;
            int tableOffset = pos * halfDim;

            for (int i = 0; i < halfDim; i++)
            {
                float x0 = headVector[i];
                float x1 = headVector[i + halfDim];

                float cos = _cosTable[tableOffset + i];
                float sin = _sinTable[tableOffset + i];

                headVector[i] = x0 * cos - x1 * sin;
                headVector[i + halfDim] = x0 * sin + x1 * cos;
            }
        }

        /// <summary>
        /// Applies RoPE over a 3D/4D tensor [SeqLen, NumHeads, HeadDim] or [Batch, SeqLen, NumHeads, HeadDim].
        /// </summary>
        public void Apply(Tensor<float> tensor, int startPos = 0)
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));

            var dataSpan = tensor.AsSpan();

            int lastDim = tensor.Shape[tensor.Rank - 1];
            if (lastDim != HeadDim)
                throw new ArgumentException($"Last dimension {lastDim} does not match HeadDim {HeadDim}.");

            int numHeads = tensor.Shape[tensor.Rank - 2];
            int seqLen = tensor.Shape[tensor.Rank - 3];
            int batch = tensor.Rank == 4 ? tensor.Shape[0] : 1;

            int headStride = HeadDim;
            int seqStride = numHeads * HeadDim;
            int batchStride = seqLen * seqStride;

            for (int b = 0; b < batch; b++)
            {
                int bOffset = b * batchStride;
                for (int s = 0; s < seqLen; s++)
                {
                    int pos = startPos + s;
                    int sOffset = bOffset + s * seqStride;
                    for (int h = 0; h < numHeads; h++)
                    {
                        int hOffset = sOffset + h * headStride;
                        Span<float> headSpan = dataSpan.Slice(hOffset, HeadDim);
                        Apply(headSpan, pos);
                    }
                }
            }
        }
    }
}
