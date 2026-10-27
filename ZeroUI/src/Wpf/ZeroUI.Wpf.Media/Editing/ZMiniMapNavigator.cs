using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Media
{
    /// <summary>
    /// Interactive Mini-Map Navigator control for ZeroUI in WPF.
    /// Renders an aspect-fit image thumbnail with an interactive translucent viewport bounding box.
    /// Dragging the viewport box or clicking anywhere on the thumbnail pans the main viewer.
    /// Renders directly using <see cref="DrawingContext"/> for maximum performance.
    /// </summary>
    public class ZMiniMapNavigator : FrameworkElement, IZeroEditor
    {
        private bool _isDraggingViewport;
        private Point _dragStartMouse;
        private Rect _viewportAtDragStart;
        private bool _isModified;
        private bool _isUpdatingFromDp;

        #region Dependency Properties

        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register(nameof(ImageSource), typeof(ImageSource), typeof(ZMiniMapNavigator),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ViewportRectProperty =
            DependencyProperty.Register(nameof(ViewportRect), typeof(Rect), typeof(ZMiniMapNavigator),
                new FrameworkPropertyMetadata(new Rect(0, 0, 1, 1), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnViewportRectChanged));

        public static readonly DependencyProperty ViewportStrokeProperty =
            DependencyProperty.Register(nameof(ViewportStroke), typeof(Brush), typeof(ZMiniMapNavigator),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ViewportFillProperty =
            DependencyProperty.Register(nameof(ViewportFill), typeof(Brush), typeof(ZMiniMapNavigator),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ViewportThicknessProperty =
            DependencyProperty.Register(nameof(ViewportThickness), typeof(double), typeof(ZMiniMapNavigator),
                new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowDimOutsideProperty =
            DependencyProperty.Register(nameof(ShowDimOutside), typeof(bool), typeof(ZMiniMapNavigator),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(ZMiniMapNavigator),
                new FrameworkPropertyMetadata(false));

        #endregion

        #region Properties & Events

        public ImageSource? ImageSource
        {
            get => (ImageSource?)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        /// <summary>
        /// Gets or sets the normalized viewport rectangle [0..1] x [0..1].
        /// </summary>
        public Rect ViewportRect
        {
            get => (Rect)GetValue(ViewportRectProperty);
            set => SetValue(ViewportRectProperty, ClampRect(value));
        }

        public Brush? ViewportStroke
        {
            get => (Brush?)GetValue(ViewportStrokeProperty);
            set => SetValue(ViewportStrokeProperty, value);
        }

        public Brush? ViewportFill
        {
            get => (Brush?)GetValue(ViewportFillProperty);
            set => SetValue(ViewportFillProperty, value);
        }

        public double ViewportThickness
        {
            get => (double)GetValue(ViewportThicknessProperty);
            set => SetValue(ViewportThicknessProperty, value);
        }

        public bool ShowDimOutside
        {
            get => (bool)GetValue(ShowDimOutsideProperty);
            set => SetValue(ShowDimOutsideProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        /// <summary>
        /// Occurs when the viewport position is moved by dragging or clicking.
        /// </summary>
        public event EventHandler<Rect>? ViewportMoved;

        #endregion

        #region IZeroEditor Implementation

        public object? EditValue
        {
            get => ViewportRect;
            set
            {
                if (value is Rect r) ViewportRect = r;
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
            ViewportRect = new Rect(0, 0, 1, 1);
            _isModified = false;
            InvalidateVisual();
        }

        public void Clear()
        {
            ImageSource = null;
            Reset();
        }

        #endregion

        public ZMiniMapNavigator()
        {
            Width = 140;
            Height = 110;
            Focusable = true;
            ClipToBounds = true;
            Cursor = Cursors.Hand;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static void OnViewportRectChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZMiniMapNavigator nav && !nav._isUpdatingFromDp)
            {
                nav.ViewportMoved?.Invoke(nav, (Rect)e.NewValue);
                nav.EditValueChanged?.Invoke(nav, EventArgs.Empty);
            }
        }

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            // 1. Dark container background
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(255, 17, 17, 20)), null, new Rect(0, 0, w, h));

            var src = ImageSource;
            if (src == null || src.Width <= 0 || src.Height <= 0)
            {
                // Fallback "No Image" text
                var ft = new FormattedText(
                    "No Image",
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.RegularTypeface,
                    10.0,
                    ZeroWpfTheme.TextMuted,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                dc.DrawText(ft, new Point((w - ft.Width) / 2.0, (h - ft.Height) / 2.0));
                return;
            }

            // 2. Compute aspect-fit destination rectangle inside control bounds
            var imgDest = GetImageDestRect(w, h, src.Width, src.Height);

            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            dc.DrawImage(src, imgDest);

            // 3. Compute Viewport Box in pixel coordinates
            var vr = ViewportRect;
            bool isFullView = vr.Width >= 0.999 && vr.Height >= 0.999;
            if (isFullView) return; // Full view -> no viewport rectangle needed

            double vx = imgDest.Left + vr.X * imgDest.Width;
            double vy = imgDest.Top + vr.Y * imgDest.Height;
            double vw = Math.Max(4.0, vr.Width * imgDest.Width);
            double vh = Math.Max(4.0, vr.Height * imgDest.Height);
            var vpPixelRect = new Rect(vx, vy, vw, vh);

            // 4. Outside dim shades if enabled
            if (ShowDimOutside)
            {
                var dim = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0));
                dim.Freeze();
                if (vy > imgDest.Top) dc.DrawRectangle(dim, null, new Rect(imgDest.Left, imgDest.Top, imgDest.Width, vy - imgDest.Top));
                if (vy + vh < imgDest.Bottom) dc.DrawRectangle(dim, null, new Rect(imgDest.Left, vy + vh, imgDest.Width, imgDest.Bottom - (vy + vh)));
                if (vx > imgDest.Left) dc.DrawRectangle(dim, null, new Rect(imgDest.Left, vy, vx - imgDest.Left, vh));
                if (vx + vw < imgDest.Right) dc.DrawRectangle(dim, null, new Rect(vx + vw, vy, imgDest.Right - (vx + vw), vh));
            }

            // 5. Viewport box outline & fill
            var strokeBrush = ViewportStroke ?? new SolidColorBrush(Color.FromArgb(255, 79, 195, 247)); // #4FC3F7
            strokeBrush.Freeze();
            var fillBrush = ViewportFill ?? new SolidColorBrush(Color.FromArgb(48, 79, 195, 247));      // #304FC3F7
            fillBrush.Freeze();

            var pen = new Pen(strokeBrush, ViewportThickness);
            pen.Freeze();

            dc.DrawRectangle(fillBrush, pen, vpPixelRect);
        }

        private static Rect GetImageDestRect(double availW, double availH, double srcW, double srcH)
        {
            double scale = Math.Min(availW / srcW, availH / srcH);
            double destW = srcW * scale;
            double destH = srcH * scale;
            double left = (availW - destW) / 2.0;
            double top = (availH - destH) / 2.0;
            return new Rect(left, top, destW, destH);
        }

        #endregion

        #region User Interaction

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            if (ReadOnly || ImageSource == null) return;

            Point p = e.GetPosition(this);
            var imgDest = GetImageDestRect(ActualWidth, ActualHeight, ImageSource.Width, ImageSource.Height);
            if (imgDest.Width <= 0 || imgDest.Height <= 0) return;

            var vr = ViewportRect;
            double vx = imgDest.Left + vr.X * imgDest.Width;
            double vy = imgDest.Top + vr.Y * imgDest.Height;
            double vw = vr.Width * imgDest.Width;
            double vh = vr.Height * imgDest.Height;
            var vpPixelRect = new Rect(vx, vy, vw, vh);

            if (vpPixelRect.Contains(p))
            {
                // Clicked inside viewport box -> Drag it
                _isDraggingViewport = true;
                _dragStartMouse = p;
                _viewportAtDragStart = vr;
                CaptureMouse();
                e.Handled = true;
            }
            else if (imgDest.Contains(p))
            {
                // Clicked on thumbnail outside viewport box -> Center viewport on clicked point
                double normClickX = (p.X - imgDest.Left) / imgDest.Width;
                double normClickY = (p.Y - imgDest.Top) / imgDest.Height;

                double newX = normClickX - vr.Width / 2.0;
                double newY = normClickY - vr.Height / 2.0;

                SetViewportPosition(newX, newY);
                e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDraggingViewport && !ReadOnly && ImageSource != null)
            {
                Point p = e.GetPosition(this);
                var imgDest = GetImageDestRect(ActualWidth, ActualHeight, ImageSource.Width, ImageSource.Height);
                if (imgDest.Width <= 0 || imgDest.Height <= 0) return;

                double dx = (p.X - _dragStartMouse.X) / imgDest.Width;
                double dy = (p.Y - _dragStartMouse.Y) / imgDest.Height;

                double newX = _viewportAtDragStart.X + dx;
                double newY = _viewportAtDragStart.Y + dy;

                SetViewportPosition(newX, newY);
                e.Handled = true;
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_isDraggingViewport)
            {
                _isDraggingViewport = false;
                ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void SetViewportPosition(double x, double y)
        {
            var vr = ViewportRect;
            double clampedX = Clamp(x, 0.0, 1.0 - vr.Width);
            double clampedY = Clamp(y, 0.0, 1.0 - vr.Height);

            _isUpdatingFromDp = true;
            ViewportRect = new Rect(clampedX, clampedY, vr.Width, vr.Height);
            _isUpdatingFromDp = false;
            _isModified = true;

            InvalidateVisual();
            ViewportMoved?.Invoke(this, ViewportRect);
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Helpers

        private static Rect ClampRect(Rect r)
        {
            double w = Clamp(r.Width, 0.01, 1.0);
            double h = Clamp(r.Height, 0.01, 1.0);
            double x = Clamp(r.X, 0.0, 1.0 - w);
            double y = Clamp(r.Y, 0.0, 1.0 - h);
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
    /// Legacy alias for <see cref="ZMiniMapNavigator"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("MiniMapNavigator is deprecated. Use ZMiniMapNavigator instead.")]
    public class MiniMapNavigator : ZMiniMapNavigator
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZMiniMapNavigator"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroMiniMapNavigator is deprecated. Use ZMiniMapNavigator instead.")]
    public class ZeroMiniMapNavigator : ZMiniMapNavigator
    {
    }
    #endregion
}
