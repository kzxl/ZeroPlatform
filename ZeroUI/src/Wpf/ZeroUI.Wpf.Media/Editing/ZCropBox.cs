using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Media
{
    /// <summary>
    /// Composition guide overlay styles for <see cref="ZCropBox"/>.
    /// </summary>
    public enum CropGuideMode
    {
        /// <summary>Classic Rule of Thirds (3x3 grid at 1/3 and 2/3).</summary>
        RuleOfThirds,
        /// <summary>Golden Ratio grid (~0.382 and ~0.618).</summary>
        GoldenRatio,
        /// <summary>Diagonal cross lines from corner to corner.</summary>
        Diagonals,
        /// <summary>Fine 4x4 framing grid.</summary>
        Grid,
        /// <summary>No internal guide lines.</summary>
        None
    }

    /// <summary>
    /// Interactive 8-point Crop Box and Composition Gizmo control for ZeroUI in WPF.
    /// Provides normalized [0..1] crop rectangle adjustment, aspect ratio locking,
    /// 8 precision resize handles, composition guide overlays (Thirds, Golden Ratio, Diagonals),
    /// and direct high-performance <see cref="DrawingContext"/> rendering.
    /// </summary>
    public class ZCropBox : FrameworkElement, IZeroEditor
    {
        private string _activeHandle = "";
        private Point _dragStartMouse;
        private Rect _cropAtDragStart;
        private bool _isModified;
        private bool _isUpdatingFromDp;

        private const double HandlePixelSize = 12.0;

        #region Dependency Properties

        public static readonly DependencyProperty CropRectProperty =
            DependencyProperty.Register(nameof(CropRect), typeof(Rect), typeof(ZCropBox),
                new FrameworkPropertyMetadata(new Rect(0, 0, 1, 1), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnCropRectChanged));

        public static readonly DependencyProperty AspectRatioProperty =
            DependencyProperty.Register(nameof(AspectRatio), typeof(double), typeof(ZCropBox),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty GuideModeProperty =
            DependencyProperty.Register(nameof(GuideMode), typeof(CropGuideMode), typeof(ZCropBox),
                new FrameworkPropertyMetadata(CropGuideMode.RuleOfThirds, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty DimBrushProperty =
            DependencyProperty.Register(nameof(DimBrush), typeof(Brush), typeof(ZCropBox),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BorderBrushProperty =
            DependencyProperty.Register(nameof(BorderBrush), typeof(Brush), typeof(ZCropBox),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BorderThicknessProperty =
            DependencyProperty.Register(nameof(BorderThickness), typeof(double), typeof(ZCropBox),
                new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty GuideBrushProperty =
            DependencyProperty.Register(nameof(GuideBrush), typeof(Brush), typeof(ZCropBox),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty HandleSizeProperty =
            DependencyProperty.Register(nameof(HandleSize), typeof(double), typeof(ZCropBox),
                new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(ZCropBox),
                new FrameworkPropertyMetadata(false));

        #endregion

        #region Properties & Events

        /// <summary>
        /// Gets or sets the normalized crop rectangle [0..1] x [0..1].
        /// </summary>
        public Rect CropRect
        {
            get => (Rect)GetValue(CropRectProperty);
            set => SetValue(CropRectProperty, NormalizeRect(value));
        }

        /// <summary>
        /// Locked aspect ratio (Width / Height). Set to 0 for freeform cropping.
        /// </summary>
        public double AspectRatio
        {
            get => (double)GetValue(AspectRatioProperty);
            set => SetValue(AspectRatioProperty, Math.Max(0.0, value));
        }

        public CropGuideMode GuideMode
        {
            get => (CropGuideMode)GetValue(GuideModeProperty);
            set => SetValue(GuideModeProperty, value);
        }

        public Brush? DimBrush
        {
            get => (Brush?)GetValue(DimBrushProperty);
            set => SetValue(DimBrushProperty, value);
        }

        public Brush? BorderBrush
        {
            get => (Brush?)GetValue(BorderBrushProperty);
            set => SetValue(BorderBrushProperty, value);
        }

        public double BorderThickness
        {
            get => (double)GetValue(BorderThicknessProperty);
            set => SetValue(BorderThicknessProperty, value);
        }

        public Brush? GuideBrush
        {
            get => (Brush?)GetValue(GuideBrushProperty);
            set => SetValue(GuideBrushProperty, value);
        }

        public double HandleSize
        {
            get => (double)GetValue(HandleSizeProperty);
            set => SetValue(HandleSizeProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        /// <summary>
        /// Occurs when the user resizes or drags the crop rectangle.
        /// </summary>
        public event EventHandler<Rect>? CropRectChanged;

        /// <summary>
        /// Occurs when a crop manipulation is finished (mouse released).
        /// </summary>
        public event EventHandler<Rect>? CropFinished;

        #endregion

        #region IZeroEditor Implementation

        public object? EditValue
        {
            get => CropRect;
            set
            {
                if (value is Rect r) CropRect = r;
                else if (value is string s)
                {
                    var p = s.Split(',');
                    if (p.Length == 4 &&
                        double.TryParse(p[0], out var x) &&
                        double.TryParse(p[1], out var y) &&
                        double.TryParse(p[2], out var w) &&
                        double.TryParse(p[3], out var h))
                    {
                        CropRect = new Rect(x, y, w, h);
                    }
                }
            }
        }

        public event EventHandler? EditValueChanged;

        public bool IsModified
        {
            get => _isModified;
            set => _isModified = value;
        }

        public void Reset()
        {
            CropRect = new Rect(0, 0, 1, 1);
            _isModified = false;
            InvalidateVisual();
        }

        public void Clear() => Reset();

        #endregion

        public ZCropBox()
        {
            Focusable = true;
            ClipToBounds = true;
            Cursor = Cursors.Arrow;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static void OnCropRectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZCropBox ctrl && !ctrl._isUpdatingFromDp)
            {
                ctrl.CropRectChanged?.Invoke(ctrl, (Rect)e.NewValue);
                ctrl.EditValueChanged?.Invoke(ctrl, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Cycles through composition guide overlay modes (Thirds -> Golden Ratio -> Diagonals -> Grid -> None).
        /// </summary>
        public void CycleGuideMode()
        {
            int current = (int)GuideMode;
            GuideMode = (CropGuideMode)((current + 1) % 5);
        }

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            var r = CropRect;
            double cl = r.X * w;
            double ct = r.Y * h;
            double cw = r.Width * w;
            double ch = r.Height * h;

            var dim = DimBrush ?? new SolidColorBrush(Color.FromArgb(0xA0, 0, 0, 0));
            if (!dim.IsFrozen) dim.Freeze();

            // 1. Outside dim shades
            if (ct > 0) dc.DrawRectangle(dim, null, new Rect(0, 0, w, ct));                     // top
            if (ct + ch < h) dc.DrawRectangle(dim, null, new Rect(0, ct + ch, w, h - (ct + ch))); // bottom
            if (cl > 0) dc.DrawRectangle(dim, null, new Rect(0, ct, cl, ch));                  // left
            if (cl + cw < w) dc.DrawRectangle(dim, null, new Rect(cl + cw, ct, w - (cl + cw), ch)); // right

            // 2. Crop border
            var borderBrush = BorderBrush ?? Brushes.White;
            var borderPen = new Pen(borderBrush, BorderThickness);
            borderPen.Freeze();
            var cropPixelRect = new Rect(cl, ct, cw, ch);
            dc.DrawRectangle(null, borderPen, cropPixelRect);

            // 3. Composition Guides
            DrawGuides(dc, cropPixelRect);

            // 4. Handles
            if (!ReadOnly)
            {
                DrawHandles(dc, cl, ct, cw, ch);
            }
        }

        private void DrawGuides(DrawingContext dc, Rect box)
        {
            if (GuideMode == CropGuideMode.None || box.Width <= 8 || box.Height <= 8) return;

            var guideBrush = GuideBrush ?? new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF));
            if (!guideBrush.IsFrozen) guideBrush.Freeze();
            var guidePen = new Pen(guideBrush, 0.75);
            guidePen.Freeze();

            switch (GuideMode)
            {
                case CropGuideMode.RuleOfThirds:
                    for (int i = 1; i <= 2; i++)
                    {
                        double gx = box.Left + (i / 3.0) * box.Width;
                        double gy = box.Top + (i / 3.0) * box.Height;
                        dc.DrawLine(guidePen, new Point(gx, box.Top), new Point(gx, box.Bottom));
                        dc.DrawLine(guidePen, new Point(box.Left, gy), new Point(box.Right, gy));
                    }
                    break;

                case CropGuideMode.GoldenRatio:
                    double phi = 0.381966;
                    double[] gxCoords = { box.Left + phi * box.Width, box.Right - phi * box.Width };
                    double[] gyCoords = { box.Top + phi * box.Height, box.Bottom - phi * box.Height };
                    foreach (var x in gxCoords) dc.DrawLine(guidePen, new Point(x, box.Top), new Point(x, box.Bottom));
                    foreach (var y in gyCoords) dc.DrawLine(guidePen, new Point(box.Left, y), new Point(box.Right, y));
                    break;

                case CropGuideMode.Diagonals:
                    dc.DrawLine(guidePen, new Point(box.Left, box.Top), new Point(box.Right, box.Bottom));
                    dc.DrawLine(guidePen, new Point(box.Left, box.Bottom), new Point(box.Right, box.Top));
                    break;

                case CropGuideMode.Grid:
                    for (int i = 1; i <= 3; i++)
                    {
                        double gx = box.Left + (i / 4.0) * box.Width;
                        double gy = box.Top + (i / 4.0) * box.Height;
                        dc.DrawLine(guidePen, new Point(gx, box.Top), new Point(gx, box.Bottom));
                        dc.DrawLine(guidePen, new Point(box.Left, gy), new Point(box.Right, gy));
                    }
                    break;
            }
        }

        private void DrawHandles(DrawingContext dc, double cl, double ct, double cw, double ch)
        {
            double hs = HandleSize;
            double hhs = hs / 2.0;

            var fill = Brushes.White;
            var stroke = new Pen(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), 1.0);
            stroke.Freeze();

            Point[] pts =
            {
                new Point(cl, ct),            // nw
                new Point(cl + cw / 2.0, ct), // n
                new Point(cl + cw, ct),       // ne
                new Point(cl, ct + ch / 2.0), // w
                new Point(cl + cw, ct + ch / 2.0), // e
                new Point(cl, ct + ch),       // sw
                new Point(cl + cw / 2.0, ct + ch), // s
                new Point(cl + cw, ct + ch)   // se
            };

            foreach (var pt in pts)
            {
                dc.DrawRoundedRectangle(fill, stroke, new Rect(pt.X - hhs, pt.Y - hhs, hs, hs), 2.0, 2.0);
            }
        }

        #endregion

        #region Hit Testing & Interaction

        private string HitTestHandle(Point p)
        {
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return "";

            var r = CropRect;
            double cl = r.X * w;
            double ct = r.Y * h;
            double cw = r.Width * w;
            double ch = r.Height * h;
            double hitR = HandleSize + 4.0;

            if (Near(p, new Point(cl, ct), hitR)) return "nw";
            if (Near(p, new Point(cl + cw, ct), hitR)) return "ne";
            if (Near(p, new Point(cl, ct + ch), hitR)) return "sw";
            if (Near(p, new Point(cl + cw, ct + ch), hitR)) return "se";
            if (Near(p, new Point(cl + cw / 2.0, ct), hitR)) return "n";
            if (Near(p, new Point(cl + cw / 2.0, ct + ch), hitR)) return "s";
            if (Near(p, new Point(cl, ct + ch / 2.0), hitR)) return "w";
            if (Near(p, new Point(cl + cw, ct + ch / 2.0), hitR)) return "e";

            if (p.X >= cl && p.X <= cl + cw && p.Y >= ct && p.Y <= ct + ch) return "body";

            return "";
        }

        private static bool Near(Point a, Point b, double dist)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return dx * dx + dy * dy <= dist * dist;
        }

        private static Cursor CursorForHandle(string handle) => handle switch
        {
            "nw" or "se" => Cursors.SizeNWSE,
            "ne" or "sw" => Cursors.SizeNESW,
            "n" or "s" => Cursors.SizeNS,
            "w" or "e" => Cursors.SizeWE,
            "body" => Cursors.SizeAll,
            _ => Cursors.Arrow
        };

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point p = e.GetPosition(this);

            if (!string.IsNullOrEmpty(_activeHandle) && !ReadOnly)
            {
                double w = Math.Max(1.0, ActualWidth);
                double h = Math.Max(1.0, ActualHeight);
                double dx = (p.X - _dragStartMouse.X) / w;
                double dy = (p.Y - _dragStartMouse.Y) / h;

                ApplyDrag(dx, dy);
                InvalidateVisual();
            }
            else
            {
                string hit = HitTestHandle(p);
                Cursor = CursorForHandle(hit);
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            if (ReadOnly) return;

            Point p = e.GetPosition(this);
            string hit = HitTestHandle(p);
            if (!string.IsNullOrEmpty(hit))
            {
                _activeHandle = hit;
                _dragStartMouse = p;
                _cropAtDragStart = CropRect;
                CaptureMouse();
                Cursor = CursorForHandle(hit);
                e.Handled = true;
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (!string.IsNullOrEmpty(_activeHandle))
            {
                _activeHandle = "";
                ReleaseMouseCapture();
                Cursor = Cursors.Arrow;
                CropFinished?.Invoke(this, CropRect);
                e.Handled = true;
            }
        }

        private void ApplyDrag(double dx, double dy)
        {
            var s = _cropAtDragStart;
            double nx = s.X, ny = s.Y, nw = s.Width, nh = s.Height;

            if (_activeHandle == "body")
            {
                nx = Clamp(s.X + dx, 0.0, 1.0 - s.Width);
                ny = Clamp(s.Y + dy, 0.0, 1.0 - s.Height);
            }
            else
            {
                if (_activeHandle.Contains("w"))
                {
                    double d = Clamp(dx, -s.X, s.Width - 0.05);
                    nx = s.X + d;
                    nw = s.Width - d;
                }
                if (_activeHandle.Contains("e"))
                {
                    nw = Clamp(s.Width + dx, 0.05, 1.0 - s.X);
                }
                if (_activeHandle.Contains("n"))
                {
                    double d = Clamp(dy, -s.Y, s.Height - 0.05);
                    ny = s.Y + d;
                    nh = s.Height - d;
                }
                if (_activeHandle.Contains("s"))
                {
                    nh = Clamp(s.Height + dy, 0.05, 1.0 - s.Y);
                }

                // Apply aspect ratio constraint if locked
                if (AspectRatio > 0.0)
                {
                    double targetAspect = AspectRatio;
                    // Adjust height to match width / targetAspect (normalized to element dimensions)
                    double pixelW = nw * ActualWidth;
                    double pixelH = pixelW / targetAspect;
                    nh = Clamp(pixelH / ActualHeight, 0.05, 1.0 - ny);
                }
            }

            _isUpdatingFromDp = true;
            CropRect = new Rect(nx, ny, nw, nh);
            _isUpdatingFromDp = false;
            _isModified = true;

            CropRectChanged?.Invoke(this, CropRect);
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Helpers

        private static Rect NormalizeRect(Rect r)
        {
            double x = Clamp(r.X, 0.0, 1.0);
            double y = Clamp(r.Y, 0.0, 1.0);
            double w = Clamp(r.Width, 0.01, 1.0 - x);
            double h = Clamp(r.Height, 0.01, 1.0 - y);
            return new Rect(x, y, w, h);
        }

        private static double Clamp(double val, double min, double max)
        {
            if (val < min) return min;
            if (val > max) return max;
            return val;
        }

        #endregion
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged += OnThemeChanged;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
    }
    private void OnThemeChanged() => InvalidateVisual();
}

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZCropBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("CropBoxControl is deprecated. Use ZCropBox instead.")]
    public class CropBoxControl : ZCropBox
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZCropBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroCropBox is deprecated. Use ZCropBox instead.")]
    public class ZeroCropBox : ZCropBox
    {
    }
    #endregion
}
