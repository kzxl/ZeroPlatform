using System;
using Xunit;
using ZeroData.Core;

namespace ZeroData.Tests
{
    public class ColumnVectorOpsTests
    {
        [Fact]
        public void TestDoubleVectorArithmetic()
        {
            var colA = new DataColumn<double>("A", new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0 });
            var colB = new DataColumn<double>("B", new[] { 10.0, 20.0, 30.0, 40.0, 50.0, 60.0, 70.0, 80.0, 90.0 });

            var sumCol = colA.Add(colB);
            var subCol = colB.Subtract(colA);
            var mulCol = colA.Multiply(colB);
            var divCol = colB.Divide(colA);

            for (int i = 0; i < 9; i++)
            {
                Assert.Equal(colA[i] + colB[i], sumCol[i], 6);
                Assert.Equal(colB[i] - colA[i], subCol[i], 6);
                Assert.Equal(colA[i] * colB[i], mulCol[i], 6);
                Assert.Equal(colB[i] / colA[i], divCol[i], 6);
            }
        }

        [Fact]
        public void TestDoubleScalarOperations()
        {
            var col = new DataColumn<double>("A", new[] { 2.0, 4.0, 6.0, 8.0 });
            var scaled = col.Multiply(2.5);
            var offset = col.Add(1.5);

            Assert.Equal(5.0, scaled[0]);
            Assert.Equal(10.0, scaled[1]);
            Assert.Equal(15.0, scaled[2]);
            Assert.Equal(20.0, scaled[3]);

            Assert.Equal(3.5, offset[0]);
            Assert.Equal(5.5, offset[1]);
        }

        [Fact]
        public void TestSimdSumAndDot()
        {
            var colA = new DataColumn<double>("A", new[] { 1.0, 2.0, 3.0, 4.0, 5.0 });
            var colB = new DataColumn<double>("B", new[] { 2.0, 2.0, 2.0, 2.0, 2.0 });

            double sumA = colA.SimdSum();
            Assert.Equal(15.0, sumA, 6);

            double dot = colA.SimdDot(colB);
            Assert.Equal(30.0, dot, 6);
        }

        [Fact]
        public void TestNullBitmapPropagationInVectorOps()
        {
            var colA = new DataColumn<double>("A", new[] { 1.0, 2.0, 3.0 });
            var colB = new DataColumn<double>("B", new[] { 10.0, 20.0, 30.0 });
            colA.SetNull(1); // Row 1 in colA is null

            var res = colA.Add(colB);

            Assert.True(res.HasNulls);
            Assert.False(res.IsNull(0));
            Assert.True(res.IsNull(1)); // Null propagates to result
            Assert.False(res.IsNull(2));

            Assert.Equal(11.0, res[0]);
            Assert.Equal(33.0, res[2]);
        }

        [Fact]
        public void TestIntAndDecimalVectorOperations()
        {
            var colIntA = new DataColumn<int>("I1", new[] { 10, 20, 30, 40 });
            var colIntB = new DataColumn<int>("I2", new[] { 1, 2, 3, 4 });
            var colIntSum = colIntA.Add(colIntB);

            Assert.Equal(11, colIntSum[0]);
            Assert.Equal(44, colIntSum[3]);
            Assert.Equal(100L, colIntA.SimdSum());

            var colDecA = new DataColumn<decimal>("D1", new[] { 10.5m, 20.25m });
            var colDecB = new DataColumn<decimal>("D2", new[] { 1.5m, 0.75m });
            var colDecSum = colDecA.Add(colDecB);

            Assert.Equal(12.0m, colDecSum[0]);
            Assert.Equal(21.0m, colDecSum[1]);
        }
    }
}
