using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroGraphics.Core.Telemetry;
using ZeroGraphics.DirectX.Controls;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Waveform.Controls;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Graph;
using ZeroPipeline.Core.Nodes;
using ZeroPipeline.Nodes.Inspection;
using ZeroPipeline.Nodes.Storage;
using ZeroPipeline.Nodes.Vision;
using ZeroPipeline.UI.Models;
using ZeroPipeline.UI.Studio;
using ZeroUI.WinForms.Industrial;
using ZeroUI.WinForms.Layout;
using ZeroUI.WinForms.Theme;

namespace ZeroPlatform.Samples.Showcase
{
    public sealed class ShowcaseForm : Form
    {
        private readonly System.Windows.Forms.Timer _waveformTimer;
        private readonly float[] _buffer = new float[50000];
        private float _phase = 0f;
        private ZeroWaveformCanvas? _waveformCanvas;

        // ZeroPipeline Integration
        private PipelineExecutor? _pipelineExecutor;
        private InspectionResult? _lastInspectionResult;
        private TimeSeriesLogSinkNode? _tsdbLogger;
        private ZeroDescriptions? _descPipeline;
        private int _pipelineCycleCount = 0;
        private ZeroPipelineStudioControl? _studioControl;

        public ShowcaseForm()
        {
            Text = "ZeroPlatform - Unified Enterprise UI & Hardware Graphics Showcase";
            Size = new Size(1280, 800);
            MinimumSize = new Size(1024, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(16, 18, 24);
            ForeColor = Color.FromArgb(220, 225, 235);

            // Initialize GPU
            D3D11DeviceManager.EnsureInitialized();

            // Build Live Pipeline Workflow
            BuildDemoPipeline();

            // Header Banner
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Color.FromArgb(22, 26, 36),
                Padding = new Padding(20, 10, 20, 10)
            };

            var lblTitle = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 229, 255),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = $"🌟 ZeroPlatform Ecosystem | {GpuCapabilities.AdapterName} ({GpuCapabilities.DedicatedVramMb:F0} MB VRAM) | ZeroUI + ZeroGraphics + ZeroPipeline"
            };
            header.Controls.Add(lblTitle);
            Controls.Add(header);

            // Main Layout Splitter
            var splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                SplitterDistance = 380,
                BackColor = Color.FromArgb(30, 36, 50)
            };

            // Left Side: ZeroUI Industrial Controls
            var leftPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 21, 28),
                Padding = new Padding(16),
                AutoScroll = true
            };

            var stack = new ZeroStackPanel
            {
                Dock = DockStyle.Top,
                Orientation = StackOrientation.Vertical,
                Spacing = 16,
                AutoSize = true
            };

            // 1. Platform Subsystems
            var cardSys = new ZeroCard
            {
                Title = "Platform Subsystems",
                Subtitle = "11 Core Repositories & Satellites",
                Width = 340,
                Height = 150
            };

            var descSys = new ZeroDescriptions
            {
                Dock = DockStyle.Fill,
                Columns = 1,
                RowHeight = 24,
                LabelColor = Color.FromArgb(150, 160, 180),
                ValueColor = Color.FromArgb(220, 225, 235)
            };
            descSys.Add("Subsystems", "11 Pure C# Engines");
            descSys.Add("Pipeline Engine", "ZeroPipeline.Core (DAG)");
            descSys.Add("Graphics Core", "ZeroGraphics (DX11 + D2D)");
            descSys.Add("Storage Core", "ZeroStorage (Gorilla TSDB)");
            cardSys.Controls.Add(descSys);
            stack.Controls.Add(cardSys);

            // 2. Hardware Telemetry Card
            var descCard = new ZeroCard
            {
                Title = "DXGI Hardware Telemetry",
                Subtitle = "Live GPU & Acceleration Metrics",
                Width = 340,
                Height = 190
            };

            var descGpu = new ZeroDescriptions
            {
                Dock = DockStyle.Fill,
                Columns = 1,
                RowHeight = 24,
                LabelColor = Color.FromArgb(150, 160, 180),
                ValueColor = Color.FromArgb(0, 229, 255)
            };
            descGpu.Add("GPU Adapter", GpuCapabilities.AdapterName);
            descGpu.Add("Hardware Tier", GpuCapabilities.CurrentTier.ToString());
            descGpu.Add("Dedicated VRAM", $"{GpuCapabilities.DedicatedVramMb:F0} MB");
            descGpu.Add("Rendering Mode", "Direct COM VTable P/Invoke");

            descCard.Controls.Add(descGpu);
            stack.Controls.Add(descCard);

            // 3. ZeroPipeline Live Workflow Card
            var pipelineCard = new ZeroCard
            {
                Title = "ZeroPipeline Live Workflow",
                Subtitle = "Real-Time AOI DAG & TSDB Logger",
                Width = 340,
                Height = 210
            };

            _descPipeline = new ZeroDescriptions
            {
                Dock = DockStyle.Fill,
                Columns = 1,
                RowHeight = 24,
                LabelColor = Color.FromArgb(150, 160, 180),
                ValueColor = Color.FromArgb(0, 255, 136)
            };
            _descPipeline.Add("Recipe", "AOI Metrology DAG (v1.0)");
            _descPipeline.Add("DAG Order", "5 Nodes (Kahn Sort)");
            _descPipeline.Add("Cycles Run", "0");
            _descPipeline.Add("Latest Part", "30.00 mm (PASSED)", Color.FromArgb(0, 255, 136));
            _descPipeline.Add("TSDB Points", "0 (Gorilla XOR)");

            pipelineCard.Controls.Add(_descPipeline);
            stack.Controls.Add(pipelineCard);

            // 4. Industrial Satellites Expansion Card
            var satCard = new ZeroCard
            {
                Title = "Satellites Subsystem",
                Subtitle = "5 Active Industrial Expansion Clusters",
                Width = 340,
                Height = 175
            };
            var descSat = new ZeroDescriptions
            {
                Dock = DockStyle.Fill,
                Columns = 1,
                RowHeight = 24,
                LabelColor = Color.FromArgb(150, 160, 180),
                ValueColor = Color.FromArgb(0, 229, 255)
            };
            descSat.Add("IoT Protocols", "OPC-UA + MQTT 5.0 + Sparkplug");
            descSat.Add("High-Speed Charts", "Direct3D 11 (LTTB 10M+ Pts)");
            descSat.Add("Traceability & Reports", "Pure C# Vector PDF 1.4 + ZPL");
            descSat.Add("Acoustic PdM & Twin3D", "STFT Spectrogram + 6-DOF DH");
            satCard.Controls.Add(descSat);
            stack.Controls.Add(satCard);

            leftPanel.Controls.Add(stack);
            splitMain.Panel1.Controls.Add(leftPanel);

            // Right Side View Switcher & Container Host
            var rightHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(16, 18, 24)
            };

            var viewSwitcher = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Color.FromArgb(22, 26, 36),
                Padding = new Padding(6, 4, 6, 4)
            };

            var btnGpu = new Button
            {
                Text = "🎨 GPU Hardware & Waveforms",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(0, 229, 255),
                BackColor = Color.FromArgb(36, 45, 64),
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                Dock = DockStyle.Left,
                Width = 220,
                Cursor = Cursors.Hand
            };
            btnGpu.FlatAppearance.BorderColor = Color.FromArgb(0, 229, 255);

            var btnStudio = new Button
            {
                Text = "🔗 ZeroPipeline Studio",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(180, 190, 210),
                BackColor = Color.FromArgb(26, 32, 44),
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                Dock = DockStyle.Left,
                Width = 180,
                Cursor = Cursors.Hand
            };
            btnStudio.FlatAppearance.BorderColor = Color.FromArgb(50, 60, 80);

            var btnSatellites = new Button
            {
                Text = "🛰️ Satellites Hub",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(180, 190, 210),
                BackColor = Color.FromArgb(26, 32, 44),
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                Dock = DockStyle.Left,
                Width = 180,
                Cursor = Cursors.Hand
            };
            btnSatellites.FlatAppearance.BorderColor = Color.FromArgb(50, 60, 80);

            viewSwitcher.Controls.Add(btnSatellites);
            viewSwitcher.Controls.Add(btnStudio);
            viewSwitcher.Controls.Add(btnGpu);

            var contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(16, 18, 24)
            };

            // View 1: ZeroGraphics Hardware Accelerators (SDF Card + Real-Time Oscilloscope)
            var splitGraphics = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 340,
                BackColor = Color.FromArgb(30, 36, 50)
            };

            // DirectX 11 Analytical SDF Card
            var sdfCanvas = new ZeroDirectXCanvas
            {
                Dock = DockStyle.Fill,
                Elevation = 14f,
                BlurRadius = 26f,
                CornerRadius = 18f,
                BorderWidth = 1.5f,
                GlowIntensity = 0.7f,
                CardColor = Color.FromArgb(24, 29, 42)
            };
            splitGraphics.Panel1.Controls.Add(sdfCanvas);

            // High-Speed Oscilloscope Waveform (50k points streaming at 60 FPS)
            _waveformCanvas = new ZeroWaveformCanvas
            {
                Dock = DockStyle.Fill,
                AutoScale = true,
                TraceColor = Color.FromArgb(0, 255, 136)
            };
            splitGraphics.Panel2.Controls.Add(_waveformCanvas);

            // View 2: ZeroPipeline Visual Node Studio
            _studioControl = new ZeroPipelineStudioControl
            {
                Dock = DockStyle.Fill,
                Visible = false
            };

            // Populate sample demo DAG
            PopulateStudioDemoGraph(_studioControl);
            if (_pipelineExecutor != null)
            {
                _studioControl.AttachExecutor(_pipelineExecutor);
            }

            // View 3: Industrial Satellites Hub
            var satellitesView = new SatellitesHubView
            {
                Dock = DockStyle.Fill,
                Visible = false
            };

            contentHost.Controls.Add(splitGraphics);
            contentHost.Controls.Add(_studioControl);
            contentHost.Controls.Add(satellitesView);

            void SelectView(Button activeBtn, Control activeControl)
            {
                splitGraphics.Visible = false;
                _studioControl.Visible = false;
                satellitesView.Visible = false;

                activeControl.Visible = true;
                activeControl.BringToFront();

                foreach (var b in new[] { btnGpu, btnStudio, btnSatellites })
                {
                    if (b == activeBtn)
                    {
                        b.ForeColor = Color.FromArgb(0, 229, 255);
                        b.BackColor = Color.FromArgb(36, 45, 64);
                        b.FlatAppearance.BorderColor = Color.FromArgb(0, 229, 255);
                    }
                    else
                    {
                        b.ForeColor = Color.FromArgb(180, 190, 210);
                        b.BackColor = Color.FromArgb(26, 32, 44);
                        b.FlatAppearance.BorderColor = Color.FromArgb(50, 60, 80);
                    }
                }
            }

            btnGpu.Click += (s, e) => SelectView(btnGpu, splitGraphics);

            btnStudio.Click += (s, e) =>
            {
                SelectView(btnStudio, _studioControl);
                _studioControl.Canvas.ZoomToFit();
            };

            btnSatellites.Click += (s, e) => SelectView(btnSatellites, satellitesView);

            rightHost.Controls.Add(contentHost);
            rightHost.Controls.Add(viewSwitcher);

            splitMain.Panel2.Controls.Add(rightHost);
            Controls.Add(splitMain);
            splitMain.BringToFront();

            // 60 FPS Stream Timer
            _waveformTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _waveformTimer.Tick += OnStreamTick;
            _waveformTimer.Start();
        }

        private void BuildDemoPipeline()
        {
            var graph = new PipelineGraph();

            var source = new ImageSourceNode(seq =>
            {
                // Sine wave variation around 30.0mm (+- 1.8mm)
                double width = 30.0 + Math.Sin(seq * 0.15) * 1.8;
                return CreateSyntheticFrame(width);
            }, "CameraSource");

            var gray = new ImageGrayscaleNode("GrayscaleConverter");
            var caliper = new EdgeCaliperNode(10, 30, 90, 30, 20.0, name: "CaliperRake");
            var judge = new DimensionJudgeNode("PinWidth", 30.0, 28.5, 31.5, name: "JudgePin");
            _tsdbLogger = new TimeSeriesLogSinkNode(metricId: 1, blockSize: 10, name: "TSDB");
            var sink = new ActionSinkNode<InspectionResult>(r => _lastInspectionResult = r, "UIConsumer");

            graph.Connect(source.Output, gray.Input);
            graph.Connect(gray.Output, caliper.Input);
            graph.Connect(caliper.Output, judge.Input);
            graph.Connect(judge.Output, _tsdbLogger.Input);
            graph.Connect(judge.Output, sink.Input);

            _pipelineExecutor = new PipelineExecutor(graph);
            _pipelineExecutor.InitializeAsync().GetAwaiter().GetResult();
        }

        private static ImageBuffer CreateSyntheticFrame(double stripeWidth)
        {
            var image = ImageBuffer.CreateBgra32(100, 60);
            int startX = 20;
            int endX = (int)Math.Round(startX + stripeWidth);

            unsafe
            {
                for (int y = 0; y < 60; y++)
                {
                    byte* row = image.GetRowPointer(y);
                    for (int x = 0; x < 100; x++)
                    {
                        byte val = (x >= startX && x <= endX) ? (byte)230 : (byte)25;
                        int offset = x * 4;
                        row[offset] = val;
                        row[offset + 1] = val;
                        row[offset + 2] = val;
                        row[offset + 3] = 255;
                    }
                }
            }
            return image;
        }

        private void OnStreamTick(object? sender, EventArgs e)
        {
            // 1. Update Waveform
            if (_waveformCanvas != null && _waveformCanvas.Visible)
            {
                _phase += 0.1f;
                for (int i = 0; i < _buffer.Length; i++)
                {
                    float t = i + _phase * 40f;
                    _buffer[i] = (float)(Math.Sin(t * 0.003) * 40.0 + Math.Cos(t * 0.02) * 15.0);
                }
                _waveformCanvas.SetData(_buffer);
            }

            // 2. Step ZeroPipeline Execution Engine
            if (_pipelineExecutor != null && _descPipeline != null)
            {
                _pipelineExecutor.ExecuteStepAsync().GetAwaiter().GetResult();
                _pipelineCycleCount++;

                if (_lastInspectionResult != null)
                {
                    var color = _lastInspectionResult.IsPassed ? Color.FromArgb(0, 255, 136) : Color.FromArgb(255, 80, 80);
                    string statusText = $"{_lastInspectionResult.MeasuredValue:F2} mm ({(_lastInspectionResult.IsPassed ? "PASSED" : "FAILED")})";

                    _descPipeline.SetValue("Cycles Run", _pipelineCycleCount.ToString());
                    _descPipeline.SetValue("Latest Part", statusText, color);
                    _descPipeline.SetValue("TSDB Points", $"{_tsdbLogger?.TotalLoggedPoints ?? 0} (Gorilla XOR)");
                }
            }
        }

        private static void PopulateStudioDemoGraph(ZeroPipelineStudioControl studio)
        {
            var cam = new CanvasNode("CameraSource", "Camera Source", "SyntheticCameraNode", "Vision", 50f, 100f);
            cam.AddOutputPin("Frame", typeof(byte[]));
            cam.AddOutputPin("FrameIndex", typeof(long));
            cam.Properties["FrameRate"] = "60";

            var thresh = new CanvasNode("GrayscaleConverter", "Gray Converter", "ImageThresholdNode", "Vision", 320f, 100f);
            thresh.AddInputPin("SourceImage", typeof(byte[]));
            thresh.AddOutputPin("BinaryImage", typeof(byte[]));
            thresh.Properties["Threshold"] = "128";

            var caliper = new CanvasNode("CaliperRake", "Edge Caliper", "MetrologyCaliperNode", "Inspection", 590f, 100f);
            caliper.AddInputPin("Image", typeof(byte[]));
            caliper.AddOutputPin("Measurement", typeof(InspectionResult));
            caliper.Properties["ExpectedWidthMm"] = "30.0";
            caliper.Properties["ToleranceMm"] = "1.5";

            var tsdb = new CanvasNode("TSDB", "TSDB Storage", "TsdbStorageNode", "Storage", 860f, 100f);
            tsdb.AddInputPin("Record", typeof(InspectionResult));
            tsdb.Properties["RetentionDays"] = "30";

            studio.Canvas.AddNode(cam);
            studio.Canvas.AddNode(thresh);
            studio.Canvas.AddNode(caliper);
            studio.Canvas.AddNode(tsdb);

            studio.Canvas.Connect(cam.Outputs[0], thresh.Inputs[0]);
            studio.Canvas.Connect(thresh.Outputs[0], caliper.Inputs[0]);
            studio.Canvas.Connect(caliper.Outputs[0], tsdb.Inputs[0]);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _waveformTimer.Stop();
                _waveformTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
