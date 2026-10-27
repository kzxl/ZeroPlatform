using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Pipeline;

namespace ZeroGraphics.DirectX.Controls
{
    /// <summary>
    /// High-performance hardware-accelerated DirectX canvas for Windows Forms.
    /// Hosts a native DXGI SwapChain directly on the control HWND, completely eliminating GDI/GDI+ CPU overhead,
    /// flickering, and tear artifacts with 0% CPU consumption when idle.
    /// </summary>
    [ToolboxItem(true)]
    public class ZeroDirectXCanvas : Control
    {
        private HwndSwapChain? _swapChain;
        private SdfCardPipeline? _pipeline;
        private bool _vsync = false;

        private float _elevation = 8f;
        private float _blurRadius = 16f;
        private float _cornerRadius = 10f;
        private float _borderWidth = 1f;
        private float _glowIntensity = 0f;
        private Color _glowColor = Color.FromArgb(0, 229, 255);
        private Color _cardColor = Color.FromArgb(22, 27, 38);
        private Color _cardBorderColor = Color.FromArgb(42, 51, 71);
        private Color _shadowColor = Color.FromArgb(120, 0, 0, 0);

        [Category("ZeroUI DirectX")]
        [DefaultValue(false)]
        [Description("Enables vertical synchronization to eliminate tearing.")]
        public bool Vsync
        {
            get => _vsync;
            set { _vsync = value; Invalidate(); }
        }

        [Category("ZeroUI DirectX")]
        [DefaultValue(8f)]
        [Description("Elevation distance in pixels for the analytical drop shadow.")]
        public float Elevation
        {
            get => _elevation;
            set { _elevation = Math.Max(0f, value); Invalidate(); }
        }

        [Category("ZeroUI DirectX")]
        [DefaultValue(16f)]
        [Description("Gaussian blur radius for the analytical drop shadow and bloom glow.")]
        public float BlurRadius
        {
            get => _blurRadius;
            set { _blurRadius = Math.Max(0f, value); Invalidate(); }
        }

        [Category("ZeroUI DirectX")]
        [DefaultValue(10f)]
        [Description("Corner radius for the rounded rectangle card.")]
        public float CornerRadius
        {
            get => _cornerRadius;
            set { _cornerRadius = Math.Max(0f, value); Invalidate(); }
        }

        [Category("ZeroUI DirectX")]
        [DefaultValue(1f)]
        [Description("Border stroke width in pixels.")]
        public float BorderWidth
        {
            get => _borderWidth;
            set { _borderWidth = Math.Max(0f, value); Invalidate(); }
        }

        [Category("ZeroUI DirectX")]
        [DefaultValue(0f)]
        [Description("Exponential neon bloom glow intensity (0.0 to 2.0).")]
        public float GlowIntensity
        {
            get => _glowIntensity;
            set { _glowIntensity = Math.Max(0f, value); Invalidate(); }
        }

        [Category("ZeroUI DirectX")]
        [Description("Neon glow emission color.")]
        public Color GlowColor
        {
            get => _glowColor;
            set { _glowColor = value; Invalidate(); }
        }

        [Category("ZeroUI DirectX")]
        [Description("Background fill color of the card surface.")]
        public Color CardColor
        {
            get => _cardColor;
            set { _cardColor = value; Invalidate(); }
        }

        [Category("ZeroUI DirectX")]
        [Description("Border stroke color.")]
        public Color CardBorderColor
        {
            get => _cardBorderColor;
            set { _cardBorderColor = value; Invalidate(); }
        }

        [Category("ZeroUI DirectX")]
        [Description("Drop shadow falloff color.")]
        public Color ShadowColor
        {
            get => _shadowColor;
            set { _shadowColor = value; Invalidate(); }
        }

        public HwndSwapChain? SwapChain => _swapChain;
        public SdfCardPipeline? Pipeline => _pipeline;

        public ZeroDirectXCanvas()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.Opaque |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            DoubleBuffered = false; // DXGI handles double-buffering via swapchain
            BackColor = Color.FromArgb(13, 17, 23);
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
                    _pipeline = new SdfCardPipeline(D3D11DeviceManager.Device, D3D11DeviceManager.Context);
                }
                catch
                {
                    // Fallback to standard control if DirectX cannot be initialized
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
                // Suppress GDI background erasure to eliminate flicker completely
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

        /// <summary>
        /// Executes an on-demand hardware render pass.
        /// </summary>
        public void Render()
        {
            if (_swapChain == null || !_swapChain.IsValid || _pipeline == null) return;

            var rtv = _swapChain.RenderTargetView;
            if (rtv == null || !rtv.IsValid) return;

            // 1. Clear backbuffer
            float[] clearColor = new[]
            {
                BackColor.R / 255f,
                BackColor.G / 255f,
                BackColor.B / 255f,
                1.0f
            };
            D3D11DeviceManager.Context.ClearRenderTargetView(rtv, clearColor);

            // 2. Custom or Default Rendering
            OnRenderDirectX(_swapChain, _pipeline);

            // 3. Present to HWND
            _swapChain.Present(_vsync ? 1u : 0u);
        }

        /// <summary>
        /// Override this method in derived controls to execute custom Direct3D 11 rendering.
        /// </summary>
        protected virtual void OnRenderDirectX(HwndSwapChain swapChain, SdfCardPipeline pipeline)
        {
            if (swapChain.RenderTargetView == null) return;

            // Leave margin for drop shadow and neon blur
            float margin = _blurRadius + _elevation + 4f;
            float cardW = Math.Max(10f, Width - margin * 2f);
            float cardH = Math.Max(10f, Height - margin * 2f);
            float cardX = margin;
            float cardY = margin;

            pipeline.RenderCard(
                swapChain.RenderTargetView,
                Width,
                Height,
                cardX,
                cardY,
                cardW,
                cardH,
                _cornerRadius,
                _borderWidth,
                _blurRadius,
                _elevation,
                _glowIntensity,
                _cardColor,
                _cardBorderColor,
                _shadowColor,
                _glowColor);
        }
    }
}
