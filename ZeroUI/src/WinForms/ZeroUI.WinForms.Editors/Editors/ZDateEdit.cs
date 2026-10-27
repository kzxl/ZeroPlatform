using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Modern, anti-aliased single date picker control with 100% custom-drawn calendar popup,
    /// quick preset pills, year/month navigation, and native Obsidian Dark / Clean Light theming.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroDatePicker.bmp")]
    [Category("ZeroUI - Editors")]
    [DefaultProperty("Value")]
    [DefaultEvent("ValueChanged")]
    [Description("Modern date picker with custom-drawn popup calendar and quick-select presets")]
    [Designer("ZeroUI.WinForms.Design.Editors.ZDateEditDesigner, ZeroUI.WinForms.Design")]
    public class ZDateEdit : ControlBase, IZeroEditor
    {
        private DateTime _selectedDate = DateTime.Today;
        private string _dateFormat = "yyyy-MM-dd";
        private bool _showPresets = true;
        private bool _isHovered = false;
        private bool _isFocused = false;

        private DropDownHost? _popup;
        private ZeroCalendarPopupControl? _calendarControl;
        private Rectangle _chevronRect;
        private readonly TextBox _innerBox;

        public event EventHandler? ValueChanged;
        public event EventHandler? EditValueChanged;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object? EditValue
        {
            get => Value;
            set
            {
                if (value == null || value == DBNull.Value)
                {
                    Value = DateTime.Today;
                }
                else if (value is DateTime dt)
                {
                    Value = dt;
                }
                else if (DateTime.TryParse(value.ToString(), out DateTime parsed))
                {
                    Value = parsed;
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool IsModified { get; set; } = false;

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool ReadOnly
        {
            get => _innerBox.ReadOnly;
            set
            {
                _innerBox.ReadOnly = value;
                Invalidate();
            }
        }

        public void Reset()
        {
            Value = DateTime.Today;
            IsModified = false;
        }

        public void Clear() => Reset();

        public ZDateEdit()
        {
            Size = new Size(160, 36);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            Cursor = Cursors.Default;

            _innerBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = new Font(Font.FontFamily, 9.25f, FontStyle.Bold),
                BackColor = CurrentPalette.Surface,
                ForeColor = CurrentPalette.TextPrimary,
                TextAlign = HorizontalAlignment.Left
            };
            _innerBox.Text = _selectedDate.ToString(_dateFormat);
            _innerBox.KeyDown += OnInnerBoxKeyDown;
            _innerBox.LostFocus += (s, e) =>
            {
                _isFocused = false;
                ParseAndApplyText();
                Invalidate();
            };
            _innerBox.GotFocus += (s, e) =>
            {
                _isFocused = true;
                Invalidate();
            };
            Controls.Add(_innerBox);

            ZeroUIConfig.CornerStyleChanged += (s, e) => Invalidate();
            ZeroUIConfig.FontChanged += (s, e) =>
            {
                Font = ZeroUIConfig.DefaultFont;
                _innerBox.Font = new Font(Font.FontFamily, 9.25f, FontStyle.Bold);
                Invalidate();
            };
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            var palette = CurrentPalette;
            if (_innerBox != null)
            {
                _innerBox.BackColor = palette.Surface;
                _innerBox.ForeColor = palette.TextPrimary;
            }
            _calendarControl?.Invalidate();
            Invalidate();
        }

        private void ParseAndApplyText()
        {
            string txt = _innerBox.Text.Trim();
            if (DateTime.TryParseExact(txt, _dateFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed) ||
                DateTime.TryParse(txt, out parsed))
            {
                Value = parsed.Date;
            }
            else
            {
                _innerBox.Text = _selectedDate.ToString(_dateFormat);
            }
        }

        private void OnInnerBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ParseAndApplyText();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Down && (e.Alt || e.Modifiers == Keys.None))
            {
                ShowCalendarPopup();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                _innerBox.Text = _selectedDate.ToString(_dateFormat);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        [Category("Data")]
        public DateTime Value
        {
            get => _selectedDate;
            set
            {
                var val = value.Date;
                if (_selectedDate != val)
                {
                    _selectedDate = val;
                    IsModified = true;
                    if (_innerBox != null && _innerBox.Text != val.ToString(_dateFormat))
                    {
                        _innerBox.Text = val.ToString(_dateFormat);
                    }
                    Invalidate();
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
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
                _dateFormat = value ?? "yyyy-MM-dd";
                if (_innerBox != null) _innerBox.Text = _selectedDate.ToString(_dateFormat);
                Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool ShowPresets
        {
            get => _showPresets;
            set
            {
                _showPresets = value;
                Invalidate();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _chevronRect = new Rectangle(Width - 24, (Height - 14) / 2, 14, 14);

            if (_innerBox != null)
            {
                int boxW = Math.Max(20, Width - 56);
                int boxH = _innerBox.PreferredHeight;
                _innerBox.SetBounds(28, (Height - boxH) / 2, boxW, boxH);
            }
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
            if (e.X < 28 || e.X >= Width - 26)
            {
                ShowCalendarPopup();
            }
            else
            {
                _innerBox.Focus();
            }
        }

        private void ShowCalendarPopup()
        {
            if (ReadOnly || !Enabled) return;

            if (_popup != null && _popup.Visible)
            {
                _popup.Close();
                return;
            }

            _calendarControl = new ZeroCalendarPopupControl(this, _selectedDate, _showPresets);
            _popup = new DropDownHost
            {
                Content = _calendarControl
            };

            _popup.Closed += (s, e) =>
            {
                _isFocused = false;
                Invalidate();
            };

            _isFocused = true;
            Invalidate();
            _popup.ShowDropDown(this, _calendarControl.Width, _calendarControl.Height);
        }

        internal void OnDateSelectedFromPopup(DateTime date)
        {
            Value = date;
            _popup?.Close();
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

            // 2. Box Background & Border
            using (var path = ZeroUIConfig.CreateRoundedRectangle(rect, effRadius))
            {
                using var bgBrush = new SolidBrush(palette.Surface);
                g.FillPath(bgBrush, path);

                Color borderColor = _isFocused ? palette.Primary : (_isHovered ? palette.PrimaryHover : palette.Border);
                using var pen = new Pen(borderColor, _isFocused ? 1.5f : 1f);
                g.DrawPath(pen, path);
            }

            // 3. Calendar Glyph Icon (📅)
            using (var iconFont = new Font("Segoe UI Emoji", 9.5f))
            using (var brushIcon = new SolidBrush(palette.Primary))
            {
                g.DrawString("📅", iconFont, brushIcon, 7, (Height - 18) / 2);
            }

            // 4. Dropdown Chevron (▼)
            using (var chevBrush = new SolidBrush(_isFocused ? palette.Primary : palette.TextSecondary))
            {
                PointF center = new PointF(_chevronRect.X + (_chevronRect.Width / 2f), _chevronRect.Y + (_chevronRect.Height / 2f));
                PointF[] pts;
                if (_isFocused)
                {
                    // Up arrow (▲) when popup is open
                    pts = new[]
                    {
                        new PointF(center.X - 3.5f, center.Y + 2f),
                        new PointF(center.X + 3.5f, center.Y + 2f),
                        new PointF(center.X, center.Y - 2.5f)
                    };
                }
                else
                {
                    // Down arrow (▼)
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

        private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius) =>
            ZeroUIConfig.CreateRoundedRectangle(rect, radius);

        private enum CalendarViewMode
        {
            Days,
            Months,
            Years
        }

        /// <summary>
        /// 100% custom-drawn calendar popup control for ZeroDatePicker.
        /// Features multi-tier zoom navigation (Days <-> Months <-> Years),
        /// year/month steppers, interactive day grid, quick presets, today highlight, and dark/light theming.
        /// </summary>
        private sealed class ZeroCalendarPopupControl : Control
        {
            private readonly ZDateEdit _owner;
            private DateTime _viewMonth;
            private DateTime _selectedDate;
            private readonly bool _showPresets;

            private CalendarViewMode _viewMode = CalendarViewMode.Days;
            private bool _isHeaderHovered = false;

            private Rectangle _prevYearRect;
            private Rectangle _prevMonthRect;
            private Rectangle _nextMonthRect;
            private Rectangle _nextYearRect;
            private Rectangle _monthTitleRect;
            private Rectangle _todayLinkRect;

            private Rectangle[] _presetRects = new Rectangle[4];
            private readonly string[] _presetNames = new[] { "Today", "Yesterday", "Tomorrow", "+7 Days" };
            private int _hoveredPreset = -1;

            private int _hoveredDayIndex = -1; // 0..41
            private DateTime[] _gridDates = new DateTime[42];

            private int _hoveredMonthIndex = -1; // 0..11
            private int _hoveredYearIndex = -1;  // 0..11

            private readonly string[] _monthNames = new[]
            {
                "Jan", "Feb", "Mar", "Apr",
                "May", "Jun", "Jul", "Aug",
                "Sep", "Oct", "Nov", "Dec"
            };

            public ZeroCalendarPopupControl(ZDateEdit owner, DateTime initialDate, bool showPresets)
            {
                _owner = owner;
                _selectedDate = initialDate.Date;
                _viewMonth = new DateTime(initialDate.Year, initialDate.Month, 1);
                _showPresets = showPresets;

                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.Selectable, true);

                int w = 270;
                int h = _showPresets ? 316 : 280;
                Size = new Size(w, h);
                Font = new Font("Segoe UI", 9f);
                BackColor = ZeroTheme.Colors.CardBackground;

                BuildGridDates();
            }

            private void BuildGridDates()
            {
                int firstDayOfWeek = (int)_viewMonth.DayOfWeek; // 0 = Sunday
                DateTime startDate = _viewMonth.AddDays(-firstDayOfWeek);

                for (int i = 0; i < 42; i++)
                {
                    _gridDates[i] = startDate.AddDays(i);
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);

                int hovPreset = -1;
                if (_viewMode == CalendarViewMode.Days && _showPresets)
                {
                    for (int i = 0; i < _presetRects.Length; i++)
                    {
                        if (_presetRects[i].Contains(e.Location))
                        {
                            hovPreset = i;
                            break;
                        }
                    }
                }

                int hovDay = -1;
                int hovMonth = -1;
                int hovYear = -1;

                if (_viewMode == CalendarViewMode.Days)
                {
                    int gridTop = _showPresets ? 74 : 38;
                    int cellW = (Width - 16) / 7;
                    int cellH = 28;

                    if (e.X >= 8 && e.X < Width - 8 && e.Y >= gridTop && e.Y < gridTop + (6 * cellH))
                    {
                        int col = (e.X - 8) / cellW;
                        int row = (e.Y - gridTop) / cellH;
                        if (col >= 0 && col < 7 && row >= 0 && row < 6)
                        {
                            hovDay = (row * 7) + col;
                        }
                    }
                }
                else
                {
                    int gridTop = 40;
                    int gridAvailH = (Height - 32) - gridTop;
                    int rowH = gridAvailH / 3;
                    int colW = (Width - 20) / 4;

                    if (e.X >= 10 && e.X < Width - 10 && e.Y >= gridTop && e.Y < gridTop + (3 * rowH))
                    {
                        int col = (e.X - 10) / colW;
                        int row = (e.Y - gridTop) / rowH;
                        if (col >= 0 && col < 4 && row >= 0 && row < 3)
                        {
                            int idx = (row * 4) + col;
                            if (_viewMode == CalendarViewMode.Months) hovMonth = idx;
                            else if (_viewMode == CalendarViewMode.Years) hovYear = idx;
                        }
                    }
                }

                bool hovHeader = _monthTitleRect.Contains(e.Location) && _viewMode != CalendarViewMode.Years;
                bool onNav = _prevYearRect.Contains(e.Location) || _prevMonthRect.Contains(e.Location) ||
                             _nextMonthRect.Contains(e.Location) || _nextYearRect.Contains(e.Location) ||
                             _todayLinkRect.Contains(e.Location);

                Cursor = (hovPreset >= 0 || hovDay >= 0 || hovMonth >= 0 || hovYear >= 0 || onNav || hovHeader)
                    ? Cursors.Hand
                    : Cursors.Default;

                if (_hoveredPreset != hovPreset || _hoveredDayIndex != hovDay ||
                    _hoveredMonthIndex != hovMonth || _hoveredYearIndex != hovYear ||
                    _isHeaderHovered != hovHeader)
                {
                    _hoveredPreset = hovPreset;
                    _hoveredDayIndex = hovDay;
                    _hoveredMonthIndex = hovMonth;
                    _hoveredYearIndex = hovYear;
                    _isHeaderHovered = hovHeader;
                    Invalidate();
                }
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _hoveredPreset = -1;
                _hoveredDayIndex = -1;
                _hoveredMonthIndex = -1;
                _hoveredYearIndex = -1;
                _isHeaderHovered = false;
                Cursor = Cursors.Default;
                Invalidate();
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);

                // 1. Presets (Days mode only)
                if (_viewMode == CalendarViewMode.Days && _showPresets)
                {
                    for (int i = 0; i < _presetRects.Length; i++)
                    {
                        if (_presetRects[i].Contains(e.Location))
                        {
                            DateTime target = i switch
                            {
                                0 => DateTime.Today,
                                1 => DateTime.Today.AddDays(-1),
                                2 => DateTime.Today.AddDays(1),
                                _ => DateTime.Today.AddDays(7)
                            };
                            _owner.OnDateSelectedFromPopup(target);
                            return;
                        }
                    }
                }

                // 2. Header Title Click (Zoom out)
                if (_monthTitleRect.Contains(e.Location))
                {
                    if (_viewMode == CalendarViewMode.Days)
                    {
                        _viewMode = CalendarViewMode.Months;
                        Invalidate();
                        return;
                    }
                    if (_viewMode == CalendarViewMode.Months)
                    {
                        _viewMode = CalendarViewMode.Years;
                        Invalidate();
                        return;
                    }
                }

                // 3. Navigation Buttons
                if (_viewMode == CalendarViewMode.Days)
                {
                    if (_prevYearRect.Contains(e.Location))
                    {
                        _viewMonth = _viewMonth.AddYears(-1);
                        BuildGridDates();
                        Invalidate();
                        return;
                    }
                    if (_prevMonthRect.Contains(e.Location))
                    {
                        _viewMonth = _viewMonth.AddMonths(-1);
                        BuildGridDates();
                        Invalidate();
                        return;
                    }
                    if (_nextMonthRect.Contains(e.Location))
                    {
                        _viewMonth = _viewMonth.AddMonths(1);
                        BuildGridDates();
                        Invalidate();
                        return;
                    }
                    if (_nextYearRect.Contains(e.Location))
                    {
                        _viewMonth = _viewMonth.AddYears(1);
                        BuildGridDates();
                        Invalidate();
                        return;
                    }
                }
                else if (_viewMode == CalendarViewMode.Months)
                {
                    if (_prevMonthRect.Contains(e.Location) || _prevYearRect.Contains(e.Location))
                    {
                        _viewMonth = _viewMonth.AddYears(-1);
                        Invalidate();
                        return;
                    }
                    if (_nextMonthRect.Contains(e.Location) || _nextYearRect.Contains(e.Location))
                    {
                        _viewMonth = _viewMonth.AddYears(1);
                        Invalidate();
                        return;
                    }
                }
                else if (_viewMode == CalendarViewMode.Years)
                {
                    if (_prevMonthRect.Contains(e.Location) || _prevYearRect.Contains(e.Location))
                    {
                        _viewMonth = _viewMonth.AddYears(-10);
                        Invalidate();
                        return;
                    }
                    if (_nextMonthRect.Contains(e.Location) || _nextYearRect.Contains(e.Location))
                    {
                        _viewMonth = _viewMonth.AddYears(10);
                        Invalidate();
                        return;
                    }
                }

                // 4. Today Link Click
                if (_todayLinkRect.Contains(e.Location))
                {
                    _owner.OnDateSelectedFromPopup(DateTime.Today);
                    return;
                }

                // 5. Grid Selection Clicks
                if (_viewMode == CalendarViewMode.Days)
                {
                    if (_hoveredDayIndex >= 0 && _hoveredDayIndex < 42)
                    {
                        _owner.OnDateSelectedFromPopup(_gridDates[_hoveredDayIndex]);
                    }
                }
                else if (_viewMode == CalendarViewMode.Months)
                {
                    if (_hoveredMonthIndex >= 0 && _hoveredMonthIndex < 12)
                    {
                        int targetMonth = _hoveredMonthIndex + 1;
                        int maxDays = DateTime.DaysInMonth(_viewMonth.Year, targetMonth);
                        _viewMonth = new DateTime(_viewMonth.Year, targetMonth, Math.Min(_selectedDate.Day, maxDays));
                        _viewMode = CalendarViewMode.Days;
                        BuildGridDates();
                        Invalidate();
                    }
                }
                else if (_viewMode == CalendarViewMode.Years)
                {
                    if (_hoveredYearIndex >= 0 && _hoveredYearIndex < 12)
                    {
                        int startDecade = (_viewMonth.Year / 10) * 10;
                        int chosenYear = (startDecade - 1) + _hoveredYearIndex;
                        if (chosenYear >= 1 && chosenYear <= 9999)
                        {
                            int maxDays = DateTime.DaysInMonth(chosenYear, _viewMonth.Month);
                            _viewMonth = new DateTime(chosenYear, _viewMonth.Month, Math.Min(_selectedDate.Day, maxDays));
                            _viewMode = CalendarViewMode.Months;
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

                var palette = _owner.CurrentPalette;
                g.Clear(palette.CardBackground);

                // Border around popup
                using (var penBorder = new Pen(palette.Border, 1f))
                {
                    g.DrawRectangle(penBorder, 0, 0, Width - 1, Height - 1);
                }

                int curY = 6;

                // 1. Quick Presets Bar (Days mode only)
                if (_viewMode == CalendarViewMode.Days && _showPresets)
                {
                    int pW = (Width - 20) / 4;
                    using var fontPreset = new Font(Font.FontFamily, 8f, FontStyle.Bold);

                    for (int i = 0; i < 4; i++)
                    {
                        _presetRects[i] = new Rectangle(8 + (i * pW) + (i * 2), curY, pW - 2, 24);

                        bool isHov = i == _hoveredPreset;
                        Color pillBg = isHov ? Color.FromArgb(35, palette.Primary) : Color.FromArgb(20, palette.Border);

                        using var brushPill = new SolidBrush(pillBg);
                        using var pathPill = CreateRoundedRectangle(_presetRects[i], 4);
                        g.FillPath(brushPill, pathPill);

                        Color textColor = isHov ? palette.Primary : palette.TextSecondary;
                        using var brushText = new SolidBrush(textColor);
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(_presetNames[i], fontPreset, brushText, _presetRects[i], sf);
                    }
                    curY += 28;
                }

                // 2. Month & Year Navigation Header
                int navBtnSz = 22;
                _prevYearRect = new Rectangle(8, curY + 2, navBtnSz, navBtnSz);
                _prevMonthRect = new Rectangle(32, curY + 2, navBtnSz, navBtnSz);
                _nextMonthRect = new Rectangle(Width - 54, curY + 2, navBtnSz, navBtnSz);
                _nextYearRect = new Rectangle(Width - 30, curY + 2, navBtnSz, navBtnSz);
                _monthTitleRect = new Rectangle(56, curY, Width - 112, 26);

                int startDecade = (_viewMonth.Year / 10) * 10;
                string titleText = _viewMode switch
                {
                    CalendarViewMode.Months => $"{_viewMonth.Year} ▾",
                    CalendarViewMode.Years => $"{startDecade} - {startDecade + 9}",
                    _ => $"{_viewMonth.ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture)} ▾"
                };

                // Draw Header Title with hover pill effect
                if (_isHeaderHovered && _viewMode != CalendarViewMode.Years)
                {
                    using var brushHovHeader = new SolidBrush(Color.FromArgb(25, palette.Primary));
                    using var pathHovHeader = CreateRoundedRectangle(_monthTitleRect, 5);
                    g.FillPath(brushHovHeader, pathHovHeader);
                }

                using (var fontTitle = new Font(Font.FontFamily, 9.5f, FontStyle.Bold))
                using (var brushTitle = new SolidBrush((_isHeaderHovered && _viewMode != CalendarViewMode.Years) ? palette.Primary : palette.TextPrimary))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(titleText, fontTitle, brushTitle, _monthTitleRect, sf);
                }

                // Draw Arrow Nav Buttons («, ‹, ›, »)
                using (var fontNav = new Font("Segoe UI", 9f, FontStyle.Bold))
                using (var brushNav = new SolidBrush(palette.TextSecondary))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    if (_viewMode == CalendarViewMode.Days)
                    {
                        g.DrawString("«", fontNav, brushNav, _prevYearRect, sf);
                        g.DrawString("‹", fontNav, brushNav, _prevMonthRect, sf);
                        g.DrawString("›", fontNav, brushNav, _nextMonthRect, sf);
                        g.DrawString("»", fontNav, brushNav, _nextYearRect, sf);
                    }
                    else if (_viewMode == CalendarViewMode.Months)
                    {
                        g.DrawString("‹", fontNav, brushNav, _prevYearRect, sf);
                        g.DrawString("›", fontNav, brushNav, _nextYearRect, sf);
                    }
                    else if (_viewMode == CalendarViewMode.Years)
                    {
                        g.DrawString("«", fontNav, brushNav, _prevYearRect, sf);
                        g.DrawString("»", fontNav, brushNav, _nextYearRect, sf);
                    }
                }

                curY += 28;

                // 3. Grid content depending on ViewMode
                if (_viewMode == CalendarViewMode.Days)
                {
                    // Day of Week Column Headers
                    string[] dayHeaders = new[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
                    int cellW = (Width - 16) / 7;
                    using (var fontDOW = new Font(Font.FontFamily, 7.75f, FontStyle.Bold))
                    {
                        for (int c = 0; c < 7; c++)
                        {
                            var cellRect = new Rectangle(8 + (c * cellW), curY, cellW, 18);
                            Color cColor = (c == 0 || c == 6) ? palette.Warning : palette.TextSecondary;
                            using var brushDOW = new SolidBrush(cColor);
                            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            g.DrawString(dayHeaders[c], fontDOW, brushDOW, cellRect, sf);
                        }
                    }

                    curY += 20;

                    // Days Grid (42 cells: 6 rows x 7 cols)
                    int cellH = 26;
                    DateTime today = DateTime.Today;

                    using var fontDay = new Font(Font.FontFamily, 8.5f, FontStyle.Regular);
                    using var fontDayBold = new Font(Font.FontFamily, 8.5f, FontStyle.Bold);

                    for (int i = 0; i < 42; i++)
                    {
                        int r = i / 7;
                        int c = i % 7;
                        DateTime cellDate = _gridDates[i];
                        var cellRect = new Rectangle(8 + (c * cellW), curY + (r * cellH), cellW, cellH);

                        bool isCurrentMonth = cellDate.Month == _viewMonth.Month;
                        bool isSelected = cellDate == _selectedDate;
                        bool isToday = cellDate == today;
                        bool isHovered = i == _hoveredDayIndex;

                        if (isSelected)
                        {
                            using var brushSel = new SolidBrush(palette.Primary);
                            using var pathSel = CreateRoundedRectangle(new Rectangle(cellRect.X + 2, cellRect.Y + 1, cellW - 4, cellH - 2), 5);
                            g.FillPath(brushSel, pathSel);
                        }
                        else if (isHovered)
                        {
                            using var brushHov = new SolidBrush(Color.FromArgb(25, palette.Primary));
                            using var pathHov = CreateRoundedRectangle(new Rectangle(cellRect.X + 2, cellRect.Y + 1, cellW - 4, cellH - 2), 5);
                            g.FillPath(brushHov, pathHov);
                        }

                        if (isToday && !isSelected)
                        {
                            using var penToday = new Pen(palette.Primary, 1.2f);
                            using var pathToday = CreateRoundedRectangle(new Rectangle(cellRect.X + 2, cellRect.Y + 1, cellW - 4, cellH - 2), 5);
                            g.DrawPath(penToday, pathToday);
                        }

                        Color textColor;
                        if (isSelected) textColor = Color.White;
                        else if (!isCurrentMonth) textColor = Color.FromArgb(90, palette.TextSecondary);
                        else if (isToday) textColor = palette.Primary;
                        else textColor = palette.TextPrimary;

                        using (var brushDayText = new SolidBrush(textColor))
                        {
                            var activeFont = (isSelected || isToday) ? fontDayBold : fontDay;
                            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            g.DrawString(cellDate.Day.ToString(), activeFont, brushDayText, cellRect, sf);
                        }
                    }
                }
                else if (_viewMode == CalendarViewMode.Months)
                {
                    int gridTop = curY + 6;
                    int gridAvailH = (Height - 32) - gridTop;
                    int rowH = gridAvailH / 3;
                    int colW = (Width - 20) / 4;
                    DateTime today = DateTime.Today;

                    using var fontMonth = new Font(Font.FontFamily, 9f, FontStyle.Regular);
                    using var fontMonthBold = new Font(Font.FontFamily, 9f, FontStyle.Bold);

                    for (int i = 0; i < 12; i++)
                    {
                        int r = i / 4;
                        int c = i % 4;
                        var cellRect = new Rectangle(10 + (c * colW) + 2, gridTop + (r * rowH) + 2, colW - 4, rowH - 4);

                        bool isSelected = (_viewMonth.Year == _selectedDate.Year && (i + 1) == _selectedDate.Month);
                        bool isCurrentMonth = (_viewMonth.Year == today.Year && (i + 1) == today.Month);
                        bool isHovered = (i == _hoveredMonthIndex);

                        if (isSelected)
                        {
                            using var brushSel = new SolidBrush(palette.Primary);
                            using var pathSel = CreateRoundedRectangle(cellRect, 6);
                            g.FillPath(brushSel, pathSel);
                        }
                        else if (isHovered)
                        {
                            using var brushHov = new SolidBrush(Color.FromArgb(30, palette.Primary));
                            using var pathHov = CreateRoundedRectangle(cellRect, 6);
                            g.FillPath(brushHov, pathHov);
                        }
                        else
                        {
                            using var brushCard = new SolidBrush(Color.FromArgb(12, palette.Border));
                            using var pathCard = CreateRoundedRectangle(cellRect, 6);
                            g.FillPath(brushCard, pathCard);
                        }

                        if (isCurrentMonth && !isSelected)
                        {
                            using var penCurrent = new Pen(palette.Primary, 1.2f);
                            using var pathCurrent = CreateRoundedRectangle(cellRect, 6);
                            g.DrawPath(penCurrent, pathCurrent);
                        }

                        Color textColor = isSelected ? Color.White : (isHovered || isCurrentMonth ? palette.Primary : palette.TextPrimary);
                        using var brushText = new SolidBrush(textColor);
                        var activeFont = (isSelected || isCurrentMonth) ? fontMonthBold : fontMonth;
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(_monthNames[i], activeFont, brushText, cellRect, sf);
                    }
                }
                else if (_viewMode == CalendarViewMode.Years)
                {
                    int gridTop = curY + 6;
                    int gridAvailH = (Height - 32) - gridTop;
                    int rowH = gridAvailH / 3;
                    int colW = (Width - 20) / 4;
                    DateTime today = DateTime.Today;

                    using var fontYear = new Font(Font.FontFamily, 9f, FontStyle.Regular);
                    using var fontYearBold = new Font(Font.FontFamily, 9f, FontStyle.Bold);

                    for (int i = 0; i < 12; i++)
                    {
                        int r = i / 4;
                        int c = i % 4;
                        var cellRect = new Rectangle(10 + (c * colW) + 2, gridTop + (r * rowH) + 2, colW - 4, rowH - 4);
                        int year = (startDecade - 1) + i;

                        bool isSelected = (year == _selectedDate.Year);
                        bool isCurrentYear = (year == today.Year);
                        bool isHovered = (i == _hoveredYearIndex);
                        bool isOutside = (year < startDecade || year > startDecade + 9);

                        if (isSelected)
                        {
                            using var brushSel = new SolidBrush(palette.Primary);
                            using var pathSel = CreateRoundedRectangle(cellRect, 6);
                            g.FillPath(brushSel, pathSel);
                        }
                        else if (isHovered)
                        {
                            using var brushHov = new SolidBrush(Color.FromArgb(30, palette.Primary));
                            using var pathHov = CreateRoundedRectangle(cellRect, 6);
                            g.FillPath(brushHov, pathHov);
                        }
                        else
                        {
                            using var brushCard = new SolidBrush(Color.FromArgb(12, palette.Border));
                            using var pathCard = CreateRoundedRectangle(cellRect, 6);
                            g.FillPath(brushCard, pathCard);
                        }

                        if (isCurrentYear && !isSelected)
                        {
                            using var penCurrent = new Pen(palette.Primary, 1.2f);
                            using var pathCurrent = CreateRoundedRectangle(cellRect, 6);
                            g.DrawPath(penCurrent, pathCurrent);
                        }

                        Color textColor;
                        if (isSelected) textColor = Color.White;
                        else if (isHovered || isCurrentYear) textColor = palette.Primary;
                        else if (isOutside) textColor = Color.FromArgb(90, palette.TextSecondary);
                        else textColor = palette.TextPrimary;

                        using var brushText = new SolidBrush(textColor);
                        var activeFont = (isSelected || isCurrentYear) ? fontYearBold : fontYear;
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(year.ToString(), activeFont, brushText, cellRect, sf);
                    }
                }

                // 4. Footer Bar (Divider + Today link docked at bottom)
                using (var penDiv = new Pen(Color.FromArgb(20, palette.Border), 1f))
                {
                    g.DrawLine(penDiv, 8, Height - 26, Width - 8, Height - 26);
                }

                _todayLinkRect = new Rectangle(8, Height - 24, Width - 16, 20);
                using (var fontFoot = new Font(Font.FontFamily, 8f, FontStyle.Regular))
                using (var brushFoot = new SolidBrush(palette.Primary))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString($"Today: {DateTime.Today:yyyy-MM-dd}", fontFoot, brushFoot, _todayLinkRect, sf);
                }
            }
        }
    }

    /// <summary>
    /// Legacy alias for DateEdit.
    /// Preserved for 100% backward compatibility.
    /// </summary>
    [Obsolete("ZeroDatePicker is deprecated and will be removed in 5 release cycles. Please migrate to ZDateEdit instead.")]
    [ToolboxItem(false)]
    public class ZeroDatePicker : ZDateEdit
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZDateEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("DateEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZDateEdit instead.")]
    [ToolboxItem(false)]
    public class DateEdit : ZDateEdit
    {
    }

    #endregion
}
