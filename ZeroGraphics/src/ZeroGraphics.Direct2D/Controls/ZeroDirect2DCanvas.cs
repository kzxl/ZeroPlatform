using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroGraphics.Direct2D.Core;

namespace ZeroGraphics.Direct2D.Controls
{
    /// <summary>
    /// High-performance Direct2D and DirectWrite vector and typography canvas for Windows Forms.
    /// Provides hardware-accelerated 2D primitives and subpixel ClearType rendering with zero heap allocations on render loops.
    /// </summary>
    [ToolboxItem(true)]
    public class ZeroDirect2DCanvas : Control
    {
        private D2DHwndRenderTarget? _renderTarget;
        private DWriteTextFormat? _defaultTextFormat;
        private D2DSolidColorBrush? _textBrush;
        private D2DSolidColorBrush? _accentBrush;
        private D2DSolidColorBrush? _surfaceBrush;

        private string _sampleText = "ZeroUI DirectWrite ClearType Subpixel Engine ⚡";
        private float _fontSize = 14f;
        private string _fontFamily = "Segoe UI";

        [Category("ZeroUI Direct2D")]
        [DefaultValue("ZeroUI DirectWrite ClearType Subpixel Engine ⚡")]
        public string SampleText
        {
            get => _sampleText;
            set { _sampleText = value; Invalidate(); }
        }

        [Category("ZeroUI Direct2D")]
        [DefaultValue(14f)]
        public float TextSize
        {
            get => _fontSize;
            set
            {
                _fontSize = Math.Max(6f, value);
                RecreateTextFormat();
                Invalidate();
            }
        }

        [Category("ZeroUI Direct2D")]
        [DefaultValue("Segoe UI")]
        public string TextFontFamily
        {
            get => _fontFamily;
            set
            {
                _fontFamily = !string.IsNullOrWhiteSpace(value) ? value : "Segoe UI";
                RecreateTextFormat();
                Invalidate();
            }
        }

        public D2DHwndRenderTarget? RenderTarget => _renderTarget;

        public ZeroDirect2DCanvas()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.Opaque |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);

            DoubleBuffered = false; // Direct2D HWND render target handles its own presentation
            BackColor = Color.FromArgb(13, 17, 23);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!DesignMode)
            {
                RecreateRenderTarget();
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            DisposeResources();
            base.OnHandleDestroyed(e);
        }

        private void DisposeResources()
        {
            _textBrush?.Dispose();
            _textBrush = null;

            _accentBrush?.Dispose();
            _accentBrush = null;

            _surfaceBrush?.Dispose();
            _surfaceBrush = null;

            _defaultTextFormat?.Dispose();
            _defaultTextFormat = null;

            _renderTarget?.Dispose();
            _renderTarget = null;
        }

        private void RecreateRenderTarget()
        {
            DisposeResources();
            if (IsHandleCreated && Width > 0 && Height > 0)
            {
                try
                {
                    _renderTarget = D2DFactory.Default.CreateHwndRenderTarget(Handle, Width, Height);
                    ApplyDpi();
                    RecreateBrushes();
                    RecreateTextFormat();
                }
                catch
                {
                    // Direct2D recreation fallback
                }
            }
        }

        /// <summary>
        /// Synchronizes the Direct2D render target DPI with the current screen/window DPI.
        /// </summary>
        public void ApplyDpi()
        {
            if (_renderTarget == null || !_renderTarget.IsValid) return;

            try
            {
                using (Graphics g = CreateGraphics())
                {
                    _renderTarget.SetDpi(g.DpiX, g.DpiY);
                }
            }
            catch
            {
                _renderTarget.SetDpi(96.0f, 96.0f);
            }
        }

        /// <summary>
        /// Explicitly updates the render target DPI for multi-monitor High-DPI scaling.
        /// </summary>
        public void UpdateDpi(float dpiX, float dpiY)
        {
            if (_renderTarget != null && _renderTarget.IsValid)
            {
                _renderTarget.SetDpi(dpiX, dpiY);
                Invalidate();
            }
        }

        private void RecreateBrushes()
        {
            if (_renderTarget == null || !_renderTarget.IsValid) return;

            _textBrush?.Dispose();
            _textBrush = _renderTarget.CreateSolidColorBrush(Color.FromArgb(240, 246, 252));

            _accentBrush?.Dispose();
            _accentBrush = _renderTarget.CreateSolidColorBrush(Color.FromArgb(56, 189, 248));

            _surfaceBrush?.Dispose();
            _surfaceBrush = _renderTarget.CreateSolidColorBrush(Color.FromArgb(22, 27, 38));
        }

        private void RecreateTextFormat()
        {
            try
            {
                _defaultTextFormat?.Dispose();
                _defaultTextFormat = DWriteFactory.Default.CreateTextFormat(_fontFamily, _fontSize);
            }
            catch
            {
                // Fallback
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_renderTarget != null && _renderTarget.IsValid && Width > 0 && Height > 0)
            {
                _renderTarget.Resize(Width, Height);
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
            if (DesignMode || _renderTarget == null || !_renderTarget.IsValid)
            {
                base.OnPaint(e);
                return;
            }

            Render();
        }

        public void Render()
        {
            if (_renderTarget == null || !_renderTarget.IsValid) return;

            _renderTarget.BeginDraw();
            _renderTarget.Clear(BackColor);

            OnRenderDirect2D(_renderTarget);

            int hr = _renderTarget.EndDraw();
            const int D2DERR_RECREATE_TARGET = unchecked((int)0x8899000C);
            if (hr == D2DERR_RECREATE_TARGET)
            {
                RecreateRenderTarget();
                Invalidate();
            }
        }

        protected virtual void OnRenderDirect2D(D2DHwndRenderTarget rt)
        {
            if (_surfaceBrush == null || _accentBrush == null || _textBrush == null || _defaultTextFormat == null)
            {
                RecreateBrushes();
                RecreateTextFormat();
            }

            if (_surfaceBrush != null && _accentBrush != null)
            {
                // Draw rounded surface card
                rt.FillRoundedRectangle(16, 16, Width - 32, Height - 32, 10, _surfaceBrush);
                rt.DrawRoundedRectangle(16, 16, Width - 32, Height - 32, 10, _accentBrush, 1.5f);
            }

            if (_textBrush != null && _defaultTextFormat != null)
            {
                // Draw crisp DirectWrite subpixel typography
                rt.DrawText(_sampleText, _defaultTextFormat, 32, 32, Width - 64, Height - 64, _textBrush);
            }
        }
    }
}
