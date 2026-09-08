using System;
using System.Drawing;
using System.Windows.Forms;
using ZeroGraphics.Core.Telemetry;
using ZeroGraphics.DirectX.Controls;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.Waveform.Controls;
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
                Text = $"🌟 ZeroPlatform Ecosystem | {GpuCapabilities.AdapterName} ({GpuCapabilities.DedicatedVramMb:F0} MB VRAM) | ZeroUI Controls + ZeroGraphics DirectX"
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

            var cardSys = new ZeroCard
            {
                Title = "Platform Subsystems",
                Subtitle = "Active Core Repositories & Satellites",
                Width = 340,
                Height = 120
            };
            stack.Controls.Add(cardSys);

            var descCard = new ZeroCard
            {
                Title = "DXGI Hardware Telemetry",
                Subtitle = "Live GPU & Acceleration Metrics",
                Width = 340,
                Height = 220
            };

            var descGpu = new ZeroDescriptions
            {
                Dock = DockStyle.Fill,
                Columns = 1,
                RowHeight = 26,
                LabelColor = Color.FromArgb(150, 160, 180),
                ValueColor = Color.FromArgb(0, 229, 255)
            };
            descGpu.Add("GPU Adapter", GpuCapabilities.AdapterName);
            descGpu.Add("Hardware Tier", GpuCapabilities.CurrentTier.ToString());
            descGpu.Add("Dedicated VRAM", $"{GpuCapabilities.DedicatedVramMb:F0} MB");
            descGpu.Add("Rendering Mode", "Direct COM VTable P/Invoke");
            descGpu.Add("External Libs", "0 (Pure ZeroPlatform)");

            descCard.Controls.Add(descGpu);
            stack.Controls.Add(descCard);

            leftPanel.Controls.Add(stack);
            splitMain.Panel1.Controls.Add(leftPanel);

            // Right Side: ZeroGraphics Hardware Accelerators (SDF Card + Real-Time Oscilloscope)
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

            splitMain.Panel2.Controls.Add(splitGraphics);
            Controls.Add(splitMain);
            splitMain.BringToFront();

            // 60 FPS Stream Timer
            _waveformTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _waveformTimer.Tick += OnStreamTick;
            _waveformTimer.Start();
        }

        private void OnStreamTick(object? sender, EventArgs e)
        {
            if (_waveformCanvas == null || !_waveformCanvas.Visible) return;

            _phase += 0.1f;
            for (int i = 0; i < _buffer.Length; i++)
            {
                float t = i + _phase * 40f;
                _buffer[i] = (float)(Math.Sin(t * 0.003) * 40.0 + Math.Cos(t * 0.02) * 15.0);
            }
            _waveformCanvas.SetData(_buffer);
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
