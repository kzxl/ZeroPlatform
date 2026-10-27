using System;
using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;
using ZeroUI.WinForms.DataGrid;

namespace ZeroUI.Desktop.Tests
{
    public class WinFormsGridControlTests
    {
        public class ProductRecord
        {
            public int SkuId { get; set; }
            public string SkuCode { get; set; } = string.Empty;
            public decimal UnitPrice { get; set; }
            public string Category { get; set; } = string.Empty;
        }

        private static List<ProductRecord> CreateSampleProducts(int count)
        {
            var list = new List<ProductRecord>(count);
            for (int i = 1; i <= count; i++)
            {
                list.Add(new ProductRecord
                {
                    SkuId = i,
                    SkuCode = $"SKU-{i:00000}",
                    UnitPrice = (i % 20) * 12.5m + 1.0m,
                    Category = (i % 3 == 0) ? "Mechanical" : (i % 3 == 1) ? "Electrical" : "Optical"
                });
            }
            return list;
        }

        [Fact]
        public void WinForms_GridControl_DefaultProperties_ShouldBeValid()
        {
            StaTestRunner.Run(() =>
            {
                using var grid = new GridControl();
                Assert.NotNull(grid.Columns);
                Assert.Empty(grid.Columns);
                Assert.Null(grid.DataSource);
                Assert.Equal(GridViewType.Table, grid.ViewType);
                Assert.True(grid.EnableAdaptiveHighRefresh);
                Assert.Equal(0, grid.VisualRowCount);
            });
        }

        [Fact]
        public void WinForms_GridControl_ColumnsAndDataSource_ShouldBindCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                using var grid = new GridControl();
                grid.Columns.Add(new ZeroColumn("SkuId", "ID", 60));
                grid.Columns.Add(new ZeroColumn("SkuCode", "Code", 120));
                grid.Columns.Add(new ZeroColumn("UnitPrice", "Price", 90));
                grid.Columns.Add(new ZeroColumn("Category", "Category", 110));

                var data = CreateSampleProducts(50);
                var source = new ZeroListSource<ProductRecord>(data, grid.Columns);
                grid.DataSource = source;

                Assert.Equal(50, grid.VisualRowCount);
                Assert.Equal(0, grid.GetModelRowIndex(0));
                Assert.Equal(49, grid.GetModelRowIndex(49));
            });
        }

        [Fact]
        public void WinForms_GridControl_VirtualMode_OneMillionRows_ShouldInitializeAndMap()
        {
            StaTestRunner.Run(() =>
            {
                using var grid = new GridControl();
                grid.Columns.Add(new ZeroColumn("Col1", "Col 1", 100));
                grid.Columns.Add(new ZeroColumn("Col2", "Col 2", 150));

                var mockSource = new MockVirtualSource(1_000_000, 2);
                grid.DataSource = mockSource;

                Assert.Equal(1_000_000, grid.VisualRowCount);
                Assert.Equal(0, grid.GetModelRowIndex(0));
                Assert.Equal(500_000, grid.GetModelRowIndex(500_000));
                Assert.Equal(999_999, grid.GetModelRowIndex(999_999));
            });
        }

        [Fact]
        public void WinForms_GridControl_VirtualMode_TenMillionRows_ShouldInitializeAndOperateInstantly()
        {
            StaTestRunner.Run(() =>
            {
                using var grid = new GridControl();
                grid.SelectionMode = ZeroGridSelectionMode.MultiRow;
                var col1 = new ZeroColumn("Col1", "Col 1", 100);
                col1.Summary = SummaryType.Sum;
                grid.Columns.Add(col1);
                grid.Columns.Add(new ZeroColumn("Col2", "Col 2", 150));

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var mockSource = new MockVirtualSource(10_000_000, 2);
                grid.DataSource = mockSource;
                sw.Stop();

                // Binding 10M rows must take less than 100ms with identity virtualization
                Assert.True(sw.ElapsedMilliseconds < 100, $"Binding took {sw.ElapsedMilliseconds}ms, expected < 100ms");
                Assert.Equal(10_000_000, grid.VisualRowCount);
                Assert.Equal(0, grid.GetModelRowIndex(0));
                Assert.Equal(5_000_000, grid.GetModelRowIndex(5_000_000));
                Assert.Equal(9_999_999, grid.GetModelRowIndex(9_999_999));

                // Virtual selection must be O(1) instantaneous
                sw.Restart();
                grid.SelectAllRows();
                sw.Stop();
                Assert.True(sw.ElapsedMilliseconds < 50, $"SelectAll took {sw.ElapsedMilliseconds}ms, expected < 50ms");
                Assert.Equal(10_000_000, grid.SelectedRowCount);
                Assert.True(grid.IsVisualRowSelected(0));
                Assert.True(grid.IsVisualRowSelected(5_000_000));
                Assert.True(grid.IsVisualRowSelected(9_999_999));

                grid.ClearRowSelection();
                Assert.Equal(0, grid.SelectedRowCount);
                Assert.False(grid.IsVisualRowSelected(5_000_000));

                // Summary for 10M rows must not block UI thread (returns "Calculating..." immediately)
                sw.Restart();
                string summaryText = grid.GetColumnSummaryText(0);
                sw.Stop();
                Assert.True(sw.ElapsedMilliseconds < 50, $"Summary query took {sw.ElapsedMilliseconds}ms, expected < 50ms");
                Assert.Equal("Calculating...", summaryText);
            });
        }

        [Fact]
        public void WinForms_GridControl_Filtering_ShouldFilterRows()
        {
            StaTestRunner.Run(() =>
            {
                using var grid = new GridControl();
                grid.Columns.Add(new ZeroColumn("SkuId", "ID", 60));
                grid.Columns.Add(new ZeroColumn("Category", "Category", 110));

                var data = CreateSampleProducts(30);
                grid.DataSource = new ZeroListSource<ProductRecord>(data, grid.Columns);

                Assert.Equal(30, grid.VisualRowCount);

                // Filter to "Optical" category (Category index 1)
                grid.ApplyFilter(modelRow => data[modelRow].Category == "Optical");
                Assert.Equal(10, grid.VisualRowCount);

                // Clear filter
                grid.ApplyFilter(null);
                Assert.Equal(30, grid.VisualRowCount);
            });
        }

        [Fact]
        public void WinForms_GridControl_Grouping_GroupByColumn_ShouldExpandAndCollapse()
        {
            StaTestRunner.Run(() =>
            {
                using var grid = new GridControl();
                grid.Columns.Add(new ZeroColumn("SkuId", "ID", 60));
                grid.Columns.Add(new ZeroColumn("Category", "Category", 110));

                var data = CreateSampleProducts(30);
                grid.DataSource = new ZeroListSource<ProductRecord>(data, grid.Columns);

                grid.GroupBy(new[] { 1 });
                Assert.True(grid.VisualRowCount >= 3);

                grid.CollapseAllGroups();
                Assert.Equal(3, grid.VisualRowCount);

                grid.ExpandAllGroups();
                Assert.Equal(33, grid.VisualRowCount);

                grid.ClearGrouping();
                Assert.Equal(30, grid.VisualRowCount);
            });
        }

        [Fact]
        public void WinForms_GridControl_SetDataSource_DataTable_BindsCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                using var grid = new GridControl();
                var dt = new System.Data.DataTable();
                dt.Columns.Add("Code", typeof(string));
                dt.Columns.Add("Qty", typeof(int));
                dt.Rows.Add("ITEM-01", 100);
                dt.Rows.Add("ITEM-02", 250);

                grid.SetDataSource(dt);

                Assert.NotNull(grid.DataSource);
                Assert.IsType<DataTableSource>(grid.DataSource);
                Assert.Equal(2, grid.Columns.Count);
                Assert.Equal(2, grid.VisualRowCount);
            });
        }

        [Fact]
        public void WinForms_GridControl_SetDataSource_DataFrame_BindsCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                using var grid = new GridControl();
                var df = new ZeroData.Core.DataFrame();
                df.AddColumn(new ZeroData.Core.DataColumn<string>("Machine", new[] { "CNC-01", "CNC-02", "Laser-01" }));
                df.AddColumn(new ZeroData.Core.DataColumn<double>("Temp", new[] { 65.5, 72.1, 44.0 }));

                grid.SetDataSource(df);

                Assert.NotNull(grid.DataSource);
                Assert.IsType<DataFrameSource>(grid.DataSource);
                Assert.Equal(2, grid.Columns.Count);
                Assert.Equal(3, grid.VisualRowCount);
            });
        }

        private sealed class MockVirtualSource : IZeroVirtualSource
        {
            public int TotalRowCount { get; }
            public int TotalColumnCount { get; }

            public MockVirtualSource(int rowCount, int colCount)
            {
                TotalRowCount = rowCount;
                TotalColumnCount = colCount;
            }

            public void GetCellValue(int rowIndex, int columnIndex, ref CellValueBuffer buffer)
            {
                buffer.Text = $"Row_{rowIndex}".AsSpan();
            }
        }
    }
}
