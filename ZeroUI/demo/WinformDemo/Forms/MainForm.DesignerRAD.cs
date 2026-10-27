using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;
using ZeroUI.Core.Pivot;
using ZeroUI.Core.Scada;
using ZeroUI.WinForms.Buttons;
using ZeroUI.WinForms.Charts;
using ZeroUI.WinForms.Charts.Model;
using ZeroUI.WinForms.Containers;
using ZeroUI.WinForms.Data;
using ZeroUI.WinForms.DataGrid;
using ZeroUI.WinForms.Design.Editors;
using ZeroUI.WinForms.Design.Forms;
using ZeroUI.WinForms.Industrial;
using ZeroUI.WinForms.Navigation;
using ZeroUI.WinForms.PivotGrid;
using ZeroUI.WinForms.Theme;
using ZeroTabPage = ZeroUI.WinForms.Navigation.ZeroTabPage;

namespace ZeroUI.Samples.WinformDemo.Forms
{
    public sealed partial class MainForm
    {
        private ZGrid _designerGrid = null!;
        private ZTreeList _designerTreeList = null!;
        private ZChart _designerChart = null!;
        private ZRadialGauge _designerGauge = null!;
        private ZPivotGrid _designerPivotGrid = null!;
        private TextBox _txtGeneratedCode = null!;

        private void InitializeDesignerRadDemo(ZeroTabPage targetTab)
        {
            var pnlContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(248, 249, 251),
                AutoScroll = true,
                AutoScrollMinSize = new Size(680, 500)
            };

            // 1. Top Header Card
            var headerCard = new ZeroUI.WinForms.Containers.ZeroCard
            {
                Dock = DockStyle.Top,
                Height = 72,
                Title = "DevExpress-Grade Visual Designers, Smart Tags & RAD Code Generation",
                Subtitle = "Test in-place modal designers, property wizards, SCADA editors, and export instant C# & XAML setup code."
            };
            pnlContainer.Controls.Add(headerCard);

            // 2. RAD Action Bar
            var radActionBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.White,
                Padding = new Padding(8, 8, 8, 8)
            };
            radActionBar.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(226, 232, 240));
                e.Graphics.DrawLine(pen, 0, radActionBar.Height - 1, radActionBar.Width, radActionBar.Height - 1);
            };

            var flowButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(0, 0, 4, 4)
            };

            var btnRunGridDesigner = CreateRadActionButton("⚡ ZGrid Designer", Color.FromArgb(37, 99, 235), () => RunZGridDesigner());
            var btnRunChartWizard = CreateRadActionButton("📊 ZChart Wizard", Color.FromArgb(16, 185, 129), () => RunZChartWizard());
            var btnRunTreeListDesigner = CreateRadActionButton("🌳 ZTreeList Designer", Color.FromArgb(139, 92, 246), () => RunZTreeListDesigner());
            var btnRunPivotChooser = CreateRadActionButton("🔄 ZPivotGrid Field Chooser", Color.FromArgb(245, 158, 11), () => RunZPivotChooser());
            var btnRunGaugeThreshold = CreateRadActionButton("🎛️ Gauge Thresholds", Color.FromArgb(239, 68, 68), () => RunGaugeThresholdEditor());
            var btnRunColorPalette = CreateRadActionButton("🎨 Color Palette Swatches", Color.FromArgb(6, 182, 212), () => RunColorPaletteEditor());
            var btnExportAll = CreateRadActionButton("📋 Export C# Setup Code", Color.FromArgb(71, 85, 105), () => ExportCurrentDesignerCode(isXaml: false));
            var btnExportXaml = CreateRadActionButton("⚡ Export XAML", Color.FromArgb(71, 85, 105), () => ExportCurrentDesignerCode(isXaml: true));

            flowButtons.Controls.Add(btnRunGridDesigner);
            flowButtons.Controls.Add(btnRunChartWizard);
            flowButtons.Controls.Add(btnRunTreeListDesigner);
            flowButtons.Controls.Add(btnRunPivotChooser);
            flowButtons.Controls.Add(btnRunGaugeThreshold);
            flowButtons.Controls.Add(btnRunColorPalette);
            flowButtons.Controls.Add(btnExportAll);
            flowButtons.Controls.Add(btnExportXaml);

            radActionBar.Controls.Add(flowButtons);
            pnlContainer.Controls.Add(radActionBar);

            // 3. Main Workspace with Splitter (Top: Live Preview Tabs, Bottom: Live Code Inspector)
            var splitWorkspace = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                Panel1MinSize = 160,
                Panel2MinSize = 100,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(226, 232, 240)
            };

            pnlContainer.Resize += (s, e) =>
            {
                if (splitWorkspace.Height > 280)
                {
                    try
                    {
                        int targetDist = (int)(splitWorkspace.Height * 0.58);
                        splitWorkspace.SplitterDistance = Math.Max(splitWorkspace.Panel1MinSize, Math.Min(splitWorkspace.Height - splitWorkspace.Panel2MinSize, targetDist));
                    }
                    catch { }
                }
            };

            // Top Area: Live Component Tabs
            var tabsPreview = new ZeroTabControl
            {
                Dock = DockStyle.Fill,
                Orientation = ZeroTabOrientation.Horizontal,
                TabHeight = 34,
                TabStyle = ZeroTabStyle.Pill
            };

            // Tab 1: Live ZGrid
            var tabGrid = new ZeroTabPage("Live ZGrid Preview", "⚡", page =>
            {
                _designerGrid = new ZGrid
                {
                    Dock = DockStyle.Fill,
                    ShowAutoFilterRow = true,
                    ShowGroupPanel = true,
                    ShowFooter = true
                };

                _designerGrid.Columns.Add(new ZeroColumn("ID", 70, CellAlignment.Right) { ColumnType = GridColumnType.Numeric, ReadOnly = true });
                _designerGrid.Columns.Add(new ZeroColumn("Product", 180, CellAlignment.Left) { AllowGrouping = true });
                _designerGrid.Columns.Add(new ZeroColumn("Category", 140, CellAlignment.Left) { AllowGrouping = true });
                _designerGrid.Columns.Add(new ZeroColumn("UnitPrice", 110, CellAlignment.Right) { ColumnType = GridColumnType.Numeric });
                _designerGrid.Columns.Add(new ZeroColumn("StockQty", 90, CellAlignment.Right) { ColumnType = GridColumnType.Numeric });
                _designerGrid.Columns.Add(new ZeroColumn("InStock", 80, CellAlignment.Center) { ColumnType = GridColumnType.Boolean });

                var items = new List<object>();
                for (int i = 1; i <= 200; i++)
                {
                    items.Add(new
                    {
                        ID = i,
                        Product = $"Industrial Unit {i:D3}",
                        Category = (i % 3 == 0) ? "Controllers" : ((i % 3 == 1) ? "Sensors" : "Actuators"),
                        UnitPrice = 120.50m + (i * 3.75m),
                        StockQty = (i * 7) % 500,
                        InStock = (i % 5 != 0)
                    });
                }
                _designerGrid.SetDataSource(items);
                page.Controls.Add(_designerGrid);
            });

            // Tab 2: Live ZChart
            var tabChart = new ZeroTabPage("Live ZChart Preview", "📊", page =>
            {
                _designerChart = new ZChart
                {
                    Dock = DockStyle.Fill,
                    Title = "Factory Telemetry - Live Real-Time Analytics",
                    Subtitle = "Multi-series trend analyzer with dynamic palette support",
                    ShowGridLines = true
                };

                var s1 = _designerChart.AddSeries("Motor Current (A)", Color.FromArgb(0, 210, 255));
                var s2 = _designerChart.AddSeries("Thermal Temperature (°C)", Color.FromArgb(245, 158, 11));
                var s3 = _designerChart.AddSeries("Vibration RMS (mm/s)", Color.FromArgb(16, 185, 129));

                for (int i = 0; i < 24; i++)
                {
                    s1.AddPoint($"{i:D2}:00", 12.0 + Math.Sin(i * 0.5) * 4.0);
                    s2.AddPoint($"{i:D2}:00", 45.0 + Math.Cos(i * 0.4) * 8.0);
                    s3.AddPoint($"{i:D2}:00", 2.5 + Math.Sin(i * 0.8) * 1.5);
                }

                page.Controls.Add(_designerChart);
            });

            // Tab 3: Live ZTreeList
            var tabTreeList = new ZeroTabPage("Live ZTreeList Preview", "🌳", page =>
            {
                _designerTreeList = new ZTreeList
                {
                    Dock = DockStyle.Fill,
                    ShowLines = true,
                    ShowCheckBoxes = true
                };

                _designerTreeList.Columns.Add(new TreeListColumn("TaskName", "Task / Deliverable", 260));
                _designerTreeList.Columns.Add(new TreeListColumn("Owner", "Assignee", 140));
                _designerTreeList.Columns.Add(new TreeListColumn("Progress", "Progress %", 100));
                _designerTreeList.Columns.Add(new TreeListColumn("Status", "Status", 120));

                var root1 = new ZeroUI.WinForms.Data.ZeroTreeNode("Engineering & SCADA Modernization", "⚙️", "85%");
                root1.AddChild("Visual Studio Form Designers", "🛠️", "100%");
                root1.AddChild("Real-Time OPC-UA Tag Streamer", "⚡", "70%");

                var root2 = new ZeroUI.WinForms.Data.ZeroTreeNode("MES Production Orchestration", "🏭", "92%");
                root2.AddChild("Smart Kanban WIP Throttling", "📋", "95%");

                _designerTreeList.AddNode(root1);
                _designerTreeList.AddNode(root2);
                root1.IsExpanded = true;
                root2.IsExpanded = true;

                page.Controls.Add(_designerTreeList);
            });

            // Tab 4: Live ZPivotGrid
            var tabPivot = new ZeroTabPage("Live ZPivotGrid Preview", "🔄", page =>
            {
                _designerPivotGrid = new ZPivotGrid
                {
                    Dock = DockStyle.Fill,
                    RowHeaderWidth = 160,
                    CellWidth = 115,
                    CellHeight = 28
                };

                _designerPivotGrid.Fields.Add(new PivotGridField("Region", PivotArea.RowArea, "Region"));
                _designerPivotGrid.Fields.Add(new PivotGridField("Category", PivotArea.RowArea, "Product Category"));
                _designerPivotGrid.Fields.Add(new PivotGridField("Year", PivotArea.ColumnArea, "Year"));
                _designerPivotGrid.Fields.Add(new PivotGridField("Revenue", PivotArea.DataArea, "Total Revenue ($)", PivotSummaryType.Sum));

                var salesData = new List<object>();
                string[] regions = { "North America", "Europe", "Asia-Pacific" };
                string[] categories = { "PLC Modules", "HMI Panels", "Industrial Drives", "Safety Relays" };
                int[] years = { 2024, 2025, 2026 };

                var rnd = new Random(42);
                for (int i = 0; i < 300; i++)
                {
                    salesData.Add(new
                    {
                        Region = regions[rnd.Next(regions.Length)],
                        Category = categories[rnd.Next(categories.Length)],
                        Year = years[rnd.Next(years.Length)],
                        Revenue = (decimal)(rnd.Next(50, 800) * 100)
                    });
                }
                _designerPivotGrid.DataSource = salesData;
                page.Controls.Add(_designerPivotGrid);
            });

            // Tab 5: Live SCADA Radial Gauge
            var tabGauge = new ZeroTabPage("Live SCADA Gauge Preview", "🎛️", page =>
            {
                var pnlGaugeContainer = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(20, 24, 34) };

                _designerGauge = new ZRadialGauge
                {
                    Dock = DockStyle.Fill,
                    Minimum = 0,
                    Maximum = 160,
                    Value = 92.4,
                    Unit = "PSI",
                    Title = "Primary Boiler Steam Pressure"
                };

                _designerGauge.Thresholds.Add(new GaugeThresholdRange(0, 100, GaugeSeverity.Normal, (uint)Color.FromArgb(16, 185, 129).ToArgb(), "Normal Operation"));
                _designerGauge.Thresholds.Add(new GaugeThresholdRange(100, 135, GaugeSeverity.Warning, (uint)Color.FromArgb(245, 158, 11).ToArgb(), "Warning Alert"));
                _designerGauge.Thresholds.Add(new GaugeThresholdRange(135, 160, GaugeSeverity.Critical, (uint)Color.FromArgb(239, 68, 68).ToArgb(), "Critical Safety Trip"));

                pnlGaugeContainer.Controls.Add(_designerGauge);
                page.Controls.Add(pnlGaugeContainer);
            });

            tabsPreview.AddTab(tabGrid);
            tabsPreview.AddTab(tabChart);
            tabsPreview.AddTab(tabTreeList);
            tabsPreview.AddTab(tabPivot);
            tabsPreview.AddTab(tabGauge);

            splitWorkspace.Panel1.Controls.Add(tabsPreview);

            // Bottom Area: Live Code Inspector
            var pnlBottomCode = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), BackColor = Color.White };
            var lblCodeHeader = new Label
            {
                Dock = DockStyle.Top,
                Height = 26,
                Text = "⚡ Instant Setup Code Generator (Synchronized with Designer Changes)",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _txtGeneratedCode = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                ReadOnly = true,
                Font = new Font("Consolas", 9.5f),
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = Color.FromArgb(15, 23, 42),
                Text = "// Select any designer wizard above to customize components and inspect live generated setup code."
            };

            pnlBottomCode.Controls.Add(_txtGeneratedCode);
            pnlBottomCode.Controls.Add(lblCodeHeader);
            splitWorkspace.Panel2.Controls.Add(pnlBottomCode);

            pnlContainer.Controls.Add(splitWorkspace);
            targetTab.Controls.Add(pnlContainer);

            // Generate initial code snippet
            UpdateGeneratedCodeSnippet("ZGrid", _designerGrid);
        }

        private Button CreateRadActionButton(string text, Color accentColor, Action onClick)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 34,
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = accentColor,
                ForeColor = Color.White,
                Margin = new Padding(3, 2, 6, 2),
                Padding = new Padding(8, 0, 8, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private void RunZGridDesigner()
        {
            if (_designerGrid == null) return;
            using var designerForm = new GridDesignerForm(_designerGrid);
            if (designerForm.ShowDialog(this) == DialogResult.OK)
            {
                _designerGrid.Invalidate();
                UpdateGeneratedCodeSnippet("ZGrid", _designerGrid);
            }
        }

        private void RunZChartWizard()
        {
            if (_designerChart == null) return;
            using var wizardForm = new ChartWizardForm(_designerChart);
            if (wizardForm.ShowDialog(this) == DialogResult.OK)
            {
                _designerChart.Invalidate();
                UpdateGeneratedCodeSnippet("ZChart", _designerChart);
            }
        }

        private void RunZTreeListDesigner()
        {
            if (_designerTreeList == null) return;
            using var treeForm = new TreeListDesignerForm(_designerTreeList);
            if (treeForm.ShowDialog(this) == DialogResult.OK)
            {
                _designerTreeList.Invalidate();
                UpdateGeneratedCodeSnippet("ZTreeList", _designerTreeList);
            }
        }

        private void RunZPivotChooser()
        {
            if (_designerPivotGrid == null) return;
            _designerPivotGrid.ShowFieldList();
            UpdateGeneratedCodeSnippet("ZPivotGrid", _designerPivotGrid);
        }

        private void RunGaugeThresholdEditor()
        {
            if (_designerGauge == null) return;
            using var dlg = new GaugeThresholdEditor.GaugeThresholdEditorDialog(_designerGauge.Thresholds);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _designerGauge.Thresholds.Clear();
                _designerGauge.Thresholds.AddRange(dlg.GetRanges());
                _designerGauge.Invalidate();
                UpdateGeneratedCodeSnippet("ZRadialGauge", _designerGauge);
            }
        }

        private void RunColorPaletteEditor()
        {
            using var dlg = new ZeroColorPaletteDialog(Color.FromArgb(37, 99, 235));
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                MessageBox.Show(this, $"Selected Color: #{dlg.SelectedColor.R:X2}{dlg.SelectedColor.G:X2}{dlg.SelectedColor.B:X2}", "Color Palette Result", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ExportCurrentDesignerCode(bool isXaml)
        {
            string title = isXaml ? "ZeroUI - XAML Control Markup Export" : "ZeroUI - C# Setup Code Export";
            string lang = isXaml ? "XAML" : "C#";
            string code;

            if (_designerGrid != null)
            {
                code = isXaml ? CodeExportForm.GenerateXamlCode("ZGrid", _designerGrid) : CodeExportForm.GenerateCSharpCode("ZGrid", _designerGrid);
            }
            else
            {
                code = "// No active control instance available.";
            }

            using var exportDlg = new CodeExportForm(title, lang, code);
            exportDlg.ShowDialog(this);
        }

        private void UpdateGeneratedCodeSnippet(string controlName, object controlInstance)
        {
            if (_txtGeneratedCode == null || controlInstance == null) return;
            string csharpCode = CodeExportForm.GenerateCSharpCode(controlName, controlInstance);
            _txtGeneratedCode.Text = csharpCode;
        }
    }
}
