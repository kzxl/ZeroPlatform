using System;
using ZeroTensor.Core;

namespace ZeroInference.Core.Layers
{
    /// <summary>
    /// CPU cache-tiled online Softmax FlashAttention-2 algorithm.
    /// Computes scaled dot-product attention O = Softmax(Q * K^T * scale) * V in O(1) memory
    /// without ever materializing the quadratic N x N attention matrix.
    /// Pure C# with zero external dependencies.
    /// </summary>
    public static class FlashAttentionKernel
    {
        public const int DefaultBlockSize = 32;

        /// <summary>
        /// Computes FlashAttention-2 over 2D input matrices Q [SeqLenQ, HeadDim], K [SeqLenK, HeadDim], V [SeqLenK, HeadDim].
        /// </summary>
        public static Tensor<float> Execute(Tensor<float> q, Tensor<float> k, Tensor<float> v, float? scale = null, int blockSize = DefaultBlockSize)
        {
            if (q == null) throw new ArgumentNullException(nameof(q));
            if (k == null) throw new ArgumentNullException(nameof(k));
            if (v == null) throw new ArgumentNullException(nameof(v));

            if (q.Rank != 2 || k.Rank != 2 || v.Rank != 2)
                throw new ArgumentException("Inputs Q, K, V must be 2D matrices [SeqLen, HeadDim].");

            int seqLenQ = q.Shape[0];
            int headDim = q.Shape[1];
            int seqLenK = k.Shape[0];

            if (k.Shape[1] != headDim || v.Shape[1] != headDim || v.Shape[0] != seqLenK)
                throw new ArgumentException("Dimensions of Q, K, V must align: Q[Nq, D], K[Nk, D], V[Nk, D].");

            float s = scale ?? (1.0f / (float)Math.Sqrt(headDim));
            int outLen = seqLenQ * headDim;
            var output = new float[outLen];

            // Running stats for online Softmax per query row
            var runningMax = new float[seqLenQ];
            for (int i = 0; i < seqLenQ; i++) runningMax[i] = float.NegativeInfinity;
            var runningSum = new float[seqLenQ];

            var qSpan = q.AsSpan();
            var kSpan = k.AsSpan();
            var vSpan = v.AsSpan();

            int br = Math.Min(blockSize, seqLenQ);
            int bc = Math.Min(blockSize, seqLenK);

            // Buffer for tile scores [br, bc]
            var tileScores = new float[br * bc];

            // Outer loop over Key/Value blocks
            for (int jStart = 0; jStart < seqLenK; jStart += bc)
            {
                int curBc = Math.Min(bc, seqLenK - jStart);

                // Inner loop over Query blocks
                for (int iStart = 0; iStart < seqLenQ; iStart += br)
                {
                    int curBr = Math.Min(br, seqLenQ - iStart);

                    // 1. Compute tile scores S_ij = (Q_i * K_j^T) * s: [curBr, curBc]
                    for (int r = 0; r < curBr; r++)
                    {
                        int qRowIdx = (iStart + r) * headDim;
                        for (int c = 0; c < curBc; c++)
                        {
                            int kRowIdx = (jStart + c) * headDim;
                            float dot = 0f;
                            for (int d = 0; d < headDim; d++)
                            {
                                dot += qSpan[qRowIdx + d] * kSpan[kRowIdx + d];
                            }
                            tileScores[r * bc + c] = dot * s;
                        }
                    }

                    // 2. Online Softmax update per query row in this tile
                    for (int r = 0; r < curBr; r++)
                    {
                        int globalRow = iStart + r;
                        int outRowIdx = globalRow * headDim;

                        // Find max in this tile row
                        float tileRowMax = float.NegativeInfinity;
                        for (int c = 0; c < curBc; c++)
                        {
                            float score = tileScores[r * bc + c];
                            if (score > tileRowMax) tileRowMax = score;
                        }

                        float oldMax = runningMax[globalRow];
                        float newMax = Math.Max(oldMax, tileRowMax);
                        float alpha = (float)Math.Exp(oldMax - newMax);

                        // Rescale existing output accumulator
                        if (oldMax != float.NegativeInfinity)
                        {
                            for (int d = 0; d < headDim; d++)
                            {
                                output[outRowIdx + d] *= alpha;
                            }
                            runningSum[globalRow] *= alpha;
                        }
                        else
                        {
                            runningSum[globalRow] = 0f;
                        }

                        // Compute tile exp and accumulate O_i += P_ij * V_j
                        float tileExpSum = 0f;
                        for (int c = 0; c < curBc; c++)
                        {
                            float expVal = (float)Math.Exp(tileScores[r * bc + c] - newMax);
                            tileExpSum += expVal;

                            int vRowIdx = (jStart + c) * headDim;
                            for (int d = 0; d < headDim; d++)
                            {
                                output[outRowIdx + d] += expVal * vSpan[vRowIdx + d];
                            }
                        }

                        runningMax[globalRow] = newMax;
                        runningSum[globalRow] += tileExpSum;
                    }
                }
            }

            // 3. Final normalization O_i = O_i / l_i
            for (int r = 0; r < seqLenQ; r++)
            {
                float sum = runningSum[r];
                if (sum > 0f)
                {
                    float invSum = 1.0f / sum;
                    int outRowIdx = r * headDim;
                    for (int d = 0; d < headDim; d++)
                    {
                        output[outRowIdx + d] *= invSum;
                    }
                }
            }

            return Tensor.FromArray(output, seqLenQ, headDim);
        }

        /// <summary>
        /// Computes FlashAttention-2 over 4D multi-head tensors [Batch, NumHeads, SeqLen, HeadDim].
        /// </summary>
        public static Tensor<float> ExecuteMultiHead(Tensor<float> q, Tensor<float> k, Tensor<float> v, float? scale = null, int blockSize = DefaultBlockSize)
        {
            if (q == null) throw new ArgumentNullException(nameof(q));
            if (k == null) throw new ArgumentNullException(nameof(k));
            if (v == null) throw new ArgumentNullException(nameof(v));

            if (q.Rank != 4 || k.Rank != 4 || v.Rank != 4)
                throw new ArgumentException("Inputs Q, K, V must be 4D tensors [Batch, NumHeads, SeqLen, HeadDim].");

            int batch = q.Shape[0];
            int numHeads = q.Shape[1];
            int seqLenQ = q.Shape[2];
            int headDim = q.Shape[3];
            int seqLenK = k.Shape[2];

            int totalElements = batch * numHeads * seqLenQ * headDim;
            var result = new float[totalElements];

            int matrixSizeQ = seqLenQ * headDim;
            int matrixSizeK = seqLenK * headDim;

            var qSpan = q.AsSpan();
            var kSpan = k.AsSpan();
            var vSpan = v.AsSpan();

            for (int b = 0; b < batch; b++)
            {
                for (int h = 0; h < numHeads; h++)
                {
                    int headOffsetQ = (b * numHeads + h) * matrixSizeQ;
                    int headOffsetK = (b * numHeads + h) * matrixSizeK;

                    var qSub = Tensor.FromArray(qSpan.Slice(headOffsetQ, matrixSizeQ).ToArray(), seqLenQ, headDim);
                    var kSub = Tensor.FromArray(kSpan.Slice(headOffsetK, matrixSizeK).ToArray(), seqLenK, headDim);
                    var vSub = Tensor.FromArray(vSpan.Slice(headOffsetK, matrixSizeK).ToArray(), seqLenK, headDim);

                    var outSub = Execute(qSub, kSub, vSub, scale, blockSize);
                    var outSpan = outSub.AsSpan();
                    outSpan.CopyTo(result.AsSpan(headOffsetQ, matrixSizeQ));
                }
            }

            return Tensor.FromArray(result, batch, numHeads, seqLenQ, headDim);
        }
    }
}
