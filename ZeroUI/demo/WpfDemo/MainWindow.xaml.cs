using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;
using ZeroUI.Core.Input.Date;
using ZeroUI.Core.Scene;
using ZeroUI.Core.Theme;
using ZeroUI.Core.Signal;
using ZeroUI.Core.Memory;
using ZeroUI.Core.Automation;
using ZeroUI.Core.Pivot;
using ZeroUI.Core.Range;
using ZeroUI.Demo.Data;
using ZeroUI.Wpf.Range;
using ZeroUI.Wpf.Charts.Model;
using ZeroUI.Wpf.Editors;
using ZeroUI.Wpf.Industrial;
using ZeroUI.Wpf.Docking;
using ZeroUI.Wpf.Diagram;
using ZeroUI.Wpf.DataGrid;
using ZeroUI.Wpf.Charts;
using ZeroUI.Wpf.Navigation;
using ZeroUI.Wpf.Overlays;
using ZeroUI.Wpf.Reporting;
using ZeroUI.Wpf.Feedback;
using ZeroUI.Wpf.Theme;
using ZeroUI.Core.Scada.Safety;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Scada;
using ZeroUI.Core.Notification;
using ZeroUI.Wpf.Rendering;
using ZeroUI.Wpf.Layout;

namespace ZeroUI.Samples.WpfDemo
{
    public partial class MainWindow : Window
    {
        private InventorySource? _inventorySource;
        private DispatcherTimer? _telemetryTimer;
        private DispatcherTimer? _scadaSimTimer;
        private bool _isSimulating = true;
        private System.Threading.CancellationTokenSource? _filterCts;

        // Telemetry tracking
        private int _frameCount = 0;
        private Stopwatch _fpsStopwatch = Stopwatch.StartNew();
        private double _currentFps = 0.0;
        private int _initialGc0 = 0;
        private int _initialGc1 = 0;
        private int _initialGc2 = 0;

        // SCADA Simulation State
        private double _simRpm = 1840;
        private double _simPressure = 145;
        private int _simTaktSeconds = 265;
        private int _simCycles = 1842;
        private bool _isScopePaused = false;
        private double _scopeAngle = 0.0;

        public MainWindow()
        {
            InitializeComponent();
            SetupNavigationRail();
            SetupColumns();
            SetupCharts();
            SetupTelemetry();
            SetupScadaSimulation();
            SetupSafetyDemo();
            SetupEnterpriseControls();
            SetupPivotGrid();
            SetupGanttChart();
            SetupDockAndDiagram();
            SetupSignalScope();
            SetupHexInspector();
            SetupStateExecutor();
            SetupEnterpriseCommercialSuite();
            SetupDesignToolsDemo();
            SetupSecurityDemo();
            SetupAiOcrDemo();

            CompositionTarget.Rendering += OnCompositionRendering;

            // Setup Skin Selector & Studio
            RefreshSkinSelector();
            ZeroSkinManager.SkinChanged += skin =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (ComboSkinSelector.SelectedItem != skin)
                    {
                        ComboSkinSelector.SelectedItem = skin;
                    }
                    ApplySkin(skin);
                });
            };
            ZeroSkinManager.RegistryChanged += () =>
            {
                Dispatcher.Invoke(RefreshSkinSelector);
            };

            // Load initial 100k records for instant wow factor
            LoadData(100000);
            ApplySkin(ZeroSkinManager.CurrentSkin);

            // Populate Radio Group options
            RadioDispatchMode.Items = new[] { "Standard JIT Dispatch", "High-Priority Hotlot", "Hold for Quality Audit" };
            RadioSamplingTier.Items = new[] { "Level I (10%)", "Level II (Normal 100%)", "Level III (Tightened)" };

            // Populate Segmented options
            SegPeriod.Items = new[] { "Real-Time 1s", "Shift 8h", "Daily", "Weekly", "Quarterly" };
            SegFilter.Items = new[] { "All Units", "Running", "Degraded", "Faulted" };

            // Setup DatePickers
            DatePickerStart.SelectedDate = DateTime.Today;
            DatePickerEnd.SelectedDate = DateTime.Today.AddDays(14);

            // Setup DateRangePicker
            DemoDateRangePicker.SetRange(DateTime.Today.AddDays(-7), DateTime.Today);

            // Populate Lookup with 5,000 industrial items
            var lookupItems = new List<LookUpItem>(5000);
            for (int i = 1; i <= 5000; i++)
            {
                lookupItems.Add(new LookUpItem(
                    key: $"AST-{i:D5}",
                    displayText: $"Transducer Transmitter PT-{i:D4}",
                    subText: $"Building {((i % 5) + 1)} • Line {((char)('A' + (i % 6)))} • Modbus ID {i % 254 + 1}",
                    category: i % 10 == 0 ? "Calibrated" : (i % 7 == 0 ? "Inspect" : "Active")
                ));
            }
            DemoAssetLookup.SetItems(lookupItems);

            // Populate TokenEdit autocomplete suggestions
            DemoNodeTokenEdit.AvailableTokens = new[]
            {
                "PLC-01", "PLC-02", "Modbus-Gateway", "HighPriority", "LowLatency",
                "OPC-UA", "SCADA-Master", "LineA", "LineB", "SafetyInterlock", "ISA-95"
            };

            // Setup SCADA Plant Mimic Scene
            var plantScene = new ZeroScene();
            var tk1 = ZeroSceneNode.CreateTank("TK-101", "Raw Chemical TK-101", 60, 50, 95, 140);
            tk1.Value = 76.5;
            tk1.State = ScadaNodeState.Running;
            plantScene.AddNode(tk1);

            var tk2 = ZeroSceneNode.CreateTank("TK-102", "Catalyst Mix TK-102", 320, 50, 95, 140);
            tk2.Value = 44.0;
            tk2.State = ScadaNodeState.Running;
            plantScene.AddNode(tk2);

            var tk3 = ZeroSceneNode.CreateTank("TK-103", "Finished Yield TK-103", 580, 50, 95, 140);
            tk3.Value = 89.2;
            tk3.State = ScadaNodeState.Warning;
            plantScene.AddNode(tk3);

            var p1 = ZeroSceneNode.CreatePump("P-101A", "Primary Feed P-101A", 205, 105, 24);
            p1.Value = 1450;
            p1.State = ScadaNodeState.Running;
            plantScene.AddNode(p1);

            var p2 = ZeroSceneNode.CreatePump("P-102A", "Transfer Pump P-102A", 465, 105, 24);
            p2.Value = 1780;
            p2.State = ScadaNodeState.Running;
            plantScene.AddNode(p2);

            var v1 = ZeroSceneNode.CreateValve("XV-101", "Inlet Valve XV-101", 210, 65);
            v1.State = ScadaNodeState.Running;
            plantScene.AddNode(v1);

            var v2 = ZeroSceneNode.CreateValve("XV-102", "Transfer Valve XV-102", 470, 65);
            v2.State = ScadaNodeState.Running;
            plantScene.AddNode(v2);

            var tt1 = ZeroSceneNode.CreateSensor("TT-101", "Tank Temp 68.5°C", 72, 210, "°C");
            tt1.Value = 68.5;
            plantScene.AddNode(tt1);

            var pt2 = ZeroSceneNode.CreateSensor("PT-102", "Line Press 4.2 Bar", 332, 210, "Bar");
            pt2.Value = 4.2;
            plantScene.AddNode(pt2);

            PlantCanvas.Scene = plantScene;

            // Setup Recirculation Header Polyline Pipeline
            RecircPipeFlow.FluidType = ZeroFluidType.CoolingWater;
            RecircPipeFlow.SetPoints(new[]
            {
                new Point(30, 20),
                new Point(220, 20),
                new Point(220, 75),
                new Point(450, 75),
                new Point(450, 25),
                new Point(680, 25),
                new Point(680, 85),
                new Point(920, 85)
            });

            // Register Windows Action Center Notification Bridge
            WindowsNotificationBridge.RegisterMainWindow(this);
            ComboDeliveryMode.ItemsSource = Enum.GetValues(typeof(ZeroNotificationDeliveryMode));
            ComboDeliveryMode.SelectedItem = ToastStackManager.DeliveryMode;
            ChkRouteAlarmsToSystem.IsChecked = ToastStackManager.RouteAlarmsToSystem;
        }

        private void RefreshSkinSelector()
        {
            var cur = ZeroSkinManager.CurrentSkin;
            ComboSkinSelector.ItemsSource = null;
            ComboSkinSelector.ItemsSource = ZeroSkinManager.AvailableSkins;
            ComboSkinSelector.SelectedItem = cur;
        }

        private void BtnSkinStudio_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SkinStudioDialog { Owner = this };
            dialog.ShowDialog();
        }

        private void SetupNavigationRail()
        {
            MainNavRail.Items.Clear();
            MainNavRail.Items.Add(new SideNavItem("c1", "Big Data & Grids", "⚡", "DATA & BENCHMARK", 3));
            MainNavRail.Items.Add(new SideNavItem("c2", "Industrial Verticals", "🏭", "VERTICAL DOMAINS", 7));
            MainNavRail.Items.Add(new SideNavItem("c3", "SCADA & Edge", "⚙️", "AUTOMATION & SCADA", 5));
            MainNavRail.Items.Add(new SideNavItem("c4", "Network & Infra", "🌐", "AUTOMATION & SCADA", 6));
            MainNavRail.Items.Add(new SideNavItem("c5", "MES Smart Factory", "📦", "MANUFACTURING & OPS", 2));
            MainNavRail.Items.Add(new SideNavItem("c6", "Analytics & Signals", "📊", "ANALYTICS & DIAGNOSTICS", 4));
            MainNavRail.Items.Add(new SideNavItem("c7", "Form Editors & UI", "🎨", "UI TOOLKIT & CONTROLS", 5));
            MainNavRail.Items.Add(new SideNavItem("c8", "AI & Vision Studio", "🤖", "AI & COMPUTER VISION", 2));

            MainNavRail.SelectedIndex = 0;
            MainNavRail.ItemSelected += (s, e) =>
            {
                if (MainTabs.SelectedIndex != e.Index && e.Index >= 0 && e.Index < MainTabs.Items.Count)
                {
                    MainTabs.SelectedIndex = e.Index;
                }
            };

            MainTabs.SelectionChanged += (s, e) =>
            {
                if (e.Source == MainTabs && MainNavRail.SelectedIndex != MainTabs.SelectedIndex && MainTabs.SelectedIndex >= 0)
                {
                    MainNavRail.SelectedIndex = MainTabs.SelectedIndex;
                }
            };
        }

        private void SetupColumns()
        {
            VirtualGrid.Columns.Clear();

            var colActive = new ZeroColumn("Active", 65, CellAlignment.Center)
            {
                ColumnType = GridColumnType.Boolean,
                Summary = SummaryType.Count,
                SummaryFormat = "{0:N0} total"
            };
            var colCategory = new ZeroColumn("Category", 145, CellAlignment.Left) { AllowGrouping = true };
            var colId = new ZeroColumn("ID", 75, CellAlignment.Right) { ReadOnly = true, IsPinned = true };
            var colCode = new ZeroColumn("Material Code", 125, CellAlignment.Left) { ReadOnly = true, IsPinned = true };
            var colName = new ZeroColumn("Description / Component Name", 230, CellAlignment.Left);
            var colQty = new ZeroColumn("Quantity", 90, CellAlignment.Right)
            {
                ColumnType = GridColumnType.Numeric,
                Summary = SummaryType.Sum,
                SummaryFormat = "{0:N0}",
                CustomValidator = val =>
                {
                    if (int.TryParse(val.Replace(",", ""), out int q) && q >= 0 && q <= 100000)
                        return (true, null);
                    return (false, "Quantity must be an integer between 0 and 100,000");
                }
            };
            var colPrice = new ZeroColumn("Unit Price ($)", 115, CellAlignment.Right)
            {
                ColumnType = GridColumnType.Numeric,
                Summary = SummaryType.Average,
                SummaryFormat = "Avg: ${0:N2}",
                CustomValidator = val =>
                {
                    if (double.TryParse(val.Replace(",", ""), out double p) && p >= 0)
                        return (true, null);
                    return (false, "Unit Price must be non-negative");
                }
            };
            var colTotal = new ZeroColumn("Total Amount ($)", 135, CellAlignment.Right) { ReadOnly = true, Summary = SummaryType.Sum, SummaryFormat = "${0:N2}" };
            var colYield = new ZeroColumn("Yield %", 120, CellAlignment.Center)
            {
                ColumnType = GridColumnType.Numeric,
                Summary = SummaryType.Average,
                SummaryFormat = "Avg: {0:P0}"
            };
            var colLot = new ZeroColumn("Lot Number", 120, CellAlignment.Center)
            {
                ColumnType = GridColumnType.Masked,
                Mask = "LOT-000000",
                CustomValidator = val =>
                {
                    if (string.IsNullOrWhiteSpace(val) || val.Contains("_"))
                        return (false, "Lot number must have 6 digits (e.g. LOT-202601)");
                    return (true, null);
                }
            };
            var colStatus = new ZeroColumn("Inspection Status", 135, CellAlignment.Center);

            VirtualGrid.Columns.Add(colActive);
            VirtualGrid.Columns.Add(colCategory);
            VirtualGrid.Columns.Add(colId);
            VirtualGrid.Columns.Add(colCode);
            VirtualGrid.Columns.Add(colName);
            VirtualGrid.Columns.Add(colQty);
            VirtualGrid.Columns.Add(colPrice);
            VirtualGrid.Columns.Add(colTotal);
            VirtualGrid.Columns.Add(colYield);
            VirtualGrid.Columns.Add(colLot);
            VirtualGrid.Columns.Add(colStatus);

            VirtualGrid.ShowFooter = true;
            VirtualGrid.ShowGroupPanel = true;
            VirtualGrid.SelectionMode = ZeroGridSelectionMode.MultiRow;

            // Enterprise Group Summaries
            VirtualGrid.GroupSummaries.Add(new GroupSummaryItem(-1, GroupSummaryType.Count, null, "Items"));
            VirtualGrid.GroupSummaries.Add(new GroupSummaryItem(5, GroupSummaryType.Sum, "{0:N0}", "Total Qty"));
            VirtualGrid.GroupSummaries.Add(new GroupSummaryItem(6, GroupSummaryType.Average, "${0:N2}", "Avg Price"));
            VirtualGrid.GroupSummaries.Add(new GroupSummaryItem(7, GroupSummaryType.Sum, "${0:N2}", "Total Amount"));

            // Conditional Formatting Rule: Highlight high quantity
            VirtualGrid.ConditionalRules.Add(new ConditionalFormattingRule(
                columnIndex: 5,
                op: ConditionOperator.GreaterThan,
                value1: 400,
                backColor: 0x4010B981,
                textColor: 0xFF34D399
            ));

            VirtualGrid.CellValueChanged += (s, args) =>
            {
                TxtLatency.Text = $"Cell edited: [{args.VisualRowIndex},{args.ColumnIndex}] = \"{args.NewValue}\"";
            };

            VirtualGrid.SelectionChanged += (s, e) =>
            {
                if (MainDetailDrawer.IsOpen)
                {
                    UpdateWpfDrawerSelection();
                }
            };

            VirtualGrid.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    UpdateWpfDrawerSelection();
                    MainDetailDrawer.Open();
                }
            };

            // Header sort click
            VirtualGrid.ColumnHeaderClicked += async (s, colIdx) =>
            {
                await VirtualGrid.SortByColumnAsync(colIdx);
            };

            VirtualGrid.SortingStarted += (s, e) =>
            {
                TxtLatency.Text = "Sorting... ⏳";
            };

            VirtualGrid.SortingCompleted += (s, elapsed) =>
            {
                TxtLatency.Text = $"{elapsed.TotalMilliseconds:0.#} ms (Sort)";
            };

            // Search Bar wireup
            GridSearch.DensityChanged += (s, density) => VirtualGrid.Density = density;
            GridSearch.SearchTriggered += (s, query) => FilterData(query);
            GridSearch.ExportTriggered += (s, e) =>
            {
                MessageBox.Show($"Exported {VirtualGrid.IndexMap.ActiveCount:N0} rows to CSV successfully!", "ZeroUI Export", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            // Pagination wireup
            GridPager.PageSizeChanged += (s, size) => VirtualGrid.InvalidateVisual();
        }

        private async void LoadData(int count)
        {
            if (count >= 10_000_000)
            {
                BtnLoad10M_Click(this, new RoutedEventArgs());
                return;
            }

            var sw = Stopwatch.StartNew();
            TxtLatency.Text = "Generating...";
            var items = await Task.Run(() => InventorySource.Generate(count));
            _inventorySource = new InventorySource(items);
            VirtualGrid.DataSource = _inventorySource;
            sw.Stop();

            TxtCapacity.Text = $"{count:N0} Rows";
            GridSearch.SetMatchCount(count, count);
            GridPager.UpdateTotalRecords(count);

            _initialGc0 = GC.CollectionCount(0);
            _initialGc1 = GC.CollectionCount(1);
            _initialGc2 = GC.CollectionCount(2);

            TxtLatency.Text = $"{sw.ElapsedMilliseconds} ms (Init)";
        }

        private async void FilterData(string query)
        {
            _filterCts?.Cancel();
            _filterCts = new System.Threading.CancellationTokenSource();
            var token = _filterCts.Token;

            var ds = VirtualGrid.DataSource;
            if (ds == null) return;

            int total = ds.TotalRowCount;
            if (string.IsNullOrWhiteSpace(query))
            {
                VirtualGrid.IndexMap.ResetIdentity(total);
                GridSearch.SetMatchCount(total, total);
                GridPager.UpdateTotalRecords(total);
                VirtualGrid.ClearRowSelection();
                VirtualGrid.InvalidateVisual();
                return;
            }

            try
            {
                var matches = new List<int>();
                if (_inventorySource != null)
                {
                    var items = _inventorySource.Items;
                    await Task.Run(() =>
                    {
                        for (int i = 0; i < items.Length; i++)
                        {
                            if ((i & 0x7FFF) == 0 && token.IsCancellationRequested) return;
                            if (items[i].ItemName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                items[i].ItemCode.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                items[i].Category.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                items[i].LotNumber.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                matches.Add(i);
                            }
                        }
                    }, token);
                }
                else
                {
                    await Task.Run(() =>
                    {
                        CellValueBuffer buf = new CellValueBuffer();
                        int limit = Math.Min(total, 500_000);
                        for (int i = 0; i < limit && matches.Count < 50_000; i++)
                        {
                            if ((i & 0x7FFF) == 0 && token.IsCancellationRequested) return;
                            buf.Reset();
                            ds.GetCellValue(i, 1, ref buf);
                            if (buf.Text.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                matches.Add(i);
                                continue;
                            }
                            buf.Reset();
                            ds.GetCellValue(i, 2, ref buf);
                            if (buf.Text.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                matches.Add(i);
                            }
                        }
                    }, token);
                }

                if (!token.IsCancellationRequested)
                {
                    int matchCount = matches.Count;
                    int[] buffer = matches.ToArray();
                    VirtualGrid.IndexMap.SetUnderlyingBuffer(buffer, matchCount);
                    GridSearch.SetMatchCount(matchCount, total);
                    GridPager.UpdateTotalRecords(matchCount);
                    VirtualGrid.ClearRowSelection();
                    VirtualGrid.InvalidateVisual();
                }
            }
            catch (OperationCanceledException) { }
        }

        private void SetupTelemetry()
        {
            _telemetryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _telemetryTimer.Tick += (s, e) =>
            {
                TxtFps.Text = $"{_currentFps:0} FPS";

                int cur0 = GC.CollectionCount(0);
                int cur1 = GC.CollectionCount(1);
                int cur2 = GC.CollectionCount(2);
                int delta0 = cur0 - _initialGc0;
                int delta1 = cur1 - _initialGc1;
                int delta2 = cur2 - _initialGc2;

                TxtGc.Text = $"{delta0} / {delta1} / {delta2}";

                long ramBytes = Process.GetCurrentProcess().WorkingSet64;
                TxtRam.Text = $"{ramBytes / (1024.0 * 1024.0):0.#} MB";
            };
            _telemetryTimer.Start();
        }

        private int _scopeStep = 0;

        private void OnCompositionRendering(object? sender, EventArgs e)
        {
            _frameCount++;
            if (_fpsStopwatch.ElapsedMilliseconds >= 500)
            {
                _currentFps = (_frameCount * 1000.0) / _fpsStopwatch.ElapsedMilliseconds;
                _frameCount = 0;
                _fpsStopwatch.Restart();

                if (DemoD3DCanvas != null && DemoD3DCanvas.IsRenderingActive && TxtD3DFps != null)
                {
                    TxtD3DFps.Text = $"FPS: {DemoD3DCanvas.CurrentFps:F1}";
                }
            }

            // High-Speed Real-Time Oscilloscope Streaming (60 - 144 FPS)
            if (DemoSignalScope != null && DemoSignalScope.IsVisible && DemoSignalScope.Channels.Count >= 4 && !_isScopePaused)
            {
                for (int s = 0; s < 40; s++)
                {
                    _scopeAngle += 0.04;
                    float ch1 = (float)(Math.Sin(_scopeAngle) * 3.3 + (Random.Shared.NextDouble() - 0.5) * 0.15);
                    float ch2 = (float)(Math.Sin(_scopeAngle * 2.8) * 1.8 + Math.Cos(_scopeAngle * 6.5) * 0.6);
                    float ch3 = (_scopeStep % 160 < 80) ? 1.0f : 0.0f;
                    float ch4 = (_scopeStep % 60 < 30) ? 1.0f : 0.0f;
                    _scopeStep++;

                    DemoSignalScope.Channels[0].Buffer.Write(ch1);
                    DemoSignalScope.Channels[1].Buffer.Write(ch2);
                    DemoSignalScope.Channels[2].Buffer.Write(ch3);
                    DemoSignalScope.Channels[3].Buffer.Write(ch4);
                }
                DemoSignalScope.InvalidateVisual();
            }

            // Enterprise Switch Faceplate 60-144 FPS LED Animation
            if (DemoSwitchFaceplate != null && DemoSwitchFaceplate.IsVisible)
            {
                DemoSwitchFaceplate.InvalidateVisual();
            }
        }

        private void SetupScadaSimulation()
        {
            var rand = new Random();
            _scadaSimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _scadaSimTimer.Tick += (s, e) =>
            {
                if (!_isSimulating) return;

                // Turbine speed random walk
                _simRpm += (rand.NextDouble() - 0.49) * 35;
                if (_simRpm < 1200) _simRpm = 1200;
                if (_simRpm > 3400) _simRpm = 3400;
                SpindleGauge.Value = _simRpm;

                // Hydraulic pressure
                _simPressure += (rand.NextDouble() - 0.5) * 4;
                _simPressure = Math.Max(90, Math.Min(230, _simPressure));
                PressureGauge.Value = _simPressure;

                // Takt Timer
                if (rand.Next(0, 10) == 0)
                {
                    _simTaktSeconds--;
                    if (_simTaktSeconds <= 0)
                    {
                        _simTaktSeconds = 300;
                        _simCycles++;
                        CycleCounterDisplay.ValueText = $"{_simCycles}";
                    }
                    int min = _simTaktSeconds / 60;
                    int sec = _simTaktSeconds % 60;
                    TaktTimerDisplay.ValueText = $"{min:00}:{sec:00}";
                }

                // Random yellow warning alert on andon tower
                if (_simRpm > 2900)
                {
                    AndonTower.YellowOn = true;
                    AndonTower.RedBlink = true;
                }
                else
                {
                    AndonTower.YellowOn = false;
                    AndonTower.RedBlink = false;
                }

                // Dynamic packet burst traffic fluctuations for switch faceplate
                if (DemoSwitchFaceplate != null && DemoSwitchFaceplate.IsVisible)
                {
                    var ports = DemoSwitchFaceplate.Layout.Ports;
                    if (ports.Count > 0)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            int pIdx = rand.Next(0, ports.Count);
                            var p = ports[pIdx];
                            if (p.IsLinkUp)
                            {
                                p.RxBytesPerSec = rand.NextDouble() > 0.15 ? rand.Next(1024 * 10, 1024 * 1024 * 80) : 0;
                                p.TxBytesPerSec = rand.NextDouble() > 0.20 ? rand.Next(1024 * 5, 1024 * 1024 * 40) : 0;
                            }
                        }
                    }
                }
            };
            _scadaSimTimer.Start();
        }

        private void SetupCharts()
        {
            // 1. Bar Chart
            var salesSeries = new ChartSeries("Sales", Color.FromRgb(129, 140, 248));
            var targetSeries = new ChartSeries("Target", Color.FromRgb(166, 227, 161));
            string[] months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            double[] sales = new[] { 45.0, 52.0, 58.0, 64.0, 78.0, 85.0, 92.0, 88.0, 95.0, 104.0, 115.0, 128.0 };

            for (int i = 0; i < months.Length; i++)
            {
                salesSeries.AddPoint(months[i], sales[i]);
            }
            BarChart.Series.Add(salesSeries);

            // 2. Area Spline Chart
            var workloadSeries = new ChartSeries("CPU %", Color.FromRgb(243, 139, 168));
            double[] loads = new double[] { 18, 22, 25, 42, 68, 75, 82, 80, 71, 65, 54, 48, 52, 60, 78, 88, 92, 85, 62, 45, 34, 28, 22, 19 };
            for (int i = 0; i < loads.Length; i++)
            {
                workloadSeries.AddPoint($"{i}:00", loads[i]);
            }
            AreaChart.Series.Add(workloadSeries);

            // 3. Candlestick Chart
            var now = DateTime.Now.Date.AddDays(-30);
            double basePrice = 180.0;
            var rand = new Random(42);
            for (int i = 0; i < 30; i++)
            {
                double open = basePrice + (rand.NextDouble() - 0.48) * 8;
                double close = open + (rand.NextDouble() - 0.48) * 10;
                double high = Math.Max(open, close) + rand.NextDouble() * 5;
                double low = Math.Min(open, close) - rand.NextDouble() * 5;
                basePrice = close;
                CandleChart.CandleData.Add(new CandlePoint(now.AddDays(i), open, high, low, close, 15000));
            }

            // 4. Donut Chart
            var donutSeries = new ChartSeries("Inventory", Color.FromRgb(129, 140, 248));
            donutSeries.AddPoint("SMT Active (42%)", 420);
            donutSeries.AddPoint("Warehouse A (28%)", 280);
            donutSeries.AddPoint("QC Quarantine (12%)", 120);
            donutSeries.AddPoint("WIP Processing (10%)", 100);
            donutSeries.AddPoint("Safety Reserve (8%)", 80);
            DonutChart.Series.Add(donutSeries);

            // 5. Statistical & Relational Charts Suite
            SetupStatisticalCharts();
        }

        private void SetupStatisticalCharts()
        {
            if (DemoHistogramChart != null)
            {
                DemoHistogramChart.Title = "Component Tolerance Distribution (Sturges Bins)";
                DemoHistogramChart.XAxisTitle = "Dimension Offset (μm)";
                DemoHistogramChart.YAxisTitle = "Frequency Count";
                DemoHistogramChart.BinCount = 12;
                DemoHistogramChart.ShowNormalCurve = true;
                DemoHistogramChart.LoadSampleData();
            }

            if (DemoScatterChart != null)
            {
                DemoScatterChart.Title = "Torque vs. Tensile Yield Regression (OLS)";
                DemoScatterChart.XAxisTitle = "RPM (x100)";
                DemoScatterChart.YAxisTitle = "Yield Stress (MPa)";
                DemoScatterChart.ShowRegressionLine = true;
                DemoScatterChart.LoadSampleData();
            }

            if (DemoSunburstChart != null)
            {
                DemoSunburstChart.Title = "Global Corporate Capital & Supply Chain";
                DemoSunburstChart.InnerHoleRatio = 0.25;
                DemoSunburstChart.LoadSampleData();
            }

            if (DemoLollipopChart != null)
            {
                DemoLollipopChart.Title = "Regional Supply Chain Fulfillment Ranking";
                DemoLollipopChart.Orientation = ZeroUI.Core.Analytics.LollipopOrientation.Horizontal;
                DemoLollipopChart.ShowValueLabels = true;
                DemoLollipopChart.LoadSampleData();
            }
        }

        private void BtnReloadStatsDemo_Click(object sender, RoutedEventArgs e)
        {
            SetupStatisticalCharts();
            ToastNotification.Info(this, "Regenerated sample datasets for Histogram, Scatter, Sunburst, and Lollipop charts.");
        }

        private void ComboSkinSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboSkinSelector.SelectedItem is ZeroSkin skin && skin != ZeroSkinManager.CurrentSkin)
            {
                ZeroSkinManager.ApplySkin(skin);
            }
        }

        private void ApplySkin(ZeroSkin skin)
        {
            RootGrid.Background = ZeroWpfTheme.BgPrimary;
            HeaderBar.Background = ZeroWpfTheme.BgCard;
            HeaderBar.BorderBrush = ZeroWpfTheme.BorderDefault;
            HudBorder.Background = ZeroWpfTheme.BgCard;
            HudBorder.BorderBrush = ZeroWpfTheme.BorderDefault;

            BtnSkinStudio.Background = ZeroWpfTheme.PrimaryAccent;
            BtnSkinStudio.Foreground = ZeroWpfTheme.SelectionForeground;

            BtnAbout.Background = ZeroWpfTheme.BgInput;
            BtnAbout.Foreground = ZeroWpfTheme.TextPrimary;

            BtnLoad100K.Background = ZeroWpfTheme.BgInput;
            BtnLoad100K.Foreground = ZeroWpfTheme.TextPrimary;

            BtnLoad1M.Background = ZeroWpfTheme.PrimaryAccent;
            BtnLoad1M.Foreground = ZeroWpfTheme.SelectionForeground;

            BtnLoad10M.Background = ZeroWpfTheme.SecondaryAccent;
            BtnLoad10M.Foreground = ZeroWpfTheme.SelectionForeground;

            BtnGroupCategory.Background = ZeroWpfTheme.BgInput;
            BtnGroupCategory.Foreground = ZeroWpfTheme.TextPrimary;

            BtnClearGrouping.Background = ZeroWpfTheme.BgInput;
            BtnClearGrouping.Foreground = ZeroWpfTheme.TextPrimary;

            BtnToggleExpand.Background = ZeroWpfTheme.BgInput;
            BtnToggleExpand.Foreground = ZeroWpfTheme.TextPrimary;

            BtnSortPrice.Background = ZeroWpfTheme.BgInput;
            BtnSortPrice.Foreground = ZeroWpfTheme.TextPrimary;

            BtnSortQuantity.Background = ZeroWpfTheme.BgInput;
            BtnSortQuantity.Foreground = ZeroWpfTheme.TextPrimary;

            BtnTogglePinning.Background = ZeroWpfTheme.BgInput;
            BtnTogglePinning.Foreground = ZeroWpfTheme.TextPrimary;

            BtnToggleFooter.Background = ZeroWpfTheme.BgInput;
            BtnToggleFooter.Foreground = ZeroWpfTheme.TextPrimary;

            BtnLoadGenericList.Background = ZeroWpfTheme.BgInput;
            BtnLoadGenericList.Foreground = ZeroWpfTheme.TextPrimary;

            BtnClearGrid.Background = ZeroWpfTheme.BgInput;
            BtnClearGrid.Foreground = ZeroWpfTheme.TextPrimary;

            BtnToggleGroupPanel.Background = ZeroWpfTheme.BgInput;
            BtnToggleGroupPanel.Foreground = ZeroWpfTheme.TextPrimary;

            BtnToggleSummaries.Background = ZeroWpfTheme.BgInput;
            BtnToggleSummaries.Foreground = ZeroWpfTheme.TextPrimary;

            BtnRefreshPivot.Background = ZeroWpfTheme.PrimaryAccent;
            BtnRefreshPivot.Foreground = ZeroWpfTheme.SelectionForeground;

            BtnGanttZoomIn.Background = ZeroWpfTheme.BgInput;
            BtnGanttZoomIn.Foreground = ZeroWpfTheme.TextPrimary;

            BtnGanttZoomOut.Background = ZeroWpfTheme.BgInput;
            BtnGanttZoomOut.Foreground = ZeroWpfTheme.TextPrimary;

            BtnGanttAddDemo.Background = ZeroWpfTheme.PrimaryAccent;
            BtnGanttAddDemo.Foreground = ZeroWpfTheme.SelectionForeground;

            BtnAddNode.Background = ZeroWpfTheme.PrimaryAccent;
            BtnAddNode.Foreground = ZeroWpfTheme.SelectionForeground;

            BtnResetDiagram.Background = ZeroWpfTheme.BgInput;
            BtnResetDiagram.Foreground = ZeroWpfTheme.TextPrimary;

            BtnToggleScopeCursors.Background = ZeroWpfTheme.BgInput;
            BtnToggleScopeCursors.Foreground = ZeroWpfTheme.TextPrimary;

            BtnScopeHold.Background = ZeroWpfTheme.PrimaryAccent;
            BtnScopeHold.Foreground = ZeroWpfTheme.SelectionForeground;

            BtnRandomizePacket.Background = ZeroWpfTheme.PrimaryAccent;
            BtnRandomizePacket.Foreground = ZeroWpfTheme.SelectionForeground;

            BtnStateStep.Background = ZeroWpfTheme.PrimaryAccent;
            BtnStateStep.Foreground = ZeroWpfTheme.SelectionForeground;

            BtnStatePause.Background = ZeroWpfTheme.BgInput;
            BtnStatePause.Foreground = ZeroWpfTheme.TextPrimary;

            BtnStateReset.Background = ZeroWpfTheme.BgInput;
            BtnStateReset.Foreground = ZeroWpfTheme.TextPrimary;

            BtnToggleSim.Background = ZeroWpfTheme.BgInput;
            BtnToggleSim.Foreground = ZeroWpfTheme.TextPrimary;

            VirtualGrid.InvalidateVisual();
            BarChart.InvalidateVisual();
            AreaChart.InvalidateVisual();
            CandleChart.InvalidateVisual();
            DonutChart.InvalidateVisual();
            MesHeatmap.InvalidateVisual();
            MainNavRail.Refresh();
        }

        private void BtnLoad100K_Click(object sender, RoutedEventArgs e) => LoadData(100000);

        private void BtnLoad1M_Click(object sender, RoutedEventArgs e) => LoadData(1000000);

        private void BtnLoad10M_Click(object sender, RoutedEventArgs e)
        {
            var sw = Stopwatch.StartNew();
            var source = new ProceduralSource(10000000);
            _inventorySource = null;
            VirtualGrid.DataSource = source;
            sw.Stop();

            TxtCapacity.Text = "10,000,000 Rows (Limitless)";
            GridSearch.SetMatchCount(10000000, 10000000);
            GridPager.UpdateTotalRecords(10000000);

            _initialGc0 = GC.CollectionCount(0);
            _initialGc1 = GC.CollectionCount(1);
            _initialGc2 = GC.CollectionCount(2);

            TxtLatency.Text = $"{sw.ElapsedMilliseconds} ms (Init)";
        }

        private void BtnGroupCategory_Click(object sender, RoutedEventArgs e)
        {
            VirtualGrid.GroupBy(1);
            TxtLatency.Text = $"Grouped by Category ({VirtualGrid.VisualRowCount:N0} visual rows)";
        }

        private void BtnClearGrouping_Click(object sender, RoutedEventArgs e)
        {
            VirtualGrid.ClearGrouping();
            TxtLatency.Text = $"Grouping cleared ({VirtualGrid.VisualRowCount:N0} rows)";
        }

        private void BtnToggleGroupPanel_Click(object sender, RoutedEventArgs e)
        {
            VirtualGrid.ShowGroupPanel = !VirtualGrid.ShowGroupPanel;
            TxtLatency.Text = $"Group Panel {(VirtualGrid.ShowGroupPanel ? "Shown" : "Hidden")}";
        }

        private void BtnToggleSummaries_Click(object sender, RoutedEventArgs e)
        {
            if (VirtualGrid.GroupSummaries.Count > 0)
            {
                VirtualGrid.GroupSummaries.Clear();
                TxtLatency.Text = "Group summaries cleared";
            }
            else
            {
                VirtualGrid.GroupSummaries.Add(new GroupSummaryItem(-1, GroupSummaryType.Count, null, "Items"));
                VirtualGrid.GroupSummaries.Add(new GroupSummaryItem(5, GroupSummaryType.Sum, "{0:N0}", "Total Qty"));
                VirtualGrid.GroupSummaries.Add(new GroupSummaryItem(6, GroupSummaryType.Average, "${0:N2}", "Avg Price"));
                VirtualGrid.GroupSummaries.Add(new GroupSummaryItem(7, GroupSummaryType.Sum, "${0:N2}", "Total Amount"));
                TxtLatency.Text = "Group summaries enabled";
            }
            VirtualGrid.RecalculateGroupSummaries();
            VirtualGrid.InvalidateVisual();
        }

        private bool _allGroupsExpanded = true;
        private void BtnToggleExpand_Click(object sender, RoutedEventArgs e)
        {
            if (_allGroupsExpanded)
            {
                VirtualGrid.CollapseAllGroups();
                _allGroupsExpanded = false;
                BtnToggleExpand.Content = "➕ Expand All";
            }
            else
            {
                VirtualGrid.ExpandAllGroups();
                _allGroupsExpanded = true;
                BtnToggleExpand.Content = "➖ Collapse All";
            }
        }

        private async void BtnSortPrice_Click(object sender, RoutedEventArgs e)
        {
            await VirtualGrid.SortByColumnAsync(6);
        }

        private async void BtnSortQuantity_Click(object sender, RoutedEventArgs e)
        {
            await VirtualGrid.SortByColumnAsync(5);
        }

        private void BtnClearGrid_Click(object sender, RoutedEventArgs e)
        {
            VirtualGrid.DataSource = null;
            TxtCapacity.Text = "0 Rows";
            GridSearch.SetMatchCount(0, 0);
            GridPager.UpdateTotalRecords(0);
        }

        private void BtnToggleSim_Click(object sender, RoutedEventArgs e)
        {
            _isSimulating = !_isSimulating;
            BtnToggleSim.Content = _isSimulating ? "⏸ Pause Simulation" : "▶ Resume Simulation";
        }

        private void BtnTogglePinning_Click(object sender, RoutedEventArgs e)
        {
            if (VirtualGrid.Columns.Count > 3)
            {
                bool newState = !VirtualGrid.Columns[2].IsPinned;
                VirtualGrid.Columns[2].IsPinned = newState;
                VirtualGrid.Columns[3].IsPinned = newState;
                BtnTogglePinning.Content = newState ? "📌 Unpin Columns" : "📌 Pin Columns";
                VirtualGrid.InvalidateVisual();
            }
        }

        private void BtnToggleFooter_Click(object sender, RoutedEventArgs e)
        {
            VirtualGrid.ShowFooter = !VirtualGrid.ShowFooter;
            BtnToggleFooter.Content = VirtualGrid.ShowFooter ? "∑ Hide Footer" : "∑ Show Footer";
        }

        private void BtnLoadGenericList_Click(object sender, RoutedEventArgs e)
        {
            var sw = Stopwatch.StartNew();
            var list = new System.Collections.Generic.List<DemoProduct>(5000);
            for (int i = 1; i <= 5000; i++)
            {
                list.Add(new DemoProduct
                {
                    Id = i,
                    Code = $"PRD-{i:00000}",
                    Name = $"Industrial Sensor Model #{i}",
                    Quantity = 50 + (i % 200),
                    Price = 120000 + (i * 150),
                    Category = (i % 2 == 0) ? "Optics" : "Electronics",
                    InStock = (i % 5 != 0)
                });
            }

            VirtualGrid.SetDataSource(list, autoGenerateColumns: true);
            if (VirtualGrid.Columns.Count >= 5)
            {
                VirtualGrid.Columns[0].IsPinned = true;
                VirtualGrid.Columns[1].IsPinned = true;
                VirtualGrid.Columns[3].Summary = SummaryType.Sum;
                VirtualGrid.Columns[3].SummaryFormat = "{0:N0}";
                VirtualGrid.Columns[4].Summary = SummaryType.Average;
                VirtualGrid.Columns[4].SummaryFormat = "Avg: {0:N0}";
            }
            VirtualGrid.ShowFooter = true;

            _inventorySource = null;
            sw.Stop();

            TxtCapacity.Text = $"{list.Count:N0} Objects (ZeroListSource)";
            GridSearch.SetMatchCount(list.Count, list.Count);
            GridPager.UpdateTotalRecords(list.Count);
            TxtLatency.Text = $"{sw.ElapsedMilliseconds} ms (Generic List Adapter)";
        }

        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "ZeroUI ⚡ High-Performance WPF Desktop Suite\n\n" +
                "• Powered by ZeroUI.Core engine (VirtualViewport2D, RowIndexMap, PrefixSumArray).\n" +
                "• Single-Visual Direct DrawingContext rendering (0 Visual Tree overhead).\n" +
                "• In-Place Editing: Flyweight floating editor overlay with Tab/Enter commit.\n" +
                "• Fixed / Pinned Columns: Freeze columns to the left with elevation shadow.\n" +
                "• Multi-Row Selection: Multi-row select & Ctrl+C clipboard TSV copy.\n" +
                "• Summary Footer: Real-time footer aggregation (Sum, Count, Avg, Min, Max).\n" +
                "• Universal Generic Adapter: ZeroListSource<T> for IList<T>.\n" +
                "• 100% Zero-Allocation hotpaths on scroll and render.\n" +
                "• Supports both .NET Framework 4.6.2 and .NET 8.0-windows.\n\n" +
                "Developed with Deepmind Advanced Agentic Engineering.",
                "ZeroUI Architecture Overview", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnTour_Click(object sender, RoutedEventArgs e)
        {
            var tour = new ZTour(this);
            tour.AddStep(new ZTourStep
            {
                Title = "Welcome to ZeroUI!",
                Description = "Enterprise high-performance, zero-allocation desktop UI suite engineered for large-scale mission-critical applications.",
                Placement = ZTourPlacement.Center,
                Target = null
            });

            tour.AddStep(new ZTourStep
            {
                Title = "Theme & Skin Selector",
                Description = "Instantly switch visual themes (Dark, Light, Cyberpunk, High-Contrast) in real time with zero layout flicker or UI reloads.",
                Placement = ZTourPlacement.Bottom,
                Target = ComboSkinSelector
            });

            tour.AddStep(new ZTourStep
            {
                Title = "Cluster Navigation Rail",
                Description = "Seamlessly browse 7 enterprise clusters: Virtual DataGrid, Industrial SCADA, Real-time Charts, In-Memory Reporting, and Optimization Tools.",
                Placement = ZTourPlacement.Right,
                Target = MainNavRail
            });

            tour.AddStep(new ZTourStep
            {
                Title = "Diagnostics HUD",
                Description = "Toggle real-time telemetry overlay to monitor render FPS, drawing throughput, and memory consumption directly on the canvas.",
                Placement = ZTourPlacement.Bottom,
                Target = BtnToggleHUD
            });

            tour.AddStep(new ZTourStep
            {
                Title = "Drawers & Overlays",
                Description = "Explore slide-out drawers, contextual popovers, and interactive guided walkthroughs powered by the ZTour component.",
                Placement = ZTourPlacement.Bottom,
                Target = BtnDetailDrawer
            });

            await tour.StartAsync();
        }

        private bool _isBanded = false;

        private void BtnBandedHeaders_Click(object sender, RoutedEventArgs e)
        {
            _isBanded = !_isBanded;
            VirtualGrid.Bands.Clear();

            if (_isBanded && VirtualGrid.Columns.Count >= 7)
            {
                // Band 1: Master Identification (cols 0, 1)
                var bandMaster = new GridBand("📦 Product Master & Identification");
                bandMaster.AddColumn(VirtualGrid.Columns[0]);
                bandMaster.AddColumn(VirtualGrid.Columns[1]);

                // Band 2: Commercial & Inventory (cols 2, 3, 4, 5)
                var bandCommercial = new GridBand("💰 Commercial & Inventory Metrics");
                bandCommercial.AddColumn(VirtualGrid.Columns[2]);
                bandCommercial.AddColumn(VirtualGrid.Columns[3]);
                bandCommercial.AddColumn(VirtualGrid.Columns[4]);
                bandCommercial.AddColumn(VirtualGrid.Columns[5]);

                // Band 3: Categorization & Status (cols 6..)
                var bandCategory = new GridBand("🏷️ Categorization");
                for (int i = 6; i < VirtualGrid.Columns.Count; i++)
                {
                    bandCategory.AddColumn(VirtualGrid.Columns[i]);
                }

                VirtualGrid.Bands.Add(bandMaster);
                VirtualGrid.Bands.Add(bandCommercial);
                VirtualGrid.Bands.Add(bandCategory);

                if (VirtualGrid.Columns.Count > 6)
                {
                    VirtualGrid.Columns[6].AllowCellMerge = true;
                }

                BtnBandedHeaders.Content = "🏛️ Multi-Tier Bands: ON";
            }
            else
            {
                if (VirtualGrid.Columns.Count > 6)
                {
                    VirtualGrid.Columns[6].AllowCellMerge = false;
                }
                BtnBandedHeaders.Content = "🏛️ Multi-Tier Bands";
            }

            VirtualGrid.InvalidateVisual();
        }

        private void BtnBlockSelection_Click(object sender, RoutedEventArgs e)
        {
            if (VirtualGrid.SelectionMode == ZeroGridSelectionMode.Block)
            {
                VirtualGrid.SelectionMode = ZeroGridSelectionMode.SingleRow;
                BtnBlockSelection.Content = "📐 Block Marquee";
            }
            else
            {
                VirtualGrid.SelectionMode = ZeroGridSelectionMode.Block;
                BtnBlockSelection.Content = "📐 Block Marquee: ON";
            }
        }

        private void BtnExpandAllTree_Click(object sender, RoutedEventArgs e)
        {
            DemoTreeList.Model.ExpandAll();
            DemoTreeList.InvalidateVisual();
        }

        private void BtnCollapseAllTree_Click(object sender, RoutedEventArgs e)
        {
            DemoTreeList.Model.CollapseAll();
            DemoTreeList.InvalidateVisual();
        }

        private void SetupEnterpriseControls()
        {
            // Configure DemoTreeList columns
            DemoTreeList.Columns.Add(new ZeroColumn("Component / Asset", 210, CellAlignment.Left));
            DemoTreeList.Columns.Add(new ZeroColumn("Asset Tag", 85, CellAlignment.Center));
            DemoTreeList.Columns.Add(new ZeroColumn("Status", 80, CellAlignment.Center));
            DemoTreeList.Columns.Add(new ZeroColumn("OEE %", 75, CellAlignment.Right));
            DemoTreeList.Columns.Add(new ZeroColumn("Next Maintenance", 115, CellAlignment.Center));

            // Populate hierarchical model
            var model = new TreeModel();

            // Root 1: Gigafactory
            var root1 = new TreeNode("🏭 North American Gigafactory", "FAC-001", "Online", "94.2%", "2026-11-15");
            var line1 = new TreeNode("⚙️ Assembly Line 01 (Welding)", "LNE-010", "Running", "96.5%", "2026-10-01");
            line1.AddChild(new TreeNode("🦾 KUKA Titan Welder Arm", "ROB-101", "Running", "98.1%", "2026-09-20"));
            line1.AddChild(new TreeNode("🦾 Fanuc M-20iA Feed Cell", "ROB-102", "Running", "97.4%", "2026-09-28"));
            line1.AddChild(new TreeNode("📷 Cognex In-Sight 3D Vision", "CAM-105", "Running", "99.2%", "2026-12-10"));

            var line2 = new TreeNode("📦 Packaging & Case Packing", "LNE-020", "Standby", "91.8%", "2026-09-18");
            line2.AddChild(new TreeNode("🤖 Omron Delta High-Speed Robot", "ROB-201", "Standby", "93.4%", "2026-09-22"));
            line2.AddChild(new TreeNode("🌀 Automated Stretch Wrapper", "WRP-202", "Running", "95.0%", "2026-10-15"));

            root1.AddChild(line1);
            root1.AddChild(line2);
            model.AddRoot(root1);

            // Root 2: European Distribution Hub
            var root2 = new TreeNode("🌐 European Logistics Center", "FAC-002", "Online", "88.9%", "2026-10-30");
            var asrs = new TreeNode("🏗️ High-Bay ASRS Storage Bay A", "ASRS-01", "Running", "99.5%", "2026-12-01");
            asrs.AddChild(new TreeNode("🪜 Dual-Mast Crane Stacker 01", "CRN-301", "Running", "99.1%", "2026-11-10"));
            asrs.AddChild(new TreeNode("🪜 Dual-Mast Crane Stacker 02", "CRN-302", "Running", "98.7%", "2026-11-12"));

            var agvFleet = new TreeNode("🚜 Autonomous Mobile Robots (AMR)", "AMR-GRP", "Running", "86.4%", "2026-09-15");
            agvFleet.AddChild(new TreeNode("🤖 Tugger AMR Unit #04", "AMR-004", "Charging", "82.0%", "2026-09-16"));
            agvFleet.AddChild(new TreeNode("🤖 Forklift AMR Unit #09", "AMR-009", "Running", "91.5%", "2026-09-25"));

            root2.AddChild(asrs);
            root2.AddChild(agvFleet);
            model.AddRoot(root2);

            DemoTreeList.Model = model;

            // PropertyGrid initial object
            var cellConfig = new RoboticCellConfig
            {
                CellName = "KUKA Titan Heavy Welder ROB-101",
                AssetTag = "AST-ROB-101",
                ControllerIp = "192.168.1.101",
                ModbusPort = 502,
                TargetCycleTimeSec = 14.5,
                MaxPayloadKg = 1000.0,
                IsSafetyCurtainActive = true,
                AutoRestartOnClear = false,
                EmergencyStopTripped = false,
                OperatingMode = "Continuous Auto-Weld"
            };
            DemoPropertyGrid.SelectedObject = cellConfig;

            // Wire up TreeList selection to update PropertyGrid and Breadcrumb
            DemoTreeList.NodeSelected += (s, node) =>
            {
                // Build Breadcrumb path
                var pathStack = new List<string>();
                var curr = node;
                while (curr != null)
                {
                    string rawText = curr.GetValue(0);
                    int spIdx = rawText.IndexOf(' ');
                    pathStack.Insert(0, spIdx >= 0 ? rawText.Substring(spIdx + 1) : rawText);
                    curr = curr.Parent;
                }
                DemoBreadcrumb.Path = string.Join(" / ", pathStack);

                // Update PropertyGrid
                var nodeConfig = new RoboticCellConfig
                {
                    CellName = node.GetValue(0),
                    AssetTag = node.GetValue(1),
                    OperatingMode = node.GetValue(2),
                    TargetCycleTimeSec = 12.0,
                    IsSafetyCurtainActive = true
                };
                DemoPropertyGrid.SelectedObject = nodeConfig;
            };
        }

        #region Pivot Grid OLAP Setup

        private void SetupPivotGrid()
        {
            ComboPivotAgg.ItemsSource = new[] { "Sum of Revenue", "Average Revenue", "Count of Orders" };
            ComboPivotAgg.SelectedIndex = 0;
            RebuildPivotData(GroupSummaryType.Sum);
        }

        private void RebuildPivotData(GroupSummaryType summaryType)
        {
            DemoPivotGrid.Engine.RowDimensions.Clear();
            DemoPivotGrid.Engine.ColumnDimensions.Clear();
            DemoPivotGrid.Engine.Measures.Clear();

            DemoPivotGrid.Engine.RowDimensions.Add(new PivotDimension(0, "Region"));
            DemoPivotGrid.Engine.ColumnDimensions.Add(new PivotDimension(1, "Quarter"));

            string formatStr = summaryType == GroupSummaryType.Count ? "{0:N0}" : "${0:N2}";
            DemoPivotGrid.Engine.Measures.Add(new PivotMeasure(2, "Revenue", summaryType, formatStr));

            // Generate sample regional sales data
            string[] regions = { "North America", "Europe EMEA", "Asia-Pacific", "Latin America", "Middle East" };
            string[] quarters = { "Q1 2026", "Q2 2026", "Q3 2026", "Q4 2026" };

            var rawRows = new List<(string Region, string Quarter, double Amount)>();
            var rand = new Random(101);

            foreach (var reg in regions)
            {
                foreach (var qtr in quarters)
                {
                    int orders = rand.Next(10, 30);
                    for (int i = 0; i < orders; i++)
                    {
                        double amount = rand.Next(1200, 85000);
                        rawRows.Add((reg, qtr, amount));
                    }
                }
            }

            DemoPivotGrid.Engine.Compute(rawRows.Count,
                (r, field) => field == 0 ? rawRows[r].Region : (field == 1 ? rawRows[r].Quarter : rawRows[r].Amount.ToString()),
                (r, field) => field == 2 ? rawRows[r].Amount : 1.0);

            DemoPivotGrid.RefreshData();
        }

        private void ComboPivotAgg_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DemoPivotGrid == null || ComboPivotAgg == null) return;
            GroupSummaryType agg = ComboPivotAgg.SelectedIndex switch
            {
                1 => GroupSummaryType.Average,
                2 => GroupSummaryType.Count,
                _ => GroupSummaryType.Sum
            };
            RebuildPivotData(agg);
        }

        private void BtnRefreshPivot_Click(object sender, RoutedEventArgs e)
        {
            GroupSummaryType agg = ComboPivotAgg.SelectedIndex switch
            {
                1 => GroupSummaryType.Average,
                2 => GroupSummaryType.Count,
                _ => GroupSummaryType.Sum
            };
            RebuildPivotData(agg);
        }

        #endregion

        #region Gantt Chart Setup

        private void SetupGanttChart()
        {
            DemoGanttChart.ProjectStart = DateTime.Today.AddDays(-3);
            DemoGanttChart.ProjectEnd = DateTime.Today.AddDays(30);
            DemoGanttChart.PixelsPerDay = 38.0;

            var baseDate = DateTime.Today;

            var t1 = new GanttTaskItem(1, "1. Process Safety & Hazard Audit", baseDate.AddDays(-2), baseDate.AddDays(3), 1.0f, false, "HSE Team") { BarColor = 0xFF10B981 };
            var t2 = new GanttTaskItem(2, "2. SMT Stencil & Tooling Setup", baseDate.AddDays(2), baseDate.AddDays(7), 0.85f, false, "Tooling Lead") { BarColor = 0xFF3B82F6 };
            t2.PredecessorIds.Add(1);

            var t3 = new GanttTaskItem(3, "3. Pilot Lot SMT Component Placement", baseDate.AddDays(7), baseDate.AddDays(14), 0.50f, false, "Line Operator A") { BarColor = 0xFF8B5CF6 };
            t3.PredecessorIds.Add(2);

            var t4 = new GanttTaskItem(4, "4. Nitrogen Reflow Oven Thermal Profiling", baseDate.AddDays(12), baseDate.AddDays(18), 0.25f, false, "Thermal Specialist") { BarColor = 0xFFF59E0B };
            t4.PredecessorIds.Add(3);

            var t5 = new GanttTaskItem(5, "★ Factory Acceptance Gate (FAT)", baseDate.AddDays(19), baseDate.AddDays(19), 0.0f, true, "Executive Committee") { BarColor = 0xFFEF4444 };
            t5.PredecessorIds.Add(4);

            var t6 = new GanttTaskItem(6, "5. Mass Production Ramp & Packaging", baseDate.AddDays(20), baseDate.AddDays(28), 0.0f, false, "Shift Supervisor") { BarColor = 0xFF06B6D4 };
            t6.PredecessorIds.Add(5);

            DemoGanttChart.Tasks.Add(t1);
            DemoGanttChart.Tasks.Add(t2);
            DemoGanttChart.Tasks.Add(t3);
            DemoGanttChart.Tasks.Add(t4);
            DemoGanttChart.Tasks.Add(t5);
            DemoGanttChart.Tasks.Add(t6);
        }

        private void BtnGanttZoomIn_Click(object sender, RoutedEventArgs e)
        {
            DemoGanttChart.PixelsPerDay = Math.Min(100.0, DemoGanttChart.PixelsPerDay * 1.25);
        }

        private void BtnGanttZoomOut_Click(object sender, RoutedEventArgs e)
        {
            DemoGanttChart.PixelsPerDay = Math.Max(12.0, DemoGanttChart.PixelsPerDay * 0.8);
        }

        private void BtnGanttAddDemo_Click(object sender, RoutedEventArgs e)
        {
            int nextId = DemoGanttChart.Tasks.Count + 1;
            var lastTask = DemoGanttChart.Tasks.Count > 0 ? DemoGanttChart.Tasks[DemoGanttChart.Tasks.Count - 1] : null;
            DateTime start = lastTask != null ? lastTask.EndDate.AddDays(1) : DateTime.Today;
            DateTime end = start.AddDays(5);

            var newTask = new GanttTaskItem(nextId, $"Task {nextId}: Quality Burn-in & Aging", start, end, 0.1f, false, "QC Team") { BarColor = 0xFF6366F1 };
            if (lastTask != null) newTask.PredecessorIds.Add(lastTask.Id);

            DemoGanttChart.Tasks.Add(newTask);
        }

        #endregion

        #region Dock Manager & P&ID Diagram Setup

        private DiagramCanvas? _diagramCanvas;

        private void SetupDockAndDiagram()
        {
            // Left Toolbox Panel
            var leftPanel = new DockPanelControl { Title = "Toolbox & Asset Library", DockPosition = DockPosition.Left, PanelKey = "ToolboxPanel" };
            var toolboxStack = new StackPanel { Margin = new Thickness(12) };
            toolboxStack.Children.Add(new TextBlock { Text = "📐 Process Library", FontWeight = FontWeights.Bold, Foreground = ZeroWpfTheme.TextPrimary, Margin = new Thickness(0, 0, 0, 8) });
            toolboxStack.Children.Add(new TextBlock { Text = "• Primary Buffer Tank (TK-101)\n• Centrifugal Feed Pump (P-201)\n• Proportional Control Valve (XV-301)\n• Exothermic Reactor Vessel (RX-401)\n• RTD Temperature Sensor (TE-501)", Foreground = ZeroWpfTheme.TextSecondary, LineHeight = 20 });
            leftPanel.Content = toolboxStack;
            DemoDockManager.AddPanel(leftPanel);

            // Center Document: DiagramCanvas
            var centerDoc = new DockPanelControl { Title = "P&ID Process Diagram Loop", DockPosition = DockPosition.Document, PanelKey = "DiagramDoc" };
            _diagramCanvas = new DiagramCanvas();
            centerDoc.Content = _diagramCanvas;
            DemoDockManager.AddPanel(centerDoc);

            // Bottom Output Panel
            var bottomPanel = new DockPanelControl { Title = "Output & Fieldbus Telemetry", DockPosition = DockPosition.Bottom, PanelKey = "OutputPanel" };
            var outputBox = new TextBox
            {
                IsReadOnly = true,
                Background = ZeroWpfTheme.BgInput,
                Foreground = ZeroWpfTheme.TextSecondary,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11.5,
                Text = "[SYS] Process loop initialized. Baud: 115200. OPC-UA server listening on opc.tcp://10.0.1.50:4840\n[DIAG] Single-Visual Diagram engine ready. Drag nodes or connect port dots to route pipes."
            };
            bottomPanel.Content = outputBox;
            DemoDockManager.AddPanel(bottomPanel);

            // Right Properties Panel
            var rightPanel = new DockPanelControl { Title = "Node Inspector", DockPosition = DockPosition.Right, PanelKey = "InspectorPanel" };
            var rightStack = new StackPanel { Margin = new Thickness(12) };
            var lblInspector = new TextBlock { Text = "Select a diagram node to inspect parameters", TextWrapping = TextWrapping.Wrap, Foreground = ZeroWpfTheme.TextMuted };
            rightStack.Children.Add(lblInspector);
            rightPanel.Content = rightStack;
            DemoDockManager.AddPanel(rightPanel);

            // Connect diagram selection event to update inspector
            _diagramCanvas.NodeSelected += (s, node) =>
            {
                if (node != null)
                {
                    lblInspector.Text = $"Node ID: {node.Id}\nTitle: {node.Title}\nRole: {node.Subtitle}\nCoordinates: ({node.X:0}, {node.Y:0})\nPorts: {node.Ports.Count}";
                }
                else
                {
                    lblInspector.Text = "No node selected. Click a node to view properties.";
                }
            };

            ResetDiagramContent();
        }

        private void ResetDiagramContent()
        {
            if (_diagramCanvas == null) return;
            _diagramCanvas.Nodes.Clear();
            _diagramCanvas.Connections.Clear();

            var nodeTank = new DiagramNode("tank1", "Buffer Tank TK-101", "Raw Material Feed", 40, 60, Color.FromRgb(59, 130, 246), "🛢");
            var nodePump = new DiagramNode("pump1", "Feed Pump P-201", "Centrifugal 45kW", 240, 60, Color.FromRgb(16, 185, 129), "⚙");
            var nodeValve = new DiagramNode("valve1", "Pneumatic Valve XV-301", "Linear Throttle", 440, 60, Color.FromRgb(245, 158, 11), "🚰");
            var nodeReactor = new DiagramNode("reactor1", "Batch Reactor RX-401", "Exothermic Mixing", 640, 60, Color.FromRgb(239, 68, 68), "⚡");
            var nodeSensor = new DiagramNode("sensor1", "Temp Sensor TE-501", "RTD Dual Pt100", 640, 190, Color.FromRgb(139, 92, 246), "📡");

            _diagramCanvas.Nodes.Add(nodeTank);
            _diagramCanvas.Nodes.Add(nodePump);
            _diagramCanvas.Nodes.Add(nodeValve);
            _diagramCanvas.Nodes.Add(nodeReactor);
            _diagramCanvas.Nodes.Add(nodeSensor);

            _diagramCanvas.Connections.Add(new DiagramConnection("tank1", "out", "pump1", "in", "Slurry Feed"));
            _diagramCanvas.Connections.Add(new DiagramConnection("pump1", "out", "valve1", "in", "14.5 Bar"));
            _diagramCanvas.Connections.Add(new DiagramConnection("valve1", "out", "reactor1", "in", "Rate: 85 L/min"));
            _diagramCanvas.Connections.Add(new DiagramConnection("reactor1", "out", "sensor1", "in", "Thermal feedback"));
        }

        private void BtnAddNode_Click(object sender, RoutedEventArgs e)
        {
            if (_diagramCanvas == null) return;
            int count = _diagramCanvas.Nodes.Count + 1;
            var newNode = new DiagramNode($"custom{count}", $"Station #{count}", "Sub-process Unit", 180 + (count * 20), 180, Color.FromRgb(14, 165, 233), "🔄");
            _diagramCanvas.Nodes.Add(newNode);
        }

        private void BtnResetDiagram_Click(object sender, RoutedEventArgs e)
        {
            ResetDiagramContent();
        }

        private string? _savedWpfDockLayout;

        private void BtnSaveDockLayout_Click(object sender, RoutedEventArgs e)
        {
            _savedWpfDockLayout = DemoDockManager.SaveLayoutToJson();
            ToastNotification.Success(this, "Dock layout configuration saved to JSON.", 3000);
        }

        private void BtnRestoreDockLayout_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_savedWpfDockLayout))
            {
                DemoDockManager.RestoreLayoutFromJson(_savedWpfDockLayout);
                ToastNotification.Success(this, "Dock layout restored from JSON configuration.", 3000);
            }
            else
            {
                ToastNotification.Warning(this, "No saved layout state found. Please click 'Save Layout' first.", 3500);
            }
        }

        #endregion

        #region Real-Time Oscilloscope & Logic Analyzer Setup

        private void SetupSignalScope()
        {
            ComboScopeTimebase.ItemsSource = new[] { "1 ms/Div", "5 ms/Div", "10 ms/Div", "20 ms/Div", "50 ms/Div" };
            ComboScopeTimebase.SelectedIndex = 2; // 10 ms/Div

            DemoSignalScope.Channels.Clear();

            // Ch1: 50Hz Inverter Output Sine Wave (Yellow)
            var ch1 = new ScopeChannel(1, "CH1 (Inverter Phase A)", ScopeChannelType.Analog, 0xFFFACC15, 65536)
            {
                VoltsPerDiv = 1.0f,
                VerticalOffsetDiv = 1.2f,
                Unit = "V"
            };

            // Ch2: Spindle Bearing Piezo Vibration Sensor (Sky Blue)
            var ch2 = new ScopeChannel(2, "CH2 (Spindle Vibration)", ScopeChannelType.Analog, 0xFF38BDF8, 65536)
            {
                VoltsPerDiv = 1.0f,
                VerticalOffsetDiv = -1.0f,
                Unit = "g"
            };

            // Ch3: Optical Part Detection Sensor (Green logic)
            var ch3 = new ScopeChannel(3, "D0 (Optical Sensor)", ScopeChannelType.DigitalLogic, 0xFF4ADE80, 65536)
            {
                VerticalOffsetDiv = -2.6f
            };

            // Ch4: Rotary Encoder Phase A (Purple logic)
            var ch4 = new ScopeChannel(4, "D1 (Encoder Pulse)", ScopeChannelType.DigitalLogic, 0xFFA855F7, 65536)
            {
                VerticalOffsetDiv = -3.4f
            };

            // Pre-seed buffer data for immediate visual impact
            var rand = new Random(42);
            for (int i = 0; i < 2000; i++)
            {
                double a = i * 0.08;
                ch1.Buffer.Write((float)(Math.Sin(a) * 3.3 + (rand.NextDouble() - 0.5) * 0.15));
                ch2.Buffer.Write((float)(Math.Sin(a * 2.8) * 1.8 + Math.Cos(a * 6.5) * 0.6));
                ch3.Buffer.Write((i % 200 < 100) ? 1.0f : 0.0f);
                ch4.Buffer.Write((i % 40 < 20) ? 1.0f : 0.0f);
            }

            DemoSignalScope.Channels.Add(ch1);
            DemoSignalScope.Channels.Add(ch2);
            DemoSignalScope.Channels.Add(ch3);
            DemoSignalScope.Channels.Add(ch4);

            DemoSignalScope.Trigger.Mode = TriggerMode.Auto;
            DemoSignalScope.Trigger.ChannelId = 1;
            DemoSignalScope.Trigger.Threshold = 0.0f;
            DemoSignalScope.Trigger.Slope = TriggerSlope.RisingEdge;
        }

        private void ComboScopeTimebase_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DemoSignalScope == null || ComboScopeTimebase == null) return;
            double timeSec = ComboScopeTimebase.SelectedIndex switch
            {
                0 => 0.001,
                1 => 0.005,
                2 => 0.010,
                3 => 0.020,
                4 => 0.050,
                _ => 0.010
            };
            DemoSignalScope.TimePerDiv = timeSec;
        }

        private void BtnToggleScopeCursors_Click(object sender, RoutedEventArgs e)
        {
            DemoSignalScope.ShowCursors = !DemoSignalScope.ShowCursors;
        }

        private void BtnScopeHold_Click(object sender, RoutedEventArgs e)
        {
            _isScopePaused = !_isScopePaused;
            BtnScopeHold.Content = _isScopePaused ? "▶️ Resume" : "⏸️ Freeze";
        }

        #endregion

        #region OT Protocol & Hex Inspector Setup

        private void SetupHexInspector()
        {
            ComboHexPackets.ItemsSource = new[]
            {
                "Modbus TCP: Read Holding Regs (FC 03)",
                "Modbus TCP: Write Multiple Regs (FC 16)",
                "Siemens S7: Read Data Block DB1",
                "Industrial Binary Telemetry Log (512B)"
            };
            ComboHexPackets.SelectedIndex = 0;
            LoadHexPacket(0);
        }

        private void LoadHexPacket(int index)
        {
            byte[] packet;
            switch (index)
            {
                case 1: // Modbus TCP Write Multiple Registers
                    packet = new byte[]
                    {
                        0x00, 0x02,             // Transaction ID: 2
                        0x00, 0x00,             // Protocol ID: 0 (Modbus)
                        0x00, 0x0B,             // Length: 11 bytes following
                        0x01,                   // Unit ID: 1
                        0x10,                   // Function Code: 16 (0x10)
                        0x00, 0x01,             // Starting Address: 1
                        0x00, 0x02,             // Quantity of Registers: 2
                        0x04,                   // Byte Count: 4
                        0x01, 0x56, 0x02, 0x34  // Register Values: 0x0156, 0x0234
                    };
                    DemoHexInspector.SetBuffer(packet);
                    DemoHexInspector.Engine.Dissector.DissectModbusTcp(packet);
                    break;

                case 2: // Siemens S7 Protocol Header & COTP
                    packet = new byte[]
                    {
                        0x03, 0x00, 0x00, 0x1F, // TPKT Header (Version 3, Length 31)
                        0x02, 0xF0, 0x80,       // COTP Header
                        0x32, 0x01, 0x00, 0x00, // S7 Protocol ID 0x32, ROSCTR (Job 1)
                        0x00, 0x01, 0x00, 0x0E, // Redundancy ID, Parameter Length 14
                        0x00, 0x00,             // Data Length 0
                        0x04, 0x01, 0x12, 0x0A, // Function: Read Var (0x04), Item Count: 1
                        0x10, 0x02, 0x00, 0x04, // Transport size: BYTE, Length: 4
                        0x00, 0x01, 0x84, 0x00, // DB Number: 1, Area: DB (0x84)
                        0x00, 0x00              // Start Address: 0
                    };
                    DemoHexInspector.SetBuffer(packet);
                    DemoHexInspector.Engine.Dissector.Segments.Clear();
                    DemoHexInspector.Engine.Dissector.Segments.Add(new HexByteSegment(0, 4, "TPKT Header", 0xFF3B82F6));
                    DemoHexInspector.Engine.Dissector.Segments.Add(new HexByteSegment(4, 3, "COTP Layer", 0xFF6366F1));
                    DemoHexInspector.Engine.Dissector.Segments.Add(new HexByteSegment(7, 10, "S7 Header", 0xFFF59E0B));
                    DemoHexInspector.Engine.Dissector.Segments.Add(new HexByteSegment(17, packet.Length - 17, "S7 Request Item", 0xFF10B981));
                    break;

                case 3: // 512B Industrial Telemetry Buffer
                    packet = new byte[512];
                    var rand = new Random(101);
                    rand.NextBytes(packet);
                    DemoHexInspector.SetBuffer(packet);
                    DemoHexInspector.Engine.Dissector.Segments.Clear();
                    DemoHexInspector.Engine.Dissector.Segments.Add(new HexByteSegment(0, 16, "Sync Header", 0xFF3B82F6));
                    DemoHexInspector.Engine.Dissector.Segments.Add(new HexByteSegment(16, 64, "Calibration Table", 0xFFF59E0B));
                    DemoHexInspector.Engine.Dissector.Segments.Add(new HexByteSegment(80, 416, "Sensor Ring Buffer", 0xFF10B981));
                    DemoHexInspector.Engine.Dissector.Segments.Add(new HexByteSegment(496, 16, "CRC / Checksum Block", 0xFFEF4444));
                    break;

                default: // Modbus TCP Read Holding Registers (FC 03)
                    packet = new byte[]
                    {
                        0x00, 0x01,             // Transaction ID: 1
                        0x00, 0x00,             // Protocol ID: 0
                        0x00, 0x06,             // Length: 6
                        0x01,                   // Unit ID: 1
                        0x03,                   // Function Code: 3
                        0x00, 0x6B,             // Start Register: 107
                        0x00, 0x03              // Register Count: 3
                    };
                    DemoHexInspector.SetBuffer(packet);
                    DemoHexInspector.Engine.Dissector.DissectModbusTcp(packet);
                    break;
            }
        }

        private void ComboHexPackets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DemoHexInspector == null || ComboHexPackets == null) return;
            LoadHexPacket(ComboHexPackets.SelectedIndex);
        }

        private void BtnRandomizePacket_Click(object sender, RoutedEventArgs e)
        {
            byte[] randBuf = new byte[128];
            new Random().NextBytes(randBuf);
            DemoHexInspector.SetBuffer(randBuf);
            DemoHexInspector.Engine.Dissector.Segments.Clear();
            DemoHexInspector.Engine.Dissector.Segments.Add(new HexByteSegment(0, 8, "Frame Header", 0xFF3B82F6));
            DemoHexInspector.Engine.Dissector.Segments.Add(new HexByteSegment(8, 116, "Payload", 0xFF10B981));
            DemoHexInspector.Engine.Dissector.Segments.Add(new HexByteSegment(124, 4, "CRC32", 0xFFEF4444));
        }

        #endregion

        #region State Machine & Pulse Executor Setup

        private void SetupStateExecutor()
        {
            var sm = DemoStateExecutor.Engine;
            sm.Nodes.Clear();
            sm.Transitions.Clear();

            // Packaging Line Sequence States:
            // IDLE -> INFEED -> HEAT_SEAL -> VACUUM_PUMP -> INSPECT -> DISPATCH -> IDLE
            var sIdle = new MachineStateNode("idle", "1. IDLE", 80, 160, 0xFF64748B, duration: 2.0);
            var sInfeed = new MachineStateNode("infeed", "2. INFEED", 240, 70, 0xFF3B82F6, duration: 3.0);
            var sSeal = new MachineStateNode("seal", "3. HEAT SEAL", 440, 70, 0xFFF59E0B, duration: 2.5);
            var sVacuum = new MachineStateNode("vacuum", "4. VACUUM", 640, 70, 0xFFA855F7, duration: 3.5);
            var sInspect = new MachineStateNode("inspect", "5. QC INSPECT", 640, 250, 0xFF06B6D4, duration: 2.0);
            var sPass = new MachineStateNode("pass", "6. DISPATCH", 380, 250, 0xFF10B981, duration: 2.5);

            sm.Nodes.Add(sIdle);
            sm.Nodes.Add(sInfeed);
            sm.Nodes.Add(sSeal);
            sm.Nodes.Add(sVacuum);
            sm.Nodes.Add(sInspect);
            sm.Nodes.Add(sPass);

            sm.Transitions.Add(new StateTransitionEdge("t1", "idle", "infeed", "Sensor: Tray Ready"));
            sm.Transitions.Add(new StateTransitionEdge("t2", "infeed", "seal", "Pusher Limit Sw"));
            sm.Transitions.Add(new StateTransitionEdge("t3", "seal", "vacuum", "Temp > 185°C"));
            sm.Transitions.Add(new StateTransitionEdge("t4", "vacuum", "inspect", "Press < -0.8 Bar"));
            sm.Transitions.Add(new StateTransitionEdge("t5", "inspect", "pass", "Camera: OK 99.8%"));
            sm.Transitions.Add(new StateTransitionEdge("t6", "pass", "idle", "Belt Clear"));

            sm.SetInitialState("idle");
        }

        private void BtnStateStep_Click(object sender, RoutedEventArgs e)
        {
            var sm = DemoStateExecutor.Engine;
            var edge = sm.Transitions.Find(t => t.SourceId == sm.ActiveStateId);
            if (edge != null)
            {
                sm.TriggerTransition(edge.TargetId);
            }
        }

        private void BtnStatePause_Click(object sender, RoutedEventArgs e)
        {
            DemoStateExecutor.IsAnimating = !DemoStateExecutor.IsAnimating;
            BtnStatePause.Content = DemoStateExecutor.IsAnimating ? "⏸️ Pause" : "▶️ Resume";
        }

        private void BtnStateReset_Click(object sender, RoutedEventArgs e)
        {
            DemoStateExecutor.Engine.SetInitialState("idle");
        }

        #endregion

        #region Cluster 14: Enterprise Commercial Suite Setup & Handlers

        private void SetupEnterpriseCommercialSuite()
        {
            // 1. Setup ZeroGridLookup
            var sampleMaterials = new List<DemoProduct>
            {
                new DemoProduct { Id = 101, Code = "RES-10K-0402", Name = "SMD Thick Film Resistor 10k", Category = "Passive Components", Price = 0.005, Quantity = 250000, InStock = true },
                new DemoProduct { Id = 102, Code = "CAP-100N-0603", Name = "Ceramic Capacitor 100nF 50V", Category = "Passive Components", Price = 0.012, Quantity = 180000, InStock = true },
                new DemoProduct { Id = 103, Code = "MCU-STM32F4", Name = "ARM Cortex-M4 168MHz MCU", Category = "Semiconductors", Price = 4.25, Quantity = 4500, InStock = true },
                new DemoProduct { Id = 104, Code = "CONN-RJ45-1G", Name = "Ethernet MagJack 1000Base-T", Category = "Connectors", Price = 1.85, Quantity = 12000, InStock = true },
                new DemoProduct { Id = 105, Code = "REG-LM2596-5", Name = "DC-DC Step Down Converter 5V", Category = "Power Electronics", Price = 0.95, Quantity = 8200, InStock = true },
                new DemoProduct { Id = 106, Code = "DIO-SS34-SMA", Name = "Schottky Barrier Diode 40V 3A", Category = "Discrete Semis", Price = 0.08, Quantity = 95000, InStock = true },
                new DemoProduct { Id = 107, Code = "RLY-12V-10A", Name = "Miniature Power Relay 12VDC", Category = "Electromechanical", Price = 1.20, Quantity = 3500, InStock = false }
            };
            DemoGridLookup.DisplayMember = "Name";
            DemoGridLookup.ValueMember = "Code";
            DemoGridLookup.SetDataSource(sampleMaterials);
            DemoGridLookup.ProcessNewValue += (s, e) =>
            {
                ToastNotification.Success(this, $"Quick Add requested for new material: {e.DisplayText}");
                e.Handled = true;
            };

            // 1b. Setup High-Capacity SearchLookUpEdit
            DemoSearchLookup.DisplayMember = "Name";
            DemoSearchLookup.ValueMember = "Code";
            DemoSearchLookup.SetDataSource(sampleMaterials);
            DemoSearchLookup.SelectionChanged += (s, e) =>
            {
                if (DemoSearchLookup.EditValue is DemoProduct prod)
                {
                    ToastNotification.Info(this, $"SearchLookUpEdit selected: {prod.Name} ({prod.Code})");
                }
            };

            // 2. Setup ZeroCheckedComboBox
            DemoCheckedCombo.Items.Add(new CheckedComboItem("ZoneA", "Zone A: SMT Cleanroom (Class 10k)", true));
            DemoCheckedCombo.Items.Add(new CheckedComboItem("ZoneB", "Zone B: Automated PCB Assembly", true));
            DemoCheckedCombo.Items.Add(new CheckedComboItem("ZoneC", "Zone C: CNC Precision Machining", false));
            DemoCheckedCombo.Items.Add(new CheckedComboItem("ZoneD", "Zone D: Automated Storage & Retrieval", true));
            DemoCheckedCombo.Items.Add(new CheckedComboItem("ZoneE", "Zone E: Chemical Batching & Solvent Dispensing", false));
            DemoCheckedCombo.Items.Add(new CheckedComboItem("ZoneF", "Zone F: Packaging & Palletizing Hub", true));

            // 3. Setup ZeroTokenEdit
            DemoTokenEdit.Tokens.Add("Solder-Bridge");
            DemoTokenEdit.Tokens.Add("Cold-Joint");
            DemoTokenEdit.Tokens.Add("Tombstone");
            DemoTokenEdit.Tokens.Add("AOI-Flagged");

            // 4. Setup ZeroColorPicker
            DemoColorPicker.SelectedColor = Color.FromRgb(129, 140, 248);

            // 5. Setup ZeroFilterControl
            DemoFilterControl.AvailableFields.AddRange(new[] { "Status", "Category", "Quantity", "UnitPrice", "WarehouseZone", "LotNumber" });
            DemoFilterControl.RebuildTreeUI();
            TxtGeneratedSql.Text = DemoFilterControl.RootGroup.ToSqlWhere();
            DemoFilterControl.FilterChanged += (s, e) =>
            {
                TxtGeneratedSql.Text = DemoFilterControl.RootGroup.ToSqlWhere();
            };

            // 6. Setup ZeroWizard
            var p1 = new WizardPage
            {
                Title = "1. Work Order Parameters",
                Subtitle = "Specify manufacturing lot code, scheduled unit quota, and line assignment.",
                Icon = "📋"
            };
            var p1Stack = new StackPanel { Margin = new Thickness(16) };
            p1Stack.Children.Add(new TextBlock { Text = "Target Production Work Order: WO-2026-904", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = ZeroWpfTheme.TextPrimary, Margin = new Thickness(0, 0, 0, 8) });
            p1Stack.Children.Add(new TextBlock { Text = "Line: SMT Robotic Cell A  |  Operator: OP-492  |  Target: 2,500 Units", FontSize = 12, Foreground = ZeroWpfTheme.TextSecondary });
            p1.Content = p1Stack;

            var p2 = new WizardPage
            {
                Title = "2. Material Feeder Verification",
                Subtitle = "Verify electronic reel lot barcodes and feeder reel alignment.",
                Icon = "🔍"
            };
            var p2Stack = new StackPanel { Margin = new Thickness(16) };
            p2Stack.Children.Add(new TextBlock { Text = "Feeder 01: RES-10K-0402 (Reel #88192) - OK", FontSize = 12, Foreground = ZeroWpfTheme.SuccessAccent, Margin = new Thickness(0, 0, 0, 4) });
            p2Stack.Children.Add(new TextBlock { Text = "Feeder 02: CAP-100N-0603 (Reel #91024) - OK", FontSize = 12, Foreground = ZeroWpfTheme.SuccessAccent, Margin = new Thickness(0, 0, 0, 4) });
            p2Stack.Children.Add(new TextBlock { Text = "Feeder 03: MCU-STM32F4 (Tray #12) - OK", FontSize = 12, Foreground = ZeroWpfTheme.SuccessAccent });
            p2.Content = p2Stack;

            var p3 = new WizardPage
            {
                Title = "3. Quality & Safety Sign-Off",
                Subtitle = "Inspect optical safety interlocks and electronic authorization signature.",
                Icon = "✔"
            };
            var p3Stack = new StackPanel { Margin = new Thickness(16) };
            p3Stack.Children.Add(new TextBlock { Text = "All safety curtains and guard interlocks verified active.", FontSize = 12, Foreground = ZeroWpfTheme.TextSecondary, Margin = new Thickness(0, 0, 0, 6) });
            p3Stack.Children.Add(new TextBlock { Text = "Click 'Finish' to arm robotic cell and begin high-speed execution.", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = ZeroWpfTheme.PrimaryAccent });
            p3.Content = p3Stack;

            DemoInlineWizard.Pages.Add(p1);
            DemoInlineWizard.Pages.Add(p2);
            DemoInlineWizard.Pages.Add(p3);
            DemoInlineWizard.Finished += (s, e) =>
            {
                ToastNotification.Success(this, "Production Order WO-2026-904 successfully launched!");
            };

            // 7. Setup BoxPlotChart
            DemoBoxPlot.ChartTitle = "SPC Tolerance Distribution (Six Sigma)";
            DemoBoxPlot.UpperSpecLimit = 12.08;
            DemoBoxPlot.LowerSpecLimit = 11.92;
            DemoBoxPlot.AddPoint(new BoxPlotDataPoint("Batch A", 11.94, 11.98, 12.01, 12.03, 12.06, Color.FromRgb(129, 140, 248)));
            DemoBoxPlot.AddPoint(new BoxPlotDataPoint("Batch B", 11.95, 11.99, 12.00, 12.02, 12.05, Color.FromRgb(56, 189, 248)));
            DemoBoxPlot.AddPoint(new BoxPlotDataPoint("Batch C", 11.91, 11.97, 12.02, 12.04, 12.09, Color.FromRgb(248, 113, 113)) { Outliers = { 12.11 } });
            DemoBoxPlot.AddPoint(new BoxPlotDataPoint("Batch D", 11.96, 11.99, 12.01, 12.03, 12.06, Color.FromRgb(52, 211, 153)));
            DemoBoxPlot.AddPoint(new BoxPlotDataPoint("Batch E", 11.93, 11.98, 12.00, 12.02, 12.07, Color.FromRgb(251, 191, 36)));

            // 8. Setup ZeroPrintPreview
            var docCanvas = new Canvas { Width = 794, Height = 1123, Background = Brushes.White };
            
            var headerBorder = new Border
            {
                Width = 714,
                Height = 80,
                Margin = new Thickness(40, 40, 0, 0),
                BorderThickness = new Thickness(0, 0, 0, 2),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59))
            };
            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock { Text = "CERTIFICATE OF CONFORMANCE & ANALYSIS", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)), FontFamily = new FontFamily("Segoe UI") });
            titleStack.Children.Add(new TextBlock { Text = "Standard Industrial Quality Control Report  |  ISO 9001:2015 Compliant", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Margin = new Thickness(0, 4, 0, 0) });
            headerBorder.Child = titleStack;
            docCanvas.Children.Add(headerBorder);

            var contentBox = new Border
            {
                Width = 714,
                Height = 850,
                Margin = new Thickness(40, 140, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(24)
            };
            var bodyStack = new StackPanel();
            bodyStack.Children.Add(new TextBlock { Text = "Batch Lot Number: LOT-2026-0904-MESX", FontWeight = FontWeights.Bold, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)), Margin = new Thickness(0, 0, 0, 8) });
            bodyStack.Children.Add(new TextBlock { Text = "Inspection Station: AOI Workcell #04 (Robotic Cell A)", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)), Margin = new Thickness(0, 0, 0, 4) });
            bodyStack.Children.Add(new TextBlock { Text = "Total Units Inspected: 5,000 units  |  Defect Rate: 0.02% (Pass: 4,999, NG: 1)", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)), Margin = new Thickness(0, 0, 0, 16) });
            bodyStack.Children.Add(new TextBlock { Text = "Statistical Six Sigma Process Capability: Cpk = 1.67 (Capable)", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(22, 101, 52)), Margin = new Thickness(0, 0, 0, 24) });
            bodyStack.Children.Add(new TextBlock { Text = "Authorized QA Lead Signature:  [ELECTRONICALLY SIGNED BY SYSTEM AUDIT]", FontSize = 12, FontStyle = FontStyles.Italic, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)) });
            contentBox.Child = bodyStack;
            docCanvas.Children.Add(contentBox);

            DemoPrintPreview.SetPages(new[] { docCanvas });

            // 9. Setup PivotGridControl (Multidimensional Cross-Tab OLAP)
            var samplePivotData = new List<DemoProduct>
            {
                new DemoProduct { Category = "Semiconductors", Code = "MCU-STM32F4", Name = "North Plant", Quantity = 4500, Price = 4.25 },
                new DemoProduct { Category = "Semiconductors", Code = "MCU-STM32F4", Name = "South Plant", Quantity = 3200, Price = 4.25 },
                new DemoProduct { Category = "Passive Components", Code = "RES-10K-0402", Name = "North Plant", Quantity = 150000, Price = 0.005 },
                new DemoProduct { Category = "Passive Components", Code = "RES-10K-0402", Name = "South Plant", Quantity = 100000, Price = 0.005 },
                new DemoProduct { Category = "Passive Components", Code = "CAP-100N-0603", Name = "North Plant", Quantity = 90000, Price = 0.012 },
                new DemoProduct { Category = "Passive Components", Code = "CAP-100N-0603", Name = "South Plant", Quantity = 90000, Price = 0.012 },
                new DemoProduct { Category = "Connectors", Code = "CONN-RJ45-1G", Name = "North Plant", Quantity = 6000, Price = 1.85 },
                new DemoProduct { Category = "Connectors", Code = "CONN-RJ45-1G", Name = "South Plant", Quantity = 6000, Price = 1.85 },
                new DemoProduct { Category = "Power Electronics", Code = "REG-LM2596-5", Name = "North Plant", Quantity = 4100, Price = 0.95 },
                new DemoProduct { Category = "Power Electronics", Code = "REG-LM2596-5", Name = "South Plant", Quantity = 4100, Price = 0.95 }
            };
            EnterprisePivotGrid.AddField("Category", PivotArea.RowArea, "Category");
            EnterprisePivotGrid.AddField("Name", PivotArea.ColumnArea, "Factory Facility");
            EnterprisePivotGrid.AddField("Quantity", PivotArea.DataArea, "Total Qty", PivotSummaryType.Sum);
            EnterprisePivotGrid.AddField("Total", PivotArea.DataArea, "Total Value ($)", PivotSummaryType.Sum);
            EnterprisePivotGrid.DataSource = samplePivotData;

            // 10. Setup RangeControl (Visual Timeline & Range Selector)
            DemoRangeControl.DataType = RangeDataType.DateTime;
            DemoRangeControl.Interval = RangeInterval.Day;
            DemoRangeControl.TotalStartDate = new DateTime(2026, 1, 1);
            DemoRangeControl.TotalEndDate = new DateTime(2026, 12, 31);
            DemoRangeControl.SelectedStartDate = new DateTime(2026, 2, 15);
            DemoRangeControl.SelectedEndDate = new DateTime(2026, 8, 30);

            // Populate sample time-series data points (weekly production volumes)
            var rangePoints = new List<RangeDataPoint>();
            var rnd = new Random(42);
            for (var d = new DateTime(2026, 1, 1); d <= new DateTime(2026, 12, 31); d = d.AddDays(7))
            {
                double val = 40 + rnd.Next(10, 80) + Math.Sin(d.DayOfYear / 40.0) * 25;
                rangePoints.Add(new RangeDataPoint(d, Math.Max(5, val)));
            }
            DemoRangeControl.SetDataPoints(rangePoints);
            DemoRangeControl.RangeSelectionChanged += (s, e) => UpdateRangeReadout();
            UpdateRangeReadout();
        }

        private void UpdateRangeReadout()
        {
            if (TxtRangeReadout == null || DemoRangeControl == null) return;
            var start = DemoRangeControl.SelectedStartDate;
            var end = DemoRangeControl.SelectedEndDate;
            int days = (int)(end - start).TotalDays;
            TxtRangeReadout.Text = $"{start:yyyy-MM-dd} to {end:yyyy-MM-dd} (Span: {days} days)";
        }

        private void RangeOption_Changed(object sender, RoutedEventArgs e)
        {
            if (DemoRangeControl == null) return;
            DemoRangeControl.ShowRangeThumbs = ChkRangeThumbs?.IsChecked == true;
            DemoRangeControl.ShowBackgroundGraph = ChkRangeGraph?.IsChecked == true;
            DemoRangeControl.ShowRuler = ChkRangeRuler?.IsChecked == true;
            DemoRangeControl.EnableZoom = ChkRangeZoom?.IsChecked == true;
            DemoRangeControl.EnablePan = ChkRangePan?.IsChecked == true;
            DemoRangeControl.SnapToInterval = ChkRangeSnap?.IsChecked == true;
        }

        private void GraphType_Changed(object sender, RoutedEventArgs e)
        {
            if (DemoRangeControl == null) return;
            if (RadGraphHist?.IsChecked == true)
            {
                DemoRangeControl.BackgroundGraphType = RangeGraphType.Histogram;
            }
            else if (RadGraphLine?.IsChecked == true)
            {
                DemoRangeControl.BackgroundGraphType = RangeGraphType.Line;
            }
            else
            {
                DemoRangeControl.BackgroundGraphType = RangeGraphType.Area;
            }
        }

        private void BtnResetRangeView_Click(object sender, RoutedEventArgs e)
        {
            if (DemoRangeControl == null) return;
            DemoRangeControl.TotalStartDate = new DateTime(2026, 1, 1);
            DemoRangeControl.TotalEndDate = new DateTime(2026, 12, 31);
            DemoRangeControl.SelectedStartDate = new DateTime(2026, 1, 1);
            DemoRangeControl.SelectedEndDate = new DateTime(2026, 12, 31);
            UpdateRangeReadout();
            ToastNotification.Info(this, "RangeControl reset to full 2026 timeline.");
        }

        private void BtnApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            string sql = DemoFilterControl.RootGroup.ToSqlWhere();
            TxtGeneratedSql.Text = sql;
            ToastNotification.Info(this, "Filter Criteria Compiled: " + sql);
        }

        private void BtnResetFilter_Click(object sender, RoutedEventArgs e)
        {
            DemoFilterControl.RootGroup.Children.Clear();
            DemoFilterControl.RootGroup.AddCondition("Status", FilterComparisonOperator.Equals, "Active");
            DemoFilterControl.RebuildTreeUI();
            TxtGeneratedSql.Text = DemoFilterControl.RootGroup.ToSqlWhere();
            ToastNotification.Info(this, "Filter criteria reset to default.");
        }

        private void BtnToastSuccess_Click(object sender, RoutedEventArgs e)
        {
            ToastNotification.Success(this, "Production batch #8091 successfully released to shopfloor.");
        }

        private void BtnToastInfo_Click(object sender, RoutedEventArgs e)
        {
            ToastNotification.Info(this, "PLC connection synchronized at 10 ms cycle (Modbus TCP).");
        }

        private void BtnToastWarning_Click(object sender, RoutedEventArgs e)
        {
            ToastNotification.Warning(this, "Thermal threshold approaching USL warning limit (82.5°C).");
        }

        private void BtnToastError_Click(object sender, RoutedEventArgs e)
        {
            ToastNotification.Error(this, "Emergency Stop circuit tripped on Conveyor CV-401!");
        }

        private void BtnToastAlarm_Click(object sender, RoutedEventArgs e)
        {
            ToastNotification.Alarm(this, "Critical: High-temperature limit exceeded on Exothermic Reactor RX-401 (94.8°C)!", 4000);
        }

        private void ComboDeliveryMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboDeliveryMode.SelectedItem is ZeroNotificationDeliveryMode mode)
            {
                ToastStackManager.DeliveryMode = mode;
                if (IsLoaded)
                {
                    ToastNotification.Info(this, $"Notification delivery mode switched to: {mode}");
                }
            }
        }

        private void ChkRouteAlarmsToSystem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk)
            {
                ToastStackManager.RouteAlarmsToSystem = chk.IsChecked == true;
            }
        }

        private void BtnToastSystem_Click(object sender, RoutedEventArgs e)
        {
            WindowsNotificationBridge.ShowNotification(
                "ZeroUI SCADA Event",
                "PLC Alert: Reactor RX-401 pressure threshold exceeded (4.8 Bar). Click to inspect.",
                ToastType.Alarm,
                onClick: () =>
                {
                    ToastNotification.Info(this, "Operator activated window via Windows Action Center notification.");
                });
        }

        private void BtnToggleD3D_Click(object sender, RoutedEventArgs e)
        {
            DemoD3DCanvas.IsRenderingActive = !DemoD3DCanvas.IsRenderingActive;
            BtnToggleD3D.Content = DemoD3DCanvas.IsRenderingActive ? "⏸️ Pause GPU" : "▶️ Resume GPU";
            ToastNotification.Info(this, DemoD3DCanvas.IsRenderingActive ? "DirectX 11 GPU rendering active (60-144 FPS)." : "DirectX 11 GPU rendering paused.");
        }

        private void BtnTriggerD3DFrame_Click(object sender, RoutedEventArgs e)
        {
            DemoD3DCanvas.RenderCurrentFrame();
            ToastNotification.Success(this, "Direct3D 11 GPU frame rendered & composited via D3DImage.");
        }

        private void BtnD3DStream1M_Click(object sender, RoutedEventArgs e)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            const int count = 1_000_000;
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                data[i] = (float)(Math.Sin(t * 120.0) * Math.Cos(t * 3.0) + 0.15 * Math.Sin(t * 5000.0));
            }
            // Add narrow spikes to prove MinMax decimation preserves critical peaks
            data[count / 4] = 2.8f;
            data[count / 2] = -2.8f;
            data[count * 3 / 4] = 2.5f;

            DemoD3DCanvas.EnableWaveformDemo = false;
            DemoD3DCanvas.DecimationMode = ZeroGraphics.Waveform.Pipeline.WaveformDecimationMode.MinMax;
            DemoD3DCanvas.TraceColor = System.Windows.Media.Color.FromRgb(0x00, 0xFF, 0x88); // Neon emerald
            DemoD3DCanvas.AutoScale = true;
            DemoD3DCanvas.SetData(data);
            sw.Stop();

            ToastNotification.Success(this, $"Streamed 1,000,000 points to GPU with MinMax Decimation in {sw.ElapsedMilliseconds} ms.");
        }

        private void BtnD3DStream100kLttb_Click(object sender, RoutedEventArgs e)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            const int count = 100_000;
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                data[i] = (float)(Math.Sin(t * 40.0) + 0.5 * Math.Sin(t * 120.0) + 0.25 * Math.Sin(t * 300.0));
            }

            DemoD3DCanvas.EnableWaveformDemo = false;
            DemoD3DCanvas.DecimationMode = ZeroGraphics.Waveform.Pipeline.WaveformDecimationMode.Lttb;
            DemoD3DCanvas.TraceColor = System.Windows.Media.Color.FromRgb(0xFF, 0xB7, 0x03); // Amber/gold
            DemoD3DCanvas.AutoScale = true;
            DemoD3DCanvas.SetData(data);
            sw.Stop();

            ToastNotification.Success(this, $"Streamed 100,000 points to GPU with LTTB Decimation in {sw.ElapsedMilliseconds} ms.");
        }

        private void BtnD3DOscilloscope_Click(object sender, RoutedEventArgs e)
        {
            DemoD3DCanvas.SetData((float[]?)null);
            DemoD3DCanvas.EnableWaveformDemo = true;
            DemoD3DCanvas.TraceColor = System.Windows.Media.Color.FromRgb(0x00, 0xE5, 0xFF); // Neon cyan
            ToastNotification.Info(this, "Switched to interactive animated multi-frequency oscilloscope demo.");
        }

        #region Automatic Render Optimizer Handlers

        private void CmbOptimizerMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PrimaryCard == null) return;
            UpdateOptimizerState();
        }

        private void SliderElevation_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtValElevation != null) TxtValElevation.Text = $"{SliderElevation.Value:F0} px";
            UpdateOptimizerState();
        }

        private void SliderBlur_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtValBlur != null) TxtValBlur.Text = $"{SliderBlur.Value:F0} px";
            UpdateOptimizerState();
        }

        private void SliderGlow_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtValGlow != null) TxtValGlow.Text = $"{SliderGlow.Value:F1}";
            UpdateOptimizerState();
        }

        private void CmbBatchCount_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PanelOptimizerCards == null) return;
            UpdateOptimizerState();
        }

        private void BtnPresetFlat_Click(object sender, RoutedEventArgs e)
        {
            SliderElevation.Value = 0;
            SliderBlur.Value = 0;
            SliderGlow.Value = 0;
            CmbBatchCount.SelectedIndex = 0;
            CmbOptimizerMode.SelectedIndex = 0;
            ToastNotification.Info(this, "Preset applied: Flat UI (Labels + Buttons). Routed to pure CPU DirectWrite!");
        }

        private void BtnPresetElevated_Click(object sender, RoutedEventArgs e)
        {
            SliderElevation.Value = 12;
            SliderBlur.Value = 20;
            SliderGlow.Value = 0;
            CmbBatchCount.SelectedIndex = 0;
            CmbOptimizerMode.SelectedIndex = 0;
            ToastNotification.Success(this, "Preset applied: Elevated Glass Card. Routed to Hybrid (GPU SDF Shadow + CPU subpixel text)!");
        }

        private void BtnPresetNeon_Click(object sender, RoutedEventArgs e)
        {
            SliderElevation.Value = 10;
            SliderBlur.Value = 16;
            SliderGlow.Value = 1.0;
            CmbBatchCount.SelectedIndex = 0;
            CmbOptimizerMode.SelectedIndex = 0;
            ToastNotification.Success(this, "Preset applied: Cyberpunk Neon Bloom. Routed to Live GPU NeonGlowSdf shader!");
        }

        private void BtnPresetBatch_Click(object sender, RoutedEventArgs e)
        {
            SliderElevation.Value = 8;
            SliderBlur.Value = 14;
            SliderGlow.Value = 0;
            CmbBatchCount.SelectedIndex = 3; // 32 Cards
            CmbOptimizerMode.SelectedIndex = 0;
            ToastNotification.Success(this, "Preset applied: 32x Batched Cards. Routed to 9-Slice Atlas (95% draw calls saved)!");
        }

        private void BtnFlushAtlas_Click(object sender, RoutedEventArgs e)
        {
            ZeroUI.Core.Rendering.Optimizer.ZeroShadowAtlas.Clear();
            UpdateAtlasTelemetry();
            ToastNotification.Info(this, "9-Slice Shadow Atlas cache cleared.");
        }

        private void UpdateOptimizerState()
        {
            if (PrimaryCard == null || SecondaryCard == null) return;

            int batchCount = 1;
            if (CmbBatchCount != null)
            {
                switch (CmbBatchCount.SelectedIndex)
                {
                    case 1: batchCount = 4; break;
                    case 2: batchCount = 12; break;
                    case 3: batchCount = 32; break;
                    default: batchCount = 1; break;
                }
            }

            var mode = ZeroUI.Wpf.Rendering.Optimizer.OptimizerRoutingMode.Auto;
            if (CmbOptimizerMode != null)
            {
                switch (CmbOptimizerMode.SelectedIndex)
                {
                    case 1: mode = ZeroUI.Wpf.Rendering.Optimizer.OptimizerRoutingMode.ForceCpu; break;
                    case 2: mode = ZeroUI.Wpf.Rendering.Optimizer.OptimizerRoutingMode.ForceGpuShader; break;
                    case 3: mode = ZeroUI.Wpf.Rendering.Optimizer.OptimizerRoutingMode.ForceGpuAtlas; break;
                    default: mode = ZeroUI.Wpf.Rendering.Optimizer.OptimizerRoutingMode.Auto; break;
                }
            }

            double elev = SliderElevation?.Value ?? 8.0;
            double blur = SliderBlur?.Value ?? 14.0;
            double glow = SliderGlow?.Value ?? 0.0;

            PrimaryCard.Elevation = elev;
            PrimaryCard.BlurRadius = blur;
            PrimaryCard.GlowIntensity = glow;
            PrimaryCard.OptimizationMode = mode;
            PrimaryCard.BatchCount = batchCount;

            SecondaryCard.Elevation = elev;
            SecondaryCard.BlurRadius = blur;
            SecondaryCard.GlowIntensity = glow;
            SecondaryCard.OptimizationMode = mode;
            SecondaryCard.BatchCount = batchCount;

            // Generate additional sample cards when batchCount > 1
            SyncBatchCards(batchCount, elev, blur, glow, mode);

            // Trigger visual update
            PrimaryCard.InvalidateVisual();
            SecondaryCard.InvalidateVisual();

            // Telemetry
            UpdateAtlasTelemetry();
            UpdateDecisionDisplay();
        }

        private void SyncBatchCards(int count, double elev, double blur, double glow, ZeroUI.Wpf.Rendering.Optimizer.OptimizerRoutingMode mode)
        {
            if (PanelOptimizerCards == null) return;

            // Keep the 3 permanent showcase cards
            while (PanelOptimizerCards.Children.Count > 3)
            {
                PanelOptimizerCards.Children.RemoveAt(PanelOptimizerCards.Children.Count - 1);
            }

            int extra = Math.Min(count - 1, 30);
            for (int i = 0; i < extra; i++)
            {
                var card = new ZeroUI.Wpf.Rendering.Optimizer.ZeroOptimizedCard
                {
                    Width = 260,
                    Height = 140,
                    Margin = new Thickness(0, 0, 16, 16),
                    Elevation = elev,
                    BlurRadius = blur,
                    GlowIntensity = glow,
                    OptimizationMode = mode,
                    BatchCount = count,
                    Padding = new Thickness(14)
                };

                var sp = new StackPanel();
                sp.Children.Add(new TextBlock { Text = $"📦 Batched Card #{i + 3}", FontWeight = FontWeights.Bold, FontSize = 12.5, Foreground = ZeroWpfTheme.TextPrimary });
                sp.Children.Add(new TextBlock { Text = "9-Slice Atlas Cache Instance", FontSize = 11, Foreground = ZeroWpfTheme.TextSecondary, Margin = new Thickness(0, 4, 0, 8) });
                sp.Children.Add(new TextBlock { Text = "Zero VRAM Re-allocation", FontSize = 10, Foreground = ZeroWpfTheme.SuccessAccent, FontWeight = FontWeights.SemiBold });

                card.Child = sp;
                PanelOptimizerCards.Children.Add(card);
            }
        }

        private void UpdateDecisionDisplay()
        {
            if (PrimaryCard == null) return;

            var pipeline = PrimaryCard.AssignedPipeline;
            TxtTelemetryPipeline.Text = $"PIPELINE: {pipeline.ToString().ToUpperInvariant()}";
            TxtTelemetryShader.Text = $"Shader: {PrimaryCard.ActiveShader}";
            TxtTelemetrySpeedup.Text = $"Speedup: {PrimaryCard.EstimatedSpeedupFactor:F1}x";
            TxtTelemetryReason.Text = PrimaryCard.DecisionReason;

            switch (pipeline)
            {
                case ZeroUI.Core.Rendering.Optimizer.RenderPipelineTarget.Cpu:
                    BadgePipeline.Background = new SolidColorBrush(Color.FromRgb(100, 116, 139)); // Slate
                    break;
                case ZeroUI.Core.Rendering.Optimizer.RenderPipelineTarget.GpuAtlas:
                    BadgePipeline.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Emerald
                    break;
                case ZeroUI.Core.Rendering.Optimizer.RenderPipelineTarget.GpuShader:
                    BadgePipeline.Background = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Amber
                    break;
                default: // Hybrid
                    BadgePipeline.Background = new SolidColorBrush(Color.FromRgb(2, 132, 199)); // Sky blue
                    break;
            }

            var monitor = ZeroUI.Wpf.Rendering.Optimizer.ZeroWpfRenderMonitor.Instance;
            TxtFidelityBadge.Text = $"TIER: {monitor.CurrentFidelity.ToString().ToUpperInvariant()} ({(monitor.CurrentFidelity == ZeroUI.Core.Rendering.Optimizer.RenderFidelityTier.Ultra ? "144 FPS" : "60 FPS")})";
            TxtMonitorFps.Text = $"FPS: {monitor.CurrentFps:F1} | {monitor.RollingAverageFrameTimeMs:F1}ms";

            if (TxtGpuAdapterBadge != null)
            {
                var tier = ZeroUI.Core.Rendering.Optimizer.ZeroGpuCapabilities.CurrentTier;
                string tierName = tier switch
                {
                    ZeroUI.Core.Rendering.Optimizer.HardwareGpuTier.Tier2_Discrete => "Discrete",
                    ZeroUI.Core.Rendering.Optimizer.HardwareGpuTier.Tier1_Integrated => "Integrated",
                    _ => "Software"
                };
                TxtGpuAdapterBadge.Text = $"GPU: {ZeroUI.Core.Rendering.Optimizer.ZeroGpuCapabilities.AdapterName} ({tierName} | {ZeroUI.Core.Rendering.Optimizer.ZeroGpuCapabilities.DedicatedVramMb:F0}MB)";
            }
        }

        private void UpdateAtlasTelemetry()
        {
            if (TxtAtlasPatches == null) return;
            TxtAtlasPatches.Text = $"{ZeroUI.Core.Rendering.Optimizer.ZeroShadowAtlas.CachedPatchCount} Patches";
            TxtAtlasHitRate.Text = $"{ZeroUI.Core.Rendering.Optimizer.ZeroShadowAtlas.HitRatePercentage:F1}%";
            long hits = ZeroUI.Core.Rendering.Optimizer.ZeroShadowAtlas.CacheHits;
            TxtAtlasSavedCalls.Text = hits > 0 ? $"{hits} Calls Saved" : "95% Reduction";
        }

        private void BtnToggleHUD_Click(object sender, RoutedEventArgs e)
        {
            if (MasterHUD == null) return;
            MasterHUD.Visibility = MasterHUD.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            BtnToggleHUD.Content = MasterHUD.Visibility == Visibility.Visible ? "⚡ HUD [ON]" : "⚡ HUD";
        }

        #endregion

        private void BtnShowModalConfirm_Click(object sender, RoutedEventArgs e)
        {
            bool confirmed = ModalDialog.Confirm(this,
                "Confirm Batch Release",
                "Are you sure you want to dispatch Batch WO-2026-904 to Robotic Workcell A? All feeder allocations and safety interlocks have been verified.",
                "Release Batch",
                "Cancel");

            if (confirmed)
            {
                ToastNotification.Success(this, "Batch dispatched successfully to manufacturing line!");
            }
            else
            {
                ToastNotification.Warning(this, "Dispatch cancelled by operator.");
            }
        }

        private bool _isLotoActive = false;

        private void BtnToggleLoto_Click(object sender, RoutedEventArgs e)
        {
            _isLotoActive = !_isLotoActive;
            var flag = _isLotoActive ? DeviceStatusFlags.LockedOut : DeviceStatusFlags.None;
            if (PlantCanvas.Scene != null)
            {
                foreach (var node in PlantCanvas.Scene.RootNodes)
                {
                    if (node is ZeroSceneNode zNode && (zNode.Id == "P-101A" || zNode.Id == "XV-101"))
                    {
                        zNode.StatusFlags = flag;
                    }
                }
                PlantCanvas.InvalidateVisual();
            }
            BtnToggleLoto.Content = _isLotoActive ? "🔓 Release OSHA LOTO" : "🔒 Toggle OSHA LOTO";
            if (_isLotoActive)
            {
                ToastNotification.Alarm(this, "OSHA LOTO Padlock applied to Pump P-101A and Valve XV-101!", 4000);
            }
            else
            {
                ToastNotification.Success(this, "OSHA LOTO Padlock removed. System clear for operation.");
            }
        }

        private void BtnTunePid_Click(object sender, RoutedEventArgs e)
        {
            var screenPoint = PointToScreen(new Point(Math.Max(50, ActualWidth - 420), 100));
            PidFlyout.ShowFlyout(this, screenPoint, "PIC-101", "Boiler Steam Header Pressure");
            ToastNotification.Info(this, "PID Faceplate activated. Live 60 FPS loop tuning active.");
        }

        private void BtnToggleCrosshair_Click(object sender, RoutedEventArgs e)
        {
            AreaChart.CrosshairMode = AreaChart.CrosshairMode == CrosshairMode.Both ? CrosshairMode.None : CrosshairMode.Both;
            AreaChart.InvalidateVisual();
            BarChart.CrosshairMode = AreaChart.CrosshairMode;
            BarChart.InvalidateVisual();
            ToastNotification.Info(this, $"Crosshair HUD mode: {AreaChart.CrosshairMode}");
        }

        private void BtnToggleSpcBelts_Click(object sender, RoutedEventArgs e)
        {
            AreaChart.EnableSpcBelts = !AreaChart.EnableSpcBelts;
            AreaChart.InvalidateVisual();
            BarChart.EnableSpcBelts = AreaChart.EnableSpcBelts;
            BarChart.InvalidateVisual();
            ToastNotification.Info(this, AreaChart.EnableSpcBelts ? "SPC 3σ Limit Belts (UCL, CL, LCL) enabled." : "SPC 3σ Limit Belts hidden.");
        }

        #endregion

        #region Detail Drawer Handlers

        private void BtnDetailDrawer_Click(object sender, RoutedEventArgs e)
        {
            UpdateWpfDrawerSelection();
            MainDetailDrawer.Toggle();
        }

        private void BtnDrawerDispatch_Click(object sender, RoutedEventArgs e)
        {
            ToastNotification.Success(this, $"Express dispatch requested for {DrawerItemCode.Text} ({DrawerQuantity.Text})!");
        }

        private void BtnDrawerPrintLabel_Click(object sender, RoutedEventArgs e)
        {
            ToastNotification.Info(this, $"Print job queued for lot {DrawerLotNumber.Text} on Zebra Thermal Printer.");
        }

        private void BtnDrawerAuditLog_Click(object sender, RoutedEventArgs e)
        {
            ToastNotification.Info(this, $"Audit trail verified: All quality check gates passed for {DrawerItemCode.Text}.");
        }

        private void UpdateWpfDrawerSelection()
        {
            if (VirtualGrid == null || VirtualGrid.DataSource == null) return;
            int modelRow = VirtualGrid.SelectedIndex;
            if (modelRow < 0 || modelRow >= VirtualGrid.DataSource.TotalRowCount) return;

            CellValueBuffer buf = new CellValueBuffer();
            // Columns:
            // 0: Active, 1: Category, 2: Id, 3: Material Code, 4: Description, 5: Quantity, 6: Unit Price, 7: Total Amount, 8: Yield, 9: Lot, 10: Status
            VirtualGrid.DataSource.GetCellValue(modelRow, 3, ref buf);
            string itemCode = buf.Text.ToString();

            VirtualGrid.DataSource.GetCellValue(modelRow, 4, ref buf);
            string itemName = buf.Text.ToString();

            VirtualGrid.DataSource.GetCellValue(modelRow, 1, ref buf);
            string category = buf.Text.ToString();

            VirtualGrid.DataSource.GetCellValue(modelRow, 5, ref buf);
            string qty = buf.Text.ToString();

            VirtualGrid.DataSource.GetCellValue(modelRow, 6, ref buf);
            string price = buf.Text.ToString();

            VirtualGrid.DataSource.GetCellValue(modelRow, 7, ref buf);
            string total = buf.Text.ToString();

            VirtualGrid.DataSource.GetCellValue(modelRow, 8, ref buf);
            string yieldRate = buf.Text.ToString();

            VirtualGrid.DataSource.GetCellValue(modelRow, 9, ref buf);
            string lot = buf.Text.ToString();

            VirtualGrid.DataSource.GetCellValue(modelRow, 10, ref buf);
            string status = buf.Text.ToString();

            MainDetailDrawer.Title = string.IsNullOrEmpty(itemCode) ? "Material Specification" : itemCode;
            MainDetailDrawer.Subtitle = string.IsNullOrEmpty(itemName) ? "Entity Telemetry & Parameters" : itemName;

            DrawerItemCode.Text = itemCode;
            DrawerItemName.Text = itemName;
            DrawerCategory.Text = category;
            DrawerQuantity.Text = $"{qty} Units";
            DrawerUnitPrice.Text = $"${price}";
            DrawerTotalAmount.Text = $"${total}";
            DrawerLotNumber.Text = lot;
            DrawerYieldRate.Text = yieldRate;
            DrawerStatus.Text = status;

            bool isPassed = status.IndexOf("Pass", StringComparison.OrdinalIgnoreCase) >= 0;
            DrawerStatus.Foreground = isPassed ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(245, 158, 11));
        }

        #endregion

        #region Issue #2 Audit & Modern UI Handlers

        private void OnOpenFlyoutClicked(object sender, RoutedEventArgs e)
        {
            if (AuditDemoFlyout != null)
            {
                AuditDemoFlyout.Show(DemoFlyoutTargetBtn);
            }
        }

        private void OnApplyFlyoutFilterClicked(object sender, RoutedEventArgs e)
        {
            if (AuditDemoFlyout != null)
            {
                AuditDemoFlyout.Hide();
            }
        }

        private void OnLaunchZeroWindowClicked(object sender, RoutedEventArgs e)
        {
            var win = new ZeroWindow
            {
                Title = "ZeroWindow ⚡ Frameless Chrome Demo",
                Width = 640,
                Height = 420,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var titleBar = new ZeroTitleBar
            {
                Title = "ZeroWindow ⚡ Frameless Demo"
            };

            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(titleBar, 0);
            rootGrid.Children.Add(titleBar);

            var contentPanel = new StackPanel { Margin = new Thickness(24) };
            contentPanel.Children.Add(new TextBlock
            {
                Text = "Modern Frameless ZeroWindow Architecture",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            });
            contentPanel.Children.Add(new TextBlock
            {
                Text = "Integrated WindowChrome, custom dark title bar, draggable client frame, double-click maximize, and ZeroUI dynamic skin synchronization.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
            });

            var closeBtn = new Button
            {
                Content = "Close Demo Window",
                Height = 32,
                Padding = new Thickness(16, 0, 16, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            closeBtn.Click += (s, args) => win.Close();
            contentPanel.Children.Add(closeBtn);

            Grid.SetRow(contentPanel, 1);
            rootGrid.Children.Add(contentPanel);

            win.Content = rootGrid;
            win.ShowDialog();
        }

        #region SCADA Safety & Instruments Demo Handlers

        private void SetupSafetyDemo()
        {
            if (WpfEStop != null)
            {
                WpfEStop.Tripped += (s, e) =>
                {
                    if (TxtWpfEStopStatus != null)
                    {
                        TxtWpfEStopStatus.Text = "Status: TRIPPED (Interlock Open)";
                        TxtWpfEStopStatus.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    }
                    ScadaAlarmEngine.RaiseAlarm("ESTOP_WPF", "Safety.Line1.EStop", "CRITICAL: E-Stop Latched on Line 1", ScadaAlarmSeverity.Critical);
                };
                WpfEStop.ResetCompleted += (s, e) =>
                {
                    if (TxtWpfEStopStatus != null)
                    {
                        TxtWpfEStopStatus.Text = "Status: READY (Contacts 1 & 2 Closed)";
                        TxtWpfEStopStatus.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                    }
                    ScadaAlarmEngine.Acknowledge("ESTOP_WPF", "WPF Operator");
                    ScadaAlarmEngine.ClearAlarm("ESTOP_WPF");
                };
            }
        }

        private void BtnWpfTripEStop_Click(object sender, RoutedEventArgs e)
        {
            WpfEStop.Trip();
            if (TxtWpfEStopStatus != null)
            {
                TxtWpfEStopStatus.Text = "Status: TRIPPED (Interlock Open)";
                TxtWpfEStopStatus.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }
            ScadaAlarmEngine.RaiseAlarm("ESTOP_WPF", "Safety.Line1.EStop", "CRITICAL: E-Stop Latched on Line 1", ScadaAlarmSeverity.Critical);
        }

        private void BtnWpfResetEStop_Click(object sender, RoutedEventArgs e)
        {
            WpfEStop.Reset();
            if (TxtWpfEStopStatus != null)
            {
                TxtWpfEStopStatus.Text = "Status: READY (Contacts 1 & 2 Closed)";
                TxtWpfEStopStatus.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            }
            ScadaAlarmEngine.Acknowledge("ESTOP_WPF", "WPF Operator");
            ScadaAlarmEngine.ClearAlarm("ESTOP_WPF");
        }

        private void BtnWpfBreakBeam_Click(object sender, RoutedEventArgs e)
        {
            WpfLightCurtain.SimulateObstacle(7, 3);
            if (TxtWpfCurtainStatus != null)
            {
                TxtWpfCurtainStatus.Text = "Safety Field: INTRUSION (OSSD Trip)";
                TxtWpfCurtainStatus.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }
            ScadaAlarmEngine.RaiseAlarm("CURTAIN_WPF", "Safety.Curtain.Trip", "HIGH: Optical Curtain Intrusion Detected at Station 4", ScadaAlarmSeverity.High);
        }

        private void BtnWpfClearBeam_Click(object sender, RoutedEventArgs e)
        {
            WpfLightCurtain.ClearObstacle();
            if (TxtWpfCurtainStatus != null)
            {
                TxtWpfCurtainStatus.Text = "Safety Field: CLEAR (OSSD Active)";
                TxtWpfCurtainStatus.Foreground = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            }
            ScadaAlarmEngine.ClearAlarm("CURTAIN_WPF");
        }

        private void BtnWpfSpikeMeter_Click(object sender, RoutedEventArgs e)
        {
            WpfMeterVertical.Value = 92.4;
            ScadaAlarmEngine.RaiseAlarm("PRESS_WPF", "Header.Line1.Pressure", "WARNING: Header pressure exceeds 85 bar threshold", ScadaAlarmSeverity.High);
        }

        private void BtnWpfResetMeter_Click(object sender, RoutedEventArgs e)
        {
            WpfMeterVertical.Value = 68.5;
            ScadaAlarmEngine.ClearAlarm("PRESS_WPF");
        }

        private void BtnWpfCycleLed_Click(object sender, RoutedEventArgs e)
        {
            WpfLedRun.State = (MultiStateLedState)(((int)WpfLedRun.State + 1) % 7);
            WpfLedIdle.State = (MultiStateLedState)(((int)WpfLedIdle.State + 1) % 7);
        }

        #region Design-Time & RAD Demo Handlers
        private ZeroUI.Wpf.Design.Adorners.DesignOverlayAdorner? _gridAdorner;
        private bool _isAdornerVisible = false;

        private void SetupDesignToolsDemo()
        {
            try
            {
                ZeroUI.Wpf.Design.Metadata.ZeroWpfMetadataProvider.Register();
                PopulateDesignMockData();
                TxtDesignCodeSnippet.Text = @"<!-- ZeroUI WPF Design-Time Telemetry Grid -->
<datagrid:ZeroGridControl x:Name=""TelemetryGrid""
                          ShowAutoFilterRow=""True""
                          ShowGroupPanel=""True""
                          d:DataContext=""{d:DesignInstance Type=mock:DesignTelemetryRecord, IsDesignTimeCreatable=True}"">
    <datagrid:ZeroGridControl.Columns>
        <datagrid:ZeroColumn FieldName=""Id"" HeaderText=""ID"" Width=""60"" Alignment=""Right"" />
        <datagrid:ZeroColumn FieldName=""TagName"" HeaderText=""Tag Name"" Width=""140"" Alignment=""Left"" />
        <datagrid:ZeroColumn FieldName=""Value"" HeaderText=""Process Value"" Width=""120"" Alignment=""Right"" FormatString=""N2"" />
        <datagrid:ZeroColumn FieldName=""Unit"" HeaderText=""Eng Unit"" Width=""80"" Alignment=""Center"" />
        <datagrid:ZeroColumn FieldName=""Status"" HeaderText=""Health Status"" Width=""100"" Alignment=""Center"" />
        <datagrid:ZeroColumn FieldName=""Timestamp"" HeaderText=""Last Sample"" Width=""160"" Alignment=""Left"" />
    </datagrid:ZeroGridControl.Columns>
</datagrid:ZeroGridControl>";
            }
            catch { }
        }

        private void PopulateDesignMockData()
        {
            var virtualSource = ZeroUI.Wpf.Design.Mock.DesignDataFactory.CreateSampleVirtualSource(15);
            DesignSampleGrid.Columns.Clear();
            DesignSampleGrid.Columns.Add(new ZeroColumn("Id", 60, CellAlignment.Right));
            DesignSampleGrid.Columns.Add(new ZeroColumn("TagName", 130, CellAlignment.Left));
            DesignSampleGrid.Columns.Add(new ZeroColumn("Value", 100, CellAlignment.Right));
            DesignSampleGrid.Columns.Add(new ZeroColumn("Unit", 70, CellAlignment.Center));
            DesignSampleGrid.Columns.Add(new ZeroColumn("Status", 90, CellAlignment.Center));
            DesignSampleGrid.Columns.Add(new ZeroColumn("Timestamp", 150, CellAlignment.Left));
            DesignSampleGrid.DataSource = virtualSource;
        }

        private void BtnRegenerateDesignData_Click(object sender, RoutedEventArgs e)
        {
            PopulateDesignMockData();
        }

        private void BtnToggleDesignAdorner_Click(object sender, RoutedEventArgs e)
        {
            var layer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(DesignSampleGrid);
            if (layer == null) return;

            if (_isAdornerVisible && _gridAdorner != null)
            {
                layer.Remove(_gridAdorner);
                _isAdornerVisible = false;
            }
            else
            {
                _gridAdorner = new ZeroUI.Wpf.Design.Adorners.DesignOverlayAdorner(DesignSampleGrid, "ZeroGridControl (d:DataContext Active)");
                layer.Add(_gridAdorner);
                _isAdornerVisible = true;
            }
        }

        private void BtnCopyDesignSnippet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TxtDesignCodeSnippet.Text);
                MessageBox.Show("XAML Snippet copied to clipboard!", "ZeroUI RAD Assistant", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch { }
        }
        #endregion

        #endregion
        #endregion
    }

    public class RoboticCellConfig
    {
        [Category("1. Identification")]
        [DisplayName("Asset Cell Name")]
        [Description("Descriptive human-readable identifier of the automated machinery or work cell.")]
        public string CellName { get; set; } = "Workstation Cell";

        [Category("1. Identification")]
        [DisplayName("Asset Tag Code")]
        [Description("Global enterprise asset barcode / ERP registration identifier.")]
        public string AssetTag { get; set; } = "AST-001";

        [Category("2. Industrial Network")]
        [DisplayName("Fieldbus IP Address")]
        [Description("IPv4 static endpoint assigned on the machine OT subnet.")]
        public string ControllerIp { get; set; } = "192.168.1.100";

        [Category("2. Industrial Network")]
        [DisplayName("Modbus TCP Port")]
        [Description("Port number for Modbus or OPC-UA fieldbus telemetry ingestion.")]
        public int ModbusPort { get; set; } = 502;

        [Category("3. Process Kinematics")]
        [DisplayName("Target Cycle (sec)")]
        [Description("Expected takt time per finished workpiece assembly in seconds.")]
        public double TargetCycleTimeSec { get; set; } = 15.0;

        [Category("3. Process Kinematics")]
        [DisplayName("Max Rated Payload (kg)")]
        [Description("Maximum allowable mechanical lifting payload on robot end effector.")]
        public double MaxPayloadKg { get; set; } = 500.0;

        [Category("3. Process Kinematics")]
        [DisplayName("Operational Mode")]
        [Description("Current supervisory state machine operation mode.")]
        public string OperatingMode { get; set; } = "Automatic";

        [Category("4. Safety & Interlocks")]
        [DisplayName("Optoelectronic Light Curtain")]
        [Description("Active optical safety curtain barrier protecting human operator envelope.")]
        public bool IsSafetyCurtainActive { get; set; } = true;

        [Category("4. Safety & Interlocks")]
        [DisplayName("Auto Restart on Clear")]
        [Description("Automatically resume production cycle after safety zone breach reset.")]
        public bool AutoRestartOnClear { get; set; } = false;

        [Category("4. Safety & Interlocks")]
        [DisplayName("Emergency Stop Latched")]
        [Description("Hardware SIL-3 E-Stop circuit trip indicator.")]
        public bool EmergencyStopTripped { get; set; } = false;
    }

    public class DemoProduct
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public double Price { get; set; }
        public double Total => Quantity * Price;
        public string Category { get; set; } = string.Empty;
        public bool InStock { get; set; }
    }
}
