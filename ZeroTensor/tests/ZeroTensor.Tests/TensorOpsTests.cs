using System;
using Xunit;
using ZeroTensor.Core;

namespace ZeroTensor.Tests
{
    public class TensorOpsTests
    {
        [Fact]
        public void ElementWise_Arithmetic_Contiguous_MatchesExpected()
        {
            var a = Tensor.FromArray(new float[] { 1, 2, 3, 4 }, 2, 2);
            var b = Tensor.FromArray(new float[] { 10, 20, 30, 40 }, 2, 2);

            var sum = a + b;
            Assert.Equal(new float[] { 11, 22, 33, 44 }, sum.ToArray());

            var diff = b - a;
            Assert.Equal(new float[] { 9, 18, 27, 36 }, diff.ToArray());

            var prod = a * b;
            Assert.Equal(new float[] { 10, 40, 90, 160 }, prod.ToArray());

            var div = b / a;
            Assert.Equal(new float[] { 10, 10, 10, 10 }, div.ToArray());
        }

        [Fact]
        public void ElementWise_Arithmetic_WithBroadcasting_MatchesNumPy()
        {
            // A is (2, 3), B is (3,) -> broadcasts along rows
            var a = Tensor.FromArray(new float[]
            {
                1, 2, 3,
                4, 5, 6
            }, 2, 3);

            var b = Tensor.FromArray(new float[] { 10, 20, 30 }, 3);

            var sum = a + b;
            Assert.Equal(new TensorShape(2, 3), sum.Shape);
            Assert.Equal(new float[] { 11, 22, 33, 14, 25, 36 }, sum.ToArray());

            // A is (2, 1), B is (1, 3) -> broadcasts to (2, 3)
            var colVec = Tensor.FromArray(new float[] { 1, 2 }, 2, 1);
            var rowVec = Tensor.FromArray(new float[] { 10, 20, 30 }, 1, 3);

            var broadSum = colVec + rowVec;
            Assert.Equal(new TensorShape(2, 3), broadSum.Shape);
            Assert.Equal(new float[]
            {
                11, 21, 31,
                12, 22, 32
            }, broadSum.ToArray());
        }

        [Fact]
        public void Scalar_Arithmetic_WorksCorrectly()
        {
            var a = Tensor.FromArray(new float[] { 2, 4, 6 }, 3);

            var add = a + 10f;
            Assert.Equal(new float[] { 12, 14, 16 }, add.ToArray());

            var mul = a * 3f;
            Assert.Equal(new float[] { 6, 12, 18 }, mul.ToArray());

            var div = a / 2f;
            Assert.Equal(new float[] { 1, 2, 3 }, div.ToArray());
        }

        [Fact]
        public void Vectorized_Activations_AreAccurate()
        {
            var input = Tensor.FromArray(new float[] { -2f, -1f, 0f, 1f, 2f }, 5);

            // ReLU
            var relu = TensorOps.ReLU(input);
            Assert.Equal(new float[] { 0f, 0f, 0f, 1f, 2f }, relu.ToArray());

            // Sigmoid: 1 / (1 + e^-x) -> 0.5 at x=0
            var sig = TensorOps.Sigmoid(input);
            Assert.True(Math.Abs(sig[2] - 0.5f) < 1e-5f);
            Assert.True(sig[0] > 0f && sig[0] < 0.5f);
            Assert.True(sig[4] > 0.5f && sig[4] < 1.0f);

            // GELU: 0 at x=0
            var gelu = TensorOps.GELU(input);
            Assert.True(Math.Abs(gelu[2]) < 1e-5f);
            Assert.True(gelu[0] < 0f && gelu[0] > -0.1f);
        }

        [Fact]
        public void Axis_Reductions_WorkCorrectly()
        {
            var m = Tensor.FromArray(new float[]
            {
                1, 2, 3,
                4, 5, 6
            }, 2, 3);

            // Overall Sum & Mean
            Assert.Equal(21f, TensorOps.Sum(m)[0]);
            Assert.Equal(3.5f, TensorOps.Mean(m)[0]);

            // Sum along axis 0 (down columns) -> [5, 7, 9]
            var sumAxis0 = TensorOps.Sum(m, axis: 0);
            Assert.Equal(new TensorShape(3), sumAxis0.Shape);
            Assert.Equal(new float[] { 5, 7, 9 }, sumAxis0.ToArray());

            // Sum along axis 1 (across rows) -> [6, 15]
            var sumAxis1 = TensorOps.Sum(m, axis: 1);
            Assert.Equal(new TensorShape(2), sumAxis1.Shape);
            Assert.Equal(new float[] { 6, 15 }, sumAxis1.ToArray());

            // ArgMax along axis 1 -> [2, 2] (both rows have max at col 2)
            var argMax1 = TensorOps.ArgMax(m, axis: 1);
            Assert.Equal(new int[] { 2, 2 }, argMax1.ToArray());
        }

        [Fact]
        public void Softmax_SumsToOneAcrossAxis()
        {
            var logits = Tensor.FromArray(new float[]
            {
                2.0f, 1.0f, 0.1f,
                1.0f, 3.0f, 0.5f
            }, 2, 3);

            var probs = TensorOps.Softmax(logits, axis: 1);
            Assert.Equal(new TensorShape(2, 3), probs.Shape);

            // Each row must sum to 1.0
            for (int r = 0; r < 2; r++)
            {
                float sum = probs[r, 0] + probs[r, 1] + probs[r, 2];
                Assert.True(Math.Abs(sum - 1.0f) < 1e-5f, $"Row {r} sum is {sum}");
            }
        }

        [Fact]
        public void NativeMemory_Roundtrip_WorksCorrectly()
        {
            var orig = Tensor.FromArray(new float[] { 10.5f, 20.5f, 30.5f, 40.5f, 50.5f, 60.5f }, 2, 3);
            using var nativeBlock = orig.ToNativeBlock();

            Assert.NotNull(nativeBlock);
            Assert.True(nativeBlock.Length >= 6 * sizeof(float));

            var restored = Tensor.FromNativeBlock<float>(nativeBlock, 2, 3);
            Assert.Equal(orig.Shape, restored.Shape);
            Assert.Equal(orig.ToArray(), restored.ToArray());
        }

        [Fact]
        public void AsSpan_ContiguousTensor_AllowsDirectSpanMutation()
        {
            var tensor = Tensor.Zeros<float>(3, 3);
            var span = tensor.AsSpan();
            span[0] = 99.0f;
            span[4] = 42.0f;

            Assert.Equal(99.0f, tensor[0, 0]);
            Assert.Equal(42.0f, tensor[1, 1]);
        }
    }
}
