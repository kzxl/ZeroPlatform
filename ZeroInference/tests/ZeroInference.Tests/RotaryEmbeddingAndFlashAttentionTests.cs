using System;
using Xunit;
using ZeroInference.Core.Layers;
using ZeroTensor.Core;

namespace ZeroInference.Tests
{
    public class RotaryEmbeddingAndFlashAttentionTests
    {
        [Fact]
        public void RotaryEmbedding_PositionZero_PreservesNorm()
        {
            int headDim = 16;
            var rope = new RotaryEmbedding(headDim, maxSeqLen: 128);

            float[] vec = new float[headDim];
            for (int i = 0; i < headDim; i++) vec[i] = (i + 1) * 0.5f;

            float normBefore = 0f;
            for (int i = 0; i < headDim; i++) normBefore += vec[i] * vec[i];

            // At position 0, cos(0) = 1, sin(0) = 0 => vector is unchanged
            rope.Apply(vec.AsSpan(), pos: 0);

            float normAfter = 0f;
            for (int i = 0; i < headDim; i++)
            {
                normAfter += vec[i] * vec[i];
                Assert.Equal((i + 1) * 0.5f, vec[i], precision: 5);
            }

            Assert.Equal(normBefore, normAfter, precision: 5);
        }

        [Fact]
        public void RotaryEmbedding_OrthogonalRotation_PreservesVectorNormAtAnyPosition()
        {
            int headDim = 32;
            var rope = new RotaryEmbedding(headDim, maxSeqLen: 256);

            float[] vec = new float[headDim];
            for (int i = 0; i < headDim; i++) vec[i] = (float)Math.Cos(i * 0.3);

            float normBefore = 0f;
            for (int i = 0; i < headDim; i++) normBefore += vec[i] * vec[i];

            rope.Apply(vec.AsSpan(), pos: 42);

            float normAfter = 0f;
            for (int i = 0; i < headDim; i++) normAfter += vec[i] * vec[i];

            // Rotation is unitary/orthogonal, so 2-norm must be strictly invariant
            Assert.True(Math.Abs(normBefore - normAfter) < 1e-4f, $"Expected {normBefore} == {normAfter}");
        }

        [Fact]
        public void RotaryEmbedding_ApplyTensor_RunsOverMultiDimensionalTensor()
        {
            int batch = 2;
            int seqLen = 8;
            int numHeads = 4;
            int headDim = 16;

            var rope = new RotaryEmbedding(headDim, maxSeqLen: 64);
            var tensor = new Tensor<float>(batch, seqLen, numHeads, headDim);
            var span = tensor.AsSpan();
            for (int i = 0; i < span.Length; i++) span[i] = (float)Math.Sin(i * 0.1);

            // Should succeed without throwing
            rope.Apply(tensor, startPos: 0);

            Assert.Equal(4, tensor.Rank);
            Assert.Equal(batch, tensor.Shape[0]);
            Assert.Equal(seqLen, tensor.Shape[1]);
            Assert.Equal(numHeads, tensor.Shape[2]);
            Assert.Equal(headDim, tensor.Shape[3]);
        }

        [Fact]
        public void FlashAttentionKernel_MatchesExactMathematicalAttention()
        {
            int seqLenQ = 8;
            int seqLenK = 8;
            int headDim = 16;
            float scale = 1.0f / (float)Math.Sqrt(headDim);

            var q = Tensor.RandomUniform(seqLenQ, headDim);
            var k = Tensor.RandomUniform(seqLenK, headDim);
            var v = Tensor.RandomUniform(seqLenK, headDim);

            // 1. Full attention: O = Softmax(Q * K^T * scale) * V
            var qSpan = q.AsSpan();
            var kSpan = k.AsSpan();
            var vSpan = v.AsSpan();

            var exactOutput = new float[seqLenQ * headDim];
            for (int r = 0; r < seqLenQ; r++)
            {
                // compute scores
                float[] scores = new float[seqLenK];
                float maxScore = float.NegativeInfinity;
                for (int c = 0; c < seqLenK; c++)
                {
                    float dot = 0f;
                    for (int d = 0; d < headDim; d++)
                    {
                        dot += qSpan[r * headDim + d] * kSpan[c * headDim + d];
                    }
                    scores[c] = dot * scale;
                    if (scores[c] > maxScore) maxScore = scores[c];
                }

                // softmax
                float expSum = 0f;
                for (int c = 0; c < seqLenK; c++)
                {
                    scores[c] = (float)Math.Exp(scores[c] - maxScore);
                    expSum += scores[c];
                }

                for (int c = 0; c < seqLenK; c++)
                {
                    scores[c] /= expSum;
                }

                // score * V
                for (int d = 0; d < headDim; d++)
                {
                    float sum = 0f;
                    for (int c = 0; c < seqLenK; c++)
                    {
                        sum += scores[c] * vSpan[c * headDim + d];
                    }
                    exactOutput[r * headDim + d] = sum;
                }
            }

            // 2. FlashAttention with small block size (e.g. 4) to stress tiling
            var flashOutput = FlashAttentionKernel.Execute(q, k, v, scale: scale, blockSize: 4);

            var flashSpan = flashOutput.AsSpan();
            for (int i = 0; i < exactOutput.Length; i++)
            {
                Assert.True(Math.Abs(exactOutput[i] - flashSpan[i]) < 1e-4f,
                    $"Mismatch at index {i}: Exact={exactOutput[i]}, Flash={flashSpan[i]}");
            }
        }

        [Fact]
        public void FlashAttentionKernel_ExecuteMultiHead_Succeeds()
        {
            int batch = 2;
            int numHeads = 2;
            int seqLen = 6;
            int headDim = 8;

            var q = new Tensor<float>(batch, numHeads, seqLen, headDim);
            var k = new Tensor<float>(batch, numHeads, seqLen, headDim);
            var v = new Tensor<float>(batch, numHeads, seqLen, headDim);

            var outTensor = FlashAttentionKernel.ExecuteMultiHead(q, k, v, blockSize: 4);

            Assert.Equal(4, outTensor.Rank);
            Assert.Equal(batch, outTensor.Shape[0]);
            Assert.Equal(numHeads, outTensor.Shape[1]);
            Assert.Equal(seqLen, outTensor.Shape[2]);
            Assert.Equal(headDim, outTensor.Shape[3]);
        }
    }
}
