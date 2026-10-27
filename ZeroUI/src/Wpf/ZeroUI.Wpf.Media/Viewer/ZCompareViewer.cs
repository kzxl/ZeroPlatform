using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Media
{
    /// <summary>
    /// Comparison display modes for <see cref="ZCompareViewer"/>.
    /// </summary>
    public enum CompareViewMode
    {
        /// <summary>Two viewports side-by-side with synchronized zoom and pan.</summary>
        SideBySide,
        /// <summary>Curtain split slider revealing Before and After on the same viewport.</summary>
        SplitCurtain,
        /// <summary>Alpha blend cross-fading smoothly between Before and After.</summary>
        FadeBlend
    }

    /// <summary>
    /// High-performance Before/After image comparison control for ZeroUI in WPF.
    /// Supports synchronized dual-pane zoom & pan, interactive draggable curtain split line,
    /// cross-fade blend, and custom metadata badges.
    /// Renders directly using <see cref="DrawingContext"/> for maximum frame rates.
    /// </summary>
    public class ZCompareViewer : FrameworkElement, IZeroEditor
    {
        private bool _isDraggingSplitter;
        private bool _isPanning;
        private Point _panStartMouse;
        private Point _panStartOffset;
        private bool _isModified;

        #region Dependency Properties

        public static readonly DependencyProperty BeforeSourceProperty =
            DependencyProperty.Register(nameof(BeforeSource), typeof(ImageSource), typeof(ZCompareViewer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AfterSourceProperty =
            DependencyProperty.Register(nameof(AfterSource), typeof(ImageSource), typeof(ZCompareViewer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ModeProperty =
            DependencyProperty.Register(nameof(Mode), typeof(CompareViewMode), typeof(ZCompareViewer),
                new FrameworkPropertyMetadata(CompareViewMode.SideBySide, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SplitPositionProperty =
            DependencyProperty.Register(nameof(SplitPosition), typeof(double), typeof(ZCompareViewer),
                new FrameworkPropertyMetadata(0.5, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(ZCompareViewer),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnZoomChanged));

        public static readonly DependencyProperty PanOffsetProperty =
            DependencyProperty.Register(nameof(PanOffset), typeof(Point), typeof(ZCompareViewer),
                new FrameworkPropertyMetadata(new Point(0, 0), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BeforeLabelProperty =
            DependencyProperty.Register(nameof(BeforeLabel), typeof(string), typeof(ZCompareViewer),
                new FrameworkPropertyMetadata("BEFORE", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AfterLabelProperty =
            DependencyProperty.Register(nameof(AfterLabel), typeof(string), typeof(ZCompareViewer),
                new FrameworkPropertyMetadata("AFTER", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowBadgesProperty =
            DependencyProperty.Register(nameof(ShowBadges), typeof(bool), typeof(ZCompareViewer),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CurtainBrushProperty =
            DependencyProperty.Register(nameof(CurtainBrush), typeof(Brush), typeof(ZCompareViewer),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CurtainThicknessProperty =
            DependencyProperty.Register(nameof(CurtainThickness), typeof(double), typeof(ZCompareViewer),
                new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

        #endregion

        #region Properties & Events

        public ImageSource? BeforeSource
        {
            get => (ImageSource?)GetValue(BeforeSourceProperty);
            set => SetValue(BeforeSourceProperty, value);
        }

        public ImageSource? AfterSource
        {
            get => (ImageSource?)GetValue(AfterSourceProperty);
            set => SetValue(AfterSourceProperty, value);
        }

        public CompareViewMode Mode
        {
            get => (CompareViewMode)GetValue(ModeProperty);
            set => SetValue(ModeProperty, value);
        }

        public double SplitPosition
        {
            get => (double)GetValue(SplitPositionProperty);
            set => SetValue(SplitPositionProperty, Clamp(value, 0.0, 1.0));
        }

        public double Zoom
        {
            get => (double)GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, Clamp(value, 1.0, 16.0));
        }

        public Point PanOffset
        {
            get => (Point)GetValue(PanOffsetProperty);
            set => SetValue(PanOffsetProperty, value);
        }

        public string BeforeLabel
        {
            get => (string)GetValue(BeforeLabelProperty);
            set => SetValue(BeforeLabelProperty, value);
        }

        public string AfterLabel
        {
            get => (string)GetValue(AfterLabelProperty);
            set => SetValue(AfterLabelProperty, value);
        }

        public bool ShowBadges
        {
            get => (bool)GetValue(ShowBadgesProperty);
            set => SetValue(ShowBadgesProperty, value);
        }

        public Brush? CurtainBrush
        {
            get => (Brush?)GetValue(CurtainBrushProperty);
            set => SetValue(CurtainBrushProperty, value);
        }

        public double CurtainThickness
        {
            get => (double)GetValue(CurtainThicknessProperty);
            set => SetValue(CurtainThicknessProperty, value);
        }

        public event EventHandler<double>? SplitPositionChanged;
        public event EventHandler<double>? ZoomChanged;

        #endregion

        #region IZeroEditor Implementation

        public object? EditValue
        {
            get => SplitPosition;
            set
            {
                if (value is double d) SplitPosition = d;
                else if (value is float f) SplitPosition = f;
            }
        }

        public event EventHandler? EditValueChanged;

        public bool IsModified
        {
            get => _isModified;
            set => _isModified = value;
        }

        public bool ReadOnly { get; set; }

        public void Reset()
        {
            Zoom = 1.0;
            PanOffset = new Point(0, 0);
            SplitPosition = 0.5;
            _isModified = false;
            InvalidateVisual();
        }

        public void Clear()
        {
            BeforeSource = null;
            AfterSource = null;
            Reset();
        }

        #endregion

        public ZCompareViewer()
        {
            Focusable = true;
            ClipToBounds = true;
            Cursor = Cursors.Arrow;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZCompareViewer ctrl)
            {
                ctrl.ZoomChanged?.Invoke(ctrl, (double)e.NewValue);
            }
        }

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 0 || height <= 0) return;

            // Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, new Rect(0, 0, width, height));

            switch (Mode)
            {
                case CompareViewMode.SideBySide:
                    RenderSideBySide(dc, width, height);
                    break;
                case CompareViewMode.SplitCurtain:
                    RenderSplitCurtain(dc, width, height);
                    break;
                case CompareViewMode.FadeBlend:
                    RenderFadeBlend(dc, width, height);
                    break;
            }
        }

        private void RenderSideBySide(DrawingContext dc, double width, double height)
        {
            double halfW = width / 2.0;

            // Left Pane: Before
            var leftRect = new Rect(0, 0, halfW, height);
            dc.PushClip(new RectangleGeometry(leftRect));
            DrawImageInViewport(dc, BeforeSource, leftRect, Zoom, PanOffset);
            if (ShowBadges && !string.IsNullOrEmpty(BeforeLabel))
            {
                DrawBadge(dc, BeforeLabel, new Point(12, 12), Color.FromArgb(180, 20, 20, 20), Brushes.White);
            }
            dc.Pop();

            // Right Pane: After
            var rightRect = new Rect(halfW, 0, halfW, height);
            dc.PushClip(new RectangleGeometry(rightRect));
            DrawImageInViewport(dc, AfterSource, rightRect, Zoom, PanOffset);
            if (ShowBadges && !string.IsNullOrEmpty(AfterLabel))
            {
                DrawBadge(dc, AfterLabel, new Point(halfW + 12, 12), Color.FromArgb(200, 0, 122, 255), Brushes.White);
            }
            dc.Pop();

            // Center Divider
            var dividerPen = new Pen(ZeroWpfTheme.BorderSubtle, 1.0);
            dividerPen.Freeze();
            dc.DrawLine(dividerPen, new Point(halfW, 0), new Point(halfW, height));
        }

        private void RenderSplitCurtain(DrawingContext dc, double width, double height)
        {
            var fullRect = new Rect(0, 0, width, height);
            double splitX = width * SplitPosition;

            // 1. Draw After (full background)
            dc.PushClip(new RectangleGeometry(fullRect));
            DrawImageInViewport(dc, AfterSource, fullRect, Zoom, PanOffset);
            dc.Pop();

            // 2. Draw Before (clipped to left of split)
            var beforeClip = new Rect(0, 0, splitX, height);
            dc.PushClip(new RectangleGeometry(beforeClip));
            DrawImageInViewport(dc, BeforeSource, fullRect, Zoom, PanOffset);
            dc.Pop();

            // 3. Draw Curtain Line
            var lineBrush = CurtainBrush ?? ZeroWpfTheme.PrimaryAccent;
            var linePen = new Pen(lineBrush, CurtainThickness);
            linePen.Freeze();
            dc.DrawLine(linePen, new Point(splitX, 0), new Point(splitX, height));

            // 4. Draw Curtain Circular Handle with Chevrons
            double handleRadius = 14.0;
            Point handleCenter = new Point(splitX, height / 2.0);

            var handleBg = new SolidColorBrush(Color.FromArgb(230, 20, 20, 25));
            handleBg.Freeze();
            var handleBorder = new Pen(lineBrush, 1.5);
            handleBorder.Freeze();

            dc.DrawEllipse(handleBg, handleBorder, handleCenter, handleRadius, handleRadius);

            // Draw chevrons: < >
            var text = "◀ ▶";
            var typeface = ZeroWpfTheme.BoldTypeface;
            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                8.5,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(ft, new Point(handleCenter.X - ft.Width / 2.0, handleCenter.Y - ft.Height / 2.0));

            // 5. Floating Badges
            if (ShowBadges)
            {
                if (!string.IsNullOrEmpty(BeforeLabel) && splitX > 60)
                {
                    DrawBadge(dc, BeforeLabel, new Point(12, 12), Color.FromArgb(180, 20, 20, 20), Brushes.White);
                }
                if (!string.IsNullOrEmpty(AfterLabel) && (width - splitX) > 60)
                {
                    DrawBadge(dc, AfterLabel, new Point(width - 60, 12), Color.FromArgb(200, 0, 122, 255), Brushes.White);
                }
            }
        }

        private void RenderFadeBlend(DrawingContext dc, double width, double height)
        {
            var fullRect = new Rect(0, 0, width, height);
            double blend = SplitPosition;

            // Before (opacity 1 - blend)
            if (blend < 0.999 && BeforeSource != null)
            {
                dc.PushOpacity(1.0 - blend);
                DrawImageInViewport(dc, BeforeSource, fullRect, Zoom, PanOffset);
                dc.Pop();
            }

            // After (opacity blend)
            if (blend > 0.001 && AfterSource != null)
            {
                dc.PushOpacity(blend);
                DrawImageInViewport(dc, AfterSource, fullRect, Zoom, PanOffset);
                dc.Pop();
            }

            if (ShowBadges)
            {
                string badgeText = string.Format(CultureInfo.InvariantCulture, "BLEND: {0:0}% AFTER", blend * 100.0);
                DrawBadge(dc, badgeText, new Point(12, 12), Color.FromArgb(200, 15, 15, 20), Brushes.White);
            }
        }

        private void DrawImageInViewport(DrawingContext dc, ImageSource? src, Rect viewport, double zoom, Point pan)
        {
            if (src == null || viewport.Width <= 0 || viewport.Height <= 0) return;

            double srcW = src.Width;
            double srcH = src.Height;
            if (srcW <= 0 || srcH <= 0) return;

            // Aspect fit into viewport
            double scale = Math.Min(viewport.Width / srcW, viewport.Height / srcH) * zoom;
            double destW = srcW * scale;
            double destH = srcH * scale;

            double destX = viewport.Left + (viewport.Width - destW) / 2.0 + pan.X;
            double destY = viewport.Top + (viewport.Height - destH) / 2.0 + pan.Y;

            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            dc.DrawImage(src, new Rect(destX, destY, destW, destH));
        }

        private void DrawBadge(DrawingContext dc, string text, Point pos, Color bgCol, Brush textBrush)
        {
            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                ZeroWpfTheme.BoldTypeface,
                10.0,
                textBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double padX = 7.0;
            double padY = 3.0;
            var badgeRect = new Rect(pos.X, pos.Y, ft.Width + padX * 2, ft.Height + padY * 2);

            var bg = new SolidColorBrush(bgCol);
            bg.Freeze();
            var border = new Pen(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 0.8);
            border.Freeze();

            dc.DrawRoundedRectangle(bg, border, badgeRect, 3.0, 3.0);
            dc.DrawText(ft, new Point(pos.X + padX, pos.Y + padY));
        }

        #endregion

        #region Mouse & Keyboard Interaction

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            double factor = e.Delta > 0 ? 1.2 : (1.0 / 1.2);
            double newZoom = Clamp(Zoom * factor, 1.0, 16.0);
            if (Math.Abs(newZoom - Zoom) > 1e-4)
            {
                Zoom = newZoom;
                if (Zoom <= 1.01) PanOffset = new Point(0, 0);
                InvalidateVisual();
                e.Handled = true;
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();

            if (e.ClickCount == 2)
            {
                Reset();
                e.Handled = true;
                return;
            }

            Point p = e.GetPosition(this);

            if (Mode == CompareViewMode.SplitCurtain)
            {
                double splitX = ActualWidth * SplitPosition;
                if (Math.Abs(p.X - splitX) <= 18.0)
                {
                    _isDraggingSplitter = true;
                    CaptureMouse();
                    Cursor = Cursors.SizeWE;
                    e.Handled = true;
                    return;
                }
            }

            if (Zoom > 1.01)
            {
                _isPanning = true;
                _panStartMouse = p;
                _panStartOffset = PanOffset;
                CaptureMouse();
                Cursor = Cursors.ScrollAll;
                e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point p = e.GetPosition(this);

            if (_isDraggingSplitter)
            {
                SplitPosition = Clamp(p.X / Math.Max(1.0, ActualWidth), 0.02, 0.98);
                _isModified = true;
                SplitPositionChanged?.Invoke(this, SplitPosition);
                EditValueChanged?.Invoke(this, EventArgs.Empty);
                InvalidateVisual();
            }
            else if (_isPanning)
            {
                double dx = p.X - _panStartMouse.X;
                double dy = p.Y - _panStartMouse.Y;
                PanOffset = new Point(_panStartOffset.X + dx, _panStartOffset.Y + dy);
                InvalidateVisual();
            }
            else if (Mode == CompareViewMode.SplitCurtain)
            {
                double splitX = ActualWidth * SplitPosition;
                Cursor = Math.Abs(p.X - splitX) <= 18.0 ? Cursors.SizeWE : (Zoom > 1.01 ? Cursors.ScrollAll : Cursors.Arrow);
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_isDraggingSplitter || _isPanning)
            {
                _isDraggingSplitter = false;
                _isPanning = false;
                ReleaseMouseCapture();
                Cursor = Cursors.Arrow;
                InvalidateVisual();
            }
        }

        #endregion

        private static double Clamp(double val, double min, double max)
        {
            if (val < min) return min;
            if (val > max) return max;
            return val;
        }
    
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
    /// Backward-compatibility alias for <see cref="ZCompareViewer"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("CompareViewerControl is deprecated and will be removed in 5 release cycles. Please migrate to ZCompareViewer instead.")]
    public class CompareViewerControl : ZCompareViewer
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZCompareViewer"/>.
    /// </summary>
    [Obsolete("ZeroCompareViewer is deprecated and will be removed in 5 release cycles. Please migrate to ZCompareViewer instead.")]
    public class ZeroCompareViewer : ZCompareViewer
    {
    }

    #endregion
}
