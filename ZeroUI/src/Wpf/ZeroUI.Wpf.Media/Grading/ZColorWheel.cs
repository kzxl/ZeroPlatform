using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Media
{
    /// <summary>
    /// Interactive 360° Color Grading Wheel control for ZeroUI in WPF.
    /// Provides continuous Hue angle (0°–360°) and Saturation radius (0.0–1.0) selection
    /// matching professional color correction suites (Lightroom, DaVinci Resolve).
    /// Uses a pre-rendered high-fidelity anti-aliased gamut bitmap and direct <see cref="DrawingContext"/>
    /// rendering for zero visual-tree overhead.
    /// </summary>
    public class ZColorWheel : FrameworkElement, IZeroEditor
    {
        private static BitmapSource? s_cachedWheelBitmap;
        private static readonly object s_bitmapLock = new object();

        private bool _dragging;
        private bool _suppressEvents;
        private bool _isUpdatingFromDp;
        private bool _isModified;
        private bool _isHovered;

        #region Dependency Properties

        public static readonly DependencyProperty HueProperty =
            DependencyProperty.Register(nameof(Hue), typeof(float), typeof(ZColorWheel),
                new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnHueChanged));

        public static readonly DependencyProperty SaturationProperty =
            DependencyProperty.Register(nameof(Saturation), typeof(float), typeof(ZColorWheel),
                new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnSaturationChanged));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(float), typeof(ZColorWheel),
                new FrameworkPropertyMetadata(1f, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnValueChanged));

        public static readonly DependencyProperty ThumbSizeProperty =
            DependencyProperty.Register(nameof(ThumbSize), typeof(double), typeof(ZColorWheel),
                new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ThumbStrokeProperty =
            DependencyProperty.Register(nameof(ThumbStroke), typeof(Brush), typeof(ZColorWheel),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty RingStrokeProperty =
            DependencyProperty.Register(nameof(RingStroke), typeof(Brush), typeof(ZColorWheel),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty RingThicknessProperty =
            DependencyProperty.Register(nameof(RingThickness), typeof(double), typeof(ZColorWheel),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowCrosshairProperty =
            DependencyProperty.Register(nameof(ShowCrosshair), typeof(bool), typeof(ZColorWheel),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowTooltipProperty =
            DependencyProperty.Register(nameof(ShowTooltip), typeof(bool), typeof(ZColorWheel),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(ZColorWheel),
                new FrameworkPropertyMetadata(false));

        #endregion

        #region Properties & Events

        public float Hue
        {
            get => (float)GetValue(HueProperty);
            set => SetValue(HueProperty, value);
        }

        public float Saturation
        {
            get => (float)GetValue(SaturationProperty);
            set => SetValue(SaturationProperty, value);
        }

        /// <summary>
        /// Alias for <see cref="Saturation"/> for backward compatibility.
        /// </summary>
        public float Sat
        {
            get => Saturation;
            set => Saturation = value;
        }

        public float Value
        {
            get => (float)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double ThumbSize
        {
            get => (double)GetValue(ThumbSizeProperty);
            set => SetValue(ThumbSizeProperty, value);
        }

        public Brush? ThumbStroke
        {
            get => (Brush?)GetValue(ThumbStrokeProperty);
            set => SetValue(ThumbStrokeProperty, value);
        }

        public Brush? RingStroke
        {
            get => (Brush?)GetValue(RingStrokeProperty);
            set => SetValue(RingStrokeProperty, value);
        }

        public double RingThickness
        {
            get => (double)GetValue(RingThicknessProperty);
            set => SetValue(RingThicknessProperty, value);
        }

        public bool ShowCrosshair
        {
            get => (bool)GetValue(ShowCrosshairProperty);
            set => SetValue(ShowCrosshairProperty, value);
        }

        public bool ShowTooltip
        {
            get => (bool)GetValue(ShowTooltipProperty);
            set => SetValue(ShowTooltipProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public Color SelectedColor => HsvToColor(Hue, Saturation, Value);

        /// <summary>
        /// Fired when Hue or Saturation changes via user interaction.
        /// Passes (hue 0..360, sat 0..1) tuple for 100% backward compatibility with ZVision.
        /// </summary>
        public event EventHandler<(float hue, float sat)>? ColorChanged;

        /// <summary>
        /// Fired when the calculated RGB color changes.
        /// </summary>
        public event EventHandler<Color>? SelectedColorChanged;

        #endregion

        #region IZeroEditor Implementation

        public object? EditValue
        {
            get => string.Format(CultureInfo.InvariantCulture, "{0:F1},{1:F3}", Hue, Saturation);
            set
            {
                if (value is ValueTuple<float, float> hs)
                {
                    SetValue(hs.Item1, hs.Item2);
                }
                else if (value is string str)
                {
                    var parts = str.Split(',');
                    if (parts.Length == 2 &&
                        float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var h) &&
                        float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var s))
                    {
                        SetValue(h, s);
                    }
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
            SetValue(0f, 0f);
            _isModified = false;
        }

        public void Clear() => Reset();

        #endregion

        public ZColorWheel()
        {
            Width = 120;
            Height = 120;
            Focusable = true;
            Cursor = Cursors.Hand;

            EnsureWheelBitmap();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// Sets the Hue and Saturation values programmatically.
        /// </summary>
        public void SetValue(float hue, float sat)
        {
            _suppressEvents = true;
            Hue = ((hue % 360f) + 360f) % 360f;
            Saturation = Clamp(sat, 0f, 1f);
            _suppressEvents = false;
            InvalidateVisual();
        }

        #region DependencyProperty Callbacks

        private static void OnHueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZColorWheel wheel && !wheel._isUpdatingFromDp)
            {
                wheel.InvalidateVisual();
            }
        }

        private static void OnSaturationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZColorWheel wheel && !wheel._isUpdatingFromDp)
            {
                wheel.InvalidateVisual();
            }
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZColorWheel wheel && !wheel._isUpdatingFromDp)
            {
                wheel.InvalidateVisual();
            }
        }

        #endregion

        #region Wheel Bitmap Generation

        private static void EnsureWheelBitmap()
        {
            if (s_cachedWheelBitmap != null) return;

            lock (s_bitmapLock)
            {
                if (s_cachedWheelBitmap != null) return;

                const int size = 256;
                const double radius = (size / 2.0) - 2.0;
                double cx = size / 2.0;
                double cy = size / 2.0;

                int stride = size * 4;
                byte[] pixels = new byte[stride * size];

                for (int y = 0; y < size; y++)
                {
                    int rowOffset = y * stride;
                    for (int x = 0; x < size; x++)
                    {
                        double dx = x - cx;
                        double dy = cy - y; // y inverted for Cartesian
                        double dist = Math.Sqrt(dx * dx + dy * dy);

                        int px = rowOffset + x * 4;

                        if (dist > radius)
                        {
                            // Fully transparent outside radius
                            pixels[px + 0] = 0;
                            pixels[px + 1] = 0;
                            pixels[px + 2] = 0;
                            pixels[px + 3] = 0;
                            continue;
                        }

                        double ang = Math.Atan2(dy, dx);
                        if (ang < 0.0) ang += 2.0 * Math.PI;

                        float hue = (float)(ang * 180.0 / Math.PI);
                        float sat = (float)Math.Min(1.0, dist / radius);

                        // Anti-aliasing alpha at outer boundary
                        byte alpha = 255;
                        if (dist > radius - 1.0)
                        {
                            alpha = (byte)Clamp((radius - dist) * 255.0, 0.0, 255.0);
                        }

                        var (r, g, b) = HsvToRgb(hue, sat, 1.0f);

                        // BGRA format
                        pixels[px + 0] = b;
                        pixels[px + 1] = g;
                        pixels[px + 2] = r;
                        pixels[px + 3] = alpha;
                    }
                }

                var bmp = BitmapSource.Create(
                    size, size, 96, 96,
                    PixelFormats.Bgra32, null,
                    pixels, stride);

                bmp.Freeze();
                s_cachedWheelBitmap = bmp;
            }
        }

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 0 || height <= 0) return;

            double radius = Math.Min(width, height) / 2.0 - (ThumbSize / 2.0 + 2.0);
            if (radius <= 2.0) return;

            Point center = new Point(width / 2.0, height / 2.0);
            Rect wheelRect = new Rect(center.X - radius, center.Y - radius, radius * 2.0, radius * 2.0);

            // 1. Draw pre-rendered color wheel bitmap
            EnsureWheelBitmap();
            if (s_cachedWheelBitmap != null)
            {
                RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
                dc.DrawImage(s_cachedWheelBitmap, wheelRect);
            }

            // 2. Subtle outer ring
            var ringStroke = RingStroke ?? ZeroWpfTheme.BorderSubtle;
            var ringPen = new Pen(ringStroke, RingThickness);
            ringPen.Freeze();
            dc.DrawEllipse(null, ringPen, center, radius, radius);

            // 3. Optional center neutral crosshair
            if (ShowCrosshair)
            {
                var crossPen = new Pen(new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), 0.8);
                crossPen.Freeze();
                dc.DrawLine(crossPen, new Point(center.X - 4, center.Y), new Point(center.X + 4, center.Y));
                dc.DrawLine(crossPen, new Point(center.X, center.Y - 4), new Point(center.X, center.Y + 4));
            }

            // 4. Thumb handle
            DrawThumb(dc, center, radius);

            // 5. Readout badge when dragging / hovered
            if (ShowTooltip && (_dragging || _isHovered) && Saturation > 0.001f)
            {
                DrawBadge(dc, center, radius);
            }
        }

        private void DrawThumb(DrawingContext dc, Point center, double radius)
        {
            double ang = Hue * Math.PI / 180.0;
            double r = Saturation * radius;
            Point thumbPos = new Point(center.X + Math.Cos(ang) * r, center.Y - Math.Sin(ang) * r);

            double thumbSize = ThumbSize;
            double thumbRadius = thumbSize / 2.0;

            // Outer glow when active/hovered
            if (_dragging || _isHovered)
            {
                var glowBrush = new SolidColorBrush(Color.FromArgb(60, 0, 122, 255));
                glowBrush.Freeze();
                dc.DrawEllipse(glowBrush, null, thumbPos, thumbRadius + 3.5, thumbRadius + 3.5);
            }

            // Outer ring (white with drop shadow effect)
            var outerPen = new Pen(ThumbStroke ?? Brushes.White, 2.0);
            outerPen.Freeze();

            // Inner fill with currently selected color
            var currentColor = SelectedColor;
            var fillBrush = new SolidColorBrush(currentColor);
            fillBrush.Freeze();

            dc.DrawEllipse(fillBrush, outerPen, thumbPos, thumbRadius, thumbRadius);

            // Subtle dark border to ensure visibility on bright colors
            var contrastPen = new Pen(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), 0.8);
            contrastPen.Freeze();
            dc.DrawEllipse(null, contrastPen, thumbPos, thumbRadius + 1.0, thumbRadius + 1.0);
        }

        private void DrawBadge(DrawingContext dc, Point center, double radius)
        {
            string text = string.Format(CultureInfo.InvariantCulture, "{0:0}°  {1:0}%", Hue, Saturation * 100f);

            var typeface = ZeroWpfTheme.RegularTypeface;
            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                9.5,
                ZeroWpfTheme.TextPrimary,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double padX = 5.0;
            double padY = 2.0;
            double w = ft.Width + padX * 2;
            double h = ft.Height + padY * 2;

            double bx = center.X - w / 2.0;
            double by = center.Y + radius - h - 2.0;
            var badgeRect = new Rect(bx, by, w, h);

            var bg = new SolidColorBrush(Color.FromArgb(210, 15, 15, 20));
            bg.Freeze();
            var border = new Pen(ZeroWpfTheme.BorderSubtle, 0.8);
            border.Freeze();

            dc.DrawRoundedRectangle(bg, border, badgeRect, 3.0, 3.0);
            dc.DrawText(ft, new Point(bx + padX, by + padY));
        }

        #endregion

        #region User Interaction

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            InvalidateVisual();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();

            if (ReadOnly) return;

            if (e.ClickCount == 2)
            {
                // Double click anywhere -> Snap to neutral (0,0)
                SetValue(0f, 0f);
                _isModified = true;
                RaiseChanged();
                return;
            }

            _dragging = true;
            CaptureMouse();
            UpdateFromPoint(e.GetPosition(this));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging && !ReadOnly)
            {
                UpdateFromPoint(e.GetPosition(this));
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_dragging)
            {
                _dragging = false;
                ReleaseMouseCapture();
                InvalidateVisual();
            }
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);
            if (ReadOnly) return;

            // Right click -> Instant reset to neutral
            SetValue(0f, 0f);
            _isModified = true;
            RaiseChanged();
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (ReadOnly) return;

            float hueStep = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 1f : 5f;
            float satStep = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 0.01f : 0.05f;
            bool modified = false;

            if (e.Key == Key.Left)
            {
                Hue = ((Hue + hueStep) % 360f + 360f) % 360f;
                modified = true;
            }
            else if (e.Key == Key.Right)
            {
                Hue = ((Hue - hueStep) % 360f + 360f) % 360f;
                modified = true;
            }
            else if (e.Key == Key.Up)
            {
                Saturation = Clamp(Saturation + satStep, 0f, 1f);
                modified = true;
            }
            else if (e.Key == Key.Down)
            {
                Saturation = Clamp(Saturation - satStep, 0f, 1f);
                modified = true;
            }
            else if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                SetValue(0f, 0f);
                modified = true;
            }

            if (modified)
            {
                _isModified = true;
                InvalidateVisual();
                RaiseChanged();
                e.Handled = true;
            }
        }

        private void UpdateFromPoint(Point p)
        {
            double width = ActualWidth;
            double height = ActualHeight;
            Point center = new Point(width / 2.0, height / 2.0);
            double radius = Math.Min(width, height) / 2.0 - (ThumbSize / 2.0 + 2.0);
            if (radius <= 0.0) return;

            double dx = p.X - center.X;
            double dy = center.Y - p.Y; // Cartesian y
            double ang = Math.Atan2(dy, dx);
            if (ang < 0.0) ang += 2.0 * Math.PI;

            double dist = Math.Sqrt(dx * dx + dy * dy);
            float h = (float)(ang * 180.0 / Math.PI);
            float s = (float)Clamp(dist / radius, 0.0, 1.0);

            _isUpdatingFromDp = true;
            Hue = h;
            Saturation = s;
            _isUpdatingFromDp = false;

            _isModified = true;
            InvalidateVisual();
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            if (_suppressEvents) return;
            ColorChanged?.Invoke(this, (Hue, Saturation));
            SelectedColorChanged?.Invoke(this, SelectedColor);
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Helpers

        public static (byte r, byte g, byte b) HsvToRgb(float h, float s, float v)
        {
            float c = v * s;
            float x = c * (1f - Math.Abs((h / 60f) % 2f - 1f));
            float m = v - c;
            float r1, g1, b1;

            if (h < 60f) { r1 = c; g1 = x; b1 = 0f; }
            else if (h < 120f) { r1 = x; g1 = c; b1 = 0f; }
            else if (h < 180f) { r1 = 0f; g1 = c; b1 = x; }
            else if (h < 240f) { r1 = 0f; g1 = x; b1 = c; }
            else if (h < 300f) { r1 = x; g1 = 0f; b1 = c; }
            else { r1 = c; g1 = 0f; b1 = x; }

            byte r = (byte)Clamp((r1 + m) * 255f, 0f, 255f);
            byte g = (byte)Clamp((g1 + m) * 255f, 0f, 255f);
            byte b = (byte)Clamp((b1 + m) * 255f, 0f, 255f);
            return (r, g, b);
        }

        public static Color HsvToColor(float h, float s, float v)
        {
            var (r, g, b) = HsvToRgb(h, s, v);
            return Color.FromRgb(r, g, b);
        }

        private static float Clamp(float val, float min, float max)
        {
            if (val < min) return min;
            if (val > max) return max;
            return val;
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
    /// Legacy alias for <see cref="ZColorWheel"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ColorWheelEdit is deprecated. Use ZColorWheel instead.")]
    public class ColorWheelEdit : ZColorWheel
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZColorWheel"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ColorWheel is deprecated. Use ZColorWheel instead.")]
    public class ColorWheel : ZColorWheel
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZColorWheel"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroColorWheel is deprecated. Use ZColorWheel instead.")]
    public class ZeroColorWheel : ZColorWheel
    {
    }
    #endregion
}
