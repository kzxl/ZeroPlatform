using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;
using ZeroUI.Wpf.DataGrid;

namespace ZeroUI.Desktop.Tests
{
    public class WpfGridControlTests
    {
        public class ItemModel
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public double Price { get; set; }
            public string Category { get; set; } = string.Empty;
        }

        private static List<ItemModel> CreateSampleData(int count)
        {
            var list = new List<ItemModel>(count);
            for (int i = 1; i <= count; i++)
            {
                list.Add(new ItemModel
                {
                    Id = i,
                    Name = $"Product_{i:D4}",
                    Price = (i % 10) * 15.5 + 5.0,
                    Category = (i % 3 == 0) ? "Electronics" : (i % 3 == 1) ? "Hardware" : "Apparel"
                });
            }
            return list;
        }

        [Fact]
        public void GridControl_DefaultState_ShouldHaveValidDefaults()
        {
            StaTestRunner.Run(() =>
            {
                var grid = new GridControl();
                Assert.NotNull(grid.Columns);
                Assert.Empty(grid.Columns);
                Assert.Null(grid.DataSource);
                Assert.Equal(0, grid.VisualRowCount);
                Assert.Equal(-1, grid.SelectedIndex);
            });
        }

        [Fact]
        public void GridControl_Columns_CanAddAndConfigureColumns()
        {
            StaTestRunner.Run(() =>
            {
                var grid = new GridControl();
                grid.Columns.Add(new ZeroColumn("Id", "ID", 60, CellAlignment.Right));
                grid.Columns.Add(new ZeroColumn("Name", "Product Name", 150, CellAlignment.Left));
                grid.Columns.Add(new ZeroColumn("Category", "Category", 120, CellAlignment.Center));

                Assert.Equal(3, grid.Columns.Count);
                Assert.Equal("ID", grid.Columns[0].HeaderText);
                Assert.Equal(CellAlignment.Right, grid.Columns[0].Alignment);
            });
        }

        [Fact]
        public void GridControl_DataSource_BindsAndUpdatesRowCount()
        {
            StaTestRunner.Run(() =>
            {
                var grid = new GridControl();
                grid.Columns.Add(new ZeroColumn("Id", "ID", 60));
                grid.Columns.Add(new ZeroColumn("Name", "Name", 150));
                grid.Columns.Add(new ZeroColumn("Price", "Price", 100));
                grid.Columns.Add(new ZeroColumn("Category", "Category", 100));

                var data = CreateSampleData(100);
                var source = new ZeroListSource<ItemModel>(data, grid.Columns);
                grid.DataSource = source;

                Assert.Equal(100, grid.VisualRowCount);
                Assert.Equal(0, grid.GetModelRowIndex(0));
                Assert.Equal(99, grid.GetModelRowIndex(99));
            });
        }

        [Fact]
        public void GridControl_Sorting_ShouldSortRowsCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                var grid = new GridControl();
                grid.Columns.Add(new ZeroColumn("Id", "ID", 60));
                grid.Columns.Add(new ZeroColumn("Price", "Price", 100));

                var data = CreateSampleData(20);
                var source = new ZeroListSource<ItemModel>(data, grid.Columns);
                grid.DataSource = source;

                // Sort ascending on Price (column 1)
                grid.SortByColumnAsync(1).GetAwaiter().GetResult();
                Assert.Equal(SortDirection.Ascending, grid.Columns[1].SortOrder);

                int firstModelIdx = grid.GetModelRowIndex(0);
                var firstItem = data[firstModelIdx];
                int lastModelIdx = grid.GetModelRowIndex(grid.VisualRowCount - 1);
                var lastItem = data[lastModelIdx];

                Assert.True(firstItem.Price <= lastItem.Price);

                // Sort again -> toggles to Descending
                grid.SortByColumnAsync(1).GetAwaiter().GetResult();
                Assert.Equal(SortDirection.Descending, grid.Columns[1].SortOrder);

                firstModelIdx = grid.GetModelRowIndex(0);
                firstItem = data[firstModelIdx];
                lastModelIdx = grid.GetModelRowIndex(grid.VisualRowCount - 1);
                lastItem = data[lastModelIdx];

                Assert.True(firstItem.Price >= lastItem.Price);
            });
        }

        [Fact]
        public void GridControl_Filtering_DistinctFilterShouldFilterRows()
        {
            StaTestRunner.Run(() =>
            {
                var grid = new GridControl();
                grid.Columns.Add(new ZeroColumn("Id", "ID", 60));
                grid.Columns.Add(new ZeroColumn("Category", "Category", 100));

                var data = CreateSampleData(30);
                var source = new ZeroListSource<ItemModel>(data, grid.Columns);
                grid.DataSource = source;

                Assert.Equal(30, grid.VisualRowCount);

                // Filter column 1 (Category) only to "Electronics"
                var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Electronics" };
                grid.ApplyDistinctColumnFilter(1, selected);

                Assert.True(grid.IsColumnFiltered(1));
                Assert.Equal(10, grid.VisualRowCount); // 30 / 3 = 10 Electronics items

                // Clear filters
                grid.ClearAllFilters();
                Assert.False(grid.IsColumnFiltered(1));
                Assert.Equal(30, grid.VisualRowCount);
            });
        }

        [Fact]
        public void GridControl_Grouping_GroupByColumn_ShouldGroupRowsAndExpandCollapse()
        {
            StaTestRunner.Run(() =>
            {
                var grid = new GridControl();
                grid.Columns.Add(new ZeroColumn("Id", "ID", 60));
                grid.Columns.Add(new ZeroColumn("Category", "Category", 100));

                var data = CreateSampleData(30);
                var source = new ZeroListSource<ItemModel>(data, grid.Columns);
                grid.DataSource = source;

                // Group by column 1 (Category: 3 categories -> 3 group headers + data rows)
                grid.GroupBy(new[] { 1 });
                Assert.True(grid.VisualRowCount >= 3);

                // Collapse all groups -> only group headers visible (3 headers)
                grid.CollapseAllGroups();
                Assert.Equal(3, grid.VisualRowCount);

                // Expand all groups -> group headers + data rows
                grid.ExpandAllGroups();
                Assert.Equal(33, grid.VisualRowCount); // 3 group headers + 30 data rows

                // Clear grouping
                grid.ClearGrouping();
                Assert.Equal(30, grid.VisualRowCount);
            });
        }

        [Fact]
        public void GridControl_MasterDetail_ToggleMasterRow_ShouldExpandAndCollapse()
        {
            StaTestRunner.Run(() =>
            {
                var grid = new GridControl();
                grid.AllowMasterDetail = true;
                grid.Columns.Add(new ZeroColumn("Id", "ID", 60));

                var data = CreateSampleData(10);
                grid.DataSource = new ZeroListSource<ItemModel>(data, grid.Columns);

                Assert.False(grid.IsMasterRowExpanded(0));

                // Toggle expand row 0
                grid.ToggleMasterRow(0);
                Assert.True(grid.IsMasterRowExpanded(0));

                // Toggle collapse row 0
                grid.ToggleMasterRow(0);
                Assert.False(grid.IsMasterRowExpanded(0));
            });
        }

        [Fact]
        public void GridControl_Virtualization_OneMillionRows_ShouldInitializeAndMapWithoutCrashing()
        {
            StaTestRunner.Run(() =>
            {
                var grid = new GridControl();
                grid.Columns.Add(new ZeroColumn("Id", "ID", 80));
                grid.Columns.Add(new ZeroColumn("Title", "Title", 200));

                // Virtual source simulating 1,000,000 rows
                var mockSource = new MockLargeVirtualSource(1_000_000, 2);
                grid.DataSource = mockSource;

                Assert.Equal(1_000_000, grid.VisualRowCount);

                // Test row mapping at various extremities
                Assert.Equal(0, grid.GetModelRowIndex(0));
                Assert.Equal(500_000, grid.GetModelRowIndex(500_000));
                Assert.Equal(999_999, grid.GetModelRowIndex(999_999));
            });
        }

        [Fact]
        public void GridControl_ColumnFilterPopup_EmptyAndNonEmptyValues_ShouldNotCrash()
        {
            StaTestRunner.Run(() =>
            {
                var grid = new GridControl();
                grid.Columns.Add(new ZeroColumn("Id", "ID", 60));
                grid.Columns.Add(new ZeroColumn("Name", "Name", 150));

                // 1. Popup on empty grid
                grid.ShowColumnFilterPopup(0);

                // 2. Popup with data
                var data = CreateSampleData(10);
                grid.DataSource = new ZeroListSource<ItemModel>(data, grid.Columns);

                var distinctValues = grid.GetDistinctColumnValues(1);
                Assert.Equal(10, distinctValues.Count);

                // Trigger popup
                grid.ShowColumnFilterPopup(1);
            });
        }

        [Fact]
        public void GridControl_Selection_ShouldUpdateSelectedIndexAndBlock()
        {
            StaTestRunner.Run(() =>
            {
                var grid = new GridControl();
                grid.Columns.Add(new ZeroColumn("Id", "ID", 60));
                var data = CreateSampleData(20);
                grid.DataSource = new ZeroListSource<ItemModel>(data, grid.Columns);

                grid.SelectedIndex = 5;
                Assert.Equal(5, grid.SelectedIndex);

                // Negative index resets selection
                grid.SelectedIndex = -1;
                Assert.Equal(-1, grid.SelectedIndex);
            });
        }

        [Fact]
        public void Wpf_GridControl_VirtualMode_TenMillionRows_ShouldInitializeAndOperateInstantly()
        {
            StaTestRunner.Run(() =>
            {
                var grid = new GridControl();
                grid.SelectionMode = ZeroGridSelectionMode.MultiRow;
                var col1 = new ZeroColumn("Col1", "Col 1", 100);
                col1.Summary = SummaryType.Sum;
                grid.Columns.Add(col1);
                grid.Columns.Add(new ZeroColumn("Col2", "Col 2", 150));

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var mockSource = new MockLargeVirtualSource(10_000_000, 2);
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

        private sealed class MockLargeVirtualSource : IZeroVirtualSource
        {
            public int TotalRowCount { get; }
            public int TotalColumnCount { get; }

            public MockLargeVirtualSource(int rowCount, int colCount)
            {
                TotalRowCount = rowCount;
                TotalColumnCount = colCount;
            }

            public void GetCellValue(int rowIndex, int columnIndex, ref CellValueBuffer buffer)
            {
                if (columnIndex == 0)
                {
                    buffer.Text = rowIndex.ToString().AsSpan();
                }
                else
                {
                    buffer.Text = "Item_Data".AsSpan();
                }
            }
        }
    }
}
