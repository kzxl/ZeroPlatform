using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Smooth vector signature capture pad for WPF supporting touch and mouse drawing,
    /// stroke thickness and color adjustments, baseline guides, and BitmapSource export.
    /// </summary>
    public class ZSignaturePad : Control
    {
        private Color _strokeColor = Colors.Black;
        private int _strokeWidth = 3;
        private Color _backgroundColor = Colors.White;
        private bool _isDrawing = false;
        private readonly List<List<Point>> _strokes = new List<List<Point>>();
        private List<Point>? _currentStroke;

        public event EventHandler? SignatureChanged;

        public Color StrokeColor
        {
            get => _strokeColor;
            set { _strokeColor = value; InvalidateVisual(); }
        }

        public int StrokeWidth
        {
            get => _strokeWidth;
            set { _strokeWidth = Math.Max(1, value); InvalidateVisual(); }
        }

        public Color BackgroundColor
        {
            get => _backgroundColor;
            set { _backgroundColor = value; InvalidateVisual(); }
        }

        public bool IsEmpty => _strokes.Count == 0;

        static ZSignaturePad()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZSignaturePad), new FrameworkPropertyMetadata(typeof(ZSignaturePad)));
        }

        public ZSignaturePad()
        {
            Width = 320;
            Height = 160;
            Cursor = Cursors.Cross;
            Focusable = true;

            Loaded += (s, e) => ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            Unloaded += (s, e) => ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            if (!Dispatcher.CheckAccess())
            {
                if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                    Dispatcher.BeginInvoke((Action)OnThemeChanged);
                return;
            }
            InvalidateVisual();
        }

        public void Clear()
        {
            _strokes.Clear();
            _currentStroke = null;
            _isDrawing = false;
            InvalidateVisual();
            SignatureChanged?.Invoke(this, EventArgs.Empty);
        }

        public ImageSource? GetSignatureImage()
        {
            int w = (int)Math.Max(1, ActualWidth > 0 ? ActualWidth : Width);
            int h = (int)Math.Max(1, ActualHeight > 0 ? ActualHeight : Height);

            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var bgBrush = new SolidColorBrush(_backgroundColor);
                bgBrush.Freeze();
                dc.DrawRectangle(bgBrush, null, new Rect(0, 0, w, h));

                RenderStrokes(dc);
            }
            rtb.Render(visual);
            rtb.Freeze();
            return rtb;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            CaptureMouse();
            _isDrawing = true;
            _currentStroke = new List<Point> { e.GetPosition(this) };
            _strokes.Add(_currentStroke);
            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDrawing && _currentStroke != null)
            {
                _currentStroke.Add(e.GetPosition(this));
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_isDrawing)
            {
                _isDrawing = false;
                _currentStroke = null;
                ReleaseMouseCapture();
                InvalidateVisual();
                SignatureChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void RenderStrokes(DrawingContext dc)
        {
            if (_strokes.Count == 0) return;

            var strokeBrush = new SolidColorBrush(_strokeColor);
            strokeBrush.Freeze();
            var pen = new Pen(strokeBrush, _strokeWidth)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            pen.Freeze();

            foreach (var stroke in _strokes)
            {
                if (stroke.Count == 1)
                {
                    double r = Math.Max(1, _strokeWidth / 2.0);
                    dc.DrawEllipse(strokeBrush, null, stroke[0], r, r);
                }
                else if (stroke.Count > 1)
                {
                    var geom = new StreamGeometry();
                    using (var ctx = geom.Open())
                    {
                        ctx.BeginFigure(stroke[0], false, false);
                        ctx.PolyLineTo(stroke.GetRange(1, stroke.Count - 1), true, false);
                    }
                    geom.Freeze();
                    dc.DrawGeometry(null, pen, geom);
                }
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            // Background canvas
            var bgBrush = new SolidColorBrush(_backgroundColor);
            bgBrush.Freeze();
            dc.DrawRectangle(bgBrush, null, bounds);

            // Border
            dc.DrawRectangle(null, ZeroWpfTheme.GridLinePen, bounds);

            // Strokes
            RenderStrokes(dc);

            // Baseline guide & prompt when empty
            if (IsEmpty)
            {
                double baselineY = bounds.Bottom - 30;
                var dashPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 180, 180, 180)), 1.0)
                {
                    DashStyle = DashStyles.Dash
                };
                dashPen.Freeze();
                dc.DrawLine(dashPen, new Point(20, baselineY), new Point(bounds.Right - 20, baselineY));

                var typeface = new Typeface(FontFamily, FontStyles.Italic, FontWeights.Normal, FontStretches.Normal);
                var promptText = new FormattedText(
                    "✕ Sign here",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    11.0,
                    new SolidColorBrush(Color.FromArgb(160, 150, 150, 150)),
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                dc.DrawText(promptText, new Point(22, baselineY - 18));
            }
        }
    }

    [Obsolete("SignaturePad is deprecated and will be removed in 5 release cycles. Please migrate to ZSignaturePad instead.")]
    public class SignaturePad : ZSignaturePad { }

    [Obsolete("ZeroSignaturePad is deprecated and will be removed in 5 release cycles. Please migrate to ZSignaturePad instead.")]
    public class ZeroSignaturePad : ZSignaturePad { }
}
