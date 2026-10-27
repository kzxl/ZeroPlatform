using System;
using System.Runtime.CompilerServices;

namespace ZeroLlm.Core.Layers
{
    /// <summary>
    /// Pure C# Rotary Position Embedding (RoPE) kernel for Query and Key projections.
    /// Hardware-optimized with precomputed cos/sin table caching and pointer arithmetic.
    /// </summary>
    public static unsafe class RoPE
    {
        private static readonly object _initLock = new object();
        private static float[]? _cachedCos;
        private static float[]? _cachedSin;
        private static int _cachedMaxPos = 0;
        private static int _cachedHeadDim = 0;
        private static float _cachedFreqBase = 0f;

        /// <summary>
        /// Precomputes and caches trigonometric rotary embedding tables to eliminate transcendental Math calls.
        /// </summary>
        public static void EnsureCache(int maxPositions, int headDim, float freqBase = 10000.0f)
        {
            if (_cachedCos != null && _cachedMaxPos >= maxPositions && _cachedHeadDim == headDim && Math.Abs(_cachedFreqBase - freqBase) < 1e-4f)
            {
                return;
            }

            lock (_initLock)
            {
                if (_cachedCos != null && _cachedMaxPos >= maxPositions && _cachedHeadDim == headDim && Math.Abs(_cachedFreqBase - freqBase) < 1e-4f)
                {
                    return;
                }

                int halfDim = headDim / 2;
                float[] cosTable = new float[maxPositions * halfDim];
                float[] sinTable = new float[maxPositions * halfDim];

                double[] freqs = new double[halfDim];
                for (int i = 0; i < halfDim; i++)
                {
                    double exponent = 2.0 * i / headDim;
                    freqs[i] = 1.0 / Math.Pow(freqBase, exponent);
                }

                for (int p = 0; p < maxPositions; p++)
                {
                    int offset = p * halfDim;
                    for (int i = 0; i < halfDim; i++)
                    {
                        double theta = p * freqs[i];
                        cosTable[offset + i] = (float)Math.Cos(theta);
                        sinTable[offset + i] = (float)Math.Sin(theta);
                    }
                }

                _cachedCos = cosTable;
                _cachedSin = sinTable;
                _cachedMaxPos = maxPositions;
                _cachedHeadDim = headDim;
                _cachedFreqBase = freqBase;
            }
        }

        /// <summary>
        /// Applies in-place rotary position embedding to a head vector of length headDim.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyInplace(Span<float> vector, int pos, int headDim, float freqBase = 10000.0f)
        {
            if (headDim % 2 != 0)
            {
                throw new ArgumentException("Head dimension must be even for RoPE.", nameof(headDim));
            }

            int halfDim = headDim / 2;
            var cosArr = _cachedCos;
            var sinArr = _cachedSin;

            if (cosArr != null && sinArr != null && pos < _cachedMaxPos && headDim == _cachedHeadDim && Math.Abs(_cachedFreqBase - freqBase) < 1e-4f)
            {
                int offset = pos * halfDim;
                fixed (float* pVec = vector)
                fixed (float* pCos = &cosArr[offset])
                fixed (float* pSin = &sinArr[offset])
                {
                    for (int i = 0; i < halfDim; i++)
                    {
                        int idx0 = 2 * i;
                        int idx1 = 2 * i + 1;

                        float x0 = pVec[idx0];
                        float x1 = pVec[idx1];
                        float c = pCos[i];
                        float s = pSin[i];

                        pVec[idx0] = x0 * c - x1 * s;
                        pVec[idx1] = x0 * s + x1 * c;
                    }
                }
                return;
            }

            // Fallback for uncached or out-of-range positions
            fixed (float* pVec = vector)
            {
                for (int i = 0; i < halfDim; i++)
                {
                    double exponent = 2.0 * i / headDim;
                    double freq = 1.0 / Math.Pow(freqBase, exponent);
                    double theta = pos * freq;

                    float cosTheta = (float)Math.Cos(theta);
                    float sinTheta = (float)Math.Sin(theta);

                    int idx0 = 2 * i;
                    int idx1 = 2 * i + 1;

                    float x0 = pVec[idx0];
                    float x1 = pVec[idx1];

                    pVec[idx0] = x0 * cosTheta - x1 * sinTheta;
                    pVec[idx1] = x0 * sinTheta + x1 * cosTheta;
                }
            }
        }

        /// <summary>
        /// Applies in-place inverse (transposed) rotary position embedding to a head vector for backpropagation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyInverseInplace(Span<float> vector, int pos, int headDim, float freqBase = 10000.0f)
        {
            if (headDim % 2 != 0)
            {
                throw new ArgumentException("Head dimension must be even for RoPE.", nameof(headDim));
            }

            int halfDim = headDim / 2;
            var cosArr = _cachedCos;
            var sinArr = _cachedSin;

            if (cosArr != null && sinArr != null && pos < _cachedMaxPos && headDim == _cachedHeadDim && Math.Abs(_cachedFreqBase - freqBase) < 1e-4f)
            {
                int offset = pos * halfDim;
                fixed (float* pVec = vector)
                fixed (float* pCos = &cosArr[offset])
                fixed (float* pSin = &sinArr[offset])
                {
                    for (int i = 0; i < halfDim; i++)
                    {
                        int idx0 = 2 * i;
                        int idx1 = 2 * i + 1;

                        float x0 = pVec[idx0];
                        float x1 = pVec[idx1];
                        float c = pCos[i];
                        float s = pSin[i];

                        pVec[idx0] = x0 * c + x1 * s;
                        pVec[idx1] = -x0 * s + x1 * c;
                    }
                }
                return;
            }

            fixed (float* pVec = vector)
            {
                for (int i = 0; i < halfDim; i++)
                {
                    double exponent = 2.0 * i / headDim;
                    double freq = 1.0 / Math.Pow(freqBase, exponent);
                    double theta = pos * freq;

                    float cosTheta = (float)Math.Cos(theta);
                    float sinTheta = (float)Math.Sin(theta);

                    int idx0 = 2 * i;
                    int idx1 = 2 * i + 1;

                    float x0 = pVec[idx0];
                    float x1 = pVec[idx1];

                    pVec[idx0] = x0 * cosTheta + x1 * sinTheta;
                    pVec[idx1] = -x0 * sinTheta + x1 * cosTheta;
                }
            }
        }
    }
}
