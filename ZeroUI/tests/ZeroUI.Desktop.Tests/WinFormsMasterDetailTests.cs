using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Xunit;
using ZeroUI.WinForms.DataGrid;

namespace ZeroUI.Desktop.Tests
{
    public class WinFormsMasterDetailTests
    {
        public class TestOrder
        {
            public int Id { get; set; }
            public string Code { get; set; } = string.Empty;
        }

        public class TestOrderItem
        {
            public int ItemId { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        [Fact]
        public void MasterDetail_ExpandMasterRow_CreatesDetailControl_WithoutInfinitePaintLoop()
        {
            StaTestRunner.Run(() =>
            {
                using var form = new Form { Size = new Size(800, 600) };
                using var grid = new GridControl
                {
                    Dock = DockStyle.Fill,
                    EnableMasterDetail = true,
                    DetailRowHeight = 150,
                    RowHeight = 35
                };
                form.Controls.Add(grid);
                form.Show();

                var orders = new List<TestOrder>
                {
                    new TestOrder { Id = 1, Code = "ORD-001" },
                    new TestOrder { Id = 2, Code = "ORD-002" },
                    new TestOrder { Id = 3, Code = "ORD-003" }
                };

                grid.SetDataSource(orders);

                bool childDataRequested = false;
                grid.MasterRowGetChildData += (s, e) =>
                {
                    childDataRequested = true;
                    var childGrid = new GridControl
                    {
                        Dock = DockStyle.None,
                        RowHeight = 25
                    };
                    childGrid.SetDataSource(new List<TestOrderItem>
                    {
                        new TestOrderItem { ItemId = 101, Name = "Item A" },
                        new TestOrderItem { ItemId = 102, Name = "Item B" }
                    });
                    e.ChildControl = childGrid;
                };

                // Expand row 0
                grid.ExpandMasterRow(0);

                // Trigger multiple paints to verify no recursive infinite loop
                for (int i = 0; i < 5; i++)
                {
                    grid.Refresh();
                    Application.DoEvents();
                }

                Assert.True(childDataRequested, "MasterRowGetChildData should be invoked when row is expanded.");
                Assert.True(grid.IsMasterRowExpanded(0), "Row 0 should be marked as expanded.");

                // Detail control should be visible and positioned correctly below row 0
                Control? detailCtrl = null;
                foreach (Control c in grid.Controls)
                {
                    if (c is GridControl)
                    {
                        detailCtrl = c;
                        break;
                    }
                }

                Assert.NotNull(detailCtrl);
                Assert.True(detailCtrl!.Visible, "Detail control should be visible.");
                Assert.True(detailCtrl.Height > 0, "Detail control height should be positive.");

                // Now collapse row 0
                grid.CollapseMasterRow(0);
                grid.Refresh();
                Application.DoEvents();

                Assert.False(grid.IsMasterRowExpanded(0), "Row 0 should not be expanded.");
                Assert.False(detailCtrl.Visible, "Detail control should be hidden when collapsed.");

                form.Close();
            });
        }

        [Fact]
        public void MasterDetail_CalculateTotalContentHeight_IncludesExpandedDetailRows()
        {
            StaTestRunner.Run(() =>
            {
                using var grid = new GridControl
                {
                    EnableMasterDetail = true,
                    DetailRowHeight = 160,
                    RowHeight = 40
                };

                var orders = new List<TestOrder>
                {
                    new TestOrder { Id = 1, Code = "ORD-001" },
                    new TestOrder { Id = 2, Code = "ORD-002" },
                    new TestOrder { Id = 3, Code = "ORD-003" },
                    new TestOrder { Id = 4, Code = "ORD-004" }
                };

                grid.SetDataSource(orders);

                // Initial: 4 rows * 40 = 160
                int initialH = grid.CalculateTotalContentHeight();
                Assert.Equal(160, initialH);

                // Expand 2 rows: 160 + (2 * 160) = 480
                grid.ExpandMasterRow(0);
                grid.ExpandMasterRow(1);

                int expandedH = grid.CalculateTotalContentHeight();
                Assert.Equal(480, expandedH);

                // Collapse 1 row: 480 - 160 = 320
                grid.CollapseMasterRow(0);
                int collapsedH = grid.CalculateTotalContentHeight();
                Assert.Equal(320, collapsedH);
            });
        }
    }
}
