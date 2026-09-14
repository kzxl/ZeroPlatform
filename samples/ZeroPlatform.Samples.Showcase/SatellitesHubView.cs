using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using ZeroAudioVisual.Acoustic;
using ZeroAudioVisual.Analysis;
using ZeroCharts.Axis;
using ZeroCharts.Controls;
using ZeroCharts.DataModels;
using ZeroReports.Dom;
using ZeroReports.Pdf;
using ZeroReports.Thermal;
using ZeroTwin3D.Engine;
using ZeroTwin3D.Kinematics;
using ZeroTwin3D.Sync;
using ZeroUI.WinForms.Industrial;
using ZeroUI.WinForms.Theme;

namespace ZeroPlatform.Samples.Showcase
{
    /// <summary>
    /// Interactive Industrial Satellites Hub showcasing all 5 expansion clusters:
    /// Charts, Twin3D, AudioVisual, Reports, and IoT.
    /// </summary>
    public sealed class SatellitesHubView : UserControl
    {
        // 1. Charts
        private ZeroChartControl? _chartControl;
        private LineSeries? _liveTelemetrySeries;
        private CandlestickSeries? _candlestickSeries;
        private bool _showCandlestick = false;

        // 2. Twin3D
        private RobotKinematics _robot;
        private TwinScene _twinScene;
        private TwinTelemetryBridge _telemetryBridge;
        private Label? _lblRobotCoordinates;
        private Label? _lblSafetyStatus;
        private TrackBar? _tbJoint1;
        private TrackBar? _tbJoint2;
        private TrackBar? _tbJoint3;

        // 3. AudioVisual
        private AcousticWaveBuffer _acousticBuffer;
        private Label? _lblRms;
        private Label? _lblKurtosis;
        private Label? _lblBearingHealth;
        private BearingGeometry _bearing6205;

        // 4. Reports & IoT
        private TextBox? _txtZplPreview;

        public SatellitesHubView()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(16, 18, 24);
            ForeColor = Color.FromArgb(220, 225, 235);

            // Initialize 3D Robot & Scene
            _robot = RobotKinematics.CreateStandard6AxisRobot(d1: 400f, a1: 150f, a2: 450f, d4: 400f, d6: 100f);
            _twinScene = new TwinScene();
            // Restricted Zone: [X: 300..600, Y: 100..400, Z: 200..600]
            var restrictedZone = new Aabb3D(new Vec3(300, 100, 200), new Vec3(600, 400, 600));
            _twinScene.SafetyZones.Add(restrictedZone);

            var robotNode = new TwinNode { Name = "6Axis_Industrial_Robot" };
            _twinScene.Root.AddChild(robotNode);
            _telemetryBridge = new TwinTelemetryBridge(_twinScene)
            {
                Robot = _robot,
                RobotEndEffectorNode = robotNode
            };

            // Initialize Acoustic buffer
            _acousticBuffer = new AcousticWaveBuffer(4096, sampleRate: 16000);
            _bearing6205 = new BearingGeometry(pitchDiameterMm: 39f, ballDiameterMm: 7.94f, numBalls: 9);

            InitializeLayout();
            SimulateNormalAcoustic();
            UpdateRobotFromSliders();
        }

        private void InitializeLayout()
        {
            var tableLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(16, 18, 24)
            };

            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tableLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            tableLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            // Section 1: Charts
            tableLayout.Controls.Add(BuildChartsSection(), 0, 0);

            // Section 2: Twin3D
            tableLayout.Controls.Add(BuildTwin3DSection(), 1, 0);

            // Section 3: AudioVisual
            tableLayout.Controls.Add(BuildAudioVisualSection(), 0, 1);

            // Section 4: Reports & IoT
            tableLayout.Controls.Add(BuildReportsIotSection(), 1, 1);

            Controls.Add(tableLayout);
        }

        #region 1. Charts Section (ZeroCharts)
        private Control BuildChartsSection()
        {
            var card = new ZeroCard
            {
                Dock = DockStyle.Fill,
                Title = "📊 High-Density Plots (ZeroCharts)",
                Subtitle = "Direct3D 11 GPU Pipeline | LTTB Decimation | Multi-Series"
            };

            var container = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.FromArgb(20, 24, 34)
            };

            var btnAddPoints = new Button
            {
                Text = "⚡ Add 20K Pts",
                ForeColor = Color.FromArgb(0, 229, 255),
                BackColor = Color.FromArgb(32, 40, 56),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                Height = 28,
                Width = 110,
                Cursor = Cursors.Hand
            };
            btnAddPoints.FlatAppearance.BorderColor = Color.FromArgb(0, 229, 255);
            btnAddPoints.Click += (s, e) => AddTelemetryBatch();

            var btnToggleMode = new Button
            {
                Text = "📈 Toggle Candle",
                ForeColor = Color.FromArgb(0, 255, 136),
                BackColor = Color.FromArgb(32, 40, 56),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                Height = 28,
                Width = 130,
                Cursor = Cursors.Hand
            };
            btnToggleMode.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 136);
            btnToggleMode.Click += (s, e) =>
            {
                _showCandlestick = !_showCandlestick;
                if (_chartControl != null)
                {
                    _chartControl.LineSeriesList.Clear();
                    _chartControl.CandlestickSeriesList.Clear();

                    if (_showCandlestick && _candlestickSeries != null)
                    {
                        _chartControl.AddSeries(_candlestickSeries);
                    }
                    else if (_liveTelemetrySeries != null)
                    {
                        _chartControl.AddSeries(_liveTelemetrySeries);
                    }
                    _chartControl.AutoFit();
                }
            };

            toolbar.Controls.Add(btnAddPoints);
            toolbar.Controls.Add(btnToggleMode);

            _chartControl = new ZeroChartControl
            {
                Dock = DockStyle.Fill
            };

            // Setup Data Series
            _liveTelemetrySeries = new LineSeries("Turbine Vibration (g)")
            {
                StrokeColor = ChartPalette.Cyan,
                StrokeThickness = 1.8f
            };
            for (int i = 0; i < 2000; i++)
            {
                double val = Math.Sin(i * 0.05) * 4.0 + Math.Cos(i * 0.12) * 2.0;
                _liveTelemetrySeries.Add(i * 0.1, val);
            }

            _candlestickSeries = new CandlestickSeries("Production Yield Index");
            DateTime baseTime = DateTime.Today.AddHours(8);
            for (int i = 0; i < 50; i++)
            {
                double open = 100.0 + Math.Sin(i * 0.3) * 15.0;
                double close = open + (i % 2 == 0 ? 3.5 : -2.8);
                double high = Math.Max(open, close) + 2.0;
                double low = Math.Min(open, close) - 1.5;
                _candlestickSeries.Add(new CandlestickItem(baseTime.AddMinutes(i * 15), open, high, low, close));
            }

            _chartControl.AddSeries(_liveTelemetrySeries);

            container.Controls.Add(_chartControl);
            container.Controls.Add(toolbar);
            card.Controls.Add(container);
            return card;
        }

        private void AddTelemetryBatch()
        {
            if (_liveTelemetrySeries == null || _chartControl == null) return;
            double startX = _liveTelemetrySeries.Count * 0.1;
            for (int i = 0; i < 5000; i++)
            {
                double x = startX + i * 0.1;
                double val = Math.Sin(x * 0.2) * 5.0 + (new Random(i).NextDouble() - 0.5) * 2.0;
                _liveTelemetrySeries.Add(x, val);
            }
            _chartControl.AutoFit();
        }
        #endregion

        #region 2. Twin3D Section (ZeroTwin3D)
        private Control BuildTwin3DSection()
        {
            var card = new ZeroCard
            {
                Dock = DockStyle.Fill,
                Title = "🤖 Digital Twin Kinematics (ZeroTwin3D)",
                Subtitle = "Denavit-Hartenberg 6-DOF Solver | Workcell Safety Zone"
            };

            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

            var stack = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoScroll = true
            };

            _lblRobotCoordinates = new Label
            {
                Text = "End-Effector: X=0.0 mm | Y=0.0 mm | Z=0.0 mm",
                Font = new Font("Consolas", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 229, 255),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            };

            _lblSafetyStatus = new Label
            {
                Text = "✅ SAFETY STATUS: Workcell Clear (Nominal)",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 255, 136),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 12)
            };

            // Slider Joint 1
            var lblJ1 = new Label { Text = "Joint 1 (Base Yaw): 0°", AutoSize = true, ForeColor = Color.FromArgb(180, 190, 210) };
            _tbJoint1 = new TrackBar { Minimum = -180, Maximum = 180, Value = 0, Width = 340, TickFrequency = 30 };
            _tbJoint1.ValueChanged += (s, e) => { lblJ1.Text = $"Joint 1 (Base Yaw): {_tbJoint1.Value}°"; UpdateRobotFromSliders(); };

            // Slider Joint 2
            var lblJ2 = new Label { Text = "Joint 2 (Shoulder Pitch): 30°", AutoSize = true, ForeColor = Color.FromArgb(180, 190, 210) };
            _tbJoint2 = new TrackBar { Minimum = -90, Maximum = 90, Value = 30, Width = 340, TickFrequency = 15 };
            _tbJoint2.ValueChanged += (s, e) => { lblJ2.Text = $"Joint 2 (Shoulder Pitch): {_tbJoint2.Value}°"; UpdateRobotFromSliders(); };

            // Slider Joint 3
            var lblJ3 = new Label { Text = "Joint 3 (Elbow Pitch): -20°", AutoSize = true, ForeColor = Color.FromArgb(180, 190, 210) };
            _tbJoint3 = new TrackBar { Minimum = -90, Maximum = 90, Value = -20, Width = 340, TickFrequency = 15 };
            _tbJoint3.ValueChanged += (s, e) => { lblJ3.Text = $"Joint 3 (Elbow Pitch): {_tbJoint3.Value}°"; UpdateRobotFromSliders(); };

            stack.Controls.Add(_lblRobotCoordinates);
            stack.Controls.Add(_lblSafetyStatus);
            stack.Controls.Add(lblJ1);
            stack.Controls.Add(_tbJoint1);
            stack.Controls.Add(lblJ2);
            stack.Controls.Add(_tbJoint2);
            stack.Controls.Add(lblJ3);
            stack.Controls.Add(_tbJoint3);

            panel.Controls.Add(stack);
            card.Controls.Add(panel);
            return card;
        }

        private void UpdateRobotFromSliders()
        {
            if (_tbJoint1 == null || _tbJoint2 == null || _tbJoint3 == null || _lblRobotCoordinates == null || _lblSafetyStatus == null) return;

            float j1 = _tbJoint1.Value;
            float j2 = _tbJoint2.Value;
            float j3 = _tbJoint3.Value;

            var fk = _robot.ComputeForwardKinematics(new float[] { j1, j2, j3, 0f, 0f, 0f });
            var pos = fk.EndEffectorPosition;

            _lblRobotCoordinates.Text = $"End-Effector: X={pos.X:F1} mm | Y={pos.Y:F1} mm | Z={pos.Z:F1} mm";

            // Safety exclusion zone check
            bool breach = _twinScene.CheckSafetyViolation(out string violatingNode, out var zone);
            if (breach)
            {
                _lblSafetyStatus.Text = $"🚨 SAFETY BREACH! Arm entered Danger Zone: {zone.Center}";
                _lblSafetyStatus.ForeColor = Color.FromArgb(255, 75, 75);
            }
            else
            {
                _lblSafetyStatus.Text = "✅ SAFETY STATUS: Workcell Clear (Nominal)";
                _lblSafetyStatus.ForeColor = Color.FromArgb(0, 255, 136);
            }
        }
        #endregion

        #region 3. AudioVisual Section (ZeroAudioVisual)
        private Control BuildAudioVisualSection()
        {
            var card = new ZeroCard
            {
                Dock = DockStyle.Fill,
                Title = "🔊 Acoustic PdM & Audio (ZeroAudioVisual)",
                Subtitle = "Vibration Statistical Metrics & Rolling Bearing Fault Diagnostic"
            };

            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
            var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoScroll = true };

            _lblRms = new Label { Text = "Vibration RMS: 0.000 g | Peak: 0.000 g", AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 229, 255) };
            _lblKurtosis = new Label { Text = "Kurtosis: 3.00 (Gaussian Baseline) | Crest Factor: 1.41", AutoSize = true, Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(180, 190, 210), Margin = new Padding(0, 4, 0, 8) };
            _lblBearingHealth = new Label { Text = "Diagnosis: Bearing Status Normal (1800 RPM)", AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(0, 255, 136), Margin = new Padding(0, 0, 0, 12) };

            var btnNormal = new Button
            {
                Text = "🟢 Normal Machinery Audio",
                Width = 200,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(0, 255, 136),
                BackColor = Color.FromArgb(30, 40, 50),
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnNormal.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 136);
            btnNormal.Click += (s, e) => SimulateNormalAcoustic();

            var btnFault = new Button
            {
                Text = "🔴 Inject BPFO Bearing Flaw",
                Width = 200,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(255, 90, 90),
                BackColor = Color.FromArgb(45, 30, 35),
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 6, 0, 0)
            };
            btnFault.FlatAppearance.BorderColor = Color.FromArgb(255, 90, 90);
            btnFault.Click += (s, e) => SimulateDefectAcoustic();

            stack.Controls.Add(_lblRms);
            stack.Controls.Add(_lblKurtosis);
            stack.Controls.Add(_lblBearingHealth);
            stack.Controls.Add(btnNormal);
            stack.Controls.Add(btnFault);

            panel.Controls.Add(stack);
            card.Controls.Add(panel);
            return card;
        }

        private void SimulateNormalAcoustic()
        {
            float[] data = new float[1024];
            var rnd = new Random(42);
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (float)(rnd.NextDouble() * 0.4 - 0.2); // Low Gaussian noise
            }
            _acousticBuffer.Write(data);
            var m = _acousticBuffer.ComputeMetrics();

            if (_lblRms != null) _lblRms.Text = $"Vibration RMS: {m.Rms:F3} g | Peak: {m.Peak:F3} g";
            if (_lblKurtosis != null) _lblKurtosis.Text = $"Kurtosis: {m.Kurtosis:F2} (Nominal) | Crest Factor: {m.CrestFactor:F2}";
            if (_lblBearingHealth != null)
            {
                _lblBearingHealth.Text = "Diagnosis: Bearing Status Nominal (No defect frequencies detected)";
                _lblBearingHealth.ForeColor = Color.FromArgb(0, 255, 136);
            }
        }

        private void SimulateDefectAcoustic()
        {
            float[] data = new float[1024];
            var freqs = BearingDefectDetector.CalculateFaultFrequencies(_bearing6205, 1800f);
            float bpfo = freqs.Bpfo; // ~107.5 Hz

            for (int i = 0; i < data.Length; i++)
            {
                // High-amplitude fault impact at BPFO frequency
                float impact = (float)Math.Sin(2 * Math.PI * bpfo * i / 16000.0) * 1.8f;
                data[i] = impact;
            }
            _acousticBuffer.Write(data);
            var m = _acousticBuffer.ComputeMetrics();

            if (_lblRms != null) _lblRms.Text = $"Vibration RMS: {m.Rms:F3} g | Peak: {m.Peak:F3} g";
            if (_lblKurtosis != null) _lblKurtosis.Text = $"Kurtosis: {m.Kurtosis:F2} (ELEVATED!) | Crest Factor: {m.CrestFactor:F2}";
            if (_lblBearingHealth != null)
            {
                _lblBearingHealth.Text = $"🚨 CRITICAL: Outer Race Defect (BPFO: {bpfo:F1} Hz) Detected!";
                _lblBearingHealth.ForeColor = Color.FromArgb(255, 75, 75);
            }
        }
        #endregion

        #region 4. Reports & IoT Section (ZeroReports & ZeroIoT)
        private Control BuildReportsIotSection()
        {
            var card = new ZeroCard
            {
                Dock = DockStyle.Fill,
                Title = "🏷️ Traceability & IoT (ZeroReports & ZeroIoT)",
                Subtitle = "Pure C# Vector PDF 1.4 | Thermal ZPL II / TSPL | MQTT 5.0"
            };

            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

            var btnPdf = new Button
            {
                Text = "📄 Generate Vector PDF Report",
                Width = 220,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(0, 229, 255),
                BackColor = Color.FromArgb(26, 36, 52),
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                Dock = DockStyle.Top,
                Cursor = Cursors.Hand
            };
            btnPdf.FlatAppearance.BorderColor = Color.FromArgb(0, 229, 255);
            btnPdf.Click += (s, e) => ExportSamplePdf();

            var btnZpl = new Button
            {
                Text = "🏷️ Generate Zebra ZPL II Label",
                Width = 220,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(0, 255, 136),
                BackColor = Color.FromArgb(26, 42, 34),
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                Dock = DockStyle.Top,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 6, 0, 8)
            };
            btnZpl.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 136);
            btnZpl.Click += (s, e) => GenerateZplLabel();

            _txtZplPreview = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(22, 26, 36),
                ForeColor = Color.FromArgb(200, 210, 225),
                Font = new Font("Consolas", 8f),
                ReadOnly = true,
                Text = "// ZPL II / TSPL Thermal Stream Preview..."
            };

            var pnlButtons = new Panel { Dock = DockStyle.Top, Height = 76 };
            pnlButtons.Controls.Add(btnZpl);
            pnlButtons.Controls.Add(btnPdf);

            panel.Controls.Add(_txtZplPreview);
            panel.Controls.Add(pnlButtons);

            card.Controls.Add(panel);
            return card;
        }

        private void ExportSamplePdf()
        {
            try
            {
                var report = new ReportDocument("ZERO PLATFORM INDUSTRIAL INSPECTION REPORT")
                {
                    Subtitle = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Engine: ZeroReports (PDF 1.4)"
                };

                var sec1 = report.AddSection("1. Workcell & Kinematics Telemetry");
                sec1.AddKeyValue("Machine Cell", "CNC-Alpha-09")
                    .AddKeyValue("Controller", "ZeroTwin3D 6-DOF DH Solver")
                    .AddKeyValue("Safety Status", "NOMINAL (Pass)")
                    .AddSpacer(12);

                var sec2 = report.AddSection("2. Part Quality Inspection (AOI)");
                sec2.AddTable(tbl =>
                {
                    tbl.AddColumn("Feature")
                       .AddColumn("Nominal")
                       .AddColumn("Measured")
                       .AddColumn("Result");

                    tbl.AddRow("Diameter Outer", "50.00 mm", "50.02 mm", "PASS");
                    tbl.AddRow("Surface Roughness", "0.80 um", "0.74 um", "PASS");
                    tbl.AddRow("Total Runout", "0.02 mm", "0.01 mm", "PASS");
                });

                byte[] pdfBytes = report.RenderToPdf();

                string tempPath = Path.Combine(Path.GetTempPath(), $"ZeroReport_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
                File.WriteAllBytes(tempPath, pdfBytes);

                MessageBox.Show($"Vector PDF 1.4 generated successfully ({pdfBytes.Length} bytes)!\nSaved to: {tempPath}", "ZeroReports", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PDF Generation failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void GenerateZplLabel()
        {
            var zpl = new ZplEncoder();
            zpl.SetDarkness(24)
               .SetPrintSpeed(6)
               .DrawBox(20, 20, 560, 360, borderThickness: 3)
               .DrawText(40, 40, "ZERO UNIVERSE WORK ORDER", fontHeight: 28, fontWidth: 28)
               .DrawLine(40, 80, 520, thickness: 2)
               .DrawBarcode128(40, 100, "WO-2026-LOT991", height: 70)
               .DrawQrCode(380, 100, "https://zerouniverse.internal/p/991", magnification: 4)
               .DrawText(40, 200, "Part: Quantum Microcontroller", fontHeight: 22, fontWidth: 22)
               .DrawText(40, 235, "Station: Line 4 / Inspection Pass", fontHeight: 20, fontWidth: 20)
               .EndLabel();

            if (_txtZplPreview != null)
            {
                _txtZplPreview.Text = zpl.ToZplString();
            }
        }
        #endregion
    }
}
