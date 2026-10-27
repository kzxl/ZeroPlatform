using System;
using System.Linq;
using Xunit;
using ZeroPrimitives.Simd;

namespace ZeroPrimitives.Tests
{
    public class SimdComparisonAndAggregationsTests
    {
        [Fact]
        public void Double_SelectIndices_MatchesExpected()
        {
            var data = new double[] { 1.5, 10.0, 25.5, 3.0, 50.2, 0.5, 100.0, 75.0, 10.0, 12.0 };
            Span<int> dst = new int[data.Length];

            // GreaterThan 20.0 -> indices 2 (25.5), 4 (50.2), 6 (100.0), 7 (75.0)
            int count = SimdComparison.SelectIndices(data, VectorCompareOp.GreaterThan, 20.0, dst);
            Assert.Equal(4, count);
            Assert.Equal(2, dst[0]);
            Assert.Equal(4, dst[1]);
            Assert.Equal(6, dst[2]);
            Assert.Equal(7, dst[3]);

            // Equal 10.0 -> indices 1, 8
            count = SimdComparison.SelectIndices(data, VectorCompareOp.Equal, 10.0, dst);
            Assert.Equal(2, count);
            Assert.Equal(1, dst[0]);
            Assert.Equal(8, dst[1]);

            // LessThanOrEqual 3.0 -> indices 0 (1.5), 3 (3.0), 5 (0.5)
            count = SimdComparison.SelectIndices(data, VectorCompareOp.LessThanOrEqual, 3.0, dst);
            Assert.Equal(3, count);
            Assert.Equal(0, dst[0]);
            Assert.Equal(3, dst[1]);
            Assert.Equal(5, dst[2]);
        }

        [Fact]
        public void Double_SelectBetween_MatchesExpected()
        {
            var data = new double[64];
            for (int i = 0; i < 64; i++) data[i] = i * 2.0; // 0, 2, 4, ... 126

            Span<int> dst = new int[64];
            // Select [20.0, 40.0] inclusive -> values 20, 22, 24, 26, 28, 30, 32, 34, 36, 38, 40 (11 elements)
            int count = SimdComparison.SelectBetween(data, 20.0, 40.0, dst, inclusive: true);
            Assert.Equal(11, count);
            for (int i = 0; i < count; i++)
            {
                double val = data[dst[i]];
                Assert.True(val >= 20.0 && val <= 40.0);
            }

            // Exclusive (20.0, 40.0) -> 9 elements
            count = SimdComparison.SelectBetween(data, 20.0, 40.0, dst, inclusive: false);
            Assert.Equal(9, count);
        }

        [Fact]
        public void Float_And_Int_SelectIndices_MatchesExpected()
        {
            var floats = new float[32];
            for (int i = 0; i < 32; i++) floats[i] = i * 1.5f;

            Span<int> dstF = new int[32];
            int countF = SimdComparison.SelectIndices(floats, VectorCompareOp.GreaterThan, 20.0f, dstF);
            var expectedCountF = floats.Count(x => x > 20.0f);
            Assert.Equal(expectedCountF, countF);

            var ints = new int[48];
            for (int i = 0; i < 48; i++) ints[i] = i * 5;

            Span<int> dstI = new int[48];
            int countI = SimdComparison.SelectBetween(ints, 50, 100, dstI, inclusive: true);
            var expectedCountI = ints.Count(x => x >= 50 && x <= 100);
            Assert.Equal(expectedCountI, countI);
        }

        [Fact]
        public void Long_SelectIndices_MatchesExpected()
        {
            var longs = new long[] { 100L, 500L, 1000L, 2000L, 50L, 800L, 1500L, 3000L, 100L };
            Span<int> dst = new int[longs.Length];

            int count = SimdComparison.SelectIndices(longs, VectorCompareOp.GreaterThanOrEqual, 1000L, dst);
            Assert.Equal(4, count);
            Assert.Equal(2, dst[0]); // 1000
            Assert.Equal(3, dst[1]); // 2000
            Assert.Equal(6, dst[2]); // 1500
            Assert.Equal(7, dst[3]); // 3000
        }

        [Fact]
        public void SimdAggregations_MinMax_Sum_Mean_Variance()
        {
            var data = new double[] { 10.0, 20.0, 30.0, 40.0, 50.0, 60.0, 70.0, 80.0, 90.0 };

            SimdAggregations.MinMax(data, out double min, out double max);
            Assert.Equal(10.0, min);
            Assert.Equal(90.0, max);

            double sum = SimdAggregations.Sum(data);
            Assert.Equal(450.0, sum);

            double mean = SimdAggregations.Mean(data);
            Assert.Equal(50.0, mean);

            SimdAggregations.Variance(data, out double varMean, out double variance);
            Assert.Equal(50.0, varMean);
            Assert.Equal(750.0, variance, 4);

            var intData = new int[] { -5, 100, 20, 50, -200, 350 };
            SimdAggregations.MinMax(intData, out int intMin, out int intMax);
            Assert.Equal(-200, intMin);
            Assert.Equal(350, intMax);

            var floatData = new float[] { 1.5f, 2.5f, 3.5f, 4.5f };
            SimdAggregations.MinMax(floatData, out float fMin, out float fMax);
            Assert.Equal(1.5f, fMin);
            Assert.Equal(4.5f, fMax);
            Assert.Equal(12.0f, SimdAggregations.Sum(floatData));
            Assert.Equal(3.0f, SimdAggregations.Mean(floatData));
        }
    }
}
