using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Media
{
    /// <summary>
    /// Interactive 2D Tone Curve Editor for ZeroUI in WPF.
    /// Employs Fritsch-Carlson monotone cubic Hermite spline interpolation (<see cref="CurveMath"/>)
    /// to guarantee monotonic tone transitions without ringing or overshoot artifacts.
    /// Supports control point dragging, double-click to add/remove, right-click to delete,
    /// arrow-key nudging, background histogram visualization, and theme synchronization.
    /// Renders directly using <see cref="DrawingContext"/> for maximum performance.
    /// </summary>
    public class ZCurveEditor : FrameworkElement, IZeroEditor
    {
        private readonly List<(float x, float y)> _points = new List<(float x, float y)> { (0f, 0f), (1f, 1f) };
        private int _dragIndex = -1;
        private int _hoverIndex = -1;
        private int _selectedIndex = -1;
        private bool _isUpdatingFromDp = false;
        private bool _isModified = false;
        private bool _suppressEvents = false;

        private const double HitRadiusNorm = 0.045; // Normalized hit-test radius

        #region Dependency Properties

        public static readonly DependencyProperty CurveDataProperty =
            DependencyProperty.Register(nameof(CurveData), typeof(string), typeof(ZCurveEditor),
                new FrameworkPropertyMetadata("0,0;1,1", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnCurveDataChanged));

        public static readonly DependencyProperty BackgroundHistogramProperty =
            DependencyProperty.Register(nameof(BackgroundHistogram), typeof(float[]), typeof(ZCurveEditor),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty GridDivisionsProperty =
            DependencyProperty.Register(nameof(GridDivisions), typeof(int), typeof(ZCurveEditor),
                new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowDiagonalProperty =
            DependencyProperty.Register(nameof(ShowDiagonal), typeof(bool), typeof(ZCurveEditor),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CurveBrushProperty =
            DependencyProperty.Register(nameof(CurveBrush), typeof(Brush), typeof(ZCurveEditor),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CurveThicknessProperty =
            DependencyProperty.Register(nameof(CurveThickness), typeof(double), typeof(ZCurveEditor),
                new FrameworkPropertyMetadata(1.8, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ThumbSizeProperty =
            DependencyProperty.Register(nameof(ThumbSize), typeof(double), typeof(ZCurveEditor),
                new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ThumbBrushProperty =
            DependencyProperty.Register(nameof(ThumbBrush), typeof(Brush), typeof(ZCurveEditor),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ThumbStrokeProperty =
            DependencyProperty.Register(nameof(ThumbStroke), typeof(Brush), typeof(ZCurveEditor),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ThumbStrokeThicknessProperty =
            DependencyProperty.Register(nameof(ThumbStrokeThickness), typeof(double), typeof(ZCurveEditor),
                new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty GridStrokeProperty =
            DependencyProperty.Register(nameof(GridStroke), typeof(Brush), typeof(ZCurveEditor),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty GridThicknessProperty =
            DependencyProperty.Register(nameof(GridThickness), typeof(double), typeof(ZCurveEditor),
                new FrameworkPropertyMetadata(0.6, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty HistogramBrushProperty =
            DependencyProperty.Register(nameof(HistogramBrush), typeof(Brush), typeof(ZCurveEditor),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BackgroundBrushProperty =
            DependencyProperty.Register(nameof(BackgroundBrush), typeof(Brush), typeof(ZCurveEditor),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BorderBrushProperty =
            DependencyProperty.Register(nameof(BorderBrush), typeof(Brush), typeof(ZCurveEditor),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BorderThicknessProperty =
            DependencyProperty.Register(nameof(BorderThickness), typeof(double), typeof(ZCurveEditor),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(double), typeof(ZCurveEditor),
                new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(ZCurveEditor),
                new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty ShowCoordinatesBadgeProperty =
            DependencyProperty.Register(nameof(ShowCoordinatesBadge), typeof(bool), typeof(ZCurveEditor),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        #endregion

        #region Properties & Events

        public string CurveData
        {
            get => (string)GetValue(CurveDataProperty);
            set => SetValue(CurveDataProperty, value);
        }

        public float[]? BackgroundHistogram
        {
            get => (float[]?)GetValue(BackgroundHistogramProperty);
            set => SetValue(BackgroundHistogramProperty, value);
        }

        public int GridDivisions
        {
            get => (int)GetValue(GridDivisionsProperty);
            set => SetValue(GridDivisionsProperty, value);
        }

        public bool ShowDiagonal
        {
            get => (bool)GetValue(ShowDiagonalProperty);
            set => SetValue(ShowDiagonalProperty, value);
        }

        public Brush? CurveBrush
        {
            get => (Brush?)GetValue(CurveBrushProperty);
            set => SetValue(CurveBrushProperty, value);
        }

        public double CurveThickness
        {
            get => (double)GetValue(CurveThicknessProperty);
            set => SetValue(CurveThicknessProperty, value);
        }

        public double ThumbSize
        {
            get => (double)GetValue(ThumbSizeProperty);
            set => SetValue(ThumbSizeProperty, value);
        }

        public Brush? ThumbBrush
        {
            get => (Brush?)GetValue(ThumbBrushProperty);
            set => SetValue(ThumbBrushProperty, value);
        }

        public Brush? ThumbStroke
        {
            get => (Brush?)GetValue(ThumbStrokeProperty);
            set => SetValue(ThumbStrokeProperty, value);
        }

        public double ThumbStrokeThickness
        {
            get => (double)GetValue(ThumbStrokeThicknessProperty);
            set => SetValue(ThumbStrokeThicknessProperty, value);
        }

        public Brush? GridStroke
        {
            get => (Brush?)GetValue(GridStrokeProperty);
            set => SetValue(GridStrokeProperty, value);
        }

        public double GridThickness
        {
            get => (double)GetValue(GridThicknessProperty);
            set => SetValue(GridThicknessProperty, value);
        }

        public Brush? HistogramBrush
        {
            get => (Brush?)GetValue(HistogramBrushProperty);
            set => SetValue(HistogramBrushProperty, value);
        }

        public Brush? BackgroundBrush
        {
            get => (Brush?)GetValue(BackgroundBrushProperty);
            set => SetValue(BackgroundBrushProperty, value);
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

        public double CornerRadius
        {
            get => (double)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public bool ShowCoordinatesBadge
        {
            get => (bool)GetValue(ShowCoordinatesBadgeProperty);
            set => SetValue(ShowCoordinatesBadgeProperty, value);
        }

        public IReadOnlyList<(float x, float y)> Points => _points.AsReadOnly();

        public bool IsIdentity => CurveMath.IsIdentity(CurveMath.Normalize(_points));

        /// <summary>
        /// Fires when the user modifies control points (dragging, adding, or deleting).
        /// Passes the serialized curve string "x0,y0;x1,y1;...".
        /// </summary>
        public event EventHandler<string>? CurveChanged;

        #endregion

        #region IZeroEditor Implementation

        public object? EditValue
        {
            get => CurveData;
            set
            {
                if (value is string str)
                {
                    CurveData = str;
                }
                else if (value is IReadOnlyList<(float x, float y)> pts)
                {
                    SetPoints(CurveMath.Serialize(pts));
                }
                else if (value == null)
                {
                    Reset();
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
            _points.Clear();
            _points.Add((0f, 0f));
            _points.Add((1f, 1f));
            _selectedIndex = -1;
            _isModified = false;
            SyncCurveData();
            InvalidateVisual();
            RaiseChanged();
        }

        public void Clear() => Reset();

        #endregion

        public ZCurveEditor()
        {
            Focusable = true;
            ClipToBounds = true;
            Cursor = Cursors.Cross;

            // Default dimensions
            Height = 180;
            MinWidth = 120;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// Loads control points from serialized string without raising the user <see cref="CurveChanged"/> event.
        /// </summary>
        public void SetPoints(string? serialized)
        {
            var parsed = CurveMath.Parse(serialized) ?? new List<(float, float)> { (0f, 0f), (1f, 1f) };
            _suppressEvents = true;
            _points.Clear();
            _points.AddRange(CurveMath.Normalize(parsed));
            _isUpdatingFromDp = true;
            CurveData = CurveMath.Serialize(_points);
            _isUpdatingFromDp = false;
            _suppressEvents = false;
            _selectedIndex = -1;
            InvalidateVisual();
        }

        /// <summary>
        /// Returns the current serialized point string ("x0,y0;x1,y1;...").
        /// </summary>
        public string GetSerialized() => CurveMath.Serialize(_points);

        #region DependencyProperty Callbacks

        private static void OnCurveDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZCurveEditor editor && !editor._isUpdatingFromDp)
            {
                editor.SetPoints(e.NewValue as string);
            }
        }

        #endregion

        #region Coordinate Mapping

        private Point ToPixel(float nx, float ny, Rect bounds)
        {
            return new Point(
                bounds.Left + nx * bounds.Width,
                bounds.Bottom - ny * bounds.Height);
        }

        private (float x, float y) ToNorm(Point p, Rect bounds)
        {
            float nx = (float)((p.X - bounds.Left) / Math.Max(1.0, bounds.Width));
            float ny = (float)((bounds.Bottom - p.Y) / Math.Max(1.0, bounds.Height));
            return (Clamp(nx, 0f, 1f), Clamp(ny, 0f, 1f));
        }

        private Rect GetInnerBounds()
        {
            double inset = BorderThickness;
            return new Rect(
                inset,
                inset,
                Math.Max(0.0, ActualWidth - inset * 2),
                Math.Max(0.0, ActualHeight - inset * 2));
        }

        private int HitTest(Point p, Rect bounds)
        {
            var np = ToNorm(p, bounds);
            int best = -1;
            double bestDistSq = HitRadiusNorm * HitRadiusNorm;

            for (int i = 0; i < _points.Count; i++)
            {
                double dx = _points[i].x - np.x;
                double dy = _points[i].y - np.y;
                double dSq = dx * dx + dy * dy;
                if (dSq <= bestDistSq)
                {
                    bestDistSq = dSq;
                    best = i;
                }
            }

            return best;
        }

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 0 || height <= 0) return;

            var bgBrush = BackgroundBrush ?? ZeroWpfTheme.BgInput;
            var borderBrush = BorderBrush ?? (IsFocused ? ZeroWpfTheme.BorderFocus : ZeroWpfTheme.BorderDefault);
            var borderPen = new Pen(borderBrush, BorderThickness);
            borderPen.Freeze();

            var outerRect = new Rect(0, 0, width, height);
            double cornerRadius = CornerRadius;

            // Background & Border
            dc.DrawRoundedRectangle(bgBrush, borderPen, outerRect, cornerRadius, cornerRadius);

            var innerBounds = GetInnerBounds();
            if (innerBounds.Width <= 0 || innerBounds.Height <= 0) return;

            // Clip content to inner bounds with rounded corner geometry
            var clipGeo = new RectangleGeometry(innerBounds, Math.Max(0, cornerRadius - 1), Math.Max(0, cornerRadius - 1));
            clipGeo.Freeze();
            dc.PushClip(clipGeo);

            // 1. Background Histogram Underlay
            DrawHistogram(dc, innerBounds);

            // 2. Grid Divisions
            DrawGrid(dc, innerBounds);

            // 3. Diagonal Reference
            if (ShowDiagonal)
            {
                var diagPen = new Pen(ZeroWpfTheme.BorderSubtle, 0.8) { DashStyle = DashStyles.Dash };
                diagPen.Freeze();
                dc.DrawLine(diagPen, new Point(innerBounds.Left, innerBounds.Bottom), new Point(innerBounds.Right, innerBounds.Top));
            }

            // 4. Spline Curve
            DrawCurve(dc, innerBounds);

            // 5. Control Points
            DrawControlPoints(dc, innerBounds);

            // 6. Coordinates Badge when hovering / dragging
            if (ShowCoordinatesBadge)
            {
                int activeIdx = _dragIndex >= 0 ? _dragIndex : _hoverIndex;
                if (activeIdx >= 0 && activeIdx < _points.Count)
                {
                    DrawCoordinateBadge(dc, innerBounds, _points[activeIdx]);
                }
            }

            dc.Pop(); // Pop clip
        }

        private void DrawHistogram(DrawingContext dc, Rect bounds)
        {
            var hist = BackgroundHistogram;
            if (hist == null || hist.Length == 0) return;

            var histBrush = HistogramBrush ?? new SolidColorBrush(Color.FromArgb(35, 255, 255, 255));
            if (!histBrush.IsFrozen) histBrush.Freeze();

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(bounds.Left, bounds.Bottom), true, true);
                int count = hist.Length;
                for (int i = 0; i < count; i++)
                {
                    double x = bounds.Left + (i / (double)(count - 1)) * bounds.Width;
                    double normH = Clamp(hist[i], 0f, 1f);
                    double y = bounds.Bottom - normH * bounds.Height;
                    ctx.LineTo(new Point(x, y), false, false);
                }
                ctx.LineTo(new Point(bounds.Right, bounds.Bottom), false, false);
            }
            geo.Freeze();
            dc.DrawGeometry(histBrush, null, geo);
        }

        private void DrawGrid(DrawingContext dc, Rect bounds)
        {
            int div = Math.Max(2, GridDivisions);
            var gridStroke = GridStroke ?? ZeroWpfTheme.BorderSubtle;
            var gridPen = new Pen(gridStroke, GridThickness);
            gridPen.Freeze();

            for (int i = 1; i < div; i++)
            {
                double fx = bounds.Left + (i / (double)div) * bounds.Width;
                double fy = bounds.Top + (i / (double)div) * bounds.Height;

                dc.DrawLine(gridPen, new Point(fx, bounds.Top), new Point(fx, bounds.Bottom));
                dc.DrawLine(gridPen, new Point(bounds.Left, fy), new Point(bounds.Right, fy));
            }
        }

        private void DrawCurve(DrawingContext dc, Rect bounds)
        {
            var lut = CurveMath.BuildLut(_points);
            int n = CurveMath.LutSize;
            if (n < 2) return;

            var stroke = CurveBrush ?? ZeroWpfTheme.PrimaryAccent;
            var pen = new Pen(stroke, CurveThickness);
            pen.Freeze();

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                double startY = bounds.Bottom - lut[0] * bounds.Height;
                ctx.BeginFigure(new Point(bounds.Left, startY), false, false);

                for (int i = 1; i < n; i++)
                {
                    double x = bounds.Left + (i / (double)(n - 1)) * bounds.Width;
                    double y = bounds.Bottom - lut[i] * bounds.Height;
                    ctx.LineTo(new Point(x, y), true, false);
                }
            }
            geo.Freeze();
            dc.DrawGeometry(null, pen, geo);
        }

        private void DrawControlPoints(DrawingContext dc, Rect bounds)
        {
            double size = ThumbSize;
            double radius = size / 2.0;

            var fillBrush = ThumbBrush ?? ZeroWpfTheme.PrimaryAccent;
            var strokeBrush = ThumbStroke ?? ZeroWpfTheme.TextPrimary;
            var thumbPen = new Pen(strokeBrush, ThumbStrokeThickness);
            thumbPen.Freeze();

            var glowBrush = new SolidColorBrush(Color.FromArgb(60, 0, 122, 255));
            glowBrush.Freeze();

            for (int i = 0; i < _points.Count; i++)
            {
                var pt = ToPixel(_points[i].x, _points[i].y, bounds);
                bool isHovered = (i == _hoverIndex || i == _dragIndex);
                bool isSelected = (i == _selectedIndex);

                if (isHovered || isSelected)
                {
                    dc.DrawEllipse(glowBrush, null, pt, radius + 4, radius + 4);
                }

                dc.DrawEllipse(fillBrush, thumbPen, pt, radius, radius);
            }
        }

        private void DrawCoordinateBadge(DrawingContext dc, Rect bounds, (float x, float y) pt)
        {
            int inVal = (int)Math.Round(pt.x * 255.0);
            int outVal = (int)Math.Round(pt.y * 255.0);
            string text = string.Format(CultureInfo.InvariantCulture, "In: {0}  Out: {1}", inVal, outVal);

            var typeface = ZeroWpfTheme.RegularTypeface;
            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                10.5,
                ZeroWpfTheme.TextPrimary,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double padX = 6.0;
            double padY = 3.0;
            double badgeW = ft.Width + padX * 2;
            double badgeH = ft.Height + padY * 2;

            // Position badge at top right of the curve editor
            double bx = bounds.Right - badgeW - 6.0;
            double by = bounds.Top + 6.0;
            var badgeRect = new Rect(bx, by, badgeW, badgeH);

            var bg = new SolidColorBrush(Color.FromArgb(200, 20, 20, 25));
            bg.Freeze();
            var border = new Pen(ZeroWpfTheme.BorderSubtle, 1.0);
            border.Freeze();

            dc.DrawRoundedRectangle(bg, border, badgeRect, 3.0, 3.0);
            dc.DrawText(ft, new Point(bx + padX, by + padY));
        }

        #endregion

        #region User Interaction

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            UpdateHover(e.GetPosition(this));
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverIndex = -1;
            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var pos = e.GetPosition(this);
            var inner = GetInnerBounds();

            if (_dragIndex >= 0 && !ReadOnly)
            {
                var np = ToNorm(pos, inner);
                float x;
                bool isFirst = (_dragIndex == 0);
                bool isLast = (_dragIndex == _points.Count - 1);

                if (isFirst)
                {
                    x = 0f;
                }
                else if (isLast)
                {
                    x = 1f;
                }
                else
                {
                    float lo = _points[_dragIndex - 1].x + 0.002f;
                    float hi = _points[_dragIndex + 1].x - 0.002f;
                    x = Clamp(np.x, lo, hi);
                }

                _points[_dragIndex] = (x, np.y);
                _isModified = true;
                SyncCurveData();
                InvalidateVisual();
                RaiseChanged();
            }
            else
            {
                UpdateHover(pos);
            }
        }

        private void UpdateHover(Point pos)
        {
            var inner = GetInnerBounds();
            int hit = HitTest(pos, inner);
            if (hit != _hoverIndex)
            {
                _hoverIndex = hit;
                Cursor = hit >= 0 ? Cursors.Hand : Cursors.Cross;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();

            if (ReadOnly) return;

            var pos = e.GetPosition(this);
            var inner = GetInnerBounds();
            int hit = HitTest(pos, inner);

            if (e.ClickCount == 2)
            {
                if (hit > 0 && hit < _points.Count - 1)
                {
                    // Double-click on interior point -> Delete
                    _points.RemoveAt(hit);
                    _selectedIndex = -1;
                    _hoverIndex = -1;
                    _isModified = true;
                    SyncCurveData();
                    InvalidateVisual();
                    RaiseChanged();
                }
                else if (hit < 0)
                {
                    // Double-click on empty space -> Add new point
                    var np = ToNorm(pos, inner);
                    _points.Add(np);
                    _points.Sort((a, b) => a.x.CompareTo(b.x));
                    _selectedIndex = _points.IndexOf(np);
                    _isModified = true;
                    SyncCurveData();
                    InvalidateVisual();
                    RaiseChanged();
                }
                return;
            }

            if (hit >= 0)
            {
                _dragIndex = hit;
                _selectedIndex = hit;
                CaptureMouse();
                InvalidateVisual();
            }
            else
            {
                _selectedIndex = -1;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_dragIndex >= 0)
            {
                _dragIndex = -1;
                ReleaseMouseCapture();
                InvalidateVisual();
            }
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            if (ReadOnly) return;

            var pos = e.GetPosition(this);
            var inner = GetInnerBounds();
            int hit = HitTest(pos, inner);

            if (hit > 0 && hit < _points.Count - 1)
            {
                // Right click on intermediate point -> Delete
                _points.RemoveAt(hit);
                _selectedIndex = -1;
                _hoverIndex = -1;
                _isModified = true;
                SyncCurveData();
                InvalidateVisual();
                RaiseChanged();
                e.Handled = true;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (ReadOnly || _selectedIndex < 0 || _selectedIndex >= _points.Count) return;

            float step = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 0.002f : 0.01f;
            var (x, y) = _points[_selectedIndex];
            bool modified = false;

            if (e.Key == Key.Up)
            {
                y = Clamp(y + step, 0f, 1f);
                modified = true;
            }
            else if (e.Key == Key.Down)
            {
                y = Clamp(y - step, 0f, 1f);
                modified = true;
            }
            else if (e.Key == Key.Left && _selectedIndex > 0 && _selectedIndex < _points.Count - 1)
            {
                float lo = _points[_selectedIndex - 1].x + 0.002f;
                x = Clamp(x - step, lo, 1f);
                modified = true;
            }
            else if (e.Key == Key.Right && _selectedIndex > 0 && _selectedIndex < _points.Count - 1)
            {
                float hi = _points[_selectedIndex + 1].x - 0.002f;
                x = Clamp(x + step, 0f, hi);
                modified = true;
            }
            else if ((e.Key == Key.Delete || e.Key == Key.Back) && _selectedIndex > 0 && _selectedIndex < _points.Count - 1)
            {
                _points.RemoveAt(_selectedIndex);
                _selectedIndex = -1;
                _isModified = true;
                SyncCurveData();
                InvalidateVisual();
                RaiseChanged();
                e.Handled = true;
                return;
            }

            if (modified)
            {
                _points[_selectedIndex] = (x, y);
                _isModified = true;
                SyncCurveData();
                InvalidateVisual();
                RaiseChanged();
                e.Handled = true;
            }
        }

        #endregion

        #region Helpers

        private void SyncCurveData()
        {
            _isUpdatingFromDp = true;
            CurveData = CurveMath.Serialize(_points);
            _isUpdatingFromDp = false;
        }

        private void RaiseChanged()
        {
            if (_suppressEvents) return;
            string serialized = CurveMath.Serialize(_points);
            CurveChanged?.Invoke(this, serialized);
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        private static float Clamp(float val, float min, float max)
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
    /// Legacy alias for <see cref="ZCurveEditor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("CurveEditor is deprecated. Use ZCurveEditor instead.")]
    public class CurveEditor : ZCurveEditor
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZCurveEditor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroCurveEditor is deprecated. Use ZCurveEditor instead.")]
    public class ZeroCurveEditor : ZCurveEditor
    {
    }
    #endregion
}
