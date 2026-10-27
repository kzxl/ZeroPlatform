using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.Samples.WinformDemo.Forms
{
    public sealed class ShowcaseFeatureItem
    {
        public string Key { get; }
        public string Title { get; }
        public string Category { get; }
        public string Icon { get; }
        public string Badge { get; }
        public Color BadgeColor { get; }
        public string Description { get; }
        public string CodeSnippet { get; }

        public ShowcaseFeatureItem(string key, string title, string category, string icon, string badge, Color badgeColor, string description, string codeSnippet)
        {
            Key = key;
            Title = title;
            Category = category;
            Icon = icon;
            Badge = badge;
            BadgeColor = badgeColor;
            Description = description;
            CodeSnippet = codeSnippet;
        }
    }

    public sealed class ShowcaseFeatureExplorer : BaseUserControl, IZeroDpiScalable
    {
        private readonly TextBox _txtSearch;
        private readonly Panel _searchContainer;
        private readonly Panel _itemsPanel;
        private readonly List<ShowcaseFeatureItem> _allItems = new List<ShowcaseFeatureItem>();
        private readonly List<Control> _renderedRows = new List<Control>();
        private readonly ToolTip _toolTip;
        private string _selectedKey = string.Empty;

        private float _currentDpiScale = 1.0f;
        private int _baseWidth = 340;
        private int _baseSearchHeight = 44;

        [Browsable(false)]
        public float DpiScale => _currentDpiScale;

        public void ApplyDpiScaling(float scaleFactor)
        {
            if (scaleFactor <= 0f) scaleFactor = 1.0f;
            _currentDpiScale = scaleFactor;

            Width = (int)Math.Round(_baseWidth * scaleFactor);
            _searchContainer.Height = (int)Math.Round(_baseSearchHeight * scaleFactor);
            _txtSearch.Font = new Font("Segoe UI", 9.5f * scaleFactor, FontStyle.Regular);
            RenderItems(_allItems);
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            float factor = ZeroDpi.GetScaleFactor(this);
            if (Math.Abs(factor - _currentDpiScale) > 0.001f)
            {
                ApplyDpiScaling(factor);
            }
        }

        public event Action<ShowcaseFeatureItem>? FeatureSelected;

        public ShowcaseFeatureExplorer()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Left;
            Width = 340;
            BackColor = Color.FromArgb(248, 249, 251);
            Padding = new Padding(0);

            _toolTip = new ToolTip
            {
                InitialDelay = 350,
                ReshowDelay = 100,
                AutoPopDelay = 6000
            };

            // 1. Search Container
            _searchContainer = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.FromArgb(240, 242, 245),
                Padding = new Padding(10, 8, 10, 8)
            };

            _txtSearch = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(40, 44, 52)
            };
            _txtSearch.Text = "Type keywords here...";
            _txtSearch.ForeColor = Color.Gray;

            _txtSearch.GotFocus += (s, e) =>
            {
                if (_txtSearch.Text == "Type keywords here...")
                {
                    _txtSearch.Text = "";
                    _txtSearch.ForeColor = ZeroTheme.Colors.TextPrimary;
                }
            };

            _txtSearch.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtSearch.Text))
                {
                    _txtSearch.Text = "Type keywords here...";
                    _txtSearch.ForeColor = Color.Gray;
                }
            };

            _txtSearch.TextChanged += (s, e) =>
            {
                if (_txtSearch.Text != "Type keywords here...")
                {
                    FilterItems(_txtSearch.Text.Trim());
                }
            };

            var searchBoxWrapper = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(8, 4, 8, 4)
            };
            searchBoxWrapper.Paint += (s, e) =>
            {
                using var pen = new Pen(Color.FromArgb(218, 222, 229));
                e.Graphics.DrawRectangle(pen, 0, 0, searchBoxWrapper.Width - 1, searchBoxWrapper.Height - 1);
            };
            searchBoxWrapper.Controls.Add(_txtSearch);
            _searchContainer.Controls.Add(searchBoxWrapper);

            // 2. Scrollable Items Panel
            _itemsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(248, 249, 251),
                Padding = new Padding(0, 4, 0, 8)
            };

            Controls.Add(_itemsPanel);
            Controls.Add(_searchContainer);

            PopulateDefaultFeatures();
            RenderItems(_allItems);
        }

        public string SelectedKey => _selectedKey;

        public void SelectFeature(string key)
        {
            // Normalize legacy / sub-feature alias keys to their parent subsystems
            switch (key?.ToLowerInvariant())
            {
                case "feat_components":
                case "feat_editors_core":
                case "feat_commercial_suite":
                case "feat_commercial_query":
                case "feat_commercial_editors":
                case "feat_commercial_wizard":
                case "feat_office_docs":
                case "feat_office_pdf":
                case "feat_office_spreadsheet":
                case "feat_tree_list":
                case "feat_nav_ribbon":
                case "feat_master_detail":
                case "feat_pivot":
                    key = "feat_components";
                    break;

                case "feat_datagrid":
                case "feat_virtual_10m":
                case "feat_autofilter":
                case "feat_grouping_summaries":
                case "feat_banded_headers":
                case "feat_standard_dgv":
                    key = "feat_datagrid";
                    break;

                case "feat_mes":
                case "feat_mes_dashboard":
                case "feat_workflow_kanban":
                    key = "feat_mes";
                    break;

                case "feat_warehouse":
                case "feat_barcode_station":
                case "feat_warehouse_lot":
                case "feat_warehouse_racks":
                    key = "feat_warehouse";
                    break;

                case "feat_scada":
                case "feat_scada_synoptic":
                case "feat_scada_pid":
                case "feat_scada_alarms":
                case "feat_scada_tags":
                case "feat_scada_gauges":
                case "feat_scada_interlock":
                    key = "feat_scada";
                    break;

                case "feat_network":
                case "feat_net_rack":
                case "feat_net_topology":
                case "feat_net_chassis":
                case "feat_net_ipam":
                case "feat_net_fieldbus":
                    key = "feat_network";
                    break;

                case "feat_industrial":
                case "feat_workflow_gantt":
                case "feat_vert_energy":
                case "feat_vert_petrochem":
                case "feat_vert_pharma":
                case "feat_vert_water":
                case "feat_vert_bms":
                case "feat_vert_life_sciences":
                case "feat_vert_robotics":
                    key = "feat_industrial";
                    break;

                case "feat_analytics":
                case "feat_analytics_charts":
                case "feat_analytics_spc":
                    key = "feat_analytics";
                    break;
            }

            var item = _allItems.FirstOrDefault(i => i.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                _selectedKey = item.Key;
                HighlightSelectedRow();
                FeatureSelected?.Invoke(item);
            }
        }

        public void SelectNext()
        {
            int index = _allItems.FindIndex(i => i.Key == _selectedKey);
            if (index >= 0 && index < _allItems.Count - 1)
            {
                SelectFeature(_allItems[index + 1].Key);
            }
        }

        public void SelectPrevious()
        {
            int index = _allItems.FindIndex(i => i.Key == _selectedKey);
            if (index > 0)
            {
                SelectFeature(_allItems[index - 1].Key);
            }
        }

        private void FilterItems(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                RenderItems(_allItems);
            }
            else
            {
                var filtered = _allItems.Where(i =>
                    i.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    i.Category.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    i.Badge.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    i.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                RenderItems(filtered);
            }
        }

        private void RenderItems(List<ShowcaseFeatureItem> items)
        {
            _itemsPanel.SuspendLayout();
            _itemsPanel.Controls.Clear();
            _renderedRows.Clear();

            var groups = items.GroupBy(i => i.Category).ToList();

            foreach (var group in groups)
            {
                // Category Header
                var lblHeader = new Label
                {
                    Dock = DockStyle.Top,
                    Height = (int)Math.Round(28 * _currentDpiScale),
                    Text = group.Key.ToUpperInvariant(),
                    Font = new Font("Segoe UI", 8.25f * _currentDpiScale, FontStyle.Bold),
                    ForeColor = Color.FromArgb(120, 130, 145),
                    TextAlign = ContentAlignment.BottomLeft,
                    Padding = new Padding((int)Math.Round(12 * _currentDpiScale), 0, 0, (int)Math.Round(4 * _currentDpiScale))
                };
                _itemsPanel.Controls.Add(lblHeader);

                foreach (var item in group)
                {
                    var row = CreateItemRow(item);
                    _itemsPanel.Controls.Add(row);
                    _renderedRows.Add(row);
                }
            }

            // Reverse order for WinForms DockStyle.Top stacking
            var controls = _itemsPanel.Controls.Cast<Control>().ToList();
            _itemsPanel.Controls.Clear();
            for (int i = controls.Count - 1; i >= 0; i--)
            {
                _itemsPanel.Controls.Add(controls[i]);
            }

            _itemsPanel.ResumeLayout(true);
            HighlightSelectedRow();
        }

        private Control CreateItemRow(ShowcaseFeatureItem item)
        {
            var row = new Panel
            {
                Dock = DockStyle.Top,
                Height = (int)Math.Round(34 * _currentDpiScale),
                Tag = item,
                Cursor = Cursors.Hand,
                Padding = new Padding((int)Math.Round(10 * _currentDpiScale), 0, (int)Math.Round(6 * _currentDpiScale), 0)
            };

            bool isSelected = item.Key == _selectedKey;
            row.BackColor = isSelected ? Color.FromArgb(228, 236, 252) : Color.Transparent;

            var lblTitle = new Label
            {
                Dock = DockStyle.Fill,
                Text = $"{item.Icon}  {item.Title}",
                Font = new Font("Segoe UI", 9.25f * _currentDpiScale, isSelected ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = isSelected ? Color.FromArgb(18, 86, 209) : Color.FromArgb(33, 37, 41),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            Panel? pnlBadge = null;
            if (!string.IsNullOrEmpty(item.Badge))
            {
                var badgeFont = new Font("Segoe UI", 7.25f * _currentDpiScale, FontStyle.Bold);
                Size textSize = TextRenderer.MeasureText(item.Badge, badgeFont, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                int hPad = (int)Math.Round(10 * _currentDpiScale);
                int pillW = Math.Max((int)Math.Round(36 * _currentDpiScale), textSize.Width + hPad);
                int containerW = pillW + (int)Math.Round(6 * _currentDpiScale);

                pnlBadge = new Panel
                {
                    Dock = DockStyle.Right,
                    Width = containerW,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };

                pnlBadge.Paint += (s, pe) =>
                {
                    var g = pe.Graphics;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                    int pillH = (int)Math.Round(18 * _currentDpiScale);
                    int pillY = (pnlBadge.Height - pillH) / 2;
                    int pillX = pnlBadge.Width - pillW - (int)Math.Round(2 * _currentDpiScale);
                    var pillRect = new Rectangle(pillX, pillY, pillW, pillH);

                    // 1. Soft pill background
                    int radius = (int)Math.Round(4 * _currentDpiScale);
                    using var path = CreateRoundedRectanglePath(pillRect, radius);
                    using var bgBrush = new SolidBrush(Color.FromArgb(28, item.BadgeColor));
                    g.FillPath(bgBrush, path);

                    // 2. Subtle outline border
                    using var borderPen = new Pen(Color.FromArgb(60, item.BadgeColor), 1f);
                    g.DrawPath(borderPen, path);

                    // 3. Centered, single-line text (guaranteed never to wrap)
                    TextRenderer.DrawText(
                        g,
                        item.Badge,
                        badgeFont,
                        pillRect,
                        item.BadgeColor,
                        TextFormatFlags.SingleLine | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                };

                row.Controls.Add(pnlBadge);
            }

            row.Controls.Add(lblTitle);

            Action selectAction = () =>
            {
                _selectedKey = item.Key;
                HighlightSelectedRow();
                FeatureSelected?.Invoke(item);
            };

            row.Click += (s, e) => selectAction();
            lblTitle.Click += (s, e) => selectAction();
            if (pnlBadge != null) pnlBadge.Click += (s, e) => selectAction();

            void OnHover()
            {
                if (item.Key != _selectedKey)
                    row.BackColor = Color.FromArgb(240, 243, 248);
            }
            void OnLeave()
            {
                if (item.Key != _selectedKey)
                {
                    Point pt = row.PointToClient(Cursor.Position);
                    if (!row.ClientRectangle.Contains(pt))
                    {
                        row.BackColor = Color.Transparent;
                    }
                }
            }

            row.MouseEnter += (s, e) => OnHover();
            lblTitle.MouseEnter += (s, e) => OnHover();
            row.MouseLeave += (s, e) => OnLeave();
            lblTitle.MouseLeave += (s, e) => OnLeave();
            if (pnlBadge != null)
            {
                pnlBadge.MouseEnter += (s, e) => OnHover();
                pnlBadge.MouseLeave += (s, e) => OnLeave();
            }

            string tipText = $"{item.Title}\n[{item.Category}] • {item.Badge}\n\n{item.Description}";
            _toolTip.SetToolTip(row, tipText);
            _toolTip.SetToolTip(lblTitle, tipText);
            if (pnlBadge != null) _toolTip.SetToolTip(pnlBadge, tipText);

            return row;
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            if (rect.Width <= 0 || rect.Height <= 0) return path;

            int diameter = radius * 2;
            if (diameter > rect.Width) diameter = rect.Width;
            if (diameter > rect.Height) diameter = rect.Height;

            var arc = new Rectangle(rect.X, rect.Y, diameter, diameter);

            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void HighlightSelectedRow()
        {
            foreach (var row in _renderedRows)
            {
                if (row.Tag is ShowcaseFeatureItem item)
                {
                    bool isSelected = item.Key == _selectedKey;
                    row.BackColor = isSelected ? Color.FromArgb(228, 236, 252) : Color.Transparent;
                    foreach (Control child in row.Controls)
                    {
                        if (child is Label lbl && child.Dock == DockStyle.Fill)
                        {
                            lbl.Font = new Font("Segoe UI", 9.25f * _currentDpiScale, isSelected ? FontStyle.Bold : FontStyle.Regular);
                            lbl.ForeColor = isSelected ? Color.FromArgb(18, 86, 209) : Color.FromArgb(33, 37, 41);
                        }
                    }
                }
            }
        }

        public void ApplyTheme(ZeroSkin skin)
        {
            bool isDark = skin.IsDark;
            BackColor = isDark ? Color.FromArgb(28, 30, 42) : Color.FromArgb(248, 249, 251);
            _searchContainer.BackColor = isDark ? Color.FromArgb(22, 24, 34) : Color.FromArgb(240, 242, 245);
            _itemsPanel.BackColor = BackColor;
            _txtSearch.BackColor = isDark ? Color.FromArgb(36, 38, 52) : Color.White;
            _txtSearch.ForeColor = isDark ? Color.FromArgb(220, 224, 235) : Color.FromArgb(40, 44, 52);
            HighlightSelectedRow();
        }

        private void PopulateDefaultFeatures()
        {
            _allItems.Clear();

            // 1. UI & DATA
            _allItems.Add(new ShowcaseFeatureItem(
                "feat_design_rad", "Visual Designers & RAD Suite", "UI & Data", "🛠️", "RAD SUITE", Color.FromArgb(14, 165, 233),
                "Enterprise visual design tools: Smart Tag Action Lists, In-Place Grid/TreeList/Chart Wizards, Modal Threshold/Palette UITypeEditors, and 1-Click C#/XAML Code Generation.",
                @"// Launch Visual Studio In-Place Designers & Wizards
using var gridDesigner = new GridDesignerForm(myZGrid);
gridDesigner.ShowDialog();

using var chartWizard = new ChartWizardForm(myZChart);
chartWizard.ShowDialog();

using var treeDesigner = new TreeListDesignerForm(myZTreeList);
treeDesigner.ShowDialog();"
            ));

            _allItems.Add(new ShowcaseFeatureItem(
                "feat_components", "UI Components & Editors", "UI & Data", "🎨", "CATALOG", Color.FromArgb(59, 130, 246),
                "Comprehensive control suite: ButtonEdit, CalcEdit, ColorPick, Rating, RangeSlider, PictureEdit, TreeList, PropertyGrid, PivotGrid, FilterControl, Wizard, etc.",
                @"// ZeroUI Components & Editors Suite
var btnEdit = new ZButtonEdit();
var calc = new ZCalcEdit { Value = 18450.75m };
var rating = new ZRatingControl { Value = 4.5m };
var propGrid = new PropertyGridControl();
var pivotGrid = new ZPivotGrid();"
            ));

            _allItems.Add(new ShowcaseFeatureItem(
                "feat_datagrid", "DataGrid & Benchmarks", "UI & Data", "⚡", "10M ROWS", Color.FromArgb(16, 185, 129),
                "Zero-allocation virtual data grid smoothly scrolling up to 10,000,000 rows with Auto-filter, Grouping, Summaries, Banded Headers, and standard DataGridView comparison.",
                @"// Initialize High-Volume Virtual DataGrid
var grid = new ZeroGridControl();
grid.VirtualMode = true;
grid.RowCount = 10_000_000;
grid.ShowAutoFilterRow = true;
grid.ShowFindPanel = true;
grid.ShowGroupPanel = true;"
            ));

            _allItems.Add(new ShowcaseFeatureItem(
                "feat_analytics", "Analytics & Trends", "UI & Data", "📊", "ANALYTICS", Color.FromArgb(99, 102, 241),
                "100kHz real-time waveform charts, multi-axis trend graphs, and Six Sigma SPC statistical process control (UCL, CL, LCL).",
                @"// Real-Time Waveforms & SPC Charts
var chart = new ZTrendChart();
chart.AddSeries(""Pressure"", SeriesType.FastLine);
var spc = new ZSpcChart();
spc.SetLimits(ucl: 15.2, cl: 14.0, lcl: 12.8);"
            ));

            // 2. OPERATIONS & LOGISTICS
            _allItems.Add(new ShowcaseFeatureItem(
                "feat_mes", "MES & Smart Factory", "Operations & Logistics", "🏭", "OPERATIONS", Color.FromArgb(245, 158, 11),
                "Real-time manufacturing execution: MES Telemetry HUD, OEE 88.4%, Takt Time Pacing, Smart Kanban Board with WIP limits.",
                @"// MES Operations & Kanban Board
var mesHud = new MesTelemetryHud();
mesHud.UpdateOee(availability: 0.94, performance: 0.96, quality: 0.98);
var kanban = new ZKanbanBoard();"
            ));

            _allItems.Add(new ShowcaseFeatureItem(
                "feat_warehouse", "Warehouse & Logistics", "Operations & Logistics", "📦", "LOGISTICS", Color.FromArgb(16, 185, 129),
                "GS1-128 barcode receiving station, FEFO/FIFO lot traceability, and 2D/3D visual warehouse rack modeling (WMS).",
                @"// Warehouse Workstation & Traceability
var scanner = new BarcodeScanControl();
var timeline = new ZStockMovementTimeline();
var rack = new ZWarehouseRack();"
            ));

            // 3. AUTOMATION & INFRASTRUCTURE
            _allItems.Add(new ShowcaseFeatureItem(
                "feat_scada", "SCADA Process & P&ID", "Automation & Infrastructure", "🔄", "AUTOMATION", Color.FromArgb(239, 68, 68),
                "P&ID process mimic canvas, Closed-Loop PID tuning, ISA-18.2 alarm management, and 200 Hz OPC-UA engine.",
                @"// SCADA Synoptic & ISA-18.2 Alarms
var synoptic = new ZPlantMimicCanvas();
var pid = new ZPidFaceplate();
var alarmGrid = new ZAlarmGrid();"
            ));

            _allItems.Add(new ShowcaseFeatureItem(
                "feat_network", "Network & Infrastructure", "Automation & Infrastructure", "🌐", "INFRASTRUCTURE", Color.FromArgb(6, 182, 212),
                "19\" 42U equipment rack, 48-port switch faceplate, dynamic network topology, IPAM /24 matrix, and Fieldbus line diagnostics.",
                @"// Network & IT/OT Infrastructure
var devRack = new ZDeviceRack { ShowThermalOverlay = true };
var netTopo = new ZNetworkTopology();
var ipMatrix = new ZIpMatrix();"
            ));

            _allItems.Add(new ShowcaseFeatureItem(
                "feat_industrial", "Industrial Verticals", "Automation & Infrastructure", "⚙️", "VERTICALS", Color.FromArgb(139, 92, 246),
                "8 domain verticals: Gantt schedule, Energy & Smart Grid, Oil & Gas, Pharma ISA-88, Water Treatment, HVAC/BMS, Lab & AGV/ASRS Robotics.",
                @"// Industrial Domain Solutions
var gantt = new ZGantt();
var sld = new SingleLineDiagram();
var column = new ZDistillationColumn();
var bioreactor = new ZBioreactorVessel();"
            ));

            _selectedKey = _allItems[0].Key;
        }
    }
}
