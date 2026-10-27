using System;
using Xunit;
using ZeroInference.Core.Layers;
using ZeroTensor.Core;

namespace ZeroInference.Tests
{
    public class MultiHeadAttentionTests
    {
        [Fact]
        public void TestMultiHeadAttention_Forward2D()
        {
            int seqLen = 4;
            int embedDim = 8;
            int numHeads = 2;

            var mha = new MultiHeadAttention(embedDim, numHeads);

            var inputData = new float[seqLen * embedDim];
            for (int i = 0; i < inputData.Length; i++) inputData[i] = (float)Math.Sin(i * 0.2);
            var inputTensor = Tensor.FromArray(inputData, seqLen, embedDim);

            var output = mha.Forward(inputTensor);

            Assert.Equal(2, output.Rank);
            Assert.Equal(seqLen, output.Shape[0]);
            Assert.Equal(embedDim, output.Shape[1]);

            // All outputs should be valid finite numbers
            for (int i = 0; i < seqLen; i++)
            {
                for (int j = 0; j < embedDim; j++)
                {
                    Assert.False(float.IsNaN(output[i, j]));
                    Assert.False(float.IsInfinity(output[i, j]));
                }
            }
        }

        [Fact]
        public void TestMultiHeadAttention_Forward3D()
        {
            int batch = 2;
            int seqLen = 5;
            int embedDim = 12;
            int numHeads = 3;

            var mha = new MultiHeadAttention(embedDim, numHeads);

            var inputData = new float[batch * seqLen * embedDim];
            for (int i = 0; i < inputData.Length; i++) inputData[i] = (float)Math.Cos(i * 0.1);
            var inputTensor = Tensor.FromArray(inputData, batch, seqLen, embedDim);

            var output = mha.Forward(inputTensor);

            Assert.Equal(3, output.Rank);
            Assert.Equal(batch, output.Shape[0]);
            Assert.Equal(seqLen, output.Shape[1]);
            Assert.Equal(embedDim, output.Shape[2]);
        }

        [Fact]
        public void TestMultiHeadAttention_AttentionMapProbabilitySumsToOne()
        {
            int seqLen = 6;
            int embedDim = 8;
            int numHeads = 2;

            var mha = new MultiHeadAttention(embedDim, numHeads);

            var inputData = new float[seqLen * embedDim];
            for (int i = 0; i < inputData.Length; i++) inputData[i] = i * 0.15f;
            var inputTensor = Tensor.FromArray(inputData, seqLen, embedDim);

            var attnMap = mha.ComputeAttentionMap(inputTensor);

            Assert.Equal(2, attnMap.Rank);
            Assert.Equal(seqLen, attnMap.Shape[0]);
            Assert.Equal(seqLen, attnMap.Shape[1]);

            // Each row must sum to 1.0 (softmax property)
            for (int i = 0; i < seqLen; i++)
            {
                float rowSum = 0.0f;
                for (int j = 0; j < seqLen; j++)
                {
                    rowSum += attnMap[i, j];
                }
                Assert.True(Math.Abs(rowSum - 1.0f) < 1e-4f, $"Row {i} sum {rowSum} should be ~1.0");
            }
        }
    }
}
