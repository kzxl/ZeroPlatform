using System;
using System.Data;
using Xunit;
using ZeroData.Core;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;

namespace ZeroUI.Core.Tests
{
    public class MultiSourceGridTests
    {
        [Fact]
        public void ZeroDataTableSource_BasicBinding_MapsCorrectly()
        {
            var dt = new DataTable("TestTable");
            dt.Columns.Add("Id", typeof(int));
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("Score", typeof(double));
            dt.Columns.Add("IsActive", typeof(bool));
            dt.Columns.Add("CreatedAt", typeof(DateTime));

            var now = new DateTime(2026, 9, 21, 15, 0, 0);
            dt.Rows.Add(1, "Alpha", 95.5, true, now);
            dt.Rows.Add(2, "Beta", 88.0, false, now.AddDays(1));
            dt.Rows.Add(3, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value);

            var source = new DataTableSource(dt);

            Assert.Equal(3, source.TotalRowCount);
            Assert.Equal(5, source.TotalColumnCount);

            // Test Cell Values
            var buf = new CellValueBuffer();
            source.GetCellValue(0, 0, ref buf);
            Assert.Equal("1", buf.Text.ToString());
            Assert.Equal(CellAlignment.Right, buf.Alignment);

            buf.Reset();
            source.GetCellValue(0, 1, ref buf);
            Assert.Equal("Alpha", buf.Text.ToString());
            Assert.Equal(CellAlignment.Left, buf.Alignment);

            buf.Reset();
            source.GetCellValue(0, 3, ref buf);
            Assert.Equal(CellAlignment.Center, buf.Alignment);

            // Test Null cell
            buf.Reset();
            source.GetCellValue(2, 1, ref buf);
            Assert.True(buf.Text.IsEmpty);

            // Test Editing
            bool edited = source.SetCellValue(2, 1, "Gamma");
            Assert.True(edited);
            buf.Reset();
            source.GetCellValue(2, 1, ref buf);
            Assert.Equal("Gamma", buf.Text.ToString());

            // Test Sorting Comparison
            int cmp = source.CompareRows(0, 1, 0); // 1 vs 2
            Assert.True(cmp < 0);
        }

        [Fact]
        public void ZeroDataTableSource_DataViewFilter_ReflectsActiveCount()
        {
            var dt = new DataTable();
            dt.Columns.Add("Status", typeof(string));
            dt.Rows.Add("Active");
            dt.Rows.Add("Inactive");
            dt.Rows.Add("Active");

            var view = dt.DefaultView;
            view.RowFilter = "Status = 'Active'";

            var source = new DataTableSource(view);

            Assert.Equal(2, source.TotalRowCount);
            var buf = new CellValueBuffer();
            source.GetCellValue(0, 0, ref buf);
            Assert.Equal("Active", buf.Text.ToString());
            buf.Reset();
            source.GetCellValue(1, 0, ref buf);
            Assert.Equal("Active", buf.Text.ToString());
        }

        [Fact]
        public void DataFrameSource_BasicAndCategoricalBinding_WorksAccurately()
        {
            var df = new DataFrame();
            df.AddColumn(new DataColumn<int>("Id", new[] { 101, 102, 103 }));
            df.AddColumn(new DataColumn<string>("Category", new[] { "CNC", "Laser", "CNC" }));
            df.AddColumn(new DataColumn<double>("Power", new[] { 45.5, 120.0, 48.2 }));

            // Categorize column
            df.Categorize("Category");

            var source = new DataFrameSource(df);

            Assert.Equal(3, source.TotalRowCount);
            Assert.Equal(3, source.TotalColumnCount);

            // Test Int column
            var buf = new CellValueBuffer();
            source.GetCellValue(0, 0, ref buf);
            Assert.Equal("101", buf.Text.ToString());
            Assert.Equal(CellAlignment.Right, buf.Alignment);

            // Test Categorical String column
            buf.Reset();
            source.GetCellValue(0, 1, ref buf);
            Assert.Equal("CNC", buf.Text.ToString());
            Assert.Equal(CellAlignment.Left, buf.Alignment);

            buf.Reset();
            source.GetCellValue(1, 1, ref buf);
            Assert.Equal("Laser", buf.Text.ToString());

            // Test CompareRows
            int cmp = source.CompareRows(0, 1, 0); // 101 vs 102
            Assert.True(cmp < 0);

            // Test Column Generation
            var cols = source.GenerateColumns();
            Assert.Equal(3, cols.Count);
            Assert.Equal("Id", cols[0].FieldName);
            Assert.Equal("Category", cols[1].FieldName);
            Assert.Equal("Power", cols[2].FieldName);
        }

        [Fact]
        public void DataFrameSource_Editing_UpdatesUnderlyingColumn()
        {
            var df = new DataFrame();
            df.AddColumn(new DataColumn<int>("Qty", new[] { 10, 20 }));
            var source = new DataFrameSource(df);

            bool success = source.SetCellValue(0, 0, "99");
            Assert.True(success);

            var buf = new CellValueBuffer();
            source.GetCellValue(0, 0, ref buf);
            Assert.Equal("99", buf.Text.ToString());
            Assert.Equal(99, df.Column<int>("Qty")[0]);
        }

        [Fact]
        public void LegacyAliases_ZeroDataTableSource_And_ZeroDataFrameSource_MaintainBackwardCompatibility()
        {
            var dt = new DataTable();
            dt.Columns.Add("X", typeof(int));
            dt.Rows.Add(123);

#pragma warning disable CS0618 // Type or member is obsolete
            var legacyDtSource = new ZeroDataTableSource(dt);
            Assert.IsAssignableFrom<DataTableSource>(legacyDtSource);
            Assert.Equal(1, legacyDtSource.TotalRowCount);

            var df = new DataFrame();
            df.AddColumn(new DataColumn<int>("Y", new[] { 456 }));
            var legacyDfSource = new ZeroDataFrameSource(df);
            Assert.IsAssignableFrom<DataFrameSource>(legacyDfSource);
            Assert.Equal(1, legacyDfSource.TotalRowCount);
#pragma warning restore CS0618
        }
    }
}
