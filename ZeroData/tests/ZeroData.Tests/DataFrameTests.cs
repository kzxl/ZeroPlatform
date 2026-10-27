using System;
using System.Linq;
using Xunit;
using ZeroData.Core;

namespace ZeroData.Tests
{
    public class DataFrameTests
    {
        [Fact]
        public void DataFrame_BasicOperations_WorkCorrectly()
        {
            var df = new DataFrame();
            df.AddColumn(new DataColumn<int>("Id", new[] { 1, 2, 3, 4 }));
            df.AddColumn(new DataColumn<string>("Category", new[] { "CNC", "Laser", "CNC", "Robot" }));
            df.AddColumn(new DataColumn<double>("Power", new[] { 15.5, 45.2, 18.0, 8.5 }));

            Assert.Equal(4, df.RowCount);
            Assert.Equal(3, df.ColumnCount);

            // Access typed column
            var powerCol = df.Column<double>("Power");
            Assert.Equal(15.5, powerCol[0]);

            // Slicing
            var slice = df.Slice(1, 2);
            Assert.Equal(2, slice.RowCount);
            Assert.Equal("Laser", slice.Column<string>("Category")[0]);
            Assert.Equal(45.2, slice.Column<double>("Power")[0]);
        }

        [Fact]
        public void DataFrame_WhereFilter_ExtractsMatchingRows()
        {
            var df = new DataFrame();
            df.AddColumn(new DataColumn<string>("Part", new[] { "P1", "P2", "P3", "P4" }));
            df.AddColumn(new DataColumn<double>("DefectRate", new[] { 0.01, 0.05, 0.02, 0.08 }));

            // Filter parts with defect rate > 0.03
            var highDefects = df.Where<double>("DefectRate", rate => rate > 0.03);

            Assert.Equal(2, highDefects.RowCount);
            var parts = highDefects.Column<string>("Part");
            Assert.Equal("P2", parts[0]);
            Assert.Equal("P4", parts[1]);
        }

        [Fact]
        public void DataFrame_OrderBy_SortsAscendingAndDescending()
        {
            var df = new DataFrame();
            df.AddColumn(new DataColumn<int>("Score", new[] { 50, 10, 80, 30 }));
            df.AddColumn(new DataColumn<string>("Name", new[] { "B", "D", "A", "C" }));

            var sortedAsc = df.OrderBy<int>("Score", ascending: true);
            Assert.Equal(new[] { 10, 30, 50, 80 }, sortedAsc.Column<int>("Score").ToArray());
            Assert.Equal(new[] { "D", "C", "B", "A" }, sortedAsc.Column<string>("Name").ToArray());

            var sortedDesc = df.OrderBy<int>("Score", ascending: false);
            Assert.Equal(new[] { 80, 50, 30, 10 }, sortedDesc.Column<int>("Score").ToArray());
            Assert.Equal(new[] { "A", "B", "C", "D" }, sortedDesc.Column<string>("Name").ToArray());
        }

        [Fact]
        public void DataFrame_GroupBy_AggregatesGroupMetrics()
        {
            var df = new DataFrame();
            df.AddColumn(new DataColumn<string>("Machine", new[] { "M1", "M2", "M1", "M2", "M1" }));
            df.AddColumn(new DataColumn<double>("Yield", new[] { 95.0, 80.0, 99.0, 84.0, 94.0 }));

            var group = df.GroupBy<string>("Machine");
            Assert.Equal(2, group.GroupCount);

            // Count aggregation
            var counts = group.Count();
            Assert.Equal(2, counts.RowCount);

            // Mean aggregation
            var means = group.Mean("Yield");
            Assert.Equal(2, means.RowCount);

            // Machine M1 yield mean: (95 + 99 + 94) / 3 = 96.0
            // Machine M2 yield mean: (80 + 84) / 2 = 82.0
            var meanCol = means.Column<double>("Yield_mean");
            var machCol = means.Column<string>("Machine");

            int idxM1 = machCol.ToArray().ToList().IndexOf("M1");
            int idxM2 = machCol.ToArray().ToList().IndexOf("M2");

            Assert.True(Math.Abs(meanCol[idxM1] - 96.0) < 1e-4);
            Assert.True(Math.Abs(meanCol[idxM2] - 82.0) < 1e-4);
        }

        [Fact]
        public void DataFrame_TimeSeriesResampling_DownsamplesAccurately()
        {
            var baseTime = new DateTime(2026, 9, 1, 10, 0, 0);

            var times = new[]
            {
                baseTime.AddSeconds(10), // Bucket 10:00
                baseTime.AddSeconds(40), // Bucket 10:00
                baseTime.AddMinutes(1).AddSeconds(15), // Bucket 10:01
                baseTime.AddMinutes(1).AddSeconds(45), // Bucket 10:01
            };

            var telemetry = new[] { 100.0, 110.0, 200.0, 220.0 };

            var df = new DataFrame();
            df.AddColumn(new DataColumn<DateTime>("Timestamp", times));
            df.AddColumn(new DataColumn<double>("Current", telemetry));

            // Resample by 1-minute intervals with Mean
            var resampled = df.Resample("Timestamp", TimeSpan.FromMinutes(1), "Current", ResampleAgg.Mean);

            Assert.Equal(2, resampled.RowCount);
            var resTimes = resampled.Column<DateTime>("Timestamp");
            var resMeans = resampled.Column<double>("Current_mean");

            Assert.Equal(baseTime, resTimes[0]);
            Assert.Equal(105.0, resMeans[0]); // (100 + 110) / 2

            Assert.Equal(baseTime.AddMinutes(1), resTimes[1]);
            Assert.Equal(210.0, resMeans[1]); // (200 + 220) / 2
        }

        [Fact]
        public void DataFrame_CsvRoundtrip_PreservesData()
        {
            var df = new DataFrame();
            df.AddColumn(new DataColumn<string>("Component", new[] { "Diode", "Resistor, SMD", "Capacitor" }));
            df.AddColumn(new DataColumn<double>("Resistance", new[] { 0.5, 100.0, 0.0 }));

            string csv = df.ToCsv();
            var parsed = DataFrame.FromCsv(csv);

            Assert.Equal(df.RowCount, parsed.RowCount);
            Assert.Equal(df.ColumnCount, parsed.ColumnCount);

            var compParsed = parsed.Column<string>("Component");
            Assert.Equal("Diode", compParsed[0]);
            Assert.Equal("Resistor, SMD", compParsed[1]);
            Assert.Equal("Capacitor", compParsed[2]);

            var resParsed = parsed.Column<double>("Resistance");
            Assert.Equal(0.5, resParsed[0]);
            Assert.Equal(100.0, resParsed[1]);
        }

        [Fact]
        public void ZeroDataVirtualProvider_BindsDirectlyToGrid()
        {
            var df = new DataFrame();
            df.AddColumn(new DataColumn<int>("ID", new[] { 101, 102 }));
            df.AddColumn(new DataColumn<string>("Status", new[] { "Pass", "Fail" }));

            var provider = df.CreateVirtualProvider();
            Assert.Equal(2, provider.RowCount);
            Assert.Equal(2, provider.ColumnCount);
            Assert.Equal("ID", provider.GetColumnName(0));
            Assert.Equal("Status", provider.GetColumnName(1));

            Assert.Equal(101, provider.GetValue(0, 0));
            Assert.Equal("Pass", provider.GetValue(0, 1));
            Assert.Equal(102, provider.GetValue(1, 0));
            Assert.Equal("Fail", provider.GetValue(1, 1));
        }

        [Fact]
        public void DataFrame_RowView_ZeroAllocationEnumeration()
        {
            var df = new DataFrame();
            df.AddColumn(new DataColumn<int>("Id", new[] { 1, 2, 3 }));
            df.AddColumn(new DataColumn<decimal>("Price", new[] { 19.99m, 49.50m, 9.95m }));
            df.AddColumn(new DataColumn<string>("Item", new[] { "Bolt", "Nut", "Washer" }));

            int count = 0;
            decimal totalPrice = 0m;

            foreach (var row in df.Rows)
            {
                Assert.Equal(count + 1, row.GetInt("Id"));
                Assert.Equal(count + 1, row.GetInt(0));
                totalPrice += row.GetDecimal("Price");
                Assert.NotNull(row.GetString("Item"));
                Assert.False(row.IsNull("Item"));
                count++;
            }

            Assert.Equal(3, count);
            Assert.Equal(79.44m, totalPrice);
        }

        [Fact]
        public void DataFrame_AdoNet_DataTableAndDataReaderRoundtrip()
        {
            // 1. Create source System.Data.DataTable with nulls
            var dt = new System.Data.DataTable("Products");
            dt.Columns.Add("Code", typeof(string));
            dt.Columns.Add("Qty", typeof(int));
            dt.Columns.Add("Cost", typeof(decimal));

            dt.Rows.Add("P001", 10, 150.25m);
            dt.Rows.Add("P002", DBNull.Value, 85.00m);
            dt.Rows.Add(DBNull.Value, 50, DBNull.Value);

            // 2. Convert from DataTable to DataFrame
            var df = DataFrame.FromDataTable(dt);
            Assert.Equal(3, df.RowCount);
            Assert.Equal(3, df.ColumnCount);
            Assert.Equal("P001", df.Column<string>("Code")[0]);
            Assert.True(df.GetColumn("Qty").IsNull(1));
            Assert.True(df.GetColumn("Code").IsNull(2));
            Assert.True(df.GetColumn("Cost").IsNull(2));

            // 3. Convert back to DataTable
            var exportedDt = df.ToDataTable();
            Assert.Equal(3, exportedDt.Rows.Count);
            Assert.Equal("P001", exportedDt.Rows[0]["Code"]);
            Assert.Equal(DBNull.Value, exportedDt.Rows[1]["Qty"]);
            Assert.Equal(DBNull.Value, exportedDt.Rows[2]["Code"]);

            // 4. Ingest via IDataReader
            using var reader = dt.CreateDataReader();
            var dfFromReader = DataFrame.FromDataReader(reader);
            Assert.Equal(3, dfFromReader.RowCount);
            Assert.Equal(150.25m, dfFromReader.Column<decimal>("Cost")[0]);
            Assert.True(dfFromReader.GetColumn("Qty").IsNull(1));
        }
    }
}
