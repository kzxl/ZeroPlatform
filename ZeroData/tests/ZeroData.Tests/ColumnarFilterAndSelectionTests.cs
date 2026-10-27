using System;
using System.Linq;
using Xunit;
using ZeroData.Core;

namespace ZeroData.Tests
{
    public class ColumnarFilterAndSelectionTests
    {
        [Fact]
        public void TestVectorizedColumnFilter_Double()
        {
            var data = new double[100];
            for (int i = 0; i < 100; i++) data[i] = i * 2.5; // 0.0, 2.5, 5.0, ... 247.5

            var col = new DataColumn<double>("Values", data);

            // GreaterThan 100.0
            var filteredGt = col.Filter(FilterOp.GreaterThan, 100.0);
            Assert.Equal(data.Count(x => x > 100.0), filteredGt.Length);
            for (int i = 0; i < filteredGt.Length; i++)
            {
                Assert.True(filteredGt[i] > 100.0);
            }

            // Between [50.0, 150.0] inclusive
            var filteredBetween = col.FilterBetween(50.0, 150.0, inclusive: true);
            Assert.Equal(data.Count(x => x >= 50.0 && x <= 150.0), filteredBetween.Length);
            for (int i = 0; i < filteredBetween.Length; i++)
            {
                Assert.True(filteredBetween[i] >= 50.0 && filteredBetween[i] <= 150.0);
            }
        }

        [Fact]
        public void TestVectorizedColumnFilter_IntAndLong()
        {
            var intData = new int[] { 10, 25, 50, 75, 100, 125, 150, 175, 200 };
            var colInt = new DataColumn<int>("Ints", intData);

            var filteredInt = colInt.Filter(FilterOp.LessThanOrEqual, 100);
            Assert.Equal(5, filteredInt.Length);
            Assert.Equal(10, filteredInt[0]);
            Assert.Equal(100, filteredInt[4]);

            var longData = new long[] { 1000L, 2000L, 3000L, 4000L, 5000L };
            var colLong = new DataColumn<long>("Longs", longData);
            var filteredLong = colLong.Filter(FilterOp.Equal, 3000L);
            Assert.Equal(1, filteredLong.Length);
            Assert.Equal(3000L, filteredLong[0]);
        }

        [Fact]
        public void TestNullBitmapHandling_In_Filter()
        {
            var data = new double[] { 10.0, 20.0, 30.0, 40.0, 50.0 };
            var col = new DataColumn<double>("WithNulls", data);
            col.SetNull(1); // Row 1 (20.0) is null
            col.SetNull(3); // Row 3 (40.0) is null

            // Filter GreaterThan 15.0 -> would match 20.0, 30.0, 40.0, 50.0 in scalar, but 20 & 40 are null!
            var filtered = col.Filter(FilterOp.GreaterThan, 15.0);
            Assert.Equal(2, filtered.Length); // Only 30.0 and 50.0
            Assert.Equal(30.0, filtered[0]);
            Assert.Equal(50.0, filtered[1]);
        }

        [Fact]
        public void TestSelectionMask_BitwiseComposition()
        {
            var colA = new DataColumn<double>("A", new double[] { 10, 20, 30, 40, 50, 60 });
            var colB = new DataColumn<int>("B", new int[] { 100, 200, 300, 400, 500, 600 });
            var df = new DataFrame(colA, colB);

            // Condition 1: A >= 30 (rows 2, 3, 4, 5)
            var maskA = colA.CreateMask(FilterOp.GreaterThanOrEqual, 30.0);
            // Condition 2: B <= 400 (rows 0, 1, 2, 3)
            var maskB = colB.CreateMask(FilterOp.LessThanOrEqual, 400);

            // AND: rows 2, 3
            var andMask = maskA.And(maskB);
            var andIndices = andMask.ToIndices();
            Assert.Equal(2, andIndices.Length);
            Assert.Equal(2, andIndices[0]);
            Assert.Equal(3, andIndices[1]);

            var filteredDf = df.Filter(andMask);
            Assert.Equal(2, filteredDf.RowCount);
            Assert.Equal(30.0, filteredDf.Column<double>("A")[0]);
            Assert.Equal(40.0, filteredDf.Column<double>("A")[1]);
            Assert.Equal(300, filteredDf.Column<int>("B")[0]);
            Assert.Equal(400, filteredDf.Column<int>("B")[1]);

            // OR: rows 0, 1, 2, 3, 4, 5
            var orMask = maskA.Or(maskB);
            Assert.Equal(6, orMask.SelectedCount);
        }

        [Fact]
        public void TestDataFrame_Where_VectorizedAndLazyFrame()
        {
            var colPrice = new DataColumn<double>("Price", new double[] { 15.0, 80.0, 120.0, 45.0, 200.0, 95.0 });
            var colQty = new DataColumn<int>("Qty", new int[] { 1, 2, 3, 4, 5, 6 });
            var df = new DataFrame(colPrice, colQty);

            // Eager vectorized filter
            var eagerFiltered = df.Where<double>("Price", FilterOp.GreaterThan, 90.0);
            Assert.Equal(3, eagerFiltered.RowCount); // 120.0, 200.0, 95.0

            // Eager range filter
            var rangeFiltered = df.WhereBetween<double>("Price", 40.0, 100.0);
            Assert.Equal(3, rangeFiltered.RowCount); // 80.0, 45.0, 95.0

            // Lazy vectorized execution
            var lazyResult = df.Lazy()
                .Where<double>("Price", FilterOp.GreaterThan, 50.0)
                .Select("Price")
                .Collect();

            Assert.Equal(4, lazyResult.RowCount);
            Assert.Equal(1, lazyResult.ColumnCount);
            Assert.Equal("Price", lazyResult.ColumnNames[0]);
        }

        [Fact]
        public void TestVectorAggregations_MinMax_Variance()
        {
            var col = new DataColumn<double>("Values", new double[] { 10.0, 20.0, 30.0, 40.0, 50.0 });

            col.SimdMinMax(out double min, out double max);
            Assert.Equal(10.0, min);
            Assert.Equal(50.0, max);

            col.SimdVariance(out double mean, out double variance);
            Assert.Equal(30.0, mean);
            Assert.Equal(250.0, variance, 4);

            var floatCol = new DataColumn<float>("FValues", new float[] { 2.0f, 4.0f, 6.0f, 8.0f });
            floatCol.SimdMinMax(out float fMin, out float fMax);
            Assert.Equal(2.0f, fMin);
            Assert.Equal(8.0f, fMax);
        }

        [Fact]
        public void TestVectorizedColumnFilter_DateTime()
        {
            var baseDate = new DateTime(2026, 1, 1);
            var dates = new DateTime[10];
            for (int i = 0; i < 10; i++) dates[i] = baseDate.AddDays(i);

            var col = new DataColumn<DateTime>("Dates", dates);
            col.SetNull(5); // row 5 is null

            // GreaterThan Jan 4th
            var filteredGt = col.Filter(FilterOp.GreaterThan, baseDate.AddDays(4));
            // Should match row 6, 7, 8, 9 (row 5 is null) => 4 rows
            Assert.Equal(4, filteredGt.Length);
            for (int i = 0; i < filteredGt.Length; i++)
            {
                Assert.True(filteredGt[i] > baseDate.AddDays(4));
            }

            // Between Jan 2nd and Jan 7th
            var filteredBetween = col.FilterBetween(baseDate.AddDays(2), baseDate.AddDays(7), inclusive: true);
            // Rows 2, 3, 4, 6, 7 (5 is null) => 5 rows
            Assert.Equal(5, filteredBetween.Length);
        }

        [Fact]
        public void TestVectorizedColumnFilter_Decimal()
        {
            var amounts = new decimal[] { 100.50m, 250.75m, 500.00m, 750.25m, 1000.00m };
            var col = new DataColumn<decimal>("Amounts", amounts);
            col.SetNull(2); // row 2 (500.00m) is null

            // GreaterThanOrEqual 250.75m
            var filtered = col.Filter(FilterOp.GreaterThanOrEqual, 250.75m);
            // Rows 1, 3, 4 (row 2 is null) => 3 rows
            Assert.Equal(3, filtered.Length);
            Assert.Equal(250.75m, filtered[0]);
            Assert.Equal(750.25m, filtered[1]);
            Assert.Equal(1000.00m, filtered[2]);

            // FilterBetween [200m, 800m]
            var filteredBetween = col.FilterBetween(200.00m, 800.00m, inclusive: true);
            // Rows 1, 3 (row 2 is null) => 2 rows
            Assert.Equal(2, filteredBetween.Length);
            Assert.Equal(250.75m, filteredBetween[0]);
            Assert.Equal(750.25m, filteredBetween[1]);
        }
    }
}
