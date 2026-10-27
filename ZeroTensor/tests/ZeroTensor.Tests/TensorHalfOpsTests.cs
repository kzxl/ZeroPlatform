using System;
using Xunit;
using ZeroTensor.Core;

namespace ZeroTensor.Tests
{
    public class TensorHalfOpsTests
    {
        [Fact]
        public void HalfTensor_BasicCreationAndIndexing()
        {
            var shape = new TensorShape(2, 3);
            var tensor = new Tensor<Half>(shape);

            tensor[0, 0] = (Half)1.0f;
            tensor[0, 1] = (Half)2.5f;
            tensor[1, 2] = (Half)(-4.25f);

            Assert.Equal((Half)1.0f, tensor[0, 0]);
            Assert.Equal((Half)2.5f, tensor[0, 1]);
            Assert.Equal((Half)(-4.25f), tensor[1, 2]);
            Assert.Equal(6, tensor.Length);
            Assert.True(tensor.IsContiguous);
        }

        [Fact]
        public void HalfTensor_ConversionsToAndFromFloat()
        {
            var floatTensor = new Tensor<float>(2, 4);
            for (int i = 0; i < 8; i++)
            {
                floatTensor[i / 4, i % 4] = (i + 1) * 0.5f;
            }

            Tensor<Half> halfTensor = floatTensor.ToHalf();
            Assert.Equal(floatTensor.Shape, halfTensor.Shape);

            for (int i = 0; i < 8; i++)
            {
                Assert.Equal((Half)((i + 1) * 0.5f), halfTensor[i / 4, i % 4]);
            }

            Tensor<float> roundTripFloat = halfTensor.ToFloat();
            for (int i = 0; i < 8; i++)
            {
                Assert.Equal(floatTensor[i / 4, i % 4], roundTripFloat[i / 4, i % 4], precision: 3);
            }
        }

        [Fact]
        public void HalfTensor_BinaryArithmetic()
        {
            var a = new Tensor<Half>(2, 2);
            var b = new Tensor<Half>(2, 2);

            a[0, 0] = (Half)2.0f; a[0, 1] = (Half)4.0f;
            a[1, 0] = (Half)6.0f; a[1, 1] = (Half)8.0f;

            b[0, 0] = (Half)1.0f; b[0, 1] = (Half)2.0f;
            b[1, 0] = (Half)3.0f; b[1, 1] = (Half)4.0f;

            var sum = TensorOps.Add(a, b);
            Assert.Equal((Half)3.0f, sum[0, 0]);
            Assert.Equal((Half)6.0f, sum[0, 1]);
            Assert.Equal((Half)9.0f, sum[1, 0]);
            Assert.Equal((Half)12.0f, sum[1, 1]);

            var diff = TensorOps.Subtract(a, b);
            Assert.Equal((Half)1.0f, diff[0, 0]);
            Assert.Equal((Half)2.0f, diff[0, 1]);
            Assert.Equal((Half)3.0f, diff[1, 0]);
            Assert.Equal((Half)4.0f, diff[1, 1]);

            var prod = TensorOps.Multiply(a, b);
            Assert.Equal((Half)2.0f, prod[0, 0]);
            Assert.Equal((Half)8.0f, prod[0, 1]);
            Assert.Equal((Half)18.0f, prod[1, 0]);
            Assert.Equal((Half)32.0f, prod[1, 1]);

            var div = TensorOps.Divide(a, b);
            Assert.Equal((Half)2.0f, div[0, 0]);
            Assert.Equal((Half)2.0f, div[0, 1]);
            Assert.Equal((Half)2.0f, div[1, 0]);
            Assert.Equal((Half)2.0f, div[1, 1]);
        }

        [Fact]
        public void HalfTensor_ScalarOpsAndNegate()
        {
            var a = new Tensor<Half>(2, 2);
            a[0, 0] = (Half)10.0f; a[0, 1] = (Half)20.0f;
            a[1, 0] = (Half)30.0f; a[1, 1] = (Half)40.0f;

            var scaled = TensorOps.MultiplyScalar(a, (Half)0.5f);
            Assert.Equal((Half)5.0f, scaled[0, 0]);
            Assert.Equal((Half)10.0f, scaled[0, 1]);
            Assert.Equal((Half)15.0f, scaled[1, 0]);
            Assert.Equal((Half)20.0f, scaled[1, 1]);

            var neg = TensorOps.Negate(scaled);
            Assert.Equal((Half)(-5.0f), neg[0, 0]);
            Assert.Equal((Half)(-10.0f), neg[0, 1]);
            Assert.Equal((Half)(-15.0f), neg[1, 0]);
            Assert.Equal((Half)(-20.0f), neg[1, 1]);
        }

        [Fact]
        public void HalfTensor_MatMul()
        {
            // A: (2 x 3)
            var a = new Tensor<Half>(2, 3);
            a[0, 0] = (Half)1f; a[0, 1] = (Half)2f; a[0, 2] = (Half)3f;
            a[1, 0] = (Half)4f; a[1, 1] = (Half)5f; a[1, 2] = (Half)6f;

            // B: (3 x 2)
            var b = new Tensor<Half>(3, 2);
            b[0, 0] = (Half)7f; b[0, 1] = (Half)8f;
            b[1, 0] = (Half)9f; b[1, 1] = (Half)1f;
            b[2, 0] = (Half)2f; b[2, 1] = (Half)3f;

            // C = A @ B (2 x 2)
            // C[0, 0] = 1*7 + 2*9 + 3*2 = 7 + 18 + 6 = 31
            // C[0, 1] = 1*8 + 2*1 + 3*3 = 8 + 2 + 9 = 19
            // C[1, 0] = 4*7 + 5*9 + 6*2 = 28 + 45 + 12 = 85
            // C[1, 1] = 4*8 + 5*1 + 6*3 = 32 + 5 + 18 = 55
            var c = TensorBlas.MatMul(a, b);

            Assert.Equal(2, c.Shape[0]);
            Assert.Equal(2, c.Shape[1]);
            Assert.Equal((Half)31f, c[0, 0]);
            Assert.Equal((Half)19f, c[0, 1]);
            Assert.Equal((Half)85f, c[1, 0]);
            Assert.Equal((Half)55f, c[1, 1]);
        }

        [Fact]
        public void HalfTensor_ActivationsAndReductions()
        {
            var a = new Tensor<Half>(4);
            a[0] = (Half)(-2.0f);
            a[1] = (Half)0.0f;
            a[2] = (Half)2.0f;
            a[3] = (Half)4.0f;

            var relu = TensorOps.ReLUHalf(a);
            Assert.Equal((Half)0.0f, relu[0]);
            Assert.Equal((Half)0.0f, relu[1]);
            Assert.Equal((Half)2.0f, relu[2]);
            Assert.Equal((Half)4.0f, relu[3]);

            var sum = TensorOps.SumHalf(a);
            Assert.Equal((Half)4.0f, sum);

            var mean = TensorOps.MeanHalf(a);
            Assert.Equal((Half)1.0f, mean);

            var max = TensorOps.MaxHalf(a);
            Assert.Equal((Half)4.0f, max);

            var min = TensorOps.MinHalf(a);
            Assert.Equal((Half)(-2.0f), min);
        }
    }
}
