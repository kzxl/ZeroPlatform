using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using ZeroLlm.Core.Engine;
using ZeroLlm.Core.Memory;

namespace ZeroLlm.Core.Layers
{
    /// <summary>
    /// Pure C# Grouped-Query Attention (GQA) execution kernel with zero-lock Paged KV-Cache retrieval
    /// and SIMD (AVX2/AVX-512) vector acceleration.
    /// </summary>
    public static unsafe class GqaAttention
    {
        public static void Forward(
            ReadOnlySpan<float> q,
            int seqId,
            int layer,
            PagedKvCache kvCache,
            int qHeadCount,
            int kvHeadCount,
            int headDim,
            Span<float> output)
        {
            var kvView = kvCache.GetSequenceView(seqId, layer);
            Forward(q, kvView, kvView.SequenceLength, qHeadCount, kvHeadCount, headDim, output);
        }

        public static void Forward(
            ReadOnlySpan<float> q,
            KvSequenceView kvView,
            int seqLen,
            int qHeadCount,
            int kvHeadCount,
            int headDim,
            Span<float> output)
        {
            if (seqLen <= 0)
            {
                output.Clear();
                return;
            }

            int groupSize = qHeadCount / kvHeadCount;
            float scale = 1.0f / (float)Math.Sqrt(headDim);

            // Scratch buffer for attention weights across context length
            Span<float> scores = stackalloc float[Math.Min(seqLen, 1024)];
            float[]? rentedScores = null;
            if (seqLen > 1024)
            {
                rentedScores = new float[seqLen];
                scores = rentedScores;
            }

            int vecSize = Vector<float>.Count;
            int simdLimit = headDim - (headDim % vecSize);

            try
            {
                for (int qh = 0; qh < qHeadCount; qh++)
                {
                    int kvHead = qh / groupSize;
                    var qSpan = q.Slice(qh * headDim, headDim);
                    var outSpan = output.Slice(qh * headDim, headDim);
                    outSpan.Clear();

                    float maxScore = float.NegativeInfinity;

                    // 1. Compute dot-product attention scores
                    fixed (float* pQ = qSpan)
                    {
                        for (int t = 0; t < seqLen; t++)
                        {
                            var keyAllHeads = kvView.GetKeySpan(t);
                            var kSpan = keyAllHeads.Slice(kvHead * headDim, headDim);

                            fixed (float* pK = kSpan)
                            {
                                float dot = LlmEngine.DotProductSimd(pQ, pK, headDim);
                                float s = dot * scale;
                                scores[t] = s;
                                if (s > maxScore) maxScore = s;
                            }
                        }
                    }

                    // 2. Softmax normalization
                    float sumExp = 0.0f;
                    for (int t = 0; t < seqLen; t++)
                    {
                        float exp = (float)Math.Exp(scores[t] - maxScore);
                        scores[t] = exp;
                        sumExp += exp;
                    }

                    float invSum = sumExp > 0 ? 1.0f / sumExp : 0.0f;

                    // 3. SIMD Weighted accumulation of Value vectors
                    fixed (float* pOut = outSpan)
                    {
                        for (int t = 0; t < seqLen; t++)
                        {
                            float weight = scores[t] * invSum;
                            if (weight <= 0.0f) continue;

                            var valAllHeads = kvView.GetValueSpan(t);
                            var vSpan = valAllHeads.Slice(kvHead * headDim, headDim);

                            fixed (float* pV = vSpan)
                            {
                                var vWeight = new Vector<float>(weight);
                                for (int d = 0; d < simdLimit; d += vecSize)
                                {
                                    *(Vector<float>*)(pOut + d) += vWeight * *(Vector<float>*)(pV + d);
                                }
                                for (int d = simdLimit; d < headDim; d++)
                                {
                                    pOut[d] += weight * pV[d];
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                rentedScores = null;
            }
        }
    }
}
