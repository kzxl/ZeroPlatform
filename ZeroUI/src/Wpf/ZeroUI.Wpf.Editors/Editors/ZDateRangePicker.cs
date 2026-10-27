using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Input.Date;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Enterprise dual-date range selector (From Date -> To Date) for WPF with connected range ribbon,
    /// 1-click quick preset filters, interactive hover range preview, and calendar popup powered by CalendarModel.
    /// </summary>
    public class ZDateRangePicker : Control
    {
        private readonly Border _containerBorder;
        private readonly TextBlock _rangeDisplayBlock;
        private readonly Button _calendarTriggerButton;
        private readonly Popup _calendarPopup;
        private readonly DateRangePopupContent _popupContent;

        #region Dependency Properties

        public static readonly DependencyProperty StartDateProperty =
            DependencyProperty.Register(nameof(StartDate), typeof(DateTime), typeof(ZDateRangePicker),
                new FrameworkPropertyMetadata(DateTime.Today.AddDays(-6), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDateRangeChanged));

        public static readonly DependencyProperty EndDateProperty =
            DependencyProperty.Register(nameof(EndDate), typeof(DateTime), typeof(ZDateRangePicker),
                new FrameworkPropertyMetadata(DateTime.Today, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnDateRangeChanged));

        public static readonly DependencyProperty DateFormatProperty =
            DependencyProperty.Register(nameof(DateFormat), typeof(string), typeof(ZDateRangePicker),
                new FrameworkPropertyMetadata("yyyy-MM-dd", OnDateFormatChanged));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(ZDateRangePicker),
                new FrameworkPropertyMetadata(new CornerRadius(6)));

        public static readonly DependencyProperty ShowPresetsProperty =
            DependencyProperty.Register(nameof(ShowPresets), typeof(bool), typeof(ZDateRangePicker),
                new FrameworkPropertyMetadata(true, (d, e) => ((DateRangePicker)d)._popupContent.Refresh()));

        public static readonly DependencyProperty ViewModeProperty =
            DependencyProperty.Register(nameof(ViewMode), typeof(DateRangeViewMode), typeof(ZDateRangePicker),
                new FrameworkPropertyMetadata(DateRangeViewMode.Day, OnViewModeChanged));

        #endregion

        #region Properties & Events

        public DateTime StartDate
        {
            get => (DateTime)GetValue(StartDateProperty);
            set => SetValue(StartDateProperty, value);
        }

        public DateTime EndDate
        {
            get => (DateTime)GetValue(EndDateProperty);
            set => SetValue(EndDateProperty, value);
        }

        public string DateFormat
        {
            get => (string)GetValue(DateFormatProperty);
            set => SetValue(DateFormatProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public bool ShowPresets
        {
            get => (bool)GetValue(ShowPresetsProperty);
            set => SetValue(ShowPresetsProperty, value);
        }

        public DateRangeViewMode ViewMode
        {
            get => (DateRangeViewMode)GetValue(ViewModeProperty);
            set => SetValue(ViewModeProperty, value);
        }

        public event EventHandler<(DateTime Start, DateTime End)>? DateRangeChanged;

        #endregion

        public ZDateRangePicker()
        {
            Height = 32;
            Focusable = true;
            Cursor = Cursors.Hand;

            _containerBorder = new Border
            {
                CornerRadius = CornerRadius,
                BorderThickness = new Thickness(1),
                SnapsToDevicePixels = true,
                Padding = new Thickness(10, 0, 6, 0)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });

            _rangeDisplayBlock = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12.5,
                FontWeight = FontWeights.Medium
            };
            UpdateDisplayText();

            Grid.SetColumn(_rangeDisplayBlock, 0);
            grid.Children.Add(_rangeDisplayBlock);

            _calendarTriggerButton = new Button
            {
                Content = "📅",
                FontSize = 12,
                Width = 22,
                Height = 22,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            _calendarTriggerButton.Click += (s, e) => TogglePopup();

            Grid.SetColumn(_calendarTriggerButton, 1);
            grid.Children.Add(_calendarTriggerButton);

            _containerBorder.Child = grid;

            // Calendar Popup
            _popupContent = new DateRangePopupContent(this);
            _calendarPopup = new Popup
            {
                PlacementTarget = this,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                Child = _popupContent
            };

            AddVisualChild(_containerBorder);
            AddLogicalChild(_containerBorder);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            UpdateThemeColors(isFocused: false);
        }

        private static void OnDateRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZDateRangePicker picker)
            {
                picker.UpdateDisplayText();
                picker.DateRangeChanged?.Invoke(picker, (picker.StartDate, picker.EndDate));
                picker._popupContent.Refresh();
            }
        }

        private static void OnDateFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZDateRangePicker picker)
            {
                picker.UpdateDisplayText();
            }
        }

        private static void OnViewModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZDateRangePicker picker)
            {
                var mode = (DateRangeViewMode)e.NewValue;
                picker.DateFormat = DateRangeViewModeHelper.GetDefaultFormat(mode);
                var (s, eDate) = DateRangeViewModeHelper.NormalizeRange(mode, picker.StartDate, picker.EndDate);
                picker.StartDate = s;
                picker.EndDate = eDate;
                picker.UpdateDisplayText();
                picker._popupContent.Refresh();
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (IsEnabled && e.LeftButton == MouseButtonState.Pressed)
            {
                Focus();
                TogglePopup();
                e.Handled = true;
            }
        }

        public void TogglePopup()
        {
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

        public void SetRange(DateTime start, DateTime end)
        {
            var (s, e) = DateRangeViewModeHelper.NormalizeRange(ViewMode, start, end);
            StartDate = s;
            EndDate = e;
            UpdateDisplayText();
        }

        private void UpdateDisplayText()
        {
            _rangeDisplayBlock.Text = $"{StartDate.ToString(DateFormat)}   ➜   {EndDate.ToString(DateFormat)}";
        }

        private void UpdateThemeColors(bool isFocused)
        {
            _containerBorder.Background = ZeroWpfTheme.BgInput;
            _containerBorder.BorderBrush = isFocused ? ZeroWpfTheme.BorderFocus : ZeroWpfTheme.BorderDefault;
            _rangeDisplayBlock.Foreground = ZeroWpfTheme.TextPrimary;
            _calendarTriggerButton.Foreground = ZeroWpfTheme.TextSecondary;
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            UpdateThemeColors(true);
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);
            UpdateThemeColors(false);
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

        #region Date Range Popup Content

        private class DateRangePopupContent : FrameworkElement
        {
            private readonly ZDateRangePicker _owner;
            private readonly CalendarModel _calendarModel;
            private readonly CalendarDayCell[] _cells = new CalendarDayCell[CalendarModel.TotalCells];
            private DateTime? _pendingStartDate;
            private int _hoveredCellIndex = -1;
            private int _hoveredPresetIndex = -1;
            private bool _hoveredPrevNav = false;
            private bool _hoveredNextNav = false;

            private static readonly string[] MonthNames = new[]
            {
                "Jan", "Feb", "Mar", "Apr",
                "May", "Jun", "Jul", "Aug",
                "Sep", "Oct", "Nov", "Dec"
            };

            public DateRangePopupContent(ZDateRangePicker owner)
            {
                _owner = owner;
                _calendarModel = new CalendarModel(owner.StartDate);
                Width = 380;
                Height = 310;
                Cursor = Cursors.Hand;
            }

            public void Refresh()
            {
                _pendingStartDate = null;
                _calendarModel.ViewDate = new DateTime(_owner.StartDate.Year, _owner.StartDate.Month, 1);
                _calendarModel.FillDaysGrid(_cells);
                InvalidateVisual();
            }

            private string[] GetPresetLabels()
            {
                return _owner.ViewMode switch
                {
                    DateRangeViewMode.Month => new[]
                    {
                        "This Month", "Last Month", "This Quarter", "Last Quarter", "This Year", "Last Year"
                    },
                    DateRangeViewMode.Year => new[]
                    {
                        "This Year", "Last Year", "Last 3 Years", "Last 5 Years", "Last 10 Years"
                    },
                    _ => new[]
                    {
                        "Today", "Yesterday", "Last 7 Days", "Last 30 Days", "This Month", "Last Month", "Year to Date"
                    }
                };
            }

            private void ApplyPresetByIndex(int index)
            {
                DateTime today = DateTime.Today;
                DateTime start, end;

                switch (_owner.ViewMode)
                {
                    case DateRangeViewMode.Month:
                        switch (index)
                        {
                            case 0:
                                start = new DateTime(today.Year, today.Month, 1);
                                end = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                                break;
                            case 1:
                                var lm = today.AddMonths(-1);
                                start = new DateTime(lm.Year, lm.Month, 1);
                                end = new DateTime(lm.Year, lm.Month, DateTime.DaysInMonth(lm.Year, lm.Month));
                                break;
                            case 2:
                                int qStart = ((today.Month - 1) / 3) * 3 + 1;
                                start = new DateTime(today.Year, qStart, 1);
                                end = new DateTime(today.Year, qStart + 2, DateTime.DaysInMonth(today.Year, qStart + 2));
                                break;
                            case 3:
                                var lqDate = today.AddMonths(-3);
                                int lqStart = ((lqDate.Month - 1) / 3) * 3 + 1;
                                start = new DateTime(lqDate.Year, lqStart, 1);
                                end = new DateTime(lqDate.Year, lqStart + 2, DateTime.DaysInMonth(lqDate.Year, lqStart + 2));
                                break;
                            case 4:
                                start = new DateTime(today.Year, 1, 1);
                                end = new DateTime(today.Year, 12, 31);
                                break;
                            case 5:
                            default:
                                start = new DateTime(today.Year - 1, 1, 1);
                                end = new DateTime(today.Year - 1, 12, 31);
                                break;
                        }
                        _owner.SetRange(start, end);
                        _owner.ClosePopup();
                        break;

                    case DateRangeViewMode.Year:
                        switch (index)
                        {
                            case 0:
                                start = new DateTime(today.Year, 1, 1);
                                end = new DateTime(today.Year, 12, 31);
                                break;
                            case 1:
                                start = new DateTime(today.Year - 1, 1, 1);
                                end = new DateTime(today.Year - 1, 12, 31);
                                break;
                            case 2:
                                start = new DateTime(today.Year - 2, 1, 1);
                                end = new DateTime(today.Year, 12, 31);
                                break;
                            case 3:
                                start = new DateTime(today.Year - 4, 1, 1);
                                end = new DateTime(today.Year, 12, 31);
                                break;
                            case 4:
                            default:
                                start = new DateTime(today.Year - 9, 1, 1);
                                end = new DateTime(today.Year, 12, 31);
                                break;
                        }
                        _owner.SetRange(start, end);
                        _owner.ClosePopup();
                        break;

                    case DateRangeViewMode.Day:
                    default:
                        var dayPresets = new[]
                        {
                            DateRangePreset.Today, DateRangePreset.Yesterday, DateRangePreset.Last7Days,
                            DateRangePreset.Last30Days, DateRangePreset.ThisMonth, DateRangePreset.LastMonth, DateRangePreset.YearToDate
                        };
                        if (index >= 0 && index < dayPresets.Length)
                        {
                            var (s, e) = DateRangePresetHelper.CalculateRange(dayPresets[index]);
                            _owner.SetRange(s, e);
                            _owner.ClosePopup();
                        }
                        break;
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                Point pt = e.GetPosition(this);

                // Sidebar presets: X <= 100
                string[] presets = GetPresetLabels();
                int hovPreset = -1;
                if (pt.X >= 8 && pt.X <= 96 && pt.Y >= 36)
                {
                    int pIdx = (int)((pt.Y - 36) / 28);
                    if (pIdx >= 0 && pIdx < presets.Length) hovPreset = pIdx;
                }

                // Calendar navigation chevrons
                bool hovPrev = pt.X >= 115 && pt.X <= 135 && pt.Y >= 10 && pt.Y <= 30;
                bool hovNext = pt.X >= (ActualWidth - 30) && pt.X <= (ActualWidth - 10) && pt.Y >= 10 && pt.Y <= 30;

                int hovCell = -1;
                double calLeft = 106;

                if (_owner.ViewMode == DateRangeViewMode.Month || _owner.ViewMode == DateRangeViewMode.Year)
                {
                    double calTop = 50;
                    double cellW = (ActualWidth - calLeft - 10) / 4.0;
                    double cellH = 65;

                    if (pt.X >= calLeft && pt.X <= ActualWidth - 10 && pt.Y >= calTop && pt.Y < calTop + (3 * cellH))
                    {
                        int col = (int)((pt.X - calLeft) / cellW);
                        int row = (int)((pt.Y - calTop) / cellH);
                        if (col >= 0 && col < 4 && row >= 0 && row < 3)
                        {
                            hovCell = (row * 4) + col;
                        }
                    }
                }
                else
                {
                    double calTop = 64;
                    double cellW = (ActualWidth - calLeft - 10) / 7.0;
                    double cellH = 28;

                    if (pt.X >= calLeft && pt.X <= ActualWidth - 10 && pt.Y >= calTop && pt.Y < calTop + (6 * cellH))
                    {
                        int col = (int)((pt.X - calLeft) / cellW);
                        int row = (int)((pt.Y - calTop) / cellH);
                        if (col >= 0 && col < 7 && row >= 0 && row < 6)
                        {
                            hovCell = (row * 7) + col;
                        }
                    }
                }

                if (_hoveredPresetIndex != hovPreset || _hoveredPrevNav != hovPrev ||
                    _hoveredNextNav != hovNext || _hoveredCellIndex != hovCell)
                {
                    _hoveredPresetIndex = hovPreset;
                    _hoveredPrevNav = hovPrev;
                    _hoveredNextNav = hovNext;
                    _hoveredCellIndex = hovCell;
                    InvalidateVisual();
                }
            }

            protected override void OnMouseLeave(MouseEventArgs e)
            {
                base.OnMouseLeave(e);
                _hoveredPresetIndex = -1;
                _hoveredPrevNav = false;
                _hoveredNextNav = false;
                _hoveredCellIndex = -1;
                InvalidateVisual();
            }

            protected override void OnMouseDown(MouseButtonEventArgs e)
            {
                base.OnMouseDown(e);

                if (_hoveredPrevNav)
                {
                    if (_owner.ViewMode == DateRangeViewMode.Year)
                        _calendarModel.ViewDate = _calendarModel.ViewDate.AddYears(-10);
                    else if (_owner.ViewMode == DateRangeViewMode.Month)
                        _calendarModel.NavigatePreviousYear();
                    else
                    {
                        _calendarModel.NavigatePreviousMonth();
                        _calendarModel.FillDaysGrid(_cells);
                    }
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }

                if (_hoveredNextNav)
                {
                    if (_owner.ViewMode == DateRangeViewMode.Year)
                        _calendarModel.ViewDate = _calendarModel.ViewDate.AddYears(10);
                    else if (_owner.ViewMode == DateRangeViewMode.Month)
                        _calendarModel.NavigateNextYear();
                    else
                    {
                        _calendarModel.NavigateNextMonth();
                        _calendarModel.FillDaysGrid(_cells);
                    }
                    InvalidateVisual();
                    e.Handled = true;
                    return;
                }

                if (_hoveredPresetIndex >= 0)
                {
                    ApplyPresetByIndex(_hoveredPresetIndex);
                    e.Handled = true;
                    return;
                }

                if (_owner.ViewMode == DateRangeViewMode.Month && _hoveredCellIndex >= 0 && _hoveredCellIndex < 12)
                {
                    int m = _hoveredCellIndex + 1;
                    DateTime cellDate = new DateTime(_calendarModel.ViewDate.Year, m, 1);
                    if (_pendingStartDate == null)
                    {
                        _pendingStartDate = cellDate;
                        InvalidateVisual();
                    }
                    else
                    {
                        DateTime s = _pendingStartDate.Value < cellDate ? _pendingStartDate.Value : cellDate;
                        DateTime endMonth = cellDate >= _pendingStartDate.Value ? cellDate : _pendingStartDate.Value;
                        DateTime end = new DateTime(endMonth.Year, endMonth.Month, DateTime.DaysInMonth(endMonth.Year, endMonth.Month));
                        _pendingStartDate = null;
                        _owner.SetRange(s, end);
                        _owner.ClosePopup();
                    }
                    e.Handled = true;
                    return;
                }

                if (_owner.ViewMode == DateRangeViewMode.Year && _hoveredCellIndex >= 0 && _hoveredCellIndex < 12)
                {
                    int startDecade = (_calendarModel.ViewDate.Year / 10) * 10;
                    int y = (startDecade - 1) + _hoveredCellIndex;
                    DateTime cellDate = new DateTime(y, 1, 1);
                    if (_pendingStartDate == null)
                    {
                        _pendingStartDate = cellDate;
                        InvalidateVisual();
                    }
                    else
                    {
                        DateTime s = _pendingStartDate.Value < cellDate ? _pendingStartDate.Value : cellDate;
                        DateTime endYear = cellDate >= _pendingStartDate.Value ? cellDate : _pendingStartDate.Value;
                        DateTime end = new DateTime(endYear.Year, 12, 31);
                        _pendingStartDate = null;
                        _owner.SetRange(s, end);
                        _owner.ClosePopup();
                    }
                    e.Handled = true;
                    return;
                }

                if (_owner.ViewMode == DateRangeViewMode.Day && _hoveredCellIndex >= 0 && _hoveredCellIndex < _cells.Length)
                {
                    DateTime clickedDate = _cells[_hoveredCellIndex].Date;
                    if (_pendingStartDate == null)
                    {
                        _pendingStartDate = clickedDate;
                        InvalidateVisual();
                    }
                    else
                    {
                        DateTime start = _pendingStartDate.Value;
                        DateTime end = clickedDate;
                        _pendingStartDate = null;
                        _owner.SetRange(start, end);
                        _owner.ClosePopup();
                    }
                    e.Handled = true;
                }
            }

            protected override void OnRender(DrawingContext dc)
            {
                base.OnRender(dc);

                double w = ActualWidth;
                double h = ActualHeight;
                if (w <= 0 || h <= 0) return;

                double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

                // 1. Popup Background & Border
                Rect bgRect = new Rect(0.5, 0.5, w - 1.0, h - 1.0);
                dc.DrawRoundedRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, bgRect, 8, 8);

                // 2. Sidebar for Quick Presets (X: 8 to 98)
                dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(100, 8), new Point(100, h - 8));

                var sidebarTitle = new FormattedText("Quick Filters", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.BoldTypeface, 11.0, ZeroWpfTheme.TextMuted, dpi);
                dc.DrawText(sidebarTitle, new Point(12, 14));

                string[] presets = GetPresetLabels();
                for (int i = 0; i < presets.Length; i++)
                {
                    double py = 36 + (i * 28);
                    Rect pRect = new Rect(8, py, 88, 24);
                    bool hov = (_hoveredPresetIndex == i);

                    if (hov)
                    {
                        dc.DrawRoundedRectangle(ZeroWpfTheme.BgHover, null, pRect, 4, 4);
                    }

                    var pFt = new FormattedText(
                        presets[i],
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        ZeroWpfTheme.MediumTypeface,
                        10.5,
                        hov ? ZeroWpfTheme.PrimaryAccent : ZeroWpfTheme.TextSecondary,
                        dpi);

                    dc.DrawText(pFt, new Point(14, py + 4));
                }

                // 3. Right Calendar Panel Header
                double calLeft = 106;
                double calRight = w - 10;
                double calWidth = calRight - calLeft;

                string headerText = _owner.ViewMode switch
                {
                    DateRangeViewMode.Month => _calendarModel.ViewDate.Year.ToString(),
                    DateRangeViewMode.Year => $"{(_calendarModel.ViewDate.Year / 10) * 10} - {((_calendarModel.ViewDate.Year / 10) * 10) + 9}",
                    _ => _calendarModel.ViewDate.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
                };

                var headerFt = new FormattedText(
                    headerText,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.BoldTypeface,
                    13.0,
                    ZeroWpfTheme.TextPrimary,
                    dpi);

                dc.DrawText(headerFt, new Point(calLeft + (calWidth - headerFt.Width) / 2.0, 10));

                Brush prevBrush = _hoveredPrevNav ? ZeroWpfTheme.PrimaryAccent : ZeroWpfTheme.TextSecondary;
                Brush nextBrush = _hoveredNextNav ? ZeroWpfTheme.PrimaryAccent : ZeroWpfTheme.TextSecondary;
                var prevFt = new FormattedText("◀", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.RegularTypeface, 11.0, prevBrush, dpi);
                var nextFt = new FormattedText("▶", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.RegularTypeface, 11.0, nextBrush, dpi);
                dc.DrawText(prevFt, new Point(calLeft + 10, 11));
                dc.DrawText(nextFt, new Point(calRight - 18, 11));

                Brush rangeBandBrush = new SolidColorBrush(ZeroWpfTheme.PrimaryAccent.Color) { Opacity = 0.22 };
                rangeBandBrush.Freeze();

                if (_owner.ViewMode == DateRangeViewMode.Month)
                {
                    double calTop = 50;
                    double cellW = calWidth / 4.0;
                    double cellH = 65;

                    DateTime startRange = _pendingStartDate ?? _owner.StartDate;
                    DateTime endRange = _pendingStartDate != null
                        ? (_hoveredCellIndex >= 0 ? new DateTime(_calendarModel.ViewDate.Year, _hoveredCellIndex + 1, 1) : startRange)
                        : _owner.EndDate;

                    if (startRange > endRange) (startRange, endRange) = (endRange, startRange);

                    for (int m = 0; m < 12; m++)
                    {
                        int row = m / 4;
                        int col = m % 4;
                        double cx = calLeft + (col * cellW);
                        double cy = calTop + (row * cellH);

                        DateTime mDate = new DateTime(_calendarModel.ViewDate.Year, m + 1, 1);
                        bool isStart = mDate.Year == startRange.Year && mDate.Month == startRange.Month;
                        bool isEnd = mDate.Year == endRange.Year && mDate.Month == endRange.Month;
                        bool inRange = mDate >= new DateTime(startRange.Year, startRange.Month, 1) &&
                                       mDate <= new DateTime(endRange.Year, endRange.Month, 1);

                        if (inRange && (startRange.Year != endRange.Year || startRange.Month != endRange.Month))
                        {
                            Rect bandRect = new Rect(
                                isStart ? (cx + (cellW / 2.0)) : cx,
                                cy + 12,
                                isStart || isEnd ? (cellW / 2.0) : cellW,
                                cellH - 24);
                            dc.DrawRectangle(rangeBandBrush, null, bandRect);
                        }

                        Rect pillRect = new Rect(cx + 6, cy + 12, cellW - 12, cellH - 24);
                        if (isStart || isEnd)
                        {
                            dc.DrawRoundedRectangle(ZeroWpfTheme.PrimaryAccent, null, pillRect, 6, 6);
                        }
                        else if (m == _hoveredCellIndex)
                        {
                            dc.DrawRoundedRectangle(ZeroWpfTheme.BgHover, null, pillRect, 6, 6);
                        }

                        Brush textBrush = (isStart || isEnd) ? Brushes.White : ZeroWpfTheme.TextPrimary;
                        var mFt = new FormattedText(
                            MonthNames[m],
                            CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight,
                            (isStart || isEnd) ? ZeroWpfTheme.BoldTypeface : ZeroWpfTheme.MediumTypeface,
                            12.0,
                            textBrush,
                            dpi);

                        dc.DrawText(mFt, new Point(cx + (cellW - mFt.Width) / 2.0, cy + (cellH - mFt.Height) / 2.0));
                    }
                }
                else if (_owner.ViewMode == DateRangeViewMode.Year)
                {
                    double calTop = 50;
                    double cellW = calWidth / 4.0;
                    double cellH = 65;

                    int startDecade = (_calendarModel.ViewDate.Year / 10) * 10;
                    int startYear = _pendingStartDate?.Year ?? _owner.StartDate.Year;
                    int endYear = _pendingStartDate != null
                        ? (_hoveredCellIndex >= 0 ? (startDecade - 1) + _hoveredCellIndex : startYear)
                        : _owner.EndDate.Year;

                    if (startYear > endYear) (startYear, endYear) = (endYear, startYear);

                    for (int i = 0; i < 12; i++)
                    {
                        int yr = (startDecade - 1) + i;
                        int row = i / 4;
                        int col = i % 4;
                        double cx = calLeft + (col * cellW);
                        double cy = calTop + (row * cellH);

                        bool isStart = yr == startYear;
                        bool isEnd = yr == endYear;
                        bool inRange = yr >= startYear && yr <= endYear;
                        bool isOutside = (yr < startDecade || yr > startDecade + 9);

                        if (inRange && startYear != endYear)
                        {
                            Rect bandRect = new Rect(
                                isStart ? (cx + (cellW / 2.0)) : cx,
                                cy + 12,
                                isStart || isEnd ? (cellW / 2.0) : cellW,
                                cellH - 24);
                            dc.DrawRectangle(rangeBandBrush, null, bandRect);
                        }

                        Rect pillRect = new Rect(cx + 6, cy + 12, cellW - 12, cellH - 24);
                        if (isStart || isEnd)
                        {
                            dc.DrawRoundedRectangle(ZeroWpfTheme.PrimaryAccent, null, pillRect, 6, 6);
                        }
                        else if (i == _hoveredCellIndex)
                        {
                            dc.DrawRoundedRectangle(ZeroWpfTheme.BgHover, null, pillRect, 6, 6);
                        }

                        Brush textBrush = (isStart || isEnd) ? Brushes.White : (isOutside ? ZeroWpfTheme.TextMuted : ZeroWpfTheme.TextPrimary);
                        var yFt = new FormattedText(
                            yr.ToString(),
                            CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight,
                            (isStart || isEnd) ? ZeroWpfTheme.BoldTypeface : ZeroWpfTheme.MediumTypeface,
                            12.0,
                            textBrush,
                            dpi);

                        dc.DrawText(yFt, new Point(cx + (cellW - yFt.Width) / 2.0, cy + (cellH - yFt.Height) / 2.0));
                    }
                }
                else
                {
                    // 4. Day of Week Headers
                    string[] dows = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
                    double cellW = calWidth / 7.0;
                    double cellH = 28;
                    for (int i = 0; i < 7; i++)
                    {
                        var dowFt = new FormattedText(dows[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.MediumTypeface, 10.5, ZeroWpfTheme.TextMuted, dpi);
                        dc.DrawText(dowFt, new Point(calLeft + (i * cellW) + (cellW - dowFt.Width) / 2.0, 42));
                    }

                    dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(calLeft, 60), new Point(calRight, 60));

                    // 5. 42-Day Range Rendering
                    _calendarModel.FillDaysGrid(_cells);
                    double gridTop = 64;
                    DateTime startRange = _pendingStartDate ?? _owner.StartDate;
                    DateTime endRange = _pendingStartDate != null
                        ? (_hoveredCellIndex >= 0 ? _cells[_hoveredCellIndex].Date : startRange)
                        : _owner.EndDate;

                    if (startRange > endRange) (startRange, endRange) = (endRange, startRange);

                    for (int i = 0; i < CalendarModel.TotalCells; i++)
                    {
                        var cell = _cells[i];
                        int row = i / 7;
                        int col = i % 7;
                        double cx = calLeft + (col * cellW);
                        double cy = gridTop + (row * cellH);
                        Rect cellRect = new Rect(cx, cy, cellW, cellH);

                        bool isStart = cell.Date.Date == startRange.Date;
                        bool isEnd = cell.Date.Date == endRange.Date;
                        bool inRange = cell.Date.Date >= startRange.Date && cell.Date.Date <= endRange.Date;

                        if (inRange && startRange != endRange)
                        {
                            Rect bandRect = new Rect(
                                isStart ? (cx + (cellW / 2.0)) : cx,
                                cy + 3,
                                isStart || isEnd ? (cellW / 2.0) : cellW,
                                cellH - 6);
                            dc.DrawRectangle(rangeBandBrush, null, bandRect);
                        }

                        Rect pillRect = new Rect(cx + 2, cy + 2, cellW - 4, cellH - 4);
                        if (isStart || isEnd)
                        {
                            dc.DrawRoundedRectangle(ZeroWpfTheme.PrimaryAccent, null, pillRect, 4, 4);
                        }
                        else if (i == _hoveredCellIndex)
                        {
                            dc.DrawRoundedRectangle(ZeroWpfTheme.BgHover, null, pillRect, 4, 4);
                        }

                        Brush textBrush;
                        if (isStart || isEnd) textBrush = Brushes.White;
                        else if (!cell.IsCurrentMonth) textBrush = ZeroWpfTheme.TextMuted;
                        else textBrush = ZeroWpfTheme.TextPrimary;

                        var dayFt = new FormattedText(
                            cell.DayNumber.ToString(),
                            CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight,
                            (isStart || isEnd) ? ZeroWpfTheme.BoldTypeface : ZeroWpfTheme.RegularTypeface,
                            11.5,
                            textBrush,
                            dpi);

                        dc.DrawText(dayFt, new Point(cx + (cellW - dayFt.Width) / 2.0, cy + (cellH - dayFt.Height) / 2.0));
                    }
                }

                // Footer Prompt
                string prompt = _pendingStartDate == null
                    ? (_owner.ViewMode == DateRangeViewMode.Month ? "Select start month..." : (_owner.ViewMode == DateRangeViewMode.Year ? "Select start year..." : "Select start date..."))
                    : $"Start: {_pendingStartDate:yyyy-MM} — Select end date";

                var promptFt = new FormattedText(prompt, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.MediumTypeface, 10.0, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(promptFt, new Point(calLeft + 4, h - 20));
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
    private void OnThemeChanged() => UpdateThemeColors(isFocused: IsFocused);
}

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZDateRangePicker"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("DateRangePicker is deprecated and will be removed in 5 release cycles. Please migrate to ZDateRangePicker instead.")]
    public class DateRangePicker : ZDateRangePicker { }

    /// <summary>
    /// Legacy alias for <see cref="ZDateRangePicker"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroDateRangePicker is deprecated and will be removed in 5 release cycles. Please migrate to ZDateRangePicker instead.")]
    public class ZeroDateRangePicker : ZDateRangePicker { }

    #endregion

}
