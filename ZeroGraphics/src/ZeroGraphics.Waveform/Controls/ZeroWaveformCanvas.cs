using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Pipeline;
using ZeroGraphics.Waveform.Pipeline;

namespace ZeroGraphics.Waveform.Controls
{
    /// <summary>
    /// Ultra-high-speed real-time oscilloscope and telemetry waveform control for Windows Forms.
    /// Plots streaming sensor series (up to 10,000,000 points) via Direct3D 11 hardware LineStrip rendering.
    /// </summary>
    [ToolboxItem(true)]
    public class ZeroWaveformCanvas : Control
    {
        private HwndSwapChain? _swapChain;
        private WaveformPipeline? _pipeline;
        private float[]? _dataPoints;
        private float _minY = -1.0f;
        private float _maxY = 1.0f;
        private Color _traceColor = Color.FromArgb(0, 255, 136); // Phosphor green
        private bool _autoScale = true;
        private WaveformDecimationMode _decimationMode = WaveformDecimationMode.MinMax;

        [Category("ZeroUI Waveform")]
        public Color TraceColor
        {
            get => _traceColor;
            set { _traceColor = value; Invalidate(); }
        }

        [Category("ZeroUI Waveform")]
        [DefaultValue(true)]
        public bool AutoScale
        {
            get => _autoScale;
            set { _autoScale = value; Invalidate(); }
        }

        [Category("ZeroUI Waveform")]
        [DefaultValue(-1.0f)]
        public float MinY
        {
            get => _minY;
            set { _minY = value; Invalidate(); }
        }

        [Category("ZeroUI Waveform")]
        [DefaultValue(1.0f)]
        public float MaxY
        {
            get => _maxY;
            set { _maxY = value; Invalidate(); }
        }

        [Category("ZeroUI Waveform")]
        [DefaultValue(WaveformDecimationMode.MinMax)]
        public WaveformDecimationMode DecimationMode
        {
            get => _decimationMode;
            set { _decimationMode = value; Invalidate(); }
        }

        public ZeroWaveformCanvas()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.Opaque |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            DoubleBuffered = false;
            BackColor = Color.FromArgb(10, 14, 20);
        }

        public void SetData(float[] points)
        {
            _dataPoints = points;

            if (_autoScale && points != null && points.Length > 0)
            {
                float min = float.MaxValue;
                float max = float.MinValue;
                for (int i = 0; i < points.Length; i++)
                {
                    float v = points[i];
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
                if (min < max)
                {
                    _minY = min;
                    _maxY = max;
                }
            }

            Invalidate();
        }

        public void SetData(ReadOnlySpan<float> points)
        {
            if (points.Length == 0)
            {
                _dataPoints = null;
                Invalidate();
                return;
            }

            if (_dataPoints == null || _dataPoints.Length != points.Length)
            {
                _dataPoints = new float[points.Length];
            }
            points.CopyTo(_dataPoints);

            if (_autoScale && _dataPoints.Length > 0)
            {
                float min = float.MaxValue;
                float max = float.MinValue;
                for (int i = 0; i < _dataPoints.Length; i++)
                {
                    float v = _dataPoints[i];
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
                if (min < max)
                {
                    _minY = min;
                    _maxY = max;
                }
            }

            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!DesignMode && D3D11DeviceManager.IsSupported)
            {
                InitializePipeline();
                D3D11DeviceManager.DeviceRestored += OnDeviceRestored;
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            D3D11DeviceManager.DeviceRestored -= OnDeviceRestored;
            DisposePipeline();
            base.OnHandleDestroyed(e);
        }

        private void OnDeviceRestored()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(OnDeviceRestored));
                return;
            }

            DisposePipeline();
            InitializePipeline();
            Invalidate();
        }

        private void InitializePipeline()
        {
            if (IsHandleCreated && Width > 0 && Height > 0)
            {
                try
                {
                    _swapChain = new HwndSwapChain(Handle, Width, Height);
                    _pipeline = new WaveformPipeline(D3D11DeviceManager.Device, D3D11DeviceManager.Context);
                }
                catch
                {
                    // Fallback
                }
            }
        }

        private void DisposePipeline()
        {
            _pipeline?.Dispose();
            _pipeline = null;

            _swapChain?.Dispose();
            _swapChain = null;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_swapChain != null && Width > 0 && Height > 0)
            {
                _swapChain.Resize(Width, Height);
                Invalidate();
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_ERASEBKGND = 0x0014;
            if (m.Msg == WM_ERASEBKGND)
            {
                m.Result = (IntPtr)1;
                return;
            }

            base.WndProc(ref m);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (DesignMode || _swapChain == null || !_swapChain.IsValid || _pipeline == null)
            {
                base.OnPaint(e);
                return;
            }

            Render();
        }

        public void Render()
        {
            if (_swapChain == null || !_swapChain.IsValid || _pipeline == null) return;

            var rtv = _swapChain.RenderTargetView;
            if (rtv == null || !rtv.IsValid) return;

            // 1. Clear background
            float[] clearColor = new[]
            {
                BackColor.R / 255f,
                BackColor.G / 255f,
                BackColor.B / 255f,
                1.0f
            };
            D3D11DeviceManager.Context.ClearRenderTargetView(rtv, clearColor);

            // 2. Render Waveform LineStrip
            if (_dataPoints != null && _dataPoints.Length >= 2)
            {
                _pipeline.RenderWaveform(
                    rtv,
                    Width,
                    Height,
                    _dataPoints,
                    _minY,
                    _maxY,
                    _traceColor,
                    _decimationMode);
            }

            // 3. Present SwapChain
            _swapChain.Present(0);
        }
    }
}
