using System;
using Xunit;
using ZeroTensor.Core;

namespace ZeroTensor.Tests
{
    public class TensorDoubleOpsTests
    {
        [Fact]
        public void Double_BinaryArithmetic_And_Broadcasting()
        {
            var a = Tensor.Create<double>(new[] { 2, 2 }, new double[]
            {
                10.0, 20.0,
                30.0, 40.0
            });

            var b = Tensor.Create<double>(new[] { 2, 2 }, new double[]
            {
                1.0, 2.0,
                3.0, 4.0
            });

            // Add
            var add = a + b;
            Assert.Equal(11.0, add[0, 0]);
            Assert.Equal(44.0, add[1, 1]);

            // Subtract
            var sub = a - b;
            Assert.Equal(9.0, sub[0, 0]);
            Assert.Equal(36.0, sub[1, 1]);

            // Multiply
            var mul = a * b;
            Assert.Equal(10.0, mul[0, 0]);
            Assert.Equal(160.0, mul[1, 1]);

            // Divide
            var div = a / b;
            Assert.Equal(10.0, div[0, 0]);
            Assert.Equal(10.0, div[1, 1]);

            // Scalar ops
            var scaled = a * 2.0;
            Assert.Equal(20.0, scaled[0, 0]);
            Assert.Equal(80.0, scaled[1, 1]);
        }

        [Fact]
        public void Double_ElementWise_Math_And_Reductions()
        {
            var t = Tensor.FromArray(new double[] { 1.0, 4.0, 9.0, 16.0 });

            // Sqrt
            var sqrt = TensorOps.Sqrt(t);
            Assert.Equal(new double[] { 1.0, 2.0, 3.0, 4.0 }, sqrt.ToArray());

            // Reductions
            double sum = TensorOps.Sum(t).Scalar;
            Assert.Equal(30.0, sum);

            double mean = TensorOps.Mean(t).Scalar;
            Assert.Equal(7.5, mean);

            double max = TensorOps.Max(t).Scalar;
            Assert.Equal(16.0, max);

            double min = TensorOps.Min(t).Scalar;
            Assert.Equal(1.0, min);
        }
    }
}
