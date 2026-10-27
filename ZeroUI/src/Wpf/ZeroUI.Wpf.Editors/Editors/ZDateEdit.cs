using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Input.Date;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Modern anti-aliased single date picker control for ZeroUI in WPF.
    /// Backed by headless CalendarModel from ZeroUI.Core with 42-cell matrix generation,
    /// quick presets (Today, Yesterday, Next Week), month/year navigation, and obsidian/light theming.
    /// </summary>
    public class ZDateEdit : Control, IZeroEditor
    {
        private readonly CalendarModel _calendarModel;
        private readonly TextBox _dateBox;
        private readonly Border _containerBorder;
        private readonly Button _calendarTriggerButton;
        private readonly Popup _calendarPopup;
        private readonly CalendarPopupContent _popupContent;

        #region Dependency Properties

        public static readonly DependencyProperty EditValueProperty =
            DependencyProperty.Register(nameof(EditValue), typeof(object), typeof(ZDateEdit),
                new FrameworkPropertyMetadata(DateTime.Today, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnEditValueChanged));

        public static readonly DependencyProperty SelectedDateProperty =
            DependencyProperty.Register(nameof(SelectedDate), typeof(DateTime), typeof(ZDateEdit),
                new FrameworkPropertyMetadata(DateTime.Today, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedDateChanged));

        public static readonly DependencyProperty DateFormatProperty =
            DependencyProperty.Register(nameof(DateFormat), typeof(string), typeof(ZDateEdit),
                new FrameworkPropertyMetadata("yyyy-MM-dd", OnDateFormatChanged));

        public static readonly DependencyProperty ShowPresetsProperty =
            DependencyProperty.Register(nameof(ShowPresets), typeof(bool), typeof(ZDateEdit),
                new FrameworkPropertyMetadata(true));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(ZDateEdit),
                new FrameworkPropertyMetadata(new CornerRadius(6)));

        public static readonly DependencyProperty IsModifiedProperty =
            DependencyProperty.Register(nameof(IsModified), typeof(bool), typeof(ZDateEdit),
                new PropertyMetadata(false));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(ZDateEdit),
                new PropertyMetadata(false, OnReadOnlyChanged));

        #endregion

        #region Properties & Events

        public event EventHandler? EditValueChanged;
        public event EventHandler<DateTime>? SelectedDateChanged;

        public object? EditValue
        {
            get => GetValue(EditValueProperty);
            set => SetValue(EditValueProperty, value);
        }

        public bool IsModified
        {
            get => (bool)GetValue(IsModifiedProperty);
            set => SetValue(IsModifiedProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public DateTime SelectedDate
        {
            get => (DateTime)GetValue(SelectedDateProperty);
            set => SetValue(SelectedDateProperty, value);
        }

        public string DateFormat
        {
            get => (string)GetValue(DateFormatProperty);
            set => SetValue(DateFormatProperty, value);
        }

        public bool ShowPresets
        {
            get => (bool)GetValue(ShowPresetsProperty);
            set => SetValue(ShowPresetsProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public bool IsDropDownOpen => _calendarPopup?.IsOpen == true;

        public void Reset()
        {
            SelectedDate = DateTime.Today;
            EditValue = DateTime.Today;
            IsModified = false;
        }

        public void Clear() => Reset();

        private static void OnEditValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZDateEdit edit)
            {
                if (e.NewValue is DateTime dt)
                {
                    if (edit.SelectedDate != dt) edit.SelectedDate = dt;
                }
                else if (e.NewValue != null && DateTime.TryParse(e.NewValue.ToString(), out DateTime parsed))
                {
                    if (edit.SelectedDate != parsed) edit.SelectedDate = parsed;
                }
                edit.EditValueChanged?.Invoke(edit, EventArgs.Empty);
            }
        }

        private static void OnReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZDateEdit edit && edit._dateBox != null)
            {
                edit._dateBox.IsReadOnly = (bool)e.NewValue;
            }
        }

        #endregion

        public ZDateEdit()
        {
            Height = 32;
            Focusable = false;

            _calendarModel = new CalendarModel(DateTime.Today);
            _calendarModel.SelectedDateChanged += (s, e) =>
            {
                if (SelectedDate != _calendarModel.SelectedDate)
                {
                    SelectedDate = _calendarModel.SelectedDate;
                }
            };

            // 1. Container Border
            _containerBorder = new Border
            {
                CornerRadius = CornerRadius,
                BorderThickness = new Thickness(1),
                SnapsToDevicePixels = true
            };

            // 2. Grid Layout: [TextBox (Auto) | Calendar Icon Button (28px)]
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });

            _dateBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 0, 0, 0),
                Text = SelectedDate.ToString(DateFormat),
                SnapsToDevicePixels = true
            };

            _dateBox.LostFocus += (s, e) => TryCommitDateFromText();
            _dateBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    TryCommitDateFromText();
                    e.Handled = true;
                }
                else if (e.Key == Key.F4 || (e.Key == Key.Down && (Keyboard.Modifiers & ModifierKeys.Alt) != 0))
                {
                    TogglePopup();
                    e.Handled = true;
                }
            };

            _dateBox.GotFocus += (s, e) => UpdateThemeColors(isFocused: true);
            _dateBox.LostFocus += (s, e) => UpdateThemeColors(isFocused: false);

            Grid.SetColumn(_dateBox, 0);
            grid.Children.Add(_dateBox);

            _calendarTriggerButton = new Button
            {
                Content = "📅",
                FontSize = 12,
                Width = 24,
                Height = 24,
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _calendarTriggerButton.Click += (s, e) => TogglePopup();
            Grid.SetColumn(_calendarTriggerButton, 1);
            grid.Children.Add(_calendarTriggerButton);

            _containerBorder.Child = grid;
            AddVisualChild(_containerBorder);

            // 3. Popup & Content
            _popupContent = new CalendarPopupContent(_calendarModel, this);
            _calendarPopup = new Popup
            {
                PlacementTarget = _containerBorder,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                Child = _popupContent
            };
            _calendarPopup.Closed += (s, e) => _dateBox.Text = SelectedDate.ToString(DateFormat);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            UpdateThemeColors(isFocused: false);
        }

        private static void OnSelectedDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZDateEdit dp)
            {
                DateTime dt = (DateTime)e.NewValue;
                if (!Equals(dp.EditValue, dt))
                {
                    dp.EditValue = dt;
                }
                dp.IsModified = true;
                dp._calendarModel.SelectDate(dt);
                dp._dateBox.Text = dt.ToString(dp.DateFormat);
                dp.SelectedDateChanged?.Invoke(dp, dt);
                dp.EditValueChanged?.Invoke(dp, EventArgs.Empty);
            }
        }

        private static void OnDateFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZDateEdit dp)
            {
                dp._dateBox.Text = dp.SelectedDate.ToString((string)e.NewValue);
            }
        }

        private void TogglePopup()
        {
            if (ReadOnly || !IsEnabled) return;
            _calendarPopup.IsOpen = !_calendarPopup.IsOpen;
            if (_calendarPopup.IsOpen)
            {
                _popupContent.Refresh();
            }
        }

        public void ClosePopup()
        {
            _calendarPopup.IsOpen = false;
        }

        private void TryCommitDateFromText()
        {
            if (ReadOnly || !IsEnabled) return;
            if (DateTime.TryParseExact(_dateBox.Text, DateFormat, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime parsed))
            {
                SelectedDate = parsed;
            }
            else if (DateTime.TryParse(_dateBox.Text, out DateTime fallback))
            {
                SelectedDate = fallback;
            }
            else
            {
                _dateBox.Text = SelectedDate.ToString(DateFormat);
            }
        }

        private void UpdateThemeColors(bool isFocused)
        {
            _containerBorder.Background = ZeroWpfTheme.BgInput;
            _containerBorder.BorderBrush = isFocused ? ZeroWpfTheme.BorderFocus : ZeroWpfTheme.BorderDefault;
            _dateBox.Foreground = ZeroWpfTheme.TextPrimary;
            _dateBox.CaretBrush = ZeroWpfTheme.PrimaryAccent;
            _calendarTriggerButton.Foreground = ZeroWpfTheme.TextSecondary;
        }

        #region Visual Tree Overrides

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index)
        {
            if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
            return _containerBorder;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            _containerBorder.Measure(constraint);
            return _containerBorder.DesiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _containerBorder.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }

        #endregion

        #region Internal Popup Content

        private sealed class CalendarPopupContent : FrameworkElement
        {
            private readonly CalendarModel _model;
            private readonly ZDateEdit _owner;
            private readonly CalendarDayCell[] _cells = new CalendarDayCell[42];

            private int _hoveredPresetIndex = -1;
            private int _hoveredCellIndex = -1;
            private bool _hoveredPrevMonth = false;
            private bool _hoveredNextMonth = false;
            private bool _hoveredTodayLink = false;

            public CalendarPopupContent(CalendarModel model, ZDateEdit owner)
            {
                _model = model;
                _owner = owner;
                Width = 260;
                Height = 290;
                Cursor = Cursors.Hand;
            }

            public void Refresh()
            {
                _model.FillDaysGrid(_cells);
                InvalidateVisual();
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                Point pt = e.GetPosition(this);

                bool hovPrev = pt.X >= 10 && pt.X <= 32 && pt.Y >= 8 && pt.Y <= 28;
                bool hovNext = pt.X >= (ActualWidth - 34) && pt.X <= (ActualWidth - 10) && pt.Y >= 8 && pt.Y <= 28;
                bool hovToday = pt.Y >= (ActualHeight - 24) && pt.Y <= ActualHeight;

                int hovPreset = -1;
                if (pt.Y >= 34 && pt.Y <= 56)
                {
                    double pW = (ActualWidth - 16) / 4.0;
                    hovPreset = (int)((pt.X - 8) / pW);
                    if (hovPreset < 0 || hovPreset > 3) hovPreset = -1;
                }

                int hovCell = -1;
                double gridTop = 82;
                double cellW = (ActualWidth - 16) / 7.0;
                double cellH = 28;

                if (pt.Y >= gridTop && pt.Y < gridTop + (6 * cellH) && pt.X >= 8 && pt.X < ActualWidth - 8)
                {
                    int col = (int)((pt.X - 8) / cellW);
                    int row = (int)((pt.Y - gridTop) / cellH);
                    if (col >= 0 && col < 7 && row >= 0 && row < 6)
                    {
                        hovCell = (row * 7) + col;
                    }
                }

                if (_hoveredPrevMonth != hovPrev || _hoveredNextMonth != hovNext ||
                    _hoveredTodayLink != hovToday || _hoveredPresetIndex != hovPreset ||
                    _hoveredCellIndex != hovCell)
                {
                    _hoveredPrevMonth = hovPrev;
                    _hoveredNextMonth = hovNext;
                    _hoveredTodayLink = hovToday;
                    _hoveredPresetIndex = hovPreset;
                    _hoveredCellIndex = hovCell;
                    InvalidateVisual();
                }
            }

            protected override void OnMouseLeave(MouseEventArgs e)
            {
                base.OnMouseLeave(e);
                _hoveredCellIndex = -1;
                _hoveredPresetIndex = -1;
                _hoveredPrevMonth = false;
                _hoveredNextMonth = false;
                _hoveredTodayLink = false;
                InvalidateVisual();
            }

            protected override void OnMouseDown(MouseButtonEventArgs e)
            {
                base.OnMouseDown(e);
                Point pt = e.GetPosition(this);

                if (_hoveredPrevMonth)
                {
                    _model.NavigatePreviousMonth();
                    Refresh();
                    e.Handled = true;
                    return;
                }

                if (_hoveredNextMonth)
                {
                    _model.NavigateNextMonth();
                    Refresh();
                    e.Handled = true;
                    return;
                }

                if (_hoveredTodayLink)
                {
                    _owner.SelectedDate = DateTime.Today;
                    _owner.ClosePopup();
                    e.Handled = true;
                    return;
                }

                if (_hoveredPresetIndex >= 0)
                {
                    DateTime target = _hoveredPresetIndex switch
                    {
                        0 => DatePresetHelper.Calculate(DatePresetType.Today),
                        1 => DatePresetHelper.Calculate(DatePresetType.Yesterday),
                        2 => DatePresetHelper.Calculate(DatePresetType.NextWeek),
                        _ => DatePresetHelper.Calculate(DatePresetType.StartOfMonth)
                    };
                    _owner.SelectedDate = target;
                    _owner.ClosePopup();
                    e.Handled = true;
                    return;
                }

                if (_hoveredCellIndex >= 0 && _hoveredCellIndex < _cells.Length)
                {
                    var cell = _cells[_hoveredCellIndex];
                    if (!cell.IsDisabled)
                    {
                        _owner.SelectedDate = cell.Date;
                        _owner.ClosePopup();
                        e.Handled = true;
                    }
                }
            }

            protected override void OnRender(DrawingContext dc)
            {
                base.OnRender(dc);

                double w = ActualWidth;
                double h = ActualHeight;
                if (w <= 0 || h <= 0) return;

                _model.FillDaysGrid(_cells);
                double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

                // 1. Popup Background & Drop Shadow Border
                Rect bgRect = new Rect(0.5, 0.5, w - 1.0, h - 1.0);
                dc.DrawRoundedRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, bgRect, 8, 8);

                // 2. Navigation Header [< Month YYYY >]
                var headerFt = new FormattedText(
                    _model.ViewDate.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.BoldTypeface,
                    13.0,
                    ZeroWpfTheme.TextPrimary,
                    dpi);

                dc.DrawText(headerFt, new Point(Math.Round((w - headerFt.Width) / 2.0), 10));

                // Nav chevrons
                Brush prevBrush = _hoveredPrevMonth ? ZeroWpfTheme.PrimaryAccent : ZeroWpfTheme.TextSecondary;
                Brush nextBrush = _hoveredNextMonth ? ZeroWpfTheme.PrimaryAccent : ZeroWpfTheme.TextSecondary;

                var prevFt = new FormattedText("◀", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.RegularTypeface, 11.0, prevBrush, dpi);
                var nextFt = new FormattedText("▶", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.RegularTypeface, 11.0, nextBrush, dpi);
                dc.DrawText(prevFt, new Point(14, 11));
                dc.DrawText(nextFt, new Point(w - 24, 11));

                // 3. Quick Presets Bar (Today, Yest, +7D, Month)
                string[] presets = { "Today", "Yest", "+7 Days", "Month" };
                double pW = (w - 16) / 4.0;
                for (int i = 0; i < 4; i++)
                {
                    Rect pRect = new Rect(8 + (i * pW) + 2, 34, pW - 4, 20);
                    bool hov = (_hoveredPresetIndex == i);
                    Brush pBg = hov ? ZeroWpfTheme.BgHover : ZeroWpfTheme.BgInput;
                    dc.DrawRoundedRectangle(pBg, null, pRect, 3, 3);

                    var pFt = new FormattedText(
                        presets[i],
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        ZeroWpfTheme.MediumTypeface,
                        9.5,
                        hov ? ZeroWpfTheme.PrimaryAccent : ZeroWpfTheme.TextSecondary,
                        dpi);

                    dc.DrawText(pFt, new Point(pRect.X + (pRect.Width - pFt.Width) / 2.0, pRect.Y + (pRect.Height - pFt.Height) / 2.0));
                }

                // 4. Day Headers (Su Mo Tu We Th Fr Sa)
                string[] dayHeaders = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
                double cellW = (w - 16) / 7.0;
                for (int i = 0; i < 7; i++)
                {
                    var dhFt = new FormattedText(
                        dayHeaders[i],
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        ZeroWpfTheme.MediumTypeface,
                        10.5,
                        ZeroWpfTheme.TextMuted,
                        dpi);

                    double dhX = 8 + (i * cellW) + (cellW - dhFt.Width) / 2.0;
                    dc.DrawText(dhFt, new Point(dhX, 62));
                }

                // Divider line
                dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(8, 78), new Point(w - 8, 78));

                // 5. 42-Day Matrix
                double gridTop = 82;
                double cellH = 28;

                for (int i = 0; i < CalendarModel.TotalCells; i++)
                {
                    var cell = _cells[i];
                    int row = i / 7;
                    int col = i % 7;

                    double cx = 8 + (col * cellW);
                    double cy = gridTop + (row * cellH);
                    Rect cRect = new Rect(cx + 2, cy + 2, cellW - 4, cellH - 4);

                    bool isHovered = (i == _hoveredCellIndex && !cell.IsDisabled);

                    if (cell.IsSelected)
                    {
                        dc.DrawRoundedRectangle(ZeroWpfTheme.PrimaryAccent, null, cRect, 4, 4);
                    }
                    else if (isHovered)
                    {
                        dc.DrawRoundedRectangle(ZeroWpfTheme.BgHover, null, cRect, 4, 4);
                    }
                    else if (cell.IsToday)
                    {
                        dc.DrawRoundedRectangle(null, new Pen(ZeroWpfTheme.PrimaryAccent, 1.0), cRect, 4, 4);
                    }

                    Brush textBrush;
                    if (cell.IsDisabled)
                    {
                        textBrush = ZeroWpfTheme.TextMuted;
                    }
                    else if (cell.IsSelected)
                    {
                        textBrush = Brushes.White;
                    }
                    else if (!cell.IsCurrentMonth)
                    {
                        textBrush = ZeroWpfTheme.TextMuted;
                    }
                    else
                    {
                        textBrush = ZeroWpfTheme.TextPrimary;
                    }

                    var dayFt = new FormattedText(
                        cell.DayNumber.ToString(),
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        cell.IsSelected ? ZeroWpfTheme.BoldTypeface : ZeroWpfTheme.RegularTypeface,
                        11.5,
                        textBrush,
                        dpi);

                    dc.DrawText(dayFt, new Point(cx + (cellW - dayFt.Width) / 2.0, cy + (cellH - dayFt.Height) / 2.0));
                }

                // 6. Today Shortcut Link
                var todayFt = new FormattedText(
                    "Today: " + DateTime.Today.ToString("MMM dd, yyyy"),
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.MediumTypeface,
                    10.5,
                    _hoveredTodayLink ? ZeroWpfTheme.PrimaryAccent : ZeroWpfTheme.TextSecondary,
                    dpi);

                dc.DrawText(todayFt, new Point((w - todayFt.Width) / 2.0, h - 22));
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
    private void OnThemeChanged() => UpdateThemeColors(isFocused: _dateBox.IsFocused);
}

    /// <summary>
    /// Legacy alias for DateEdit.
    /// Preserved for 100% backward compatibility.
    /// </summary>
    [Obsolete("ZeroDatePicker is deprecated. Use DateEdit instead.")]
    public class ZeroDatePicker : DateEdit
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZDateEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("DateEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZDateEdit instead.")]
    public class DateEdit : ZDateEdit { }

    /// <summary>
    /// Legacy alias for <see cref="ZDateEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroDateEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZDateEdit instead.")]
    public class ZeroDateEdit : ZDateEdit { }

    #endregion

}
