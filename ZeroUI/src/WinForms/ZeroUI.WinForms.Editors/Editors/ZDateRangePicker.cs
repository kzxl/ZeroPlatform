using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Input.Date;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Enterprise Dual-Date Range Selector (From Date -> To Date) with connected range ribbon,
    /// 1-click quick preset filters, interactive hover range preview, and calendar popup.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultEvent("DateRangeChanged")]
    [DefaultProperty("StartDate")]
    [Description("Enterprise dual-date range selector with 1-click presets and calendar popup")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroDateRangePicker.bmp")]
    public class ZDateRangePicker : ControlBase
    {
        private DateTime _startDate = DateTime.Today.AddDays(-6);
        private DateTime _endDate = DateTime.Today;
        private string _dateFormat = "yyyy-MM-dd";
        private DateRangePreset _preset = DateRangePreset.Last7Days;
        private DateRangeViewMode _viewMode = DateRangeViewMode.Day;

        private bool _isHovered = false;
        private bool _isFocused = false;
        private readonly DropDownHost _dropdown;
        private readonly DateRangePopupControl _popupControl;
        private Rectangle _chevronRect;

        public event EventHandler? DateRangeChanged;

        public ZDateRangePicker()
        {
            Size = new Size(260, 36);
            Font = new Font("Segoe UI", 9.25f);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;

            _popupControl = new DateRangePopupControl(this);
            _dropdown = new DropDownHost
            {
                Content = _popupControl
            };
            _dropdown.Closed += (s, e) =>
            {
                _isFocused = false;
                Invalidate();
            };

            ZeroUIConfig.CornerStyleChanged += (s, e) => Invalidate();
            ZeroUIConfig.FontChanged += (s, e) =>
            {
                Font = ZeroUIConfig.DefaultFont;
                Invalidate();
            };
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            _popupControl?.Invalidate();
            Invalidate();
        }

        [Category("Behavior")]
        [DefaultValue(DateRangeViewMode.Day)]
        [Description("Specifies whether the picker operates at Day, Month, or Year granularity.")]
        public DateRangeViewMode ViewMode
        {
            get => _viewMode;
            set
            {
                if (_viewMode != value)
                {
                    _viewMode = value;
                    _dateFormat = DateRangeViewModeHelper.GetDefaultFormat(value);
                    var (s, e) = DateRangeViewModeHelper.NormalizeRange(_viewMode, _startDate, _endDate);
                    _startDate = s;
                    _endDate = e;
                    Invalidate();
                    DateRangeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Category("Data")]
        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                var val = value.Date;
                if (_startDate != val)
                {
                    _startDate = val;
                    if (_endDate < _startDate) _endDate = _startDate;
                    var (s, e) = DateRangeViewModeHelper.NormalizeRange(_viewMode, _startDate, _endDate);
                    _startDate = s;
                    _endDate = e;
                    _preset = DateRangePreset.Custom;
                    Invalidate();
                    DateRangeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Category("Data")]
        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                var val = value.Date;
                if (_endDate != val)
                {
                    _endDate = val;
                    if (_startDate > _endDate) _startDate = _endDate;
                    var (s, e) = DateRangeViewModeHelper.NormalizeRange(_viewMode, _startDate, _endDate);
                    _startDate = s;
                    _endDate = e;
                    _preset = DateRangePreset.Custom;
                    Invalidate();
                    DateRangeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue("yyyy-MM-dd")]
        public string DateFormat
        {
            get => _dateFormat;
            set
            {
                _dateFormat = value ?? DateRangeViewModeHelper.GetDefaultFormat(_viewMode);
                Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(DateRangePreset.Last7Days)]
        public DateRangePreset Preset
        {
            get => _preset;
            set
            {
                _preset = value;
                ApplyPreset(value);
            }
        }

        public void SetRange(DateTime start, DateTime end)
        {
            var (s, e) = DateRangeViewModeHelper.NormalizeRange(_viewMode, start, end);
            _startDate = s;
            _endDate = e;
            _preset = DateRangePreset.Custom;
            Invalidate();
            DateRangeChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ApplyPreset(DateRangePreset preset)
        {
            _preset = preset;
            if (preset == DateRangePreset.Custom) return;

            var (start, end) = DateRangePresetHelper.CalculateRange(preset, fullMonthForThisMonth: true);
            var (s, e) = DateRangeViewModeHelper.NormalizeRange(_viewMode, start, end);
            _startDate = s;
            _endDate = e;

            Invalidate();
            DateRangeChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _chevronRect = new Rectangle(Width - 24, (Height - 14) / 2, 14, 14);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!_dropdown.Visible)
            {
                _popupControl.SyncFromPicker(_startDate, _endDate);
                _isFocused = true;
                Invalidate();
                _dropdown.ShowDropDown(this, 500, 280);
            }
            else
            {
                _dropdown.Close();
            }
        }

        internal void ClosePopup()
        {
            _dropdown.Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = CurrentPalette;

            // 1. Fill parent background to eliminate black corner clipping artifacts
            Color parentBg = ZeroUIConfig.GetParentBackground(this, palette.Background);
            using (var brushParent = new SolidBrush(parentBg))
            {
                g.FillRectangle(brushParent, ClientRectangle);
            }

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int effRadius = ZeroUIConfig.GetEffectiveRadius(6);

            // 2. Box Background & Rounded Border
            using (var path = ZeroUIConfig.CreateRoundedRectangle(rect, effRadius))
            {
                using var brushBg = new SolidBrush(palette.Surface);
                g.FillPath(brushBg, path);

                Color borderCol = _isFocused ? palette.Primary : (_isHovered ? palette.PrimaryHover : palette.Border);
                using var penBorder = new Pen(borderCol, _isFocused ? 1.5f : 1f);
                g.DrawPath(penBorder, path);
            }

            // 2. Calendar Glyph (📅)
            using (var iconFont = new Font("Segoe UI Emoji", 9.5f))
            using (var brushIcon = new SolidBrush(palette.Primary))
            {
                g.DrawString("📅", iconFont, brushIcon, 8, (Height - 18) / 2);
            }

            // 3. Date Range Text with pill accent: "2026-09-01  →  2026-09-03"
            string sText = _startDate.ToString(_dateFormat);
            string eText = _endDate.ToString(_dateFormat);

            using (var fontText = new Font(Font.FontFamily, 9f, FontStyle.Bold))
            using (var brushText = new SolidBrush(palette.TextPrimary))
            using (var brushArrow = new SolidBrush(palette.Primary))
            {
                g.DrawString(sText, fontText, brushText, 32, (Height - 16) / 2);

                int arrowX = 32 + (int)g.MeasureString(sText, fontText).Width + 4;
                g.DrawString("→", fontText, brushArrow, arrowX, (Height - 16) / 2);

                int endX = arrowX + 16;
                g.DrawString(eText, fontText, brushText, endX, (Height - 16) / 2);
            }

            // 4. Dropdown Chevron (▼ / ▲)
            using (var chevBrush = new SolidBrush(_isFocused ? palette.Primary : palette.TextSecondary))
            {
                PointF center = new PointF(_chevronRect.X + (_chevronRect.Width / 2f), _chevronRect.Y + (_chevronRect.Height / 2f));
                PointF[] pts;
                if (_isFocused)
                {
                    pts = new[]
                    {
                        new PointF(center.X - 3.5f, center.Y + 2f),
                        new PointF(center.X + 3.5f, center.Y + 2f),
                        new PointF(center.X, center.Y - 2.5f)
                    };
                }
                else
                {
                    pts = new[]
                    {
                        new PointF(center.X - 3.5f, center.Y - 2f),
                        new PointF(center.X + 3.5f, center.Y - 2f),
                        new PointF(center.X, center.Y + 2.5f)
                    };
                }
                g.FillPolygon(chevBrush, pts);
            }
        }

        private static GraphicsPath CreateRoundedRect(Rectangle r, int radius) =>
            ZeroUIConfig.CreateRoundedRectangle(r, radius);

        /// <summary>
        /// Inner calendar & preset popup container with connected range ribbon, hover preview,
        /// and multi-tier ViewMode support (Day, Month, Year).
        /// </summary>
        private class DateRangePopupControl : Control
        {
            private readonly ZDateRangePicker _picker;
            private DateTime _tempStart;
            private DateTime _tempEnd;
            private DateTime _hoverDate;
            private DateTime _viewMonth;
            private int _clickStep = 0; // 0: picking start, 1: picking end

            private Rectangle _prevYearRect;
            private Rectangle _prevMonthRect;
            private Rectangle _nextMonthRect;
            private Rectangle _nextYearRect;

            private readonly Rectangle[] _presetRects = new Rectangle[7];
            private readonly DateRangePreset[] _dayPresetValues = new[]
            {
                DateRangePreset.Today, DateRangePreset.Yesterday, DateRangePreset.Last7Days,
                DateRangePreset.Last30Days, DateRangePreset.ThisMonth, DateRangePreset.LastMonth, DateRangePreset.YearToDate
            };
            private int _hoveredPreset = -1;

            private readonly string[] _monthNames = new[]
            {
                "Jan", "Feb", "Mar", "Apr",
                "May", "Jun", "Jul", "Aug",
                "Sep", "Oct", "Nov", "Dec"
            };

            public DateRangePopupControl(ZDateRangePicker picker)
            {
                _picker = picker;
                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw, true);

                Font = new Font("Segoe UI", 9f);
                BackColor = ZeroTheme.Colors.CardBackground;
            }

            public void SyncFromPicker(DateTime start, DateTime end)
            {
                var (s, e) = DateRangeViewModeHelper.NormalizeRange(_picker.ViewMode, start, end);
                _tempStart = s;
                _tempEnd = e;
                _hoverDate = e;
                _viewMonth = new DateTime(s.Year, s.Month, 1);
                _clickStep = 0;
                Invalidate();
            }

            private string[] GetCurrentPresetNames()
            {
                return _picker.ViewMode switch
                {
                    DateRangeViewMode.Month => new[]
                    {
                        "This Month", "Last Month", "This Quarter", "Last Quarter", "This Year", "Last Year", "Year to Date"
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

                switch (_picker.ViewMode)
                {
                    case DateRangeViewMode.Month:
                        switch (index)
                        {
                            case 0: // This Month
                                start = new DateTime(today.Year, today.Month, 1);
                                end = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                                break;
                            case 1: // Last Month
                                var lastM = today.AddMonths(-1);
                                start = new DateTime(lastM.Year, lastM.Month, 1);
                                end = new DateTime(lastM.Year, lastM.Month, DateTime.DaysInMonth(lastM.Year, lastM.Month));
                                break;
                            case 2: // This Quarter
                                int qStartM = ((today.Month - 1) / 3) * 3 + 1;
                                start = new DateTime(today.Year, qStartM, 1);
                                end = new DateTime(today.Year, qStartM + 2, DateTime.DaysInMonth(today.Year, qStartM + 2));
                                break;
                            case 3: // Last Quarter
                                var prevQDate = today.AddMonths(-3);
                                int pqStartM = ((prevQDate.Month - 1) / 3) * 3 + 1;
                                start = new DateTime(prevQDate.Year, pqStartM, 1);
                                end = new DateTime(prevQDate.Year, pqStartM + 2, DateTime.DaysInMonth(prevQDate.Year, pqStartM + 2));
                                break;
                            case 4: // This Year
                                start = new DateTime(today.Year, 1, 1);
                                end = new DateTime(today.Year, 12, 31);
                                break;
                            case 5: // Last Year
                                start = new DateTime(today.Year - 1, 1, 1);
                                end = new DateTime(today.Year - 1, 12, 31);
                                break;
                            case 6: // Year to Date
                            default:
                                start = new DateTime(today.Year, 1, 1);
                                end = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                                break;
                        }
                        _picker.SetRange(start, end);
                        _picker.ClosePopup();
                        break;

                    case DateRangeViewMode.Year:
                        switch (index)
                        {
                            case 0: // This Year
                                start = new DateTime(today.Year, 1, 1);
                                end = new DateTime(today.Year, 12, 31);
                                break;
                            case 1: // Last Year
                                start = new DateTime(today.Year - 1, 1, 1);
                                end = new DateTime(today.Year - 1, 12, 31);
                                break;
                            case 2: // Last 3 Years
                                start = new DateTime(today.Year - 2, 1, 1);
                                end = new DateTime(today.Year, 12, 31);
                                break;
                            case 3: // Last 5 Years
                                start = new DateTime(today.Year - 4, 1, 1);
                                end = new DateTime(today.Year, 12, 31);
                                break;
                            case 4: // Last 10 Years
                            default:
                                start = new DateTime(today.Year - 9, 1, 1);
                                end = new DateTime(today.Year, 12, 31);
                                break;
                        }
                        _picker.SetRange(start, end);
                        _picker.ClosePopup();
                        break;

                    case DateRangeViewMode.Day:
                    default:
                        if (index >= 0 && index < _dayPresetValues.Length)
                        {
                            _picker.ApplyPreset(_dayPresetValues[index]);
                            _picker.ClosePopup();
                        }
                        break;
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);

                string[] presets = GetCurrentPresetNames();
                int hov = -1;
                for (int i = 0; i < presets.Length && i < _presetRects.Length; i++)
                {
                    if (_presetRects[i].Contains(e.Location))
                    {
                        hov = i;
                        break;
                    }
                }

                int calLeft = 140;
                int startY = 60;
                int calW = Width - calLeft - 20;

                if (_picker.ViewMode == DateRangeViewMode.Month)
                {
                    int colW = calW / 4;
                    int rowH = 50;
                    startY = 44;

                    if (e.X >= calLeft && e.X < calLeft + (4 * colW) && e.Y >= startY && e.Y < startY + (3 * rowH))
                    {
                        int col = (e.X - calLeft) / colW;
                        int row = (e.Y - startY) / rowH;
                        int mIdx = (row * 4) + col;
                        if (mIdx >= 0 && mIdx < 12)
                        {
                            int targetMonth = mIdx + 1;
                            int maxDays = DateTime.DaysInMonth(_viewMonth.Year, targetMonth);
                            var d = new DateTime(_viewMonth.Year, targetMonth, maxDays);
                            if (_hoverDate != d)
                            {
                                _hoverDate = d;
                                Invalidate();
                            }
                        }
                    }
                }
                else if (_picker.ViewMode == DateRangeViewMode.Year)
                {
                    int colW = calW / 4;
                    int rowH = 50;
                    startY = 44;

                    if (e.X >= calLeft && e.X < calLeft + (4 * colW) && e.Y >= startY && e.Y < startY + (3 * rowH))
                    {
                        int col = (e.X - calLeft) / colW;
                        int row = (e.Y - startY) / rowH;
                        int yIdx = (row * 4) + col;
                        if (yIdx >= 0 && yIdx < 12)
                        {
                            int startDecade = (_viewMonth.Year / 10) * 10;
                            int yr = (startDecade - 1) + yIdx;
                            if (yr >= 1 && yr <= 9999)
                            {
                                var d = new DateTime(yr, 12, 31);
                                if (_hoverDate != d)
                                {
                                    _hoverDate = d;
                                    Invalidate();
                                }
                            }
                        }
                    }
                }
                else
                {
                    int dayW = calW / 7;
                    int dayH = 26;

                    if (e.X >= calLeft && e.X < Width - 20 && e.Y >= startY && e.Y < startY + (6 * dayH))
                    {
                        int col = (e.X - calLeft) / dayW;
                        int row = (e.Y - startY) / dayH;

                        int firstDayOfWeek = (int)_viewMonth.DayOfWeek;
                        int dayIndex = (row * 7) + col - firstDayOfWeek + 1;
                        int daysInMonth = DateTime.DaysInMonth(_viewMonth.Year, _viewMonth.Month);

                        if (dayIndex >= 1 && dayIndex <= daysInMonth)
                        {
                            var d = new DateTime(_viewMonth.Year, _viewMonth.Month, dayIndex);
                            if (_hoverDate != d)
                            {
                                _hoverDate = d;
                                Invalidate();
                            }
                        }
                    }
                }

                if (_hoveredPreset != hov)
                {
                    _hoveredPreset = hov;
                    Invalidate();
                }
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _hoveredPreset = -1;
                Invalidate();
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);

                // Check Presets
                string[] presets = GetCurrentPresetNames();
                for (int i = 0; i < presets.Length && i < _presetRects.Length; i++)
                {
                    if (_presetRects[i].Contains(e.Location))
                    {
                        ApplyPresetByIndex(i);
                        return;
                    }
                }

                // Navigation Steppers
                if (_picker.ViewMode == DateRangeViewMode.Year)
                {
                    if (_prevYearRect.Contains(e.Location))
                    {
                        _viewMonth = _viewMonth.AddYears(-10);
                        Invalidate();
                        return;
                    }
                    if (_nextYearRect.Contains(e.Location))
                    {
                        _viewMonth = _viewMonth.AddYears(10);
                        Invalidate();
                        return;
                    }
                }
                else if (_picker.ViewMode == DateRangeViewMode.Month)
                {
                    if (_prevYearRect.Contains(e.Location))
                    {
                        _viewMonth = _viewMonth.AddYears(-1);
                        Invalidate();
                        return;
                    }
                    if (_nextYearRect.Contains(e.Location))
                    {
                        _viewMonth = _viewMonth.AddYears(1);
                        Invalidate();
                        return;
                    }
                }
                else
                {
                    if (_prevYearRect.Contains(e.Location))
                    {
                        _viewMonth = _viewMonth.AddYears(-1);
                        Invalidate();
                        return;
                    }
                    if (_prevMonthRect.Contains(e.Location))
                    {
                        _viewMonth = _viewMonth.AddMonths(-1);
                        Invalidate();
                        return;
                    }
                    if (_nextMonthRect.Contains(e.Location))
                    {
                        _viewMonth = _viewMonth.AddMonths(1);
                        Invalidate();
                        return;
                    }
                    if (_nextYearRect.Contains(e.Location))
                    {
                        _viewMonth = _viewMonth.AddYears(1);
                        Invalidate();
                        return;
                    }
                }

                // Apply Button
                var applyRect = new Rectangle(Width - 85, Height - 34, 75, 26);
                if (applyRect.Contains(e.Location))
                {
                    _picker.SetRange(_tempStart, _tempEnd);
                    _picker.ClosePopup();
                    return;
                }

                int calLeft = 140;
                int startY = 60;
                int calW = Width - calLeft - 20;

                if (_picker.ViewMode == DateRangeViewMode.Month)
                {
                    int colW = calW / 4;
                    int rowH = 50;
                    startY = 44;

                    if (e.X >= calLeft && e.X < calLeft + (4 * colW) && e.Y >= startY && e.Y < startY + (3 * rowH))
                    {
                        int col = (e.X - calLeft) / colW;
                        int row = (e.Y - startY) / rowH;
                        int mIdx = (row * 4) + col;
                        if (mIdx >= 0 && mIdx < 12)
                        {
                            int targetMonth = mIdx + 1;
                            DateTime mStart = new DateTime(_viewMonth.Year, targetMonth, 1);
                            DateTime mEnd = new DateTime(_viewMonth.Year, targetMonth, DateTime.DaysInMonth(_viewMonth.Year, targetMonth));

                            if (_clickStep == 0)
                            {
                                _tempStart = mStart;
                                _tempEnd = mEnd;
                                _clickStep = 1;
                            }
                            else
                            {
                                if (mStart < _tempStart)
                                {
                                    _tempEnd = new DateTime(_tempStart.Year, _tempStart.Month, DateTime.DaysInMonth(_tempStart.Year, _tempStart.Month));
                                    _tempStart = mStart;
                                }
                                else
                                {
                                    _tempEnd = mEnd;
                                }
                                _clickStep = 0;
                            }
                            Invalidate();
                        }
                    }
                }
                else if (_picker.ViewMode == DateRangeViewMode.Year)
                {
                    int colW = calW / 4;
                    int rowH = 50;
                    startY = 44;

                    if (e.X >= calLeft && e.X < calLeft + (4 * colW) && e.Y >= startY && e.Y < startY + (3 * rowH))
                    {
                        int col = (e.X - calLeft) / colW;
                        int row = (e.Y - startY) / rowH;
                        int yIdx = (row * 4) + col;
                        if (yIdx >= 0 && yIdx < 12)
                        {
                            int startDecade = (_viewMonth.Year / 10) * 10;
                            int yr = (startDecade - 1) + yIdx;
                            if (yr >= 1 && yr <= 9999)
                            {
                                DateTime yStart = new DateTime(yr, 1, 1);
                                DateTime yEnd = new DateTime(yr, 12, 31);

                                if (_clickStep == 0)
                                {
                                    _tempStart = yStart;
                                    _tempEnd = yEnd;
                                    _clickStep = 1;
                                }
                                else
                                {
                                    if (yStart < _tempStart)
                                    {
                                        _tempEnd = new DateTime(_tempStart.Year, 12, 31);
                                        _tempStart = yStart;
                                    }
                                    else
                                    {
                                        _tempEnd = yEnd;
                                    }
                                    _clickStep = 0;
                                }
                                Invalidate();
                            }
                        }
                    }
                }
                else
                {
                    int dayW = calW / 7;
                    int dayH = 26;

                    if (e.X >= calLeft && e.X < Width - 20 && e.Y >= startY && e.Y < startY + (6 * dayH))
                    {
                        int col = (e.X - calLeft) / dayW;
                        int row = (e.Y - startY) / dayH;

                        int firstDayOfWeek = (int)_viewMonth.DayOfWeek;
                        int dayIndex = (row * 7) + col - firstDayOfWeek + 1;
                        int daysInMonth = DateTime.DaysInMonth(_viewMonth.Year, _viewMonth.Month);

                        if (dayIndex >= 1 && dayIndex <= daysInMonth)
                        {
                            DateTime clickedDate = new DateTime(_viewMonth.Year, _viewMonth.Month, dayIndex);
                            if (_clickStep == 0)
                            {
                                _tempStart = clickedDate;
                                _tempEnd = clickedDate;
                                _clickStep = 1;
                            }
                            else
                            {
                                if (clickedDate < _tempStart)
                                {
                                    _tempEnd = _tempStart;
                                    _tempStart = clickedDate;
                                }
                                else
                                {
                                    _tempEnd = clickedDate;
                                }
                                _clickStep = 0;
                            }
                            Invalidate();
                        }
                    }
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var palette = _picker.CurrentPalette;
                g.Clear(palette.CardBackground);

                using (var penBorder = new Pen(palette.Border, 1f))
                {
                    g.DrawRectangle(penBorder, 0, 0, Width - 1, Height - 1);
                }

                // 1. Left Preset Sidebar (Width = 135)
                int sideW = 135;
                using (var brushSide = new SolidBrush(palette.Surface))
                {
                    g.FillRectangle(brushSide, new Rectangle(0, 0, sideW, Height));
                }
                using (var penDiv = new Pen(palette.Border, 1f))
                {
                    g.DrawLine(penDiv, sideW, 0, sideW, Height);
                }

                string[] presets = GetCurrentPresetNames();
                using var fontPreset = new Font(Font.FontFamily, 8.5f, FontStyle.Regular);
                for (int i = 0; i < presets.Length; i++)
                {
                    int py = 10 + (i * 34);
                    _presetRects[i] = new Rectangle(8, py, sideW - 16, 28);

                    bool isHov = i == _hoveredPreset;
                    bool isCur = (_picker.ViewMode == DateRangeViewMode.Day && i < _dayPresetValues.Length && _picker.Preset == _dayPresetValues[i]);

                    if (isCur)
                    {
                        using var brushCur = new SolidBrush(Color.FromArgb(40, palette.Primary));
                        using var pathCur = CreateRoundedRect(_presetRects[i], 5);
                        g.FillPath(brushCur, pathCur);

                        // Active left pill accent
                        using var penLeft = new SolidBrush(palette.Primary);
                        g.FillRectangle(penLeft, new Rectangle(_presetRects[i].X, _presetRects[i].Y + 4, 3, _presetRects[i].Height - 8));
                    }
                    else if (isHov)
                    {
                        using var brushHov = new SolidBrush(Color.FromArgb(20, palette.Primary));
                        using var pathHov = CreateRoundedRect(_presetRects[i], 5);
                        g.FillPath(brushHov, pathHov);
                    }

                    Color textColor = isCur ? palette.Primary : (isHov ? palette.TextPrimary : palette.TextSecondary);
                    using var brushText = new SolidBrush(textColor);
                    var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                    g.DrawString(presets[i], fontPreset, brushText, new Rectangle(_presetRects[i].X + 10, _presetRects[i].Y, _presetRects[i].Width - 10, _presetRects[i].Height), sf);
                }

                // 2. Right Calendar Area
                int calLeft = sideW + 14;
                int calW = Width - calLeft - 14;
                int btnSz = 22;

                if (_picker.ViewMode == DateRangeViewMode.Month)
                {
                    // Month Mode Header
                    _prevYearRect = new Rectangle(calLeft, 10, btnSz, btnSz);
                    _nextYearRect = new Rectangle(Width - 26, 10, btnSz, btnSz);

                    using var fontTitle = new Font(Font.FontFamily, 9.5f, FontStyle.Bold);
                    using var brushTitle = new SolidBrush(palette.TextPrimary);
                    string yearText = _viewMonth.Year.ToString();
                    var titleRect = new Rectangle(calLeft + 30, 10, calW - 60, 22);
                    var sfTitle = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(yearText, fontTitle, brushTitle, titleRect, sfTitle);

                    using (var fontNav = new Font("Segoe UI", 9f, FontStyle.Bold))
                    using (var brushNav = new SolidBrush(palette.TextSecondary))
                    {
                        var sfNav = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("«", fontNav, brushNav, _prevYearRect, sfNav);
                        g.DrawString("»", fontNav, brushNav, _nextYearRect, sfNav);
                    }

                    // Month Grid (3 rows x 4 cols)
                    int startY = 44;
                    int colW = calW / 4;
                    int rowH = 50;

                    DateTime rangeStart = _tempStart;
                    DateTime rangeEnd = (_clickStep == 1 && _hoverDate >= _tempStart) ? _hoverDate : _tempEnd;

                    using var fontMonth = new Font(Font.FontFamily, 9f, FontStyle.Regular);
                    using var fontMonthBold = new Font(Font.FontFamily, 9f, FontStyle.Bold);

                    for (int m = 0; m < 12; m++)
                    {
                        int r = m / 4;
                        int c = m % 4;
                        var cellRect = new Rectangle(calLeft + (c * colW), startY + (r * rowH), colW, rowH);

                        DateTime cellStart = new DateTime(_viewMonth.Year, m + 1, 1);
                        DateTime cellEnd = new DateTime(_viewMonth.Year, m + 1, DateTime.DaysInMonth(_viewMonth.Year, m + 1));

                        bool isStart = cellStart.Year == rangeStart.Year && cellStart.Month == rangeStart.Month;
                        bool isEnd = cellStart.Year == rangeEnd.Year && cellStart.Month == rangeEnd.Month;
                        bool inRange = cellStart > rangeStart && cellEnd < rangeEnd;

                        // Range Ribbon
                        if (inRange)
                        {
                            using var brushRange = new SolidBrush(Color.FromArgb(35, palette.Primary));
                            g.FillRectangle(brushRange, new Rectangle(cellRect.X, cellRect.Y + 6, colW, rowH - 12));
                        }

                        if (isStart)
                        {
                            if (rangeEnd > rangeStart)
                            {
                                using var brushHalf = new SolidBrush(Color.FromArgb(35, palette.Primary));
                                g.FillRectangle(brushHalf, new Rectangle(cellRect.X + (colW / 2), cellRect.Y + 6, colW / 2, rowH - 12));
                            }
                            using var brushEndpoint = new SolidBrush(palette.Primary);
                            using var pathEp = CreateRoundedRect(new Rectangle(cellRect.X + 4, cellRect.Y + 6, colW - 8, rowH - 12), 6);
                            g.FillPath(brushEndpoint, pathEp);
                        }

                        if (isEnd && !isStart)
                        {
                            using var brushHalf = new SolidBrush(Color.FromArgb(35, palette.Primary));
                            g.FillRectangle(brushHalf, new Rectangle(cellRect.X, cellRect.Y + 6, colW / 2, rowH - 12));

                            using var brushEndpoint = new SolidBrush(palette.Primary);
                            using var pathEp = CreateRoundedRect(new Rectangle(cellRect.X + 4, cellRect.Y + 6, colW - 8, rowH - 12), 6);
                            g.FillPath(brushEndpoint, pathEp);
                        }

                        Color textColor = (isStart || isEnd) ? Color.White : palette.TextPrimary;
                        using var brushMonth = new SolidBrush(textColor);
                        var activeFont = (isStart || isEnd) ? fontMonthBold : fontMonth;
                        var sfMonth = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(_monthNames[m], activeFont, brushMonth, cellRect, sfMonth);
                    }
                }
                else if (_picker.ViewMode == DateRangeViewMode.Year)
                {
                    // Year Mode Header
                    int startDecade = (_viewMonth.Year / 10) * 10;
                    _prevYearRect = new Rectangle(calLeft, 10, btnSz, btnSz);
                    _nextYearRect = new Rectangle(Width - 26, 10, btnSz, btnSz);

                    using var fontTitle = new Font(Font.FontFamily, 9.5f, FontStyle.Bold);
                    using var brushTitle = new SolidBrush(palette.TextPrimary);
                    string decadeText = $"{startDecade} - {startDecade + 9}";
                    var titleRect = new Rectangle(calLeft + 30, 10, calW - 60, 22);
                    var sfTitle = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(decadeText, fontTitle, brushTitle, titleRect, sfTitle);

                    using (var fontNav = new Font("Segoe UI", 9f, FontStyle.Bold))
                    using (var brushNav = new SolidBrush(palette.TextSecondary))
                    {
                        var sfNav = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("«", fontNav, brushNav, _prevYearRect, sfNav);
                        g.DrawString("»", fontNav, brushNav, _nextYearRect, sfNav);
                    }

                    // Year Grid (3 rows x 4 cols)
                    int startY = 44;
                    int colW = calW / 4;
                    int rowH = 50;

                    int curStartYear = _tempStart.Year;
                    int curEndYear = (_clickStep == 1 && _hoverDate.Year >= _tempStart.Year) ? _hoverDate.Year : _tempEnd.Year;

                    using var fontYear = new Font(Font.FontFamily, 9f, FontStyle.Regular);
                    using var fontYearBold = new Font(Font.FontFamily, 9f, FontStyle.Bold);

                    for (int i = 0; i < 12; i++)
                    {
                        int yr = (startDecade - 1) + i;
                        int r = i / 4;
                        int c = i % 4;
                        var cellRect = new Rectangle(calLeft + (c * colW), startY + (r * rowH), colW, rowH);

                        bool isStart = yr == curStartYear;
                        bool isEnd = yr == curEndYear;
                        bool inRange = yr > curStartYear && yr < curEndYear;
                        bool isOutside = (yr < startDecade || yr > startDecade + 9);

                        // Range Ribbon
                        if (inRange)
                        {
                            using var brushRange = new SolidBrush(Color.FromArgb(35, palette.Primary));
                            g.FillRectangle(brushRange, new Rectangle(cellRect.X, cellRect.Y + 6, colW, rowH - 12));
                        }

                        if (isStart)
                        {
                            if (curEndYear > curStartYear)
                            {
                                using var brushHalf = new SolidBrush(Color.FromArgb(35, palette.Primary));
                                g.FillRectangle(brushHalf, new Rectangle(cellRect.X + (colW / 2), cellRect.Y + 6, colW / 2, rowH - 12));
                            }
                            using var brushEndpoint = new SolidBrush(palette.Primary);
                            using var pathEp = CreateRoundedRect(new Rectangle(cellRect.X + 4, cellRect.Y + 6, colW - 8, rowH - 12), 6);
                            g.FillPath(brushEndpoint, pathEp);
                        }

                        if (isEnd && !isStart)
                        {
                            using var brushHalf = new SolidBrush(Color.FromArgb(35, palette.Primary));
                            g.FillRectangle(brushHalf, new Rectangle(cellRect.X, cellRect.Y + 6, colW / 2, rowH - 12));

                            using var brushEndpoint = new SolidBrush(palette.Primary);
                            using var pathEp = CreateRoundedRect(new Rectangle(cellRect.X + 4, cellRect.Y + 6, colW - 8, rowH - 12), 6);
                            g.FillPath(brushEndpoint, pathEp);
                        }

                        Color textColor = (isStart || isEnd) ? Color.White : (isOutside ? Color.FromArgb(120, palette.TextSecondary) : palette.TextPrimary);
                        using var brushYr = new SolidBrush(textColor);
                        var activeFont = (isStart || isEnd) ? fontYearBold : fontYear;
                        var sfYr = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(yr.ToString(), activeFont, brushYr, cellRect, sfYr);
                    }
                }
                else
                {
                    // Day Mode Header
                    _prevYearRect = new Rectangle(calLeft, 10, btnSz, btnSz);
                    _prevMonthRect = new Rectangle(calLeft + 24, 10, btnSz, btnSz);
                    _nextMonthRect = new Rectangle(Width - 50, 10, btnSz, btnSz);
                    _nextYearRect = new Rectangle(Width - 26, 10, btnSz, btnSz);

                    // Title: "MMMM yyyy"
                    using var fontTitle = new Font(Font.FontFamily, 9.5f, FontStyle.Bold);
                    using var brushTitle = new SolidBrush(palette.TextPrimary);
                    string monthName = _viewMonth.ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
                    var titleRect = new Rectangle(calLeft + 48, 10, calW - 96, 22);
                    var sfTitle = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(monthName, fontTitle, brushTitle, titleRect, sfTitle);

                    // Navigation Glyph Arrows («, ‹, ›, »)
                    using (var fontNav = new Font("Segoe UI", 9f, FontStyle.Bold))
                    using (var brushNav = new SolidBrush(palette.TextSecondary))
                    {
                        var sfNav = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("«", fontNav, brushNav, _prevYearRect, sfNav);
                        g.DrawString("‹", fontNav, brushNav, _prevMonthRect, sfNav);
                        g.DrawString("›", fontNav, brushNav, _nextMonthRect, sfNav);
                        g.DrawString("»", fontNav, brushNav, _nextYearRect, sfNav);
                    }

                    // Day-of-week headers
                    string[] dayHeaders = new[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
                    int dayW = calW / 7;
                    int dayH = 26;
                    int startY = 38;

                    using var fontHeader = new Font(Font.FontFamily, 7.75f, FontStyle.Bold);
                    for (int c = 0; c < 7; c++)
                    {
                        var cellRect = new Rectangle(calLeft + (c * dayW), startY, dayW, 18);
                        Color cColor = (c == 0 || c == 6) ? palette.Warning : palette.TextSecondary;
                        using var brushHeader = new SolidBrush(cColor);
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(dayHeaders[c], fontHeader, brushHeader, cellRect, sf);
                    }

                    // Render Calendar Days with Connected Ribbon
                    int firstDayOfWeek = (int)_viewMonth.DayOfWeek;
                    int daysInMonth = DateTime.DaysInMonth(_viewMonth.Year, _viewMonth.Month);
                    int gridY = startY + 20;

                    using var fontDay = new Font(Font.FontFamily, 8.5f, FontStyle.Regular);
                    using var fontDayBold = new Font(Font.FontFamily, 8.5f, FontStyle.Bold);

                    DateTime rangeStart = _tempStart;
                    DateTime rangeEnd = (_clickStep == 1 && _hoverDate >= _tempStart) ? _hoverDate : _tempEnd;

                    for (int d = 1; d <= daysInMonth; d++)
                    {
                        int cellIdx = firstDayOfWeek + d - 1;
                        int r = cellIdx / 7;
                        int c = cellIdx % 7;

                        DateTime curDate = new DateTime(_viewMonth.Year, _viewMonth.Month, d);
                        var cellRect = new Rectangle(calLeft + (c * dayW), gridY + (r * dayH), dayW, dayH);

                        bool isStart = curDate == rangeStart;
                        bool isEnd = curDate == rangeEnd;
                        bool inRange = curDate > rangeStart && curDate < rangeEnd;

                        // Connected Range Ribbon (Continuous soft highlight)
                        if (inRange)
                        {
                            using var brushRange = new SolidBrush(Color.FromArgb(35, palette.Primary));
                            g.FillRectangle(brushRange, new Rectangle(cellRect.X, cellRect.Y + 2, dayW, dayH - 4));
                        }

                        // Rounded Capsule on Start Date
                        if (isStart)
                        {
                            if (rangeEnd > rangeStart)
                            {
                                using var brushHalf = new SolidBrush(Color.FromArgb(35, palette.Primary));
                                g.FillRectangle(brushHalf, new Rectangle(cellRect.X + (dayW / 2), cellRect.Y + 2, dayW / 2, dayH - 4));
                            }
                            using var brushEndpoint = new SolidBrush(palette.Primary);
                            using var pathEp = CreateRoundedRect(new Rectangle(cellRect.X + 2, cellRect.Y + 2, dayW - 4, dayH - 4), 5);
                            g.FillPath(brushEndpoint, pathEp);
                        }

                        // Rounded Capsule on End Date
                        if (isEnd && !isStart)
                        {
                            using var brushHalf = new SolidBrush(Color.FromArgb(35, palette.Primary));
                            g.FillRectangle(brushHalf, new Rectangle(cellRect.X, cellRect.Y + 2, dayW / 2, dayH - 4));

                            using var brushEndpoint = new SolidBrush(palette.Primary);
                            using var pathEp = CreateRoundedRect(new Rectangle(cellRect.X + 2, cellRect.Y + 2, dayW - 4, dayH - 4), 5);
                            g.FillPath(brushEndpoint, pathEp);
                        }

                        Color dayColor = (isStart || isEnd) ? Color.White : palette.TextPrimary;
                        using var brushDay = new SolidBrush(dayColor);
                        var activeFont = (isStart || isEnd) ? fontDayBold : fontDay;
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(d.ToString(), activeFont, brushDay, cellRect, sf);
                    }
                }

                // 3. Bottom Action Bar (Apply button)
                var applyRect = new Rectangle(Width - 85, Height - 32, 75, 24);
                using (var brushApply = new SolidBrush(palette.Primary))
                using (var pathApply = CreateRoundedRect(applyRect, 4))
                {
                    g.FillPath(brushApply, pathApply);
                }
                using (var brushApplyText = new SolidBrush(Color.White))
                using (var fontApply = new Font(Font.FontFamily, 8f, FontStyle.Bold))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("Apply", fontApply, brushApplyText, applyRect, sf);
                }
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZDateRangePicker"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("DateRangePicker is deprecated and will be removed in 5 release cycles. Please migrate to ZDateRangePicker instead.")]
    [ToolboxItem(false)]
    public class DateRangePicker : ZDateRangePicker { }

    /// <summary>
    /// Legacy alias for <see cref="ZDateRangePicker"/>.
    /// </summary>
    [Obsolete("ZeroDateRangePicker is deprecated. Use ZDateRangePicker instead.")]
    [ToolboxItem(false)]
    public class ZeroDateRangePicker : ZDateRangePicker { }

    #endregion
}
