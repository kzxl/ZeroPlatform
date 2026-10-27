using System;
using Xunit;
using ZeroCompute.Core;
using ZeroCompute.Core.Context;
using ZeroTensor.Core;

namespace ZeroCompute.Tests
{
    public class ActivationAndReductionTests
    {
        [Fact]
        public void ElementWise_AddAndMultiply_MatchExpected()
        {
            var A = Tensor.FromArray(new float[] { 1, 2, 3, 4 }, 2, 2);
            var B = Tensor.FromArray(new float[] { 10, 20, 30, 40 }, 2, 2);
            var sum = Tensor.Zeros<float>(2, 2);
            var prod = Tensor.Zeros<float>(2, 2);

            ComputeDevice.Cpu.Add(A, B, sum);
            ComputeDevice.Cpu.Multiply(A, B, prod);

            Assert.Equal(11f, sum[0, 0]);
            Assert.Equal(44f, sum[1, 1]);
            Assert.Equal(10f, prod[0, 0]);
            Assert.Equal(160f, prod[1, 1]);
        }

        [Fact]
        public void Activations_ReLU_And_Softmax_WorkCorrectly()
        {
            var input = Tensor.FromArray(new float[] { -2f, -1f, 0f, 1f, 2f }, 1, 5);
            var reluOut = Tensor.Zeros<float>(1, 5);

            ComputeDevice.Cpu.Activation(input, reluOut, ComputeActivationType.ReLU);

            Assert.Equal(0f, reluOut[0, 0]);
            Assert.Equal(0f, reluOut[0, 1]);
            Assert.Equal(0f, reluOut[0, 2]);
            Assert.Equal(1f, reluOut[0, 3]);
            Assert.Equal(2f, reluOut[0, 4]);

            // Softmax
            var softmaxOut = Tensor.Zeros<float>(1, 5);
            ComputeDevice.Cpu.Activation(input, softmaxOut, ComputeActivationType.Softmax);

            float sumProb = 0f;
            for (int c = 0; c < 5; c++)
            {
                Assert.True(softmaxOut[0, c] > 0f);
                sumProb += softmaxOut[0, c];
            }
            Assert.True(Math.Abs(sumProb - 1.0f) < 1e-5f);
        }

        [Fact]
        public void Reductions_SumAndMax_ComputeAccurateDimensions()
        {
            // 2x3 matrix:
            // [1, 2, 3]
            // [4, 5, 6]
            var input = Tensor.FromArray(new float[]
            {
                1, 2, 3,
                4, 5, 6
            }, 2, 3);

            // ReduceSum axis 0 -> [1+4, 2+5, 3+6] = [5, 7, 9] (shape [3])
            var sum0 = ComputeDevice.Cpu.ReduceSum(input, 0);
            Assert.Equal(3, sum0.Length);
            Assert.Equal(5f, sum0[0]);
            Assert.Equal(7f, sum0[1]);
            Assert.Equal(9f, sum0[2]);

            // ReduceMax axis 1 -> [3, 6] (shape [2])
            var max1 = ComputeDevice.Cpu.ReduceMax(input, 1);
            Assert.Equal(2, max1.Length);
            Assert.Equal(3f, max1[0]);
            Assert.Equal(6f, max1[1]);
        }
    }
}
