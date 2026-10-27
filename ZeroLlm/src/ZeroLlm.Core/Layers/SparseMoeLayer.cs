using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using ZeroLlm.Core.Engine;
using ZeroLlm.Core.Quantization;

namespace ZeroLlm.Core.Layers
{
    /// <summary>
    /// Pure C# Standard Sparse Mixture-of-Experts (MoE) Layer.
    /// Implements Top-K Gating Router with Softmax dispatch across N independent Feed-Forward Experts.
    /// Supports FP32, Q8_0, and Q4_0 quantized weight representations with zero GC overhead during inference.
    /// </summary>
    public static unsafe class SparseMoeLayer
    {
        /// <summary>
        /// Executes a single forward token pass through the Sparse MoE layer.
        /// </summary>
        /// <param name="xNorm">Normalized hidden state input [embDim].</param>
        /// <param name="wrouter">Router/Gating projection matrix [expertCount * embDim].</param>
        /// <param name="wrouter_q8">Optional Q8_0 quantized router weights.</param>
        /// <param name="wrouter_q4">Optional Q4_0 quantized router weights.</param>
        /// <param name="experts">Array of expert FFN weights.</param>
        /// <param name="expertUsedCount">Top-K active experts per token (e.g. 2).</param>
        /// <param name="embDim">Embedding dimension (D).</param>
        /// <param name="ffnDim">Feed-forward intermediate dimension (FFN_D).</param>
        /// <param name="gateScratch">Scratch buffer for SwiGLU gate [ffnDim].</param>
        /// <param name="upScratch">Scratch buffer for SwiGLU up [ffnDim].</param>
        /// <param name="swigluScratch">Scratch buffer for SwiGLU activation [ffnDim].</param>
        /// <param name="residualOutput">Output accumulator where weighted expert outputs are added (x).</param>
        /// <param name="sharedExperts">Optional always-active shared experts (DeepSeek / Qwen-MoE architecture).</param>
        public static void Forward(
            ReadOnlySpan<float> xNorm,
            ReadOnlySpan<float> wrouter,
            BlockQ8_0[]? wrouter_q8,
            BlockQ4_0[]? wrouter_q4,
            LlmExpertWeights[]? experts,
            int expertUsedCount,
            int embDim,
            int ffnDim,
            Span<float> gateScratch,
            Span<float> upScratch,
            Span<float> swigluScratch,
            Span<float> residualOutput,
            LlmExpertWeights[]? sharedExperts = null)
        {
            // 1. Process Shared Experts (Always active, DeepSeek / Qwen style)
            if (sharedExperts != null && sharedExperts.Length > 0)
            {
                Span<float> sharedDownOut = stackalloc float[embDim];
                for (int s = 0; s < sharedExperts.Length; s++)
                {
                    var sexp = sharedExperts[s];
                    if (sexp.Wgate_Q8 != null) QuantizedKernels.MatVecMulQ8_0(sexp.Wgate_Q8, xNorm, gateScratch, ffnDim, embDim);
                    else if (sexp.Wgate_Q4 != null) QuantizedKernels.MatVecMulQ4_0(sexp.Wgate_Q4, xNorm, gateScratch, ffnDim, embDim);
                    else MatVec(xNorm, sexp.Wgate, embDim, ffnDim, gateScratch);

                    if (sexp.Wup_Q8 != null) QuantizedKernels.MatVecMulQ8_0(sexp.Wup_Q8, xNorm, upScratch, ffnDim, embDim);
                    else if (sexp.Wup_Q4 != null) QuantizedKernels.MatVecMulQ4_0(sexp.Wup_Q4, xNorm, upScratch, ffnDim, embDim);
                    else MatVec(xNorm, sexp.Wup, embDim, ffnDim, upScratch);

                    SwiGLU.Forward(gateScratch, upScratch, swigluScratch);

                    if (sexp.Wdown_Q8 != null) QuantizedKernels.MatVecMulQ8_0(sexp.Wdown_Q8, swigluScratch, sharedDownOut, embDim, ffnDim);
                    else if (sexp.Wdown_Q4 != null) QuantizedKernels.MatVecMulQ4_0(sexp.Wdown_Q4, swigluScratch, sharedDownOut, embDim, ffnDim);
                    else MatVec(swigluScratch, sexp.Wdown, ffnDim, embDim, sharedDownOut);

                    AddResidual(residualOutput, sharedDownOut, embDim);
                }
            }

            if (experts == null || experts.Length == 0) return;

            int expertCount = experts.Length;
            int topK = Math.Min(Math.Max(1, expertUsedCount), expertCount);

            // 2. Calculate router logits: [expertCount]
            Span<float> routerLogits = stackalloc float[expertCount];
            ComputeRouterLogits(xNorm, wrouter, wrouter_q8, wrouter_q4, routerLogits, expertCount, embDim);

            // 3. Select Top-K experts
            Span<int> topIndices = stackalloc int[topK];
            Span<float> topWeights = stackalloc float[topK];
            SelectTopK(routerLogits, topIndices, topWeights, topK);

            // 4. Compute Softmax normalization over selected Top-K logits
            ComputeSoftmax(topWeights);

            // 5. Dispatch to selected experts and accumulate weighted outputs
            Span<float> expertDownOut = stackalloc float[embDim];

            for (int k = 0; k < topK; k++)
            {
                int expIdx = topIndices[k];
                float weight = topWeights[k];

                if (expIdx < 0 || expIdx >= expertCount || Math.Abs(weight) < 1e-7f)
                {
                    continue;
                }

                var exp = experts[expIdx];

                // Gate projection: gate = Wgate * xNorm
                if (exp.Wgate_Q8 != null) QuantizedKernels.MatVecMulQ8_0(exp.Wgate_Q8, xNorm, gateScratch, ffnDim, embDim);
                else if (exp.Wgate_Q4 != null) QuantizedKernels.MatVecMulQ4_0(exp.Wgate_Q4, xNorm, gateScratch, ffnDim, embDim);
                else MatVec(xNorm, exp.Wgate, embDim, ffnDim, gateScratch);

                // Up projection: up = Wup * xNorm
                if (exp.Wup_Q8 != null) QuantizedKernels.MatVecMulQ8_0(exp.Wup_Q8, xNorm, upScratch, ffnDim, embDim);
                else if (exp.Wup_Q4 != null) QuantizedKernels.MatVecMulQ4_0(exp.Wup_Q4, xNorm, upScratch, ffnDim, embDim);
                else MatVec(xNorm, exp.Wup, embDim, ffnDim, upScratch);

                // SwiGLU activation
                SwiGLU.Forward(gateScratch, upScratch, swigluScratch);

                // Down projection: expertDownOut = Wdown * swigluScratch
                if (exp.Wdown_Q8 != null) QuantizedKernels.MatVecMulQ8_0(exp.Wdown_Q8, swigluScratch, expertDownOut, embDim, ffnDim);
                else if (exp.Wdown_Q4 != null) QuantizedKernels.MatVecMulQ4_0(exp.Wdown_Q4, swigluScratch, expertDownOut, embDim, ffnDim);
                else MatVec(swigluScratch, exp.Wdown, ffnDim, embDim, expertDownOut);

                // Weighted residual addition: residualOutput += weight * expertDownOut
                AddWeightedResidual(residualOutput, expertDownOut, weight, embDim);
            }
        }

        private static void ComputeRouterLogits(
            ReadOnlySpan<float> xNorm,
            ReadOnlySpan<float> wrouter,
            BlockQ8_0[]? wrouter_q8,
            BlockQ4_0[]? wrouter_q4,
            Span<float> logits,
            int expertCount,
            int embDim)
        {
            if (wrouter_q8 != null)
            {
                QuantizedKernels.MatVecMulQ8_0(wrouter_q8, xNorm, logits, expertCount, embDim);
            }
            else if (wrouter_q4 != null)
            {
                QuantizedKernels.MatVecMulQ4_0(wrouter_q4, xNorm, logits, expertCount, embDim);
            }
            else if (!wrouter.IsEmpty)
            {
                MatVec(xNorm, wrouter, embDim, expertCount, logits);
            }
            else
            {
                // Uniform fallback if router weights are uninitialized
                float uniformLogit = 0.0f;
                for (int e = 0; e < expertCount; e++)
                {
                    logits[e] = uniformLogit;
                }
            }
        }

        private static void SelectTopK(ReadOnlySpan<float> logits, Span<int> topIndices, Span<float> topWeights, int k)
        {
            int n = logits.Length;
            Span<bool> picked = stackalloc bool[n];

            for (int i = 0; i < k; i++)
            {
                float maxVal = float.MinValue;
                int maxIdx = 0;

                for (int j = 0; j < n; j++)
                {
                    if (!picked[j] && logits[j] > maxVal)
                    {
                        maxVal = logits[j];
                        maxIdx = j;
                    }
                }

                picked[maxIdx] = true;
                topIndices[i] = maxIdx;
                topWeights[i] = maxVal;
            }
        }

        private static void ComputeSoftmax(Span<float> weights)
        {
            int len = weights.Length;
            if (len == 0) return;

            float maxVal = weights[0];
            for (int i = 1; i < len; i++)
            {
                if (weights[i] > maxVal) maxVal = weights[i];
            }

            float sumExp = 0.0f;
            for (int i = 0; i < len; i++)
            {
                float expVal = (float)Math.Exp(weights[i] - maxVal);
                weights[i] = expVal;
                sumExp += expVal;
            }

            if (sumExp > 1e-12f)
            {
                float invSum = 1.0f / sumExp;
                for (int i = 0; i < len; i++)
                {
                    weights[i] *= invSum;
                }
            }
            else
            {
                float uniform = 1.0f / len;
                for (int i = 0; i < len; i++)
                {
                    weights[i] = uniform;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MatVec(ReadOnlySpan<float> input, ReadOnlySpan<float> weight, int inDim, int outDim, Span<float> output)
        {
            LlmEngine.MatVec(input, weight, inDim, outDim, output);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddResidual(Span<float> dest, ReadOnlySpan<float> src, int len)
        {
            int vecSize = Vector<float>.Count;
            int simdLimit = len - (len % vecSize);
            fixed (float* pDest = dest)
            fixed (float* pSrc = src)
            {
                for (int i = 0; i < simdLimit; i += vecSize)
                {
                    *(Vector<float>*)(pDest + i) += *(Vector<float>*)(pSrc + i);
                }
                for (int i = simdLimit; i < len; i++)
                {
                    pDest[i] += pSrc[i];
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AddWeightedResidual(Span<float> dest, ReadOnlySpan<float> src, float weight, int len)
        {
            int vecSize = Vector<float>.Count;
            int simdLimit = len - (len % vecSize);
            var vWeight = new Vector<float>(weight);
            fixed (float* pDest = dest)
            fixed (float* pSrc = src)
            {
                for (int i = 0; i < simdLimit; i += vecSize)
                {
                    *(Vector<float>*)(pDest + i) += vWeight * *(Vector<float>*)(pSrc + i);
                }
                for (int i = simdLimit; i < len; i++)
                {
                    pDest[i] += weight * pSrc[i];
                }
            }
        }
    }
}
