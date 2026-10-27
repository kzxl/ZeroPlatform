using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Input;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Modern anti-aliased slider / trackbar control for ZeroUI in WPF.
    /// Powered by the headless <see cref="RangeModel"/> and <see cref="RangeMath"/> engine.
    /// Supports live value tooltip badge, keyboard stepping, and theme synchronization.
    /// </summary>
    public class ZTrackBar : FrameworkElement
    {
        private readonly RangeModel _rangeModel = new RangeModel(0f, 100f, 0f, 1f);
        private bool _isDragging = false;
        private bool _isHovered = false;
        private bool _isThumbHovered = false;
        private bool _isUpdatingFromDp = false;

        #region Dependency Properties

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(float), typeof(ZTrackBar),
                new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender, OnMinimumChanged));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(float), typeof(ZTrackBar),
                new FrameworkPropertyMetadata(100f, FrameworkPropertyMetadataOptions.AffectsRender, OnMaximumChanged));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(float), typeof(ZTrackBar),
                new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnValueChanged));

        public static readonly DependencyProperty StepProperty =
            DependencyProperty.Register(nameof(Step), typeof(float), typeof(ZTrackBar),
                new FrameworkPropertyMetadata(1f, OnStepChanged));

        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(nameof(Unit), typeof(string), typeof(ZTrackBar),
                new FrameworkPropertyMetadata("%", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowValueBadgeProperty =
            DependencyProperty.Register(nameof(ShowValueBadge), typeof(bool), typeof(ZTrackBar),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(ZTrackBar),
                new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TrackThicknessProperty =
            DependencyProperty.Register(nameof(TrackThickness), typeof(double), typeof(ZTrackBar),
                new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ThumbSizeProperty =
            DependencyProperty.Register(nameof(ThumbSize), typeof(double), typeof(ZTrackBar),
                new FrameworkPropertyMetadata(18.0, FrameworkPropertyMetadataOptions.AffectsRender));

        #endregion

        #region Properties & Events

        public float Minimum
        {
            get => (float)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public float Maximum
        {
            get => (float)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public float Value
        {
            get => (float)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public float Step
        {
            get => (float)GetValue(StepProperty);
            set => SetValue(StepProperty, value);
        }

        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }

        public bool ShowValueBadge
        {
            get => (bool)GetValue(ShowValueBadgeProperty);
            set => SetValue(ShowValueBadgeProperty, value);
        }

        public Orientation Orientation
        {
            get => (Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public double TrackThickness
        {
            get => (double)GetValue(TrackThicknessProperty);
            set => SetValue(TrackThicknessProperty, value);
        }

        public double ThumbSize
        {
            get => (double)GetValue(ThumbSizeProperty);
            set => SetValue(ThumbSizeProperty, value);
        }

        public RangeModel Range => _rangeModel;

        public event EventHandler<float>? ValueChanged;

        #endregion

        public ZTrackBar()
        {
            Cursor = Cursors.Hand;
            Focusable = true;

            // Default dimensions
            Width = 200;
            Height = 36;

            _rangeModel.ValueChanged += (s, e) =>
            {
                if (!_isUpdatingFromDp)
                {
                    Value = _rangeModel.Value;
                }
                InvalidateVisual();
                ValueChanged?.Invoke(this, _rangeModel.Value);
            };

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        #region DP Callbacks

        private static void OnMinimumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZTrackBar slider) slider._rangeModel.Minimum = (float)e.NewValue;
        }

        private static void OnMaximumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZTrackBar slider) slider._rangeModel.Maximum = (float)e.NewValue;
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZTrackBar slider)
            {
                slider._isUpdatingFromDp = true;
                slider._rangeModel.Value = (float)e.NewValue;
                slider._isUpdatingFromDp = false;
            }
        }

        private static void OnStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZTrackBar slider) slider._rangeModel.Step = (float)e.NewValue;
        }

        #endregion

        #region Mouse & Keyboard Interaction

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
            _isThumbHovered = false;
            InvalidateVisual();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Focus();
                CaptureMouse();
                _isDragging = true;
                UpdateValueFromPoint(e.GetPosition(this));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var pt = e.GetPosition(this);
            if (_isDragging)
            {
                UpdateValueFromPoint(pt);
            }
            else
            {
                var thumbCenter = GetThumbCenter();
                double dist = Math.Sqrt(Math.Pow(pt.X - thumbCenter.X, 2) + Math.Pow(pt.Y - thumbCenter.Y, 2));
                bool overThumb = dist <= ThumbSize / 2.0 + 2;
                if (overThumb != _isThumbHovered)
                {
                    _isThumbHovered = overThumb;
                    InvalidateVisual();
                }
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                InvalidateVisual();
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e.Delta > 0)
                _rangeModel.Increment();
            else if (e.Delta < 0)
                _rangeModel.Decrement();
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Left || e.Key == Key.Down)
            {
                _rangeModel.Decrement();
                e.Handled = true;
            }
            else if (e.Key == Key.Right || e.Key == Key.Up)
            {
                _rangeModel.Increment();
                e.Handled = true;
            }
            else if (e.Key == Key.Home)
            {
                Value = Minimum;
                e.Handled = true;
            }
            else if (e.Key == Key.End)
            {
                Value = Maximum;
                e.Handled = true;
            }
        }

        private void UpdateValueFromPoint(Point pt)
        {
            double w = ActualWidth;
            double h = ActualHeight;
            double thumbRadius = ThumbSize / 2.0;

            if (Orientation == Orientation.Horizontal)
            {
                double trackStart = thumbRadius + 2;
                double trackEnd = w - thumbRadius - 2;
                double trackLen = Math.Max(1.0, trackEnd - trackStart);
                float normalized = (float)Math.Max(0.0, Math.Min(1.0, (pt.X - trackStart) / trackLen));
                _rangeModel.Fraction = normalized;
            }
            else
            {
                double trackStart = h - thumbRadius - 2;
                double trackEnd = thumbRadius + 2;
                double trackLen = Math.Max(1.0, trackStart - trackEnd);
                float normalized = (float)Math.Max(0.0, Math.Min(1.0, (trackStart - pt.Y) / trackLen));
                _rangeModel.Fraction = normalized;
            }
        }

        private Point GetThumbCenter()
        {
            double w = ActualWidth;
            double h = ActualHeight;
            double thumbRadius = ThumbSize / 2.0;
            float norm = _rangeModel.Fraction;

            if (Orientation == Orientation.Horizontal)
            {
                double trackStart = thumbRadius + 2;
                double trackEnd = w - thumbRadius - 2;
                double cx = trackStart + norm * (trackEnd - trackStart);
                double cy = h / 2.0;
                return new Point(cx, cy);
            }
            else
            {
                double trackStart = h - thumbRadius - 2;
                double trackEnd = thumbRadius + 2;
                double cx = w / 2.0;
                double cy = trackStart - norm * (trackStart - trackEnd);
                return new Point(cx, cy);
            }
        }

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            bool isDark = ZeroWpfTheme.IsDark;
            double thumbRadius = ThumbSize / 2.0;
            float norm = _rangeModel.Fraction;
            var thumbPt = GetThumbCenter();

            Brush trackBgBrush = ZeroWpfTheme.BgInput;
            Pen trackBorderPen = _isHovered ? ZeroWpfTheme.AccentPen : ZeroWpfTheme.BorderPen;
            Brush activeTrackBrush = ZeroWpfTheme.PrimaryAccent;

            if (Orientation == Orientation.Horizontal)
            {
                double trackY = h / 2.0 - TrackThickness / 2.0;
                double trackLeft = thumbRadius + 2;
                double trackRight = w - thumbRadius - 2;
                double trackW = Math.Max(1.0, trackRight - trackLeft);

                // Inactive Track
                dc.DrawRoundedRectangle(trackBgBrush, trackBorderPen,
                    new Rect(trackLeft, trackY, trackW, TrackThickness),
                    TrackThickness / 2.0, TrackThickness / 2.0);

                // Active Track
                double activeW = norm * trackW;
                if (activeW > 0)
                {
                    dc.DrawRoundedRectangle(activeTrackBrush, null,
                        new Rect(trackLeft, trackY, activeW, TrackThickness),
                        TrackThickness / 2.0, TrackThickness / 2.0);
                }
            }
            else
            {
                double trackX = w / 2.0 - TrackThickness / 2.0;
                double trackTop = thumbRadius + 2;
                double trackBottom = h - thumbRadius - 2;
                double trackH = Math.Max(1.0, trackBottom - trackTop);

                // Inactive Track
                dc.DrawRoundedRectangle(trackBgBrush, trackBorderPen,
                    new Rect(trackX, trackTop, TrackThickness, trackH),
                    TrackThickness / 2.0, TrackThickness / 2.0);

                // Active Track
                double activeH = norm * trackH;
                if (activeH > 0)
                {
                    dc.DrawRoundedRectangle(activeTrackBrush, null,
                        new Rect(trackX, trackBottom - activeH, TrackThickness, activeH),
                        TrackThickness / 2.0, TrackThickness / 2.0);
                }
            }

            // Outer Glow if Hovered or Dragging
            if (_isDragging || _isThumbHovered)
            {
                Brush glowBrush = new SolidColorBrush(Color.FromArgb(50, 59, 130, 246));
                glowBrush.Freeze();
                dc.DrawEllipse(glowBrush, null, thumbPt, thumbRadius + 5, thumbRadius + 5);
            }

            // Thumb Body
            Brush thumbBrush = _isDragging ? ZeroWpfTheme.PrimaryAccent : (isDark ? Brushes.White : Brushes.White);
            Pen thumbPen = new Pen(_isDragging ? ZeroWpfTheme.PrimaryAccentDark : ZeroWpfTheme.PrimaryAccent, 2.0);
            thumbPen.Freeze();

            dc.DrawEllipse(thumbBrush, thumbPen, thumbPt, thumbRadius, thumbRadius);

            // Optional Inner Dot
            Brush innerDotBrush = _isDragging ? Brushes.White : ZeroWpfTheme.PrimaryAccent;
            dc.DrawEllipse(innerDotBrush, null, thumbPt, 3.5, 3.5);

            // Value Badge
            if (ShowValueBadge && (_isDragging || _isThumbHovered || IsFocused))
            {
                string text = $"{Value:0.##} {Unit}".Trim();
                var ft = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.BoldTypeface,
                    9.5,
                    Brushes.White,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                double badgeW = ft.Width + 12;
                double badgeH = ft.Height + 6;
                double badgeX = thumbPt.X - badgeW / 2.0;
                double badgeY = thumbPt.Y - thumbRadius - badgeH - 4;

                badgeX = Math.Max(2, Math.Min(w - badgeW - 2, badgeX));
                if (badgeY < 2) badgeY = thumbPt.Y + thumbRadius + 4;

                Brush badgeBg = ZeroWpfTheme.PrimaryAccentDark;
                dc.DrawRoundedRectangle(badgeBg, null, new Rect(badgeX, badgeY, badgeW, badgeH), 4, 4);
                dc.DrawText(ft, new Point(badgeX + 6, badgeY + 3));
            }
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

    /// <summary>
    /// Backward-compatibility alias for <see cref="SliderControl"/>.
    /// </summary>
    [Obsolete("ZeroSlider is deprecated. Use SliderControl instead.")]
    public class ZeroSlider : SliderControl
    {
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="SliderControl"/>.
    /// </summary>
    [Obsolete("TrackBarControl is deprecated. Use SliderControl instead.")]
    public class TrackBarControl : SliderControl
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Convenience alias for <see cref="ZTrackBar"/>.
    /// </summary>
    public class ZSlider : ZTrackBar { }

    /// <summary>
    /// Convenience alias for <see cref="ZTrackBar"/>.
    /// </summary>
    public class ZSliderControl : ZTrackBar { }

    /// <summary>
    /// Legacy alias for <see cref="ZTrackBar"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SliderControl is deprecated and will be removed in 5 release cycles. Please migrate to ZTrackBar instead.")]
    public class SliderControl : ZTrackBar { }

    /// <summary>
    /// Legacy alias for <see cref="ZTrackBar"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroSliderControl is deprecated and will be removed in 5 release cycles. Please migrate to ZTrackBar instead.")]
    public class ZeroSliderControl : ZTrackBar { }

    #endregion

}
