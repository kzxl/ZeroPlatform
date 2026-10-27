using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ZeroGraphics.Core.Telemetry;
using ZeroGraphics.Direct2D.Controls;
using ZeroGraphics.DirectX.Controls;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.Imaging.Controls;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Gpu;
using ZeroGraphics.Vision.Codes;
using ZeroGraphics.Waveform.Controls;

namespace ZeroGraphics.Samples.Demo
{
    public sealed class MainDemoForm : Form
    {
        private readonly TabControl _tabControl;
        private readonly System.Windows.Forms.Timer _waveformTimer;
        private readonly float[] _waveformBuffer = new float[100000];
        private float _wavePhase = 0f;
        private ZeroWaveformCanvas? _waveformCanvas;
        private ZeroDirectXCanvas? _directXCanvas;
        private ZeroDirect2DCanvas? _direct2DCanvas;

        // Camera Demo
        private ZeroCameraCanvas? _cameraCanvas;
        private System.Windows.Forms.Timer? _cameraTimer;
        private IntPtr _cameraFrameMem = IntPtr.Zero;
        private readonly int _camWidth = 1408;
        private readonly int _camHeight = 1024;
        private int _camStride;
        private Label? _lblCameraFps;
        private float _conveyorOffset = 0f;
        private bool[,] _demoQrGrid = new bool[0, 0];
        private int _demoQrDim = 0;

        public MainDemoForm()
        {
            Text = "ZeroGraphics - High-Performance Hardware Graphics Ecosystem Demo";
            Size = new Size(1100, 750);
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(16, 18, 24);
            ForeColor = Color.FromArgb(220, 225, 235);

            // Initialize GPU Hardware & Telemetry
            D3D11DeviceManager.EnsureInitialized();

            // Top Status Panel (GPU Telemetry)
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(22, 26, 36),
                Padding = new Padding(16, 8, 16, 8)
            };

            var lblGpuInfo = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 229, 255),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = $"[GPU Adapter] {GpuCapabilities.AdapterName} | Tier: {GpuCapabilities.CurrentTier} | VRAM: {GpuCapabilities.DedicatedVramMb:F0} MB | D3D11 Direct VTable P/Invoke"
            };
            topPanel.Controls.Add(lblGpuInfo);
            Controls.Add(topPanel);

            // Tab Control
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f)
            };

            // Build Tabs
            BuildCameraTab();
            BuildDirectXTab();
            BuildWaveformTab();
            BuildDirect2DTab();

            Controls.Add(_tabControl);
            _tabControl.BringToFront();

            // High-resolution Waveform Animation Timer (60 FPS)
            _waveformTimer = new System.Windows.Forms.Timer
            {
                Interval = 16
            };
            _waveformTimer.Tick += OnWaveformTick;
            _waveformTimer.Start();
        }

        private void BuildDirectXTab()
        {
            var tab = new TabPage("Direct3D 11 SDF Cards & Glow");
            tab.BackColor = Color.FromArgb(16, 18, 24);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel2,
                SplitterDistance = 750,
                BackColor = Color.FromArgb(26, 32, 44)
            };

            _directXCanvas = new ZeroDirectXCanvas
            {
                Dock = DockStyle.Fill,
                Elevation = 10f,
                BlurRadius = 20f,
                CornerRadius = 16f,
                BorderWidth = 1.5f,
                GlowIntensity = 0.5f,
                CardColor = Color.FromArgb(28, 34, 48)
            };
            split.Panel1.Controls.Add(_directXCanvas);

            var controlPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(22, 26, 36),
                Padding = new Padding(12),
                AutoScroll = true
            };

            int y = 10;

            void AddSlider(string labelText, int min, int max, int value, Action<int> onChanged)
            {
                var lbl = new Label
                {
                    Text = $"{labelText}: {value}",
                    Location = new Point(12, y),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(200, 210, 225),
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold)
                };
                y += 22;

                var track = new TrackBar
                {
                    Minimum = min,
                    Maximum = max,
                    Value = value,
                    TickFrequency = (max - min) / 10 > 0 ? (max - min) / 10 : 1,
                    Location = new Point(12, y),
                    Width = 240
                };
                track.ValueChanged += (s, e) =>
                {
                    lbl.Text = $"{labelText}: {track.Value}";
                    onChanged(track.Value);
                };
                y += 45;

                controlPanel.Controls.Add(lbl);
                controlPanel.Controls.Add(track);
            }

            AddSlider("Corner Radius", 0, 60, 16, v => { if (_directXCanvas != null) _directXCanvas.CornerRadius = v; });
            AddSlider("Elevation (Offset)", 0, 40, 10, v => { if (_directXCanvas != null) _directXCanvas.Elevation = v; });
            AddSlider("Shadow Blur Radius", 0, 60, 20, v => { if (_directXCanvas != null) _directXCanvas.BlurRadius = v; });
            AddSlider("Neon Glow Intensity", 0, 20, 5, v => { if (_directXCanvas != null) _directXCanvas.GlowIntensity = v / 10.0f; });

            var chkVsync = new CheckBox
            {
                Text = "Enable Hardware VSync",
                Location = new Point(12, y),
                AutoSize = true,
                Checked = false,
                ForeColor = Color.FromArgb(200, 210, 225)
            };
            chkVsync.CheckedChanged += (s, e) =>
            {
                if (_directXCanvas != null) _directXCanvas.Vsync = chkVsync.Checked;
            };
            controlPanel.Controls.Add(chkVsync);

            split.Panel2.Controls.Add(controlPanel);
            tab.Controls.Add(split);
            _tabControl.TabPages.Add(tab);
        }

        private void BuildWaveformTab()
        {
            var tab = new TabPage("Real-Time Waveform (100k Pts @ 60 FPS)");
            tab.BackColor = Color.FromArgb(16, 18, 24);

            _waveformCanvas = new ZeroWaveformCanvas
            {
                Dock = DockStyle.Fill,
                AutoScale = true,
                TraceColor = Color.FromArgb(0, 255, 136)
            };

            var bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                BackColor = Color.FromArgb(22, 26, 36),
                Padding = new Padding(16, 8, 16, 8)
            };

            var lblInfo = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(0, 255, 136),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "⚡ Streaming 100,000 live data points | D3D11 LineStrip Topology | Native GPU Geometry Pipeline"
            };
            bottomBar.Controls.Add(lblInfo);

            tab.Controls.Add(_waveformCanvas);
            tab.Controls.Add(bottomBar);
            _tabControl.TabPages.Add(tab);
        }

        private void BuildDirect2DTab()
        {
            var tab = new TabPage("Direct2D Subpixel ClearType");
            tab.BackColor = Color.FromArgb(16, 18, 24);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 80,
                BackColor = Color.FromArgb(26, 32, 44)
            };

            var topBar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(22, 26, 36),
                Padding = new Padding(16, 12, 16, 12)
            };

            var lbl = new Label
            {
                Text = "Interactive DirectWrite Sample Text:",
                Location = new Point(16, 12),
                AutoSize = true,
                ForeColor = Color.FromArgb(200, 210, 225)
            };

            var txtInput = new TextBox
            {
                Text = "ZeroGraphics DirectWrite: Subpixel ClearType & Per-Monitor V2 High-DPI Crispness (Zero GDI Scaling Artifacts)",
                Location = new Point(16, 36),
                Width = 750,
                Font = new Font("Segoe UI", 10f)
            };
            txtInput.TextChanged += (s, e) =>
            {
                if (_direct2DCanvas != null) _direct2DCanvas.SampleText = txtInput.Text;
            };

            topBar.Controls.Add(lbl);
            topBar.Controls.Add(txtInput);
            split.Panel1.Controls.Add(topBar);

            _direct2DCanvas = new ZeroDirect2DCanvas
            {
                Dock = DockStyle.Fill,
                TextFontFamily = "Segoe UI",
                TextSize = 18f,
                SampleText = txtInput.Text
            };
            split.Panel2.Controls.Add(_direct2DCanvas);

            tab.Controls.Add(split);
            _tabControl.TabPages.Add(tab);
        }

        private unsafe void BuildCameraTab()
        {
            var tab = new TabPage("Industrial Camera & Inspection (Zero-Alloc Flip Model)");
            tab.BackColor = Color.FromArgb(16, 18, 24);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel2,
                SplitterDistance = 750,
                BackColor = Color.FromArgb(26, 32, 44)
            };

            // Allocate unmanaged camera buffer (1408 x 1024 Mono8)
            _camStride = ((_camWidth * 1) + 3) & ~3;
            _cameraFrameMem = Marshal.AllocHGlobal(_camStride * _camHeight);

            // Pre-generate QR code for demo
            _demoQrGrid = QrEncoder.EncodeSymbol("http://ndatrace.vn/02/8935217402737/02/hp9vm3mzoio", QrErrorCorrectionLevel.M);
            _demoQrDim = _demoQrGrid.GetLength(0);

            _cameraCanvas = new ZeroCameraCanvas
            {
                Dock = DockStyle.Fill,
                ZoomMode = CameraZoomMode.Fit,
                BackColor = Color.FromArgb(12, 14, 18)
            };
            split.Panel1.Controls.Add(_cameraCanvas);

            // Right Settings Panel
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(16),
                BackColor = Color.FromArgb(20, 24, 34)
            };

            var lblTitle = new Label
            {
                Text = "⚡ Hikrobot Smart Camera MV-SCC\nHigh-Speed Inspection Controls",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 229, 255),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 16)
            };
            panel.Controls.Add(lblTitle);

            _lblCameraFps = new Label
            {
                Text = "Resolution: 1408 x 1024 | Mono8\nPipeline: Zero-Alloc DMA R8_UNORM\nRender FPS: 60.0 | Frames: 0",
                Font = new Font("Consolas", 9.5f),
                ForeColor = Color.FromArgb(0, 255, 128),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 16)
            };
            panel.Controls.Add(_lblCameraFps);

            var btnResetView = new Button
            {
                Text = "Reset View (Zoom Fit)",
                Width = 240,
                Height = 36,
                BackColor = Color.FromArgb(32, 40, 56),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnResetView.Click += (s, e) => _cameraCanvas.ResetView();
            panel.Controls.Add(btnResetView);

            var btnOriginal = new Button
            {
                Text = "Zoom 100% (1:1 Pixel)",
                Width = 240,
                Height = 36,
                BackColor = Color.FromArgb(32, 40, 56),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 8, 0, 8)
            };
            btnOriginal.Click += (s, e) => { _cameraCanvas.ZoomFactor = 1.0f; };
            panel.Controls.Add(btnOriginal);

            var chkPointFilter = new CheckBox
            {
                Text = "Point Filter (Pixel Inspection)",
                ForeColor = Color.White,
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 8)
            };
            chkPointFilter.CheckedChanged += (s, e) => _cameraCanvas.UsePointFilter = chkPointFilter.Checked;
            panel.Controls.Add(chkPointFilter);

            var chkVsync = new CheckBox
            {
                Text = "Hardware VSync",
                ForeColor = Color.White,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 16)
            };
            chkVsync.CheckedChanged += (s, e) => _cameraCanvas.Vsync = chkVsync.Checked;
            panel.Controls.Add(chkVsync);

            var lblDecoded = new Label
            {
                Text = "Decoded Result (from Chunk Data):\nhttp://ndatrace.vn/02/8935217402737/02/hp9vm3mzoio",
                Font = new Font("Consolas", 8.5f),
                ForeColor = Color.FromArgb(240, 190, 80),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 16)
            };
            panel.Controls.Add(lblDecoded);

            var lblHelp = new Label
            {
                Text = "💡 Interactive Controls:\n- Mouse Wheel: Zoom in/out at cursor\n- Mouse Drag: Pan across high-res image\n- Double Click: Toggle Fit / 100%",
                ForeColor = Color.FromArgb(160, 170, 190),
                AutoSize = true
            };
            panel.Controls.Add(lblHelp);

            split.Panel2.Controls.Add(panel);
            tab.Controls.Add(split);
            _tabControl.TabPages.Add(tab);

            // 60 FPS Camera Acquisition Simulation Timer
            _cameraTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _cameraTimer.Tick += OnCameraTick;
            _cameraTimer.Start();
        }

        private unsafe void OnCameraTick(object? sender, EventArgs e)
        {
            if (_cameraCanvas == null || !_cameraCanvas.Visible || _cameraFrameMem == IntPtr.Zero)
                return;

            _conveyorOffset += 3.0f;
            if (_conveyorOffset > 300f) _conveyorOffset = -300f;

            byte* pBytes = (byte*)_cameraFrameMem;

            // Clear background (conveyor belt = 30)
            int totalBytes = _camStride * _camHeight;
            new Span<byte>(pBytes, totalBytes).Fill(30);

            // Draw simulated part body [left..right, top..bottom]
            int partW = 600;
            int partH = 500;
            int partX = (int)((_camWidth - partW) / 2 + _conveyorOffset);
            int partY = (_camHeight - partH) / 2;

            for (int y = Math.Max(0, partY); y < Math.Min(_camHeight, partY + partH); y++)
            {
                byte* row = pBytes + y * _camStride;
                for (int x = Math.Max(0, partX); x < Math.Min(_camWidth, partX + partW); x++)
                {
                    row[x] = 180; // Metallic package background
                }
            }

            // Draw QR code onto part
            if (_demoQrDim > 0)
            {
                int modSize = 7;
                int qzBorder = 4;
                int qrTotalSize = (_demoQrDim + qzBorder * 2) * modSize;
                int qrStartX = partX + 80;
                int qrStartY = partY + 80;

                // Quiet zone
                for (int y = Math.Max(0, qrStartY); y < Math.Min(_camHeight, qrStartY + qrTotalSize); y++)
                {
                    byte* row = pBytes + y * _camStride;
                    for (int x = Math.Max(0, qrStartX); x < Math.Min(_camWidth, qrStartX + qrTotalSize); x++)
                    {
                        row[x] = 250;
                    }
                }

                // QR modules
                for (int r = 0; r < _demoQrDim; r++)
                {
                    for (int c = 0; c < _demoQrDim; c++)
                    {
                        byte color = _demoQrGrid[r, c] ? (byte)10 : (byte)250;
                        int modX = qrStartX + (c + qzBorder) * modSize;
                        int modY = qrStartY + (r + qzBorder) * modSize;

                        for (int py = 0; py < modSize; py++)
                        {
                            int curY = modY + py;
                            if (curY >= 0 && curY < _camHeight)
                            {
                                byte* row = pBytes + curY * _camStride;
                                for (int px = 0; px < modSize; px++)
                                {
                                    int curX = modX + px;
                                    if (curX >= 0 && curX < _camWidth)
                                    {
                                        row[curX] = color;
                                    }
                                }
                            }
                        }
                    }
                }

                // Push unmanaged frame directly to GPU Flip Model SwapChain
                _cameraCanvas.SetFrame(_cameraFrameMem, _camWidth, _camHeight, ImageFormatMode.Gray8, _camStride);

                // Update Bounding Box overlay tracking moving part
                _cameraCanvas.SetOverlays(new[]
                {
                    new CameraOverlayBox(
                        new RectangleF(qrStartX - 10, qrStartY - 10, qrTotalSize + 20, qrTotalSize + 20),
                        Color.FromArgb(0, 255, 128),
                        thickness: 2.5f,
                        label: "PASS [QR OK]")
                });

                if (_lblCameraFps != null && _cameraCanvas.FrameCount % 30 == 0)
                {
                    _lblCameraFps.Text = $"Resolution: {_camWidth} x {_camHeight} | Mono8\nPipeline: Zero-Alloc DMA R8_UNORM\nRender FPS: {_cameraCanvas.RenderFps:F1} | Frames: {_cameraCanvas.FrameCount}";
                }
            }
        }

        private void OnWaveformTick(object? sender, EventArgs e)
        {
            if (_waveformCanvas == null || !_waveformCanvas.Visible)
                return;

            _wavePhase += 0.08f;
            float freq1 = 0.002f;
            float freq2 = 0.015f;

            for (int i = 0; i < _waveformBuffer.Length; i++)
            {
                float t = i + _wavePhase * 50f;
                _waveformBuffer[i] = (float)(Math.Sin(t * freq1) * 35.0 + Math.Sin(t * freq2) * 12.0);
            }

            _waveformCanvas.SetData(_waveformBuffer);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cameraTimer?.Stop();
                _cameraTimer?.Dispose();
                if (_cameraFrameMem != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_cameraFrameMem);
                    _cameraFrameMem = IntPtr.Zero;
                }
                _waveformTimer.Stop();
                _waveformTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
