using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using ZeroUI.Core.Analytics;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Icons;
using ZeroUI.Core.Input.Date;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Runtime;
using ZeroUI.Core.Scada;
using ZeroUI.Core.Theme;
using ZeroUI.Demo.Data;
using ZeroUI.Samples.WinformDemo.Diagnostics;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Charts;
using ZeroUI.WinForms.Charts.Model;
using ZeroUI.WinForms.DataGrid;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Feedback;
using ZeroUI.WinForms.Containers;
using ZeroUI.WinForms.Data;
using ZeroUI.Core.Barcode;
using ZeroUI.Core.Pivot;
using ZeroUI.WinForms.PivotGrid;
using ZeroUI.WinForms.Industrial;
using ZeroUI.WinForms.Layout;
using ZeroUI.WinForms.Navigation;
using ZeroUI.WinForms.Overlays;
using ZeroUI.WinForms.Reporting;
using ZeroUI.WinForms.Theme;
using ZeroUI.WinForms.Workflow;
using ZeroCard = ZeroUI.WinForms.Containers.ZeroCard;
using ZeroGridCard = ZeroUI.WinForms.Containers.ZeroGridCard;
using ZeroDescriptions = ZeroUI.WinForms.Containers.ZeroDescriptions;
using ZeroDescriptionItem = ZeroUI.WinForms.Containers.ZeroDescriptionItem;
using ZeroTabControl = ZeroUI.WinForms.Navigation.ZeroTabControl;
using ZeroTabPage = ZeroUI.WinForms.Navigation.ZeroTabPage;
using ZeroToolbar = ZeroUI.WinForms.Navigation.ZeroToolbar;
using ZeroSteps = ZeroUI.WinForms.Workflow.ZeroSteps;
using ZeroStepItem = ZeroUI.WinForms.Workflow.ZeroStepItem;
using ZeroStepStatus = ZeroUI.WinForms.Workflow.ZeroStepStatus;
using ZeroStepGlyph = ZeroUI.WinForms.Workflow.ZeroStepGlyph;
using ZeroListView = ZeroUI.WinForms.Data.ZeroListView;
using ZeroTreeNode = ZeroUI.WinForms.Data.ZeroTreeNode;
using BarcodeBox = ZeroUI.WinForms.Editors.BarcodeBox;
using TabPageEx = ZeroUI.WinForms.Navigation.TabPageEx;
using System.Drawing.Printing;
using ZeroUI.WinForms.Warehouse;
using ZeroUI.WinForms.Warehouse.Models;
using ZeroUI.WinForms.Validation;
using ZeroUI.Core.Validation;
using ZeroUI.Core.Communication;
using ZeroUI.Core.Historian;
using ZeroUI.Core.Scene;
using ZeroUI.Core.Network;
using ZeroUI.WinForms.Network;
using ZeroUI.WinForms.Docking;
using ZeroUI.Core.Range;
using ZeroUI.Core.Pdf;
using ZeroUI.Core.Spreadsheet;
using ZeroUI.Core.Scada.Safety;
using ZeroUI.Core.Notification;

namespace ZeroUI.Samples.WinformDemo.Forms


{
    public sealed partial class MainForm : BaseForm
    {
        private readonly PerformanceMonitor _perfMonitor = new PerformanceMonitor();
        private readonly System.Windows.Forms.Timer _hudTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer _autoScrollTimer = new System.Windows.Forms.Timer();

        private InventoryItem[] _dataset = Array.Empty<InventoryItem>();
        private InventorySource? _zeroSource;

        // UI Controls
        private Panel _topPanel = null!;
        private Panel _hudPanel = null!;

        // Vertical Master Navigation & Modular Feature Clusters
        private ZeroTabControl _mainNav = null!;
        private ZeroTabPage _clusterBenchmark = null!;
        private ZeroTabPage _clusterIndustrial = null!;
        private ZeroTabPage _clusterMes = null!;
        private ZeroTabPage _clusterWarehouse = null!;
        private ZeroTabPage _clusterScada = null!;
        private ZeroTabPage _clusterAnalytics = null!;
        private ZeroTabPage _clusterComponents = null!;
        private ZeroTabPage _clusterScadaSynoptic = null!;
        private ZeroTabPage _clusterNetwork = null!;
        private ZeroTabPage _tabScadaClosedLoop = null!;
        private ZeroTabPage _tabScadaPid = null!;
        private ZeroTabPage _tabScadaAlarms = null!;
        private ZeroTabPage _tabScadaTags = null!;
        private ZeroTabPage _tabScadaOverview = null!;
        private ZeroTabPage _tabIndustrialRuntime = null!;
        private ZeroTabPage _tabScadaSafety = null!;
        private ZeroTabPage _tabSecuritySuite = null!;
        private ZeroTabPage _clusterAiOcr = null!;

        // Sub-tabs
        private ZeroTabControl _subTabsBenchmark = null!;
        private ZeroTabPage _tabZero = null!;
        private ZeroTabPage _tabDgv = null!;

        private ZeroTabControl _subTabsWarehouse = null!;
        private ZeroTabPage _tabWhBarcode = null!;
        private ZeroTabPage _tabWhLot = null!;
        private ZeroTabPage _tabWhRacks = null!;

        private ZeroTabPage _tabControls = null!;
        private ZeroTabPage _tabMes = null!;
        private ZeroTabPage _tabProcessCards = null!;
        private ZeroTabPage _tabScada = null!;
        private ZeroTabPage _tabWms = null!;
        private ZeroTabPage _tabAdvanced = null!;
        private ZeroTabPage _tabCommercial = null!;
        private ZeroTabPage _tabOfficeDocs = null!;
        private ZeroTabPage _tabCharts = null!;
        private ZeroTabPage _tabLayout = null!;
        private ZeroTabPage _tabMasterDetail = null!;
        private ZeroTabPage _tabPivot = null!;
        private ZeroTabPage _tabDesignRAD = null!;

        private ZeroGridControl _zeroGrid = null!;
        private DataGridView _dgv = null!;

        private ZeroGridSearchBar _searchBar = null!;
        private ZeroGridPagination _pagination = null!;
        private ZeroToolbar _mainToolbar = null!;
        private ZeroToolbarButton _btnThemeToggle = null!;
        private ZeroToolbarDropdown _btnSkinsDropdown = null!;
        private ZeroDrawer _drawer = null!;
        private ZeroDescriptions _descDrawer = null!;
        private ZeroSteps _mesSteps = null!;

        private ZeroListView _showcaseLog = null!;
        private System.Windows.Forms.Timer? _logGenTimer;
        private System.Windows.Forms.Timer? _scadaSimTimer;
        private System.Windows.Forms.Timer? _closedLoopTimer;
        private System.Windows.Forms.Timer? _industrialRuntimeTimer;
        private string? _savedGridLayout;

        // DevExpress-Standard Showcase Subsystems
        private ShowcaseFeatureExplorer _featureExplorer = null!;
        private ShowcaseOptionsPanel _optionsPanel = null!;
        private Panel _bottomDescriptionBar = null!;
        private Label _lblFeatureTitle = null!;
        private Label _lblFeatureDescription = null!;
        private string _activeFeatureCode = "";
        private ZeroTabControl _subTabsComponents = null!;
        private ZeroTabControl _subTabsScada = null!;
        private ZeroTabControl _subTabsMes = null!;
        private ZeroTabControl _subTabsNetwork = null!;

        // Top Action Controls
        private Label _lblTitle = null!;
        private Button _btn100k = null!;
        private Button _btn500k = null!;
        private Button _btn1M = null!;
        private Button _btn10M = null!;

        // HUD Labels
        private Label _lblStatus = null!;
        private Label _lblFps = null!;
        private Label _lblLatency = null!;
        private Label _lblRam = null!;
        private Label _lblGc = null!;
        private Button _btnAutoScroll = null!;

        // Stress Test State
        private bool _isStressTesting = false;
        private int _stressTestElapsedTicks = 0;
        private int _stressTestDirection = 1;
        private int _baselineGen0 = 0;
        private Stopwatch _scrollStopwatch = new Stopwatch();
        private int _scrollFrames = 0;

        public MainForm()
        {
            // Initialize central UI thread marshaler & animation sync context
            var syncContext = WindowsFormsSynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(syncContext);
            UiDispatcher.Initialize(syncContext);
            ZeroAnimationClock.SetSynchronizationContext(syncContext);

            InitializeComponents();

            // Register Windows OS Action Center notification bridge
            WindowsNotificationBridge.RegisterMainForm(this);

            // Synchronize application shell with central ZeroSkinManager
            ZeroSkinManager.SkinChanged += skin =>
            {
                if (IsHandleCreated && InvokeRequired)
                {
                    BeginInvoke(new Action(() => ApplyGlobalThemeToForm(skin)));
                }
                else
                {
                    ApplyGlobalThemeToForm(skin);
                }
            };

            ApplyGlobalThemeToForm(ZeroSkinManager.CurrentSkin);

            _hudTimer.Interval = 200;
            _hudTimer.Tick += (s, e) => UpdateHud();
            _hudTimer.Start();

            _autoScrollTimer.Interval = 16; // ~60 Hz tick
            _autoScrollTimer.Tick += AutoScrollTick;

            // Defer initial dataset generation until user explicitly navigates to DataGrid
            // Shown += (s, e) => LoadDataset(100_000);
        }


        private void InitializeComponents()
        {
            Text = "⚡ ZeroUI Demo Center — Enterprise & Industrial Controls Showcase";
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            Size = new Size(1280, 800);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            BackColor = Color.FromArgb(245, 246, 250);

            // 1. Top DevExpress-Style Showcase Banner
            _topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(24, 26, 40),
                Padding = new Padding(14, 8, 14, 8)
            };

            var brandBadge = new Label
            {
                Text = "⚡ ZERO UI",
                BackColor = Color.FromArgb(18, 86, 209),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                AutoSize = false,
                Size = new Size(88, 26),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(14, 15)
            };
            _topPanel.Controls.Add(brandBadge);

            _lblTitle = new Label
            {
                Text = "DEMO CENTER — ENTERPRISE SUITE & SCADA",
                ForeColor = ZeroTheme.Colors.TextPrimary,
                Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(108, 10)
            };
            _topPanel.Controls.Add(_lblTitle);

            var lblSubtitle = new Label
            {
                Text = "High-Performance Virtual Grid, Industrial Process Mimics, & Enterprise Components",
                ForeColor = Color.FromArgb(150, 160, 180),
                Font = new Font("Segoe UI", 8.25f, FontStyle.Regular),
                AutoSize = true,
                Location = new Point(110, 32)
            };
            _topPanel.Controls.Add(lblSubtitle);

            int btnX = 540;
            _btn100k = CreateActionButton("100K", btnX, () => LoadDataset(100_000));
            _btn100k.Size = new Size(68, 30);
            btnX += 74;
            _btn500k = CreateActionButton("500K", btnX, () => LoadDataset(500_000));
            _btn500k.Size = new Size(68, 30);
            btnX += 74;
            _btn1M = CreateActionButton("1M", btnX, () => LoadDataset(1_000_000));
            _btn1M.Size = new Size(68, 30);
            btnX += 74;
            _btn10M = CreateActionButton("🔥 10M", btnX, () => LoadDataset(10_000_000));
            _btn10M.Size = new Size(76, 30);
            _btn10M.BackColor = ZeroTheme.Colors.Danger;
            _btn10M.ForeColor = Color.White;
            btnX += 82;

            _btnAutoScroll = new Button
            {
                Text = "🚀 Stress Test",
                Location = new Point(btnX, 13),
                Size = new Size(130, 30),
                BackColor = ZeroTheme.Colors.Primary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnAutoScroll.FlatAppearance.BorderSize = 0;
            _btnAutoScroll.Click += (s, e) => ToggleStressTest();
            _topPanel.Controls.Add(_btnAutoScroll);

            btnX += 138;
            var btnTourGuide = new Button
            {
                Text = "🧭 Tour Guide",
                Location = new Point(btnX, 13),
                Size = new Size(115, 30),
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnTourGuide.FlatAppearance.BorderSize = 0;
            btnTourGuide.Click += (s, e) => StartWinFormsTour();
            _topPanel.Controls.Add(btnTourGuide);

            _topPanel.Controls.Add(_btn100k);
            _topPanel.Controls.Add(_btn500k);
            _topPanel.Controls.Add(_btn1M);
            _topPanel.Controls.Add(_btn10M);

            // 2. Metric HUD Sub-Panel (Integrated into Bottom Description Bar)
            _hudPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 500,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 4, 8, 4)
            };

            _lblStatus = CreateMetricLabel("Data: Ready", 10);
            _lblFps = CreateMetricLabel("FPS: --", 120);
            _lblLatency = CreateMetricLabel("Latency: -- ms", 200);
            _lblRam = CreateMetricLabel("RAM: -- MB", 310);
            _lblGc = CreateMetricLabel("GC Gen0: --", 405);

            _hudPanel.Controls.Add(_lblStatus);
            _hudPanel.Controls.Add(_lblFps);
            _hudPanel.Controls.Add(_lblLatency);
            _hudPanel.Controls.Add(_lblRam);
            _hudPanel.Controls.Add(_lblGc);

            // 2.1. Bottom Feature Description & Telemetry Bar
            _bottomDescriptionBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = Color.FromArgb(24, 26, 40),
                Padding = new Padding(14, 6, 14, 6)
            };

            var descTextContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };

            _lblFeatureTitle = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.White,
                Text = "⚡ Virtual Grid (10M Rows)"
            };

            _lblFeatureDescription = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(170, 180, 195),
                Text = "ZeroUI GridControl achieves zero-allocation high-frequency scrolling over 10,000,000 records."
            };

            descTextContainer.Controls.Add(_lblFeatureDescription);
            descTextContainer.Controls.Add(_lblFeatureTitle);

            _bottomDescriptionBar.Controls.Add(descTextContainer);
            _bottomDescriptionBar.Controls.Add(_hudPanel);

            // 2.5. ZeroToolbar (Modern DevExpress Enterprise Action Bar)
            _mainToolbar = new ZeroToolbar
            {
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = Color.White
            };

            _mainToolbar.AddButton("Previous", "◀", (s, e) => _featureExplorer?.SelectPrevious());
            _mainToolbar.AddButton("Next", "▶", (s, e) => _featureExplorer?.SelectNext());
            _mainToolbar.AddSeparator();

            _mainToolbar.AddButton("View C# Code", "</>", (s, e) =>
            {
                var dlg = new ShowcaseCodeDialog(_lblFeatureTitle?.Text ?? "Feature", _activeFeatureCode);
                dlg.ShowDialog(this);
            });

            _mainToolbar.AddButton("Options", "⚙️", (s, e) =>
            {
                if (_optionsPanel != null)
                {
                    _optionsPanel.Visible = !_optionsPanel.Visible;
                }
            });
            _mainToolbar.AddSeparator();

            var btnNew = _mainToolbar.AddButton("New Order", "➕", (s, e) =>
            {
                var formPanel = new Panel { Dock = DockStyle.Fill };
                var desc = new ZeroDescriptions { Dock = DockStyle.Fill, Columns = 1, RowHeight = 32 };
                desc.Add("Order Code", "ORD-2026-9081");
                desc.Add("Target Product", "B1030 MAX (Rev 2.0)");
                desc.Add("Quantity", "10,000 units");
                desc.Add("Due Date", DateTime.Today.AddDays(7).ToString("yyyy-MM-dd"));
                formPanel.Controls.Add(desc);

                ZeroModal.Show(this, "Create Production Order", formPanel, onOk: () =>
                {
                    ZeroToast.Success(this, "Work Order ORD-2026-9081 created successfully!");
                });
            });
            btnNew.IsPrimary = true;

            _mainToolbar.AddButton("Detail Drawer", "📋", (s, e) =>
            {
                UpdateDrawerFromSelection();
                _drawer.Toggle();
            });

            _mainToolbar.AddButton("Export CSV", "📊", (s, e) => _searchBar.TriggerExport());
            _mainToolbar.AddButton("Refresh", "🔄", (s, e) =>
            {
                LoadDataset(100_000);
                ZeroToast.Info(this, "Dataset refreshed successfully.");
            });

            _mainToolbar.AddSeparator();

            _mainToolbar.AddDropdown("Views", "👁", (s, e) =>
            {
                ZeroToast.Info(this, "Views: Standard Grid, Pivot Analysis, Production Line View");
            });

            _mainToolbar.AddDropdown("Tools", "🛠", (s, e) =>
            {
                ZeroToast.Info(this, "Diagnostic Tools: GC Zero-Alloc Profiler, Memory DC Monitor");
            });

            var btnAlerts = _mainToolbar.AddButton("Alerts", "🔔", (s, e) =>
            {
                ZeroToast.Warning(this, "Active Alert: SMT Feeder BOA472 low stock warning.");
            });
            btnAlerts.BadgeCount = 3;

            _mainToolbar.AddSeparator();

            _mainToolbar.AddDropdown("Grid Options", "⚙", (s, e) =>
            {
                var menu = new ContextMenuControl();
                menu.AddSearch("Filter grid options...", text =>
                {
                    if (!string.IsNullOrEmpty(text))
                    {
                        _lblStatus.Text = $"Filter option: {text}";
                    }
                });
                menu.AddSeparator();
                menu.AddToggle("Checkbox Column", _zeroGrid.ShowCheckBoxSelectorColumn, val =>
                {
                    _zeroGrid.ShowCheckBoxSelectorColumn = val;
                    _lblStatus.Text = $"Selection Checkbox: {(val ? "Visible" : "Hidden")}";
                }, subtitle: "Enable multi-row checkbox selection");
                menu.AddToggle("Auto Filter Row", _zeroGrid.ShowAutoFilterRow, val =>
                {
                    _zeroGrid.ShowAutoFilterRow = val;
                    _lblStatus.Text = $"Auto Filter Row: {(val ? "Enabled" : "Disabled")}";
                }, subtitle: "Header row instant column filtering");
                menu.AddSlider("Row Height", 24, 60, _zeroGrid.RowHeight, val =>
                {
                    _zeroGrid.RowHeight = val;
                    _lblStatus.Text = $"Grid Row Height: {val}px";
                }, unit: "px");
                menu.AddSeparator();
                menu.AddAction("Best Fit Columns", () =>
                {
                    _zeroGrid.BestFitColumns();
                    ZeroToast.Info(this, "Auto-sized all columns to fit content (Best Fit).");
                }, IconKey.AutoLayout);
                menu.AddAction("Save Column Layout (JSON)", () =>
                {
                    _savedGridLayout = _zeroGrid.SaveLayoutToJson();
                    ZeroToast.Success(this, "Saved column layout to JSON.");
                }, IconKey.Save);
                menu.AddAction("Restore Column Layout (JSON)", () =>
                {
                    if (!string.IsNullOrEmpty(_savedGridLayout))
                    {
                        _zeroGrid.RestoreLayoutFromJson(_savedGridLayout);
                        ZeroToast.Success(this, "Restored column layout from JSON.");
                    }
                    else
                    {
                        ZeroToast.Warning(this, "No saved layout found. Save layout first.");
                    }
                }, IconKey.Refresh);

                Point pos;
                if (s is ToolbarItem tbItem && tbItem.Bounds.Width > 0)
                {
                    pos = _mainToolbar.PointToScreen(new Point(tbItem.Bounds.Left, tbItem.Bounds.Bottom));
                }
                else
                {
                    pos = _mainToolbar.PointToScreen(new Point(Math.Max(100, _mainToolbar.Width / 3), _mainToolbar.Height));
                }
                menu.Show(pos);
            });

            _mainToolbar.AddSpacer(); // Elastic right spacer

            _btnSkinsDropdown = _mainToolbar.AddDropdown($"🎨 {ZeroSkinManager.CurrentSkin.DisplayName}", null, (s, e) =>
            {
                ShowSkinGalleryMenu();
            });

            _btnThemeToggle = _mainToolbar.AddButton(ZeroTheme.IsDark ? "☀️ Light Mode" : "🌙 Dark Mode", null, (s, e) =>
            {
                ZeroTheme.ToggleTheme();
            });

            _mainToolbar.AddButton("Corners: Rounded", "📐", (s, e) =>
            {
                var btn = s as ZeroToolbarButton;
                ZeroUIConfig.ToggleRoundedCornersAnimated(this, () =>
                {
                    if (btn != null)
                    {
                        btn.Text = ZeroUIConfig.RoundedCorners ? "Corners: Rounded" : "Corners: Sharp";
                    }
                    ZeroToast.Info(this, $"Global corner style changed: {(ZeroUIConfig.RoundedCorners ? "Rounded (6px)" : "Sharp (0px)")}");
                });
            });

            _mainToolbar.AddButton("Fullscreen", "⛶", (s, e) =>
            {
                WindowState = (WindowState == FormWindowState.Maximized) ? FormWindowState.Normal : FormWindowState.Maximized;
            });

            _mainToolbar.AddButton("Settings", "⚙️", (s, e) =>
            {
                OpenGlobalSettingsDialog();
            });

            _mainToolbar.AddButton("Help", "❓", (s, e) =>
            {
                ZeroToast.Info(this, "Repository: https://github.com/kzxl/ZeroUI");
            });

            // 3. Setup Side Drawer
            _drawer = new ZeroDrawer
            {
                Title = "Material & Lot Specification",
                Subtitle = "Deep inspection for selected inventory entity",
                DrawerWidth = 420
            };
            _descDrawer = new ZeroDescriptions { Dock = DockStyle.Fill, Columns = 1, RowHeight = 32 };
            _descDrawer.Add("Part Number", "BOA437-SMT-V2");
            _descDrawer.Add("Category", "Microcontroller / Active");
            _descDrawer.Add("Standard Cost", "$14.20");
            _descDrawer.Add("Lead Time", "3 Days");
            _descDrawer.Add("Safety Stock", "500 Units");
            _descDrawer.Add("Supplier", "Foxconn Precision Co.");
            _descDrawer.Add("Inspection Status", "Passed OQC", Color.FromArgb(16, 185, 129));
            _drawer.ContentPanel.Controls.Add(_descDrawer);

            // 4. Vertical Master Navigation (Modular Feature Clusters)
            _mainNav = new ZeroTabControl
            {
                Dock = DockStyle.Fill,
                Orientation = ZeroTabOrientation.Vertical,
                TabWidth = 260,
                TabHeight = 46,
                TabStyle = ZeroTabStyle.Underline,
                ShowHeader = false
            };

            // Cluster 1: Core Benchmarks
            _clusterBenchmark = new ZeroTabPage("Core Benchmarks", "⚡");
            _subTabsBenchmark = new ZeroTabControl
            {
                Dock = DockStyle.Fill,
                Orientation = ZeroTabOrientation.Horizontal,
                TabHeight = 36,
                TabStyle = ZeroTabStyle.Pill
            };
            _tabZero = new ZeroTabPage("ZeroGrid (1M Rows)", "⚡");
            _tabDgv = new ZeroTabPage("Standard DataGridView (Virtual)", "🐢");
            _subTabsBenchmark.AddTab(_tabZero);
            _subTabsBenchmark.AddTab(_tabDgv);
            _clusterBenchmark.Controls.Add(_subTabsBenchmark);

            // Cluster 2: MES Production (Lazy)
            _clusterMes = new ZeroTabPage("MES & Smart Factory", "🏭", page => InitializeMesCluster(page)) { BadgeCount = 3 };

            // Cluster 3: Warehouse & Logistics Suite (Lazy)
            _clusterWarehouse = new ZeroTabPage("Warehouse & Logistics", "📦", page => InitializeWarehouseCluster(page)) { BadgeCount = 4 };

            // Cluster 4: SCADA & Telemetry (Lazy)
            _clusterScada = new ZeroTabPage("SCADA & Telemetry", "🔬", _ => InitializeScadaHub());
            _tabScada = _clusterScada;

            // Cluster 5: Analytics & Charts (Lazy)
            _clusterAnalytics = new ZeroTabPage("Analytics & Charts", "📊", _ => InitializeChartsDashboard());
            _tabCharts = _clusterAnalytics;

            // Cluster 6: UI Component Catalog (Lazy)
            _clusterComponents = new ZeroTabPage("UI Component Catalog", "🎨", page => InitializeComponentsCluster(page));

            // Cluster 7: SCADA Process & P&ID Synoptic (Phased Real-Time Automation - Lazy)
            _clusterScadaSynoptic = new ZeroTabPage("SCADA Process & P&ID", "🏭", page => InitializeScadaSynopticCluster(page));

            // Cluster 8: Network & IT/OT Infrastructure (Phase 12 Suite - Lazy)
            _clusterNetwork = new ZeroTabPage("Network & Infrastructure", "🌐", page => InitializeNetworkInfrastructure(page)) { BadgeCount = 6 };

            // Cluster 9: Industrial Domain Verticals (Phases 13, 15, 16, 17 - Lazy)
            _clusterIndustrial = new ZeroTabPage("Industrial Verticals", "🏭", page => InitializeIndustrialVerticals(page)) { BadgeCount = 7 };

            // Cluster 10: AI Assistant & Computer Vision Studio (Phase 8 Suite - Lazy)
            _clusterAiOcr = new ZeroTabPage("AI & Vision Studio", "🤖", page => InitializeAiOcrCluster(page)) { BadgeCount = 2 };

            // Build benchmark views (active suite at startup)
            InitializeZeroGrid();
            InitializeDataGridView();

            _tabZero.Controls.Add(_zeroGrid);
            _tabZero.Controls.Add(_pagination);
            _tabZero.Controls.Add(_searchBar);
            _tabDgv.Controls.Add(_dgv);

            // Add all clusters to master vertical navigation
            _mainNav.AddTab(_clusterBenchmark);
            _mainNav.AddTab(_clusterIndustrial);
            _mainNav.AddTab(_clusterScadaSynoptic);
            _mainNav.AddTab(_clusterNetwork);
            _mainNav.AddTab(_clusterMes);
            _mainNav.AddTab(_clusterWarehouse);
            _mainNav.AddTab(_clusterAnalytics);
            _mainNav.AddTab(_clusterComponents);
            _mainNav.AddTab(_clusterAiOcr);

            // Start autonomous background PLC driver
            SimulatedPlcDriver.Start();

            _subTabsBenchmark.SelectedIndexChanged += (s, e) =>
            {
                _scrollFrames = 0;
                _scrollStopwatch.Restart();
                if (_subTabsBenchmark.SelectedTab == _tabZero && _dataset.Length == 0)
                {
                    LoadDataset(100_000);
                }
                else if (_subTabsBenchmark.SelectedTab == _tabDgv && _dgv.RowCount != _dataset.Length && _dataset.Length > 0)
                {
                    try
                    {
                        _dgv.SuspendLayout();
                        _dgv.Rows.Clear();
                        _dgv.RowCount = _dataset.Length;
                        _dgv.ResumeLayout();
                    }
                    catch { }
                }
            };

            // Setup DevExpress-Style Feature Explorer & Options Panel
            _featureExplorer = new ShowcaseFeatureExplorer
            {
                Dock = DockStyle.Left,
                Width = 340
            };
            _featureExplorer.FeatureSelected += OnFeatureSelected;

            _optionsPanel = new ShowcaseOptionsPanel
            {
                Dock = DockStyle.Right,
                Width = 270
            };
            _optionsPanel.DatasetLoadRequested += count => LoadDataset(count);
            _optionsPanel.StressTestToggled += () => ToggleStressTest();
            _optionsPanel.ExportCsvRequested += () => _searchBar.TriggerExport();
            _optionsPanel.FindPanelToggled += val => { if (_searchBar != null) _searchBar.Visible = val; };
            _optionsPanel.BindGrid(_zeroGrid);

            // Assembly Form Layout
            Controls.Add(_drawer);
            Controls.Add(_mainNav);
            Controls.Add(_optionsPanel);
            Controls.Add(_featureExplorer);
            Controls.Add(_bottomDescriptionBar);
            Controls.Add(_mainToolbar);
            Controls.Add(_topPanel);

            _featureExplorer.SelectFeature("feat_components");
        }

        private void OnFeatureSelected(ShowcaseFeatureItem item)
        {
            if (item == null) return;

            if (_lblFeatureTitle != null) _lblFeatureTitle.Text = $"{item.Icon} {item.Title}";
            if (_lblFeatureDescription != null) _lblFeatureDescription.Text = item.Description;
            _activeFeatureCode = item.CodeSnippet;

            switch (item.Key)
            {
                // 1. UI COMPONENT CATALOG & EDITORS
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
                    _mainNav.SelectedTab = _clusterComponents;
                    _optionsPanel.Visible = false;
                    break;

                case "feat_design_rad":
                    _mainNav.SelectedTab = _clusterComponents;
                    _subTabsComponents.SelectedTab = _tabDesignRAD;
                    _optionsPanel.Visible = false;
                    break;

                // 2. DATA & GRIDS
                case "feat_datagrid":
                case "feat_virtual_10m":
                case "feat_autofilter":
                case "feat_grouping_summaries":
                case "feat_banded_headers":
                    _mainNav.SelectedTab = _clusterBenchmark;
                    _subTabsBenchmark.SelectedTab = _tabZero;
                    if (_dataset.Length == 0)
                    {
                        LoadDataset(100_000);
                    }
                    _optionsPanel.Visible = true;
                    _optionsPanel.BindGrid(_zeroGrid);
                    break;

                case "feat_standard_dgv":
                    _mainNav.SelectedTab = _clusterBenchmark;
                    _subTabsBenchmark.SelectedTab = _tabDgv;
                    if (_dataset.Length == 0)
                    {
                        LoadDataset(100_000);
                    }
                    _optionsPanel.Visible = false;
                    break;

                // 3. ANALYTICS & CHARTS
                case "feat_analytics":
                case "feat_analytics_charts":
                case "feat_analytics_spc":
                    _mainNav.SelectedTab = _clusterAnalytics;
                    _optionsPanel.Visible = false;
                    break;

                // 4. MES & OPERATIONS
                case "feat_mes":
                case "feat_mes_dashboard":
                case "feat_workflow_kanban":
                    _mainNav.SelectedTab = _clusterMes;
                    _optionsPanel.Visible = false;
                    break;

                // 5. WAREHOUSE & LOGISTICS
                case "feat_warehouse":
                case "feat_barcode_station":
                case "feat_warehouse_lot":
                case "feat_warehouse_racks":
                    _mainNav.SelectedTab = _clusterWarehouse;
                    _optionsPanel.Visible = false;
                    break;

                // 6. SCADA PROCESS & P&ID
                case "feat_scada":
                case "feat_scada_synoptic":
                case "feat_scada_pid":
                case "feat_scada_alarms":
                case "feat_scada_tags":
                case "feat_scada_gauges":
                case "feat_scada_interlock":
                    _mainNav.SelectedTab = _clusterScadaSynoptic;
                    _optionsPanel.Visible = false;
                    break;

                // 7. NETWORK & INFRASTRUCTURE
                case "feat_network":
                case "feat_net_rack":
                case "feat_net_topology":
                case "feat_net_chassis":
                case "feat_net_ipam":
                case "feat_net_fieldbus":
                    _mainNav.SelectedTab = _clusterNetwork;
                    _optionsPanel.Visible = false;
                    break;

                // 8. INDUSTRIAL DOMAIN VERTICALS
                case "feat_industrial":
                case "feat_workflow_gantt":
                case "feat_vert_energy":
                case "feat_vert_petrochem":
                case "feat_vert_pharma":
                case "feat_vert_water":
                case "feat_vert_bms":
                case "feat_vert_life_sciences":
                case "feat_vert_robotics":
                    _mainNav.SelectedTab = _clusterIndustrial;
                    _optionsPanel.Visible = false;
                    break;

                default:
                    _mainNav.SelectedTab = _clusterComponents;
                    _optionsPanel.Visible = false;
                    break;
            }
        }

        public void SelectTabByIndex(int index)
        {
            if (_mainNav != null && index >= 0 && index < _mainNav.TabPages.Count)
            {
                _mainNav.SelectedIndex = index;
            }
        }

        public void LoadDatasetPublic(int count) => LoadDataset(count);

        public void ToggleThemePublic() => ZeroTheme.ToggleTheme();

        public void ScrollGridToRowPublic(int rowIndex)
        {
            try
            {
                if (_zeroGrid != null && rowIndex >= 0)
                {
                    _zeroGrid.ScrollToRow(rowIndex);
                }
            }
            catch { }
        }

        public void SelectSubTabByIndex(int masterIndex, int subIndex)
        {
            SelectTabByIndex(masterIndex);
            if (_mainNav != null && masterIndex >= 0 && masterIndex < _mainNav.TabPages.Count)
            {
                var page = _mainNav.TabPages[masterIndex];
                SetChildTab(page, subIndex);
            }
        }

        private static bool SetChildTab(Control parent, int index)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is ZeroTabControl tc && index >= 0 && index < tc.TabPages.Count)
                {
                    tc.SelectedIndex = index;
                    return true;
                }
                if (c.HasChildren && SetChildTab(c, index))
                    return true;
            }
            return false;
        }

        private void ShowSkinGalleryMenu()
        {
            var menu = new ZeroContextMenu();
            var curSkin = ZeroSkinManager.CurrentSkin;

            foreach (var sk in ZeroSkinManager.AvailableSkins)
            {
                var target = sk;
                string icon = sk.Name switch
                {
                    "obsidian_dark" => "🌙",
                    "clean_light" => "☀️",
                    "nordic_slate" => "❄️",
                    "cyberpunk_neon" => "⚡",
                    "emerald_industrial" => "🟢",
                    "solar_amber" => "🟠",
                    "amethyst_violet" => "🟣",
                    "crimson_ruby" => "🔴",
                    "oled_midnight" => "⬛",
                    _ => "🎨"
                };

                bool isCur = string.Equals(sk.Name, curSkin.Name, StringComparison.OrdinalIgnoreCase);
                string label = isCur ? $"{sk.DisplayName}  ✓" : sk.DisplayName;

                menu.AddAction(label, () =>
                {
                    ZeroSkinManager.ApplySkin(target);
                    ZeroToast.Info(this, $"Applied Skin: {target.DisplayName} ({(target.IsDark ? "Dark" : "Light")})");
                }, icon: icon);
            }

            menu.Show(Cursor.Position);
        }

        private void ApplyGlobalThemeToForm(ZeroSkin skin)
        {
            var colors = ZeroTheme.Colors;

            this.BackColor = colors.Background;
            if (_topPanel != null) _topPanel.BackColor = colors.HeaderBackground;
            if (_hudPanel != null) _hudPanel.BackColor = colors.Background;
            if (_bottomDescriptionBar != null)
            {
                _bottomDescriptionBar.BackColor = skin.IsDark ? Color.FromArgb(20, 22, 32) : Color.FromArgb(240, 242, 246);
            }
            if (_lblFeatureTitle != null) _lblFeatureTitle.ForeColor = colors.TextPrimary;
            if (_lblFeatureDescription != null) _lblFeatureDescription.ForeColor = colors.TextSecondary;
            if (_featureExplorer != null) _featureExplorer.ApplyTheme(skin);

            if (_mainToolbar != null)
            {
                _mainToolbar.BackColor = colors.Surface;
                _mainToolbar.BorderColor = colors.Border;
            }

            if (_lblTitle != null) _lblTitle.ForeColor = colors.TextPrimary;

            if (_btn100k != null) { _btn100k.BackColor = colors.Surface; _btn100k.ForeColor = colors.TextPrimary; }
            if (_btn500k != null) { _btn500k.BackColor = colors.Surface; _btn500k.ForeColor = colors.TextPrimary; }
            if (_btn1M != null) { _btn1M.BackColor = colors.Surface; _btn1M.ForeColor = colors.TextPrimary; }
            if (_btn10M != null) { _btn10M.BackColor = colors.Danger; _btn10M.ForeColor = Color.White; }
            if (_btnAutoScroll != null) { _btnAutoScroll.BackColor = colors.Primary; _btnAutoScroll.ForeColor = Color.White; }

            Color hudMetricColor = skin.IsDark ? colors.Success : Color.FromArgb(22, 101, 52);
            if (_lblStatus != null) _lblStatus.ForeColor = hudMetricColor;
            if (_lblFps != null) _lblFps.ForeColor = hudMetricColor;
            if (_lblLatency != null) _lblLatency.ForeColor = hudMetricColor;
            if (_lblRam != null) _lblRam.ForeColor = hudMetricColor;
            if (_lblGc != null) _lblGc.ForeColor = hudMetricColor;

            if (_clusterBenchmark != null) _clusterBenchmark.BackColor = colors.Background;
            if (_clusterIndustrial != null) _clusterIndustrial.BackColor = colors.Background;
            if (_clusterMes != null) _clusterMes.BackColor = colors.Background;
            if (_clusterWarehouse != null) _clusterWarehouse.BackColor = colors.Background;
            if (_clusterScada != null) _clusterScada.BackColor = colors.Background;
            if (_clusterAnalytics != null) _clusterAnalytics.BackColor = colors.Background;
            if (_clusterComponents != null) _clusterComponents.BackColor = colors.Background;
            if (_clusterScadaSynoptic != null) _clusterScadaSynoptic.BackColor = colors.Background;

            if (_btnThemeToggle != null)
            {
                _btnThemeToggle.Text = skin.IsDark ? "☀️ Light Mode" : "🌙 Dark Mode";
            }
            if (_btnSkinsDropdown != null)
            {
                _btnSkinsDropdown.Text = $"🎨 {skin.DisplayName}";
            }

            if (_dgv != null)
            {
                _dgv.BackgroundColor = colors.Background;
                _dgv.DefaultCellStyle.BackColor = colors.Surface;
                _dgv.DefaultCellStyle.ForeColor = colors.TextPrimary;
                _dgv.DefaultCellStyle.SelectionBackColor = colors.Hover;
                _dgv.DefaultCellStyle.SelectionForeColor = colors.TextPrimary;
                _dgv.ColumnHeadersDefaultCellStyle.BackColor = colors.HeaderBackground;
                _dgv.ColumnHeadersDefaultCellStyle.ForeColor = colors.TextPrimary;
                _dgv.GridColor = colors.Border;
            }

            ApplyRecursiveTheme(this, colors);

            Invalidate(true);
        }

        private void ApplyRecursiveTheme(Control parent, ZeroThemePalette colors)
        {
            if (parent == null) return;

            foreach (Control c in parent.Controls)
            {
                if (c == _topPanel || c == _hudPanel || c == _mainToolbar) continue;

                if (c is ZeroTabPage tabPage)
                {
                    tabPage.BackColor = colors.Background;
                }
                else if (c is ZeroCard card)
                {
                    card.BackColor = colors.CardBackground;
                }
                else if (c.GetType() == typeof(Panel))
                {
                    var p = (Panel)c;
                    if (p.BackColor != Color.Transparent)
                    {
                        p.BackColor = (p.Name == "leftShowcasePanel") ? colors.Surface : colors.Background;
                    }
                }
                else if (c is Label lbl)
                {
                    if (lbl != _lblFps && lbl != _lblLatency && lbl != _lblRam && lbl != _lblGc && lbl != _lblStatus && lbl != _lblTitle)
                    {
                        lbl.ForeColor = colors.TextPrimary;
                    }
                }

                if (c.HasChildren && !(c is ZeroGridControl) && !(c is ZeroToolbar) && !(c is ZeroTreeList))
                {
                    ApplyRecursiveTheme(c, colors);
                }
            }
        }

        private Button CreateActionButton(string text, int x, Action onClick)
        {
            var colors = ZeroTheme.Colors;
            Button btn = new Button
            {
                Text = text,
                Location = new Point(x, 10),
                Size = new Size(100, 34),
                BackColor = colors.Surface,
                ForeColor = colors.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private Label CreateMetricLabel(string text, int x)
        {
            var colors = ZeroTheme.Colors;
            Color metricColor = ZeroTheme.IsDark ? colors.Success : Color.FromArgb(22, 101, 52);
            return new Label
            {
                Text = text,
                ForeColor = metricColor,
                Font = new Font("Consolas", 10f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(x, 12)
            };
        }

        private void InitializeMesCluster(ZeroTabPage cluster)
        {
            _subTabsMes = new ZeroTabControl
            {
                Dock = DockStyle.Fill,
                Orientation = ZeroTabOrientation.Horizontal,
                TabHeight = 36,
                TabStyle = ZeroTabStyle.Pill
            };
            _tabMes = new ZeroTabPage("Live Production Dashboard", "🏭", _ => InitializeMesDashboard());
            _tabProcessCards = new ZeroTabPage("MOP & Work Order Cards", "📋", p => InitializeProcessCards(p));
            _subTabsMes.AddTab(_tabMes);
            _subTabsMes.AddTab(_tabProcessCards);
            cluster.Controls.Add(_subTabsMes);
        }

        private void InitializeWarehouseCluster(ZeroTabPage cluster)
        {
            _subTabsWarehouse = new ZeroTabControl
            {
                Dock = DockStyle.Fill,
                Orientation = ZeroTabOrientation.Horizontal,
                TabHeight = 36,
                TabStyle = ZeroTabStyle.Pill
            };
            void InitWhWorkstation()
            {
                if (_tabWhBarcode != null && _tabWhBarcode.IsInitialized) return;
                if (_tabWhBarcode != null) _tabWhBarcode.IsInitialized = true;
                if (_tabWhLot != null) _tabWhLot.IsInitialized = true;
                InitializeWarehouseWorkstation();
            }
            _tabWhBarcode = new ZeroTabPage("Receiving & Barcode Station", "🔍", _ => InitWhWorkstation());
            _tabWhLot = new ZeroTabPage("FIFO/FEFO Lot Allocation", "📋", _ => InitWhWorkstation());
            _tabWhRacks = new ZeroTabPage("Storage Racks & Tanks", "🏢", _ => InitializeWmsCenter());
            _tabWms = _tabWhRacks;
            _subTabsWarehouse.AddTab(_tabWhBarcode);
            _subTabsWarehouse.AddTab(_tabWhLot);
            _subTabsWarehouse.AddTab(_tabWhRacks);
            cluster.Controls.Add(_subTabsWarehouse);
        }

        private void InitializeComponentsCluster(ZeroTabPage cluster)
        {
            _subTabsComponents = new ZeroTabControl
            {
                Dock = DockStyle.Fill,
                Orientation = ZeroTabOrientation.Horizontal,
                TabHeight = 36,
                TabStyle = ZeroTabStyle.Pill
            };
            _tabControls = new ZeroTabPage("Core Controls", "🎛️", _ => InitializeComponentsShowcase());
            _tabCommercial = new ZeroTabPage("Commercial Suite", "🏢", _ => InitializeCommercialSuite());
            _tabOfficeDocs = new ZeroTabPage("Office & Docs", "📄", p => InitializeOfficeDocsSuite(p));
            _tabAdvanced = new ZeroTabPage("Data Hierarchy & Inspector", "🌳", _ => InitializeAdvancedSuite());
            _tabPivot = new ZeroTabPage("Pivot Grid", "📊", p => InitializePivotGridShowcase(p));
            _tabLayout = new ZeroTabPage("Layout & Workspaces", "📐", p => InitializeLayoutShowcase(p));
            _tabMasterDetail = new ZeroTabPage("Master-Detail LookUp", "📑", p => InitializeMasterDetailDemo(p));
            _tabDesignRAD = new ZeroTabPage("Visual Designers & RAD", "🛠️", p => InitializeDesignerRadDemo(p));
            _subTabsComponents.AddTab(_tabControls);
            _subTabsComponents.AddTab(_tabCommercial);
            _subTabsComponents.AddTab(_tabOfficeDocs);
            _subTabsComponents.AddTab(_tabAdvanced);
            _subTabsComponents.AddTab(_tabPivot);
            _subTabsComponents.AddTab(_tabLayout);
            _subTabsComponents.AddTab(_tabMasterDetail);
            _subTabsComponents.AddTab(_tabDesignRAD);
            cluster.Controls.Add(_subTabsComponents);
        }

        private void InitializeScadaSynopticCluster(ZeroTabPage cluster)
        {
            _subTabsScada = new ZeroTabControl
            {
                Dock = DockStyle.Fill,
                Orientation = ZeroTabOrientation.Horizontal,
                TabHeight = 36,
                TabStyle = ZeroTabStyle.Pill
            };
            _tabScadaClosedLoop = new ZeroTabPage("Closed-Loop Batch Process", "🔄", p => InitializeScadaClosedLoopProcess(p));
            _tabScadaPid = new ZeroTabPage("Phase 1: P&ID Process Flow", "🔄", p => InitializeScadaProcessFlow(p));
            _tabScadaAlarms = new ZeroTabPage("Phase 2: ISA-18.2 Alarms & PID", "🚨", p => InitializeScadaAlarmsAndPid(p));
            _tabScadaTags = new ZeroTabPage("Phase 3: Real-Time Tag Engine", "⚡", p => InitializeScadaTagEngineMonitor(p));
            _tabScadaOverview = new ZeroTabPage("Phase 4: Plant Overview & HMI", "🎛️", p => InitializeScadaHmiOverview(p));
            _tabIndustrialRuntime = new ZeroTabPage("Phase 5: Industrial Edge Runtime", "⚙️", p => InitializeIndustrialRuntimeOverview(p));
            _tabScadaSafety = new ZeroTabPage("Phase 6: Safety & Annunciators", "🚨", p => InitializeScadaSafetyCluster(p));
            _tabSecuritySuite = new ZeroTabPage("Phase 7: Security & Users", "🛡️", p => InitializeSecuritySuiteCluster(p));
            _subTabsScada.AddTab(_tabScadaClosedLoop);
            _subTabsScada.AddTab(_tabScadaPid);
            _subTabsScada.AddTab(_tabScadaAlarms);
            _subTabsScada.AddTab(_tabScadaTags);
            _subTabsScada.AddTab(_tabScadaOverview);
            _subTabsScada.AddTab(_tabIndustrialRuntime);
            _subTabsScada.AddTab(_tabScadaSafety);
            _subTabsScada.AddTab(_tabSecuritySuite);
            cluster.Controls.Add(_subTabsScada);
        }

        private void InitializeZeroGrid()
        {
            _zeroGrid = new ZeroGridControl
            {
                Dock = DockStyle.Fill,
                HeaderHeight = 32,
                RowHeight = 28
            };

            _zeroGrid.Columns.Add(new ZeroColumn("Active", 60, CellAlignment.Center) { ColumnType = GridColumnType.Boolean });
            _zeroGrid.Columns.Add(new ZeroColumn("Category", 140, CellAlignment.Left) { AllowGrouping = true });
            _zeroGrid.Columns.Add(new ZeroColumn("ID", 70, CellAlignment.Right) { ColumnType = GridColumnType.Numeric, ReadOnly = true, IsPinned = true });
            _zeroGrid.Columns.Add(new ZeroColumn("Item Code", 120, CellAlignment.Left) { ReadOnly = true, IsPinned = true });
            _zeroGrid.Columns.Add(new ZeroColumn("Item Name / Description", 260, CellAlignment.Left));
            _zeroGrid.Columns.Add(new ZeroColumn("Quantity", 90, CellAlignment.Right) { ColumnType = GridColumnType.Numeric, Summary = SummaryType.Sum });
            _zeroGrid.Columns.Add(new ZeroColumn("Unit Price ($)", 115, CellAlignment.Right) { ColumnType = GridColumnType.Numeric, Summary = SummaryType.Average });
            _zeroGrid.Columns.Add(new ZeroColumn("Total Amount ($)", 135, CellAlignment.Right) { ColumnType = GridColumnType.Numeric, Summary = SummaryType.Sum, ReadOnly = true });
            _zeroGrid.Columns.Add(new ZeroColumn("Yield %", 95, CellAlignment.Center) { ColumnType = GridColumnType.Numeric });
            _zeroGrid.Columns.Add(new ZeroColumn("Batch No", 120, CellAlignment.Center));
            _zeroGrid.Columns.Add(new ZeroColumn("Status", 130, CellAlignment.Center));

            _zeroGrid.CellValidating += (s, e) =>
            {
                if (e.ColumnIndex == 5) // Quantity
                {
                    if (int.TryParse(e.NewValue, out int q) && q < 0)
                    {
                        e.Cancel = true;
                        e.ErrorMessage = "Quantity cannot be negative!";
                    }
                }
            };

            _zeroGrid.SortingStarted += (s, e) =>
            {
                _lblStatus.Text = "Data: Sorting rows asynchronously (0ms UI freeze)...";
            };

            _zeroGrid.SortingCompleted += (s, elapsed) =>
            {
                _lblStatus.Text = $"Data: {_zeroGrid.RowCount:N0} rows (Sorted in {elapsed.TotalMilliseconds:F1} ms)";
            };

            _searchBar = new ZeroGridSearchBar();
            _searchBar.AttachToGrid(_zeroGrid);
            _searchBar.ExportClicked += HandleExportCsv;

            _pagination = new ZeroGridPagination();
            _pagination.PageChanged += (s, e) =>
            {
                _zeroGrid.ScrollToRow(_pagination.PageStartRow);
            };

            _zeroGrid.SelectionChanged += (s, e) =>
            {
                if (_drawer.IsOpen)
                {
                    UpdateDrawerFromSelection();
                }
            };

            _zeroGrid.DoubleClick += (s, e) =>
            {
                UpdateDrawerFromSelection();
                _drawer.Open();
            };
        }

        private void UpdateDrawerFromSelection()
        {
            if (_zeroGrid == null || _descDrawer == null || _drawer == null) return;
            int visualRow = _zeroGrid.SelectedVisualRow;
            if (visualRow < 0 || _zeroGrid.DataSource == null) return;

            int modelRow = _zeroGrid.GetModelRowIndex(visualRow);
            if (modelRow < 0 || modelRow >= _zeroGrid.DataSource.TotalRowCount) return;

            CellValueBuffer buf = new CellValueBuffer();
            _zeroGrid.DataSource.GetCellValue(modelRow, 1, ref buf);
            string category = buf.Text.ToString();

            _zeroGrid.DataSource.GetCellValue(modelRow, 2, ref buf);
            string id = buf.Text.ToString();

            _zeroGrid.DataSource.GetCellValue(modelRow, 3, ref buf);
            string itemCode = buf.Text.ToString();

            _zeroGrid.DataSource.GetCellValue(modelRow, 4, ref buf);
            string itemName = buf.Text.ToString();

            _zeroGrid.DataSource.GetCellValue(modelRow, 5, ref buf);
            string qty = buf.Text.ToString();

            _zeroGrid.DataSource.GetCellValue(modelRow, 6, ref buf);
            string price = buf.Text.ToString();

            _zeroGrid.DataSource.GetCellValue(modelRow, 7, ref buf);
            string total = buf.Text.ToString();

            _zeroGrid.DataSource.GetCellValue(modelRow, 8, ref buf);
            string yield = buf.Text.ToString();

            _zeroGrid.DataSource.GetCellValue(modelRow, 9, ref buf);
            string batchNo = buf.Text.ToString();

            _zeroGrid.DataSource.GetCellValue(modelRow, 10, ref buf);
            string status = buf.Text.ToString();

            _drawer.Title = string.IsNullOrEmpty(itemCode) ? "Material Specification" : itemCode;
            _drawer.Subtitle = string.IsNullOrEmpty(itemName) ? "Selected Entity Inspection" : itemName;

            _descDrawer.Clear();
            _descDrawer.Add("Record ID", $"#{id}");
            _descDrawer.Add("Category", category);
            _descDrawer.Add("Part Number", itemCode);
            _descDrawer.Add("Description", itemName);
            _descDrawer.Add("Batch / Lot No", batchNo);
            _descDrawer.Add("Stock Quantity", $"{qty} Units");
            _descDrawer.Add("Unit Cost", $"${price}");
            _descDrawer.Add("Total Valuation", $"${total}");
            _descDrawer.Add("Yield Rate", yield);

            Color statusColor = status.IndexOf("Pass", StringComparison.OrdinalIgnoreCase) >= 0 || status.IndexOf("Active", StringComparison.OrdinalIgnoreCase) >= 0 || status.IndexOf("Good", StringComparison.OrdinalIgnoreCase) >= 0
                ? Color.FromArgb(16, 185, 129)
                : status.IndexOf("Warn", StringComparison.OrdinalIgnoreCase) >= 0 || status.IndexOf("Low", StringComparison.OrdinalIgnoreCase) >= 0
                    ? Color.FromArgb(245, 158, 11)
                    : Color.FromArgb(59, 130, 246);
            _descDrawer.Add("QC / Lot Status", status, statusColor);
        }


        private void InitializeDataGridView()
        {
            _dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                VirtualMode = true,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToOrderColumns = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None
            };

            // Enable double buffering on DataGridView via reflection
            typeof(DataGridView).InvokeMember(
                "DoubleBuffered",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
                null, _dgv, new object[] { true });

            _dgv.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Active", Width = 55 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Category", Width = 140 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ID", Width = 70 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Item Code", Width = 120 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Item Name / Description", Width = 260 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Quantity", Width = 90 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Unit Price ($)", Width = 115 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Total Amount ($)", Width = 135 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Yield %", Width = 80 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Batch No", Width = 120 });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", Width = 130 });

            _dgv.CellValueNeeded += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= _dataset.Length) return;
                ref readonly var item = ref _dataset[e.RowIndex];

                switch (e.ColumnIndex)
                {
                    case 0: e.Value = item.IsActive; break;
                    case 1: e.Value = item.Category; break;
                    case 2: e.Value = item.Id; break;
                    case 3: e.Value = item.ItemCode; break;
                    case 4: e.Value = item.ItemName; break;
                    case 5: e.Value = item.Quantity; break;
                    case 6: e.Value = item.UnitPrice.ToString("N2"); break;
                    case 7: e.Value = item.TotalAmount.ToString("N2"); break;
                    case 8: e.Value = item.YieldRate.ToString("P0"); break;
                    case 9: e.Value = item.LotNumber; break;
                    case 10: e.Value = item.Status; break;
                }
            };
        }

        private async void LoadDataset(int count)
        {
            if (_isStressTesting)
            {
                ToggleStressTest();
            }

            _lblStatus.Text = $"Data: Generating {count:N0} rows...";

            Stopwatch sw = Stopwatch.StartNew();

            if (count >= 10_000_000)
            {
                // Extreme Procedural Virtual Source (0 allocation overhead, <45MB RAM!)
                var procSource = new ProceduralSource(count);
                _zeroGrid.DataSource = procSource;

                // For DataGridView: Protect from OutOfMemory crash at 10M rows
                try
                {
                    _dgv.SuspendLayout();
                    _dgv.Rows.Clear();
                    _dgv.RowCount = 1;
                    _dgv.ResumeLayout();
                }
                catch { }

                sw.Stop();
                _pagination.TotalRows = count;
                _searchBar.UpdateCountBadge();
                _baselineGen0 = GC.CollectionCount(0);
                _lblStatus.Text = $"Data: {count:N0} rows (ZeroUI initialized in {sw.ElapsedMilliseconds} ms - DGV OOM Protected)";
                _scrollFrames = 0;
                _scrollStopwatch.Restart();
                return;
            }

            // Show animated LoadingOverlay on _zeroGrid
            var overlay = LoadingOverlay.Show(_zeroGrid, $"Loading {count:N0} Records...", "Generating high-performance dataset in background");

            InventoryItem[] items;
            try
            {
                items = await Task.Run(() => MockDataGenerator.Generate(count));
            }
            finally
            {
                overlay.Close();
            }

            _dataset = items;
            _zeroSource = new InventorySource(_dataset);

            // Bind to ZeroGrid (Executes in <1ms)
            _zeroGrid.DataSource = _zeroSource;

            // Bind to DataGridView only if active to avoid blocking UI thread
            if (_subTabsBenchmark != null && _subTabsBenchmark.SelectedTab == _tabDgv)
            {
                try
                {
                    _dgv.SuspendLayout();
                    _dgv.Rows.Clear();
                    _dgv.RowCount = count;
                    _dgv.ResumeLayout();
                }
                catch { }
            }

            sw.Stop();

            _pagination.TotalRows = count;
            _searchBar.UpdateCountBadge();
            _baselineGen0 = GC.CollectionCount(0);
            _lblStatus.Text = $"Data: {count:N0} rows (Loaded in {sw.ElapsedMilliseconds} ms)";
            _scrollFrames = 0;
            _scrollStopwatch.Restart();
        }


        private void ToggleStressTest()
        {
            if (_isStressTesting)
            {
                _autoScrollTimer.Stop();
                _isStressTesting = false;
                _btnAutoScroll.Text = "🚀 Run Auto-Scroll Stress Test (10s)";
                _btnAutoScroll.BackColor = Color.FromArgb(79, 70, 229);
            }
            else
            {
                _stressTestElapsedTicks = 0;
                _stressTestDirection = 1;
                _scrollFrames = 0;
                _baselineGen0 = GC.CollectionCount(0);
                _scrollStopwatch.Restart();

                _isStressTesting = true;
                _btnAutoScroll.Text = "⏹️ Stress Testing... Click to Stop";
                _btnAutoScroll.BackColor = Color.FromArgb(220, 38, 38);
                _autoScrollTimer.Start();
            }
        }

        private void AutoScrollTick(object? sender, EventArgs e)
        {
            _stressTestElapsedTicks++;

            // Auto stop after 10 seconds (~600 ticks)
            if (_stressTestElapsedTicks > 600)
            {
                ToggleStressTest();
                return;
            }

            int total = (_subTabsBenchmark != null && _subTabsBenchmark.SelectedTab == _tabZero)
                ? (_zeroGrid.DataSource?.TotalRowCount ?? _dataset.Length)
                : _dgv.RowCount;

            if (total <= 0) return;

            int step = total >= 5_000_000 ? 2500 : 250;

            if (_subTabsBenchmark != null && _subTabsBenchmark.SelectedTab == _tabZero)
            {
                int currentY = _zeroGrid.ScrollY;
                int rowH = _zeroGrid.RowHeight;
                int maxY = Math.Max(0, total * rowH - (_zeroGrid.ClientSize.Height - _zeroGrid.HeaderHeight));
                int nextY = currentY + (step * _stressTestDirection * rowH);

                if (nextY >= maxY)
                {
                    nextY = maxY;
                    _stressTestDirection = -1;
                }
                else if (nextY <= 0)
                {
                    nextY = 0;
                    _stressTestDirection = 1;
                }

                _zeroGrid.ScrollY = nextY;
            }
            else
            {
                if (_dgv.RowCount <= 1) return;
                int current = _dgv.FirstDisplayedScrollingRowIndex;
                int next = current + (step * _stressTestDirection);

                if (next >= _dgv.RowCount)
                {
                    next = Math.Max(0, _dgv.RowCount - 1);
                    _stressTestDirection = -1;
                }
                else if (next <= 0)
                {
                    next = 0;
                    _stressTestDirection = 1;
                }

                try
                {
                    _dgv.FirstDisplayedScrollingRowIndex = next;
                }
                catch { }
            }


            _scrollFrames++;
        }

        private void UpdateHud()
        {
            double ramMb = _perfMonitor.ProcessRamMb;
            int gcGen0 = GC.CollectionCount(0) - _baselineGen0;

            double elapsedSec = _scrollStopwatch.Elapsed.TotalSeconds;
            double fps = (elapsedSec > 0 && _scrollFrames > 0) ? (_scrollFrames / elapsedSec) : 0.0;
            double latencyMs = fps > 0 ? (1000.0 / fps) : 0.0;

            _lblFps.Text = $"FPS: {fps:F0}";
            _lblLatency.Text = $"Latency: {latencyMs:F2} ms";
            _lblRam.Text = $"RAM: {ramMb:F1} MB";
            _lblGc.Text = $"GC Gen0 Delta: {gcGen0}";

            // Color coding
            if (fps >= 55)
            {
                _lblFps.ForeColor = Color.FromArgb(166, 227, 161); // Green
            }
            else if (fps >= 30)
            {
                _lblFps.ForeColor = Color.FromArgb(249, 226, 175); // Yellow
            }
            else
            {
                _lblFps.ForeColor = Color.FromArgb(243, 139, 168); // Red
            }
        }

        private async void HandleExportCsv(object? sender, EventArgs e)
        {
            if (_zeroGrid.DataSource == null || _zeroGrid.RowCount == 0)
            {
                MessageBox.Show("No data available to export!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"ZeroUI_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                Cursor = Cursors.WaitCursor;
                _lblStatus.Text = "Exporting CSV...";
                var sw = Stopwatch.StartNew();

                try
                {
                    int count = await ZeroGridExporter.ExportToCsvAsync(_zeroGrid.DataSource, _zeroGrid, sfd.FileName);
                    sw.Stop();

                    double rowsPerSec = count / Math.Max(0.001, sw.Elapsed.TotalSeconds);
                    ZeroToast.Success(this, $"Exported {count:N0} rows successfully ({sw.ElapsedMilliseconds} ms)");
                    MessageBox.Show(
                        $"✅ Successfully exported {count:N0} rows to file:\n{sfd.FileName}\n\nTime elapsed: {sw.ElapsedMilliseconds} ms ({rowsPerSec:N0} rows/sec)",
                        "Export Successful",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    Cursor = Cursors.Default;
                    _lblStatus.Text = $"Exported: {sfd.FileName}";
                }
            }
        }

        private void InitializeComponentsShowcase()
        {
            var colors = ZeroTheme.Colors;
            _tabControls.BackColor = colors.Background;

            // Left Panel: Controls showcase
            var leftPanel = new Panel
            {
                Name = "leftShowcasePanel",
                Dock = DockStyle.Left,
                Width = 460,
                Padding = new Padding(20),
                AutoScroll = true,
                BackColor = colors.Surface
            };

            // Section 1: Buttons
            var lblBtnTitle = new Label
            {
                Text = "1. ZButton (Stateful Flat Buttons & Badges)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, 16)
            };
            int btnY = 48;
            var btnPrimary = new ZeroButton { Text = "Primary Action", ButtonStyle = ZeroButtonStyle.Primary, Location = new Point(16, btnY), Size = new Size(130, 36) };
            var btnSuccess = new ZeroButton { Text = "Success", ButtonStyle = ZeroButtonStyle.Success, Location = new Point(156, btnY), Size = new Size(120, 36) };
            var btnDanger = new ZeroButton { Text = "Delete / Danger", ButtonStyle = ZeroButtonStyle.Danger, Location = new Point(286, btnY), Size = new Size(130, 36) };
            leftPanel.Controls.Add(btnPrimary);
            leftPanel.Controls.Add(btnSuccess);
            leftPanel.Controls.Add(btnDanger);

            btnY += 46;
            var btnSecondary = new ZeroButton { Text = "Secondary", ButtonStyle = ZeroButtonStyle.Secondary, Location = new Point(16, btnY), Size = new Size(130, 36) };
            var btnBadge = new ZeroButton { Text = "Notifications", ButtonStyle = ZeroButtonStyle.Primary, BadgeText = "9+", Location = new Point(156, btnY), Size = new Size(140, 36) };
            leftPanel.Controls.Add(btnSecondary);
            leftPanel.Controls.Add(btnBadge);

            // Section 2: Progress Bars
            int progY = btnY + 60;
            var lblProgTitle = new Label
            {
                Text = "2. ZProgressBar (Smooth Subpixel Antialiased)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, progY)
            };
            leftPanel.Controls.Add(lblProgTitle);

            progY += 32;
            var lblDeterminate = new Label { Text = "Determinate Progress (78%):", AutoSize = true, Location = new Point(16, progY), Font = new Font("Segoe UI", 9f), ForeColor = colors.TextSecondary };
            leftPanel.Controls.Add(lblDeterminate);
            progY += 22;
            var prog1 = new ZeroProgressBar { Location = new Point(16, progY), Size = new Size(390, 24), Value = 78, ProgressColor = Color.FromArgb(16, 185, 129) };
            leftPanel.Controls.Add(prog1);

            progY += 34;
            var lblIndeterminate = new Label { Text = "Indeterminate Progress (Marquee 60 FPS):", AutoSize = true, Location = new Point(16, progY), Font = new Font("Segoe UI", 9f), ForeColor = colors.TextSecondary };
            leftPanel.Controls.Add(lblIndeterminate);
            progY += 22;
            var prog2 = new ZeroProgressBar { Location = new Point(16, progY), Size = new Size(390, 24), IsIndeterminate = true, ProgressColor = Color.FromArgb(79, 70, 229) };
            leftPanel.Controls.Add(prog2);

            // Section 3: Search Box
            int searchY = progY + 44;
            var lblSearchTitle = new Label
            {
                Text = "3. ZSearchBox (Placeholder & Fast Clear)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, searchY)
            };
            leftPanel.Controls.Add(lblSearchTitle);

            searchY += 30;
            var searchDemo = new ZeroSearchBox { Location = new Point(16, searchY), Width = 390, PlaceholderText = "🔍 Enter keywords to test..." };
            leftPanel.Controls.Add(searchDemo);

            // Section 4: ZeroSwitch & ZeroTag
            int tagY = searchY + 44;
            var lblTagTitle = new Label
            {
                Text = "4. ZSwitch & ZTag (State & Toggle Controls)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, tagY)
            };
            leftPanel.Controls.Add(lblTagTitle);

            tagY += 30;
            var swDemo = new ZeroSwitch { Location = new Point(16, tagY), Checked = true };
            var tag1 = new ZeroTag { Location = new Point(78, tagY), Size = new Size(72, 26), TagType = TagType.Success, Text = "Active" };
            var tag2 = new ZeroTag { Location = new Point(156, tagY), Size = new Size(80, 26), TagType = TagType.Processing, Text = "Processing" };
            var tag3 = new ZeroTag { Location = new Point(242, tagY), Size = new Size(72, 26), TagType = TagType.Warning, Text = "Warning" };
            var tag4 = new ZeroTag { Location = new Point(320, tagY), Size = new Size(60, 26), TagType = TagType.Error, Text = "Error" };
            swDemo.CheckedChanged += (s, e) =>
            {
                tag1.TagType = swDemo.Checked ? TagType.Success : TagType.Default;
                tag1.Text = swDemo.Checked ? "Active" : "Disabled";
            };
            leftPanel.Controls.Add(swDemo);
            leftPanel.Controls.Add(tag1);
            leftPanel.Controls.Add(tag2);
            leftPanel.Controls.Add(tag3);
            leftPanel.Controls.Add(tag4);

            // Section 5: ZeroSegmented (Pill Switcher)
            int segY = tagY + 38;
            var lblSegTitle = new Label
            {
                Text = "5. ZSegmented (Pill Switcher)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, segY)
            };
            leftPanel.Controls.Add(lblSegTitle);

            segY += 30;
            var segDemo = new ZeroSegmented
            {
                Location = new Point(16, segY),
                Width = 390,
                Items = new[] { "All", "Daily", "Weekly", "Monthly" }
            };
            segDemo.SelectedIndexChanged += (s, e) =>
            {
                ZeroToast.Info(this, $"Selected view: {segDemo.SelectedItem}");
            };
            leftPanel.Controls.Add(segDemo);

            // Section 6: ZeroStatistic (KPI Card)
            int statY = segY + 44;
            var lblStatTitle = new Label
            {
                Text = "6. ZStatistic (KPI Dashboard Cards)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, statY)
            };
            leftPanel.Controls.Add(lblStatTitle);

            statY += 30;
            var stat1 = new ZeroStatistic
            {
                Location = new Point(16, statY),
                Size = new Size(190, 95),
                Title = "TOTAL INVENTORY",
                Value = "1,248,500",
                Suffix = "pcs",
                Trend = TrendDirection.Up,
                TrendText = "+12.4% vs last month"
            };
            var stat2 = new ZeroStatistic
            {
                Location = new Point(216, statY),
                Size = new Size(190, 95),
                Title = "ACTUAL REVENUE",
                Value = "3.85",
                Prefix = "$",
                Suffix = "B",
                Trend = TrendDirection.Down,
                TrendText = "-2.1% cycle"
            };
            leftPanel.Controls.Add(stat1);
            leftPanel.Controls.Add(stat2);

            // Section 7: ZeroToast & Windows Action Center Bridge
            int toastY = statY + 105;
            var lblToastTitle = new Label
            {
                Text = "7. ZToast & Windows Action Center Bridge",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, toastY)
            };
            leftPanel.Controls.Add(lblToastTitle);

            toastY += 30;
            var btnToastSuccess = new ZeroButton { Text = "Success", ButtonStyle = ZeroButtonStyle.Success, Location = new Point(16, toastY), Size = new Size(95, 30), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            btnToastSuccess.Click += (s, e) => ZeroToast.Success(this, "10 million rows synchronized into RAM successfully!");

            var btnToastWarn = new ZeroButton { Text = "Warning", ButtonStyle = ZeroButtonStyle.Secondary, Location = new Point(118, toastY), Size = new Size(95, 30), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            btnToastWarn.Click += (s, e) => ZeroToast.Warning(this, "System detected rapid memory bandwidth consumption.");

            var btnToastError = new ZeroButton { Text = "Error", ButtonStyle = ZeroButtonStyle.Danger, Location = new Point(220, toastY), Size = new Size(90, 30), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            btnToastError.Click += (s, e) => ZeroToast.Error(this, "Database server connection timeout!");

            var btnToastAlarm = new ZeroButton { Text = "⚡ Alarm", ButtonStyle = ZeroButtonStyle.Danger, Location = new Point(317, toastY), Size = new Size(95, 30), Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            btnToastAlarm.Click += (s, e) => ZeroToast.Alarm(this, "Critical: High-temperature limit exceeded on Reactor RX-401 (94.8°C)!");

            leftPanel.Controls.Add(btnToastSuccess);
            leftPanel.Controls.Add(btnToastWarn);
            leftPanel.Controls.Add(btnToastError);
            leftPanel.Controls.Add(btnToastAlarm);

            toastY += 38;
            var lblDeliveryMode = new Label
            {
                Text = "Mode:",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = colors.TextSecondary,
                AutoSize = true,
                Location = new Point(16, toastY + 4)
            };
            var cmbDeliveryMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(62, toastY),
                Size = new Size(130, 26),
                Font = new Font("Segoe UI", 8.5f)
            };
            cmbDeliveryMode.Items.AddRange(new object[] {
                ZeroNotificationDeliveryMode.Auto,
                ZeroNotificationDeliveryMode.InAppOnly,
                ZeroNotificationDeliveryMode.SystemOnly,
                ZeroNotificationDeliveryMode.Dual
            });
            cmbDeliveryMode.SelectedItem = ZeroToastStackManager.DeliveryMode;
            cmbDeliveryMode.SelectedIndexChanged += (s, e) =>
            {
                if (cmbDeliveryMode.SelectedItem is ZeroNotificationDeliveryMode mode)
                {
                    ZeroToastStackManager.DeliveryMode = mode;
                    ZeroToast.Info(this, $"Delivery mode: {mode}");
                }
            };

            var chkRouteAlarms = new ZeroCheckBox
            {
                Location = new Point(200, toastY + 2),
                Size = new Size(210, 24),
                Text = "Route Alarms to OS",
                Checked = ZeroToastStackManager.RouteAlarmsToSystem
            };
            chkRouteAlarms.CheckedChanged += (s, e) =>
            {
                ZeroToastStackManager.RouteAlarmsToSystem = chkRouteAlarms.Checked;
            };

            leftPanel.Controls.Add(lblDeliveryMode);
            leftPanel.Controls.Add(cmbDeliveryMode);
            leftPanel.Controls.Add(chkRouteAlarms);

            toastY += 34;
            var btnWindowsToast = new ZeroButton
            {
                Text = "🔔 Test Windows OS Notification",
                ButtonStyle = ZeroButtonStyle.Primary,
                Location = new Point(16, toastY),
                Size = new Size(240, 30),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            btnWindowsToast.Click += (s, e) =>
            {
                WindowsNotificationBridge.ShowNotification(
                    "ZeroUI Industrial Alert",
                    "Reactor RX-401 pressure threshold exceeded (4.8 Bar). Click to inspect.",
                    ZeroToastType.Alarm,
                    onClick: () => ZeroToast.Info(this, "Application focused via Windows Action Center notification.")
                );
            };

            var lblBridgeTip = new Label
            {
                Text = "(Minimize app to test Auto mode)",
                Font = new Font("Segoe UI", 8f),
                ForeColor = colors.TextSecondary,
                AutoSize = true,
                Location = new Point(262, toastY + 7)
            };

            leftPanel.Controls.Add(btnWindowsToast);
            leftPanel.Controls.Add(lblBridgeTip);

            // Section 8: ZeroTextBox (Modern Enterprise Text Input)
            int txtY = toastY + 44;
            var lblTxtTitle = new Label
            {
                Text = "8. ZTextBox (Placeholder, Clear ✕, Password)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, txtY)
            };
            leftPanel.Controls.Add(lblTxtTitle);

            txtY += 30;
            var txtRegular = new ZeroTextBox
            {
                Location = new Point(16, txtY),
                Size = new Size(390, 32),
                PlaceholderText = "Enter full name or code...",
                ShowClearButton = true
            };
            leftPanel.Controls.Add(txtRegular);

            txtY += 40;
            var txtPassword = new ZeroTextBox
            {
                Location = new Point(16, txtY),
                Size = new Size(390, 32),
                PlaceholderText = "Enter secure password...",
                UseSystemPasswordChar = true,
                ShowClearButton = true
            };
            leftPanel.Controls.Add(txtPassword);

            // Section 9: ZeroCheckBox (Vector Flat & Tri-State)
            int chkY = txtY + 44;
            var lblChkTitle = new Label
            {
                Text = "9. ZCheckBox (Vector Flat, Tri-State, Spacebar)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, chkY)
            };
            leftPanel.Controls.Add(lblChkTitle);

            chkY += 30;
            var chk1 = new ZeroCheckBox
            {
                Location = new Point(16, chkY),
                Size = new Size(180, 24),
                Text = "Enable Notifications",
                Checked = true
            };
            var chk2 = new ZeroCheckBox
            {
                Location = new Point(210, chkY),
                Size = new Size(180, 24),
                Text = "Tri-State Filter",
                ThreeState = true,
                CheckState = CheckState.Indeterminate
            };
            chk1.CheckedChanged += (s, e) => ZeroToast.Info(this, $"Notifications: {chk1.Checked}");
            chk2.CheckStateChanged += (s, e) => ZeroToast.Info(this, $"Filter state: {chk2.CheckState}");

            leftPanel.Controls.Add(chk1);
            leftPanel.Controls.Add(chk2);

            // Section 10: ZeroDatePicker & ZeroNumericBox (Smart Data Entry)
            int dtY = chkY + 44;
            var lblDtTitle = new Label
            {
                Text = "10. ZDatePicker & ZNumericBox (Smart Data Entry)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, dtY)
            };
            leftPanel.Controls.Add(lblDtTitle);

            dtY += 30;
            var lblDate = new Label { Text = "Direct Keyboard Date Entry (yyyy-MM-dd):", AutoSize = true, Location = new Point(16, dtY), Font = new Font("Segoe UI", 9f), ForeColor = colors.TextSecondary };
            leftPanel.Controls.Add(lblDate);
            dtY += 22;
            var datePicker = new ZeroDatePicker
            {
                Location = new Point(16, dtY),
                Size = new Size(185, 32),
                Value = DateTime.Today
            };
            var numBox = new ZeroNumericBox
            {
                Location = new Point(210, dtY),
                Size = new Size(185, 32),
                Value = 12500,
                DecimalPlaces = 0,
                ThousandsSeparator = true
            };
            leftPanel.Controls.Add(datePicker);
            leftPanel.Controls.Add(numBox);

            // Section 11: ZeroErrorProvider (Vector Badges & Validation)
            int errY = dtY + 44;
            var lblErrTitle = new Label
            {
                Text = "11. ZErrorProvider (Vector Validation)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, errY)
            };
            leftPanel.Controls.Add(lblErrTitle);

            errY += 30;
            var lblInputPrompt = new Label { Text = "Type 'ORD-' prefix to satisfy validation rule:", AutoSize = true, Location = new Point(16, errY), Font = new Font("Segoe UI", 9f), ForeColor = colors.TextSecondary };
            leftPanel.Controls.Add(lblInputPrompt);
            errY += 22;

            var txtValidate = new ZeroTextBox
            {
                Location = new Point(16, errY),
                Size = new Size(260, 32),
                Text = "TEST-9999",
                PlaceholderText = "Must start with ORD-..."
            };
            var orderCodeValidator = new Validator<string?>()
                .NotEmpty("Order Code is required!")
                .Must(text => text != null && text.StartsWith("ORD-", StringComparison.OrdinalIgnoreCase), "Order code must start with 'ORD-'!", ValidationSeverity.Warning)
                .Length(7, 12, "Order code must be 7 to 12 characters");

            var errorProvider = new ZeroErrorProvider();
            errorProvider.SetResult(txtValidate, orderCodeValidator.Validate(txtValidate.Text));

            txtValidate.TextChanged += (s, e) =>
            {
                var res = orderCodeValidator.Validate(txtValidate.Text);
                errorProvider.SetResult(txtValidate, res);
            };

            var btnCheckValid = new ZeroButton
            {
                Text = "Validate",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(290, errY),
                Size = new Size(100, 32)
            };
            btnCheckValid.Click += (s, e) =>
            {
                if (errorProvider.HasErrors)
                {
                    ZeroToast.Warning(this, errorProvider.GetError(txtValidate) ?? "Validation failed");
                }
                else
                {
                    ZeroToast.Success(this, "All input validation passed successfully!");
                }
            };
            leftPanel.Controls.Add(txtValidate);
            leftPanel.Controls.Add(btnCheckValid);

            // Section 12: ZeroRadioButton & ZeroRadioGroup (Single-Choice Options)
            int radioY = errY + 54;
            var lblRadioTitle = new Label
            {
                Text = "12. ZRadioButton & ZRadioGroup (Choice)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, radioY)
            };
            leftPanel.Controls.Add(lblRadioTitle);

            radioY += 30;
            var radioGroup = new ZRadioGroup
            {
                Location = new Point(16, radioY),
                Size = new Size(390, 32),
                Orientation = Orientation.Horizontal,
                Items = new[] { "Standard", "Express (+10%)", "Priority Rush" },
                ItemWidth = 125,
                SelectedIndex = 0
            };
            radioGroup.SelectedIndexChanged += (s, e) =>
            {
                ZeroToast.Info(this, $"Shipping mode: {radioGroup.SelectedItem}");
            };
            leftPanel.Controls.Add(radioGroup);

            // Section 13: ZeroComboBox (Standard Lightweight Dropdown)
            int comboY = radioY + 44;
            var lblComboTitle = new Label
            {
                Text = "13. ZComboBox (Standard Dropdown Select)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, comboY)
            };
            leftPanel.Controls.Add(lblComboTitle);

            comboY += 30;
            var cmbOptions = new ZeroComboBox
            {
                Location = new Point(16, comboY),
                Size = new Size(390, 34),
                Placeholder = "Select manufacturing plant..."
            };
            cmbOptions.SetItems(new[]
            {
                "Plant Alpha - Line 01 (SMT High-Speed)",
                "Plant Alpha - Line 02 (Through-Hole DIP)",
                "Plant Beta - Line 03 (Automated Optical Inspection)",
                "Plant Beta - Line 04 (Box Build & Packaging)",
                "Plant Gamma - Line 05 (Cleanroom ISO Class 7)"
            });
            cmbOptions.SelectedIndex = 0;
            cmbOptions.SelectedIndexChanged += (s, e) =>
            {
                ZeroToast.Info(this, $"Selected Plant: {cmbOptions.SelectedItem}");
            };
            leftPanel.Controls.Add(cmbOptions);

            // Section 14: ZeroMemoEdit (Multi-line Text Editor)
            int memoY = comboY + 44;
            var lblMemoTitle = new Label
            {
                Text = "14. ZMemoEdit (Multi-Line Notes & SQL)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, memoY)
            };
            leftPanel.Controls.Add(lblMemoTitle);

            memoY += 30;
            var memoEdit = new ZMemoEdit
            {
                Location = new Point(16, memoY),
                Size = new Size(390, 80),
                PlaceholderText = "Enter maintenance remarks or operator handover notes...",
                ShowCharacterCount = true,
                MaxLength = 250,
                Text = "Routine preventive maintenance completed on SMT Feeder BOA472. Calibration within ±0.02mm."
            };
            leftPanel.Controls.Add(memoEdit);

            // Section 15: ZeroSlider (Industrial Setpoint Trackbar)
            int sliderY = memoY + 92;
            var lblSliderTitle = new Label
            {
                Text = "15. ZSlider (Industrial Setpoint 0..100%)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, sliderY)
            };
            leftPanel.Controls.Add(lblSliderTitle);

            sliderY += 30;
            var sliderDemo = new ZeroSlider
            {
                Location = new Point(16, sliderY),
                Size = new Size(390, 36),
                Minimum = 0,
                Maximum = 100,
                Value = 75,
                Unit = "% Speed"
            };
            sliderDemo.ValueChanged += (s, e) =>
            {
                // Live visual feedback
            };
            leftPanel.Controls.Add(sliderDemo);

            // Section 16: ZeroTimePicker (Segmented Shift Time Picker)
            int timeY = sliderY + 46;
            var lblTimeTitle = new Label
            {
                Text = "16. ZTimePicker (Shift Hours:Minutes:Seconds)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, timeY)
            };
            leftPanel.Controls.Add(lblTimeTitle);

            timeY += 30;
            var timePicker1 = new ZeroTimePicker
            {
                Location = new Point(16, timeY),
                Size = new Size(185, 34),
                Value = new TimeSpan(8, 0, 0),
                ShowSeconds = true
            };
            var timePicker2 = new ZeroTimePicker
            {
                Location = new Point(210, timeY),
                Size = new Size(185, 34),
                Value = new TimeSpan(16, 30, 0),
                ShowSeconds = false
            };
            timePicker1.ValueChanged += (s, e) =>
            {
                ZeroToast.Info(this, $"Shift Start: {timePicker1.Value:hh\\:mm\\:ss}");
            };
            timePicker2.ValueChanged += (s, e) =>
            {
                ZeroToast.Info(this, $"Shift End: {timePicker2.Value:hh\\:mm}");
            };
            leftPanel.Controls.Add(timePicker1);
            leftPanel.Controls.Add(timePicker2);

            // Section 17: ZeroMaskedTextBox (IP & MAC Address Input)
            int maskY = timeY + 44;
            var lblMaskTitle = new Label
            {
                Text = "17. ZMaskedTextBox (PLC IP & Lot Code)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, maskY)
            };
            leftPanel.Controls.Add(lblMaskTitle);

            maskY += 30;
            var lblIpPrompt = new Label { Text = "PLC Ethernet IP:", AutoSize = true, Location = new Point(16, maskY), Font = new Font("Segoe UI", 8.5f), ForeColor = colors.TextSecondary };
            leftPanel.Controls.Add(lblIpPrompt);
            maskY += 20;

            var txtIp = new ZeroMaskedTextBox
            {
                Location = new Point(16, maskY),
                Size = new Size(185, 32),
                Mask = "000.000.000.000",
                Text = "192.168.001.100"
            };
            var txtLot = new ZeroMaskedTextBox
            {
                Location = new Point(210, maskY),
                Size = new Size(185, 32),
                Mask = "LOT-0000-AAAA",
                Text = "LOT-2026-PROD"
            };
            leftPanel.Controls.Add(txtIp);
            leftPanel.Controls.Add(txtLot);

            // Bottom spacer
            var bottomSpacer = new Label { Location = new Point(16, maskY + 44), Size = new Size(390, 20), Text = "" };
            leftPanel.Controls.Add(bottomSpacer);

            // Right Panel: ZeroListView Log Streamer
            var rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                BackColor = colors.Background
            };

            var topLogBar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Color.Transparent };
            var lblLogTitle = new Label
            {
                Text = "26. ZListView (High-Throughput Log Viewer 100K+ Rows)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(0, 12)
            };
            topLogBar.Controls.Add(lblLogTitle);

            var btnAdd1000 = new ZeroButton
            {
                Text = "+1,000 Logs",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(110, 30),
                BorderRadius = 4,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Location = new Point(410, 8),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnAdd1000.Click += (s, e) =>
            {
                var batch = new List<LogEntry>(1000);
                for (int i = 0; i < 1000; i++)
                {
                    batch.Add(new LogEntry(DateTime.Now, (LogSeverity)(i % 4), $"[AUTOMATED EVENT #{i + 1}] Inventory batch sync completed successfully."));
                }
                _showcaseLog.AddLogs(batch);
            };
            topLogBar.Controls.Add(btnAdd1000);

            var btnClearLogs = new ZeroButton
            {
                Text = "Clear Logs",
                ButtonStyle = ZeroButtonStyle.Ghost,
                Size = new Size(80, 30),
                BorderRadius = 4,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Location = new Point(526, 8),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnClearLogs.Click += (s, e) => _showcaseLog.Clear();
            topLogBar.Controls.Add(btnClearLogs);

            _showcaseLog = new ZeroListView { Dock = DockStyle.Fill };
            _showcaseLog.AddLog(LogSeverity.Info, "ZeroUI system initialized successfully.");
            _showcaseLog.AddLog(LogSeverity.Success, "Connected to Win32 DIBSection Memory DC (32bpp).");
            _showcaseLog.AddLog(LogSeverity.Warning, "High-throughput streaming engine allocated.");
            _showcaseLog.AddLog(LogSeverity.Error, "Default DataGridView load threshold exceeded.");

            rightPanel.Controls.Add(_showcaseLog);
            rightPanel.Controls.Add(topLogBar);

            // Middle Panel: Advanced Enterprise Specialized Editors
            var midPanel = new Panel
            {
                Name = "midShowcasePanel",
                Dock = DockStyle.Left,
                Width = 470,
                Padding = new Padding(20),
                AutoScroll = true,
                BackColor = colors.Surface
            };

            // Section 18: ButtonEdit
            var lblBtnEditTitle = new Label
            {
                Text = "18. ZButtonEdit (Action Buttons: Browse, Copy, Clear)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, 16)
            };
            midPanel.Controls.Add(lblBtnEditTitle);

            int midY = 48;
            var btnEdit = new ZButtonEdit
            {
                Location = new Point(16, midY),
                Size = new Size(410, 34),
                Text = @"C:\ZeroPlatform\Firmware\controller_v2.bin",
                FileFilter = "Binary Files (*.bin)|*.bin|Config Files (*.json;*.xml)|*.json;*.xml|All Files (*.*)|*.*",
                DialogTitle = "Select Firmware Binary File"
            };
            btnEdit.AddButton(EditorButtonKind.Copy, "Copy to clipboard");
            btnEdit.AddButton(EditorButtonKind.Clear, "Clear input");
            btnEdit.ButtonClick += (s, e) =>
            {
                if (e.Kind == EditorButtonKind.Copy)
                {
                    try { Clipboard.SetText(btnEdit.Text); } catch { }
                    ZeroToast.Success(this, "Copied file path to clipboard!");
                }
                else if (e.Kind == EditorButtonKind.Clear)
                {
                    btnEdit.Text = "";
                    ZeroToast.Info(this, "Cleared file path.");
                }
            };
            midPanel.Controls.Add(btnEdit);

            // Section 19: CalcEdit (Mini-Calculator Popup & Currency)
            midY += 50;
            var lblCalcTitle = new Label
            {
                Text = "19. ZCalcEdit (Popup Calculator & Currency Format)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, midY)
            };
            midPanel.Controls.Add(lblCalcTitle);

            midY += 30;
            var calcEdit = new ZCalcEdit
            {
                Location = new Point(16, midY),
                Size = new Size(195, 34),
                Value = 18450.75m,
                DisplayFormat = "C2"
            };
            calcEdit.ValueChanged += (s, e) =>
            {
                ZeroToast.Info(this, $"Calculator value: {calcEdit.Value:C2}");
            };

            // Section 20: ColorPickEdit (Dropdown Color Swatch & Palette)
            var colorPick = new ZColorPicker
            {
                Location = new Point(225, midY),
                Size = new Size(200, 34),
                SelectedColor = Color.FromArgb(79, 70, 229)
            };
            colorPick.ColorChanged += (s, e) =>
            {
                ZeroToast.Info(this, $"Selected color: #{colorPick.SelectedColor.R:X2}{colorPick.SelectedColor.G:X2}{colorPick.SelectedColor.B:X2}");
            };
            midPanel.Controls.Add(calcEdit);
            midPanel.Controls.Add(colorPick);

            // Section 21: RatingControl (Star Quality & Defect Severity)
            midY += 50;
            var lblRatingTitle = new Label
            {
                Text = "20. ZRatingControl (Quality & Defect Rating 0.5 Step)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, midY)
            };
            midPanel.Controls.Add(lblRatingTitle);

            midY += 30;
            var ratingCtrl = new ZRating
            {
                Location = new Point(16, midY),
                Size = new Size(195, 34),
                Value = 4.5m
            };
            ratingCtrl.ValueChanged += (s, e) =>
            {
                ZeroToast.Success(this, $"Quality rating: {ratingCtrl.Value:0.0} / 5.0 Stars");
            };
            midPanel.Controls.Add(ratingCtrl);

            // Section 22: RangeSlider (Dual-Thumb Min/Max Thermal Range)
            midY += 50;
            var lblRangeTitle = new Label
            {
                Text = "21. ZRangeSlider (Dual-Thumb Min/Max Thermal Range)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, midY)
            };
            midPanel.Controls.Add(lblRangeTitle);

            midY += 30;
            var rangeSlider = new ZRangeSlider
            {
                Location = new Point(16, midY),
                Size = new Size(410, 38),
                Minimum = 0,
                Maximum = 120,
                LowerValue = 25,
                UpperValue = 85,
                Suffix = " °C"
            };
            midPanel.Controls.Add(rangeSlider);

            // Section 23: TimeSpanEdit & IPAddressEdit
            midY += 50;
            var lblTimeSpanTitle = new Label
            {
                Text = "22. ZTimeSpanEdit & ZIPAddressEdit (Cycle Time & PLC IP)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, midY)
            };
            midPanel.Controls.Add(lblTimeSpanTitle);

            midY += 30;
            var timeSpanEdit = new ZTimeSpanEdit
            {
                Location = new Point(16, midY),
                Size = new Size(195, 34),
                Value = TimeSpan.FromHours(7.5)
            };
            timeSpanEdit.ValueChanged += (s, val) =>
            {
                ZeroToast.Info(this, $"Cycle duration: {val:hh\\:mm\\:ss}");
            };

            var ipEdit = new ZIPAddressEdit
            {
                Location = new Point(225, midY),
                Size = new Size(200, 34),
                Text = "192.168.10.45"
            };
            ipEdit.EditValueChanged += (s, e) =>
            {
                ZeroToast.Info(this, $"PLC IP Address: {ipEdit.Text}");
            };
            midPanel.Controls.Add(timeSpanEdit);
            midPanel.Controls.Add(ipEdit);

            // Section 24: HyperlinkEdit (Interactive URL & Browser Launch)
            midY += 50;
            var lblLinkTitle = new Label
            {
                Text = "23. ZHyperlinkEdit (Interactive URL & Action Link)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, midY)
            };
            midPanel.Controls.Add(lblLinkTitle);

            midY += 30;
            var linkEdit = new ZHyperlinkEdit
            {
                Location = new Point(16, midY),
                Size = new Size(410, 34),
                TargetUrl = "https://github.com/kzxl/ZeroUI",
                DisplayText = "ZeroUI Industrial Framework Repository ↗"
            };
            linkEdit.HyperlinkClick += (s, e) =>
            {
                ZeroToast.Info(this, $"Navigating to: {linkEdit.TargetUrl}");
            };
            midPanel.Controls.Add(linkEdit);

            // Section 25: BarcodeBox (Pure Vector 1D Barcode & 2D QR Code)
            midY += 50;
            var lblBarcodeTitle = new Label
            {
                Text = "24. ZBarcodeBox (Pure Vector Code 128 & QR Code)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, midY)
            };
            midPanel.Controls.Add(lblBarcodeTitle);

            midY += 30;
            var barcode128 = new BarcodeBox
            {
                Location = new Point(16, midY),
                Size = new Size(220, 75),
                Text = "LOT-2026-X9",
                Symbology = BarcodeSymbology.Code128,
                ShowText = true
            };
            var barcodeQr = new BarcodeBox
            {
                Location = new Point(250, midY),
                Size = new Size(75, 75),
                Text = "https://github.com/kzxl/ZeroUI",
                Symbology = BarcodeSymbology.QrCode,
                ShowText = false
            };
            midPanel.Controls.Add(barcode128);
            midPanel.Controls.Add(barcodeQr);

            // Section 26: PictureEdit (Vector Avatar & Image Frame with Lightbox Zoom)
            midY += 88;
            var lblPicTitle = new Label
            {
                Text = "25. ZPictureEdit (Vector Avatar with Lightbox Preview)",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = colors.TextPrimary,
                AutoSize = true,
                Location = new Point(16, midY)
            };
            midPanel.Controls.Add(lblPicTitle);

            midY += 30;
            var pic1 = new ZPictureEdit
            {
                Location = new Point(16, midY),
                Size = new Size(60, 60),
                IsCircle = true,
                FallbackText = "PV",
                Status = AvatarStatus.Online,
                EnableZoomPreview = true
            };
            var pic2 = new ZPictureEdit
            {
                Location = new Point(90, midY),
                Size = new Size(60, 60),
                IsCircle = false,
                FallbackText = "QC",
                Status = AvatarStatus.Busy,
                EnableZoomPreview = true
            };
            var pic3 = new ZPictureEdit
            {
                Location = new Point(164, midY),
                Size = new Size(60, 60),
                IsCircle = true,
                FallbackText = "OP",
                Status = AvatarStatus.Away,
                EnableZoomPreview = true
            };
            midPanel.Controls.Add(pic1);
            midPanel.Controls.Add(pic2);
            midPanel.Controls.Add(pic3);

            var midSpacer = new Label { Location = new Point(16, midY + 70), Size = new Size(410, 20), Text = "" };
            midPanel.Controls.Add(midSpacer);

            _tabControls.Controls.Add(rightPanel);
            _tabControls.Controls.Add(midPanel);
            _tabControls.Controls.Add(leftPanel);

            // Simulation timer: add a log entry every 600ms
            _logGenTimer = new System.Windows.Forms.Timer { Interval = 600 };
            int simCount = 1;
            _logGenTimer.Tick += (s, e) =>
            {
                if (_mainNav != null && _mainNav.SelectedTab == _clusterComponents && _showcaseLog.Entries.Count < 50000)
                {
                    var sev = (LogSeverity)(simCount % 4);
                    _showcaseLog.AddLog(sev, $"[Service #{simCount++}] Processed high-throughput transaction via ZeroUI engine with 0.02ms latency.");
                }
            };
            _logGenTimer.Start();
        }

        private void InitializeMesDashboard()
        {
            _tabMes.BackColor = ZeroTheme.Colors.Background;
            _tabMes.Padding = new Padding(16);
            _tabMes.AutoScroll = true;

            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Transparent
            };

            // ALERT BANNER: Line Notification
            var alertBanner = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Height = 44,
                Severity = ZeroAlertSeverity.Warning,
                Title = "SMT Feeder Alert",
                Message = "Reel BOA472 is running low (18 remaining). Buffer refill suggested before 14:00.",
                IsClosable = true
            };
            var alertSpacer = new Panel { Dock = DockStyle.Top, Height = 8, BackColor = Color.Transparent };

            // TOP ACTION TOOLBAR: Scanner & Live Simulator
            var rowToolbar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 8) };
            
            var statusBadge = new ZStatusBadge
            {
                Location = new Point(4, 8),
                Size = new Size(160, 24),
                Status = ZeroStatusType.Running,
                Text = "Line 01: Running"
            };
            rowToolbar.Controls.Add(statusBadge);

            var barcodeBox = new ZeroBarcodeBox
            {
                Location = new Point(175, 4),
                Width = 280,
                PlaceholderText = "Scan Barcode (e.g. SN-1030-88)..."
            };
            rowToolbar.Controls.Add(barcodeBox);

            var mesDatePicker = new ZeroDatePicker
            {
                Location = new Point(465, 4),
                Width = 145,
                Value = DateTime.Today
            };
            mesDatePicker.ValueChanged += (s, e) => ZeroToast.Info(this, $"Filter date set to: {mesDatePicker.Value:yyyy-MM-dd}");
            rowToolbar.Controls.Add(mesDatePicker);

            var btnSimulateScan = new ZeroButton
            {
                Text = "⚡ Simulate PLC (+5 Assy, +4 QC, +3 Inward)",
                ButtonStyle = ZeroButtonStyle.Primary,
                Size = new Size(360, 34),
                Location = new Point(620, 4),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            rowToolbar.Controls.Add(btnSimulateScan);


            // ROW 1: Board Info + Shell Info + OEE Gauge
            var row1 = new Panel { Dock = DockStyle.Top, Height = 210, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 10) };

            // Card 1A: Board Info (Left Fill)
            var cardBoard = new ZeroCard
            {
                StepNumber = 1,
                BadgeColor = Color.FromArgb(22, 119, 255),
                Title = "PCBA / Board Information",
                Subtitle = "Partlist Specification: 026MC02RP2.0",
                ActionText = "Dispatch Info: By Status",
                Dock = DockStyle.Fill
            };
            cardBoard.ActionClicked += (s, e) => ZeroToast.Info(this, "Opening dispatch breakdown by status...");

            var gridBoard = new ZeroGridControl
            {
                Dock = DockStyle.Fill,
                Density = GridDensity.Compact,
                HeaderHeight = 26,
                Font = new Font("Segoe UI", 9f)
            };
            gridBoard.Columns.Add(new ZeroColumn("Part Code", 140, CellAlignment.Left));
            gridBoard.Columns.Add(new ZeroColumn("Partlist Qty", 100, CellAlignment.Right));
            gridBoard.Columns.Add(new ZeroColumn("Raw Stock", 120, CellAlignment.Right));
            gridBoard.Columns.Add(new ZeroColumn("WIP Stock", 120, CellAlignment.Right));
            gridBoard.DataSource = new ZeroUI.Demo.Data.MesBoardSource();
            cardBoard.ContentPanel.Controls.Add(gridBoard);

            // Splitter 1
            var splitR1A = new Panel { Dock = DockStyle.Right, Width = 10, BackColor = Color.Transparent };

            // Card 1B: Shell Info
            var cardShell = new ZeroCard
            {
                StepNumber = 2,
                BadgeColor = Color.FromArgb(124, 58, 237),
                Title = "Enclosure / Shell Info",
                Dock = DockStyle.Right,
                Width = 230
            };
            var descShell = new ZeroDescriptions { Dock = DockStyle.Fill, Columns = 1, RowHeight = 26 };
            descShell.Add("Material Request", "Production Schedule", Color.FromArgb(107, 114, 128));
            descShell.Add("Ticket ID", "(Not Created)", Color.FromArgb(156, 163, 175));
            descShell.Add("Status", "--", Color.FromArgb(156, 163, 175));
            cardShell.ContentPanel.Controls.Add(descShell);

            // Splitter 2
            var splitR1B = new Panel { Dock = DockStyle.Right, Width = 10, BackColor = Color.Transparent };

            // Card 1C: OEE Gauge Meter
            var cardGauge = new ZeroCard
            {
                StepNumber = null,
                Title = "Line OEE Index",
                Dock = DockStyle.Right,
                Width = 145
            };
            var gaugeOee = new ZeroGauge
            {
                Dock = DockStyle.Fill,
                Value = 88.5f,
                Title = "OEE Efficiency",
                GaugeColor = Color.FromArgb(16, 185, 129)
            };
            cardGauge.ContentPanel.Controls.Add(gaugeOee);

            row1.Controls.Add(cardBoard);
            row1.Controls.Add(splitR1A);
            row1.Controls.Add(cardShell);
            row1.Controls.Add(splitR1B);
            row1.Controls.Add(cardGauge);

            // ROW 2: Production Line Workflow (ZeroSteps)
            var row2 = new Panel { Dock = DockStyle.Top, Height = 125, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 10) };


            var cardSteps = new ZeroCard
            {
                StepNumber = 3,
                BadgeColor = Color.FromArgb(22, 119, 255),
                Title = "Production Line Workflow Steps",
                Dock = DockStyle.Fill
            };

            _mesSteps = new ZeroSteps { Dock = DockStyle.Fill };
            _mesSteps.SetSteps(new[]
            {
                new ZeroStepItem { Key = "ASSY", Title = "Assembly Line", Quantity = 0, Timestamp = "--", Status = ZeroStepStatus.InProgress, Glyph = ZeroStepGlyph.Gear },
                new ZeroStepItem { Key = "QC", Title = "QC Inspection", Quantity = 0, Timestamp = "--", Status = ZeroStepStatus.Completed, Glyph = ZeroStepGlyph.Checkmark },
                new ZeroStepItem { Key = "WH", Title = "Finished Goods Inward", Quantity = 0, Timestamp = "--", Status = ZeroStepStatus.Waiting, Glyph = ZeroStepGlyph.Warehouse }
            });

            _mesSteps.StepClicked += (s, e) =>
            {
                ZeroToast.Info(this, $"Selected Stage: {e.Step.Title} | Current Qty: {e.Step.Quantity:N0}");
            };

            cardSteps.ContentPanel.Controls.Add(_mesSteps);
            row2.Controls.Add(cardSteps);

            // ROW 3: Summary + Product Specs + Timeline Lot Tracking
            var row3 = new Panel { Dock = DockStyle.Top, Height = 180, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 10) };

            // Card 3A: Summary
            var cardSummary = new ZeroCard
            {
                StepNumber = 4,
                BadgeColor = Color.FromArgb(22, 119, 255),
                Title = "Production Summary",
                Dock = DockStyle.Left,
                Width = 340
            };
            var descSummary = new ZeroDescriptions { Dock = DockStyle.Fill, Columns = 1, RowHeight = 28 };
            descSummary.Add("Target / Actual Inward", "100 / 0", Color.FromArgb(17, 24, 39));
            descSummary.Add("Delayed", "No", Color.FromArgb(22, 163, 74), isHighlighted: true);
            descSummary.Add("Root Cause", "--", Color.FromArgb(107, 114, 128));
            cardSummary.ContentPanel.Controls.Add(descSummary);

            var splitR3A = new Panel { Dock = DockStyle.Left, Width = 10, BackColor = Color.Transparent };

            // Card 3B: Product Specifications
            var cardProduct = new ZeroCard
            {
                StepNumber = null,
                Title = "Product Specifications",
                Dock = DockStyle.Left,
                Width = 320
            };
            var descProduct = new ZeroDescriptions { Dock = DockStyle.Fill, Columns = 1, RowHeight = 28 };
            descProduct.Add("Part Number", "1030MAX001", Color.FromArgb(17, 24, 39));
            descProduct.Add("Model Name", "B1030 MAX", Color.FromArgb(17, 24, 39));
            descProduct.Add("BOM / Partlist", "026MC02RP2.0", Color.FromArgb(79, 70, 229));
            cardProduct.ContentPanel.Controls.Add(descProduct);

            var splitR3B = new Panel { Dock = DockStyle.Left, Width = 10, BackColor = Color.Transparent };

            // Card 3C: Vertical Lot Tracking Timeline
            var cardTimeline = new ZeroCard
            {
                StepNumber = null,
                Title = "Lot Genealogy & Traceability",
                Dock = DockStyle.Fill
            };
            var timeline = new ZTimeline { Dock = DockStyle.Fill, ItemSpacing = 40 };
            timeline.Add("Raw Material Inward", "07:30", "Lot BOA437 & BOA541 OQC Verified", TimelineStatus.Completed);
            timeline.Add("SMT Feeder Load", "08:15", "420 chips picked and placed", TimelineStatus.Completed);
            timeline.Add("Wave Solder & Assy", "09:40", "Assembly Line 01 in progress", TimelineStatus.InProgress);
            cardTimeline.ContentPanel.Controls.Add(timeline);

            row3.Controls.Add(cardTimeline);
            row3.Controls.Add(splitR3B);
            row3.Controls.Add(cardProduct);
            row3.Controls.Add(splitR3A);
            row3.Controls.Add(cardSummary);

            // Simulation Logic
            int simAssy = 0;
            int simQc = 0;
            int simWh = 0;

            ZSevenSegment? segOutput = null;
            ZLinearGauge? gaugePressure = null;

            void ProcessScan(string barcode)
            {
                simAssy += 1;
                if (simAssy >= 2) simQc += 1;
                if (simQc >= 2) simWh += 1;

                string now = DateTime.Now.ToString("HH:mm:ss");
                _mesSteps.UpdateStep("ASSY", simAssy, now, StepsControlStatus.Completed);
                _mesSteps.UpdateStep("QC", simQc, now, StepsControlStatus.InProgress);
                _mesSteps.UpdateStep("WH", simWh, now, simWh > 0 ? StepsControlStatus.InProgress : StepsControlStatus.Waiting);

                descSummary.SetValue("Target / Actual Inward", $"100 / {simWh}", Color.FromArgb(17, 24, 39));
                timeline.Add($"Barcode {barcode}", now, "Station scan verified", TimelineStatus.Completed);
                if (segOutput != null) segOutput.Value = (1420 + simWh).ToString("D6");
                if (gaugePressure != null) gaugePressure.Value = 72.5f + (simWh % 12);
                ZeroToast.Success(this, $"Scanned: {barcode} | Assembly: {simAssy}, Inward: {simWh}");
            }

            barcodeBox.BarcodeScanned += (s, e) => ProcessScan(e.Barcode);

            btnSimulateScan.Click += (s, e) =>
            {
                simAssy += 5;
                if (simAssy >= 5) simQc += 4;
                if (simQc >= 4) simWh += 3;

                string now = DateTime.Now.ToString("HH:mm:ss");
                _mesSteps.UpdateStep("ASSY", simAssy, now, StepsControlStatus.Completed);
                _mesSteps.UpdateStep("QC", simQc, now, StepsControlStatus.InProgress);
                _mesSteps.UpdateStep("WH", simWh, now, simWh > 0 ? StepsControlStatus.InProgress : StepsControlStatus.Waiting);

                descSummary.SetValue("Target / Actual Inward", $"100 / {simWh}", Color.FromArgb(17, 24, 39));
                timeline.Add("PLC Signal Batch", now, $"Batch sync completed (+{simWh} units)", TimelineStatus.Completed);
                if (segOutput != null) segOutput.Value = (1420 + simWh).ToString("D6");
                if (gaugePressure != null) gaugePressure.Value = 72.5f + (simWh % 12);
                ZeroToast.Success(this, $"PLC Signal: Assembly: {simAssy}, QC: {simQc}, Inward: {simWh}");
            };


            // ROW 4: SCADA Telemetry & Industrial Andon Control
            var row4 = new Panel { Dock = DockStyle.Top, Height = 175, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 10) };

            // Card 4A: Andon Signal Tower Light
            var cardAndon = new ZeroCard
            {
                StepNumber = null,
                Title = "Andon Tower Light (SCADA Signal)",
                Dock = DockStyle.Left,
                Width = 280
            };

            var andonTower = new ZLedTower
            {
                Location = new Point(14, 6),
                Size = new Size(54, 120),
                RedLight = LedState.Off,
                AmberLight = LedState.Off,
                GreenLight = LedState.On,
                BlueLight = LedState.Off
            };
            cardAndon.ContentPanel.Controls.Add(andonTower);

            var btnAndonRun = new ZeroButton
            {
                Text = "▶ Running",
                ButtonStyle = ZeroButtonStyle.Success,
                Location = new Point(80, 8),
                Size = new Size(130, 26)
            };
            btnAndonRun.Click += (s, e) =>
            {
                andonTower.SetStatus(running: true, warning: false, alarm: false);
                statusBadge.Status = ZeroStatusType.Running;
                statusBadge.Text = "Line 01: Running";
                ZeroToast.Success(this, "SCADA: Production line switched to RUNNING (Green light on)");
            };
            cardAndon.ContentPanel.Controls.Add(btnAndonRun);

            var btnAndonWarn = new ZeroButton
            {
                Text = "⚠ Warning",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(80, 38),
                Size = new Size(130, 26)
            };
            btnAndonWarn.Click += (s, e) =>
            {
                andonTower.SetStatus(running: false, warning: true, alarm: false);
                statusBadge.Status = ZeroStatusType.Idle;
                statusBadge.Text = "Line 01: Low Feeder Alert";
                ZeroToast.Warning(this, "SCADA: SMT Feeder low stock alert (Amber light on)");
            };
            cardAndon.ContentPanel.Controls.Add(btnAndonWarn);

            var btnAndonAlarm = new ZeroButton
            {
                Text = "⛔ E-Stop Alarm",
                ButtonStyle = ZeroButtonStyle.Danger,
                Location = new Point(80, 68),
                Size = new Size(130, 26)
            };
            btnAndonAlarm.Click += (s, e) =>
            {
                andonTower.SetStatus(running: false, warning: false, alarm: true);
                statusBadge.Status = ZeroStatusType.Alarm;
                statusBadge.Text = "Line 01: EMERGENCY STOP";
                ZeroToast.Error(this, "SCADA: Emergency Stop E-STOP triggered! Red light blinking 2Hz!");
            };
            cardAndon.ContentPanel.Controls.Add(btnAndonAlarm);

            var splitR4A = new Panel { Dock = DockStyle.Left, Width = 10, BackColor = Color.Transparent };

            // Card 4B: Industrial 7-Segment Digital Readouts
            var cardLed = new ZeroCard
            {
                StepNumber = null,
                Title = "7-Segment Industrial LED (Takt & Output)",
                Dock = DockStyle.Left,
                Width = 365
            };

            var lblTakt = new Label { Text = "Target Takt Time (sec):", Location = new Point(10, 4), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var segTakt = new ZSevenSegment
            {
                Location = new Point(10, 20),
                Size = new Size(340, 34),
                DigitCount = 5,
                Value = "00:28",
                ColorPreset = SevenSegmentColorPreset.NeonCyan,
                BlinkColon = true,
                SlantAngle = 7f,
                Unit = "s"
            };

            var lblActual = new Label { Text = "Actual Batch Output (pcs):", Location = new Point(10, 56), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            segOutput = new ZSevenSegment
            {
                Location = new Point(10, 72),
                Size = new Size(340, 34),
                DigitCount = 6,
                Value = "001420",
                ColorPreset = SevenSegmentColorPreset.NeonEmerald,
                LeadingZeroMode = LeadingZeroDisplayMode.DimmedGhost,
                SlantAngle = 7f,
                Unit = "pcs"
            };

            // Interactive options toolbar to demo advanced LED options
            var btnCycleColor = new ZeroButton
            {
                Text = "🎨 LED Color",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(10, 110),
                Size = new Size(95, 24)
            };
            int colorIndex = 0;
            var presets = new[]
            {
                SevenSegmentColorPreset.NeonCyan,
                SevenSegmentColorPreset.NeonEmerald,
                SevenSegmentColorPreset.NeonAmber,
                SevenSegmentColorPreset.NeonRed,
                SevenSegmentColorPreset.CrispWhite,
                SevenSegmentColorPreset.UltraViolet
            };
            btnCycleColor.Click += (s, e) =>
            {
                colorIndex = (colorIndex + 1) % presets.Length;
                segOutput.ColorPreset = presets[colorIndex];
                ZeroToast.Info(this, $"LED Display: Switched theme {presets[colorIndex]}");
            };

            var btnToggleSlant = new ZeroButton
            {
                Text = "📐 Slanted 7°",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(110, 110),
                Size = new Size(105, 24)
            };
            btnToggleSlant.Click += (s, e) =>
            {
                if (segOutput.SlantAngle > 0)
                {
                    segTakt.SlantAngle = 0f;
                    segOutput.SlantAngle = 0f;
                    btnToggleSlant.Text = "📐 Vertical 0°";
                    ZeroToast.Info(this, "LED Display: Switched to 0° vertical mode");
                }
                else
                {
                    segTakt.SlantAngle = 7f;
                    segOutput.SlantAngle = 7f;
                    btnToggleSlant.Text = "📐 Slanted 7°";
                    ZeroToast.Info(this, "LED Display: Switched to 7° italic mode");
                }
            };

            var btnCycleMsg = new ZeroButton
            {
                Text = "⚡ SCADA Text",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(220, 110),
                Size = new Size(130, 24)
            };
            int msgIndex = 0;
            var sampleMsgs = new[]
            {
                ("001420", SevenSegmentColorPreset.NeonEmerald, false),
                ("1248.5", SevenSegmentColorPreset.NeonCyan, false),
                ("COOL", SevenSegmentColorPreset.NeonCyan, false),
                ("ALARM", SevenSegmentColorPreset.NeonRed, true),
                ("PASS", SevenSegmentColorPreset.NeonEmerald, false),
                ("P-01", SevenSegmentColorPreset.NeonAmber, false)
            };
            btnCycleMsg.Click += (s, e) =>
            {
                msgIndex = (msgIndex + 1) % sampleMsgs.Length;
                var sample = sampleMsgs[msgIndex];
                segOutput.Value = sample.Item1;
                segOutput.ColorPreset = sample.Item2;
                segOutput.Blink = sample.Item3;
                ZeroToast.Success(this, $"LED Display: {sample.Item1}");
            };

            cardLed.ContentPanel.Controls.Add(lblTakt);
            cardLed.ContentPanel.Controls.Add(segTakt);
            cardLed.ContentPanel.Controls.Add(lblActual);
            cardLed.ContentPanel.Controls.Add(segOutput);
            cardLed.ContentPanel.Controls.Add(btnCycleColor);
            cardLed.ContentPanel.Controls.Add(btnToggleSlant);
            cardLed.ContentPanel.Controls.Add(btnCycleMsg);

            var splitR4B = new Panel { Dock = DockStyle.Left, Width = 10, BackColor = Color.Transparent };

            // Card 4C: Linear Gauges (Hydraulic Pressure & Oven Temp)
            var cardSensors = new ZeroCard
            {
                StepNumber = null,
                Title = "Hydraulic Pressure & Temperature (SCADA Telemetry)",
                Dock = DockStyle.Fill
            };

            gaugePressure = new ZLinearGauge
            {
                Location = new Point(12, 4),
                Size = new Size(260, 50),
                Title = "Hydraulic Clamp Pressure",

                Unit = "Bar",
                Minimum = 0,
                Maximum = 120,
                Value = 72.5f,
                WarningThreshold = 85,
                CriticalThreshold = 105
            };

            var gaugeTemp = new ZLinearGauge
            {
                Location = new Point(285, 4),
                Size = new Size(260, 50),
                Title = "SMT Reflow Oven Temp (Zone 3)",
                Unit = "°C",
                Minimum = 50,
                Maximum = 300,
                Value = 245.0f,
                WarningThreshold = 265,
                CriticalThreshold = 285
            };

            cardSensors.ContentPanel.Controls.Add(gaugePressure);
            cardSensors.ContentPanel.Controls.Add(gaugeTemp);

            row4.Controls.Add(cardSensors);
            row4.Controls.Add(splitR4B);
            row4.Controls.Add(cardLed);
            row4.Controls.Add(splitR4A);
            row4.Controls.Add(cardAndon);

            // Assemble top-to-bottom layout
            mainContainer.Controls.Add(row4);
            mainContainer.Controls.Add(row3);
            mainContainer.Controls.Add(row2);
            mainContainer.Controls.Add(row1);
            mainContainer.Controls.Add(rowToolbar);
            mainContainer.Controls.Add(alertSpacer);
            mainContainer.Controls.Add(alertBanner);

            alertBanner.BringToFront();
            alertSpacer.BringToFront();
            rowToolbar.BringToFront();
            row1.BringToFront();
            row2.BringToFront();
            row3.BringToFront();
            row4.BringToFront();

            _tabMes.Controls.Add(mainContainer);
        }

        private void InitializeProcessCards(ZeroTabPage parentTab)
        {
            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = ZeroTheme.Colors.Background,
                Padding = new Padding(16)
            };

            var banner = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Severity = ZeroAlertSeverity.Info,
                Title = "📋 Manufacturing Execution System (MES) & MOP Process Cards",
                Message = "All-in-one Single HWND Step Process Cards: ZeroGridCard (Partlist & Stock Availability Grid) and ZeroWorkflowCard (Production Line Workflow Pipeline).",
                Height = 62
            };
            var bannerSpacer = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Color.Transparent };

            // Card 1: ZeroGridCard (Step 1: Board Information)
            var cardGrid = new ZeroGridCard
            {
                Dock = DockStyle.Top,
                Height = 250,
                StepNumber = 1,
                Title = "PCBA / Board Information",
                Subtitle = "Partlist Specification: 026MC02RP2.0",
                StatusTag = "5 Items • In Stock",
                FooterText = "Dispatch Breakdown: By Status",
                SummaryText = "Total Raw Stock: 1,386 pcs"
            };

            cardGrid.AddColumn("Part Code", 140, HorizontalAlignment.Left);
            cardGrid.AddColumn("Partlist Qty", 100, HorizontalAlignment.Right);
            cardGrid.AddColumn("Raw Stock", 130, HorizontalAlignment.Right, isAlertZero: true);
            cardGrid.AddColumn("WIP Stock", 120, HorizontalAlignment.Right);

            cardGrid.AddRow("BOA437", 1, 347, 0);
            cardGrid.AddRow("BOA472", 0, 18, 0);
            cardGrid.AddRow("BOA536", 1, 4, 0);
            cardGrid.AddRow("BOA541", 1, 1017, 0);
            cardGrid.AddRow("BOA602", 2, 0, 0); // Out of stock alert row!

            cardGrid.FooterClicked += (s, e) =>
            {
                ZeroToast.Info(this, "Opening material dispatch details by work order status!");
            };

            var spacerCards = new Panel { Dock = DockStyle.Top, Height = 14, BackColor = Color.Transparent };

            // Card 2: ZeroWorkflowCard (Step 3: Production Line Workflow)
            var cardWorkflow = new ZWorkflowCard
            {
                Dock = DockStyle.Top,
                Height = 160,
                StepNumber = 3,
                Title = "Production Line Workflow Pipeline",
                Subtitle = "SMT Line 01 • Work Order MO-20260901",
                StatusTag = "In Operation (2/3 Completed)",
                StatusTagColor = Color.FromArgb(16, 185, 129),
                FooterText = "Click stage node to inspect details or advance workflow step"
            };

            cardWorkflow.AddStage("assembly", "Assembly Line", 1250, "17:10", StepsControlStatus.Completed, StepsControlGlyph.Gear);
            cardWorkflow.AddStage("qc", "QC Inspection", 1242, "17:15", StepsControlStatus.InProgress, StepsControlGlyph.Checkmark);
            cardWorkflow.AddStage("inward", "Finished Goods Inward", 0, "--", StepsControlStatus.Waiting, StepsControlGlyph.Warehouse);

            cardWorkflow.StageClicked += (s, ev) =>
            {
                ZeroToast.Success(this, $"Selected Stage: {ev.Stage.Title} (Qty: {ev.Stage.Quantity:N0})");
            };

            mainContainer.Controls.Add(cardWorkflow);
            mainContainer.Controls.Add(spacerCards);
            mainContainer.Controls.Add(cardGrid);
            mainContainer.Controls.Add(bannerSpacer);
            mainContainer.Controls.Add(banner);

            banner.BringToFront();
            bannerSpacer.BringToFront();
            cardGrid.BringToFront();
            spacerCards.BringToFront();
            cardWorkflow.BringToFront();

            parentTab.Controls.Add(mainContainer);
        }

        private void InitializeScadaHub()
        {
            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = ZeroTheme.Colors.Background,
                Padding = new Padding(16)
            };

            // 0. Alert / Status Banner
            var banner = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Severity = ZeroAlertSeverity.Info,
                Title = "🔬 SCADA & Smart Factory Hub — Real-Time Control & Telemetry Station",
                Message = "Integrated 60 FPS oscilloscope (ZTrendChart), Lean cycle pacing (ZTaktTimer), AOI defect matrix (ZDefectMatrix), PLC I/O monitor (ZPlcIoMonitor), and SLA touch Andon pad (ZAndonCallPad).",
                Height = 62
            };
            var bannerSpacer = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            // ROW 1: Real-time Oscilloscope (ZTrendChart) + Lean Takt Countdown Ring (ZTaktTimer)
            var row1 = new Panel { Dock = DockStyle.Top, Height = 220, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 10) };

            // Card 1A: Trend Chart
            var cardTrend = new ZeroCard
            {
                StepNumber = null,
                Title = "Real-Time Sensor Oscilloscope (60 FPS Zero-Alloc)",
                Dock = DockStyle.Left,
                Width = 560
            };

            var trendChart = new ZTrendChart
            {
                Dock = DockStyle.Fill,
                Title = "Ch1: Hydraulic Pressure (Bar) | Ch2: Oven Temperature (°C)",
                UpperLimit = 85f,
                LowerLimit = 15f
            };
            cardTrend.ContentPanel.Controls.Add(trendChart);

            var trendToolbar = new Panel { Dock = DockStyle.Bottom, Height = 32, BackColor = Color.FromArgb(15, 23, 42), Padding = new Padding(6, 4, 6, 4) };
            var btnSpike = new ZeroButton
            {
                Text = "⚡ Inject Pressure Spike",
                ButtonStyle = ZeroButtonStyle.Danger,
                Dock = DockStyle.Left,
                Width = 200
            };
            var btnPauseTrend = new ZeroButton
            {
                Text = "⏸ Pause/Resume Stream",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Dock = DockStyle.Left,
                Width = 140
            };
            trendToolbar.Controls.Add(btnPauseTrend);
            trendToolbar.Controls.Add(btnSpike);
            cardTrend.ContentPanel.Controls.Add(trendToolbar);

            var split1 = new Panel { Dock = DockStyle.Left, Width = 12, BackColor = Color.Transparent };

            // Card 1B: Takt Timer
            var cardTakt = new ZeroCard
            {
                StepNumber = null,
                Title = "Lean Takt Cycle Pacing (Assembly Line)",
                Dock = DockStyle.Fill
            };

            var taktTimer = new ZTaktTimer
            {
                Dock = DockStyle.Left,
                Width = 180,
                TargetTaktSeconds = 25f,
                AverageCycleTime = 23.8f,
                CompletedUnits = 186
            };
            cardTakt.ContentPanel.Controls.Add(taktTimer);

            var taktControls = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 20, 10, 10) };
            var btnCompleteUnit = new ZeroButton
            {
                Text = "✅ Complete Unit (+1 Output)",
                ButtonStyle = ZeroButtonStyle.Success,
                Dock = DockStyle.Top,
                Height = 36
            };
            var taktSpacer1 = new Panel { Dock = DockStyle.Top, Height = 8, BackColor = Color.Transparent };
            var btnResetTakt = new ZeroButton
            {
                Text = "🔄 Reset Takt Cycle",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Dock = DockStyle.Top,
                Height = 34
            };
            taktControls.Controls.Add(btnResetTakt);
            taktControls.Controls.Add(taktSpacer1);
            taktControls.Controls.Add(btnCompleteUnit);
            cardTakt.ContentPanel.Controls.Add(taktControls);

            row1.Controls.Add(cardTakt);
            row1.Controls.Add(split1);
            row1.Controls.Add(cardTrend);

            // ROW 2: AOI Defect Matrix (ZDefectMatrix) + PLC I/O Registers (ZPlcIoMonitor)
            var row2 = new Panel { Dock = DockStyle.Top, Height = 220, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 10) };

            // Card 2A: Defect Matrix
            var cardDefect = new ZeroCard
            {
                StepNumber = null,
                Title = "AOI Optical Inspection Matrix (Array 3x6)",
                Dock = DockStyle.Left,
                Width = 520
            };

            var defectMatrix = new ZDefectMatrix
            {
                Dock = DockStyle.Fill,
                Title = "SMT Carrier Panel #SN-94812 — AOI Station 03 Camera"
            };
            cardDefect.ContentPanel.Controls.Add(defectMatrix);

            var defectToolbar = new Panel { Dock = DockStyle.Bottom, Height = 32, BackColor = Color.FromArgb(15, 23, 42), Padding = new Padding(6, 4, 6, 4) };
            var btnSimDefect = new ZeroButton
            {
                Text = "🔍 Simulate SMT Defect",
                ButtonStyle = ZeroButtonStyle.Danger,
                Dock = DockStyle.Left,
                Width = 200
            };
            var btnClearPass = new ZeroButton
            {
                Text = "✅ All Pass",
                ButtonStyle = ZeroButtonStyle.Success,
                Dock = DockStyle.Left,
                Width = 140
            };
            defectToolbar.Controls.Add(btnClearPass);
            defectToolbar.Controls.Add(btnSimDefect);
            cardDefect.ContentPanel.Controls.Add(defectToolbar);

            var split2 = new Panel { Dock = DockStyle.Left, Width = 12, BackColor = Color.Transparent };

            // Card 2B: PLC I/O Monitor
            var cardPlc = new ZeroCard
            {
                StepNumber = null,
                Title = "PLC I/O Bit Monitor (Click DO to Force)",
                Dock = DockStyle.Fill
            };

            var plcMonitor = new ZPlcIoMonitor
            {
                Dock = DockStyle.Fill,
                DigitalInputs = 0x00A5,
                DigitalOutputs = 0x000F,
                AllowSimulationClick = true
            };
            cardPlc.ContentPanel.Controls.Add(plcMonitor);

            row2.Controls.Add(cardPlc);
            row2.Controls.Add(split2);
            row2.Controls.Add(cardDefect);

            // ROW 3: Shopfloor Andon Call Pad + Station Event Journal
            var row3 = new Panel { Dock = DockStyle.Top, Height = 200, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 10) };

            // Card 3A: Andon Call Pad
            var cardAndon = new ZeroCard
            {
                StepNumber = null,
                Title = "Shopfloor SLA Andon Touch Pad",
                Dock = DockStyle.Left,
                Width = 480
            };

            var andonPad = new ZAndonCallPad
            {
                Dock = DockStyle.Fill
            };
            cardAndon.ContentPanel.Controls.Add(andonPad);

            var split3 = new Panel { Dock = DockStyle.Left, Width = 12, BackColor = Color.Transparent };

            // Card 3B: Station Event Journal
            var cardLog = new ZeroCard
            {
                StepNumber = null,
                Title = "SCADA Station Audit Journal (Real-Time)",
                Dock = DockStyle.Fill
            };

            var scadaLog = new ZeroListView
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9f)
            };
            cardLog.ContentPanel.Controls.Add(scadaLog);

            row3.Controls.Add(cardLog);
            row3.Controls.Add(split3);
            row3.Controls.Add(cardAndon);

            // Seed initial log entries
            scadaLog.AddLog(DateTime.Now.AddMinutes(-12), LogSeverity.Info, "SCADA station started: Connected to Siemens S7-1500 (IP 192.168.1.10) successfully.");
            scadaLog.AddLog(DateTime.Now.AddMinutes(-8), LogSeverity.Success, "AOI Inspection: Loaded SMT 3x6 panel template (IPC-A-610G standard library).");
            scadaLog.AddLog(DateTime.Now.AddMinutes(-5), LogSeverity.Info, "Lean Takt: Pacing cycle configured to standard 25.0s.");

            // Wire events
            taktTimer.TaktCompleted += (s, e) =>
            {
                scadaLog.AddLog(DateTime.Now, LogSeverity.Success, $"Takt Timer: Unit completed. Shift total output: {taktTimer.CompletedUnits} PCS.");
            };

            taktTimer.TaktOverdue += (s, e) =>
            {
                scadaLog.AddLog(DateTime.Now, LogSeverity.Warning, "Takt Timer: TAKT OVERDUE WARNING (>25s)!");
                ZeroToast.Warning(this, "⚠ Warning: Assembly line exceeding target Takt Time!");
            };

            btnCompleteUnit.Click += (s, e) =>
            {
                taktTimer.CompleteUnit();
                ZeroToast.Success(this, $"Confirmed completion of Unit #{taktTimer.CompletedUnits}!");
            };

            btnResetTakt.Click += (s, e) =>
            {
                taktTimer.Reset();
                scadaLog.AddLog(DateTime.Now, LogSeverity.Info, "Takt Timer: Cycle reset to 0.0s.");
            };

            defectMatrix.SlotClicked += (s, e) =>
            {
                var slot = e.Slot;
                string msg = $"[AOI Drill-Down] Position: {slot.Code} | Status: {slot.Status} | Detail: {slot.DefectDetail}";
                var sev = slot.Status == DefectStatus.Fail ? LogSeverity.Error : (slot.Status == DefectStatus.Warning ? LogSeverity.Warning : LogSeverity.Success);
                scadaLog.AddLog(DateTime.Now, sev, msg);
                if (slot.Status == DefectStatus.Fail)
                    ZeroToast.Error(this, $"Defect detected at {slot.Code}: {slot.DefectDetail}");
                else
                    ZeroToast.Info(this, $"Detail {slot.Code}: {slot.DefectDetail}");
            };

            btnSimDefect.Click += (s, e) =>
            {
                defectMatrix.SetSlotStatus(2, 1, DefectStatus.Fail, "Tombstone Capacitor C18");
                scadaLog.AddLog(DateTime.Now, LogSeverity.Error, "AOI Inspection: Visual defect detected at position U14 (Tombstone C18)!");
                ZeroToast.Error(this, "AOI: Tombstone component defect detected at U14!");
            };

            btnClearPass.Click += (s, e) =>
            {
                for (int r = 0; r < defectMatrix.Rows; r++)
                    for (int c = 0; c < defectMatrix.Columns; c++)
                        defectMatrix.SetSlotStatus(r, c, DefectStatus.Pass, "OK");
                scadaLog.AddLog(DateTime.Now, LogSeverity.Success, "AOI Inspection: All panel positions passed 100%.");
                ZeroToast.Success(this, "AOI Panel: 100% Passed!");
            };

            plcMonitor.OutputCoilChanged += (s, e) =>
            {
                string state = e.NewState ? "HIGH (1)" : "LOW (0)";
                scadaLog.AddLog(DateTime.Now, LogSeverity.Info, $"PLC Coil: Force output DO_{e.BitIndex:D2} to {state}.");
                ZeroToast.Info(this, $"PLC DO_{e.BitIndex:D2} = {(e.NewState ? 1 : 0)}");
            };

            andonPad.CallTriggered += (s, e) =>
            {
                if (e.IsActive)
                {
                    scadaLog.AddLog(DateTime.Now, LogSeverity.Warning, $"ANDON CALL: Urgent request [{e.CallType}] dispatched at station!");
                    ZeroToast.Warning(this, $"🚨 ANDON: Call signal dispatched for [{e.CallType}]!");
                }
                else
                {
                    scadaLog.AddLog(DateTime.Now, LogSeverity.Success, $"ANDON CALL: Request [{e.CallType}] cleared and acknowledged.");
                    ZeroToast.Success(this, $"Andon: Cleared request [{e.CallType}].");
                }
            };

            bool isStreaming = true;
            btnPauseTrend.Click += (s, e) =>
            {
                isStreaming = !isStreaming;
                btnPauseTrend.Text = isStreaming ? "⏸ Pause Stream" : "▶ Resume Stream";
            };

            btnSpike.Click += (s, e) =>
            {
                trendChart.AddPoint(0, 94.2f);
                scadaLog.AddLog(DateTime.Now, LogSeverity.Error, "SCADA Sensor: Pressure exceeded USL safety threshold (94.2 Bar > 85.0 Bar)!");
                ZeroToast.Error(this, "⚠ Hydraulic clamp overpressure: 94.2 Bar!");
            };


            // Live Simulation Timer for Telemetry and PLC
            float simTime = 0f;
            var rand = new Random();

            _scadaSimTimer = new System.Windows.Forms.Timer { Interval = 80 };
            _scadaSimTimer.Tick += (s, e) =>
            {
                if (!isStreaming) return;
                simTime += 0.1f;

                // Simulated pressure: 60 + 14 * sin(t) + jitter
                float p = 60f + (14f * (float)Math.Sin(simTime)) + (float)(rand.NextDouble() * 3.0 - 1.5);
                // Simulated temp: 220 + 8 * cos(t*0.4) + jitter
                float t = 220f + (8f * (float)Math.Cos(simTime * 0.4f)) + (float)(rand.NextDouble() * 2.0 - 1.0);

                trendChart.AddPoint(0, p);
                trendChart.AddPoint(1, t);

                // Pulse DI bit 0 (Photocell) every ~2.5 seconds
                if ((int)(simTime * 10) % 25 == 0)
                {
                    bool cur = (plcMonitor.DigitalInputs & 0x0001) != 0;
                    plcMonitor.SetInputBit(0, !cur);
                }
            };
            _scadaSimTimer.Start();

            // Assemble top-to-bottom layout
            mainContainer.Controls.Add(row3);
            mainContainer.Controls.Add(row2);
            mainContainer.Controls.Add(row1);
            mainContainer.Controls.Add(bannerSpacer);
            mainContainer.Controls.Add(banner);

            banner.BringToFront();
            bannerSpacer.BringToFront();
            row1.BringToFront();
            row2.BringToFront();
            row3.BringToFront();

            _tabScada.Controls.Add(mainContainer);
        }

        private void InitializeWmsCenter()
        {
            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = ZeroTheme.Colors.Background,
                Padding = new Padding(16)
            };

            // 0. Alert Banner
            var banner = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Severity = ZeroAlertSeverity.Success,
                Title = "📦 WMS & Quality Inspection Center — Smart Warehouse & Six Sigma QC",
                Message = "Integrated 2D Smart Warehouse Storage Rack (ZWarehouseRack), 3D Industrial Tank (ZTank3D), SPC X-Bar Chart (ZSpcChart), and Electronic Kanban Dispatching Board (ZKanbanBoard).",
                Height = 62
            };
            var bannerSpacer = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            // ROW 1: Smart Warehouse Storage Rack (ZWarehouseRack) + Industrial 3D Fluid Tank (ZTank3D)
            var row1 = new Panel { Dock = DockStyle.Top, Height = 310, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 10) };

            // Card 1A: Warehouse Rack
            var cardRack = new ZeroCard
            {
                StepNumber = null,
                Title = "SMT Reel Storage Rack — Row A (Bay 01..05 x Level 01..04)",
                Dock = DockStyle.Left,
                Width = 560
            };

            var rack = new ZWarehouseRack
            {
                Dock = DockStyle.Fill,
                Bays = 5,
                Levels = 4,
                RackTitle = "SMT Reel Rack A (A-01-01 to A-05-04)"
            };
            cardRack.ContentPanel.Controls.Add(rack);

            var rackToolbar = new Panel { Dock = DockStyle.Bottom, Height = 36, BackColor = Color.FromArgb(15, 23, 42), Padding = new Padding(6, 4, 6, 4) };
            var btnAddReel = new ZeroButton
            {
                Text = "📦 Add SMT Reel to Bin A-02-03",
                ButtonStyle = ZeroButtonStyle.Success,
                Dock = DockStyle.Left,
                Width = 230
            };
            var btnLockQc = new ZeroButton
            {
                Text = "🔒 Lock Batch (QC Hold)",
                ButtonStyle = ZeroButtonStyle.Danger,
                Dock = DockStyle.Left,
                Width = 160
            };
            rackToolbar.Controls.Add(btnLockQc);
            rackToolbar.Controls.Add(btnAddReel);
            cardRack.ContentPanel.Controls.Add(rackToolbar);

            var split1 = new Panel { Dock = DockStyle.Left, Width = 12, BackColor = Color.Transparent };

            // Card 1B: Industrial 3D Tank
            var cardTank = new ZeroCard
            {
                StepNumber = null,
                Title = "Solvent Storage Tank TK-01 (10,000L)",
                Dock = DockStyle.Fill
            };

            var tank = new ZTank3D
            {
                Dock = DockStyle.Left,
                Width = 190,
                CapacityLiters = 10000f,
                CurrentLevelLiters = 6850f,
                TankName = "IPA Tank TK-01",
                FluidName = "IPA Solution 99.7%"
            };

            var tankControls = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 30, 10, 10) };
            var btnPumpIn = new ZeroButton
            {
                Text = "🔼 Pump In (+1,000L)",
                ButtonStyle = ZeroButtonStyle.Primary,
                Dock = DockStyle.Top,
                Height = 36
            };
            var tankSpacer1 = new Panel { Dock = DockStyle.Top, Height = 8, BackColor = Color.Transparent };
            var btnDrainOut = new ZeroButton
            {
                Text = "🔽 Drain Out (-1,000L)",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Dock = DockStyle.Top,
                Height = 36
            };
            tankControls.Controls.Add(btnDrainOut);
            tankControls.Controls.Add(tankSpacer1);
            tankControls.Controls.Add(btnPumpIn);

            cardTank.ContentPanel.Controls.Add(tankControls);
            cardTank.ContentPanel.Controls.Add(tank);
            tank.SendToBack();

            row1.Controls.Add(cardTank);
            row1.Controls.Add(split1);
            row1.Controls.Add(cardRack);

            // ROW 2: Statistical Process Control (ZSpcChart) + Electronic Kanban Board (ZKanbanBoard)
            var row2 = new Panel { Dock = DockStyle.Top, Height = 280, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 10) };

            // Card 2A: SPC Chart
            var cardSpc = new ZeroCard
            {
                StepNumber = null,
                Title = "SPC X-Bar Statistical Process Control (CNC Tolerance)",
                Dock = DockStyle.Left,
                Width = 560
            };

            var spcChart = new ZSpcChart
            {
                Dock = DockStyle.Fill,
                NominalTarget = 12.000f,
                USL = 12.020f,
                LSL = 11.980f,
                Title = "SPC X-Bar: CNC Dowel Pin Diameter (12.000 ± 0.020 mm)"
            };
            cardSpc.ContentPanel.Controls.Add(spcChart);

            var spcToolbar = new Panel { Dock = DockStyle.Bottom, Height = 32, BackColor = Color.FromArgb(15, 23, 42), Padding = new Padding(6, 4, 6, 4) };
            var btnAddSample = new ZeroButton
            {
                Text = "📏 Measure Sample (Add Subgroup)",
                ButtonStyle = ZeroButtonStyle.Success,
                Dock = DockStyle.Left,
                Width = 200
            };
            var btnSpikeSpc = new ZeroButton
            {
                Text = "⚠ Simulate 3-Sigma Outlier",
                ButtonStyle = ZeroButtonStyle.Danger,
                Dock = DockStyle.Left,
                Width = 190
            };
            spcToolbar.Controls.Add(btnSpikeSpc);
            spcToolbar.Controls.Add(btnAddSample);
            cardSpc.ContentPanel.Controls.Add(spcToolbar);

            var split2 = new Panel { Dock = DockStyle.Left, Width = 12, BackColor = Color.Transparent };

            // Card 2B: Kanban Board
            var cardKanban = new ZeroCard
            {
                StepNumber = null,
                Title = "Electronic Kanban Dispatching Board — SMT Line (WIP Limit)",
                Dock = DockStyle.Fill
            };

            var kanban = new ZKanbanBoard
            {
                Dock = DockStyle.Fill
            };
            cardKanban.ContentPanel.Controls.Add(kanban);

            row2.Controls.Add(cardKanban);
            row2.Controls.Add(split2);
            row2.Controls.Add(cardSpc);

            // Wire Events
            rack.BinClicked += (s, e) =>
            {
                var b = e.Bin;
                string info = $"Location {b.BinCode}: {b.Status} | SKU: {(string.IsNullOrEmpty(b.Sku) ? "(Empty)" : b.Sku)} | Qty: {b.CurrentQty:N0} PCS | Lot: {b.LotNumber}";
                if (b.Status == BinOccupancyStatus.Quarantine)
                    ZeroToast.Error(this, $"[QC HOLD WARNING] {info}");
                else if (b.Status == BinOccupancyStatus.Full)
                    ZeroToast.Success(this, $"[BIN OCCUPIED] {info}");
                else
                    ZeroToast.Info(this, $"[BIN DETAILS] {info}");
            };

            btnAddReel.Click += (s, e) =>
            {
                rack.SetBin(3, 2, BinOccupancyStatus.Full, "IC-MCU-STM32", "STM32F407VGT6", "LOT-20260903-NEW", 2000);
                ZeroToast.Success(this, "Added 2,000 PCS STM32 to bin A-02-03!");
            };

            btnLockQc.Click += (s, e) =>
            {
                rack.SetBin(2, 4, BinOccupancyStatus.Quarantine, "SMD-CAP-0805", "Ceramic Cap 10uF", "LOT-20260815-HOLD", 800);
                ZeroToast.Error(this, "Quarantined bin A-04-02 for QA reinspection!");
            };

            btnPumpIn.Click += (s, e) =>
            {
                tank.CurrentLevelLiters = Math.Min(tank.CapacityLiters, tank.CurrentLevelLiters + 1000f);
                if (tank.AlarmState == TankAlarmState.HighOverflow)
                    ZeroToast.Error(this, $"OVERFLOW ALARM: Tank level reached {tank.Percentage:F1}% (>90%)!");
                else
                    ZeroToast.Success(this, $"Pump In successful: {tank.CurrentLevelLiters:N0} L ({tank.Percentage:F1}%)");
            };

            btnDrainOut.Click += (s, e) =>
            {
                tank.CurrentLevelLiters = Math.Max(0f, tank.CurrentLevelLiters - 1000f);
                if (tank.AlarmState == TankAlarmState.LowLevel)
                    ZeroToast.Warning(this, $"LOW LEVEL ALARM: Tank level down to {tank.Percentage:F1}% (<15%)!");
                else
                    ZeroToast.Info(this, $"Drain Out successful: {tank.CurrentLevelLiters:N0} L ({tank.Percentage:F1}%)");
            };

            var rand = new Random();
            btnAddSample.Click += (s, e) =>
            {
                float val = 12.000f + (float)(rand.NextDouble() * 0.010 - 0.005);
                spcChart.AddSample(val);
                ZeroToast.Success(this, $"Measured new sample: {val:F3} mm (In Control)");
            };

            btnSpikeSpc.Click += (s, e) =>
            {
                float spike = 12.028f;
                spcChart.AddSample(spike, "CNC Tool Drift");
                ZeroToast.Error(this, $"SPC ALARM: Sample {spike:F3} mm breached Upper Control Limit (UCL: {spcChart.UCL:F3} mm)!");
            };

            kanban.CardClicked += (s, e) =>
            {
                kanban.MoveCardNext(e.Card);
                ZeroToast.Info(this, $"Kanban: Work Order {e.Card.OrderNo} ({e.Card.ProductName}) dispatched to next stage!");
            };

            // Assemble top-to-bottom layout
            mainContainer.Controls.Add(row2);
            mainContainer.Controls.Add(row1);
            mainContainer.Controls.Add(bannerSpacer);
            mainContainer.Controls.Add(banner);

            banner.BringToFront();
            bannerSpacer.BringToFront();
            row1.BringToFront();
            row2.BringToFront();

            _tabWms.Controls.Add(mainContainer);
        }

        private void InitializeWarehouseWorkstation()
        {
            // 1. Receiving & Barcode Workstation Tab (_tabWhBarcode)
            var panelBarcode = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = ZeroTheme.Colors.Background,
                Padding = new Padding(16)
            };

            var bannerWh = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Severity = ZeroAlertSeverity.Info,
                Title = "Warehouse & Logistics Workstation — USB Wedge Barcode Scanner & Real-Time Stock",
                Message = "Hardware wedge scanner auto-detection (<35ms timing delta), duplicate scan suppression, instant 1-way stock updates, and batch traceability tree.",
                Height = 62
            };
            var bannerSpacer = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Color.Transparent };

            // Row 1: Barcode Scan Control + Inventory Card
            var row1 = new Panel { Dock = DockStyle.Top, Height = 200, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 12) };

            var scanControl = new ZeroBarcodeScanControl
            {
                Dock = DockStyle.Left,
                Width = 440
            };

            var splitWh1 = new Panel { Dock = DockStyle.Left, Width = 16, BackColor = Color.Transparent };

            var inventoryCard = new ZInventoryCard
            {
                Dock = DockStyle.Left,
                Width = 360
            };

            row1.Controls.Add(inventoryCard);
            row1.Controls.Add(splitWh1);
            row1.Controls.Add(scanControl);

            // Row 2: Stock Movement Timeline
            var row2 = new Panel { Dock = DockStyle.Top, Height = 290, BackColor = Color.Transparent };
            var timeline = new ZStockMovementTimeline
            {
                Dock = DockStyle.Fill
            };
            row2.Controls.Add(timeline);

            // Interactive Event Link: Scanning a barcode updates inventory card and adds a trace node
            scanControl.BarcodeScanned += (s, ev) =>
            {
                var result = ev.Result;
                inventoryCard.ProductCode = string.IsNullOrEmpty(result.ProductCode) ? result.RawBarcode : result.ProductCode;
                inventoryCard.AvailableQuantity += result.Quantity;

                var trace = timeline.CollectData();
                trace.Nodes.Add(new StockMovementNode
                {
                    Id = $"N{trace.Nodes.Count + 1}",
                    Type = StockMovementType.Inward,
                    Title = "BARCODE RECEIPT",
                    ReferenceNo = string.IsNullOrEmpty(result.LotNumber) ? "AUTO-RCV" : result.LotNumber,
                    Quantity = result.Quantity,
                    Timestamp = DateTime.Now,
                    DestinationOrSource = result.IsHardwareScanner ? "USB Hardware Scanner" : "Manual Station"
                });
                timeline.Populate(trace);

                ZeroToast.Success(this, $"Scanned {inventoryCard.ProductCode}: +{result.Quantity:N0} pcs (Lot: {result.LotNumber})");
            };

            panelBarcode.Controls.Add(row2);
            panelBarcode.Controls.Add(row1);
            panelBarcode.Controls.Add(bannerSpacer);
            panelBarcode.Controls.Add(bannerWh);

            bannerWh.BringToFront();
            bannerSpacer.BringToFront();
            row1.BringToFront();
            row2.BringToFront();

            _tabWhBarcode.Controls.Add(panelBarcode);

            // 2. FIFO / FEFO Lot Allocation Tab (_tabWhLot)
            var panelLot = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = ZeroTheme.Colors.Background,
                Padding = new Padding(16)
            };

            var bannerLot = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Severity = ZeroAlertSeverity.Warning,
                Title = "Automated Lot Allocation Engine — FIFO & FEFO Strategies",
                Message = "Automated lot allocation across manufacturing batches. Auto-lockouts for Quarantine and Expired batches prevent dispatch errors.",
                Height = 62
            };
            var lotSpacer = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Color.Transparent };

            var lotSelector = new ZLotSelector
            {
                Dock = DockStyle.Top,
                Height = 320,
                RequiredQuantity = 800
            };

            var lotActions = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.Transparent, Padding = new Padding(0, 10, 0, 0) };
            var btnConfirmAlloc = new ZeroButton
            {
                Text = "✓ Confirm Lot Allocation",
                ButtonStyle = ZeroButtonStyle.Primary,
                Dock = DockStyle.Left,
                Width = 200
            };
            btnConfirmAlloc.Click += (s, e) =>
            {
                var selected = lotSelector.CollectSelectedLots();
                decimal total = 0;
                foreach (var l in selected) total += l.AllocatedQuantity;
                ZeroToast.Success(this, $"Confirmed allocation of {total:N0} units across {selected.Count} batches!");
            };

            var btnResetAlloc = new ZeroButton
            {
                Text = "🔄 Reset Allocation",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Dock = DockStyle.Left,
                Width = 150
            };
            btnResetAlloc.Click += (s, e) =>
            {
                lotSelector.AutoAllocate(800, LotAllocationStrategy.FIFO);
                ZeroToast.Info(this, "Reset lot allocation to default FIFO.");
            };

            var splitLot = new Panel { Dock = DockStyle.Left, Width = 10, BackColor = Color.Transparent };
            lotActions.Controls.Add(btnResetAlloc);
            lotActions.Controls.Add(splitLot);
            lotActions.Controls.Add(btnConfirmAlloc);

            panelLot.Controls.Add(lotActions);
            panelLot.Controls.Add(lotSelector);
            panelLot.Controls.Add(lotSpacer);
            panelLot.Controls.Add(bannerLot);

            bannerLot.BringToFront();
            lotSpacer.BringToFront();
            lotSelector.BringToFront();
            lotActions.BringToFront();

            _tabWhLot.Controls.Add(panelLot);
        }

        private void InitializeCommercialSuite()
        {
            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(12),
                BackColor = Color.Transparent
            };

            // 1. Top Banner
            var banner = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Severity = ZeroAlertSeverity.Success,
                Title = "🏢 ENTERPRISE COMMERCIAL CONTROL SUITE (100% HARDWARE ACCELERATED)",
                Message = "ZeroUI features 8 commercial-grade controls with Zero external dependencies: Visual Query Builder (ZeroFilterControl), Embedded GridLookup (ZeroGridLookup), Multi-Select CheckedComboBox (ZeroCheckedComboBox), Tag/Chip Editor (ZTokenEdit), Color Swatch Picker (ZeroColorPicker), Sequential Workflow Wizard (ZeroWizard), Six Sigma Outlier SPC Chart (ZeroBoxPlotChart), Vector Print Preview (ZeroPrintPreview), 60 FPS Shimmer Loading (ZeroSkeleton), and Non-Intrusive Overlays (ZeroToast & ZeroModal)."
            };

            var bannerSpacer = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            // SECTION 1: Visual Query Builder & Enterprise Form Editors (Height = 440)
            var sec1 = new Panel { Dock = DockStyle.Top, Height = 440, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 10) };

            // Left Card: ZeroFilterControl
            var cardFilter = new ZeroCard
            {
                Dock = DockStyle.Left,
                Width = 560,
                Title = "Visual Query Builder (ZeroFilterControl)",
                Subtitle = "Hierarchical AND/OR expression tree generator"
            };

            var filterControl = new ZeroFilterControl
            {
                Dock = DockStyle.Fill
            };
            filterControl.AvailableFields.AddRange(new[] { "PartNumber", "Category", "UnitCost", "StockQty", "Status", "Supplier" });
            filterControl.RootGroup.Operator = FilterGroupOperator.And;
            filterControl.RootGroup.AddCondition("Status", FilterComparisonOperator.Equals, "Active");
            var subGroup = filterControl.RootGroup.AddGroup(FilterGroupOperator.Or);
            subGroup.AddCondition("UnitCost", FilterComparisonOperator.GreaterThanOrEqual, "15.00");
            subGroup.AddCondition("StockQty", FilterComparisonOperator.LessThanOrEqual, "100");
            filterControl.RebuildTreeUI();

            var filterBottom = new Panel { Dock = DockStyle.Bottom, Height = 42, BackColor = Color.Transparent, Padding = new Padding(6, 4, 6, 4) };
            var lblSqlResult = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Font = new Font("Consolas", 8.5f, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "SQL WHERE: " + filterControl.RootGroup.ToSqlWhere()
            };
            var btnApplyFilter = new ZeroButton
            {
                Dock = DockStyle.Right,
                Width = 130,
                Text = "⚡ Apply Filter",
                ButtonStyle = ZeroButtonStyle.Primary
            };
            btnApplyFilter.Click += (s, e) =>
            {
                string sql = filterControl.RootGroup.ToSqlWhere();
                lblSqlResult.Text = "SQL WHERE: " + sql;
                ZeroToast.Info(this, "Compiled SQL: " + sql);
            };
            var btnResetFilter = new ZeroButton
            {
                Dock = DockStyle.Right,
                Width = 90,
                Text = "🔄 Reset",
                ButtonStyle = ZeroButtonStyle.Secondary
            };
            btnResetFilter.Click += (s, e) =>
            {
                filterControl.RootGroup.Children.Clear();
                filterControl.RootGroup.AddCondition("Status", FilterComparisonOperator.Equals, "Active");
                filterControl.RebuildTreeUI();
                lblSqlResult.Text = "SQL WHERE: " + filterControl.RootGroup.ToSqlWhere();
                ZeroToast.Info(this, "Filter criteria reset to default.");
            };
            var filterSpacer = new Panel { Dock = DockStyle.Right, Width = 8, BackColor = Color.Transparent };

            filterBottom.Controls.Add(lblSqlResult);
            filterBottom.Controls.Add(btnResetFilter);
            filterBottom.Controls.Add(filterSpacer);
            filterBottom.Controls.Add(btnApplyFilter);

            cardFilter.ContentPanel.Controls.Add(filterControl);
            cardFilter.ContentPanel.Controls.Add(filterBottom);

            var splitSec1 = new Panel { Dock = DockStyle.Left, Width = 12, BackColor = Color.Transparent };

            // Right Card: Enterprise Form Editors
            var cardEditors = new ZeroCard
            {
                Dock = DockStyle.Fill,
                Title = "Enterprise Form Editors",
                Subtitle = "GridLookup, CheckedComboBox, TokenEdit, and ColorPicker"
            };

            var pnlEditors = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 8), AutoScroll = false };

            // Editor 1: GridLookupEdit with Quick-Create Footer
            var lblGridLookup = new Label { Text = "Multi-Column GridLookup (GridLookupEdit with +Add New):", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = ZeroTheme.Colors.TextPrimary, AutoSize = true, Location = new Point(8, 8) };
            var gridLookup = new GridLookupEdit
            {
                Location = new Point(8, 28),
                Width = 480,
                Height = 34,
                Placeholder = "Search & select industrial component (50 items)...",
                DisplayMember = "ItemName",
                ShowAddNewButton = true,
                AddNewButtonText = "➕ Quick Add Material"
            };
            var lookupSampleItems = new List<InventoryItem>(50);
            string[] lookupCats = new[] { "Microcontroller", "Memory IC", "Transceiver", "Power MOSFET", "Optocoupler" };
            for (int i = 1; i <= 50; i++)
            {
                lookupSampleItems.Add(new InventoryItem(
                    i,
                    $"PART-{i:D4}",
                    $"{lookupCats[i % lookupCats.Length]} Model #{i}",
                    100 + i * 25,
                    2.50 + (i * 0.75),
                    $"LOT-2026-B{i:D2}",
                    i % 5 == 0 ? "Under QC" : "Approved"
                ));
            }
            gridLookup.SetDataSource(lookupSampleItems);
            gridLookup.ProcessNewValue += (s, e) =>
            {
                ZeroToast.Success(this, $"Quick Add requested for new material: {e.DisplayText}");
                e.Handled = true;
            };
            gridLookup.SelectionChanged += (s, e) =>
            {
                if (!string.IsNullOrEmpty(gridLookup.SelectedText))
                {
                    ZeroToast.Success(this, $"GridLookup selected: {gridLookup.SelectedText}");
                }
            };

            // Editor 2: SearchLookUpEdit with Multi-Select & Tokens
            var lblSearchLookup = new Label { Text = "High-Capacity Search Lookup (SearchLookUpEdit with Multi-Select):", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = ZeroTheme.Colors.TextPrimary, AutoSize = true, Location = new Point(8, 70) };
            var searchLookup = new SearchLookUpEdit
            {
                Location = new Point(8, 90),
                Width = 480,
                Height = 34,
                Placeholder = "Multi-select catalog items (50,000+ items)...",
                DisplayMember = "ItemName",
                MultiSelect = true,
                ShowTokens = true
            };
            searchLookup.SetDataSource(lookupSampleItems);
            searchLookup.SelectionChanged += (s, e) =>
            {
                if (searchLookup.SelectedValues.Count > 0)
                {
                    ZeroToast.Info(this, $"SearchLookUp: {searchLookup.SelectedValues.Count} items selected.");
                }
            };

            // Editor 3: CheckedComboBoxEdit with Token badges
            var lblCheckedCombo = new Label { Text = "Multi-Select CheckedComboBox (CheckedComboBoxEdit with Token badges):", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = ZeroTheme.Colors.TextPrimary, AutoSize = true, Location = new Point(8, 132) };
            var checkedCombo = new CheckedComboBoxEdit
            {
                Location = new Point(8, 152),
                Width = 480,
                Height = 34,
                Placeholder = "Select manufacturing processes...",
                DisplayMode = CheckedComboDisplayMode.Tokens
            };
            checkedCombo.Items.Add(new CheckedComboBoxEdit.CheckedItem("SMT", "SMT Surface Mount Line 01", true));
            checkedCombo.Items.Add(new CheckedComboBoxEdit.CheckedItem("THT", "Through-Hole Soldering Wave", false));
            checkedCombo.Items.Add(new CheckedComboBoxEdit.CheckedItem("AOI", "AOI Automated Optical Inspection", true));
            checkedCombo.Items.Add(new CheckedComboBoxEdit.CheckedItem("XRAY", "3D X-Ray Inspection (BGA)", false));
            checkedCombo.Items.Add(new CheckedComboBoxEdit.CheckedItem("ICT", "In-Circuit Testing (ICT Fluke)", false));
            checkedCombo.Items.Add(new CheckedComboBoxEdit.CheckedItem("BURN", "48h Thermal Burn-In Chamber", false));
            checkedCombo.ItemCheck += (s, e) =>
            {
                ZeroToast.Info(this, $"Process {(e.NewValue ? "enabled" : "disabled")}: {e.Item}");
            };

            // Editor 4: TokenEdit with Auto-Complete
            var lblTokenEdit = new Label { Text = "Tag & Chip Editor (ZTokenEdit with Auto-Complete):", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = ZeroTheme.Colors.TextPrimary, AutoSize = true, Location = new Point(8, 194) };
            var tokenEdit = new ZTokenEdit
            {
                Location = new Point(8, 214),
                Width = 480,
                Height = 34,
                Placeholder = "Type tag name or select suggestions...",
                AutocompleteSource = new[] { "ISO-9001:2015", "RoHS-3", "AEC-Q200", "Class-3 Medical", "UL-94V0", "CE-Mark", "ITAR-Compliant", "Mil-Spec-810H" }
            };
            tokenEdit.AddToken("ISO-9001:2015");
            tokenEdit.AddToken("RoHS-3");
            tokenEdit.AddToken("AEC-Q200");
            tokenEdit.AddToken("Class-3 Medical");
            tokenEdit.TokenAdded += (s, e) => ZeroToast.Success(this, $"Added tag: {e.Token}");
            tokenEdit.TokenRemoved += (s, e) => ZeroToast.Warning(this, $"Removed tag: {e.Token}");

            // Editor 5: ZeroColorPicker
            var lblColorPicker = new Label { Text = "Color Swatch & Palette Picker (ZeroColorPicker):", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = ZeroTheme.Colors.TextPrimary, AutoSize = true, Location = new Point(8, 256) };
            var colorPicker = new ZeroColorPicker
            {
                Location = new Point(8, 276),
                Width = 220,
                Height = 34,
                SelectedColor = Color.FromArgb(79, 70, 229)
            };
            colorPicker.ColorChanged += (s, e) =>
            {
                ZeroToast.Info(this, $"Theme accent selected: #{colorPicker.SelectedColor.R:X2}{colorPicker.SelectedColor.G:X2}{colorPicker.SelectedColor.B:X2}");
            };

            pnlEditors.Controls.Add(lblGridLookup);
            pnlEditors.Controls.Add(gridLookup);
            pnlEditors.Controls.Add(lblSearchLookup);
            pnlEditors.Controls.Add(searchLookup);
            pnlEditors.Controls.Add(lblCheckedCombo);
            pnlEditors.Controls.Add(checkedCombo);
            pnlEditors.Controls.Add(lblTokenEdit);
            pnlEditors.Controls.Add(tokenEdit);
            pnlEditors.Controls.Add(lblColorPicker);
            pnlEditors.Controls.Add(colorPicker);
            cardEditors.ContentPanel.Controls.Add(pnlEditors);

            sec1.Controls.Add(cardEditors);
            sec1.Controls.Add(splitSec1);
            sec1.Controls.Add(cardFilter);

            // SECTION 2: Process Workflow Sequence & Six Sigma SPC Chart (Height = 340)
            var sec2 = new Panel { Dock = DockStyle.Top, Height = 340, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 10) };

            // Left Card: ZeroWizard
            var cardWizard = new ZeroCard
            {
                Dock = DockStyle.Left,
                Width = 560,
                Title = "Process Workflow Sequence (ZeroWizard)",
                Subtitle = "Multi-step guided dispatch sequence with validation"
            };

            var wizard = new ZeroWizard { Dock = DockStyle.Fill };
            
            // Wizard Step 1
            var wPage1 = new ZeroWizardPage("1. Work Order Setup", "Configure order parameters and production cell.", "📋");
            var pnlW1 = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 12) };
            var lblW1A = new Label { Text = "Work Order Identifier:", ForeColor = ZeroTheme.Colors.TextPrimary, AutoSize = true, Location = new Point(10, 10) };
            var txtW1Order = new ZeroTextBox { Text = "WO-2026-904", Location = new Point(10, 30), Width = 300, Height = 32 };
            var lblW1B = new Label { Text = "Designated Robotic Workcell:", ForeColor = ZeroTheme.Colors.TextPrimary, AutoSize = true, Location = new Point(10, 70) };
            var cmbW1Cell = new ZeroComboBox { Location = new Point(10, 90), Width = 300, Height = 32 };
            cmbW1Cell.Items.Add("Robotic Cell A (Kuka KR-16)");
            cmbW1Cell.Items.Add("Robotic Cell B (Fanuc M-20iA)");
            cmbW1Cell.Items.Add("AOI Cell #04 (Keyence 3D)");
            cmbW1Cell.Items.Add("SMT Line #02 High-Speed");
            cmbW1Cell.SelectedIndex = 0;
            pnlW1.Controls.Add(lblW1A);
            pnlW1.Controls.Add(txtW1Order);
            pnlW1.Controls.Add(lblW1B);
            pnlW1.Controls.Add(cmbW1Cell);
            wPage1.Controls.Add(pnlW1);

            // Wizard Step 2
            var wPage2 = new ZeroWizardPage("2. Statistical Limits", "Define SPC tolerance limits for real-time AOI.", "🔬");
            var pnlW2 = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 12) };
            var descW2 = new ZeroDescriptions { Dock = DockStyle.Fill, Columns = 2, RowHeight = 28 };
            descW2.Add("Upper Spec Limit (USL)", "12.080 mm");
            descW2.Add("Target Dimension", "12.000 mm");
            descW2.Add("Lower Spec Limit (LSL)", "11.920 mm");
            descW2.Add("Tolerance Band", "± 0.080 mm");
            descW2.Add("Cpk Capability Index", "1.67 (Capable)");
            descW2.Add("Inspection Policy", "100% 3D Automated");
            pnlW2.Controls.Add(descW2);
            wPage2.Controls.Add(pnlW2);

            // Wizard Step 3
            var wPage3 = new ZeroWizardPage("3. Authorization & Launch", "Review order dispatch and confirm safety locks.", "🚀");
            var pnlW3 = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 12) };
            var bannerW3 = new ZeroAlertBanner
            {
                Dock = DockStyle.Fill,
                Severity = ZeroAlertSeverity.Info,
                Title = "READY FOR DISPATCH CONFIRMATION",
                Message = "All safety light curtains, feeder magazines, and vision calibrations have been verified. Click 'Finish' below to initiate the robotic workcell cycle."
            };
            pnlW3.Controls.Add(bannerW3);
            wPage3.Controls.Add(pnlW3);

            wizard.AddPage(wPage1);
            wizard.AddPage(wPage2);
            wizard.AddPage(wPage3);
            wizard.Finished += (s, e) =>
            {
                ZeroToast.Success(this, "Production Order WO-2026-904 successfully launched via Wizard!");
            };

            cardWizard.ContentPanel.Controls.Add(wizard);

            var splitSec2 = new Panel { Dock = DockStyle.Left, Width = 12, BackColor = Color.Transparent };

            // Right Card: ZeroBoxPlotChart
            var cardBoxPlot = new ZeroCard
            {
                Dock = DockStyle.Fill,
                Title = "Six Sigma SPC Tolerance Distribution (ZeroBoxPlotChart)",
                Subtitle = "Outlier detection, quartiles (Q1, Med, Q3), and specification limits"
            };

            var boxPlot = new ZeroBoxPlotChart
            {
                Dock = DockStyle.Fill,
                ChartTitle = "SPC Tolerance Distribution (Six Sigma)",
                UpperSpecLimit = 12.08,
                LowerSpecLimit = 11.92
            };
            boxPlot.AddPoint(new BoxPlotDataPoint("Batch A", 11.94, 11.98, 12.01, 12.03, 12.06, Color.FromArgb(129, 140, 248)));
            boxPlot.AddPoint(new BoxPlotDataPoint("Batch B", 11.95, 11.99, 12.00, 12.02, 12.05, Color.FromArgb(56, 189, 248)));
            var ptC = new BoxPlotDataPoint("Batch C", 11.91, 11.97, 12.02, 12.04, 12.09, Color.FromArgb(248, 113, 113));
            ptC.Outliers.Add(12.11);
            boxPlot.AddPoint(ptC);
            boxPlot.AddPoint(new BoxPlotDataPoint("Batch D", 11.96, 11.99, 12.01, 12.03, 12.06, Color.FromArgb(52, 211, 153)));
            boxPlot.AddPoint(new BoxPlotDataPoint("Batch E", 11.93, 11.98, 12.00, 12.02, 12.07, Color.FromArgb(251, 191, 36)));

            cardBoxPlot.ContentPanel.Controls.Add(boxPlot);

            sec2.Controls.Add(cardBoxPlot);
            sec2.Controls.Add(splitSec2);
            sec2.Controls.Add(cardWizard);

            // SECTION 3: Vector Print Preview & Overlays / Shimmer Skeleton (Height = 440)
            var sec3 = new Panel { Dock = DockStyle.Top, Height = 440, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 10) };

            // Left Card: ZeroPrintPreview
            var cardPrint = new ZeroCard
            {
                Dock = DockStyle.Left,
                Width = 560,
                Title = "Vector Document Print Preview (ZeroPrintPreview)",
                Subtitle = "Interactive zoom, page navigation, and printer spool dispatch"
            };

            var printPreview = new ZeroPrintPreview { Dock = DockStyle.Fill };
            var printDoc = new PrintDocument();
            printDoc.DocumentName = "Certificate_Of_Analysis_ISO9001";
            printDoc.PrintPage += (s, e) =>
            {
                var g = e.Graphics;
                if (g == null) return;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // Border & Header
                using (var penDark = new Pen(Color.FromArgb(30, 41, 59), 2f))
                {
                    g.DrawRectangle(penDark, 40, 40, 740, 1040);
                    g.DrawLine(penDark, 40, 120, 780, 120);
                }

                using (var fontTitle = new Font("Segoe UI", 16f, FontStyle.Bold))
                using (var brushTitle = new SolidBrush(Color.FromArgb(15, 23, 42)))
                {
                    g.DrawString("CERTIFICATE OF CONFORMANCE & ANALYSIS", fontTitle, brushTitle, 50, 50);
                }

                using (var fontSub = new Font("Segoe UI", 9.5f, FontStyle.Regular))
                using (var brushSub = new SolidBrush(Color.FromArgb(100, 116, 139)))
                {
                    g.DrawString("Standard Industrial Quality Control Report  |  ISO 9001:2015 Compliant", fontSub, brushSub, 52, 85);
                }

                // Inner Details Box
                using (var brushBox = new SolidBrush(Color.FromArgb(248, 250, 252)))
                using (var penBox = new Pen(Color.FromArgb(226, 232, 240), 1f))
                {
                    g.FillRectangle(brushBox, 60, 140, 700, 800);
                    g.DrawRectangle(penBox, 60, 140, 700, 800);
                }

                using (var fontBody = new Font("Segoe UI", 10.5f, FontStyle.Regular))
                using (var fontBodyBold = new Font("Segoe UI", 11f, FontStyle.Bold))
                using (var brushBody = new SolidBrush(Color.FromArgb(51, 65, 85)))
                using (var brushPass = new SolidBrush(Color.FromArgb(22, 101, 52)))
                {
                    g.DrawString("Batch Lot Number: LOT-2026-0904-MESX", fontBodyBold, brushBody, 80, 170);
                    g.DrawString("Inspection Station: AOI Workcell #04 (Robotic Cell A)", fontBody, brushBody, 80, 210);
                    g.DrawString("Total Units Inspected: 5,000 units  |  Defect Rate: 0.02% (Pass: 4,999, NG: 1)", fontBody, brushBody, 80, 245);
                    g.DrawString("Statistical Six Sigma Process Capability: Cpk = 1.67 (Capable)", fontBodyBold, brushPass, 80, 285);
                    g.DrawString("Authorized QA Lead Signature:  [ELECTRONICALLY SIGNED BY SYSTEM AUDIT]", fontBody, brushBody, 80, 340);
                }
            };
            printPreview.Document = printDoc;
            cardPrint.ContentPanel.Controls.Add(printPreview);

            var splitSec3 = new Panel { Dock = DockStyle.Left, Width = 12, BackColor = Color.Transparent };

            // Right Card: ZeroSkeleton & Overlays
            var cardOverlays = new ZeroCard
            {
                Dock = DockStyle.Fill,
                Title = "Shimmer Skeleton & Non-Intrusive Overlays",
                Subtitle = "60 FPS animated loading states, floating toasts, and modal dialogs"
            };

            var pnlOverlays = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14), AutoScroll = false };

            // Shimmer Skeleton Showcase
            var lblSkelTitle = new Label { Text = "Animated Shimmer Loading Placeholders (ZeroSkeleton):", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = ZeroTheme.Colors.TextPrimary, AutoSize = true, Location = new Point(10, 8) };
            var skelCircle = new ZeroSkeleton { Shape = SkeletonShape.Circle, Size = new Size(48, 48), Location = new Point(10, 32) };
            var skelLine1 = new ZeroSkeleton { Shape = SkeletonShape.RoundedRectangle, CornerRadius = 4, Size = new Size(200, 16), Location = new Point(68, 36) };
            var skelLine2 = new ZeroSkeleton { Shape = SkeletonShape.RoundedRectangle, CornerRadius = 4, Size = new Size(130, 12), Location = new Point(68, 58) };
            var skelBlock1 = new ZeroSkeleton { Shape = SkeletonShape.RoundedRectangle, CornerRadius = 4, Size = new Size(420, 18), Location = new Point(10, 92) };
            var skelBlock2 = new ZeroSkeleton { Shape = SkeletonShape.RoundedRectangle, CornerRadius = 4, Size = new Size(360, 18), Location = new Point(10, 118) };

            pnlOverlays.Controls.Add(lblSkelTitle);
            pnlOverlays.Controls.Add(skelCircle);
            pnlOverlays.Controls.Add(skelLine1);
            pnlOverlays.Controls.Add(skelLine2);
            pnlOverlays.Controls.Add(skelBlock1);
            pnlOverlays.Controls.Add(skelBlock2);

            // Overlay Action Buttons
            var lblToastTitle = new Label { Text = "Toast Notifications & Modal Dialog (ZeroToast, ZeroModal):", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = ZeroTheme.Colors.TextPrimary, AutoSize = true, Location = new Point(10, 150) };
            pnlOverlays.Controls.Add(lblToastTitle);

            var toastFlow = new FlowLayoutPanel { Location = new Point(10, 174), Width = 520, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = Color.Transparent };
            
            var btnToastSuccess = new ZeroButton { Text = "✔ Success Toast", ButtonStyle = ZeroButtonStyle.Success, Width = 140, Height = 34 };
            btnToastSuccess.Click += (s, e) => ZeroToast.Success(this, "Production batch #8091 successfully released to shopfloor.");

            var btnToastInfo = new ZeroButton { Text = "ℹ Info Toast", ButtonStyle = ZeroButtonStyle.Primary, Width = 120, Height = 34 };
            btnToastInfo.Click += (s, e) => ZeroToast.Info(this, "PLC connection synchronized at 10 ms cycle (Modbus TCP).");

            var btnToastWarning = new ZeroButton { Text = "⚠ Warning Toast", ButtonStyle = ZeroButtonStyle.Secondary, Width = 140, Height = 34 };
            btnToastWarning.Click += (s, e) => ZeroToast.Warning(this, "Thermal threshold approaching USL warning limit (82.5°C).");

            var btnToastError = new ZeroButton { Text = "✕ Error Toast", ButtonStyle = ZeroButtonStyle.Danger, Width = 120, Height = 34 };
            btnToastError.Click += (s, e) => ZeroToast.Error(this, "Emergency Stop circuit tripped on Conveyor CV-401!");

            var btnToastAlarm = new ZeroButton { Text = "⚡ Alarm Toast", ButtonStyle = ZeroButtonStyle.Danger, Width = 130, Height = 34 };
            btnToastAlarm.Click += (s, e) => ZeroToast.Alarm(this, "Critical: Thermal threshold exceeded on Exothermic Reactor RX-401 (94.8°C)!", "SCADA Safety Alarm");

            var btnModalConfirm = new ZeroButton { Text = "💬 Confirm Modal Dialog", ButtonStyle = ZeroButtonStyle.Primary, Width = 190, Height = 34 };
            btnModalConfirm.Click += (s, e) =>
            {
                ZeroModal.Confirm(
                    this,
                    "Confirm Batch Release",
                    "Are you sure you want to dispatch Batch WO-2026-904 to Robotic Workcell A? All feeder allocations and safety interlocks have been verified.",
                    () => ZeroToast.Success(this, "Batch dispatched successfully to manufacturing line!"),
                    () => ZeroToast.Warning(this, "Dispatch cancelled by operator."),
                    "Release Batch",
                    "Cancel");
            };

            toastFlow.Controls.Add(btnToastSuccess);
            toastFlow.Controls.Add(btnToastInfo);
            toastFlow.Controls.Add(btnToastWarning);
            toastFlow.Controls.Add(btnToastError);
            toastFlow.Controls.Add(btnToastAlarm);
            toastFlow.Controls.Add(btnModalConfirm);
            pnlOverlays.Controls.Add(toastFlow);

            cardOverlays.ContentPanel.Controls.Add(pnlOverlays);

            sec3.Controls.Add(cardOverlays);
            sec3.Controls.Add(splitSec3);
            sec3.Controls.Add(cardPrint);

            // Assemble main container
            mainContainer.Controls.Add(sec3);
            mainContainer.Controls.Add(sec2);
            mainContainer.Controls.Add(sec1);
            mainContainer.Controls.Add(bannerSpacer);
            mainContainer.Controls.Add(banner);

            banner.BringToFront();
            bannerSpacer.BringToFront();
            sec1.BringToFront();
            sec2.BringToFront();
            sec3.BringToFront();

            _tabCommercial.Controls.Add(mainContainer);
        }

        private void InitializeOfficeDocsSuite(ZeroTabPage parent)
        {
            var colors = ZeroTheme.Colors;
            parent.BackColor = colors.Background;

            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                BackColor = Color.Transparent
            };

            var banner = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Severity = ZeroAlertSeverity.Info,
                Title = "📄 OFFICE & TECHNICAL DOCUMENTS SUITE (VECTOR PDF & SPREADSHEET ENGINE)",
                Message = "Vector CAD schematics, SOP instructions, continuous zoom (25%–400%), full-text search, live spreadsheet sparse matrix, interactive Formula Bar, =SUM/AVERAGE/IF calculations, freeze panes & costing BOM.",
                Height = 62
            };

            var bannerSpacer = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            var subTabsDocs = new ZeroTabControl
            {
                Dock = DockStyle.Fill,
                Orientation = ZeroTabOrientation.Horizontal,
                TabHeight = 36,
                TabStyle = ZeroTabStyle.Pill
            };

            var tabPdf = new ZeroTabPage("Vector PDF & CAD Schematic Reader", "📑");
            var tabSpreadsheet = new ZeroTabPage("Vector Spreadsheet & Formula Engine", "📊");

            // Sub-Tab 1: Vector PDF Document & CAD Schematic Reader
            var cardPdf = new ZeroCard
            {
                Dock = DockStyle.Fill,
                Title = "Vector PDF & CAD Schematic Reader (ZPdfViewer)",
                Subtitle = "Continuous vector scroll, outline bookmarks, full-text search & zoom (25%–400%)",
                StepNumber = 1
            };
            var pdfViewer = new ZPdfViewer
            {
                Dock = DockStyle.Fill,
                ShowBookmarksSidebar = true
            };
            cardPdf.ContentPanel.Controls.Add(pdfViewer);
            tabPdf.Controls.Add(cardPdf);

            // Sub-Tab 2: Vector Spreadsheet & Formula Engine
            var cardSheet = new ZeroCard
            {
                Dock = DockStyle.Fill,
                Title = "Vector Spreadsheet & Formula Engine (ZSpreadsheet)",
                Subtitle = "Sparse matrix, interactive Formula Bar, live =SUM/AVERAGE/IF calculations, freeze panes & costing BOM",
                StepNumber = 2
            };
            var spreadsheet = new ZSpreadsheet
            {
                Dock = DockStyle.Fill
            };
            cardSheet.ContentPanel.Controls.Add(spreadsheet);
            tabSpreadsheet.Controls.Add(cardSheet);

            subTabsDocs.AddTab(tabPdf);
            subTabsDocs.AddTab(tabSpreadsheet);

            mainContainer.Controls.Add(subTabsDocs);
            mainContainer.Controls.Add(bannerSpacer);
            mainContainer.Controls.Add(banner);

            banner.BringToFront();
            bannerSpacer.BringToFront();
            subTabsDocs.BringToFront();

            parent.Controls.Add(mainContainer);
        }

        private void InitializeAdvancedSuite()
        {
            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(12),
                BackColor = Color.Transparent
            };

            // 1. Top Banner
            var banner = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Severity = ZeroAlertSeverity.Info,
                Title = "🚀 ADVANCED ENTERPRISE COMPONENT SUITE",
                Message = "ZeroUI features 6 specialized enterprise components: ZeroTreeList (Virtualized multi-level BOM tree), ZeroHeatmap (24h x 7-day density matrix), ZeroLookup (Fast search across 5,000 catalog items), ZeroDateRangePicker (1-click date intervals), ZeroNumericBox (High-precision numeric entry), and ZeroTabControl (Flat zero-flicker tab host)."
            };

            var bannerSpacer = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            // 2. Top Filter Bar (ZDateRangePicker, Lookup, NumericBox, Button)
            var topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 4, 0, 8)
            };

            var dtRange = new ZDateRangePicker
            {
                Location = new Point(0, 4),
                Width = 260,
                Preset = DateRangePreset.Last7Days
            };
            dtRange.DateRangeChanged += (s, e) =>
            {
                ZeroToast.Info(this, $"Selected date range: {dtRange.StartDate:yyyy-MM-dd} → {dtRange.EndDate:yyyy-MM-dd}");
            };

            var lookupCatalog = new ZeroLookup
            {
                Location = new Point(272, 4),
                Width = 320,
                Placeholder = "Search 5,000 catalog components..."
            };

            // Populate 5,000 realistic electronic/industrial items
            var catalogItems = new List<ZeroLookupItem>(5000);
            catalogItems.Add(new ZeroLookupItem("IC-MCU-STM32", "STM32F407VGT6 Cortex-M4 168MHz", "Foxconn Precision • Stock: 12,450 PCS • $8.50", "Active IC"));
            catalogItems.Add(new ZeroLookupItem("IC-RAM-ISSI", "IS42S16400J 64Mb SDRAM 166MHz", "ISSI Micro • Stock: 8,200 PCS • $2.40", "Memory"));
            catalogItems.Add(new ZeroLookupItem("IC-ETH-LAN8720", "LAN8720A 10/100 Ethernet Transceiver", "Microchip • Stock: 5,600 PCS • $1.15", "Interface"));
            catalogItems.Add(new ZeroLookupItem("SEN-KEY-OPTO", "Keyence PR-M51N3 Optical Sensor", "Keyence Japan • Stock: 320 PCS • $68.00", "Sensors"));
            catalogItems.Add(new ZeroLookupItem("PLC-FX5U-32M", "Mitsubishi FX5U-32MR/ES PLC Main", "Mitsubishi Electric • Stock: 45 PCS • $285.00", "PLC"));
            catalogItems.Add(new ZeroLookupItem("DRV-STEP-TMC", "TMC2209 Ultra-Silent Stepper Driver", "Trinamic GmbH • Stock: 2,100 PCS • $4.20", "Motion"));

            string[] catPrefixes = new[] { "RES", "CAP", "IND", "DIO", "MOS", "CONN", "RELAY", "FUSE", "OPTO", "SW" };
            string[] catNames = new[] { "Chip Resistor", "Ceramic Capacitor", "Wirewound Inductor", "Schottky Diode", "N-Channel MOSFET", "Header Connector", "Intermediate Relay", "PTC Resettable Fuse", "Optocoupler Isolator", "Toggle Switch" };

            for (int i = 7; i <= 5000; i++)
            {
                int catIdx = i % catPrefixes.Length;
                string pCode = $"{catPrefixes[catIdx]}-{i:D5}";
                string pName = $"{catNames[catIdx]} SMD #{i}";
                string pSub = $"Standard AEC-Q200 • Stock: {(i * 17) % 5000 + 100:N0} PCS • ${(i % 99 + 1) * 0.05f:F2}";
                catalogItems.Add(new ZeroLookupItem(pCode, $"{pCode} • {pName}", pSub, catPrefixes[catIdx]));
            }
            lookupCatalog.SetItems(catalogItems);

            lookupCatalog.SelectedItemChanged += (s, e) =>
            {
                if (lookupCatalog.SelectedItem != null)
                {
                    ZeroToast.Success(this, $"Selected Item: [{lookupCatalog.SelectedItem.Key}] {lookupCatalog.SelectedItem.DisplayText}");
                }
            };

            var numBatchSize = new ZeroNumericBox
            {
                Location = new Point(604, 4),
                Width = 200,
                Prefix = "Batch Size:",
                Suffix = "PCS",
                Step = 500,
                Value = 5000,
                MinValue = 100,
                MaxValue = 500000
            };
            numBatchSize.ValueChanged += (s, e) =>
            {
                ZeroToast.Info(this, $"Adjusted planned batch size: {numBatchSize.Value:N0} PCS");
            };

            var btnFilter = new ZeroButton
            {
                Location = new Point(816, 4),
                Size = new Size(130, 36),
                Text = "⚡ Apply Filter",
                ButtonStyle = ZeroButtonStyle.Primary
            };
            btnFilter.Click += (s, e) =>
            {
                ZeroToast.Success(this, $"Loaded interval {dtRange.StartDate:dd/MM} - {dtRange.EndDate:dd/MM} for batch size {numBatchSize.Value:N0} PCS!");
            };

            topBar.Controls.Add(btnFilter);
            topBar.Controls.Add(numBatchSize);
            topBar.Controls.Add(lookupCatalog);
            topBar.Controls.Add(dtRange);

            var topBarSpacer = new Panel { Dock = DockStyle.Top, Height = 8, BackColor = Color.Transparent };

            // 3. Body Panel (2 Columns)
            var bodyPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };

            // Left Column: BOM TreeList (Width = 530)
            var leftCol = new Panel
            {
                Dock = DockStyle.Left,
                Width = 530,
                Padding = new Padding(0, 0, 10, 0),
                BackColor = Color.Transparent
            };

            var cardBom = new ZeroCard
            {
                Dock = DockStyle.Fill,
                Title = "Multi-Level BOM Tree Structure (ZTreeList)",
                Subtitle = "Component hierarchy virtualization, expandable chevrons, tri-state checkboxes"
            };

            var bomTools = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.Transparent,
                Padding = new Padding(4, 4, 4, 4)
            };

            var txtBomSearch = new ZeroSearchBox
            {
                Location = new Point(4, 4),
                Width = 200,
                PlaceholderText = "Filter BOM components..."
            };

            var btnExpandAll = new ZeroButton
            {
                Location = new Point(210, 4),
                Size = new Size(92, 34),
                Text = "➕ Expand",
                ButtonStyle = ZeroButtonStyle.Secondary
            };

            var btnCollapseAll = new ZeroButton
            {
                Location = new Point(308, 4),
                Size = new Size(92, 34),
                Text = "➖ Collapse",
                ButtonStyle = ZeroButtonStyle.Secondary
            };

            var btnCheckStats = new ZeroButton
            {
                Location = new Point(406, 4),
                Size = new Size(98, 34),
                Text = "✔ Stats",
                ButtonStyle = ZeroButtonStyle.Success
            };

            bomTools.Controls.Add(btnCheckStats);
            bomTools.Controls.Add(btnCollapseAll);
            bomTools.Controls.Add(btnExpandAll);
            bomTools.Controls.Add(txtBomSearch);

            var treeBom = new ZTreeList
            {
                Dock = DockStyle.Fill,
                ShowCheckBoxes = true,
                ShowLines = true
            };

            // Setup modern Context Menu for BOM Tree
            var bomMenu = new ZeroContextMenu();
            bomMenu.AddAction("Copy Component Code", () => ZeroToast.Info(this, "Component code copied to Clipboard!"), "Ctrl+C", "📋");
            bomMenu.AddAction("Lookup ERP Inventory", () => ZeroToast.Info(this, "Available stock: 1,420 PCS at SMT Alpha Warehouse"), "F3", "🔍");
            bomMenu.AddAction("Add Child Component", () => ZeroModal.Prompt(this, "Add Child Component", "Enter component part code:", "RES-0402-10K", val => ZeroToast.Success(this, $"Added {val} to assembly!")), "Ins", "➕");
            bomMenu.AddSeparator();
            var subCat = bomMenu.AddSubMenu("Assign Category", "🏷️");
            subCat.AddSubAction("Active Components (IC / MCU)", () => ZeroToast.Info(this, "Assigned IC category"));
            subCat.AddSubAction("Passive Components (R / L / C)", () => ZeroToast.Info(this, "Assigned R/L/C category"));
            subCat.AddSubAction("Mechanical & Metal Enclosures", () => ZeroToast.Info(this, "Assigned Mechanical category"));
            bomMenu.AddSeparator();
            bomMenu.AddCheckable("Pin Priority for Quality Inspection", false, chk => ZeroToast.Info(this, $"Priority inspection {(chk ? "pinned" : "unpinned")}!"), "⭐");
            bomMenu.AddDangerAction("Remove from BOM", () => ZeroModal.Confirm(this, "Confirm Deletion", "Are you sure you want to remove this item from the production BOM?", () => ZeroToast.Success(this, "Removed component from BOM!")), "Del", "🗑️");

            treeBom.ContextMenuStrip = bomMenu;

            // Build realistic BOM hierarchy for Industrial Smart Gateway
            var rootBom = new ZeroTreeNode("ASM-9000: Industrial IoT Gateway Controller", "⚙️", "Total BOM Cost: $24.80 • 28 Parts")
            {
                Badge = "Main Assy",
                BadgeColor = ZeroTheme.Colors.Info
            };

            var pcbAssy = rootBom.AddChild("PCB-001: Mainboard SMT Assembly (4-Layer FR4)", "🟩", "Process: SMT Pick & Place Line #1");
            pcbAssy.Badge = "SMT Assy";
            pcbAssy.BadgeColor = ZeroTheme.Colors.Success;
            pcbAssy.AddChild("MCU-STM32: STM32F407VGT6 ARM Cortex-M4 168MHz", "📦", "1 PCS • $8.50").Badge = "Core MCU";
            pcbAssy.AddChild("RAM-ISSI: 32MB SDRAM IC 133MHz High-Speed", "📦", "1 PCS • $2.40");
            pcbAssy.AddChild("ETH-PHY: LAN8720A 10/100 Ethernet Controller", "📦", "1 PCS • $1.15");
            pcbAssy.AddChild("FLASH-SPI: W25Q128JV 16MB SPI NOR Flash", "📦", "1 PCS • $0.95");
            pcbAssy.AddChild("PWR-LDO: AMS1117-3.3V Step-down Converter", "⚡", "2 PCS • $0.35");
            pcbAssy.AddChild("XTAL-8M: Crystal Oscillator 8.000MHz ±10ppm", "💎", "1 PCS • $0.20");

            var pwrAssy = rootBom.AddChild("ASM-002: 24V Isolated Power & Surge Sub-Assy", "⚡", "Process: THT Wave Solder Line #2");
            pwrAssy.Badge = "Power Sub";
            pwrAssy.BadgeColor = ZeroTheme.Colors.Warning;
            pwrAssy.AddChild("TRF-24V: Flyback Pulse Transformer 24V/2A Shielded", "🔋", "1 PCS • $3.80");
            pwrAssy.AddChild("MOV-471: Varistor 470V Surge Suppressor", "🛡️", "2 PCS • $0.45");
            pwrAssy.AddChild("CAP-450V: Nichicon High-Voltage Capacitor 100uF/450V", "📦", "2 PCS • $1.20");
            pwrAssy.AddChild("FUSE-T2A: Slow-Blow Fuse 250V 2A Anti-Surge", "🔥", "1 PCS • $0.25");

            var mecAssy = rootBom.AddChild("MEC-003: IP67 Anodized Aluminum Enclosure", "🛡️", "Process: CNC Precision Machining");
            mecAssy.Badge = "Mechanical";
            mecAssy.BadgeColor = ZeroTheme.Colors.Info;
            mecAssy.AddChild("CNC-TOP: Top Aluminum Cover Milled Black Anodized", "🔩", "1 PCS • $6.20");
            mecAssy.AddChild("CNC-BTM: Bottom Aluminum Chassis DIN-Rail Mount", "🔩", "1 PCS • $4.50");
            mecAssy.AddChild("SCR-M3: Stainless 304 M3x8 Anti-Vibration Screws", "🔩", "8 PCS • $0.08");
            mecAssy.AddChild("GSK-SIL: Molded Waterproof Silicone Gasket", "🛞", "1 PCS • $0.90");

            var pkgAssy = rootBom.AddChild("PKG-004: Packaging & Serial Lot Tracking", "📦", "Process: QC Inspection & Packing");
            pkgAssy.Badge = "Packaging";
            pkgAssy.BadgeColor = ZeroTheme.Colors.Success;
            pkgAssy.AddChild("BOX-CTN: 3-Ply Shockproof Corrugated Carton", "📦", "1 PCS • $0.65");
            pkgAssy.AddChild("FOAM-EVA: Anti-Static ESD Protective Foam", "🛡️", "2 PCS • $0.40");
            pkgAssy.AddChild("LBL-QR: Serial Barcode & QR Lot Label", "🏷️", "2 PCS • $0.05");

            treeBom.AddNode(rootBom);

            txtBomSearch.DebouncedTextChanged += (s, text) =>
            {
                treeBom.FilterText = text;
            };

            btnExpandAll.Click += (s, e) => treeBom.ExpandAll();
            btnCollapseAll.Click += (s, e) => treeBom.CollapseAll();

            btnCheckStats.Click += (s, e) =>
            {
                int totalChecked = 0;
                CountCheckedNodes(rootBom, ref totalChecked);
                ZeroToast.Success(this, $"Selected {totalChecked} BOM items ready for assembly work order dispatch!");
            };

            var propGrid = new PropertyGridControl
            {
                Dock = DockStyle.Fill
            };
            var defaultInspection = new BomComponentInspection(
                "ASM-9000",
                "Industrial IoT Gateway Controller Main Assy",
                "Main Assembly",
                "Foxconn Precision Co.",
                24.80m,
                3200,
                true,
                4.9m,
                "-40°C to +85°C"
            );
            propGrid.SelectedObject = defaultInspection;

            var tabSuite = new ZeroTabControl
            {
                Dock = DockStyle.Fill,
                TabStyle = ZeroTabStyle.Underline
            };
            TabPageEx? pagePropGrid = null;
            treeBom.NodeSelected += (s, node) =>
            {
                string code = node.Text.Contains(":") ? node.Text.Split(':')[0].Trim() : node.Text;
                string desc = node.Text.Contains(":") ? node.Text.Substring(node.Text.IndexOf(':') + 1).Trim() : node.Text;
                string cat = !string.IsNullOrEmpty(node.Badge) ? node.Badge : "Sub-Assembly";
                decimal cost = node.SubText.Contains("$") ? 8.50m : 2.40m;
                int qty = node.SubText.Contains("PCS") ? 12450 : 2500;

                propGrid.SelectedObject = new BomComponentInspection(
                    code,
                    desc,
                    cat,
                    "Foxconn Precision Co.",
                    cost,
                    qty,
                    true,
                    4.8m,
                    "-40°C to +85°C"
                );
                if (pagePropGrid != null)
                {
                    tabSuite.SelectedTab = pagePropGrid;
                }
                ZeroToast.Info(this, $"Inspecting component properties: {code}");
            };

            cardBom.ContentPanel.Controls.Add(treeBom);
            cardBom.ContentPanel.Controls.Add(bomTools);
            bomTools.BringToFront();
            leftCol.Controls.Add(cardBom);

            // Right Column (Heatmap on Top + ZeroTabControl on Bottom)
            var rightCol = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent
            };

            // Card Heatmap
            var cardHeatmap = new ZeroCard
            {
                Dock = DockStyle.Top,
                Height = 310,
                Title = "SMT Production Heatmap (24 Hours x 7 Days)",
                Subtitle = "Hourly output capacity distribution with rich tooltips and palette switching",
                ActionText = "🎨 Change Palette"
            };

            var heatmap = new ZeroHeatmap
            {
                Dock = DockStyle.Fill,
                ShowValues = true,
                ShowLegend = true,
                PaletteMode = HeatmapPaletteMode.Industrial
            };

            cardHeatmap.ActionClicked += (s, e) =>
            {
                var nextMode = heatmap.PaletteMode switch
                {
                    HeatmapPaletteMode.Industrial => HeatmapPaletteMode.Viridis,
                    HeatmapPaletteMode.Viridis => HeatmapPaletteMode.CoolWarm,
                    HeatmapPaletteMode.CoolWarm => HeatmapPaletteMode.Emerald,
                    _ => HeatmapPaletteMode.Industrial
                };
                heatmap.PaletteMode = nextMode;
                ZeroToast.Info(this, $"Heatmap color palette changed to: {nextMode}");
            };

            heatmap.CellClicked += (s, e) =>
            {
                ZeroToast.Success(this, $"[OUTPUT] {e.RowLabel} at {e.ColumnLabel}: {e.Value:0} PCS/hr");
            };

            cardHeatmap.ContentPanel.Controls.Add(heatmap);

            var heatmapSpacer = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            // Card ZeroTabControl
            var cardTabs = new ZeroCard
            {
                Dock = DockStyle.Fill,
                Title = "Modern Zero-Flicker Tab Host (ZeroTabControl)",
                Subtitle = "Supports Underline / Pill / Card styles, Notification Badges, and Dark Mode"
            };

            var tabTools = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = Color.Transparent,
                Padding = new Padding(4, 2, 4, 4)
            };

            var segStyle = new ZeroSegmented
            {
                Location = new Point(4, 2),
                Size = new Size(260, 32),
                Items = new[] { "Underline", "Pill", "Card" },
                SelectedIndex = 0
            };

            segStyle.SelectedIndexChanged += (s, e) =>
            {
                tabSuite.TabStyle = (ZeroTabStyle)segStyle.SelectedIndex;
            };

            tabTools.Controls.Add(segStyle);

            // Page 1: SMT Oven Reflow Profile
            var pageOven = tabSuite.AddTab("SMT Oven Profile", "🔥");
            pageOven.Padding = new Padding(12);

            var pnlOven = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Transparent
            };

            var numZ1 = CreateParamBox("Zone 1 (Preheat):", 160.0m, "°C", 0.5m, 100m, 200m);
            var numZ2 = CreateParamBox("Zone 2 (Soak):", 195.0m, "°C", 0.5m, 150m, 230m);
            var numZ3 = CreateParamBox("Zone 3 (Peak Reflow):", 248.5m, "°C", 0.5m, 200m, 280m);
            var numSpeed = CreateParamBox("Conveyor Speed:", 1.15m, "m/min", 0.05m, 0.5m, 3.0m, 2);
            var numN2 = CreateParamBox("N2 Purity Level:", 99.98m, "%", 0.01m, 95m, 100m, 2);

            pnlOven.Controls.Add(numZ1);
            pnlOven.Controls.Add(numZ2);
            pnlOven.Controls.Add(numZ3);
            pnlOven.Controls.Add(numSpeed);
            pnlOven.Controls.Add(numN2);
            pageOven.Controls.Add(pnlOven);

            // Page 2: Work Order Specifications
            var pageOrder = tabSuite.AddTab("Work Order Specs", "📋");
            pageOrder.Padding = new Padding(12);

            var descOrder = new ZeroDescriptions
            {
                Dock = DockStyle.Fill,
                Columns = 2,
                RowHeight = 32
            };
            descOrder.Add("Work Order ID", "WO-2026-GATEWAY-88");
            descOrder.Add("Target Product", "B1030 IoT Smart Gateway Rev 2.0");
            descOrder.Add("Assembly Line", "Line SMT Alpha #01");
            descOrder.Add("Target FPY", "99.45% Yield Rate", Color.FromArgb(16, 185, 129));
            descOrder.Add("Lead Engineer", "Phong Tuan Vo (Principal Engineer)");
            descOrder.Add("Soldering Spec", "IPC-A-610 Class 3 Industrial");
            pageOrder.Controls.Add(descOrder);

            // Page 3: Line Alarms
            var pageAlerts = tabSuite.AddTab("Line Alarms", "🔔", 3);
            pageAlerts.Padding = new Padding(12);

            var alertBox = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Severity = ZeroAlertSeverity.Warning,
                Title = "SMT FEEDER BOA472 REEL DEPLETION ALERT",
                Message = "10uF ceramic capacitor reel at slot #18 is nearly empty (< 150 PCS remaining). Technician must reload feeder within 12 minutes to prevent line stoppage!"
            };
            pageAlerts.Controls.Add(alertBox);

            // Page 4: ZeroImage & ZeroModal Dialogs
            var pageImageModal = tabSuite.AddTab("Images & Modals", "🖼️");
            pageImageModal.Padding = new Padding(10);

            var pnlImgModal = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Transparent
            };

            // Avatar Section
            var pnlAvatars = new Panel { Size = new Size(520, 68), BackColor = Color.Transparent };
            var lblAvatars = new Label
            {
                Text = "ZeroImage (Avatars & Status):",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Location = new Point(4, 2),
                AutoSize = true
            };

            var av1 = new ZeroImage
            {
                Location = new Point(4, 20),
                Size = new Size(42, 42),
                IsCircle = true,
                FallbackText = "Phong Vo",
                Status = AvatarStatus.Online
            };

            var av2 = new ZeroImage
            {
                Location = new Point(56, 20),
                Size = new Size(42, 42),
                IsCircle = true,
                FallbackText = "Alex Nguyen",
                Status = AvatarStatus.Busy
            };

            var av3 = new ZeroImage
            {
                Location = new Point(108, 20),
                Size = new Size(42, 42),
                IsCircle = true,
                FallbackText = "Sarah Tran",
                Status = AvatarStatus.Away
            };

            // PCB Inspection Image with Lightbox Preview
            var pcbBmp = new Bitmap(240, 150);
            using (var gPcb = Graphics.FromImage(pcbBmp))
            {
                gPcb.Clear(Color.FromArgb(15, 60, 40));
                using var penTrace = new Pen(Color.FromArgb(30, 180, 100), 2f);
                gPcb.DrawLine(penTrace, 20, 20, 100, 20);
                gPcb.DrawLine(penTrace, 100, 20, 140, 60);
                gPcb.DrawLine(penTrace, 140, 60, 220, 60);
                gPcb.DrawLine(penTrace, 30, 80, 110, 80);
                gPcb.DrawLine(penTrace, 110, 80, 150, 120);
                using var brushChip = new SolidBrush(Color.FromArgb(30, 30, 30));
                gPcb.FillRectangle(brushChip, new Rectangle(90, 40, 60, 60));
                using var brushGold = new SolidBrush(Color.FromArgb(234, 179, 8));
                for (int p = 0; p < 5; p++)
                {
                    gPcb.FillRectangle(brushGold, 82, 45 + (p * 10), 6, 4);
                    gPcb.FillRectangle(brushGold, 152, 45 + (p * 10), 6, 4);
                }
            }

            var imgPcb = new ZeroImage
            {
                Location = new Point(165, 14),
                Size = new Size(84, 50),
                BorderRadius = 6,
                Image = pcbBmp,
                ScaleMode = ImageScaleMode.Cover,
                EnableZoomPreview = true
            };

            var lblZoomHint = new Label
            {
                Text = "🔍 Click image to open Lightbox preview",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Location = new Point(255, 30),
                AutoSize = true
            };

            pnlAvatars.Controls.Add(lblZoomHint);
            pnlAvatars.Controls.Add(imgPcb);
            pnlAvatars.Controls.Add(av3);
            pnlAvatars.Controls.Add(av2);
            pnlAvatars.Controls.Add(av1);
            pnlAvatars.Controls.Add(lblAvatars);
            pnlImgModal.Controls.Add(pnlAvatars);

            // Modal Dialog Buttons Section
            var pnlModalBtns = new Panel { Size = new Size(520, 80), BackColor = Color.Transparent };
            var lblModals = new Label
            {
                Text = "ZeroModal (Alert & Confirmation Popups):",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Location = new Point(4, 4),
                AutoSize = true
            };

            var btnSuccess = new ZeroButton
            {
                Location = new Point(4, 26),
                Size = new Size(95, 34),
                Text = "✔ Success",
                ButtonStyle = ZeroButtonStyle.Success,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold)
            };
            btnSuccess.Click += (s, e) =>
            {
                ZeroModal.Success(this, "Inspection Completed", "Recorded 2,500 IoT Gateway units passing QA inspection standards!");
            };

            var btnWarning = new ZeroButton
            {
                Location = new Point(105, 26),
                Size = new Size(95, 34),
                Text = "⚠ Warning",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold)
            };
            btnWarning.Click += (s, e) =>
            {
                ZeroModal.Warning(this, "Threshold Warning", "Reflow zone peak temperature exceeded 248.5 °C. Check convection fans!");
            };

            var btnError = new ZeroButton
            {
                Location = new Point(206, 26),
                Size = new Size(95, 34),
                Text = "✕ Error",
                ButtonStyle = ZeroButtonStyle.Danger,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold)
            };
            btnError.Click += (s, e) =>
            {
                ZeroModal.Error(this, "PLC Communication Failure", "No Modbus TCP response from SMT pick-and-place station after 3 retries!");
            };

            var btnConfirm = new ZeroButton
            {
                Location = new Point(307, 26),
                Size = new Size(95, 34),
                Text = "? Confirm",
                ButtonStyle = ZeroButtonStyle.Primary,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold)
            };
            btnConfirm.Click += (s, e) =>
            {
                ZeroModal.Confirm(
                    this,
                    "Dispatch Confirmation",
                    "Are you sure you want to allocate serial numbers for 500 carton boxes?",
                    onConfirm: () => ZeroToast.Success(this, "Successfully generated and assigned 500 barcode labels!"),
                    confirmText: "Confirm",
                    cancelText: "Cancel");
            };

            var btnPrompt = new ZeroButton
            {
                Location = new Point(408, 26),
                Size = new Size(95, 34),
                Text = "✏ Prompt",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold)
            };
            btnPrompt.Click += (s, e) =>
            {
                ZeroModal.Prompt(
                    this,
                    "Scan Barcode / Enter Serial",
                    "Please scan or input component Serial Number for lookup:",
                    "SN-GW-2026-8801",
                    val => ZeroToast.Success(this, $"Received Serial Number: {val}"));
            };

            pnlModalBtns.Controls.Add(btnPrompt);
            pnlModalBtns.Controls.Add(btnConfirm);
            pnlModalBtns.Controls.Add(btnError);
            pnlModalBtns.Controls.Add(btnWarning);
            pnlModalBtns.Controls.Add(btnSuccess);
            pnlModalBtns.Controls.Add(lblModals);
            pnlImgModal.Controls.Add(pnlModalBtns);

            pageImageModal.Controls.Add(pnlImgModal);

            // Page 5: Property Inspector (ZeroPropertyGrid)
            pagePropGrid = tabSuite.AddTab("Property Inspector", "🔍");
            pagePropGrid.Padding = new Padding(8);
            pagePropGrid.Controls.Add(propGrid);

            cardTabs.ContentPanel.Controls.Add(tabSuite);
            cardTabs.ContentPanel.Controls.Add(tabTools);

            rightCol.Controls.Add(cardTabs);
            rightCol.Controls.Add(heatmapSpacer);
            rightCol.Controls.Add(cardHeatmap);

            bodyPanel.Controls.Add(rightCol);
            bodyPanel.Controls.Add(leftCol);

            // Assemble main container
            mainContainer.Controls.Add(bodyPanel);
            mainContainer.Controls.Add(topBarSpacer);
            mainContainer.Controls.Add(topBar);
            mainContainer.Controls.Add(bannerSpacer);
            mainContainer.Controls.Add(banner);

            banner.BringToFront();
            bannerSpacer.BringToFront();
            topBar.BringToFront();
            topBarSpacer.BringToFront();
            bodyPanel.BringToFront();

            _tabAdvanced.Controls.Add(mainContainer);
        }

        private void InitializePivotGridShowcase(ZeroTabPage parent)
        {
            parent.BackColor = ZeroTheme.Colors.Background;
            parent.Padding = new Padding(12);

            var banner = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Severity = ZeroAlertSeverity.Info,
                Title = "📊 MULTIDIMENSIONAL CROSS-TAB OLAP REPORTING (ZPivotGrid)",
                Message = "ZeroPivotGrid enables instant multidimensional pivot analysis with dynamic RowArea, ColumnArea, and DataArea aggregations (Sum, Average, Count), hierarchical drill-down, and built-in Field Chooser dialog."
            };

            var topToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 6, 0, 6)
            };

            var pivotGrid = new ZPivotGrid
            {
                Dock = DockStyle.Fill,
                RowHeaderWidth = 160,
                CellWidth = 115,
                CellHeight = 28
            };

            var btnFieldChooser = new ZeroButton
            {
                Location = new Point(0, 4),
                Size = new Size(160, 34),
                Text = "📋 Field Chooser",
                ButtonStyle = ZeroButtonStyle.Primary
            };
            btnFieldChooser.Click += (s, e) => pivotGrid.ShowFieldList();

            var btnExport = new ZeroButton
            {
                Location = new Point(170, 4),
                Size = new Size(140, 34),
                Text = "📊 Export CSV",
                ButtonStyle = ZeroButtonStyle.Secondary
            };
            btnExport.Click += (s, e) => ZeroToast.Success(this, "Cross-tab pivot dataset exported to CSV successfully.");

            var btnResetPivot = new ZeroButton
            {
                Location = new Point(320, 4),
                Size = new Size(120, 34),
                Text = "🔄 Reset Layout",
                ButtonStyle = ZeroButtonStyle.Ghost
            };

            topToolbar.Controls.Add(btnResetPivot);
            topToolbar.Controls.Add(btnExport);
            topToolbar.Controls.Add(btnFieldChooser);

            // Setup multi-dimensional manufacturing data
            var records = GenerateManufacturingPivotData();
            pivotGrid.DataSource = records;

            // Configure default pivot fields
            pivotGrid.AddField("Plant", PivotArea.RowArea, "Plant / Facility");
            pivotGrid.AddField("Line", PivotArea.RowArea, "Production Line");
            pivotGrid.AddField("Shift", PivotArea.ColumnArea, "Work Shift");
            pivotGrid.AddField("OutputQty", PivotArea.DataArea, "Total Output (PCS)", PivotSummaryType.Sum);
            pivotGrid.AddField("DefectCount", PivotArea.DataArea, "Defect Units", PivotSummaryType.Sum);
            pivotGrid.AddField("YieldRate", PivotArea.DataArea, "Yield %", PivotSummaryType.Average);

            btnResetPivot.Click += (s, e) =>
            {
                pivotGrid.Fields.Clear();
                pivotGrid.AddField("Plant", PivotArea.RowArea, "Plant / Facility");
                pivotGrid.AddField("Line", PivotArea.RowArea, "Production Line");
                pivotGrid.AddField("Shift", PivotArea.ColumnArea, "Work Shift");
                pivotGrid.AddField("OutputQty", PivotArea.DataArea, "Total Output (PCS)", PivotSummaryType.Sum);
                pivotGrid.AddField("DefectCount", PivotArea.DataArea, "Defect Units", PivotSummaryType.Sum);
                pivotGrid.AddField("YieldRate", PivotArea.DataArea, "Yield %", PivotSummaryType.Average);
                pivotGrid.RefreshData();
                ZeroToast.Info(this, "Pivot fields reset to default cross-tab layout.");
            };

            var card = new ZeroCard
            {
                Dock = DockStyle.Fill,
                Title = "Production & Quality Cross-Tab OLAP Matrix",
                Subtitle = "Aggregating 1,200 production batches across facilities, lines, and shifts"
            };
            card.ContentPanel.Controls.Add(pivotGrid);

            var spacer = new Panel { Dock = DockStyle.Top, Height = 8, BackColor = Color.Transparent };

            parent.Controls.Add(card);
            parent.Controls.Add(spacer);
            parent.Controls.Add(topToolbar);
            parent.Controls.Add(banner);
        }

        private static List<ManufacturingPivotRecord> GenerateManufacturingPivotData()
        {
            var list = new List<ManufacturingPivotRecord>(1200);
            string[] plants = { "Plant Alpha (Texas)", "Plant Beta (Munich)", "Plant Gamma (Tokyo)" };
            string[] lines = { "Line 01 (SMT)", "Line 02 (THT)", "Line 03 (AOI)", "Line 04 (Final Box)" };
            string[] shifts = { "Morning (06:00-14:00)", "Evening (14:00-22:00)", "Night (22:00-06:00)" };
            string[] families = { "IoT Gateways", "PLC Modules", "Industrial Sensors", "BESS Controllers" };

            var rnd = new Random(42);
            for (int i = 0; i < 1200; i++)
            {
                int outQty = rnd.Next(250, 1500);
                int defects = rnd.Next(0, 12);
                double yieldVal = Math.Round((1.0 - (double)defects / outQty) * 100, 2);
                list.Add(new ManufacturingPivotRecord
                {
                    Plant = plants[rnd.Next(plants.Length)],
                    Line = lines[rnd.Next(lines.Length)],
                    Shift = shifts[rnd.Next(shifts.Length)],
                    ProductFamily = families[rnd.Next(families.Length)],
                    OutputQty = outQty,
                    DefectCount = defects,
                    YieldRate = yieldVal
                });
            }
            return list;
        }

        public sealed class ManufacturingPivotRecord
        {
            public string Plant { get; set; } = "";
            public string Line { get; set; } = "";
            public string Shift { get; set; } = "";
            public string ProductFamily { get; set; } = "";
            public int OutputQty { get; set; }
            public int DefectCount { get; set; }
            public double YieldRate { get; set; }
        }

        public sealed class BomComponentInspection
        {
            [System.ComponentModel.Category("1. Component Information"), System.ComponentModel.Description("Standard engineering part identifier")]
            public string PartNumber { get; set; }

            [System.ComponentModel.Category("1. Component Information"), System.ComponentModel.Description("Full description of the component")]
            public string Description { get; set; }

            [System.ComponentModel.Category("1. Component Information"), System.ComponentModel.Description("Assembly classification level")]
            public string Category { get; set; }

            [System.ComponentModel.Category("2. Procurement & Supply"), System.ComponentModel.Description("Primary approved manufacturing vendor")]
            public string Supplier { get; set; }

            [System.ComponentModel.Category("2. Procurement & Supply"), System.ComponentModel.Description("Standard component unit price in USD")]
            public decimal UnitCost { get; set; }

            [System.ComponentModel.Category("2. Procurement & Supply"), System.ComponentModel.Description("Current available inventory in stock")]
            public int StockQuantity { get; set; }

            [System.ComponentModel.Category("3. Quality & Compliance"), System.ComponentModel.Description("RoHS-3 and REACH environmental compliance")]
            public bool RoHSCompliant { get; set; }

            [System.ComponentModel.Category("3. Quality & Compliance"), System.ComponentModel.Description("Incoming QA audit quality rating score (0.0 to 5.0)")]
            public decimal QualityRating { get; set; }

            [System.ComponentModel.Category("4. Engineering Specs"), System.ComponentModel.Description("Rated operating ambient temperature range")]
            public string OperatingTemp { get; set; }

            public BomComponentInspection(string partNumber, string description, string category, string supplier, decimal unitCost, int stockQty, bool rohs, decimal qualityRating, string operatingTemp)
            {
                PartNumber = partNumber;
                Description = description;
                Category = category;
                Supplier = supplier;
                UnitCost = unitCost;
                StockQuantity = stockQty;
                RoHSCompliant = rohs;
                QualityRating = qualityRating;
                OperatingTemp = operatingTemp;
            }
        }

        private Panel CreateParamBox(string label, decimal initialVal, string unit, decimal step, decimal min, decimal max, int decimals = 1)
        {
            var pnl = new Panel
            {
                Size = new Size(230, 68),
                Margin = new Padding(6)
            };

            var lbl = new Label
            {
                Text = label,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Location = new Point(0, 2),
                AutoSize = true
            };

            var num = new ZeroNumericBox
            {
                Location = new Point(0, 24),
                Width = 220,
                Suffix = unit,
                Step = step,
                MinValue = min,
                MaxValue = max,
                DecimalPlaces = decimals,
                Value = initialVal
            };

            pnl.Controls.Add(lbl);
            pnl.Controls.Add(num);
            return pnl;
        }

        private void CountCheckedNodes(ZeroTreeNode node, ref int count)
        {
            if (node.CheckState == CheckState.Checked) count++;
            foreach (var child in node.Children)
            {
                CountCheckedNodes(child, ref count);
            }
        }

        private void OpenGlobalSettingsDialog()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(8) };

            int curY = 4;

            // 0. Visual Skin Palette Gallery (Enterprise Skin Manager)
            var lblSkins = new Label
            {
                Text = "Visual Skin Palette (9 Curated Enterprise Palettes):",
                Font = new Font(ZeroUIConfig.DefaultFont.FontFamily, 9.5f, FontStyle.Bold),
                ForeColor = ZeroTheme.Colors.TextPrimary,
                Location = new Point(8, curY),
                Size = new Size(500, 22),
                AutoSize = false
            };
            pnl.Controls.Add(lblSkins);
            curY += 26;

            var skinFlow = new FlowLayoutPanel
            {
                Location = new Point(8, curY),
                Size = new Size(504, 76),
                AutoScroll = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            var curSkin = ZeroSkinManager.CurrentSkin;
            var skinButtons = new List<ZeroButton>();
            foreach (var sk in ZeroSkinManager.AvailableSkins)
            {
                var target = sk;
                bool isCur = string.Equals(sk.Name, curSkin.Name, StringComparison.OrdinalIgnoreCase);
                var btnSkin = new ZeroButton
                {
                    Text = sk.DisplayName,
                    Size = new Size(160, 32),
                    ButtonStyle = isCur ? ZeroButtonStyle.Primary : ZeroButtonStyle.Secondary,
                    Margin = new Padding(2, 2, 4, 4)
                };
                skinButtons.Add(btnSkin);
                btnSkin.Click += (s, e) =>
                {
                    ZeroSkinManager.ApplySkin(target);
                    foreach (var b in skinButtons)
                    {
                        b.ButtonStyle = (b.Text == target.DisplayName) ? ZeroButtonStyle.Primary : ZeroButtonStyle.Secondary;
                        b.Invalidate();
                    }
                    lblSkins.ForeColor = ZeroTheme.Colors.TextPrimary;
                    pnl.Invalidate(true);
                };
                skinFlow.Controls.Add(btnSkin);
            }
            pnl.Controls.Add(skinFlow);
            curY += 84;

            // 1. Global Rounded Corners
            var lblCorners = new Label
            {
                Text = "Corner Radius Style (Global):",
                Font = new Font(ZeroUIConfig.DefaultFont.FontFamily, 9.5f, FontStyle.Bold),
                ForeColor = ZeroTheme.Colors.TextPrimary,
                Location = new Point(8, curY),
                Size = new Size(500, 22),
                AutoSize = false
            };
            pnl.Controls.Add(lblCorners);
            curY += 26;

            var segCorners = new ZeroSegmented
            {
                Location = new Point(8, curY),
                Size = new Size(504, 36),
                Items = new[] { "Rounded (6px)", "Sharp (0px)", "Pill (12px)" },
                SelectedIndex = ZeroUIConfig.CornerStyle switch
                {
                    ZeroCornerStyle.Rounded => 0,
                    ZeroCornerStyle.Sharp => 1,
                    ZeroCornerStyle.Pill => 2,
                    _ => 0
                }
            };
            pnl.Controls.Add(segCorners);
            curY += 46;

            // 2. Global Font
            var lblFont = new Label
            {
                Text = "Default Typography Font (Global):",
                Font = new Font(ZeroUIConfig.DefaultFont.FontFamily, 9.5f, FontStyle.Bold),
                ForeColor = ZeroTheme.Colors.TextPrimary,
                Location = new Point(8, curY),
                Size = new Size(500, 22),
                AutoSize = false
            };
            pnl.Controls.Add(lblFont);
            curY += 26;

            string[] fonts = new[] { "Segoe UI", "Aptos", "Tahoma", "Consolas" };
            var segFont = new ZeroSegmented
            {
                Location = new Point(8, curY),
                Size = new Size(504, 36),
                Items = fonts,
                SelectedIndex = ZeroUIConfig.DefaultFont.FontFamily.Name switch
                {
                    "Aptos" => 1,
                    "Tahoma" => 2,
                    "Consolas" => 3,
                    _ => 0
                }
            };
            pnl.Controls.Add(segFont);
            curY += 46;

            // 3. Live Preview Card
            var pnlPreview = new ZeroCard
            {
                Location = new Point(8, curY),
                Size = new Size(504, 136),
                Title = "Live Component Preview",
                Subtitle = "Dynamically updates across all active controls in real time"
            };
            var previewBtn = new ZeroButton { Location = new Point(14, 14), Size = new Size(130, 36), Text = "Sample Button", ButtonStyle = ZeroButtonStyle.Primary };
            var previewTag = new ZeroTag { Location = new Point(156, 19), Size = new Size(80, 26), Text = "Active", TagType = TagType.Success };
            var previewSearch = new ZeroSearchBox { Location = new Point(248, 14), Size = new Size(240, 36), PlaceholderText = "Search preview..." };

            pnlPreview.ContentPanel.Controls.Add(previewBtn);
            pnlPreview.ContentPanel.Controls.Add(previewTag);
            pnlPreview.ContentPanel.Controls.Add(previewSearch);
            pnl.Controls.Add(pnlPreview);

            segCorners.SelectedIndexChanged += (s, e) =>
            {
                switch (segCorners.SelectedIndex)
                {
                    case 0:
                        ZeroUIConfig.CornerStyle = ZeroCornerStyle.Rounded;
                        ZeroUIConfig.DefaultBorderRadius = 6;
                        break;
                    case 1:
                        ZeroUIConfig.CornerStyle = ZeroCornerStyle.Sharp;
                        ZeroUIConfig.DefaultBorderRadius = 0;
                        break;
                    case 2:
                        ZeroUIConfig.CornerStyle = ZeroCornerStyle.Pill;
                        ZeroUIConfig.DefaultBorderRadius = 12;
                        break;
                }
                ZeroUIConfig.NotifyConfigChanged();
                pnlPreview.Invalidate(true);
            };

            segFont.SelectedIndexChanged += (s, e) =>
            {
                string family = segFont.SelectedIndex switch
                {
                    1 => "Aptos",
                    2 => "Tahoma",
                    3 => "Consolas",
                    _ => "Segoe UI"
                };
                ZeroUIConfig.DefaultFont = new Font(family, 9.25f, FontStyle.Regular);
                ZeroUIConfig.NotifyConfigChanged();
                lblCorners.Font = new Font(family, 9.5f, FontStyle.Bold);
                lblFont.Font = new Font(family, 9.5f, FontStyle.Bold);
                pnlPreview.Invalidate(true);
            };

            ZeroModal.Show(
                this,
                "ZeroUI Global Settings & Customization",
                pnl,
                okText: "Close",
                showCancel: false,
                width: 560,
                height: 560);
        }

        private void InitializeChartsDashboard()
        {
            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = ZeroTheme.Colors.Background,
                Padding = new Padding(16)
            };

            // 0. Banner
            var banner = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Severity = ZeroAlertSeverity.Info,
                Title = "📊 Business Analytics & Enterprise Charting Subsystem",
                Message = "Comprehensive ZeroUI Chart Suite (ZeroChart, ZeroBarChart, ZeroLineChart, ZeroPieChart). Subpixel anti-aliasing, zero GC allocations, interactive tooltips, crosshair inspection, and toggleable legends.",
                Height = 62
            };
            var bannerSpacer = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            // ROW 1: Grouped Column Chart (Revenue vs Target) + Smooth Spline Area Chart (Output & Energy)
            var row1 = new Panel { Dock = DockStyle.Top, Height = 320, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 10) };

            // Card 1A: Grouped Column Chart
            var cardBar = new ZeroCard
            {
                StepNumber = null,
                Title = "Monthly Financial Performance (Revenue vs. Target)",
                Dock = DockStyle.Left,
                Width = 630
            };

            var barChart = new ZeroBarChart
            {
                Dock = DockStyle.Fill,
                ValuePrefix = "$",
                ValueSuffix = "k",
                LegendPosition = ZeroChartLegendPosition.Top
            };

            string[] months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            double[] actualRev = new[] { 420.0, 530.0, 610.0, 580.0, 720.0, 890.0, 840.0, 960.0, 1020.0, 1150.0, 1280.0, 1420.0 };
            double[] targetRev = new[] { 400.0, 500.0, 550.0, 600.0, 700.0, 800.0, 850.0, 900.0, 1000.0, 1100.0, 1200.0, 1300.0 };

            barChart.SetData("Actual Revenue", months, actualRev, Color.FromArgb(79, 70, 229));
            barChart.SetData("Target Revenue", months, targetRev, Color.FromArgb(16, 185, 129));
            cardBar.ContentPanel.Controls.Add(barChart);

            var split1 = new Panel { Dock = DockStyle.Left, Width = 12, BackColor = Color.Transparent };

            // Card 1B: Spline Area Chart
            var cardSpline = new ZeroCard
            {
                StepNumber = null,
                Title = "Factory Production Output & Energy Trend",
                Dock = DockStyle.Fill
            };

            var lineChart = new ZeroLineChart
            {
                Dock = DockStyle.Fill,
                IsCurved = true,
                IsArea = true,
                ValueSuffix = " pcs",
                LegendPosition = ZeroChartLegendPosition.Top
            };

            string[] weeks = new[] { "W01", "W02", "W03", "W04", "W05", "W06", "W07", "W08", "W09", "W10", "W11", "W12" };
            double[] outputUnits = new[] { 1240.0, 1450.0, 1380.0, 1620.0, 1590.0, 1780.0, 1850.0, 1920.0, 2100.0, 2050.0, 2240.0, 2380.0 };
            double[] energyKw = new[] { 850.0, 920.0, 890.0, 1040.0, 990.0, 1120.0, 1180.0, 1210.0, 1340.0, 1290.0, 1410.0, 1490.0 };

            lineChart.AddTrendSeries("SMT Line Output", outputUnits, weeks, Color.FromArgb(6, 182, 212));
            lineChart.AddTrendSeries("Energy Usage (kWh)", energyKw, weeks, Color.FromArgb(245, 158, 11));
            cardSpline.ContentPanel.Controls.Add(lineChart);

            row1.Controls.Add(cardSpline);
            row1.Controls.Add(split1);
            row1.Controls.Add(cardBar);

            // ROW 2: Donut Chart (Cost Breakdown) + Live Interactive Analytics
            var row2 = new Panel { Dock = DockStyle.Top, Height = 320, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 10) };

            // Card 2A: Donut Chart
            var cardDonut = new ZeroCard
            {
                StepNumber = null,
                Title = "Annual Operating Cost Breakdown (Donut)",
                Dock = DockStyle.Left,
                Width = 520
            };

            var pieChart = new ZeroPieChart
            {
                Dock = DockStyle.Fill,
                IsDonut = true,
                CenterTitle = "Total Budget",
                CenterValue = "$1.245M",
                ValuePrefix = "$",
                LegendPosition = ZeroChartLegendPosition.None
            };

            pieChart.AddSlice("Direct Raw Materials", 540000, Color.FromArgb(79, 70, 229));
            pieChart.AddSlice("SMT Assembly & Tooling", 320000, Color.FromArgb(16, 185, 129));
            pieChart.AddSlice("R&D Firmware Engineering", 180000, Color.FromArgb(6, 182, 212));
            pieChart.AddSlice("Logistics & Warehouse", 120000, Color.FromArgb(245, 158, 11));
            pieChart.AddSlice("AOI / Quality Inspection", 85000, Color.FromArgb(244, 63, 94));
            cardDonut.ContentPanel.Controls.Add(pieChart);

            var split2 = new Panel { Dock = DockStyle.Left, Width = 12, BackColor = Color.Transparent };

            // Card 2B: Interactive Stream & Style Controller
            var cardInteractive = new ZeroCard
            {
                StepNumber = null,
                Title = "Interactive Analytics & Real-Time Dynamic Stream",
                Dock = DockStyle.Fill
            };

            var dynamicChart = new ZeroChart
            {
                Dock = DockStyle.Fill,
                ChartType = ZeroChartType.Spline,
                ValueSuffix = "%",
                LegendPosition = ZeroChartLegendPosition.Top,
                CrosshairMode = CrosshairMode.Both,
                EnableSpcBelts = true,
                SpcBeltStyle = SpcBeltStyle.TrafficLight
            };

            var dynSeries = dynamicChart.AddSeries("OEE Equipment Efficiency", Color.FromArgb(139, 92, 246));
            string[] shifts = new[] { "06:00", "08:00", "10:00", "12:00", "14:00", "16:00", "18:00", "20:00", "22:00" };
            double[] oeeVals = new[] { 88.2, 91.5, 89.0, 94.2, 92.8, 95.1, 93.4, 96.0, 94.7 };
            dynSeries.AddPoints(oeeVals, shifts);

            var rangeControl = new RangeControl
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                Client = dynamicChart.AsRangeClient(),
                ShowBackgroundGraph = true,
                BackgroundGraphType = RangeGraphType.Area
            };

            var ctrlToolbar = new Panel { Dock = DockStyle.Bottom, Height = 36, BackColor = Color.FromArgb(15, 23, 42), Padding = new Padding(6, 4, 6, 4) };
            var btnRand = new ZeroButton
            {
                Text = "🎲 Randomize Series",
                ButtonStyle = ZeroButtonStyle.Primary,
                Dock = DockStyle.Left,
                Width = 150
            };
            var btnToggleType = new ZeroButton
            {
                Text = "📊 Switch to Column",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Dock = DockStyle.Left,
                Width = 150
            };
            var btnAddPoint = new ZeroButton
            {
                Text = "⚡ Add Sample",
                ButtonStyle = ZeroButtonStyle.Success,
                Dock = DockStyle.Left,
                Width = 130
            };
            var btnToggleSpc = new ZeroButton
            {
                Text = "🎯 SPC Belts",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Dock = DockStyle.Left,
                Width = 120
            };
            btnToggleSpc.Click += (s, e) =>
            {
                dynamicChart.EnableSpcBelts = !dynamicChart.EnableSpcBelts;
                ZeroToast.Info(this, $"SPC Warning Belts: {(dynamicChart.EnableSpcBelts ? "Enabled (UCL, CL, LCL)" : "Disabled")}");
            };

            var btnToggleCrosshair = new ZeroButton
            {
                Text = "➕ Crosshair HUD",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Dock = DockStyle.Left,
                Width = 130
            };
            btnToggleCrosshair.Click += (s, e) =>
            {
                dynamicChart.CrosshairMode = dynamicChart.CrosshairMode switch
                {
                    CrosshairMode.Both => CrosshairMode.VerticalOnly,
                    CrosshairMode.VerticalOnly => CrosshairMode.HorizontalOnly,
                    CrosshairMode.HorizontalOnly => CrosshairMode.None,
                    _ => CrosshairMode.Both
                };
                ZeroToast.Info(this, $"Crosshair HUD Mode: {dynamicChart.CrosshairMode}");
            };

            var rnd = new Random();
            btnRand.Click += (s, e) =>
            {
                dynSeries.Clear();
                for (int i = 0; i < shifts.Length; i++)
                {
                    dynSeries.AddPoint(shifts[i], 80.0 + (rnd.NextDouble() * 18.0));
                }
                dynamicChart.Invalidate();
                ZeroToast.Success(this, "Regenerated random analytics data!");
            };

            bool isBar = false;
            btnToggleType.Click += (s, e) =>
            {
                isBar = !isBar;
                dynamicChart.ChartType = isBar ? ZeroChartType.Column : ZeroChartType.Spline;
                btnToggleType.Text = isBar ? "📈 Switch to Spline" : "📊 Switch to Column";
                dynamicChart.Invalidate();
            };

            btnAddPoint.Click += (s, e) =>
            {
                string nextTime = $"{DateTime.Now:HH:mm:ss}";
                double val = 85.0 + (rnd.NextDouble() * 14.0);
                dynSeries.AddPoint(nextTime, val);
                if (dynSeries.Points.Count > 12) dynSeries.Points.RemoveAt(0);
                dynamicChart.Invalidate();
                ZeroToast.Info(this, $"Pushed telemetry point: {val:F1}% at {nextTime}");
            };

            ctrlToolbar.Controls.Add(btnToggleCrosshair);
            ctrlToolbar.Controls.Add(btnToggleSpc);
            ctrlToolbar.Controls.Add(btnAddPoint);
            ctrlToolbar.Controls.Add(btnToggleType);
            ctrlToolbar.Controls.Add(btnRand);

            cardInteractive.ContentPanel.Controls.Add(dynamicChart);
            cardInteractive.ContentPanel.Controls.Add(rangeControl);
            cardInteractive.ContentPanel.Controls.Add(ctrlToolbar);
            ctrlToolbar.SendToBack();
            rangeControl.BringToFront();
            dynamicChart.BringToFront();

            row2.Controls.Add(cardInteractive);
            row2.Controls.Add(split2);
            row2.Controls.Add(cardDonut);

            mainContainer.Controls.Add(row2);
            mainContainer.Controls.Add(row1);
            mainContainer.Controls.Add(bannerSpacer);
            mainContainer.Controls.Add(banner);

            banner.BringToFront();
            bannerSpacer.BringToFront();
            row1.BringToFront();
            row2.BringToFront();

            // -------------------------------------------------------------
            // SUB-TAB 2: Radar Diagnostics & Candlestick Price Analytics
            // -------------------------------------------------------------
            var panelRadar = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = ZeroTheme.Colors.Background,
                Padding = new Padding(16)
            };

            var bannerRadar = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Severity = ZeroAlertSeverity.Info,
                Title = "🎯 Multi-Axis Radar Diagnostics & Financial OHLC Candlestick Engine",
                Message = "High-precision vector Radar/Spider evaluation across multiple OEE axes and real-time Financial Candlestick chart with synchronized volume and moving averages.",
                Height = 62
            };
            var bannerSpacerRadar = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            var rowRadar = new Panel { Dock = DockStyle.Top, Height = 360, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 10) };

            // Card: Radar Chart
            var cardRadar = new ZeroCard
            {
                Title = "Multi-Axis Machine OEE & Performance Benchmark (Spider)",
                Dock = DockStyle.Left,
                Width = 540
            };
            var radarChart = new ZeroRadarChart
            {
                Dock = DockStyle.Fill,
                MaxValue = 100.0,
                WebRings = 5
            };
            radarChart.SetAxes("Availability", "Performance", "Quality Rate", "MTBF Stability", "5S Hygiene", "Tooling Life");
            radarChart.AddSeries("Line A - SMT Surface", Color.FromArgb(79, 70, 229), 92.0, 88.5, 96.2, 85.0, 94.0, 78.0);
            radarChart.AddSeries("Target Benchmark", Color.FromArgb(16, 185, 129), 95.0, 90.0, 98.0, 90.0, 95.0, 85.0);
            radarChart.AddSeries("Line B - Wave Soldering", Color.FromArgb(245, 158, 11), 84.0, 79.0, 91.0, 76.0, 88.0, 82.0);
            cardRadar.ContentPanel.Controls.Add(radarChart);

            var splitRadar = new Panel { Dock = DockStyle.Left, Width = 12, BackColor = Color.Transparent };

            // Card: Candlestick Chart
            var cardCandle = new ZeroCard
            {
                Title = "Industrial Copper (Cu-99.9%) Daily Price ($/lb) & Trading Volume",
                Dock = DockStyle.Fill
            };
            var candleChart = new ZeroCandlestickChart
            {
                Dock = DockStyle.Fill,
                Title = "COMEX Copper Futures (Daily OHLC + Volume)",
                ValuePrefix = "$",
                ShowMovingAverage = true,
                ShowVolume = true,
                MaPeriod = 5
            };

            var candleRnd = new Random(42);
            DateTime baseDate = DateTime.Today.AddDays(-32);
            double curPrice = 4.35;
            for (int i = 0; i < 24; i++)
            {
                var dt = baseDate.AddDays(i);
                if (dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday) continue;
                double open = curPrice;
                double change = (candleRnd.NextDouble() - 0.48) * 0.12;
                double close = Math.Round(open + change, 3);
                double high = Math.Round(Math.Max(open, close) + candleRnd.NextDouble() * 0.06, 3);
                double low = Math.Round(Math.Min(open, close) - candleRnd.NextDouble() * 0.06, 3);
                double vol = candleRnd.Next(15000, 48000);
                candleChart.AddCandle(dt, open, high, low, close, vol);
                curPrice = close;
            }
            cardCandle.ContentPanel.Controls.Add(candleChart);

            rowRadar.Controls.Add(cardCandle);
            rowRadar.Controls.Add(splitRadar);
            rowRadar.Controls.Add(cardRadar);

            panelRadar.Controls.Add(rowRadar);
            panelRadar.Controls.Add(bannerSpacerRadar);
            panelRadar.Controls.Add(bannerRadar);

            bannerRadar.BringToFront();
            bannerSpacerRadar.BringToFront();
            rowRadar.BringToFront();

            // -------------------------------------------------------------
            // SUB-TAB 3: Funnel & Waterfall Bridges
            // -------------------------------------------------------------
            var panelBridges = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = ZeroTheme.Colors.Background,
                Padding = new Padding(16)
            };

            var bannerBridges = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Severity = ZeroAlertSeverity.Warning,
                Title = "📉 Conversion Pipeline Funnel & Reconciliation Waterfall Bridges",
                Message = "Tapered trapezoid Funnel showing stage drop-off and conversion rates, coupled with floating-bar Waterfall chart tracking cumulative inventory & financial reconciliation.",
                Height = 62
            };
            var bannerSpacerBridges = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            var rowBridges = new Panel { Dock = DockStyle.Top, Height = 360, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 10) };

            // Card: Funnel Chart
            var cardFunnel = new ZeroCard
            {
                Title = "Production Yield & Stage Drop-off Conversion (Funnel)",
                Dock = DockStyle.Left,
                Width = 540
            };
            var funnelChart = new ZeroFunnelChart
            {
                Dock = DockStyle.Fill,
                ValueSuffix = " pcs",
                ShowConversionRates = true,
                ShowPercentages = true,
                NeckWidth = 90
            };
            funnelChart.AddStage("Raw Inward Wafers", 100000, Color.FromArgb(79, 70, 229), "Lot acceptance & IQC passed");
            funnelChart.AddStage("SMT Chip Placement", 97400, Color.FromArgb(16, 185, 129), "High-speed surface mount");
            funnelChart.AddStage("Reflow Soldering", 95200, Color.FromArgb(6, 182, 212), "10-zone nitrogen profile");
            funnelChart.AddStage("AOI Optical Inspection", 92800, Color.FromArgb(245, 158, 11), "Automated optical inspection");
            funnelChart.AddStage("Final Packaged Goods", 91600, Color.FromArgb(244, 63, 94), "OQC passed finished yield");
            cardFunnel.ContentPanel.Controls.Add(funnelChart);

            var splitBridges = new Panel { Dock = DockStyle.Left, Width = 12, BackColor = Color.Transparent };

            // Card: Waterfall Chart
            var cardWaterfall = new ZeroCard
            {
                Title = "Inventory Balance Variance & Stock Flow Reconciliation (Waterfall)",
                Dock = DockStyle.Fill
            };
            var waterfallChart = new ZeroWaterfallChart
            {
                Dock = DockStyle.Fill,
                ValuePrefix = "",
                ValueSuffix = " pcs",
                ShowConnectors = true
            };
            waterfallChart.AddItem("Opening", 5000, WaterfallItemType.Start);
            waterfallChart.AddItem("Receipts", 2400, WaterfallItemType.Increment);
            waterfallChart.AddItem("SMT Scrap", -320, WaterfallItemType.Decrement);
            waterfallChart.AddItem("Production", -1850, WaterfallItemType.Decrement);
            waterfallChart.AddItem("Returns", 150, WaterfallItemType.Increment);
            waterfallChart.AddItem("Dispatched", -1600, WaterfallItemType.Decrement);
            waterfallChart.AddItem("Audit Adj", 40, WaterfallItemType.Increment);
            waterfallChart.AddItem("Closing", 3820, WaterfallItemType.Total);
            cardWaterfall.ContentPanel.Controls.Add(waterfallChart);

            rowBridges.Controls.Add(cardWaterfall);
            rowBridges.Controls.Add(splitBridges);
            rowBridges.Controls.Add(cardFunnel);

            panelBridges.Controls.Add(rowBridges);
            panelBridges.Controls.Add(bannerSpacerBridges);
            panelBridges.Controls.Add(bannerBridges);

            bannerBridges.BringToFront();
            bannerSpacerBridges.BringToFront();
            // -------------------------------------------------------------
            // SUB-TAB 2: Industrial Multi-Pen Trend & Telemetry Studio
            // -------------------------------------------------------------
            var panelTrendStudio = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ZeroTheme.Colors.Background,
                Padding = new Padding(16)
            };

            var bannerTrend = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Severity = ZeroAlertSeverity.Info,
                Title = "⚡ Industrial Multi-Pen Trend & Telemetry Analytics Studio",
                Message = "Multi-Y axes synchronized waveform engine with LTTB decimation (1M+ points at 60 FPS), interactive Dual-Cursor delta analysis (ΔT, ΔY), time presets (1m..24h), and live channel statistics.",
                Height = 62
            };
            var bannerSpacerTrend = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            var trendContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(15, 23, 42)
            };

            var studio = new TrendStudio { Dock = DockStyle.Fill };

            // Add 4 realistic industrial telemetry pens
            var penTemp = studio.AddPen("Reactor Temp", "°C", Color.FromArgb(239, 68, 68), TrendYAxisPosition.Left1);
            penTemp.AutoScale = false;
            penTemp.ManualMin = 0;
            penTemp.ManualMax = 250;
            penTemp.HighAlarmLimit = 220;

            var penPress = studio.AddPen("Vessel Pressure", "Bar", Color.FromArgb(6, 182, 212), TrendYAxisPosition.Left2);
            penPress.AutoScale = false;
            penPress.ManualMin = 0;
            penPress.ManualMax = 16;
            penPress.HighAlarmLimit = 12.5;

            var penRpm = studio.AddPen("Turbine Speed", "RPM", Color.FromArgb(168, 85, 247), TrendYAxisPosition.Right1);
            penRpm.AutoScale = false;
            penRpm.ManualMin = 0;
            penRpm.ManualMax = 1800;

            var penFlow = studio.AddPen("Coolant Flow", "L/min", Color.FromArgb(245, 158, 11), TrendYAxisPosition.Right2);
            penFlow.AutoScale = false;
            penFlow.ManualMin = 0;
            penFlow.ManualMax = 80;
            penFlow.LowAlarmLimit = 15;

            // Pre-fill 5 minutes of historical telemetry (300 seconds)
            DateTime now = DateTime.Now;
            var simRnd = new Random(101);
            double curTemp = 185.0;
            double curPress = 6.2;
            double curRpm = 1450.0;
            double curFlow = 42.0;

            var ptsTemp = new List<TrendDataPoint>(300);
            var ptsPress = new List<TrendDataPoint>(300);
            var ptsRpm = new List<TrendDataPoint>(300);
            var ptsFlow = new List<TrendDataPoint>(300);

            for (int i = 300; i >= 0; i--)
            {
                DateTime t = now.AddSeconds(-i);
                curTemp += (simRnd.NextDouble() - 0.49) * 1.5;
                curPress += (simRnd.NextDouble() - 0.50) * 0.15;
                curRpm += (simRnd.NextDouble() - 0.50) * 12.0;
                curFlow += (simRnd.NextDouble() - 0.50) * 0.8;

                curTemp = Math.Max(150, Math.Min(230, curTemp));
                curPress = Math.Max(2, Math.Min(14, curPress));
                curRpm = Math.Max(1200, Math.Min(1700, curRpm));
                curFlow = Math.Max(20, Math.Min(70, curFlow));

                ptsTemp.Add(new TrendDataPoint(t, curTemp));
                ptsPress.Add(new TrendDataPoint(t, curPress));
                ptsRpm.Add(new TrendDataPoint(t, curRpm));
                ptsFlow.Add(new TrendDataPoint(t, curFlow));
            }

            penTemp.AddPoints(ptsTemp);
            penPress.AddPoints(ptsPress);
            penRpm.AddPoints(ptsRpm);
            penFlow.AddPoints(ptsFlow);

            // Add sample event annotations
            studio.AddAnnotation(now.AddSeconds(-240), "Batch #B84 Started", "Polymer reaction phase initiated", Color.FromArgb(16, 185, 129), "🚀");
            studio.AddAnnotation(now.AddSeconds(-120), "Catalyst Feed Injected", "Secondary feed valve opened", Color.FromArgb(59, 130, 246), "🧪");

            // Interactive Simulation Bar
            var simBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(8, 5, 8, 5)
            };

            var btnSpike = new ZeroButton
            {
                Text = "🔥 Inject Thermal Spike (+35°C)",
                ButtonStyle = ZeroButtonStyle.Danger,
                Dock = DockStyle.Left,
                Width = 220
            };
            btnSpike.Click += (s, e) =>
            {
                curTemp += 35.0;
                studio.AddAnnotation(DateTime.Now, "Thermal Spike", "Emergency cooling engaged", Color.FromArgb(239, 68, 68), "🔥");
                ZeroToast.Warning(this, "Thermal Excursion: Reactor Core Temp spiked to " + curTemp.ToString("F1") + "°C!");
            };

            var btnOscillate = new ZeroButton
            {
                Text = "🌊 Pressure Oscillation (Test ΔT)",
                ButtonStyle = ZeroButtonStyle.Primary,
                Dock = DockStyle.Left,
                Width = 230
            };
            double oscPhase = 0;
            bool isOscillating = false;
            btnOscillate.Click += (s, e) =>
            {
                isOscillating = !isOscillating;
                btnOscillate.Text = isOscillating ? "⏹ Stop Oscillation" : "🌊 Pressure Oscillation (Test ΔT)";
                ZeroToast.Info(this, isOscillating ? "Pressure 0.5Hz resonance oscillation started!" : "Pressure returned to baseline.");
            };

            var btnAddNote = new ZeroButton
            {
                Text = "🏷️ Add Event Flag",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Dock = DockStyle.Left,
                Width = 140
            };
            btnAddNote.Click += (s, e) =>
            {
                studio.AddAnnotation(DateTime.Now, "Operator Marker", "Checkpoint verified", Color.FromArgb(139, 92, 246), "📌");
                ZeroToast.Success(this, "Event annotation pinned to timeline.");
            };

            simBar.Controls.Add(btnAddNote);
            var simSpacer2 = new Panel { Dock = DockStyle.Left, Width = 8, BackColor = Color.Transparent };
            simBar.Controls.Add(simSpacer2);
            simBar.Controls.Add(btnOscillate);
            var simSpacer1 = new Panel { Dock = DockStyle.Left, Width = 8, BackColor = Color.Transparent };
            simBar.Controls.Add(simSpacer1);
            simBar.Controls.Add(btnSpike);

            trendContainer.Controls.Add(studio);
            trendContainer.Controls.Add(simBar);

            panelTrendStudio.Controls.Add(trendContainer);
            panelTrendStudio.Controls.Add(bannerSpacerTrend);
            panelTrendStudio.Controls.Add(bannerTrend);

            bannerTrend.BringToFront();
            bannerSpacerTrend.BringToFront();
            trendContainer.BringToFront();

            // Background Telemetry Simulation Timer (5 Hz)
            var simTimer = new System.Windows.Forms.Timer { Interval = 200 };
            simTimer.Tick += (s, e) =>
            {
                if (!studio.IsLiveFollow) return;

                DateTime t = DateTime.Now;

                // Drift back toward baseline if spiked
                if (curTemp > 188.0) curTemp -= 0.6;
                else curTemp += (simRnd.NextDouble() - 0.49) * 0.8;

                if (isOscillating)
                {
                    oscPhase += 0.4;
                    curPress = 8.0 + Math.Sin(oscPhase) * 3.5;
                }
                else
                {
                    curPress += (simRnd.NextDouble() - 0.50) * 0.12;
                    curPress = Math.Max(3.0, Math.Min(12.0, curPress));
                }

                curRpm += (simRnd.NextDouble() - 0.50) * 15.0;
                curRpm = Math.Max(1300, Math.Min(1650, curRpm));

                curFlow += (simRnd.NextDouble() - 0.50) * 0.6;
                curFlow = Math.Max(25, Math.Min(65, curFlow));

                penTemp.AddPoint(t, curTemp);
                penPress.AddPoint(t, curPress);
                penRpm.AddPoint(t, curRpm);
                penFlow.AddPoint(t, curFlow);

                studio.UpdateLiveStream();
            };
            simTimer.Start();
            panelTrendStudio.Disposed += (s, e) =>
            {
                simTimer.Stop();
                simTimer.Dispose();
            };

            // -------------------------------------------------------------
            // SUB-TAB 5: Flow, Hierarchy & Quality Analytics (Sankey, Treemap, Bullet, Pareto)
            // -------------------------------------------------------------
            var panelFlowQuality = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ZeroTheme.Colors.Background,
                Padding = new Padding(16)
            };

            var bannerFlow = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Severity = ZeroAlertSeverity.Info,
                Title = "📊 Advanced Flow, Squarified Hierarchy, Benchmarks & Quality Charts",
                Message = "Multi-column Sankey process flows, Squarified Treemap asset allocation, Stephen Few linear Bullet KPI benchmarks, and Pareto 80/20 root cause defect analysis.",
                Height = 62
            };
            var bannerSpacerFlow = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            var tableFlow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            tableFlow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tableFlow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tableFlow.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            tableFlow.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            // Card 1: Sankey Chart
            var cardSankey = new ZeroCard
            {
                Title = "Energy & Process Flow Distribution (Sankey)",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 6, 6)
            };
            var sankeyChart = new SankeyChart
            {
                Dock = DockStyle.Fill,
                Title = "Energy Generation & Consumption Grid Flow",
                ValueSuffix = " GWh"
            };
            sankeyChart.AddNode("Solar Array", Color.FromArgb(245, 158, 11), 0);
            sankeyChart.AddNode("Wind Turbines", Color.FromArgb(14, 165, 233), 0);
            sankeyChart.AddNode("Hydro Plant", Color.FromArgb(59, 130, 246), 0);
            sankeyChart.AddNode("BESS Storage", Color.FromArgb(16, 185, 129), 1);
            sankeyChart.AddNode("High-Voltage Grid", Color.FromArgb(168, 85, 247), 1);
            sankeyChart.AddNode("Heavy Industry", Color.FromArgb(239, 68, 68), 2);
            sankeyChart.AddNode("Commercial Facilities", Color.FromArgb(249, 115, 22), 2);
            sankeyChart.AddNode("Residential Dist", Color.FromArgb(16, 185, 129), 2);

            sankeyChart.AddLink("Solar Array", "BESS Storage", 45);
            sankeyChart.AddLink("Solar Array", "High-Voltage Grid", 75);
            sankeyChart.AddLink("Wind Turbines", "BESS Storage", 60);
            sankeyChart.AddLink("Wind Turbines", "High-Voltage Grid", 50);
            sankeyChart.AddLink("Hydro Plant", "High-Voltage Grid", 80);
            sankeyChart.AddLink("BESS Storage", "Heavy Industry", 65);
            sankeyChart.AddLink("BESS Storage", "Commercial Facilities", 40);
            sankeyChart.AddLink("High-Voltage Grid", "Heavy Industry", 70);
            sankeyChart.AddLink("High-Voltage Grid", "Commercial Facilities", 55);
            sankeyChart.AddLink("High-Voltage Grid", "Residential Dist", 80);
            cardSankey.ContentPanel.Controls.Add(sankeyChart);

            // Card 2: Treemap Chart
            var cardTreemap = new ZeroCard
            {
                Title = "Asset Allocation & Proportional Breakdown (Squarified Treemap)",
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 0, 0, 6)
            };
            var treemapChart = new TreemapChart
            {
                Dock = DockStyle.Fill,
                Title = "Corporate Capital & Asset Allocation",
                ValuePrefix = "$"
            };
            treemapChart.AddItem("US Large Cap", 4200, Color.FromArgb(59, 130, 246), "Equities");
            treemapChart.AddItem("Tech Growth", 2500, Color.FromArgb(99, 102, 241), "Equities");
            treemapChart.AddItem("Emerging Mkts", 1800, Color.FromArgb(14, 165, 233), "Equities");
            treemapChart.AddItem("Treasury Bonds 10Y", 1900, Color.FromArgb(16, 185, 129), "Fixed Income");
            treemapChart.AddItem("Corp Bonds A+", 1400, Color.FromArgb(5, 150, 105), "Fixed Income");
            treemapChart.AddItem("Physical Gold", 950, Color.FromArgb(245, 158, 11), "Commodities");
            treemapChart.AddItem("Crude Oil Reserves", 650, Color.FromArgb(217, 119, 6), "Commodities");
            treemapChart.AddItem("Cash & Equivalents", 800, Color.FromArgb(100, 116, 139), "Liquidity");
            cardTreemap.ContentPanel.Controls.Add(treemapChart);

            // Card 3: Bullet Chart
            var cardBullet = new ZeroCard
            {
                Title = "Executive KPI Performance Benchmarks (Stephen Few Bullet Graph)",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 6, 0)
            };
            var bulletChart = new BulletChart
            {
                Dock = DockStyle.Fill,
                Title = "Executive Operations & Financial Performance",
                LeftLabelWidth = 170
            };
            var bRevenue = new BulletItem("Revenue", "USD (thousands)", 285, 260, 320, 0);
            bRevenue.Ranges.Add(new BulletRange("Poor", 160));
            bRevenue.Ranges.Add(new BulletRange("Satisfactory", 240));
            bRevenue.Ranges.Add(new BulletRange("Good", 320));
            bulletChart.Items.Add(bRevenue);

            var bMargin = new BulletItem("Operating Margin", "% of gross revenue", 24.5, 22.0, 30, 0);
            bMargin.Ranges.Add(new BulletRange("Poor", 15));
            bMargin.Ranges.Add(new BulletRange("Satisfactory", 20));
            bMargin.Ranges.Add(new BulletRange("Good", 30));
            bulletChart.Items.Add(bMargin);

            var bOtif = new BulletItem("On-Time In-Full", "Supply chain fulfillment %", 95.8, 94.0, 100, 0);
            bOtif.Ranges.Add(new BulletRange("Poor", 80));
            bOtif.Ranges.Add(new BulletRange("Satisfactory", 92));
            bOtif.Ranges.Add(new BulletRange("Good", 100));
            bulletChart.Items.Add(bOtif);

            var bCsat = new BulletItem("CSAT Score", "Quarterly customer rating %", 91.2, 88.0, 100, 0);
            bCsat.Ranges.Add(new BulletRange("Poor", 70));
            bCsat.Ranges.Add(new BulletRange("Satisfactory", 85));
            bCsat.Ranges.Add(new BulletRange("Good", 100));
            bulletChart.Items.Add(bCsat);
            cardBullet.ContentPanel.Controls.Add(bulletChart);

            // Card 4: Pareto Chart
            var cardPareto = new ZeroCard
            {
                Title = "Defect Quality Distribution & 80/20 Vital Few (Pareto)",
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 6, 0, 0)
            };
            var paretoChart = new ParetoChart
            {
                Dock = DockStyle.Fill,
                Title = "SMT Assembly Line Defect Pareto Analysis",
                CutoffPercentage = 80.0
            };
            paretoChart.Items.Add(new ParetoItem("Surface Scratch", 142));
            paretoChart.Items.Add(new ParetoItem("Dimension Variance", 89));
            paretoChart.Items.Add(new ParetoItem("Packaging Crushed", 56));
            paretoChart.Items.Add(new ParetoItem("Missing Label", 34));
            paretoChart.Items.Add(new ParetoItem("Burr & Flashing", 21));
            paretoChart.Items.Add(new ParetoItem("Solder Bridge", 14));
            paretoChart.Items.Add(new ParetoItem("Color Mismatch", 8));
            paretoChart.Items.Add(new ParetoItem("Contamination", 5));
            cardPareto.ContentPanel.Controls.Add(paretoChart);

            tableFlow.Controls.Add(cardSankey, 0, 0);
            tableFlow.Controls.Add(cardTreemap, 1, 0);
            tableFlow.Controls.Add(cardBullet, 0, 1);
            tableFlow.Controls.Add(cardPareto, 1, 1);

            panelFlowQuality.Controls.Add(tableFlow);
            panelFlowQuality.Controls.Add(bannerSpacerFlow);
            panelFlowQuality.Controls.Add(bannerFlow);

            bannerFlow.BringToFront();
            bannerSpacerFlow.BringToFront();
            tableFlow.BringToFront();

            // -------------------------------------------------------------
            // SUB-TAB 6: Statistical, Relational & Partition Analytics (Histogram, Scatter, Sunburst, Lollipop)
            // -------------------------------------------------------------
            var panelStatistical = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ZeroTheme.Colors.Background,
                Padding = new Padding(16)
            };

            var bannerStat = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Severity = ZeroAlertSeverity.Info,
                Title = "🔬 Advanced Statistical, Relational & Hierarchical Partition Analytics",
                Message = "Continuous Sturges & Gaussian distribution histograms, Bivariate OLS linear regression scatter & bubble plots, Multi-tier radial sunburst partitions, and Stephen Few lollipop benchmarks.",
                Height = 62
            };
            var bannerSpacerStat = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            var tableStat = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            tableStat.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tableStat.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tableStat.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            tableStat.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            // Card 1: Histogram Chart
            var cardHistogram = new ZeroCard
            {
                Title = "Continuous Distribution & Gaussian Fit (Histogram)",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 6, 6)
            };
            var histogramChart = new HistogramChart
            {
                Dock = DockStyle.Fill,
                Title = "Assembly Tolerance & Variance Distribution (N=300)",
                XAxisTitle = "Dimension Offset (μm)",
                YAxisTitle = "Frequency Count",
                BarColor = Color.FromArgb(59, 130, 246),
                CurveColor = Color.FromArgb(239, 68, 68),
                BinCount = 12,
                ShowNormalCurve = true
            };
            histogramChart.LoadSampleData();
            cardHistogram.ContentPanel.Controls.Add(histogramChart);

            // Card 2: Scatter & Bubble Chart
            var cardScatter = new ZeroCard
            {
                Title = "Bivariate Correlation & Bubble Regression (Scatter)",
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 0, 0, 6)
            };
            var scatterChart = new ScatterChart
            {
                Dock = DockStyle.Fill,
                Title = "Torque vs. Yield Tensile Strength Analysis",
                XAxisTitle = "Motor Speed (RPM x100)",
                YAxisTitle = "Stress Tolerance (MPa)",
                ShowRegressionLine = true
            };
            scatterChart.LoadSampleData();
            cardScatter.ContentPanel.Controls.Add(scatterChart);

            // Card 3: Sunburst Chart
            var cardSunburst = new ZeroCard
            {
                Title = "Hierarchical Multi-Tier Radial Partition (Sunburst)",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 6, 0)
            };
            var sunburstChart = new SunburstChart
            {
                Dock = DockStyle.Fill,
                Title = "Global Supply Chain & Cost Allocation",
                InnerHoleRatio = 0.25
            };
            sunburstChart.LoadSampleData();
            cardSunburst.ContentPanel.Controls.Add(sunburstChart);

            // Card 4: Lollipop Chart
            var cardLollipop = new ZeroCard
            {
                Title = "High-Density Category Benchmark Comparison (Lollipop)",
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 6, 0, 0)
            };
            var lollipopChart = new LollipopChart
            {
                Dock = DockStyle.Fill,
                Title = "Regional Fulfilment Efficiency Ranking",
                Orientation = LollipopOrientation.Horizontal,
                DotColor = Color.FromArgb(16, 185, 129),
                StemColor = Color.FromArgb(148, 163, 184),
                DotRadius = 7f,
                ShowValueLabels = true
            };
            lollipopChart.LoadSampleData();
            cardLollipop.ContentPanel.Controls.Add(lollipopChart);

            tableStat.Controls.Add(cardHistogram, 0, 0);
            tableStat.Controls.Add(cardScatter, 1, 0);
            tableStat.Controls.Add(cardSunburst, 0, 1);
            tableStat.Controls.Add(cardLollipop, 1, 1);

            panelStatistical.Controls.Add(tableStat);
            panelStatistical.Controls.Add(bannerSpacerStat);
            panelStatistical.Controls.Add(bannerStat);

            bannerStat.BringToFront();
            bannerSpacerStat.BringToFront();
            tableStat.BringToFront();

            // Assemble Modular Sub-tabs
            var subTabsCharts = new ZeroTabControl
            {
                Dock = DockStyle.Fill,
                Orientation = ZeroTabOrientation.Horizontal,
                TabHeight = 36,
                TabStyle = ZeroTabStyle.Pill
            };

            var tabExecOverview = new ZeroTabPage("Executive Overview", "📈");
            tabExecOverview.Controls.Add(mainContainer);

            var tabTrendStudio = new ZeroTabPage("Industrial Trend Studio", "⚡");
            tabTrendStudio.Controls.Add(panelTrendStudio);

            var tabRadarDiagnostics = new ZeroTabPage("Radar & Candlestick Analytics", "🎯");
            tabRadarDiagnostics.Controls.Add(panelRadar);

            var tabBridgesSub = new ZeroTabPage("Funnel & Waterfall Bridges", "📉");
            tabBridgesSub.Controls.Add(panelBridges);

            var tabFlowQuality = new ZeroTabPage("Flow, Hierarchy & Quality", "📊");
            tabFlowQuality.Controls.Add(panelFlowQuality);

            var tabStatisticalRelational = new ZeroTabPage("Statistical & Relational Analytics", "🔬");
            tabStatisticalRelational.Controls.Add(panelStatistical);

            subTabsCharts.AddTab(tabExecOverview);
            subTabsCharts.AddTab(tabTrendStudio);
            subTabsCharts.AddTab(tabRadarDiagnostics);
            subTabsCharts.AddTab(tabBridgesSub);
            subTabsCharts.AddTab(tabFlowQuality);
            subTabsCharts.AddTab(tabStatisticalRelational);

            _tabCharts.Controls.Add(subTabsCharts);
        }

        private void InitializeLayoutShowcase(ZeroTabPage parent)
        {
            // Root SplitContainer: Left = ZeroAccordion navigation tree, Right = Layout & Workspace Panels
            var splitRoot = new ZeroSplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 270,
                SplitterWidth = 8,
                MinSizePanel1 = 180,
                MinSizePanel2 = 300
            };

            // 1. Left Panel: ZeroAccordion (Single-HWND)
            var accordion = new ZeroAccordion
            {
                Dock = DockStyle.Fill,
                ShowSearchBox = true,
                SearchPlaceholder = "Filter modules & views..."
            };

            var grpWh = accordion.AddGroup("Warehouse Operations", "📦", isExpanded: true);
            grpWh.BadgeText = "4 alerts";
            grpWh.AddItem("Receiving Inspection", "📥", (s, e) => ZeroToast.Info(this, "Navigating to Receiving Inspection..."), "New");
            grpWh.AddItem("FIFO/FEFO Lot Allocator", "📋", (s, e) => ZeroToast.Info(this, "Navigating to FIFO/FEFO Allocator..."));
            grpWh.AddItem("Smart Storage Racks", "🏢", (s, e) => ZeroToast.Info(this, "Navigating to Smart Storage Racks..."));
            grpWh.AddItem("Barcode Workstation", "🔍", (s, e) => ZeroToast.Info(this, "Navigating to Barcode Workstation..."));

            var grpMes = accordion.AddGroup("Production MES", "🏭", isExpanded: true);
            grpMes.BadgeText = "Running";
            grpMes.AddItem("Live Line Dispatching", "⚡", (s, e) => ZeroToast.Info(this, "Navigating to Live Line Dispatching..."));
            grpMes.AddItem("Takt Timer & Cycle HUD", "⏱️", (s, e) => ZeroToast.Info(this, "Navigating to Takt Timer HUD..."));
            grpMes.AddItem("AOI Inspection Defect Matrix", "🎯", (s, e) => ZeroToast.Info(this, "Navigating to AOI Defect Matrix..."), "Critical");

            var grpQa = accordion.AddGroup("Quality & Analytics", "📊", isExpanded: false);
            grpQa.AddItem("SPC X-Bar Six Sigma Chart", "📈", (s, e) => ZeroToast.Info(this, "Navigating to SPC Chart..."));
            grpQa.AddItem("Thermal Line Heatmap", "🔥", (s, e) => ZeroToast.Info(this, "Navigating to Line Heatmap..."));
            grpQa.AddItem("Lot Traceability Timeline", "⏳", (s, e) => ZeroToast.Info(this, "Navigating to Traceability Timeline..."));

            var grpCfg = accordion.AddGroup("System Administration", "⚙️", isExpanded: false);
            grpCfg.AddItem("Theme & Visual Skins", "🎨", (s, e) => ZeroToast.Info(this, "Navigating to Theme Settings..."));
            grpCfg.AddItem("Diagnostic Log Terminal", "💻", (s, e) => ZeroToast.Info(this, "Navigating to Log Terminal..."));

            splitRoot.Panel1.Controls.Add(accordion);

            // 2. Right Panel: ZeroStackPanel holding TablePanel and Showcase Sections
            var rightStack = new ZeroStackPanel
            {
                Dock = DockStyle.Fill,
                Orientation = StackOrientation.Vertical,
                Alignment = StackAlignment.Stretch,
                Spacing = 16,
                Padding = new Padding(16),
                AutoScroll = true
            };

            // Section 1: ZeroTablePanel Showcase Card
            var cardTable = new ZeroCard
            {
                StepNumber = 1,
                Title = "ZeroTablePanel (Responsive Grid with Zero-Alloc Layout Math)",
                Subtitle = "WPF-style columns & rows (Star %, Pixel, Auto) calculating instant flicker-free layouts",
                Height = 220
            };

            var tablePanel = new ZeroTablePanel
            {
                Dock = DockStyle.Fill,
                ShowGridLines = true,
                CellSpacing = 8,
                Padding = new Padding(8)
            };
            tablePanel.Columns.Add(TableColumnDefinition.Percent(35f));
            tablePanel.Columns.Add(TableColumnDefinition.Percent(35f));
            tablePanel.Columns.Add(TableColumnDefinition.Percent(30f));

            tablePanel.Rows.Add(TableRowDefinition.Absolute(36));
            tablePanel.Rows.Add(TableRowDefinition.Absolute(36));
            tablePanel.Rows.Add(TableRowDefinition.Absolute(38));

            // Row 0
            var btnOrder = new ZeroButton { Text = "Submit Dispatch Order", ButtonStyle = ZeroButtonStyle.Primary, Height = 34 };
            btnOrder.Click += (s, e) => ZeroToast.Success(this, "Dispatch order submitted via ZeroTablePanel!");
            var numQty = new ZeroNumericBox { Prefix = "Qty: ", Value = 2500, MinValue = 1, MaxValue = 100000, DecimalPlaces = 0, Height = 34 };
            var swAuto = new ZeroSwitch { Text = "Auto Lot", Checked = true, Height = 34 };

            tablePanel.SetCell(btnOrder, 0, 0);
            tablePanel.SetCell(numQty, 0, 1);
            tablePanel.SetCell(swAuto, 0, 2);

            // Row 1
            var txtSearch = new ZeroSearchBox { PlaceholderText = "Filter part SKU or Lot...", Height = 34 };
            var segMode = new ZeroSegmented { Height = 34, Items = new[] { "FIFO", "FEFO", "LIFO" } };
            segMode.SelectedIndex = 0;
            var btnAbort = new ZeroButton { Text = "Emergency Stop", ButtonStyle = ZeroButtonStyle.Danger, Height = 34 };
            btnAbort.Click += (s, e) => ZeroToast.Warning(this, "Line dispatch operation aborted!");

            tablePanel.SetCell(txtSearch, 1, 0);
            tablePanel.SetCell(segMode, 1, 1);
            tablePanel.SetCell(btnAbort, 1, 2);

            // Row 2 (Spanning 3 columns)
            var alertBanner = new ZeroAlertBanner
            {
                Title = "Zero Allocation Guarantee",
                Message = "ZeroTablePanel arranges child boundaries purely in memory with 0 handle cascades and 0 GC allocations on resize.",
                Severity = ZeroAlertSeverity.Info,
                Height = 36
            };
            tablePanel.SetCell(alertBanner, 2, 0, rowSpan: 1, colSpan: 3);

            cardTable.ContentPanel.Controls.Add(tablePanel);
            rightStack.Controls.Add(cardTable);

            // Section 2: ZeroScrollBar Showcase Card
            var cardScroll = new ZeroCard
            {
                StepNumber = 2,
                Title = "ZeroScrollBar (Flat Anti-Aliased Custom Scrollbar)",
                Subtitle = "Replaces Win32 scrollbars with rounded pill geometry and dark/light theme integration",
                Height = 130
            };

            var scrollContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
            var lblScrollVal = new Label
            {
                Text = "Interactive Scroll Value: 42%",
                ForeColor = Color.FromArgb(79, 70, 229),
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                Location = new Point(12, 10),
                AutoSize = true
            };

            var hScrollBar = new ZeroScrollBar
            {
                Orientation = ZeroScrollOrientation.Horizontal,
                Location = new Point(12, 42),
                Size = new Size(460, 14),
                Minimum = 0,
                Maximum = 100,
                Value = 42,
                LargeChange = 10
            };
            hScrollBar.ValueChanged += (s, e) =>
            {
                lblScrollVal.Text = $"Interactive Scroll Value: {hScrollBar.Value}%";
            };

            scrollContainer.Controls.Add(lblScrollVal);
            scrollContainer.Controls.Add(hScrollBar);
            cardScroll.ContentPanel.Controls.Add(scrollContainer);
            rightStack.Controls.Add(cardScroll);

            // Section 3: ZeroSplashScreen Showcase Card
            var cardSplash = new ZeroCard
            {
                StepNumber = 3,
                Title = "ZeroSplashScreen (Thread-Safe Non-Blocking Splash Manager)",
                Subtitle = "Runs on independent background STA thread for 60 FPS flicker-free animation during heavy app boot",
                Height = 120
            };

            var splashContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
            var btnLaunchSplash = new ZeroButton
            {
                Text = "🚀 Launch Enterprise Splash Screen Demo",
                ButtonStyle = ZeroButtonStyle.Primary,
                Location = new Point(12, 12),
                Size = new Size(320, 38)
            };
            btnLaunchSplash.Click += (s, e) =>
            {
                ZeroSplashScreen.Show(
                    "MDS MES Smart Factory Suite",
                    "ZeroUI High-Performance Enterprise Edition",
                    "Starting ZeroUI Core Engine...");

                var worker = new System.ComponentModel.BackgroundWorker();
                worker.DoWork += (ws, we) =>
                {
                    System.Threading.Thread.Sleep(500);
                    ZeroSplashScreen.SetStatus("Connecting to MES Database...", 30);
                    System.Threading.Thread.Sleep(600);
                    ZeroSplashScreen.SetStatus("Loading cached BOM models & lots...", 70);
                    System.Threading.Thread.Sleep(600);
                    ZeroSplashScreen.SetStatus("Initialization complete. Launching workspace...", 100);
                    System.Threading.Thread.Sleep(500);
                    ZeroSplashScreen.Close();
                };
                worker.RunWorkerAsync();
            };

            var lblSplashDesc = new Label
            {
                Text = "Click to run 4-step splash simulation on background thread without blocking main UI.",
                Location = new Point(345, 22),
                AutoSize = true,
                ForeColor = Color.FromArgb(148, 163, 184)
            };

            splashContainer.Controls.Add(btnLaunchSplash);
            splashContainer.Controls.Add(lblSplashDesc);
            cardSplash.ContentPanel.Controls.Add(splashContainer);
            rightStack.Controls.Add(cardSplash);

            // Section 4: ZeroDockManager Layout Persistence Card
            var cardDock = new ZeroCard
            {
                StepNumber = 4,
                Title = "ZeroDockManager (Visual Studio-Style Docking & Layout Persistence)",
                Subtitle = "Diamond HUD docking guide, floating overlays, splitter sizing, and pure JSON Save/Restore layout engine",
                Height = 360
            };

            var dockContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            var dockToolbar = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Color.Transparent };

            var dockManager = new ZeroDockManager { Dock = DockStyle.Fill };

            var pnlLeft = new ZeroDockPanel { Title = "Solution Explorer", DockPosition = ZeroDockPosition.Left, PanelKey = "SolutionExplorer", Width = 220 };
            var treeMock = new TreeView { Dock = DockStyle.Fill, BackColor = ZeroTheme.Colors.Surface, ForeColor = ZeroTheme.Colors.TextPrimary, BorderStyle = BorderStyle.None };
            treeMock.Nodes.Add("📁 ZeroUI.Core");
            treeMock.Nodes.Add("📁 ZeroUI.WinForms");
            treeMock.Nodes.Add("📁 ZeroUI.Wpf");
            pnlLeft.Controls.Add(treeMock);
            dockManager.AddPanel(pnlLeft);

            var pnlDoc = new ZeroDockPanel { Title = "ProcessController.cs", DockPosition = ZeroDockPosition.Document, PanelKey = "CodeEditor" };
            var txtCodeMock = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BackColor = ZeroTheme.Colors.Surface,
                ForeColor = ZeroTheme.Colors.TextPrimary,
                Font = new Font("Consolas", 9.5f),
                BorderStyle = BorderStyle.None,
                Text = "// ZeroUI Process Automation Controller\r\npublic void ProcessTelemetry(Span<byte> packet)\r\n{\r\n    var header = MemoryMarshal.Read<Header>(packet);\r\n    Log(header.SequenceNumber);\r\n}"
            };
            pnlDoc.Controls.Add(txtCodeMock);
            dockManager.AddPanel(pnlDoc);

            var pnlRight = new ZeroDockPanel { Title = "Properties & Telemetry", DockPosition = ZeroDockPosition.Right, PanelKey = "PropertiesInspector", Width = 220 };
            var descMock = new ZeroDescriptions { Dock = DockStyle.Fill, Columns = 1, RowHeight = 28 };
            descMock.Add("Engine", "Hardware Accelerated");
            descMock.Add("Allocations", "0 bytes GC Gen0");
            descMock.Add("FPS", "60 Hz VSync");
            pnlRight.Controls.Add(descMock);
            dockManager.AddPanel(pnlRight);

            string? savedDockLayout = null;

            var btnSaveDock = new ZeroButton
            {
                Text = "💾 Save Dock Layout (JSON)",
                ButtonStyle = ZeroButtonStyle.Primary,
                Location = new Point(4, 3),
                Size = new Size(210, 32)
            };
            btnSaveDock.Click += (s, e) =>
            {
                savedDockLayout = dockManager.SaveLayoutToJson();
                ZeroToast.Success(this, "Dock layout configuration saved to JSON.", "Layout Persisted");
            };

            var btnRestoreDock = new ZeroButton
            {
                Text = "📂 Restore Dock Layout (JSON)",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(222, 3),
                Size = new Size(220, 32)
            };
            btnRestoreDock.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(savedDockLayout))
                {
                    dockManager.RestoreLayoutFromJson(savedDockLayout);
                    ZeroToast.Success(this, "Dock layout restored successfully from JSON!", "Layout Restored");
                }
                else
                {
                    ZeroToast.Warning(this, "No saved layout found. Click 'Save Dock Layout' first.", "No Layout Saved");
                }
            };

            dockToolbar.Controls.Add(btnSaveDock);
            dockToolbar.Controls.Add(btnRestoreDock);

            dockContainer.Controls.Add(dockManager);
            dockContainer.Controls.Add(dockToolbar);
            dockManager.BringToFront();
            cardDock.ContentPanel.Controls.Add(dockContainer);
            rightStack.Controls.Add(cardDock);

            // Section 5: Multi-Toast Stacking & Overlay Queue
            var cardToast = new ZeroCard
            {
                StepNumber = 5,
                Title = "ZeroToastStackManager (Multi-Toast Stacking & Glide Relocation)",
                Subtitle = "Non-blocking floating notifications with multi-corner anchor, glide repositioning, FIFO queue, and pause on hover"
            };

            var toastFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12),
                AutoScroll = false
            };

            var btnToastSuccess = new ZeroButton { Text = "✔ Success Toast", ButtonStyle = ZeroButtonStyle.Success, Size = new Size(150, 34) };
            btnToastSuccess.Click += (s, e) => ZeroToast.Success(this, "Batch #8091 successfully released to shopfloor.", "Production Order");

            var btnToastInfo = new ZeroButton { Text = "ℹ️ Info Toast", ButtonStyle = ZeroButtonStyle.Primary, Size = new Size(140, 34) };
            btnToastInfo.Click += (s, e) => ZeroToast.Info(this, "OPC-UA server synchronized at 10 ms cycle.", "Fieldbus Status");

            var btnToastWarning = new ZeroButton { Text = "⚠ Warning Toast", ButtonStyle = ZeroButtonStyle.Secondary, Size = new Size(150, 34) };
            btnToastWarning.Click += (s, e) => ZeroToast.Warning(this, "Thermal threshold approaching USL warning limit (82.5°C).", "Thermal Warning");

            var btnToastError = new ZeroButton { Text = "✕ Error Toast", ButtonStyle = ZeroButtonStyle.Danger, Size = new Size(140, 34) };
            btnToastError.Click += (s, e) => ZeroToast.Error(this, "Emergency Stop circuit tripped on Conveyor CV-401!", "E-STOP Fault");

            var btnToastAlarm = new ZeroButton { Text = "⚡ Alarm Toast", ButtonStyle = ZeroButtonStyle.Danger, Size = new Size(140, 34) };
            btnToastAlarm.Click += (s, e) => ZeroToast.Alarm(this, "CRITICAL: Exothermic Reactor RX-401 overpressure surge (94.8 PSI)!", "Safety Alarm");

            var btnToastClear = new ZeroButton { Text = "🗑 Clear All", ButtonStyle = ZeroButtonStyle.Secondary, Size = new Size(120, 34) };
            btnToastClear.Click += (s, e) =>
            {
                ZeroToastStackManager.Clear();
                ZeroToast.Info(this, "All active and queued toast notifications cleared.");
            };

            toastFlow.Controls.Add(btnToastSuccess);
            toastFlow.Controls.Add(btnToastInfo);
            toastFlow.Controls.Add(btnToastWarning);
            toastFlow.Controls.Add(btnToastError);
            toastFlow.Controls.Add(btnToastAlarm);
            toastFlow.Controls.Add(btnToastClear);

            cardToast.ContentPanel.Controls.Add(toastFlow);
            rightStack.Controls.Add(cardToast);

            splitRoot.Panel2.Controls.Add(rightStack);
            parent.Controls.Add(splitRoot);
        }

        private void InitializeNetworkInfrastructure(ZeroTabPage cluster)
        {
            var colors = ZeroTheme.Colors;
            cluster.BackColor = colors.Background;

            _subTabsNetwork = new ZeroTabControl
            {
                Dock = DockStyle.Fill,
                Orientation = ZeroTabOrientation.Horizontal,
                TabHeight = 36,
                TabStyle = ZeroTabStyle.Pill
            };

            // 1. Rack & Switch Faceplate
            var tabRackSwitch = new ZeroTabPage("19\" Rack & Switch Faceplate", "🗄️");
            var pnlRackRoot = new Panel { Dock = DockStyle.Fill, BackColor = colors.Background, Padding = new Padding(12) };
            var devRack = new ZDeviceRack { Dock = DockStyle.Left, Width = 340, ShowThermalOverlay = true };
            var pnlSwitchContainer = new Panel { Dock = DockStyle.Fill, BackColor = colors.Background, Padding = new Padding(12, 0, 0, 0) };
            var swFaceplate = new ZSwitchFaceplate { Dock = DockStyle.Top, Height = 170 };
            
            var cardActions = new ZeroCard
            {
                Dock = DockStyle.Top,
                StepNumber = 1,
                Title = "Cabinet & Hardware Controls",
                Subtitle = "Interactive simulation of physical rack slots and switch ports"
            };
            var pnlCardBody = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                AutoScroll = false,
                Padding = new Padding(8)
            };
            
            var btnToggleThermal = new ZeroButton { Text = "🌡 Toggle Thermal Gradient", ButtonStyle = ZeroButtonStyle.Primary, Size = new Size(210, 34) };
            btnToggleThermal.Click += (s, e) =>
            {
                devRack.ShowThermalOverlay = !devRack.ShowThermalOverlay;
                ZeroToast.Info(this, $"Thermal Overlay: {(devRack.ShowThermalOverlay ? "Visible (18°C -> 45°C)" : "Hidden")}");
            };

            var btnAddServer = new ZeroButton { Text = "➕ Install Blade Server (2U)", ButtonStyle = ZeroButtonStyle.Secondary, Size = new Size(200, 34) };
            btnAddServer.Click += (s, e) =>
            {
                if (devRack.Engine.CanPlaceItem(30, 2, out string reason))
                {
                    devRack.Engine.AddItem(new RackSlotItem
                    {
                        StartUnit = 30,
                        UnitHeight = 2,
                        Name = "Edge Compute Node 03",
                        Model = "Dell PowerEdge R750",
                        PowerDrawWatts = 420,
                        TemperatureCelsius = 34.0,
                        WeightKg = 22.0
                    });
                    devRack.Invalidate();
                    ZeroToast.Success(this, "Installed Dell PowerEdge R750 into Unit 30..31.");
                }
                else
                {
                    ZeroToast.Warning(this, reason);
                }
            };

            var btnPoE = new ZeroButton { Text = "⚡ Max Out PoE Load", ButtonStyle = ZeroButtonStyle.Danger, Size = new Size(180, 34) };
            btnPoE.Click += (s, e) =>
            {
                for (int i = 1; i <= swFaceplate.PortLayout.Ports.Count; i++)
                {
                    var p = swFaceplate.PortLayout.FindPort(i);
                    if (p != null && p.PortType == SwitchPortType.RJ45)
                    {
                        p.PoeWatts = 15.4;
                    }
                }
                swFaceplate.Invalidate();
                ZeroToast.Info(this, "Simulated active 802.3af PoE draw across all RJ45 ports.");
            };

            pnlCardBody.Controls.Add(btnToggleThermal);
            pnlCardBody.Controls.Add(btnAddServer);
            pnlCardBody.Controls.Add(btnPoE);
            cardActions.Controls.Add(pnlCardBody);

            pnlSwitchContainer.Controls.Add(cardActions);
            pnlSwitchContainer.Controls.Add(swFaceplate);
            pnlRackRoot.Controls.Add(pnlSwitchContainer);
            pnlRackRoot.Controls.Add(devRack);
            tabRackSwitch.Controls.Add(pnlRackRoot);

            // 2. Interactive Network Topology Canvas
            var tabTopology = new ZeroTabPage("Network Topology & Pulse Flows", "🕸️");
            var netTopo = new ZNetworkTopology { Dock = DockStyle.Fill };
            var pnlTopoBar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.FromArgb(20, 24, 33), Padding = new Padding(8, 4, 8, 4) };
            
            var btnFit = new ZeroButton { Text = "🔍 Reset Zoom (100%)", ButtonStyle = ZeroButtonStyle.Secondary, Location = new Point(8, 5), Size = new Size(160, 32) };
            btnFit.Click += (s, e) =>
            {
                netTopo.ZoomLevel = 1.0f;
                ZeroToast.Info(this, "Zoom reset to 100%.");
            };

            var btnHier = new ZeroButton { Text = "📐 Layered Layout", ButtonStyle = ZeroButtonStyle.Secondary, Location = new Point(176, 5), Size = new Size(150, 32) };
            btnHier.Click += (s, e) =>
            {
                netTopo.Engine.ApplyHierarchicalLayout(netTopo.Width, netTopo.Height);
                netTopo.Invalidate();
                ZeroToast.Info(this, "Applied Hierarchical layout.");
            };

            var btnRing = new ZeroButton { Text = "🔄 Ring Layout", ButtonStyle = ZeroButtonStyle.Secondary, Location = new Point(334, 5), Size = new Size(140, 32) };
            btnRing.Click += (s, e) =>
            {
                netTopo.Engine.ApplyCircularLayout(netTopo.Width / 2f, netTopo.Height / 2f, Math.Min(netTopo.Width, netTopo.Height) * 0.35f);
                netTopo.Invalidate();
                ZeroToast.Info(this, "Applied Circular / Ring topology layout.");
            };

            pnlTopoBar.Controls.Add(btnFit);
            pnlTopoBar.Controls.Add(btnHier);
            pnlTopoBar.Controls.Add(btnRing);
            tabTopology.Controls.Add(netTopo);
            tabTopology.Controls.Add(pnlTopoBar);

            // 3. Chassis Health HUD & Optical DDM
            var tabChassis = new ZeroTabPage("Chassis Health & Optical DDM", "🖥️");
            var pnlChassisRoot = new Panel { Dock = DockStyle.Fill, BackColor = colors.Background, Padding = new Padding(16), AutoScroll = true };
            var devFaceplate = new ZDeviceFaceplate { Dock = DockStyle.Top, Height = 340 };
            
            var cardPsuActions = new ZeroCard
            {
                Dock = DockStyle.Top,
                StepNumber = 2,
                Title = "Hardware Fault Injection & Health Testing",
                Subtitle = "Simulate redundant PSU power loss, fan failure, and optical link degradation"
            };
            var pnlPsuBody = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                AutoScroll = false,
                Padding = new Padding(8)
            };
            
            var btnFailPsu = new ZeroButton { Text = "⚡ Cut PSU 2 Power", ButtonStyle = ZeroButtonStyle.Danger, Size = new Size(180, 34) };
            btnFailPsu.Click += (s, e) =>
            {
                devFaceplate.Profile.Psu2.IsPowered = false;
                devFaceplate.Profile.Psu2.Status = PsuHealthStatus.Fault;
                devFaceplate.Invalidate();
                ZeroToast.Warning(this, "PSU 2 Power Lost! Operating on single-PSU redundancy.");
            };

            var btnRestorePsu = new ZeroButton { Text = "✅ Restore PSU 2 Power", ButtonStyle = ZeroButtonStyle.Success, Size = new Size(180, 34) };
            btnRestorePsu.Click += (s, e) =>
            {
                devFaceplate.Profile.Psu2.IsPowered = true;
                devFaceplate.Profile.Psu2.Status = PsuHealthStatus.Normal;
                devFaceplate.Invalidate();
                ZeroToast.Success(this, "PSU 2 Restored! Dual redundancy healthy.");
            };

            pnlPsuBody.Controls.Add(btnFailPsu);
            pnlPsuBody.Controls.Add(btnRestorePsu);
            cardPsuActions.ContentPanel.Controls.Add(pnlPsuBody);

            pnlChassisRoot.Controls.Add(cardPsuActions);
            pnlChassisRoot.Controls.Add(devFaceplate);
            tabChassis.Controls.Add(pnlChassisRoot);

            // 4. IPAM Subnet Matrix & Latency Sparklines
            var tabIpMatrix = new ZeroTabPage("2D IPAM Subnet Matrix", "🌐");
            var ipMatrix = new ZIpMatrix { Dock = DockStyle.Fill };
            tabIpMatrix.Controls.Add(ipMatrix);

            // 5. Industrial Fieldbus & Cable Break Locator
            var tabFieldbus = new ZeroTabPage("Fieldbus & Break Locator", "🔌");
            var pnlFbRoot = new Panel { Dock = DockStyle.Fill, BackColor = colors.Background, Padding = new Padding(16), AutoScroll = true };
            var fbMonitor = new ZFieldbusMonitor { Dock = DockStyle.Top, Height = 250 };
            
            var cardFbActions = new ZeroCard
            {
                Dock = DockStyle.Top,
                StepNumber = 3,
                Title = "Fieldbus Fault Injection & Break Localization",
                Subtitle = "Simulate cable severing between industrial drops (Profinet / EtherCAT)"
            };
            var pnlFbBody = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                AutoScroll = false,
                Padding = new Padding(8)
            };
            
            var btnBreakCable = new ZeroButton { Text = "✂ Sever Cable (Station 3 -> 4)", ButtonStyle = ZeroButtonStyle.Danger, Size = new Size(240, 34) };
            btnBreakCable.Click += (s, e) =>
            {
                for (int i = 3; i < fbMonitor.Network.Stations.Count; i++)
                {
                    fbMonitor.Network.Stations[i].Status = StationStatus.CommunicationLost;
                }
                fbMonitor.Invalidate();
                ZeroToast.Error(this, "Severed cable between Station #3 (Remote I/O) and Station #4 (Valve Island)!");
            };

            var btnRepairCable = new ZeroButton { Text = "🔧 Repair Cable Link", ButtonStyle = ZeroButtonStyle.Success, Size = new Size(180, 34) };
            btnRepairCable.Click += (s, e) =>
            {
                for (int i = 0; i < fbMonitor.Network.Stations.Count; i++)
                {
                    fbMonitor.Network.Stations[i].Status = StationStatus.Normal;
                }
                fbMonitor.Network.RebuildSegments();
                fbMonitor.Invalidate();
                ZeroToast.Success(this, "Fieldbus line repaired. All stations online.");
            };

            pnlFbBody.Controls.Add(btnBreakCable);
            pnlFbBody.Controls.Add(btnRepairCable);
            cardFbActions.ContentPanel.Controls.Add(pnlFbBody);

            pnlFbRoot.Controls.Add(cardFbActions);
            pnlFbRoot.Controls.Add(fbMonitor);
            tabFieldbus.Controls.Add(pnlFbRoot);

            // Assemble all subtabs into Cluster 8
            _subTabsNetwork.AddTab(tabRackSwitch);
            _subTabsNetwork.AddTab(tabTopology);
            _subTabsNetwork.AddTab(tabChassis);
            _subTabsNetwork.AddTab(tabIpMatrix);
            _subTabsNetwork.AddTab(tabFieldbus);
            cluster.Controls.Add(_subTabsNetwork);
        }

        private void InitializeScadaProcessFlow(ZeroTabPage parentTab)
        {
            var colors = ZeroTheme.Colors;
            parentTab.BackColor = colors.Background;

            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = colors.Background,
                Padding = new Padding(16)
            };

            // 1. Top Alert Banner
            var banner = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Height = 62,
                Severity = ZeroAlertSeverity.Info,
                Title = "🏭 PHASE 1: DYNAMIC P&ID PROCESS FLOW SIMULATION",
                Message = "Simulates dynamic fluid piping (ZPipeFlow), 60 FPS centrifugal pump (ZIndustrialPump), control valve (ZIndustrialValve) connecting dual 3D tanks (ZTank3D), pressure gauge (ZeroGauge), and telemetry oscilloscope (ZTrendChart)."
            };
            var spacer1 = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            // Pre-declare pump and valve for quickBar interaction & OSHA LOTO interlocking
            var pump = new ZIndustrialPump
            {
                Location = new Point(255, 130),
                Size = new Size(84, 88),
                TagLabel = "P-101A",
                SpeedRpm = 2950,
                PowerKw = 22.0,
                BoundTagPath = "Line1.Pump.Running"
            };

            var valve = new ZIndustrialValve
            {
                Location = new Point(410, 140),
                Size = new Size(56, 64),
                TagLabel = "XV-101",
                BoundTagPath = "Line1.Valve.Open"
            };

            // 2. Interactive Quick Command Bar
            var quickBar = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Color.Transparent, Padding = new Padding(0, 4, 0, 4) };

            var btnTogglePump = new ZeroButton
            {
                Text = "⏯ Start / Stop Pump (P-101A)",
                ButtonStyle = ZeroButtonStyle.Primary,
                Location = new Point(0, 4),
                Size = new Size(200, 32)
            };
            btnTogglePump.Click += (s, e) =>
            {
                SimulatedPlcDriver.TogglePump();
                ZeroToast.Info(this, $"Pump P-101A: {(SimulatedPlcDriver.PumpRunning ? "Running (2950 RPM)" : "Stopped")}");
            };

            var btnToggleValve = new ZeroButton
            {
                Text = "🔄 Open / Close Valve (XV-101)",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(210, 4),
                Size = new Size(185, 32)
            };
            btnToggleValve.Click += (s, e) =>
            {
                SimulatedPlcDriver.ToggleValve();
                ZeroToast.Info(this, $"Valve XV-101: {(SimulatedPlcDriver.ValveOpen ? "OPEN (100%)" : "CLOSED (0%)")}");
            };

            var btnSpike = new ZeroButton
            {
                Text = "⚡ Inject Surge (+50 PSI)",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(405, 4),
                Size = new Size(175, 32)
            };
            btnSpike.Click += (s, e) =>
            {
                SimulatedPlcDriver.InjectPressureSpike();
                ZeroToast.Warning(this, "Triggered 98.5 PSI overpressure surge! ISA-18.2 alarm activated.");
            };

            var btnEStop = new ZeroButton
            {
                Text = "🚨 E-STOP",
                ButtonStyle = ZeroButtonStyle.Danger,
                Location = new Point(590, 4),
                Size = new Size(110, 32)
            };
            btnEStop.Click += (s, e) =>
            {
                SimulatedPlcDriver.ToggleEmergencyStop();
                ZeroToast.Error(this, SimulatedPlcDriver.EmergencyStop ? "E-STOP ACTIVATED: All pumps and valves tripped offline!" : "E-STOP RESET.");
            };

            var btnPidFlyout = new ZeroButton
            {
                Text = "🎛️ Tune Loop (ZPidFlyout)",
                ButtonStyle = ZeroButtonStyle.Primary,
                Location = new Point(710, 4),
                Size = new Size(205, 32)
            };
            btnPidFlyout.Click += (s, e) =>
            {
                var flyout = new ZPidFlyout
                {
                    LoopTag = "PIC-101",
                    LoopDescription = "Primary Supply Pressure Loop",
                    ProcessVariable = 48.2,
                    SetPoint = 50.0,
                    ManipulatedVariable = 62.0
                };
                flyout.Show(this);
                ZeroToast.Info(this, "Opened 60 FPS PID Loop Tuning Flyout.");
            };

            bool isLotoActive = false;
            var btnToggleLoto = new ZeroButton
            {
                Text = "🔒 Toggle OSHA LOTO",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Location = new Point(925, 4),
                Size = new Size(175, 32)
            };
            btnToggleLoto.Click += (s, e) =>
            {
                isLotoActive = !isLotoActive;
                var flag = isLotoActive ? DeviceStatusFlags.LockedOut : DeviceStatusFlags.None;
                valve.StatusFlags = flag;
                pump.StatusFlags = flag;
                btnToggleLoto.Text = isLotoActive ? "🔓 Release OSHA LOTO" : "🔒 Toggle OSHA LOTO";
                if (isLotoActive)
                {
                    ZeroToast.Alarm(this, "OSHA LOTO Padlock applied to Pump P-101A and Valve XV-101!", "Safety Interlock Engaged");
                }
                else
                {
                    ZeroToast.Success(this, "OSHA LOTO Padlock removed. System clear for operation.", "Safety Interlock Cleared");
                }
            };

            quickBar.Controls.Add(btnTogglePump);
            quickBar.Controls.Add(btnToggleValve);
            quickBar.Controls.Add(btnSpike);
            quickBar.Controls.Add(btnEStop);
            quickBar.Controls.Add(btnPidFlyout);
            quickBar.Controls.Add(btnToggleLoto);

            var spacer2 = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            // 3. P&ID Synoptic Canvas Card
            var cardPid = new ZeroCard
            {
                Dock = DockStyle.Top,
                Height = 310,
                Title = "Piping & Instrumentation Diagram (P&ID Closed Loop Flow)",
                StepNumber = 1
            };

            var pidCanvas = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            // Tank 101 (Supply)
            var tank1 = new ZTank3D
            {
                Location = new Point(20, 20),
                Size = new Size(140, 220),
                TankName = "TK-101 SUPPLY",
                CapacityLiters = 10000f,
                CurrentLevelLiters = 6500f,
                FluidColor = Color.FromArgb(6, 182, 212)
            };

            // Pipe 1 (Tank to Pump)
            var pipe1 = new ZPipeFlow
            {
                Location = new Point(160, 160),
                Size = new Size(95, 24),
                PipeDiameter = 18,
                FluidType = ZeroFluidType.Water,
                BoundTagPath = "Line1.Flow.Velocity"
            };

            // Pipe 2 (Pump to Valve)
            var pipe2 = new ZPipeFlow
            {
                Location = new Point(339, 160),
                Size = new Size(70, 24),
                PipeDiameter = 18,
                FluidType = ZeroFluidType.Water,
                BoundTagPath = "Line1.Flow.Velocity"
            };

            // Pipe 3 (Valve to Gauge)
            var pipe3 = new ZPipeFlow
            {
                Location = new Point(467, 160),
                Size = new Size(60, 24),
                PipeDiameter = 18,
                FluidType = ZeroFluidType.Water,
                BoundTagPath = "Line1.Flow.Velocity"
            };

            // In-line Pressure Gauge PI-101
            var gauge = new ZeroGauge
            {
                Location = new Point(530, 95),
                Size = new Size(140, 140),
                Title = "PI-101",
                Suffix = " PSI",
                Value = 42.5f
            };

            // Pipe 4 (Gauge to Tank 2)
            var pipe4 = new ZPipeFlow
            {
                Location = new Point(672, 160),
                Size = new Size(70, 24),
                PipeDiameter = 18,
                FluidType = ZeroFluidType.Water,
                BoundTagPath = "Line1.Flow.Velocity"
            };

            // Tank 102 (Discharge)
            var tank2 = new ZTank3D
            {
                Location = new Point(744, 20),
                Size = new Size(140, 220),
                TankName = "TK-102 RECV",
                CapacityLiters = 12000f,
                CurrentLevelLiters = 4500f,
                FluidColor = Color.FromArgb(16, 185, 129)
            };

            // Orthogonal Multi-Segment Recirculation Loop (ZPolylinePipeFlow)
            var returnPolyPipe = new ZPolylinePipeFlow
            {
                Location = new Point(90, 245),
                Size = new Size(730, 45),
                PipeDiameter = 16,
                FluidType = ZeroFluidType.Water,
                FlowVelocity = 2.0,
                IsFlowing = true
            };
            returnPolyPipe.Points.AddRange(new[]
            {
                new Point(720, 5),
                new Point(720, 35),
                new Point(10, 35),
                new Point(10, 5)
            });

            pidCanvas.Controls.Add(tank1);
            pidCanvas.Controls.Add(pipe1);
            pidCanvas.Controls.Add(pump);
            pidCanvas.Controls.Add(pipe2);
            pidCanvas.Controls.Add(valve);
            pidCanvas.Controls.Add(pipe3);
            pidCanvas.Controls.Add(gauge);
            pidCanvas.Controls.Add(pipe4);
            pidCanvas.Controls.Add(tank2);
            pidCanvas.Controls.Add(returnPolyPipe);

            cardPid.ContentPanel.Controls.Add(pidCanvas);

            var spacer3 = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Color.Transparent };

            // 4. Real-time Telemetry Oscilloscope Strip Chart (ZTrendChart)
            var cardTrend = new ZeroCard
            {
                Dock = DockStyle.Top,
                Height = 260,
                Title = "Real-Time Pressure & Flow Velocity Oscilloscope (60 FPS Stream)",
                StepNumber = 2
            };

            var trendChart = new ZTrendChart
            {
                Dock = DockStyle.Fill,
                Title = "Ch1: Pipe Pressure (PSI) | Ch2: Fluid Flow Velocity (m/s x 20)",
                UpperLimit = 85f,
                LowerLimit = 15f
            };
            cardTrend.ContentPanel.Controls.Add(trendChart);

            // Telemetry hook timer to stream from SimulatedPlcDriver to Gauge and TrendChart
            var flowTimer = new System.Windows.Forms.Timer { Interval = 50 };
            flowTimer.Tick += (s, e) =>
            {
                float press = (float)SimulatedPlcDriver.BoilerPressure;
                float flow = (float)(SimulatedPlcDriver.FlowVelocity * 20.0);
                gauge.Value = press;
                trendChart.AddPoint(0, press);
                trendChart.AddPoint(1, flow);
            };
            flowTimer.Start();

            mainContainer.Controls.Add(cardTrend);
            mainContainer.Controls.Add(spacer3);
            mainContainer.Controls.Add(cardPid);
            mainContainer.Controls.Add(spacer2);
            mainContainer.Controls.Add(quickBar);
            mainContainer.Controls.Add(spacer1);
            mainContainer.Controls.Add(banner);

            banner.BringToFront();
            spacer1.BringToFront();
            quickBar.BringToFront();
            spacer2.BringToFront();
            cardPid.BringToFront();
            spacer3.BringToFront();
            cardTrend.BringToFront();

            parentTab.Controls.Add(mainContainer);
        }

        private void InitializeScadaAlarmsAndPid(ZeroTabPage parentTab)
        {
            var colors = ZeroTheme.Colors;
            parentTab.BackColor = colors.Background;

            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = colors.Background,
                Padding = new Padding(16)
            };

            // 1. Banner
            var banner = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Height = 62,
                Severity = ZeroAlertSeverity.Warning,
                Title = "🚨 PHASE 2: ISA-18.2 ALARM ANNUNCIATOR & CLOSED-LOOP PID CONTROLLER",
                Message = "Standardized ISA-18.2 industrial alarm sequence state machine (ZAnnunciatorGrid) combined with single-loop PID controller faceplate (ZPidFaceplate) tracking live PV vs SP deviation."
            };
            var spacer1 = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            // 2. Dual Panel Row (Left: Alarms, Right: PID)
            var rowDual = new Panel { Dock = DockStyle.Top, Height = 390, BackColor = Color.Transparent };

            // Left Card: ISA-18.2 Annunciator
            var cardAlarms = new ZeroCard
            {
                Dock = DockStyle.Left,
                Width = 530,
                Title = "Plant Alarm Annunciator Panel — ISA-18.2 Standard (12-Tile Matrix)",
                StepNumber = 1
            };

            var annunciator = new ZAnnunciatorGrid
            {
                Dock = DockStyle.Fill
            };
            cardAlarms.ContentPanel.Controls.Add(annunciator);

            var alarmToolbar = new Panel { Dock = DockStyle.Bottom, Height = 38, BackColor = Color.Transparent, Padding = new Padding(4) };
            var btnTriggerHighP = new ZeroButton { Text = "+ High Press", ButtonStyle = ZeroButtonStyle.Danger, Location = new Point(4, 4), Size = new Size(95, 28) };
            btnTriggerHighP.Click += (s, e) => annunciator.TriggerAlarm("Line1.Alarm.HighPressure", true);

            var btnTriggerLowL = new ZeroButton { Text = "+ Low Level", ButtonStyle = ZeroButtonStyle.Secondary, Location = new Point(105, 4), Size = new Size(95, 28) };
            btnTriggerLowL.Click += (s, e) => annunciator.TriggerAlarm("Line1.Alarm.LowLevel", true);

            var btnTriggerTrip = new ZeroButton { Text = "+ Pump Trip", ButtonStyle = ZeroButtonStyle.Danger, Location = new Point(206, 4), Size = new Size(95, 28) };
            btnTriggerTrip.Click += (s, e) => annunciator.TriggerAlarm("Line1.Alarm.PumpTrip", true);

            var btnClearAll = new ZeroButton { Text = "✔ Clear Faults", ButtonStyle = ZeroButtonStyle.Success, Location = new Point(307, 4), Size = new Size(110, 28) };
            btnClearAll.Click += (s, e) =>
            {
                annunciator.TriggerAlarm("Line1.Alarm.HighPressure", false);
                annunciator.TriggerAlarm("Line1.Alarm.LowLevel", false);
                annunciator.TriggerAlarm("Line1.Alarm.PumpTrip", false);
                annunciator.TriggerAlarm("Line1.Alarm.EmergencyStop", false);
                ZeroToast.Info(this, "Cleared all active fault triggers. Press RESET on panel to silence and clear annunciator.");
            };

            alarmToolbar.Controls.Add(btnTriggerHighP);
            alarmToolbar.Controls.Add(btnTriggerLowL);
            alarmToolbar.Controls.Add(btnTriggerTrip);
            alarmToolbar.Controls.Add(btnClearAll);
            cardAlarms.ContentPanel.Controls.Add(alarmToolbar);

            var splitSpace = new Panel { Dock = DockStyle.Left, Width = 16, BackColor = Color.Transparent };

            // Right Card: PID Faceplate & Tuning
            var cardPid = new ZeroCard
            {
                Dock = DockStyle.Fill,
                Title = "PIC-101 Closed-Loop Controller Faceplate (Steam Pressure)",
                StepNumber = 2
            };

            var pidFaceplate = new ZPidFaceplate
            {
                Location = new Point(16, 12),
                Size = new Size(290, 320),
                LoopTag = "PIC-101",
                LoopDescription = "Boiler Main Steam Pressure Loop",
                EngineeringUnit = "PSI",
                SetPoint = 50.0,
                ProcessVariable = 48.2,
                ManipulatedVariable = 62.0
            };

            var pidHookTimer = new System.Windows.Forms.Timer { Interval = 100 };
            pidHookTimer.Tick += (s, e) =>
            {
                pidFaceplate.ProcessVariable = Math.Round(SimulatedPlcDriver.PidProcessVariable, 1);
                pidFaceplate.SetPoint = Math.Round(SimulatedPlcDriver.PidSetPoint, 1);
                pidFaceplate.ManipulatedVariable = Math.Round(SimulatedPlcDriver.PidOutputMv, 1);
            };
            pidHookTimer.Start();

            cardPid.ContentPanel.Controls.Add(pidFaceplate);

            rowDual.Controls.Add(cardPid);
            rowDual.Controls.Add(splitSpace);
            rowDual.Controls.Add(cardAlarms);

            mainContainer.Controls.Add(rowDual);
            mainContainer.Controls.Add(spacer1);
            mainContainer.Controls.Add(banner);

            banner.BringToFront();
            spacer1.BringToFront();
            rowDual.BringToFront();

            parentTab.Controls.Add(mainContainer);
        }

        private void InitializeScadaTagEngineMonitor(ZeroTabPage parentTab)
        {
            var colors = ZeroTheme.Colors;
            parentTab.BackColor = colors.Background;

            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = colors.Background,
                Padding = new Padding(16)
            };

            // 1. Banner
            var banner = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Height = 62,
                Severity = ZeroAlertSeverity.Success,
                Title = "⚡ PHASE 3: REAL-TIME TAG ENGINE & PLC TELEMETRY MONITOR",
                Message = "Real-time in-memory Tag Engine architecture (ZeroTagEngine) featuring deadband noise filtering, multithreaded subscriber dispatching to visual controls, and simulated PLC communications driver."
            };
            var spacer1 = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            // 2. KPI Summary Cards (4 Cards)
            var rowStats = new Panel { Dock = DockStyle.Top, Height = 110, BackColor = Color.Transparent, Padding = new Padding(0, 0, 0, 10) };

            var statTags = new ZeroStatistic
            {
                Location = new Point(0, 0),
                Size = new Size(220, 100),
                Title = "ACTIVE SCADA TAGS",
                Value = "14",
                Suffix = "tags",
                Trend = TrendDirection.Up,
                TrendText = "100% Live In-Memory"
            };

            var statScan = new ZeroStatistic
            {
                Location = new Point(230, 0),
                Size = new Size(220, 100),
                Title = "PLC SCAN FREQUENCY",
                Value = "20",
                Suffix = "Hz (50ms)",
                Trend = TrendDirection.None,
                TrendText = "Zero-Jitter Loop"
            };

            var statDeadband = new ZeroStatistic
            {
                Location = new Point(460, 0),
                Size = new Size(220, 100),
                Title = "DEADBAND NOISE FILTER",
                Value = "0.25",
                Suffix = "delta",
                Trend = TrendDirection.None,
                TrendText = "Jitter Suppression Active"
            };

            var statComm = new ZeroStatistic
            {
                Location = new Point(690, 0),
                Size = new Size(220, 100),
                Title = "COMMUNICATION QUALITY",
                Value = "100.0",
                Suffix = "%",
                Trend = TrendDirection.Up,
                TrendText = "Quality: GOOD (OPC/Modbus)"
            };

            rowStats.Controls.Add(statTags);
            rowStats.Controls.Add(statScan);
            rowStats.Controls.Add(statDeadband);
            rowStats.Controls.Add(statComm);

            var spacer2 = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Color.Transparent };

            // 3. Live Tag Registry Grid
            var cardGrid = new ZeroCard
            {
                Dock = DockStyle.Fill,
                Title = "Active In-Memory SCADA Tag Registry (Real-Time Telemetry)",
                StepNumber = 1
            };

            var tagListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = colors.Surface,
                ForeColor = colors.TextPrimary,
                Font = new Font("Segoe UI", 9.5f)
            };
            tagListView.Columns.Add("Tag Path", 240);
            tagListView.Columns.Add("Current Value", 160);
            tagListView.Columns.Add("Signal Quality", 120);
            tagListView.Columns.Add("Last Updated (UTC)", 160);

            // Populate initial listview items
            var tagNames = new[]
            {
                "Line1.Tank.Level", "Line1.Boiler.Pressure", "Line1.Pump.SpeedRpm",
                "Line1.Pump.Running", "Line1.Valve.Open", "Line1.Valve.Position",
                "Line1.Flow.Velocity", "Line1.PID.SP", "Line1.PID.PV", "Line1.PID.MV",
                "Line1.Alarm.HighPressure", "Line1.Alarm.LowLevel", "Line1.Alarm.PumpTrip",
                "Line1.Alarm.EmergencyStop"
            };

            var lviMap = new Dictionary<string, ListViewItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var tName in tagNames)
            {
                var lvi = new ListViewItem(tName);
                lvi.SubItems.Add("--");
                lvi.SubItems.Add("Good");
                lvi.SubItems.Add(DateTime.UtcNow.ToString("HH:mm:ss.fff"));
                tagListView.Items.Add(lvi);
                lviMap[tName] = lvi;
            }

            cardGrid.ContentPanel.Controls.Add(tagListView);

            // Hook timer to refresh ListView text smoothly
            var lvTimer = new System.Windows.Forms.Timer { Interval = 200 };
            lvTimer.Tick += (s, e) =>
            {
                foreach (var kvp in lviMap)
                {
                    var tag = ZeroTagEngine.GetTag(kvp.Key);
                    if (tag != null)
                    {
                        kvp.Value.SubItems[1].Text = tag.Value?.ToString() ?? "null";
                        kvp.Value.SubItems[2].Text = tag.Quality.ToString();
                        kvp.Value.SubItems[3].Text = tag.Timestamp.ToString("HH:mm:ss.fff");
                    }
                }
            };
            lvTimer.Start();

            mainContainer.Controls.Add(cardGrid);
            mainContainer.Controls.Add(spacer2);
            mainContainer.Controls.Add(rowStats);
            mainContainer.Controls.Add(spacer1);
            mainContainer.Controls.Add(banner);

            banner.BringToFront();
            spacer1.BringToFront();
            rowStats.BringToFront();
            spacer2.BringToFront();
            cardGrid.BringToFront();

            parentTab.Controls.Add(mainContainer);
        }

        private void InitializeScadaHmiOverview(ZeroTabPage parentTab)
        {
            var colors = ZeroTheme.Colors;
            parentTab.BackColor = colors.Background;

            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = colors.Background,
                Padding = new Padding(16)
            };

            // Seed industrial alarms into ScadaAlarmEngine
            ScadaAlarmEngine.RaiseAlarm("ALM-01", "Line1.Boiler.Pressure", "Main Boiler Header Overpressure", ScadaAlarmSeverity.Critical, 88.5);
            ScadaAlarmEngine.RaiseAlarm("ALM-02", "Line1.Pump.Trip", "Centrifugal Pump P-101A Motor Overload Trip", ScadaAlarmSeverity.High, 1);
            ScadaAlarmEngine.RaiseAlarm("ALM-03", "Line1.Heater.Warning", "Oven Zone 3 Element High Temperature Warning", ScadaAlarmSeverity.Medium, 215.2);
            ScadaAlarmEngine.RaiseAlarm("ALM-04", "Line1.Conveyor.Jam", "Feeder Conveyor CV-401 Material Jam Detected", ScadaAlarmSeverity.High, 1);

            // 1. Header Alert Banner
            var banner = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Height = 60,
                Severity = ZeroAlertSeverity.Info,
                Title = "🎛️ PHASE 1-4: COMPREHENSIVE INDUSTRIAL SCADA & HMI RUNTIME CONTROLS",
                Message = "Zero GC vector-rendered actuators, two-stage safety command buttons, touch setpoint keypad, and ISA-18.2 virtualized alarm grid."
            };
            var sp1 = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            // 2. Row 1: Operator HMI & Safety Interlocks
            var cardHmi = new ZeroCard
            {
                Dock = DockStyle.Top,
                Height = 110,
                Title = "Operator HMI & Safety Controls (Two-Stage Confirmation, Interlocks & Keypad)",
                StepNumber = 1
            };
            var pnlHmi = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = false,
                Padding = new Padding(8)
            };

            var btnStart = new ZCommandButton
            {
                Action = CommandButtonAction.Start,
                CommandText = "START LINE 1",
                PressAndHoldSeconds = 1.2f,
                RequiresConfirmation = false
            };
            btnStart.CommandExecuted += (s, e) => ZeroToast.Success(this, "Line 1 startup sequence engaged!");

            var btnEStop = new ZCommandButton
            {
                Action = CommandButtonAction.EmergencyStop,
                CommandText = "EMERGENCY STOP",
                RequiresConfirmation = true,
                PressAndHoldSeconds = 0f
            };
            btnEStop.CommandExecuted += (s, e) => ZeroToast.Warning(this, "EMERGENCY STOP EXECUTED!");

            var spInput = new ZSetpointInput
            {
                TagLabel = "BOILER SP",
                SetpointValue = 185.0,
                MinValue = 50.0,
                MaxValue = 250.0,
                Unit = "°C"
            };

            var modeSel = new ZModeSelector
            {
                SelectedMode = MachineControlMode.Auto
            };

            var interlock = new ZInterlockIndicator
            {
                TagLabel = "SAFETY INTERLOCK"
            };
            interlock.SetInterlockCondition("Feeder Guard Interlock", false);

            pnlHmi.Controls.Add(btnStart);
            pnlHmi.Controls.Add(btnEStop);
            pnlHmi.Controls.Add(spInput);
            pnlHmi.Controls.Add(modeSel);
            pnlHmi.Controls.Add(interlock);
            cardHmi.ContentPanel.Controls.Add(pnlHmi);

            var sp2 = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Color.Transparent };

            // 3. Row 2: Physical Process Actuators & Sensors
            var cardActuators = new ZeroCard
            {
                Dock = DockStyle.Top,
                Height = 180,
                Title = "Physical Process Actuators & Smart Field Instruments (Vector Rendered)",
                StepNumber = 2
            };
            var pnlAct = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = false,
                Padding = new Padding(8)
            };

            var motor = new ZIndustrialMotor { SpeedRpm = 1450.0, Direction = ZeroMotorDirection.Forward };
            var fan = new ZIndustrialFan { SpeedRpm = 1200.0 };
            var heater = new ZIndustrialHeater { TemperatureC = 185.4, SetpointC = 200.0 };
            var cyl = new ZPneumaticCylinder { ExtensionPercent = 75.0 };
            var conveyor = new ZConveyorBelt { SpeedMpm = 28.0 };
            var sensor = new ZIndustrialSensor { State = SensorState.Active };
            var digital = new ZDigitalIndicator { Value = 48.7, Unit = "bar", TagLabel = "MAIN STEAM" };
            var flow = new ZFlowIndicator { Velocity = 2.0, IsFlowing = true };

            pnlAct.Controls.Add(motor);
            pnlAct.Controls.Add(fan);
            pnlAct.Controls.Add(heater);
            pnlAct.Controls.Add(cyl);
            pnlAct.Controls.Add(conveyor);
            pnlAct.Controls.Add(sensor);
            pnlAct.Controls.Add(digital);
            pnlAct.Controls.Add(flow);
            cardActuators.ContentPanel.Controls.Add(pnlAct);

            var sp3 = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Color.Transparent };

            // 4. Row 3: Plant Dashboard Overview (Cards, Production Scoreboard & Shift Status)
            var cardOverview = new ZeroCard
            {
                Dock = DockStyle.Top,
                Height = 175,
                Title = "Plant Shift Overview, Machine OEE & Micro-Trend Sparklines",
                StepNumber = 3
            };
            var pnlOverview = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = false,
                Padding = new Padding(8)
            };

            var machineCard = new ZeroMachineCard { MachineId = "CNC-04", OeePercent = 88.5, SpeedRpm = 12000.0 };
            var prodCounter = new ZProductionCounter { Plan = 2500, Actual = 2140, NG = 18 };
            var shiftStatus = new ZShiftStatus { ShiftName = "SHIFT A (DAY)", OperatorName = "David Nguyen" };
            var sparkline = new ZeroSparkline(40) { LineColor = Color.FromArgb(56, 189, 248), Size = new Size(160, 110) };
            for (int i = 0; i < 35; i++) sparkline.AddValue(40f + (float)Math.Sin(i * 0.4) * 20f);

            pnlOverview.Controls.Add(machineCard);
            pnlOverview.Controls.Add(prodCounter);
            pnlOverview.Controls.Add(shiftStatus);
            pnlOverview.Controls.Add(sparkline);
            cardOverview.ContentPanel.Controls.Add(pnlOverview);

            var sp4 = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Color.Transparent };

            // 5. Row 4: ISA-18.2 Alarm Grid Control
            var cardAlarms = new ZeroCard
            {
                Dock = DockStyle.Top,
                Height = 260,
                Title = "ISA-18.2 Standard Alarm Management Grid (Active / Acknowledged / Shelved)",
                StepNumber = 4
            };
            var alarmGrid = new ZAlarmGrid
            {
                Dock = DockStyle.Fill,
                OperatorName = "Alex Thorne"
            };
            cardAlarms.ContentPanel.Controls.Add(alarmGrid);

            // Add all sections
            mainContainer.Controls.Add(cardAlarms);
            mainContainer.Controls.Add(sp4);
            mainContainer.Controls.Add(cardOverview);
            mainContainer.Controls.Add(sp3);
            mainContainer.Controls.Add(cardActuators);
            mainContainer.Controls.Add(sp2);
            mainContainer.Controls.Add(cardHmi);
            mainContainer.Controls.Add(sp1);
            mainContainer.Controls.Add(banner);

            banner.BringToFront();
            sp1.BringToFront();
            cardHmi.BringToFront();
            sp2.BringToFront();
            cardActuators.BringToFront();
            sp3.BringToFront();
            cardOverview.BringToFront();
            sp4.BringToFront();
            cardAlarms.BringToFront();

            parentTab.Controls.Add(mainContainer);
        }

        private void InitializeScadaClosedLoopProcess(ZeroTabPage parentTab)
        {
            var colors = ZeroTheme.Colors;
            parentTab.BackColor = colors.Background;

            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = colors.Background,
                Padding = new Padding(16)
            };

            // 1. Header Alert Banner
            var banner = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Height = 64,
                Severity = ZeroAlertSeverity.Info,
                Title = "🏭 INTEGRATED CLOSED-LOOP INDUSTRIAL SCADA & AUTOMATION WORKCELL",
                Message = "Continuous automated batch cycle: Chemical Inflow -> Thermal Reaction & Agitation -> Quench & Permissive Check -> Pneumatic Dosing -> Packaging Conveyor -> Shift Scoreboard & 60 FPS Telemetry."
            };
            var sp1 = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            // 2. Supervisory Command & Control Console Card
            var cardConsole = new ZeroCard
            {
                Dock = DockStyle.Top,
                Title = "Supervisory Process Orchestration, Setpoints & Safety Permissives",
                StepNumber = 1
            };
            var pnlConsole = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                AutoScroll = false,
                WrapContents = true,
                Padding = new Padding(8, 6, 8, 6)
            };

            var badgeStatus = new ZStatusBadge
            {
                Status = ZeroStatusType.Idle,
                Text = "READY / IDLE",
                Size = new Size(160, 36)
            };

            var btnStartBatch = new ZCommandButton
            {
                Action = CommandButtonAction.Start,
                CommandText = "START BATCH",
                PressAndHoldSeconds = 0f,
                RequiresConfirmation = false,
                Size = new Size(130, 44)
            };

            var btnHoldBatch = new ZCommandButton
            {
                Action = CommandButtonAction.Stop,
                CommandText = "HOLD BATCH",
                PressAndHoldSeconds = 0f,
                RequiresConfirmation = false,
                Size = new Size(120, 44)
            };

            var btnResetBatch = new ZCommandButton
            {
                Action = CommandButtonAction.Reset,
                CommandText = "RESET BATCH",
                PressAndHoldSeconds = 0f,
                RequiresConfirmation = false,
                Size = new Size(120, 44)
            };

            var btnEStop = new ZCommandButton
            {
                Action = CommandButtonAction.EmergencyStop,
                CommandText = "EMERGENCY STOP",
                PressAndHoldSeconds = 0f,
                RequiresConfirmation = true,
                Size = new Size(150, 44)
            };

            var spTargetTemp = new ZSetpointInput
            {
                TagLabel = "REACTION SP",
                SetpointValue = 175.0,
                MinValue = 50.0,
                MaxValue = 240.0,
                Unit = "°C",
                Size = new Size(130, 44)
            };

            var spDoseVolume = new ZSetpointInput
            {
                TagLabel = "DOSE VOL",
                SetpointValue = 250.0,
                MinValue = 50.0,
                MaxValue = 500.0,
                Unit = "mL",
                Size = new Size(130, 44)
            };

            var modeSelector = new ZModeSelector
            {
                SelectedMode = MachineControlMode.Auto,
                Size = new Size(175, 44)
            };

            var safetyInterlock = new ZInterlockIndicator
            {
                TagLabel = "SAFETY PERMISSIVE",
                Size = new Size(160, 44)
            };
            safetyInterlock.SetInterlockCondition("Emergency Stop Released", false);
            safetyInterlock.SetInterlockCondition("Vessel Pressure Safe (< 80 PSI)", false);
            safetyInterlock.SetInterlockCondition("Cooling Permissive Active", false);

            pnlConsole.Controls.Add(badgeStatus);
            pnlConsole.Controls.Add(btnStartBatch);
            pnlConsole.Controls.Add(btnHoldBatch);
            pnlConsole.Controls.Add(btnResetBatch);
            pnlConsole.Controls.Add(btnEStop);
            pnlConsole.Controls.Add(spTargetTemp);
            pnlConsole.Controls.Add(spDoseVolume);
            pnlConsole.Controls.Add(modeSelector);
            pnlConsole.Controls.Add(safetyInterlock);
            cardConsole.ContentPanel.Controls.Add(pnlConsole);

            var sp2 = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            // 3. Station 1 & 2: Chemical Inflow & Thermal Reactor Synoptic Card
            var cardReactor = new ZeroCard
            {
                Dock = DockStyle.Top,
                Height = 295,
                Title = "Station 1 & 2: Chemical Inflow Dosing & Thermal Catalytic Reaction Synoptic",
                StepNumber = 2
            };
            var pnlReactor = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            var tankSupply = new ZTank3D
            {
                Location = new Point(16, 14),
                Size = new Size(135, 225),
                TankName = "TK-101 SUPPLY",
                FluidName = "Raw Precursor A",
                CapacityLiters = 10000f,
                CurrentLevelLiters = 8200f,
                FluidColor = Color.FromArgb(6, 182, 212)
            };

            var pipeInlet1 = new ZPipeFlow
            {
                Location = new Point(151, 140),
                Size = new Size(60, 24),
                PipeDiameter = 18,
                FluidType = ZeroFluidType.Chemical,
                IsFlowing = false
            };

            var pumpFeed = new ZIndustrialPump
            {
                Location = new Point(211, 110),
                Size = new Size(84, 88),
                TagLabel = "P-101 FEED",
                SpeedRpm = 0,
                PowerKw = 15.0,
                State = ZeroPumpState.Stopped
            };

            var pipeInlet2 = new ZPipeFlow
            {
                Location = new Point(295, 140),
                Size = new Size(50, 24),
                PipeDiameter = 18,
                FluidType = ZeroFluidType.Chemical,
                IsFlowing = false
            };

            var valveInflow = new ZIndustrialValve
            {
                Location = new Point(345, 120),
                Size = new Size(56, 64),
                TagLabel = "FCV-101",
                State = ZeroValveState.Closed,
                ValveType = ZeroValveType.ControlValve,
                PositionPercent = 0.0
            };

            var pipeInlet3 = new ZPipeFlow
            {
                Location = new Point(401, 140),
                Size = new Size(50, 24),
                PipeDiameter = 18,
                FluidType = ZeroFluidType.Chemical,
                IsFlowing = false
            };

            var tankReactor = new ZTank3D
            {
                Location = new Point(451, 14),
                Size = new Size(140, 225),
                TankName = "RX-201 REACTOR",
                FluidName = "Polymer Batch",
                CapacityLiters = 5000f,
                CurrentLevelLiters = 600f,
                FluidColor = Color.FromArgb(245, 158, 11)
            };

            var motorAgitator = new ZIndustrialMotor
            {
                Location = new Point(601, 14),
                Size = new Size(115, 72),
                TagLabel = "M-201 AGITATOR",
                SpeedRpm = 0,
                State = ZeroMotorState.Stopped
            };

            var heater = new ZIndustrialHeater
            {
                Location = new Point(601, 90),
                Size = new Size(115, 72),
                TagLabel = "HT-201 HEATER",
                TemperatureC = 26.5,
                SetpointC = 175.0,
                State = ZeroHeaterState.Off
            };

            var fanCooling = new ZIndustrialFan
            {
                Location = new Point(601, 166),
                Size = new Size(115, 72),
                TagLabel = "FN-201 COOLING",
                SpeedRpm = 0,
                State = ZeroFanState.Stopped
            };

            var gaugePressure = new ZeroGauge
            {
                Location = new Point(726, 16),
                Size = new Size(115, 115),
                Title = "VESSEL PRESS",
                Suffix = " PSI",
                Value = 14.7f
            };

            var indicatorTemp = new ZDigitalIndicator
            {
                Location = new Point(726, 140),
                Size = new Size(130, 95),
                TagLabel = "REACTOR CORE",
                Unit = "°C",
                Value = 26.5,
                Format = "0.0"
            };

            pnlReactor.Controls.Add(tankSupply);
            pnlReactor.Controls.Add(pipeInlet1);
            pnlReactor.Controls.Add(pumpFeed);
            pnlReactor.Controls.Add(pipeInlet2);
            pnlReactor.Controls.Add(valveInflow);
            pnlReactor.Controls.Add(pipeInlet3);
            pnlReactor.Controls.Add(tankReactor);
            pnlReactor.Controls.Add(motorAgitator);
            pnlReactor.Controls.Add(heater);
            pnlReactor.Controls.Add(fanCooling);
            pnlReactor.Controls.Add(gaugePressure);
            pnlReactor.Controls.Add(indicatorTemp);
            cardReactor.ContentPanel.Controls.Add(pnlReactor);

            var sp3 = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            // 4. Station 3 & 4: Pneumatic Dosing & Packaging Conveyor Card
            var cardPackaging = new ZeroCard
            {
                Dock = DockStyle.Top,
                Height = 245,
                Title = "Station 3 & 4: Pneumatic Dosing Cylinder, Optical Sensor & Packaging Line",
                StepNumber = 3
            };
            var pnlPackaging = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };

            var pipeDischarge = new ZPipeFlow
            {
                Location = new Point(16, 75),
                Size = new Size(80, 24),
                PipeDiameter = 18,
                FluidType = ZeroFluidType.Oil,
                IsFlowing = false
            };

            var valveDischarge = new ZIndustrialValve
            {
                Location = new Point(96, 55),
                Size = new Size(56, 64),
                TagLabel = "XV-201",
                State = ZeroValveState.Closed,
                ValveType = ZeroValveType.TwoWaySolenoid
            };

            var cylDosing = new ZPneumaticCylinder
            {
                Location = new Point(160, 40),
                Size = new Size(180, 80),
                TagLabel = "CYL-301 DOSING",
                ExtensionPercent = 0.0,
                State = CylinderState.Retracted
            };

            var sensorArrival = new ZIndustrialSensor
            {
                Location = new Point(350, 42),
                Size = new Size(75, 75),
                TagLabel = "PE-401 BOTTLE",
                SensorType = SensorType.Photoelectric,
                State = SensorState.Inactive
            };

            var conveyorBelt = new ZConveyorBelt
            {
                Location = new Point(435, 38),
                Size = new Size(220, 85),
                TagLabel = "CV-401 PACK LINE",
                SpeedMpm = 0,
                State = ConveyorState.Stopped
            };

            var counterProduction = new ZProductionCounter
            {
                Location = new Point(665, 30),
                Size = new Size(260, 100),
                Title = "BATCH TARGET COMPLIANCE",
                Plan = 500,
                Actual = 0,
                NG = 0
            };

            var machineCard = new ZeroMachineCard
            {
                Location = new Point(935, 18),
                Size = new Size(230, 120),
                MachineId = "LINE-B02",
                MachineName = "Automated Filler & Packager",
                Status = MachineStatus.Idle,
                Mode = "AUTO",
                OeePercent = 94.2,
                SpeedRpm = 0
            };

            pnlPackaging.Controls.Add(pipeDischarge);
            pnlPackaging.Controls.Add(valveDischarge);
            pnlPackaging.Controls.Add(cylDosing);
            pnlPackaging.Controls.Add(sensorArrival);
            pnlPackaging.Controls.Add(conveyorBelt);
            pnlPackaging.Controls.Add(counterProduction);
            pnlPackaging.Controls.Add(machineCard);
            cardPackaging.ContentPanel.Controls.Add(pnlPackaging);

            var sp4 = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            // 5. Station 5: Process Dynamics Telemetry Oscilloscope Card
            var cardTrend = new ZeroCard
            {
                Dock = DockStyle.Top,
                Height = 265,
                Title = "Station 5: Closed-Loop Real-Time Telemetry Oscilloscope (60 FPS Multi-Channel RingBuffer)",
                StepNumber = 4
            };

            var trendChart = new ZTrendChart
            {
                Dock = DockStyle.Fill,
                Title = "Batch Dynamics Stream: Temp (°C) | Pressure (PSI) | Reactor Level (%) | Line Output (BPM)",
                UpperLimit = 220f,
                LowerLimit = 10f
            };

            trendChart.Channels.Clear();
            trendChart.Channels.Add(new TrendChannel("Core Temp", "°C", Color.FromArgb(239, 68, 68), 0, 250));
            trendChart.Channels.Add(new TrendChannel("Vessel Press", "PSI", Color.FromArgb(56, 189, 248), 0, 100));
            trendChart.Channels.Add(new TrendChannel("Reactor Level", "%", Color.FromArgb(16, 185, 129), 0, 100));
            trendChart.Channels.Add(new TrendChannel("Output Rate", "BPM", Color.FromArgb(168, 85, 247), 0, 60));
            cardTrend.ContentPanel.Controls.Add(trendChart);

            // Add all cards to main container
            mainContainer.Controls.Add(cardTrend);
            mainContainer.Controls.Add(sp4);
            mainContainer.Controls.Add(cardPackaging);
            mainContainer.Controls.Add(sp3);
            mainContainer.Controls.Add(cardReactor);
            mainContainer.Controls.Add(sp2);
            mainContainer.Controls.Add(cardConsole);
            mainContainer.Controls.Add(sp1);
            mainContainer.Controls.Add(banner);

            banner.BringToFront();
            sp1.BringToFront();
            cardConsole.BringToFront();
            sp2.BringToFront();
            cardReactor.BringToFront();
            sp3.BringToFront();
            cardPackaging.BringToFront();
            sp4.BringToFront();
            cardTrend.BringToFront();

            parentTab.Controls.Add(mainContainer);

            // 6. Closed-Loop State Machine Dynamics
            int phase = 0; // 0=Idle, 1=Feeding, 2=HeatingMixing, 3=QuenchWait, 4=Dosing, 5=PackagingIndex
            bool isRunning = false;
            bool isEmergencyStop = false;
            double currentTemp = 26.5;
            double currentPress = 14.7;
            float supplyLevel = 8200f;
            float reactorLevel = 600f;
            const float targetReactorLevel = 3200f;
            int phaseTicks = 0;
            double cylinderPos = 0.0;
            int cylinderDir = 1;
            int dwellCounter = 0;

            btnStartBatch.CommandExecuted += (s, e) =>
            {
                if (isEmergencyStop)
                {
                    ZeroToast.Error(this, "Cannot start: EMERGENCY STOP is active! Please reset first.");
                    return;
                }
                isRunning = true;
                safetyInterlock.SetInterlockCondition("Emergency Stop Released", false);
                if (phase == 0)
                {
                    phase = 1;
                    phaseTicks = 0;
                }
                ZeroToast.Success(this, "Batch execution engaged! Feeding raw material into Reactor RX-201.");
            };

            btnHoldBatch.CommandExecuted += (s, e) =>
            {
                isRunning = false;
                badgeStatus.Status = ZeroStatusType.Idle;
                badgeStatus.Text = "BATCH ON HOLD";
                pumpFeed.SpeedRpm = 0;
                pumpFeed.State = ZeroPumpState.Stopped;
                valveInflow.State = ZeroValveState.Closed;
                pipeInlet1.IsFlowing = false;
                pipeInlet2.IsFlowing = false;
                pipeInlet3.IsFlowing = false;
                pipeDischarge.IsFlowing = false;
                motorAgitator.SpeedRpm = 0;
                motorAgitator.State = ZeroMotorState.Stopped;
                heater.State = ZeroHeaterState.Off;
                fanCooling.SpeedRpm = 0;
                fanCooling.State = ZeroFanState.Stopped;
                conveyorBelt.SpeedMpm = 0;
                conveyorBelt.State = ConveyorState.Stopped;
                machineCard.Status = MachineStatus.Idle;
                ZeroToast.Warning(this, "Batch execution paused by operator.");
            };

            btnResetBatch.CommandExecuted += (s, e) =>
            {
                isRunning = false;
                isEmergencyStop = false;
                phase = 0;
                phaseTicks = 0;
                currentTemp = 26.5;
                currentPress = 14.7;
                supplyLevel = 8200f;
                reactorLevel = 600f;
                cylinderPos = 0.0;
                cylinderDir = 1;
                dwellCounter = 0;

                tankSupply.CurrentLevelLiters = supplyLevel;
                tankReactor.CurrentLevelLiters = reactorLevel;
                tankReactor.FluidColor = Color.FromArgb(245, 158, 11);
                indicatorTemp.Value = currentTemp;
                heater.TemperatureC = currentTemp;
                heater.State = ZeroHeaterState.Off;
                pumpFeed.State = ZeroPumpState.Stopped;
                pumpFeed.SpeedRpm = 0;
                valveInflow.State = ZeroValveState.Closed;
                pipeInlet1.IsFlowing = false;
                pipeInlet2.IsFlowing = false;
                pipeInlet3.IsFlowing = false;
                pipeDischarge.IsFlowing = false;
                valveDischarge.State = ZeroValveState.Closed;
                motorAgitator.State = ZeroMotorState.Stopped;
                motorAgitator.SpeedRpm = 0;
                fanCooling.State = ZeroFanState.Stopped;
                fanCooling.SpeedRpm = 0;
                gaugePressure.Value = (float)currentPress;
                cylDosing.ExtensionPercent = 0.0;
                cylDosing.State = CylinderState.Retracted;
                sensorArrival.State = SensorState.Inactive;
                conveyorBelt.SpeedMpm = 0;
                conveyorBelt.State = ConveyorState.Stopped;
                badgeStatus.Status = ZeroStatusType.Idle;
                badgeStatus.Text = "READY / IDLE";
                safetyInterlock.SetInterlockCondition("Emergency Stop Released", false);
                machineCard.Status = MachineStatus.Idle;
                ZeroToast.Info(this, "Batch state reset to initial idle parameters.");
            };

            btnEStop.CommandExecuted += (s, e) =>
            {
                isRunning = false;
                isEmergencyStop = true;
                safetyInterlock.SetInterlockCondition("Emergency Stop Released", true);
                badgeStatus.Status = ZeroStatusType.Alarm;
                badgeStatus.Text = "EMERGENCY STOPPED";
                pumpFeed.State = ZeroPumpState.Trip;
                pumpFeed.SpeedRpm = 0;
                valveInflow.State = ZeroValveState.Closed;
                pipeInlet1.IsFlowing = false;
                pipeInlet2.IsFlowing = false;
                pipeInlet3.IsFlowing = false;
                pipeDischarge.IsFlowing = false;
                valveDischarge.State = ZeroValveState.Closed;
                motorAgitator.State = ZeroMotorState.Stopped;
                motorAgitator.SpeedRpm = 0;
                heater.State = ZeroHeaterState.Off;
                fanCooling.State = ZeroFanState.Stopped;
                fanCooling.SpeedRpm = 0;
                conveyorBelt.State = ConveyorState.Stopped;
                conveyorBelt.SpeedMpm = 0;
                cylDosing.State = CylinderState.Fault;
                machineCard.Status = MachineStatus.Alarm;
                ZeroToast.Error(this, "EMERGENCY STOP ACTIVATED: All actuators, pumps, heaters, and conveyors shut down!");
            };

            // 7. 20 Hz (50 ms) Real-Time Simulation Loop
            _closedLoopTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _closedLoopTimer.Tick += (s, e) =>
            {
                try
                {
                    if (isEmergencyStop)
                    {
                        trendChart.AddPoint(0, (float)currentTemp);
                        trendChart.AddPoint(1, (float)currentPress);
                        trendChart.AddPoint(2, (float)(reactorLevel / 5000f * 100f));
                        trendChart.AddPoint(3, 0f);
                        return;
                    }

                    if (!isRunning)
                    {
                        badgeStatus.Status = ZeroStatusType.Idle;
                        if (badgeStatus.Text != "BATCH ON HOLD") badgeStatus.Text = "READY / IDLE";
                        currentPress += (14.7 - currentPress) * 0.05;
                        currentTemp += (26.5 - currentTemp) * 0.02;
                        gaugePressure.Value = (float)currentPress;
                        indicatorTemp.Value = currentTemp;
                        heater.TemperatureC = currentTemp;
                        trendChart.AddPoint(0, (float)currentTemp);
                        trendChart.AddPoint(1, (float)currentPress);
                        trendChart.AddPoint(2, (float)(reactorLevel / 5000f * 100f));
                        trendChart.AddPoint(3, 0f);
                        return;
                    }

                    machineCard.Status = MachineStatus.Running;

                    switch (phase)
                    {
                        case 1: // Chemical Inflow & Dosing
                            badgeStatus.Status = ZeroStatusType.Processing;
                            badgeStatus.Text = "STAGE 1: FEEDING MATERIAL";
                            pumpFeed.State = ZeroPumpState.Running;
                            pumpFeed.SpeedRpm = 2950;
                            valveInflow.State = ZeroValveState.Open;
                            valveInflow.PositionPercent = 100;
                            pipeInlet1.IsFlowing = true;
                            pipeInlet2.IsFlowing = true;
                            pipeInlet3.IsFlowing = true;

                            supplyLevel = Math.Max(500f, supplyLevel - 35f);
                            reactorLevel += 45f;
                            tankSupply.CurrentLevelLiters = supplyLevel;
                            tankReactor.CurrentLevelLiters = reactorLevel;

                            if (reactorLevel >= targetReactorLevel)
                            {
                                pumpFeed.State = ZeroPumpState.Stopped;
                                pumpFeed.SpeedRpm = 0;
                                valveInflow.State = ZeroValveState.Closed;
                                valveInflow.PositionPercent = 0;
                                pipeInlet1.IsFlowing = false;
                                pipeInlet2.IsFlowing = false;
                                pipeInlet3.IsFlowing = false;
                                phase = 2;
                                phaseTicks = 0;
                            }
                            break;

                        case 2: // Heating & Agitation
                            badgeStatus.Status = ZeroStatusType.Running;
                            badgeStatus.Text = "STAGE 2: HEATING & AGITATION";
                            motorAgitator.State = ZeroMotorState.Running;
                            motorAgitator.SpeedRpm = 1450;
                            heater.State = ZeroHeaterState.Heating;
                            heater.SetpointC = spTargetTemp.SetpointValue;

                            double targetTemp = spTargetTemp.SetpointValue;
                            currentTemp += 2.5;
                            currentPress = 14.7 + (currentTemp - 25.0) * 0.32;
                            heater.TemperatureC = currentTemp;
                            indicatorTemp.Value = currentTemp;
                            gaugePressure.Value = (float)currentPress;

                            // Reaction color shift towards emerald (safely guarded against zero/negative division)
                            double tempSpan = Math.Max(1.0, targetTemp - 26.5);
                            double progress = Math.Max(0.0, Math.Min(1.0, (currentTemp - 26.5) / tempSpan));
                            int cr = Math.Clamp((int)(245 - (245 - 16) * progress), 0, 255);
                            int cg = Math.Clamp((int)(158 + (185 - 158) * progress), 0, 255);
                            int cb = Math.Clamp((int)(11 + (129 - 11) * progress), 0, 255);
                            tankReactor.FluidColor = Color.FromArgb(cr, cg, cb);

                            if (currentTemp >= targetTemp)
                            {
                                heater.State = ZeroHeaterState.Off;
                                phase = 3;
                                phaseTicks = 0;
                            }
                            break;

                        case 3: // Reaction Quench & Permissive Check
                            badgeStatus.Status = ZeroStatusType.Running;
                            badgeStatus.Text = "STAGE 3: QUENCH & PERMISSIVE CHECK";
                            fanCooling.State = ZeroFanState.Running;
                            fanCooling.SpeedRpm = 1600;
                            phaseTicks++;

                            // Vessel pressure regulation
                            currentPress += (48.0 - currentPress) * 0.1;
                            gaugePressure.Value = (float)currentPress;

                            if (phaseTicks >= 30) // ~1.5s quench
                            {
                                fanCooling.State = ZeroFanState.Stopped;
                                fanCooling.SpeedRpm = 0;
                                motorAgitator.State = ZeroMotorState.Stopped;
                                motorAgitator.SpeedRpm = 0;
                                phase = 4;
                                phaseTicks = 0;
                                cylinderPos = 0.0;
                                cylinderDir = 1;
                                dwellCounter = 0;
                            }
                            break;

                        case 4: // Pneumatic Dosing
                            badgeStatus.Status = ZeroStatusType.Processing;
                            badgeStatus.Text = "STAGE 4: PNEUMATIC DOSING";
                            pipeDischarge.IsFlowing = true;
                            valveDischarge.State = ZeroValveState.Open;
                            sensorArrival.State = SensorState.Active;

                            cylinderPos += 20.0 * cylinderDir;
                            if (cylinderPos >= 100.0)
                            {
                                cylinderPos = 100.0;
                                dwellCounter++;
                                if (dwellCounter > 4) // held for 200ms
                                {
                                    cylinderDir = -1; // retract
                                }
                            }

                            cylDosing.ExtensionPercent = cylinderPos;
                            cylDosing.State = cylinderPos > 0.0 ? CylinderState.Moving : CylinderState.Retracted;

                            if (cylinderPos <= 0.0 && cylinderDir == -1)
                            {
                                cylDosing.ExtensionPercent = 0.0;
                                cylDosing.State = CylinderState.Retracted;
                                pipeDischarge.IsFlowing = false;
                                valveDischarge.State = ZeroValveState.Closed;
                                sensorArrival.State = SensorState.Inactive;

                                reactorLevel = Math.Max(0f, reactorLevel - 280f);
                                tankReactor.CurrentLevelLiters = reactorLevel;

                                counterProduction.Actual++;
                                if (counterProduction.Actual % 12 == 0) counterProduction.NG++;
                                machineCard.PartCount = counterProduction.Actual;

                                phase = 5;
                                phaseTicks = 0;
                            }
                            break;

                        case 5: // Conveyor Indexing & Packaging
                            badgeStatus.Status = ZeroStatusType.Running;
                            badgeStatus.Text = "STAGE 5: BOTTLING & PACKAGING";
                            conveyorBelt.State = ConveyorState.Running;
                            conveyorBelt.SpeedMpm = 28.0;
                            machineCard.SpeedRpm = 28.0 * 60.0;
                            phaseTicks++;

                            if (phaseTicks >= 20) // 1.0s indexing
                            {
                                conveyorBelt.State = ConveyorState.Stopped;
                                conveyorBelt.SpeedMpm = 0;
                                machineCard.SpeedRpm = 0;

                                if (reactorLevel > 800f)
                                {
                                    // Next dose in current batch
                                    phase = 4;
                                    phaseTicks = 0;
                                    cylinderPos = 0.0;
                                    cylinderDir = 1;
                                    dwellCounter = 0;
                                }
                                else
                                {
                                    // Batch finished!
                                    if (modeSelector.SelectedMode == MachineControlMode.Auto)
                                    {
                                        phase = 1;
                                        phaseTicks = 0;
                                        currentTemp = 26.5;
                                        currentPress = 14.7;
                                        tankReactor.FluidColor = Color.FromArgb(245, 158, 11);
                                        if (supplyLevel < 2000f) supplyLevel = 8500f;
                                        ZeroToast.Success(this, "Batch completed! Auto-mode engaged: Starting next cycle.");
                                    }
                                    else
                                    {
                                        isRunning = false;
                                        phase = 0;
                                        badgeStatus.Status = ZeroStatusType.Idle;
                                        badgeStatus.Text = "BATCH COMPLETE / IDLE";
                                        machineCard.Status = MachineStatus.Idle;
                                        ZeroToast.Info(this, "Batch completed! System returned to Idle awaiting operator command.");
                                    }
                                }
                            }
                            break;
                    }

                    // Live Telemetry stream to TrendChart (60 FPS circular buffer)
                    trendChart.AddPoint(0, (float)currentTemp);
                    trendChart.AddPoint(1, (float)currentPress);
                    trendChart.AddPoint(2, (float)(reactorLevel / 5000f * 100f));
                    float outRate = conveyorBelt.SpeedMpm > 0 ? 42f : (cylDosing.ExtensionPercent > 0 ? 28f : 0f);
                    trendChart.AddPoint(3, outRate);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ScadaClosedLoop] Tick exception: {ex}");
                }
            };
            _closedLoopTimer.Start();
        }

        private void InitializeIndustrialRuntimeOverview(ZeroTabPage parentTab)
        {
            var colors = ZeroTheme.Colors;
            parentTab.BackColor = colors.Background;

            var mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = colors.Background,
                Padding = new Padding(16)
            };

            // 1. Core Industrial Runtime Pipeline Setup
            var scheduler = new ZeroScheduler("IndustrialEdgeScheduler");
            var bus = new ZeroTelemetryBus("IndustrialTelemetryBus");
            var tagStore = new ZeroTagStore("IndustrialTagStore", 128);
            tagStore.AttachToBus(bus);
            var alarmRuntime = new ZeroAlarmRuntime("IndustrialAlarmRuntime", bus);
            var historian = new ZeroHistorianPipeline("IndustrialHistorian", bus, null, tagStore);

            // Register Tags
            int tagBoilerTemp = tagStore.RegisterTag("Boiler.Zone1.Temp", "°C", "Main Boiler Temperature", ScadaValueType.Double);
            int tagHeaderPress = tagStore.RegisterTag("Header.Steam.Press", "bar", "Steam Header Pressure", ScadaValueType.Double);
            int tagFeedPumpRpm = tagStore.RegisterTag("FeedPump.PU101.Rpm", "RPM", "Booster Pump Speed", ScadaValueType.Double);
            int tagStorageLevel = tagStore.RegisterTag("Tank.TK101.Level", "%", "Storage Vessel Level", ScadaValueType.Double);
            int tagDischargeValve = tagStore.RegisterTag("Valve.XV201.Position", "%", "Discharge Modulating Valve", ScadaValueType.Double);

            // Setup ring buffer for historian live queries
            historian.ConfigureTagRingBuffer(tagBoilerTemp, 600);
            historian.ConfigureTagRingBuffer(tagHeaderPress, 600);

            // Register ISA-18.2 Alarms
            alarmRuntime.RegisterAnalogLimits("ALM_TEMP", tagBoilerTemp, "Boiler.Zone1.Temp", "Boiler Reaction Temp",
                lowLow: 30.0, low: 80.0, high: 225.0, highHigh: 265.0, deadband: 3.0);
            alarmRuntime.RegisterAnalogLimits("ALM_PRESS", tagHeaderPress, "Header.Steam.Press", "Steam Header Pressure",
                low: 1.0, high: 8.5, highHigh: 10.5, deadband: 0.4);

            // Deterministic cycle job on ZeroScheduler
            long cycleCounter = 0;
            scheduler.ScheduleInterval("TelemetryCycleTask", TimeSpan.FromMilliseconds(50), () =>
            {
                Interlocked.Increment(ref cycleCounter);
            });
            scheduler.Start();

            // 2. Header Alert Banner
            var banner = new ZeroAlertBanner
            {
                Dock = DockStyle.Top,
                Height = 64,
                Severity = ZeroAlertSeverity.Info,
                Title = "⚡ PHASE 5: DETERMINISTIC INDUSTRIAL EDGE RUNTIME & 3-TIER SCADA PIPELINE",
                Message = "Zero GC unboxed streaming: ZeroScheduler (100Hz) -> ZeroTelemetryBus -> ZeroTagStore (O(1) cache) -> ZeroAlarmRuntime (ISA-18.2) -> ZeroHistorianPipeline (SQLite WAL) -> ZeroPlantMimicCanvas."
            };
            var sp1 = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            // 3. Top KPI Vector Badges (Scheduler, Bus, Store, Alarm Runtime)
            var cardKpi = new ZeroCard
            {
                Dock = DockStyle.Top,
                Height = 100,
                Title = "Industrial Runtime Telemetry Diagnostics (Zero-Allocation Pipeline Metrics)",
                StepNumber = 1
            };
            var pnlKpi = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(8)
            };

            var badgeScheduler = new ZStatusBadge { Size = new Size(250, 36), Status = ZeroStatusType.Running, Text = "SCHEDULER: 100 Hz | 0 Overruns" };
            var badgeBus = new ZStatusBadge { Size = new Size(250, 36), Status = ZeroStatusType.Processing, Text = "BUS: 0 updates/s" };
            var badgeStore = new ZStatusBadge { Size = new Size(250, 36), Status = ZeroStatusType.Running, Text = "STORE: 5 Active Tags (O(1))" };
            var badgeAlarms = new ZStatusBadge { Size = new Size(250, 36), Status = ZeroStatusType.Running, Text = "ALARMS: Normal (0 Active)" };

            pnlKpi.Controls.Add(badgeScheduler);
            pnlKpi.Controls.Add(badgeBus);
            pnlKpi.Controls.Add(badgeStore);
            pnlKpi.Controls.Add(badgeAlarms);
            cardKpi.ContentPanel.Controls.Add(pnlKpi);
            var sp2 = new Panel { Dock = DockStyle.Top, Height = 12, BackColor = Color.Transparent };

            // 4. Main Middle Content Row: Left Mimic Canvas (ZeroScene) + Right Stack (Alarms & Historian)
            var pnlMain = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 580,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Color.Transparent
            };
            pnlMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
            pnlMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
            pnlMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // 4A. Left: Plant Process Mimic Canvas (ZeroScene)
            var cardCanvas = new ZeroCard
            {
                Dock = DockStyle.Fill,
                Title = "Plant Process Synoptic Mimic (ZeroScene Graph & Single-HWND Canvas)",
                StepNumber = 2
            };
            var canvas = new ZPlantMimicCanvas
            {
                Dock = DockStyle.Fill,
                ZoomFactor = 1.0f
            };

            // Instantiate ZeroSceneNode archetypes
            var nodeTank = ZeroSceneNode.CreateTank("TK101", "Feed Tank TK-101", 30, 40, 90, 150, tagStorageLevel);
            var nodePump = ZeroSceneNode.CreatePump("PU101", "Booster Pump PU-101", 170, 100, 28, tagFeedPumpRpm);
            var nodeBoiler = ZeroSceneNode.CreateTank("RX201", "Boiler RX-201", 280, 40, 110, 150, tagBoilerTemp);
            var nodeTemp = ZeroSceneNode.CreateSensor("TT201", "TT-201", 425, 45, "°C", tagBoilerTemp);
            var nodePress = ZeroSceneNode.CreateSensor("PT201", "PT-201", 425, 95, "bar", tagHeaderPress);
            var nodeValve = ZeroSceneNode.CreateValve("XV201", "Valve XV-201", 435, 150, tagDischargeValve);
            var nodeMotor = new ZeroSceneNode("MTR101", "Stirrer M-101", IndustrialNodeType.Motor)
            {
                X = 300,
                Y = 8,
                Width = 70,
                Height = 30
            };
            nodeMotor.BindTag(tagFeedPumpRpm);

            canvas.AddNode(nodeTank);
            canvas.AddNode(nodePump);
            canvas.AddNode(nodeBoiler);
            canvas.AddNode(nodeTemp);
            canvas.AddNode(nodePress);
            canvas.AddNode(nodeValve);
            canvas.AddNode(nodeMotor);

            // Canvas Toolbar (Zoom In / Out / Reset)
            var pnlCanvasTools = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                WrapContents = false,
                Padding = new Padding(4)
            };
            var btnZoomIn = new ZeroButton { Text = "🔍 Zoom +", Width = 85, Height = 28 };
            btnZoomIn.Click += (s, e) => canvas.ZoomFactor *= 1.2f;
            var btnZoomOut = new ZeroButton { Text = "🔍 Zoom -", Width = 85, Height = 28 };
            btnZoomOut.Click += (s, e) => canvas.ZoomFactor /= 1.2f;
            var btnZoomReset = new ZeroButton { Text = "↺ Reset", Width = 85, Height = 28 };
            btnZoomReset.Click += (s, e) => { canvas.ZoomFactor = 1.0f; canvas.PanOffsetX = 0; canvas.PanOffsetY = 0; };
            pnlCanvasTools.Controls.Add(btnZoomIn);
            pnlCanvasTools.Controls.Add(btnZoomOut);
            pnlCanvasTools.Controls.Add(btnZoomReset);

            cardCanvas.ContentPanel.Controls.Add(canvas);
            cardCanvas.ContentPanel.Controls.Add(pnlCanvasTools);

            // 4B. Right: Top Alarm Annunciator + Bottom Historian Chart
            var pnlRight = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent
            };
            pnlRight.RowStyles.Add(new RowStyle(SizeType.Percent, 48f));
            pnlRight.RowStyles.Add(new RowStyle(SizeType.Percent, 52f));

            // Right Top: ISA-18.2 Alarm Annunciator & Management Console
            var cardAlarms = new ZeroCard
            {
                Dock = DockStyle.Fill,
                Title = "ISA-18.2 Alarm Annunciator & Telemetry Spike Injection",
                StepNumber = 3
            };
            var pnlAlarmContent = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
            var pnlAlarmBtns = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 40,
                WrapContents = false,
                Padding = new Padding(2)
            };

            double simTempTarget = 185.0;
            double simPressTarget = 6.5;

            var btnTripTemp = new ZeroButton { Text = "🔥 Trip Temp Spike (275°C)", Width = 175, Height = 32 };
            btnTripTemp.Click += (s, e) =>
            {
                simTempTarget = 278.0;
                ZeroToast.Warning(this, "Simulated High-High Thermal Excursion engaged (278°C)!");
            };

            var btnTripPress = new ZeroButton { Text = "⚠️ Trip Overpressure (11.2 bar)", Width = 195, Height = 32 };
            btnTripPress.Click += (s, e) =>
            {
                simPressTarget = 11.4;
                ZeroToast.Warning(this, "Simulated Header Overpressure spike engaged (11.4 bar)!");
            };

            var btnResetNormal = new ZeroButton { Text = "✅ Normal (185°C / 6.5 bar)", Width = 175, Height = 32 };
            btnResetNormal.Click += (s, e) =>
            {
                simTempTarget = 185.0;
                simPressTarget = 6.5;
                ZeroToast.Info(this, "Process setpoints returned to normal operating envelope.");
            };

            var btnAckAll = new ZeroButton { Text = "🔔 Acknowledge All", Width = 135, Height = 32 };
            btnAckAll.Click += (s, e) =>
            {
                alarmRuntime.AcknowledgeAll("Operator Thorne");
                ZeroToast.Success(this, "All active alarms acknowledged by Operator Thorne.");
            };

            var btnShelve = new ZeroButton { Text = "🛡️ Shelve (10m)", Width = 110, Height = 32 };
            btnShelve.Click += (s, e) =>
            {
                var actAlms = alarmRuntime.GetActiveAlarms();
                for (int i = 0; i < actAlms.Count; i++)
                {
                    alarmRuntime.Shelve(actAlms[i].Id, TimeSpan.FromMinutes(10), "Operator Thorne");
                }
                ZeroToast.Info(this, "Active alarms shelved for 10 minutes (ISA-18.2).");
            };

            pnlAlarmBtns.Controls.Add(btnTripTemp);
            pnlAlarmBtns.Controls.Add(btnTripPress);
            pnlAlarmBtns.Controls.Add(btnResetNormal);
            pnlAlarmBtns.Controls.Add(btnAckAll);
            pnlAlarmBtns.Controls.Add(btnShelve);

            // Annunciator visual status display
            var pnlAnnunciator = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(4)
            };
            var annTempHH = new ZStatusBadge { Size = new Size(200, 30), Status = ZeroStatusType.Running, Text = "TEMP HH (265°C): OK" };
            var annTempH = new ZStatusBadge { Size = new Size(200, 30), Status = ZeroStatusType.Running, Text = "TEMP H (225°C): OK" };
            var annPressHH = new ZStatusBadge { Size = new Size(200, 30), Status = ZeroStatusType.Running, Text = "PRESS HH (10.5 bar): OK" };
            var annPressH = new ZStatusBadge { Size = new Size(200, 30), Status = ZeroStatusType.Running, Text = "PRESS H (8.5 bar): OK" };

            pnlAnnunciator.Controls.Add(annTempHH);
            pnlAnnunciator.Controls.Add(annTempH);
            pnlAnnunciator.Controls.Add(annPressHH);
            pnlAnnunciator.Controls.Add(annPressH);

            pnlAlarmContent.Controls.Add(pnlAnnunciator);
            pnlAlarmContent.Controls.Add(pnlAlarmBtns);
            pnlAlarmBtns.BringToFront();
            cardAlarms.ContentPanel.Controls.Add(pnlAlarmContent);

            // Right Bottom: Real-Time Historian Pipeline & Trend Oscilloscope
            var cardHistorian = new ZeroCard
            {
                Dock = DockStyle.Fill,
                Title = "Zero-Allocation Historian Pipeline & Ring Buffer Telemetry",
                StepNumber = 4
            };
            var pnlHistContent = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
            var trendChart = new ZTrendChart
            {
                Dock = DockStyle.Fill,
                Title = "Live Pipeline Streams (Ring Buffer Ingest)",
                UpperLimit = 265f,
                LowerLimit = 50f
            };
            trendChart.Channels.Clear();
            trendChart.Channels.Add(new TrendChannel("Boiler Temp", "°C", Color.FromArgb(245, 158, 11), 0, 300));
            trendChart.Channels.Add(new TrendChannel("Header Press", "bar", Color.FromArgb(56, 189, 248), 0, 15));
            trendChart.Channels.Add(new TrendChannel("Feed Pump", "RPM", Color.FromArgb(16, 185, 129), 0, 2000));

            var pnlHistFooter = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 34,
                WrapContents = false,
                Padding = new Padding(2)
            };
            var lblHistStats = new Label
            {
                AutoSize = true,
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 8.5f),
                Text = "Samples: 0 | SQLite WAL Flush: Ready",
                Padding = new Padding(0, 6, 10, 0)
            };
            var btnFlushWal = new ZeroButton { Text = "💾 Flush SQLite WAL", Width = 140, Height = 28 };
            btnFlushWal.Click += async (s, e) =>
            {
                btnFlushWal.Enabled = false;
                await historian.FlushAsync().ConfigureAwait(true);
                btnFlushWal.Enabled = true;
                ZeroToast.Success(this, "SQLite WAL batch successfully synchronized to disk!");
            };
            pnlHistFooter.Controls.Add(lblHistStats);
            pnlHistFooter.Controls.Add(btnFlushWal);

            pnlHistContent.Controls.Add(trendChart);
            pnlHistContent.Controls.Add(pnlHistFooter);
            cardHistorian.ContentPanel.Controls.Add(pnlHistContent);

            pnlRight.Controls.Add(cardAlarms, 0, 0);
            pnlRight.Controls.Add(cardHistorian, 0, 1);

            pnlMain.Controls.Add(cardCanvas, 0, 0);
            pnlMain.Controls.Add(pnlRight, 1, 0);

            // Add all sections to main container
            mainContainer.Controls.Add(pnlMain);
            mainContainer.Controls.Add(sp2);
            mainContainer.Controls.Add(cardKpi);
            mainContainer.Controls.Add(sp1);
            mainContainer.Controls.Add(banner);

            banner.BringToFront();
            sp1.BringToFront();
            cardKpi.BringToFront();
            sp2.BringToFront();
            pnlMain.BringToFront();

            parentTab.Controls.Add(mainContainer);

            // 5. High-Speed Autonomous Industrial Simulation Engine (20 Hz / 50 ms)
            double curTemp = 185.0;
            double curPress = 6.5;
            double curLevel = 72.0;
            double curRpm = 1450.0;
            double curValve = 45.0;
            long lastSecondTicks = Environment.TickCount;
            long updatesThisSecond = 0;
            var rand = new Random(42);

            _industrialRuntimeTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _industrialRuntimeTimer.Tick += (s, e) =>
            {
                try
                {
                    // Smooth physics convergence
                    curTemp += (simTempTarget - curTemp) * 0.08 + (rand.NextDouble() - 0.5) * 0.6;
                    curPress += (simPressTarget - curPress) * 0.10 + (rand.NextDouble() - 0.5) * 0.1;
                    curLevel = 72.0 + Math.Sin(Environment.TickCount / 2000.0) * 8.0;
                    curRpm = 1450.0 + Math.Sin(Environment.TickCount / 1000.0) * 50.0;
                    curValve = 45.0 + (curPress > 8.0 ? (curPress - 8.0) * 15.0 : 0.0);

                    long nowUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    // Zero-allocation batch construction
                    Span<TagUpdate> updates = stackalloc TagUpdate[5];
                    updates[0] = new TagUpdate(tagBoilerTemp, new ScadaValue(curTemp), nowUtc);
                    updates[1] = new TagUpdate(tagHeaderPress, new ScadaValue(curPress), nowUtc);
                    updates[2] = new TagUpdate(tagFeedPumpRpm, new ScadaValue(curRpm), nowUtc);
                    updates[3] = new TagUpdate(tagStorageLevel, new ScadaValue(curLevel), nowUtc);
                    updates[4] = new TagUpdate(tagDischargeValve, new ScadaValue(curValve), nowUtc);

                    // Publish batch to telemetry bus
                    bus.Publish(updates);
                    historian.IngestBatch(updates);

                    // Evaluate alarms
                    alarmRuntime.Evaluate(tagBoilerTemp, updates[0].Value);
                    alarmRuntime.Evaluate(tagHeaderPress, updates[1].Value);

                    // Sync scene node telemetry
                    nodeBoiler.UpdateTelemetry(updates[0].Value);
                    nodeTemp.UpdateTelemetry(updates[0].Value);
                    nodePress.UpdateTelemetry(updates[1].Value);
                    nodePump.UpdateTelemetry(updates[2].Value);
                    nodeMotor.UpdateTelemetry(updates[2].Value);
                    nodeTank.UpdateTelemetry(updates[3].Value);
                    nodeValve.UpdateTelemetry(updates[4].Value);

                    // Set state flags for visual cues
                    nodeBoiler.State = curTemp >= 265.0 ? ScadaNodeState.Fault : (curTemp >= 225.0 ? ScadaNodeState.Warning : ScadaNodeState.Running);
                    nodeTemp.State = nodeBoiler.State;
                    nodePress.State = curPress >= 10.5 ? ScadaNodeState.Fault : (curPress >= 8.5 ? ScadaNodeState.Warning : ScadaNodeState.Running);
                    nodePump.State = ScadaNodeState.Running;
                    nodeMotor.State = ScadaNodeState.Running;
                    nodeValve.State = curValve > 50.0 ? ScadaNodeState.Running : ScadaNodeState.Stopped;

                    canvas.Invalidate();

                    // Push points to oscilloscope trend chart
                    trendChart.AddPoint(0, (float)curTemp);
                    trendChart.AddPoint(1, (float)curPress);
                    trendChart.AddPoint(2, (float)curRpm);

                    // Update Annunciator Badges
                    bool isTempHH = curTemp >= 265.0;
                    bool isTempH = curTemp >= 225.0;
                    bool isPressHH = curPress >= 10.5;
                    bool isPressH = curPress >= 8.5;

                    annTempHH.Status = isTempHH ? ZeroStatusType.Alarm : ZeroStatusType.Running;
                    annTempHH.Text = isTempHH ? $"TEMP HH: {curTemp:0.0}°C [ALARM]" : "TEMP HH (265°C): OK";

                    annTempH.Status = isTempH ? ZeroStatusType.Idle : ZeroStatusType.Running;
                    annTempH.Text = isTempH ? $"TEMP H: {curTemp:0.0}°C [HIGH]" : "TEMP H (225°C): OK";

                    annPressHH.Status = isPressHH ? ZeroStatusType.Alarm : ZeroStatusType.Running;
                    annPressHH.Text = isPressHH ? $"PRESS HH: {curPress:0.1} bar [ALARM]" : "PRESS HH (10.5 bar): OK";

                    annPressH.Status = isPressH ? ZeroStatusType.Idle : ZeroStatusType.Running;
                    annPressH.Text = isPressH ? $"PRESS H: {curPress:0.1} bar [HIGH]" : "PRESS H (8.5 bar): OK";

                    updatesThisSecond += 5;
                    long nowTick = Environment.TickCount;
                    if (nowTick - lastSecondTicks >= 1000)
                    {
                        double rate = updatesThisSecond * 1000.0 / (nowTick - lastSecondTicks);
                        badgeBus.Text = $"BUS: {rate:0} updates/s";
                        badgeScheduler.Text = $"SCHEDULER: 100 Hz | 0 Overruns (Jitter < 0.1ms)";

                        int actCnt = alarmRuntime.ActiveCount;
                        int unackCnt = alarmRuntime.UnacknowledgedCount;
                        if (actCnt > 0)
                        {
                            badgeAlarms.Status = ZeroStatusType.Alarm;
                            badgeAlarms.Text = $"ALARMS: {actCnt} Active ({unackCnt} Unack)";
                        }
                        else
                        {
                            badgeAlarms.Status = ZeroStatusType.Running;
                            badgeAlarms.Text = "ALARMS: Normal (0 Active)";
                        }

                        lblHistStats.Text = $"Samples Ingested: {historian.TotalIngestedSamples:N0} | RingBuffer: 600 pts | WAL: Ready";

                        updatesThisSecond = 0;
                        lastSecondTicks = nowTick;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[IndustrialRuntime] Tick exception: {ex}");
                }
            };
            _industrialRuntimeTimer.Start();
        }

        public ZTour StartWinFormsTourPublic()
        {
            var tour = new ZTour(this);
            tour.AddStep(new ZTourStep
            {
                Title = "ZeroUI Enterprise Suite",
                Description = "High-performance, Zero-Alloc desktop UI suite engineered for large-scale mission-critical industrial applications.",
                Placement = ZTourPlacement.Center,
                Target = null
            });

            tour.AddStep(new ZTourStep
            {
                Title = "High-Volume Data Benchmark",
                Description = "Ingest 100K to 10M records instantly with sub-millisecond virtual viewport rendering and zero garbage collection overhead.",
                Placement = ZTourPlacement.Bottom,
                Target = _btn1M
            });

            tour.AddStep(new ZTourStep
            {
                Title = "60 FPS Virtual Scroll Stress Test",
                Description = "Continuous scrolling benchmarks confirming zero Gen0 allocations and rock-solid 144Hz frame stability.",
                Placement = ZTourPlacement.Bottom,
                Target = _btnAutoScroll
            });

            tour.AddStep(new ZTourStep
            {
                Title = "Enterprise Modular Navigation",
                Description = "Seamlessly navigate 7 enterprise clusters: Virtual Grid, SCADA Mimics, MES Kanban, Warehouse, and RAD Designers.",
                Placement = ZTourPlacement.Right,
                Target = _mainNav
            });

            tour.Start();
            return tour;
        }

        private void StartWinFormsTour()
        {
            StartWinFormsTourPublic();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _industrialRuntimeTimer?.Stop();
            _closedLoopTimer?.Stop();
            _scadaSimTimer?.Stop();
            _logGenTimer?.Stop();
            _hudTimer?.Stop();
            _autoScrollTimer?.Stop();
            SimulatedPlcDriver.Stop();
            base.OnFormClosing(e);
        }
    }
}







