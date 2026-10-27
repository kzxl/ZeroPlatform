using System;
using ZeroTensor.Core;

namespace ZeroInference.Core.Layers
{
    /// <summary>
    /// Multi-Head Attention layer for sequence modeling, transformer architectures, and predictive anomaly detection.
    /// Pure C# with zero external dependencies.
    /// </summary>
    public sealed class MultiHeadAttention
    {
        public int EmbedDim { get; }
        public int NumHeads { get; }
        public int HeadDim { get; }
        public float Scale { get; }

        public Tensor<float> Wq { get; set; }
        public Tensor<float> Wk { get; set; }
        public Tensor<float> Wv { get; set; }
        public Tensor<float> Wo { get; set; }

        public Tensor<float>? Bq { get; set; }
        public Tensor<float>? Bk { get; set; }
        public Tensor<float>? Bv { get; set; }
        public Tensor<float>? Bo { get; set; }

        public MultiHeadAttention(int embedDim, int numHeads)
        {
            if (embedDim <= 0) throw new ArgumentOutOfRangeException(nameof(embedDim));
            if (numHeads <= 0 || embedDim % numHeads != 0)
                throw new ArgumentException($"embedDim ({embedDim}) must be divisible by numHeads ({numHeads}).", nameof(numHeads));

            EmbedDim = embedDim;
            NumHeads = numHeads;
            HeadDim = embedDim / numHeads;
            Scale = 1.0f / (float)Math.Sqrt(HeadDim);

            // Initialize orthogonal/identity-like weights
            Wq = InitializeProjectionWeight(embedDim);
            Wk = InitializeProjectionWeight(embedDim);
            Wv = InitializeProjectionWeight(embedDim);
            Wo = InitializeProjectionWeight(embedDim);
        }

        private static Tensor<float> InitializeProjectionWeight(int dim)
        {
            var data = new float[dim * dim];
            float initVal = 1.0f / (float)Math.Sqrt(dim);
            for (int i = 0; i < dim; i++)
            {
                data[i * dim + i] = 1.0f; // Initial identity bias with scaling
            }
            return Tensor.FromArray(data, dim, dim);
        }

        /// <summary>
        /// Executes the Multi-Head Attention forward pass over input sequence [SeqLen, EmbedDim] or [Batch, SeqLen, EmbedDim].
        /// </summary>
        public Tensor<float> Forward(Tensor<float> query, Tensor<float>? key = null, Tensor<float>? value = null)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            key = key ?? query;
            value = value ?? query;

            bool is2D = query.Rank == 2;
            int batch = is2D ? 1 : query.Shape[0];
            int seqLenQ = is2D ? query.Shape[0] : query.Shape[1];
            int seqLenK = is2D ? key.Shape[0] : key.Shape[1];
            int inDim = is2D ? query.Shape[1] : query.Shape[2];

            if (inDim != EmbedDim)
                throw new ArgumentException($"Input embedding dim {inDim} does not match layer EmbedDim {EmbedDim}.");

            var qFlat = is2D ? query : query.Reshape(batch * seqLenQ, EmbedDim);
            var kFlat = is2D ? key : key.Reshape(batch * seqLenK, EmbedDim);
            var vFlat = is2D ? value : value.Reshape(batch * seqLenK, EmbedDim);

            // 1. Linear Projections Q, K, V: [N, D] x [D, D]
            var projQ = Project(qFlat, Wq, Bq, batch * seqLenQ);
            var projK = Project(kFlat, Wk, Bk, batch * seqLenK);
            var projV = Project(vFlat, Wv, Bv, batch * seqLenK);

            // 2. Multi-Head Scaled Dot-Product Attention
            var concatOutput = new float[batch * seqLenQ * EmbedDim];

            for (int b = 0; b < batch; b++)
            {
                int bOffsetQ = b * seqLenQ;
                int bOffsetK = b * seqLenK;

                for (int h = 0; h < NumHeads; h++)
                {
                    int hStart = h * HeadDim;

                    // Attention scores A = Q_h * K_h^T * Scale -> [seqLenQ, seqLenK]
                    var scores = new float[seqLenQ, seqLenK];
                    for (int i = 0; i < seqLenQ; i++)
                    {
                        int qIdx = (bOffsetQ + i) * EmbedDim + hStart;
                        float maxScore = float.NegativeInfinity;

                        for (int j = 0; j < seqLenK; j++)
                        {
                            int kIdx = (bOffsetK + j) * EmbedDim + hStart;
                            float dot = 0.0f;
                            for (int d = 0; d < HeadDim; d++)
                            {
                                dot += projQ[qIdx + d] * projK[kIdx + d];
                            }
                            dot *= Scale;
                            scores[i, j] = dot;
                            if (dot > maxScore) maxScore = dot;
                        }

                        // Softmax along row i
                        float sumExp = 0.0f;
                        for (int j = 0; j < seqLenK; j++)
                        {
                            float exp = (float)Math.Exp(scores[i, j] - maxScore);
                            scores[i, j] = exp;
                            sumExp += exp;
                        }
                        float invSum = sumExp > 1e-12f ? 1.0f / sumExp : 0.0f;
                        for (int j = 0; j < seqLenK; j++)
                        {
                            scores[i, j] *= invSum;
                        }
                    }

                    // Context = Scores * V_h -> [seqLenQ, HeadDim]
                    for (int i = 0; i < seqLenQ; i++)
                    {
                        int outIdx = (bOffsetQ + i) * EmbedDim + hStart;
                        for (int d = 0; d < HeadDim; d++)
                        {
                            float contextVal = 0.0f;
                            for (int j = 0; j < seqLenK; j++)
                            {
                                int vIdx = (bOffsetK + j) * EmbedDim + hStart;
                                contextVal += scores[i, j] * projV[vIdx + d];
                            }
                            concatOutput[outIdx + d] = contextVal;
                        }
                    }
                }
            }

            // 3. Final Output Projection: Concat x Wo + Bo
            var concatTensor = Tensor.FromArray(concatOutput, batch * seqLenQ, EmbedDim);
            var finalProj = Project(concatTensor, Wo, Bo, batch * seqLenQ);

            if (is2D)
            {
                return Tensor.FromArray(finalProj, seqLenQ, EmbedDim);
            }
            else
            {
                return Tensor.FromArray(finalProj, batch, seqLenQ, EmbedDim);
            }
        }

        /// <summary>
        /// Computes 2D attention probability matrix [SeqLenQ, SeqLenK] averaged across all heads.
        /// Useful for inspecting feature attribution and sensor anomaly localization.
        /// </summary>
        public Tensor<float> ComputeAttentionMap(Tensor<float> query, Tensor<float>? key = null)
        {
            key = key ?? query;
            int seqLenQ = query.Rank == 2 ? query.Shape[0] : query.Shape[1];
            int seqLenK = key.Rank == 2 ? key.Shape[0] : key.Shape[1];

            var qFlat = query.Rank == 2 ? query : query.Reshape(query.Shape[0] * seqLenQ, EmbedDim);
            var kFlat = key.Rank == 2 ? key : key.Reshape(key.Shape[0] * seqLenK, EmbedDim);

            var projQ = Project(qFlat, Wq, Bq, seqLenQ);
            var projK = Project(kFlat, Wk, Bk, seqLenK);

            var avgScores = new float[seqLenQ * seqLenK];
            float invHeads = 1.0f / NumHeads;

            for (int h = 0; h < NumHeads; h++)
            {
                int hStart = h * HeadDim;

                for (int i = 0; i < seqLenQ; i++)
                {
                    int qIdx = i * EmbedDim + hStart;
                    float maxScore = float.NegativeInfinity;
                    float[] rowScores = new float[seqLenK];

                    for (int j = 0; j < seqLenK; j++)
                    {
                        int kIdx = j * EmbedDim + hStart;
                        float dot = 0.0f;
                        for (int d = 0; d < HeadDim; d++)
                            dot += projQ[qIdx + d] * projK[kIdx + d];

                        dot *= Scale;
                        rowScores[j] = dot;
                        if (dot > maxScore) maxScore = dot;
                    }

                    float sumExp = 0.0f;
                    for (int j = 0; j < seqLenK; j++)
                    {
                        rowScores[j] = (float)Math.Exp(rowScores[j] - maxScore);
                        sumExp += rowScores[j];
                    }
                    float invSum = sumExp > 1e-12f ? 1.0f / sumExp : 0.0f;
                    for (int j = 0; j < seqLenK; j++)
                    {
                        avgScores[i * seqLenK + j] += (rowScores[j] * invSum) * invHeads;
                    }
                }
            }

            return Tensor.FromArray(avgScores, seqLenQ, seqLenK);
        }

        private float[] Project(Tensor<float> x, Tensor<float> w, Tensor<float>? b, int rows)
        {
            var result = new float[rows * EmbedDim];
            for (int r = 0; r < rows; r++)
            {
                int rOffset = r * EmbedDim;
                for (int c = 0; c < EmbedDim; c++)
                {
                    float sum = b != null ? b[c] : 0.0f;
                    int wOffset = c * EmbedDim;
                    for (int k = 0; k < EmbedDim; k++)
                    {
                        sum += x[r, k] * w[k, c];
                    }
                    result[rOffset + c] = sum;
                }
            }
            return result;
        }
    }
}
