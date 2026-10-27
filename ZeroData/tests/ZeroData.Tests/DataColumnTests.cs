using System;
using Xunit;
using ZeroData.Core;

namespace ZeroData.Tests
{
    public class DataColumnTests
    {
        [Fact]
        public void DataColumn_ContiguousAccessAndSlicing_WorkAccurately()
        {
            var col = new DataColumn<double>("Pressure", new[] { 101.3, 102.5, 99.8, 104.1 });
            Assert.Equal(4, col.Length);
            Assert.Equal("Pressure", col.Name);
            Assert.Equal(typeof(double), col.DataType);

            Assert.Equal(101.3, col[0]);
            Assert.Equal(104.1, col[3]);

            // Slice [1, 2) -> 102.5, 99.8
            var slice = (DataColumn<double>)col.Slice(1, 2);
            Assert.Equal(2, slice.Length);
            Assert.Equal(102.5, slice[0]);
            Assert.Equal(99.8, slice[1]);
        }

        [Fact]
        public void DataColumn_Aggregations_AreExact()
        {
            var col = new DataColumn<double>("Voltage", new[] { 10.0, 20.0, 30.0, 40.0 });

            Assert.Equal(100.0, col.SumAsDouble());
            Assert.Equal(25.0, col.MeanAsDouble());
            Assert.Equal(10.0, col.MinAsDouble());
            Assert.Equal(40.0, col.MaxAsDouble());
        }

        [Fact]
        public void DataColumn_Filter_ExtractsTargetRowIndices()
        {
            var col = new DataColumn<string>("Device", new[] { "SensorA", "SensorB", "SensorA", "SensorC" });
            var filtered = (DataColumn<string>)col.Filter(new[] { 0, 2 });

            Assert.Equal(2, filtered.Length);
            Assert.Equal("SensorA", filtered[0]);
            Assert.Equal("SensorA", filtered[1]);
        }

        [Fact]
        public void DataColumn_NullBitmap_TracksValidityWithoutAllocation()
        {
            var col = new DataColumn<int>("Stock", new[] { 100, 200, 300, 400 });
            Assert.False(col.HasNulls);
            Assert.False(col.IsNull(0));

            col.SetNull(1);
            Assert.True(col.HasNulls);
            Assert.False(col.IsNull(0));
            Assert.True(col.IsNull(1));
            Assert.Null(col.GetValue(1));
            Assert.Equal(100, col.GetValue(0));

            // Slicing preserves null state
            var slice = (DataColumn<int>)col.Slice(0, 2);
            Assert.True(slice.HasNulls);
            Assert.False(slice.IsNull(0));
            Assert.True(slice.IsNull(1));

            // Filter preserves null state
            var filtered = (DataColumn<int>)col.Filter(new[] { 1, 2 });
            Assert.True(filtered.HasNulls);
            Assert.True(filtered.IsNull(0));
            Assert.False(filtered.IsNull(1));
            Assert.Equal(300, filtered.GetValue(1));
        }

        [Fact]
        public void DataColumn_DecimalAggregations_AreExactAndZeroBoxing()
        {
            var col = new DataColumn<decimal>("InvoiceTotal", new[] { 1500.50m, 2500.75m, 1000.25m });
            Assert.Equal(5001.50m, col.SumAsDecimal());
            Assert.Equal(1667.1666666666666666666666667m, col.AverageAsDecimal());
            Assert.Equal(1000.25m, col.MinAsDecimal());
            Assert.Equal(2500.75m, col.MaxAsDecimal());

            // With nulls
            col.SetNull(1);
            Assert.Equal(2500.75m, col.SumAsDecimal());
            Assert.Equal(1250.375m, col.AverageAsDecimal());
            Assert.Equal(1000.25m, col.MinAsDecimal());
            Assert.Equal(1500.50m, col.MaxAsDecimal());
        }
    }
}
