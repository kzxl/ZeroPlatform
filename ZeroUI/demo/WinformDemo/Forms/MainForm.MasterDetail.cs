using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;
using ZeroUI.WinForms.Containers;
using ZeroUI.WinForms.DataGrid;
using ZeroUI.WinForms.DataGrid.Repositories;
using ZeroUI.WinForms.Navigation;
using ZeroUI.WinForms.Overlays;
using ZeroUI.WinForms.Theme;
using ZeroTabPage = ZeroUI.WinForms.Navigation.ZeroTabPage;

namespace ZeroUI.Samples.WinformDemo.Forms
{
    public sealed partial class MainForm
    {
        public class DemoOrder
        {
            public int OrderId { get; set; }
            public string OrderCode { get; set; } = string.Empty;
            public string CustomerName { get; set; } = string.Empty;
            public string OrderDate { get; set; } = string.Empty;
            public decimal TotalAmount { get; set; }
            public string Status { get; set; } = "Completed";
        }

        public class DemoOrderItem
        {
            public int ItemId { get; set; }
            public string ProductCode { get; set; } = string.Empty;
            public string ProductName { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal LineTotal => Quantity * UnitPrice;
            public string Warehouse { get; set; } = "WH-Main";
        }

        public class DemoProductLookup
        {
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public decimal Price { get; set; }
        }

        private void InitializeMasterDetailDemo(ZeroTabPage targetTab)
        {
            var pnlContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

            // Top Summary Card
            var headerCard = new ZeroCard
            {
                Dock = DockStyle.Top,
                Height = 85,
                Title = "DevExpress Parity: Hierarchical Master-Detail & In-Place Multi-Column Lookups",
                Subtitle = "Deep row nesting with [+] / [-] indicators, on-demand child grid binding, and in-place GridLookupEdit dropdowns."
            };

            var actionsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight
            };

            var btnExpandAll = new Button
            {
                Text = "➕ Expand All",
                AutoSize = true,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = ZeroTheme.Colors.Primary,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnExpandAll.FlatAppearance.BorderSize = 0;

            var btnCollapseAll = new Button
            {
                Text = "➖ Collapse All",
                AutoSize = true,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = ZeroTheme.Colors.Surface,
                ForeColor = ZeroTheme.Colors.TextPrimary,
                Cursor = Cursors.Hand
            };
            btnCollapseAll.FlatAppearance.BorderSize = 0;

            var btnGetFocused = new Button
            {
                Text = "🎯 Get Focused Master Row (DevExpress Parity)",
                AutoSize = true,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = ZeroTheme.Colors.Surface,
                ForeColor = ZeroTheme.Colors.TextPrimary,
                Cursor = Cursors.Hand
            };
            btnGetFocused.FlatAppearance.BorderSize = 0;

            actionsPanel.Controls.Add(btnExpandAll);
            actionsPanel.Controls.Add(btnCollapseAll);
            actionsPanel.Controls.Add(btnGetFocused);
            headerCard.Controls.Add(actionsPanel);

            // Mock Data
            var orders = new List<DemoOrder>
            {
                new DemoOrder { OrderId = 1001, OrderCode = "ORD-2026-001", CustomerName = "Samsung Electronics VN", OrderDate = "2026-09-10", TotalAmount = 45200m, Status = "Completed" },
                new DemoOrder { OrderId = 1002, OrderCode = "ORD-2026-002", CustomerName = "Foxconn Precision Co.", OrderDate = "2026-09-11", TotalAmount = 89400m, Status = "Processing" },
                new DemoOrder { OrderId = 1003, OrderCode = "ORD-2026-003", CustomerName = "LG Display Haiphong", OrderDate = "2026-09-12", TotalAmount = 31000m, Status = "Pending" },
                new DemoOrder { OrderId = 1004, OrderCode = "ORD-2026-004", CustomerName = "VinFast Manufacturing", OrderDate = "2026-09-13", TotalAmount = 125000m, Status = "Processing" },
                new DemoOrder { OrderId = 1005, OrderCode = "ORD-2026-005", CustomerName = "Intel Products Vietnam", OrderDate = "2026-09-14", TotalAmount = 67800m, Status = "Completed" }
            };

            var orderItemsMap = new Dictionary<int, List<DemoOrderItem>>
            {
                [1001] = new List<DemoOrderItem>
                {
                    new DemoOrderItem { ItemId = 1, ProductCode = "MCU-STM32H7", ProductName = "ARM Cortex-M7 Microcontroller 480MHz", Quantity = 250, UnitPrice = 120m, Warehouse = "WH-HCM-A" },
                    new DemoOrderItem { ItemId = 2, ProductCode = "PWR-TPS5430", ProductName = "Step-Down DC/DC Converter 3A", Quantity = 500, UnitPrice = 30.4m, Warehouse = "WH-HCM-B" }
                },
                [1002] = new List<DemoOrderItem>
                {
                    new DemoOrderItem { ItemId = 3, ProductCode = "OPT-OPTO22", ProductName = "Solid State Relay Module 240VAC", Quantity = 120, UnitPrice = 450m, Warehouse = "WH-HN-01" },
                    new DemoOrderItem { ItemId = 4, ProductCode = "CAP-TANT-100", ProductName = "Tantalum Capacitor 100uF 25V SMD", Quantity = 1200, UnitPrice = 29.5m, Warehouse = "WH-HN-02" }
                },
                [1003] = new List<DemoOrderItem>
                {
                    new DemoOrderItem { ItemId = 5, ProductCode = "DSP-OLED-128", ProductName = "0.96 inch SPI Graphic OLED Display", Quantity = 400, UnitPrice = 77.5m, Warehouse = "WH-HP-01" }
                },
                [1004] = new List<DemoOrderItem>
                {
                    new DemoOrderItem { ItemId = 6, ProductCode = "SEN-HALL-V2", ProductName = "Automotive Hall Effect Current Sensor", Quantity = 800, UnitPrice = 110m, Warehouse = "WH-HP-AUTO" },
                    new DemoOrderItem { ItemId = 7, ProductCode = "MOSFET-SIC-650", ProductName = "Silicon Carbide Power MOSFET 650V 30A", Quantity = 350, UnitPrice = 105.7m, Warehouse = "WH-HP-AUTO" }
                },
                [1005] = new List<DemoOrderItem>
                {
                    new DemoOrderItem { ItemId = 8, ProductCode = "IC-FPGA-ARTIX", ProductName = "Xilinx Artix-7 FPGA 100T BGA-484", Quantity = 100, UnitPrice = 678m, Warehouse = "WH-SGN-HIGH" }
                }
            };

            var productCatalog = new List<DemoProductLookup>
            {
                new DemoProductLookup { Code = "MCU-STM32H7", Name = "ARM Cortex-M7 480MHz", Category = "Semiconductor", Price = 120m },
                new DemoProductLookup { Code = "PWR-TPS5430", Name = "Step-Down DC/DC Converter 3A", Category = "Power", Price = 30.4m },
                new DemoProductLookup { Code = "OPT-OPTO22", Name = "Solid State Relay Module 240VAC", Category = "Industrial Relay", Price = 450m },
                new DemoProductLookup { Code = "SEN-HALL-V2", Name = "Automotive Hall Effect Current Sensor", Category = "Sensors", Price = 110m },
                new DemoProductLookup { Code = "MOSFET-SIC-650", Name = "Silicon Carbide Power MOSFET 650V", Category = "Power", Price = 105.7m }
            };

            // Master Grid Setup
            var masterGrid = new GridControl
            {
                Dock = DockStyle.Fill,
                RowHeight = 38,
                HeaderHeight = 36,
                EnableMasterDetail = true,
                DetailRowHeight = 175
            };

            masterGrid.Columns.Add(new ZeroColumn(nameof(DemoOrder.OrderId), "Order #", 90, CellAlignment.Center));
            masterGrid.Columns.Add(new ZeroColumn(nameof(DemoOrder.OrderCode), "Order Code", 140, CellAlignment.Left));
            masterGrid.Columns.Add(new ZeroColumn(nameof(DemoOrder.CustomerName), "Customer", 220, CellAlignment.Left));
            masterGrid.Columns.Add(new ZeroColumn(nameof(DemoOrder.OrderDate), "Date", 110, CellAlignment.Center));
            masterGrid.Columns.Add(new ZeroColumn(nameof(DemoOrder.TotalAmount), "Total Amount ($)", 140, CellAlignment.Right));
            masterGrid.Columns.Add(new ZeroColumn(nameof(DemoOrder.Status), "Status", 120, CellAlignment.Center));

            // Set Data
            masterGrid.SetDataSource(orders);

            // Master-Detail Data Provider Event (DevExpress Parity: MasterRowGetChildList)
            masterGrid.MasterRowGetChildData += (s, e) =>
            {
                if (e.MasterRowData is DemoOrder order && orderItemsMap.TryGetValue(order.OrderId, out var items))
                {
                    var childGrid = new GridControl
                    {
                        Dock = DockStyle.None,
                        RowHeight = 30,
                        HeaderHeight = 28,
                        Font = new Font("Segoe UI", 9f)
                    };

                    childGrid.Columns.Add(new ZeroColumn(nameof(DemoOrderItem.ProductCode), "Product Code", 130, CellAlignment.Left));
                    childGrid.Columns.Add(new ZeroColumn(nameof(DemoOrderItem.ProductName), "Item Description", 280, CellAlignment.Left));
                    childGrid.Columns.Add(new ZeroColumn(nameof(DemoOrderItem.Quantity), "Qty", 80, CellAlignment.Right));
                    childGrid.Columns.Add(new ZeroColumn(nameof(DemoOrderItem.UnitPrice), "Unit Price ($)", 110, CellAlignment.Right));
                    childGrid.Columns.Add(new ZeroColumn(nameof(DemoOrderItem.LineTotal), "Subtotal ($)", 120, CellAlignment.Right));
                    childGrid.Columns.Add(new ZeroColumn(nameof(DemoOrderItem.Warehouse), "Source WH", 110, CellAlignment.Center));

                    // Embed in-place GridLookupEdit inside Child Grid's ProductCode column!
                    var repoGle = new RepositoryItemGridLookupEdit
                    {
                        DisplayMember = nameof(DemoProductLookup.Code),
                        ValueMember = nameof(DemoProductLookup.Code),
                        Placeholder = "Search SKU..."
                    };
                    repoGle.Columns.Add(new ZeroColumn(nameof(DemoProductLookup.Code), "SKU Code", 110));
                    repoGle.Columns.Add(new ZeroColumn(nameof(DemoProductLookup.Name), "Product Name", 220));
                    repoGle.Columns.Add(new ZeroColumn(nameof(DemoProductLookup.Category), "Category", 110));
                    repoGle.Columns.Add(new ZeroColumn(nameof(DemoProductLookup.Price), "Unit Price", 90, CellAlignment.Right));
                    repoGle.SetDataSource(productCatalog);

                    childGrid.Columns[0].ColumnEdit = repoGle;

                    childGrid.SetDataSource(items);
                    e.ChildControl = childGrid;
                }
            };

            // Actions wiring
            btnExpandAll.Click += (s, e) =>
            {
                for (int i = 0; i < masterGrid.VisualRowCount; i++)
                {
                    masterGrid.ExpandMasterRow(i);
                }
            };

            btnCollapseAll.Click += (s, e) =>
            {
                for (int i = 0; i < masterGrid.VisualRowCount; i++)
                {
                    masterGrid.CollapseMasterRow(i);
                }
            };

            btnGetFocused.Click += (s, e) =>
            {
                var focusedOrder = masterGrid.GetFocusedRow<DemoOrder>();
                if (focusedOrder != null)
                {
                    ZeroToast.Success(this, $"Focused Order: [{focusedOrder.OrderCode}] - {focusedOrder.CustomerName} (${focusedOrder.TotalAmount:N0})");
                }
                else
                {
                    ZeroToast.Info(this, "Please select an Order row in the Master Grid first.");
                }
            };

            pnlContainer.Controls.Add(masterGrid);
            pnlContainer.Controls.Add(headerCard);

            targetTab.Controls.Add(pnlContainer);
        }
    }
}
