using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Smooth vector signature capture pad supporting touch and mouse drawing,
    /// stroke thickness and color adjustments, baseline guides, and high-res bitmap export.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultProperty("StrokeColor")]
    [DefaultEvent("SignatureChanged")]
    [Description("Touch and mouse vector signature pad with image export")]
    public class ZSignaturePad : Control
    {
        private Color _strokeColor = Color.Black;
        private int _strokeWidth = 3;
        private Color _backgroundColor = Color.White;
        private bool _isDrawing = false;
        private readonly List<List<Point>> _strokes = new List<List<Point>>();
        private List<Point>? _currentStroke;

        public event EventHandler? SignatureChanged;

        [Category("Appearance")]
        [Description("Pen color used to render signature strokes.")]
        public Color StrokeColor
        {
            get => _strokeColor;
            set { _strokeColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(3)]
        [Description("Pen width in pixels.")]
        public int StrokeWidth
        {
            get => _strokeWidth;
            set { _strokeWidth = Math.Max(1, value); Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Background canvas color for signature capture.")]
        public Color BackgroundColor
        {
            get => _backgroundColor;
            set { _backgroundColor = value; Invalidate(); }
        }

        [Browsable(false)]
        public bool IsEmpty => _strokes.Count == 0;

        public ZSignaturePad()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(320, 160);
            Cursor = Cursors.Cross;
            BackColor = Color.White;

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ZeroTheme.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            if (IsDisposed) return;
            Invalidate();
        }

        public void Clear()
        {
            _strokes.Clear();
            _currentStroke = null;
            _isDrawing = false;
            Invalidate();
            SignatureChanged?.Invoke(this, EventArgs.Empty);
        }

        public Bitmap? GetSignatureImage()
        {
            if (Width <= 0 || Height <= 0) return null;

            var bmp = new Bitmap(Width, Height);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var bg = new SolidBrush(_backgroundColor);
            g.FillRectangle(bg, 0, 0, Width, Height);

            DrawStrokes(g);
            return bmp;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                Focus();
                _isDrawing = true;
                _currentStroke = new List<Point> { e.Location };
                _strokes.Add(_currentStroke);
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDrawing && _currentStroke != null)
            {
                _currentStroke.Add(e.Location);
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDrawing)
            {
                _isDrawing = false;
                _currentStroke = null;
                Invalidate();
                SignatureChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void DrawStrokes(Graphics g)
        {
            if (_strokes.Count == 0) return;

            using var pen = new Pen(_strokeColor, _strokeWidth)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };

            foreach (var stroke in _strokes)
            {
                if (stroke.Count == 1)
                {
                    // Draw single dot
                    int r = Math.Max(1, _strokeWidth / 2);
                    using var brush = new SolidBrush(_strokeColor);
                    g.FillEllipse(brush, stroke[0].X - r, stroke[0].Y - r, _strokeWidth, _strokeWidth);
                }
                else if (stroke.Count > 1)
                {
                    g.DrawLines(pen, stroke.ToArray());
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = ClientRectangle;
            var c = ZeroTheme.Colors;

            // Background canvas
            using (var bgBrush = new SolidBrush(_backgroundColor))
            {
                g.FillRectangle(bgBrush, bounds);
            }

            // Outer border
            using (var borderPen = new Pen(c.BorderSubtle, 1f))
            {
                g.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            }

            // Draw strokes
            DrawStrokes(g);

            // Baseline guide & prompt when empty
            if (IsEmpty)
            {
                int baselineY = bounds.Bottom - 30;
                using (var guidePen = new Pen(Color.FromArgb(120, Color.LightGray), 1f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawLine(guidePen, 20, baselineY, bounds.Right - 20, baselineY);
                }

                var promptFont = ZeroFontCache.Get(8.5f, FontStyle.Italic);
                using (var promptBrush = new SolidBrush(Color.FromArgb(160, Color.Gray)))
                {
                    g.DrawString("✕ Sign here", promptFont, promptBrush, 22, baselineY - 18);
                }
            }
        }
    }

    [Obsolete("SignaturePad is deprecated and will be removed in 5 release cycles. Please migrate to ZSignaturePad instead.")]
    [ToolboxItem(false)]
    public class SignaturePad : ZSignaturePad { }

    [Obsolete("ZeroSignaturePad is deprecated and will be removed in 5 release cycles. Please migrate to ZSignaturePad instead.")]
    [ToolboxItem(false)]
    public class ZeroSignaturePad : ZSignaturePad { }
}
