using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Input.Time;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Modern segmented time picker control for ZeroUI in WPF.
    /// Powered by the headless <see cref="TimeSegmentModel"/> for segmented keyboard/wheel editing,
    /// seamless focus transitions, and zero-allocation updates.
    /// </summary>
    public class ZTimeEdit : FrameworkElement
    {
        private readonly TimeSegmentModel _model = new TimeSegmentModel();
        private bool _isHovered = false;
        private bool _isFocused = false;
        private bool _isUpdatingFromDp = false;

        private Rect _hourRect;
        private Rect _minuteRect;
        private Rect _secondRect;

        #region Dependency Properties

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(TimeSpan), typeof(ZTimeEdit),
                new FrameworkPropertyMetadata(TimeSpan.Zero, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnValueChanged));

        public static readonly DependencyProperty ShowSecondsProperty =
            DependencyProperty.Register(nameof(ShowSeconds), typeof(bool), typeof(ZTimeEdit),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnShowSecondsChanged));

        #endregion

        #region Properties & Events

        public TimeSpan Value
        {
            get => (TimeSpan)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public bool ShowSeconds
        {
            get => (bool)GetValue(ShowSecondsProperty);
            set => SetValue(ShowSecondsProperty, value);
        }

        public TimeSegmentModel Model => _model;

        public event EventHandler<TimeSpan>? ValueChanged;

        #endregion

        public ZTimeEdit()
        {
            Cursor = Cursors.IBeam;
            Focusable = true;

            Height = 32;
            Width = 120;

            _model.TimeChanged += (s, e) =>
            {
                if (!_isUpdatingFromDp)
                {
                    Value = _model.Time;
                }
                InvalidateVisual();
                ValueChanged?.Invoke(this, _model.Time);
            };

            _model.SegmentChanged += (s, e) => InvalidateVisual();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZTimeEdit picker)
            {
                var ts = (TimeSpan)e.NewValue;
                picker._isUpdatingFromDp = true;
                picker._model.Time = ts;
                picker._isUpdatingFromDp = false;
            }
        }

        private static void OnShowSecondsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZTimeEdit picker)
            {
                bool show = (bool)e.NewValue;
                picker.Width = show ? 150 : 120;
                picker.InvalidateMeasure();
                picker.InvalidateVisual();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double w = ShowSeconds ? 150 : 120;
            return new Size(w, 32);
        }

        #region Focus & Hover

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            _isFocused = true;
            InvalidateVisual();
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);
            _isFocused = false;
            InvalidateVisual();
        }

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

        #endregion

        #region Mouse & Keyboard Interaction

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Focus();
                var pt = e.GetPosition(this);

                if (_hourRect.Contains(pt))
                {
                    _model.FocusedSegment = TimeSegment.Hour;
                }
                else if (_minuteRect.Contains(pt))
                {
                    _model.FocusedSegment = TimeSegment.Minute;
                }
                else if (ShowSeconds && _secondRect.Contains(pt))
                {
                    _model.FocusedSegment = TimeSegment.Second;
                }
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e.Delta > 0)
                _model.AdjustCurrentSegment(1);
            else if (e.Delta < 0)
                _model.AdjustCurrentSegment(-1);
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            switch (e.Key)
            {
                case Key.Left:
                    _model.MovePreviousSegment();
                    e.Handled = true;
                    break;

                case Key.Right:
                    _model.MoveNextSegment();
                    e.Handled = true;
                    break;

                case Key.Up:
                    _model.AdjustCurrentSegment(1);
                    e.Handled = true;
                    break;

                case Key.Down:
                    _model.AdjustCurrentSegment(-1);
                    e.Handled = true;
                    break;

                case Key.Tab:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        if (_model.FocusedSegment > TimeSegment.Hour)
                        {
                            _model.MovePreviousSegment();
                            e.Handled = true;
                        }
                    }
                    else
                    {
                        var maxSeg = ShowSeconds ? TimeSegment.Second : TimeSegment.Minute;
                        if (_model.FocusedSegment < maxSeg)
                        {
                            _model.MoveNextSegment();
                            e.Handled = true;
                        }
                    }
                    break;

                default:
                    // Check numeric keys
                    int digit = -1;
                    if (e.Key >= Key.D0 && e.Key <= Key.D9)
                        digit = e.Key - Key.D0;
                    else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
                        digit = e.Key - Key.NumPad0;

                    if (digit >= 0)
                    {
                        _model.TryApplyDigit(digit);
                        e.Handled = true;
                    }
                    break;
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

            // 1. Box Background & Border
            Brush bgBrush = ZeroWpfTheme.BgInput;
            Pen borderPen = _isFocused ? new Pen(ZeroWpfTheme.BorderFocus, 1.5) :
                            _isHovered ? new Pen(ZeroWpfTheme.BorderFocus, 1.0) : ZeroWpfTheme.BorderPen;

            dc.DrawRoundedRectangle(bgBrush, borderPen, new Rect(0.5, 0.5, w - 1, h - 1), 5, 5);

            // 2. Segment Layout Calculation
            double segW = 28;
            double colonW = 10;
            double startX = 14;
            double textY = h / 2.0 - 9;

            _hourRect = new Rect(startX, 4, segW, h - 8);
            double colon1X = startX + segW;
            _minuteRect = new Rect(colon1X + colonW, 4, segW, h - 8);

            if (ShowSeconds)
            {
                double colon2X = _minuteRect.Right;
                _secondRect = new Rect(colon2X + colonW, 4, segW, h - 8);
            }
            else
            {
                _secondRect = Rect.Empty;
            }

            // 3. Draw Active Segment Highlight Box
            if (_isFocused)
            {
                Rect activeRect = _model.FocusedSegment == TimeSegment.Hour ? _hourRect :
                                  _model.FocusedSegment == TimeSegment.Minute ? _minuteRect :
                                  (ShowSeconds && _model.FocusedSegment == TimeSegment.Second ? _secondRect : Rect.Empty);

                if (!activeRect.IsEmpty)
                {
                    Brush activeHighlight = new SolidColorBrush(Color.FromArgb(40, 59, 130, 246));
                    activeHighlight.Freeze();
                    dc.DrawRoundedRectangle(activeHighlight, null, activeRect, 3, 3);
                }
            }

            // 4. Draw Segments Text
            DrawSegmentText(dc, $"{_model.DisplayHour:D2}", _hourRect, _model.FocusedSegment == TimeSegment.Hour && _isFocused);
            DrawColon(dc, colon1X, textY);
            DrawSegmentText(dc, $"{_model.DisplayMinute:D2}", _minuteRect, _model.FocusedSegment == TimeSegment.Minute && _isFocused);

            if (ShowSeconds)
            {
                DrawColon(dc, _minuteRect.Right, textY);
                DrawSegmentText(dc, $"{_model.DisplaySecond:D2}", _secondRect, _model.FocusedSegment == TimeSegment.Second && _isFocused);
            }

            // 5. Clock Icon at Right
            double iconX = w - 22;
            double iconY = h / 2.0;
            Pen iconPen = new Pen(ZeroWpfTheme.TextMuted, 1.2);
            iconPen.Freeze();
            dc.DrawEllipse(null, iconPen, new Point(iconX, iconY), 7, 7);
            dc.DrawLine(iconPen, new Point(iconX, iconY), new Point(iconX, iconY - 4));
            dc.DrawLine(iconPen, new Point(iconX, iconY), new Point(iconX + 3, iconY));
        }

        private void DrawSegmentText(DrawingContext dc, string text, Rect bounds, bool isActive)
        {
            Brush brush = isActive ? ZeroWpfTheme.PrimaryAccent : ZeroWpfTheme.TextPrimary;
            var ft = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                isActive ? ZeroWpfTheme.BoldTypeface : ZeroWpfTheme.RegularTypeface,
                13.0,
                brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double x = bounds.X + (bounds.Width - ft.Width) / 2.0;
            double y = bounds.Y + (bounds.Height - ft.Height) / 2.0;
            dc.DrawText(ft, new Point(x, y));
        }

        private void DrawColon(DrawingContext dc, double x, double y)
        {
            var ft = new FormattedText(
                ":",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                ZeroWpfTheme.BoldTypeface,
                13.0,
                ZeroWpfTheme.TextMuted,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(ft, new Point(x + 3, y - 1));
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
    /// Backward-compatibility alias for <see cref="TimeEdit"/>.
    /// </summary>
    [Obsolete("ZeroTimePicker is deprecated. Use TimeEdit instead.")]
    public class ZeroTimePicker : TimeEdit
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZTimeEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("TimeEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZTimeEdit instead.")]
    public class TimeEdit : ZTimeEdit { }

    /// <summary>
    /// Legacy alias for <see cref="ZTimeEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroTimeEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZTimeEdit instead.")]
    public class ZeroTimeEdit : ZTimeEdit { }

    #endregion

}
