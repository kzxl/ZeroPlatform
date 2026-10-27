using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Modern segmented duration and cycle-time editor for ZeroUI in WPF.
    /// Supports segmented keyboard navigation, up/down arrows/spinners, mouse wheel adjustments,
    /// and dark theme enterprise styling.
    /// </summary>
    public class ZTimeSpanEdit : Control, IZeroEditor
    {
        private readonly TimeSpanModel _model = new TimeSpanModel();
        private bool _isHovered;
        private bool _isFocused;
        private bool _isInternalValueSync;
        private Rect _upButtonRect;
        private Rect _downButtonRect;
        private bool _isUpHovered;
        private bool _isDownHovered;

        #region Dependency Properties

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(TimeSpan), typeof(ZTimeSpanEdit),
                new FrameworkPropertyMetadata(TimeSpan.Zero, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnValueChanged));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(TimeSpan), typeof(ZTimeSpanEdit),
                new PropertyMetadata(TimeSpan.Zero, OnMinimumChanged));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(TimeSpan), typeof(ZTimeSpanEdit),
                new PropertyMetadata(TimeSpan.FromDays(9999), OnMaximumChanged));

        public static readonly DependencyProperty FormatModeProperty =
            DependencyProperty.Register(nameof(FormatMode), typeof(TimeSpanFormatMode), typeof(ZTimeSpanEdit),
                new PropertyMetadata(TimeSpanFormatMode.Clock, OnFormatModeChanged));

        public static readonly DependencyProperty ShowDaysProperty =
            DependencyProperty.Register(nameof(ShowDays), typeof(bool), typeof(ZTimeSpanEdit),
                new PropertyMetadata(true, OnShowDaysChanged));

        public static readonly DependencyProperty ShowMillisecondsProperty =
            DependencyProperty.Register(nameof(ShowMilliseconds), typeof(bool), typeof(ZTimeSpanEdit),
                new PropertyMetadata(false, OnShowMillisecondsChanged));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(ZTimeSpanEdit),
                new PropertyMetadata(new CornerRadius(5)));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(ZTimeSpanEdit),
                new PropertyMetadata(false));

        public static readonly DependencyProperty IsModifiedProperty =
            DependencyProperty.Register(nameof(IsModified), typeof(bool), typeof(ZTimeSpanEdit),
                new PropertyMetadata(false));

        public static readonly DependencyProperty EditValueProperty =
            DependencyProperty.Register(nameof(EditValue), typeof(object), typeof(ZTimeSpanEdit),
                new FrameworkPropertyMetadata(TimeSpan.Zero, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnEditValueChanged));

        #endregion

        #region Properties & Events

        public TimeSpan Value
        {
            get => (TimeSpan)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public TimeSpan Minimum
        {
            get => (TimeSpan)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public TimeSpan Maximum
        {
            get => (TimeSpan)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public TimeSpanFormatMode FormatMode
        {
            get => (TimeSpanFormatMode)GetValue(FormatModeProperty);
            set => SetValue(FormatModeProperty, value);
        }

        public bool ShowDays
        {
            get => (bool)GetValue(ShowDaysProperty);
            set => SetValue(ShowDaysProperty, value);
        }

        public bool ShowMilliseconds
        {
            get => (bool)GetValue(ShowMillisecondsProperty);
            set => SetValue(ShowMillisecondsProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public bool IsModified
        {
            get => (bool)GetValue(IsModifiedProperty);
            set => SetValue(IsModifiedProperty, value);
        }

        public object? EditValue
        {
            get => GetValue(EditValueProperty);
            set => SetValue(EditValueProperty, value);
        }

        public TimeSpanModel Model => _model;

        public event EventHandler<TimeSpan>? ValueChanged;
        public event EventHandler? EditValueChanged;

        #endregion

        static ZTimeSpanEdit()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZTimeSpanEdit),
                new FrameworkPropertyMetadata(typeof(ZTimeSpanEdit)));
        }

        public ZTimeSpanEdit()
        {
            Focusable = true;
            Cursor = Cursors.Arrow;
            Height = 32;
            Width = 160;

            _model.ValueChanged += (s, e) =>
            {
                if (!_isInternalValueSync)
                {
                    _isInternalValueSync = true;
                    Value = _model.Value;
                    EditValue = _model.Value;
                    IsModified = true;
                    _isInternalValueSync = false;
                    ValueChanged?.Invoke(this, _model.Value);
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
                }
                InvalidateVisual();
            };

            _model.PartChanged += (s, e) => InvalidateVisual();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZTimeSpanEdit edit && !edit._isInternalValueSync)
            {
                var ts = (TimeSpan)e.NewValue;
                edit._isInternalValueSync = true;
                edit._model.Value = ts;
                edit.EditValue = ts;
                edit._isInternalValueSync = false;
                edit.ValueChanged?.Invoke(edit, ts);
                edit.EditValueChanged?.Invoke(edit, EventArgs.Empty);
                edit.InvalidateVisual();
            }
        }

        private static void OnEditValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZTimeSpanEdit edit && !edit._isInternalValueSync)
            {
                if (e.NewValue is TimeSpan ts)
                {
                    edit.Value = ts;
                }
                else if (e.NewValue is string s && TimeSpanModel.TryParse(s, out var parsed))
                {
                    edit.Value = parsed;
                }
            }
        }

        private static void OnMinimumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZTimeSpanEdit edit) edit._model.Minimum = (TimeSpan)e.NewValue;
        }

        private static void OnMaximumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZTimeSpanEdit edit) edit._model.Maximum = (TimeSpan)e.NewValue;
        }

        private static void OnFormatModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZTimeSpanEdit edit) edit.InvalidateVisual();
        }

        private static void OnShowDaysChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZTimeSpanEdit edit)
            {
                edit._model.ShowDays = (bool)e.NewValue;
                edit.InvalidateVisual();
            }
        }

        private static void OnShowMillisecondsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZTimeSpanEdit edit)
            {
                edit._model.ShowMilliseconds = (bool)e.NewValue;
                edit.InvalidateVisual();
            }
        }

        public void Reset()
        {
            Value = TimeSpan.Zero;
            IsModified = false;
        }

        public void Clear() => Reset();

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (ReadOnly) return;

            if (e.Key == Key.Up)
            {
                _model.StepUp();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                _model.StepDown();
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                _model.NextPart();
                e.Handled = true;
            }
            else if (e.Key == Key.Left)
            {
                _model.PreviousPart();
                e.Handled = true;
            }
            else if (e.Key == Key.Tab && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                // Internal segment cycling
                _model.NextPart();
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (ReadOnly) return;

            if (e.Delta > 0)
                _model.StepUp();
            else if (e.Delta < 0)
                _model.StepDown();

            e.Handled = true;
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
            _isUpHovered = false;
            _isDownHovered = false;
            InvalidateVisual();
        }

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

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var pt = e.GetPosition(this);
            bool upHover = _upButtonRect.Contains(pt);
            bool downHover = _downButtonRect.Contains(pt);

            if (upHover != _isUpHovered || downHover != _isDownHovered)
            {
                _isUpHovered = upHover;
                _isDownHovered = downHover;
                InvalidateVisual();
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (ReadOnly) return;

            var pt = e.GetPosition(this);
            if (_upButtonRect.Contains(pt))
            {
                _model.StepUp();
                e.Handled = true;
            }
            else if (_downButtonRect.Contains(pt))
            {
                _model.StepDown();
                e.Handled = true;
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            var bounds = new Rect(0, 0, w, h);
            var radius = CornerRadius.TopLeft;

            // Background & Border
            Brush bg = ReadOnly ? ZeroWpfTheme.BgInput : ZeroWpfTheme.BgInput;
            Brush borderBrush = _isFocused ? ZeroWpfTheme.PrimaryAccent : (_isHovered ? ZeroWpfTheme.PrimaryAccentHover : ZeroWpfTheme.BorderDefault);
            var pen = new Pen(borderBrush, 1.0);

            dc.DrawRoundedRectangle(bg, pen, bounds, radius, radius);

            // Up / Down spin buttons on right
            double btnWidth = 20;
            double btnH = (h - 2) / 2.0;
            _upButtonRect = new Rect(w - btnWidth - 1, 1, btnWidth, btnH);
            _downButtonRect = new Rect(w - btnWidth - 1, 1 + btnH, btnWidth, btnH);

            // Button divider line
            dc.DrawLine(new Pen(ZeroWpfTheme.BorderDefault, 1), new Point(w - btnWidth - 1, 2), new Point(w - btnWidth - 1, h - 2));

            // Up button fill
            if (_isUpHovered)
                dc.DrawRoundedRectangle(ZeroWpfTheme.BgHover, null, _upButtonRect, 0, 0);
            if (_isDownHovered)
                dc.DrawRoundedRectangle(ZeroWpfTheme.BgHover, null, _downButtonRect, 0, 0);

            // Up arrow glyph
            var arrowPen = new Pen(ZeroWpfTheme.TextSecondary, 1.2);
            double upMidX = _upButtonRect.X + _upButtonRect.Width / 2.0;
            double upMidY = _upButtonRect.Y + _upButtonRect.Height / 2.0;
            dc.DrawLine(arrowPen, new Point(upMidX - 3.5, upMidY + 1.5), new Point(upMidX, upMidY - 2));
            dc.DrawLine(arrowPen, new Point(upMidX, upMidY - 2), new Point(upMidX + 3.5, upMidY + 1.5));

            // Down arrow glyph
            double downMidX = _downButtonRect.X + _downButtonRect.Width / 2.0;
            double downMidY = _downButtonRect.Y + _downButtonRect.Height / 2.0;
            dc.DrawLine(arrowPen, new Point(downMidX - 3.5, downMidY - 1.5), new Point(downMidX, downMidY + 2));
            dc.DrawLine(arrowPen, new Point(downMidX, downMidY + 2), new Point(downMidX + 3.5, downMidY - 1.5));

            // Formatted Text Rendering
            string text = _model.GetFormattedText();
            var ft = new FormattedText(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                ZeroWpfTheme.RegularTypeface,
                FontSize > 0 ? FontSize : 13.0,
                ZeroWpfTheme.TextPrimary,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double textX = 10;
            double textY = (h - ft.Height) / 2.0;
            dc.DrawText(ft, new Point(textX, textY));

            // Active segment underline indicator when focused
            if (_isFocused)
            {
                var focusPen = new Pen(ZeroWpfTheme.PrimaryAccent, 2.0);
                dc.DrawLine(focusPen, new Point(textX, h - 3), new Point(textX + Math.Min(ft.Width, w - btnWidth - 14), h - 3));
            }
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
    /// Legacy alias for <see cref="ZTimeSpanEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("TimeSpanEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZTimeSpanEdit instead.")]
    public class TimeSpanEdit : ZTimeSpanEdit { }

    /// <summary>
    /// Legacy alias for <see cref="ZTimeSpanEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroTimeSpanEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZTimeSpanEdit instead.")]
    public class ZeroTimeSpanEdit : ZTimeSpanEdit { }

    #endregion

}
