using System;
using Xunit;
using ZeroTensor.Core;

namespace ZeroTensor.Tests
{
    public class TensorPoolAndInPlaceTests
    {
        #region TensorPool Tests

        [Fact]
        public void TensorPool_RentAndDispose_ReusesBufferWithoutErrors()
        {
            using (var rented = TensorPool.Rent<float>(3, 4))
            {
                Assert.Equal(2, rented.Rank);
                Assert.Equal(12, rented.Length);
                Assert.True(rented.IsContiguous);

                rented[1, 2] = 42f;
                Assert.Equal(42f, rented[1, 2]);

                Tensor<float> tensor = rented; // implicit conversion
                Assert.Equal(42f, tensor[1, 2]);
            }

            // Rent again using Tensor.Rent shortcut
            using (var rented2 = Tensor.Rent<double>(2, 2))
            {
                Assert.Equal(2, rented2.Rank);
                Assert.Equal(4, rented2.Length);
                rented2[0, 0] = 3.14159;
                Assert.Equal(3.14159, rented2[0, 0]);
            }
        }

        [Fact]
        public void TensorPool_MultiDimensionalIndexers_WorkCorrectly()
        {
            using var r1D = Tensor.Rent<float>(5);
            r1D[3] = 99f;
            Assert.Equal(99f, r1D[3]);

            using var r3D = Tensor.Rent<int>(2, 3, 4);
            r3D[1, 2, 3] = 777;
            Assert.Equal(777, r3D[1, 2, 3]);

            using var r4D = Tensor.Rent<byte>(2, 2, 2, 2);
            r4D[1, 1, 1, 1] = 255;
            Assert.Equal(255, r4D[1, 1, 1, 1]);
        }

        #endregion

        #region Float In-Place Tests

        [Fact]
        public void Float_InPlace_Arithmetic_OperatesCorrectly()
        {
            var a = Tensor.FromArray(new float[] { 10f, 20f, 30f, 40f }, 2, 2);
            var b = Tensor.FromArray(new float[] { 1f, 2f, 3f, 4f }, 2, 2);

            // a += b -> [11, 22, 33, 44]
            a.Add_(b);
            Assert.Equal(11f, a[0, 0]);
            Assert.Equal(22f, a[0, 1]);
            Assert.Equal(33f, a[1, 0]);
            Assert.Equal(44f, a[1, 1]);

            // a -= b -> [10, 20, 30, 40]
            a.Subtract_(b);
            Assert.Equal(10f, a[0, 0]);
            Assert.Equal(20f, a[0, 1]);

            // a *= b -> [10, 40, 90, 160]
            a.Multiply_(b);
            Assert.Equal(10f, a[0, 0]);
            Assert.Equal(40f, a[0, 1]);
            Assert.Equal(90f, a[1, 0]);
            Assert.Equal(160f, a[1, 1]);

            // a /= b -> [10, 20, 30, 40]
            a.Divide_(b);
            Assert.Equal(10f, a[0, 0]);
            Assert.Equal(20f, a[0, 1]);
            Assert.Equal(30f, a[1, 0]);
            Assert.Equal(40f, a[1, 1]);
        }

        [Fact]
        public void Float_InPlace_Scalar_Relu_Clamp()
        {
            var a = Tensor.FromArray(new float[] { -5f, 0f, 5f, 15f }, 4);

            // a += 2 -> [-3, 2, 7, 17]
            a.AddScalar_(2f);
            Assert.Equal(-3f, a[0]);
            Assert.Equal(2f, a[1]);
            Assert.Equal(7f, a[2]);
            Assert.Equal(17f, a[3]);

            // a *= 2 -> [-6, 4, 14, 34]
            a.MultiplyScalar_(2f);
            Assert.Equal(-6f, a[0]);
            Assert.Equal(4f, a[1]);

            // Relu_ -> [0, 4, 14, 34]
            a.Relu_();
            Assert.Equal(0f, a[0]);
            Assert.Equal(4f, a[1]);

            // Clamp_(2, 20) -> [2, 4, 14, 20]
            a.Clamp_(2f, 20f);
            Assert.Equal(2f, a[0]);
            Assert.Equal(4f, a[1]);
            Assert.Equal(14f, a[2]);
            Assert.Equal(20f, a[3]);
        }

        [Fact]
        public void Float_InPlace_Broadcasting_OperatesCorrectly()
        {
            var a = Tensor.FromArray(new float[]
            {
                1f, 2f,
                3f, 4f
            }, 2, 2);
            var b = Tensor.FromArray(new float[] { 10f, 20f }, 1, 2);

            a.Add_(b);
            Assert.Equal(11f, a[0, 0]);
            Assert.Equal(22f, a[0, 1]);
            Assert.Equal(13f, a[1, 0]);
            Assert.Equal(24f, a[1, 1]);
        }

        [Fact]
        public void Float_DestinationBuffer_Operations()
        {
            var a = Tensor.FromArray(new float[] { 1f, 2f, 3f, 4f }, 2, 2);
            var b = Tensor.FromArray(new float[] { 5f, 6f, 7f, 8f }, 2, 2);
            var dst = new Tensor<float>(2, 2);

            TensorOps.Add(a, b, dst);
            Assert.Equal(6f, dst[0, 0]);
            Assert.Equal(8f, dst[0, 1]);
            Assert.Equal(10f, dst[1, 0]);
            Assert.Equal(12f, dst[1, 1]);

            TensorOps.Subtract(dst, a, dst);
            Assert.Equal(5f, dst[0, 0]);
            Assert.Equal(6f, dst[0, 1]);
            Assert.Equal(7f, dst[1, 0]);
            Assert.Equal(8f, dst[1, 1]);

            TensorOps.Multiply(a, b, dst);
            Assert.Equal(5f, dst[0, 0]);
            Assert.Equal(12f, dst[0, 1]);
            Assert.Equal(21f, dst[1, 0]);
            Assert.Equal(32f, dst[1, 1]);
        }

        #endregion

        #region Double In-Place Tests

        [Fact]
        public void Double_InPlace_Arithmetic_OperatesCorrectly()
        {
            var a = Tensor.FromArray(new double[] { 10.0, 20.0, 30.0, 40.0 }, 2, 2);
            var b = Tensor.FromArray(new double[] { 1.0, 2.0, 3.0, 4.0 }, 2, 2);

            a.Add_(b);
            Assert.Equal(11.0, a[0, 0]);
            Assert.Equal(22.0, a[0, 1]);

            a.Subtract_(b);
            Assert.Equal(10.0, a[0, 0]);
            Assert.Equal(20.0, a[0, 1]);

            a.Multiply_(b);
            Assert.Equal(10.0, a[0, 0]);
            Assert.Equal(40.0, a[0, 1]);

            a.Divide_(b);
            Assert.Equal(10.0, a[0, 0]);
            Assert.Equal(20.0, a[0, 1]);

            a.AddScalar_(5.0);
            Assert.Equal(15.0, a[0, 0]);

            a.MultiplyScalar_(2.0);
            Assert.Equal(30.0, a[0, 0]);

            var neg = Tensor.FromArray(new double[] { -10.0, 5.0 }, 2);
            neg.Relu_();
            Assert.Equal(0.0, neg[0]);
            Assert.Equal(5.0, neg[1]);

            neg.Clamp_(2.0, 4.0);
            Assert.Equal(2.0, neg[0]);
            Assert.Equal(4.0, neg[1]);
        }

        [Fact]
        public void Double_InPlace_Broadcasting_OperatesCorrectly()
        {
            var a = Tensor.FromArray(new double[]
            {
                1.0, 2.0,
                3.0, 4.0
            }, 2, 2);
            var b = Tensor.FromArray(new double[] { 10.0, 20.0 }, 1, 2);

            a.Add_(b);
            Assert.Equal(11.0, a[0, 0]);
            Assert.Equal(22.0, a[0, 1]);
            Assert.Equal(13.0, a[1, 0]);
            Assert.Equal(24.0, a[1, 1]);
        }

        [Fact]
        public void Double_DestinationBuffer_Operations()
        {
            var a = Tensor.FromArray(new double[] { 1.0, 2.0, 3.0, 4.0 }, 2, 2);
            var b = Tensor.FromArray(new double[] { 10.0, 20.0, 30.0, 40.0 }, 2, 2);
            var dst = new Tensor<double>(2, 2);

            TensorOps.Add(a, b, dst);
            Assert.Equal(11.0, dst[0, 0]);
            Assert.Equal(22.0, dst[0, 1]);

            TensorOps.Subtract(b, a, dst);
            Assert.Equal(9.0, dst[0, 0]);
            Assert.Equal(18.0, dst[0, 1]);

            TensorOps.Multiply(a, b, dst);
            Assert.Equal(10.0, dst[0, 0]);
            Assert.Equal(40.0, dst[0, 1]);
        }

        #endregion

        #region Quantized GEMM Tests

        [Fact]
        public void Quantized_GemmInt8_AccumulatesAccurately()
        {
            // A (2x3)
            var a = Tensor.FromArray(new sbyte[]
            {
                1, 2, 3,
                4, 5, 6
            }, 2, 3);

            // B (3x2)
            var b = Tensor.FromArray(new sbyte[]
            {
                7, 8,
                9, 1,
                2, 3
            }, 3, 2);

            // Expected C = A @ B:
            // [1*7 + 2*9 + 3*2, 1*8 + 2*1 + 3*3] = [7 + 18 + 6,  8 + 2 + 9]  = [31, 19]
            // [4*7 + 5*9 + 6*2, 4*8 + 5*1 + 6*3] = [28 + 45 + 12, 32 + 5 + 18] = [85, 55]
            var c = TensorBlas.MatMul(a, b);

            Assert.Equal(31, c[0, 0]);
            Assert.Equal(19, c[0, 1]);
            Assert.Equal(85, c[1, 0]);
            Assert.Equal(55, c[1, 1]);
        }

        [Fact]
        public void Quantized_GemmInt8_WithScaleAndZeroPoint_ComputesCorrectFP32()
        {
            var a = Tensor.FromArray(new sbyte[] { 10, 20 }, 1, 2);
            var b = Tensor.FromArray(new sbyte[] { 30, 40 }, 2, 1);
            var c = new Tensor<float>(1, 1);

            // zeroPointA = 5 -> a shifted is [5, 15]
            // zeroPointB = 10 -> b shifted is [20, 30]
            // int accum = 5*20 + 15*30 = 100 + 450 = 550
            // scaleA = 0.1f, scaleB = 0.2f -> combined = 0.02f
            // float output = 550 * 0.02 = 11.0f
            TensorBlas.GemmInt8(a, b, c, scaleA: 0.1f, scaleB: 0.2f, zeroPointA: 5, zeroPointB: 10);

            Assert.True(Math.Abs(11.0f - c[0, 0]) < 1e-4f);
        }

        #endregion

        #region Register Tiled GEMM Dimension Parity Tests

        [Fact]
        public void Gemm_Double_OddAndEvenRows_MatchAnalyticalValues()
        {
            // Test 3x3 @ 3x2 (exercises 2-row register tile + 1 odd row tail)
            var a = Tensor.FromArray(new double[]
            {
                1.0, 2.0, 3.0,
                4.0, 5.0, 6.0,
                7.0, 8.0, 9.0
            }, 3, 3);

            var b = Tensor.FromArray(new double[]
            {
                1.0, 0.0,
                0.0, 1.0,
                1.0, 1.0
            }, 3, 2);

            var c = TensorBlas.MatMul(a, b);

            // Row 0: [1*1+3*1, 2*1+3*1] = [4, 5]
            Assert.Equal(4.0, c[0, 0]);
            Assert.Equal(5.0, c[0, 1]);

            // Row 1: [4*1+6*1, 5*1+6*1] = [10, 11]
            Assert.Equal(10.0, c[1, 0]);
            Assert.Equal(11.0, c[1, 1]);

            // Row 2 (odd tail): [7*1+9*1, 8*1+9*1] = [16, 17]
            Assert.Equal(16.0, c[2, 0]);
            Assert.Equal(17.0, c[2, 1]);
        }

        #endregion
    }
}
