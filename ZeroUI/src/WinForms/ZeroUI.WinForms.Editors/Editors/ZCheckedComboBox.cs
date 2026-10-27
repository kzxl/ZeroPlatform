using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Localization;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    public class ItemCheckEventArgs : EventArgs
    {
        public int Index { get; }
        public bool NewValue { get; }
        public object Item { get; }

        public ItemCheckEventArgs(int index, bool newValue, object item)
        {
            Index = index;
            NewValue = newValue;
            Item = item;
        }
    }

    /// <summary>
    /// Modern anti-aliased Multi-Select CheckedComboBox for ZeroUI WinForms.
    /// Supports checkboxes per item, Select-All toggle, instant search filtering,
    /// dynamic summary display formatting, and discrete token/chip badges.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultEvent("ItemCheck")]
    [Description("Modern checked combo box allowing multi-item selection with checkboxes and filter")]
    [ToolboxBitmap(typeof(ZeroIcons), "CheckedComboBoxEdit.bmp")]
    public class ZCheckedComboBox : ControlBase, IZeroEditor
    {
        public class CheckedItem
        {
            public object Value { get; set; }
            public string DisplayText { get; set; }
            public bool IsChecked { get; set; }

            public CheckedItem(object value, string displayText, bool isChecked = false)
            {
                Value = value;
                DisplayText = displayText;
                IsChecked = isChecked;
            }

            public override string ToString() => DisplayText;
        }

        private readonly List<CheckedItem> _items = new List<CheckedItem>();
        private string? _placeholder;
        private string? _summaryFormat;
        private int _itemHeight = 30;

        private CheckedComboDisplayMode _displayMode = CheckedComboDisplayMode.Text;
        private int _tokenHeight = 22;
        private int _tokenSpacing = 4;
        private readonly List<Rectangle> _tokenBounds = new List<Rectangle>();
        private readonly List<Rectangle> _closeBounds = new List<Rectangle>();
        private readonly List<int> _tokenItemIndices = new List<int>();
        private Rectangle _overflowBounds = Rectangle.Empty;
        private int _overflowCount = 0;
        private int _hoveredCloseIndex = -1;
        private bool _isHoveredOverflow = false;

        private bool _isHovered = false;
        private bool _isFocused = false;
        private bool _isDroppedDown = false;

        private readonly DropDownHost _dropdown;
        private readonly CheckedComboPopupControl _popupControl;

        public event EventHandler<ItemCheckEventArgs>? ItemCheck;
        public event EventHandler? CheckedChanged;
        public event EventHandler? EditValueChanged;

        [Category("Appearance")]
        [DefaultValue(CheckedComboDisplayMode.Text)]
        [Description("Specifies whether selected items are displayed as a summary text or discrete chips/tokens.")]
        public CheckedComboDisplayMode DisplayMode
        {
            get => _displayMode;
            set
            {
                if (_displayMode != value)
                {
                    _displayMode = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        [Description("Convenience property to switch display to discrete chips/tokens with dismiss buttons.")]
        public bool ShowTokens
        {
            get => _displayMode == CheckedComboDisplayMode.Tokens;
            set => DisplayMode = value ? CheckedComboDisplayMode.Tokens : CheckedComboDisplayMode.Text;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object? EditValue
        {
            get => CheckedValues;
            set
            {
                if (value == null)
                {
                    CheckAll(false);
                    return;
                }
                if (value is System.Collections.IEnumerable enumerable && !(value is string))
                {
                    var set = new HashSet<object>();
                    foreach (var item in enumerable)
                    {
                        if (item != null) set.Add(item);
                    }
                    for (int i = 0; i < _items.Count; i++)
                    {
                        SetItemChecked(i, set.Contains(_items[i].Value));
                    }
                }
                else
                {
                    string strVal = value.ToString() ?? "";
                    var tokens = new HashSet<string>(strVal.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < _items.Count; i++)
                    {
                        bool matches = tokens.Contains(_items[i].Value?.ToString() ?? "") || tokens.Contains(_items[i].DisplayText);
                        SetItemChecked(i, matches);
                    }
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool IsModified { get; set; } = false;

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool ReadOnly { get; set; } = false;

        public void Reset()
        {
            CheckAll(false);
            IsModified = false;
        }

        public void Clear() => Reset();

        [Category("Appearance")]
        public string Placeholder
        {
            get => _placeholder ?? Localizer.GetString(StringId.CheckedComboPlaceholder);
            set { _placeholder = value; Invalidate(); }
        }

        [Category("Appearance")]
        public string SummaryFormat
        {
            get => _summaryFormat ?? Localizer.GetString(StringId.CheckedComboSummaryFormat);
            set { _summaryFormat = value; Invalidate(); }
        }

        [Browsable(false)]
        public List<CheckedItem> Items => _items;

        [Browsable(false)]
        public List<object> CheckedValues
        {
            get
            {
                var list = new List<object>();
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i].IsChecked) list.Add(_items[i].Value);
                }
                return list;
            }
        }

        [Browsable(false)]
        public List<int> CheckedIndices
        {
            get
            {
                var list = new List<int>();
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i].IsChecked) list.Add(i);
                }
                return list;
            }
        }

        public ZCheckedComboBox()
        {
            Height = 32;
            Cursor = Cursors.Hand;
            Localizer.CultureChanged += (s, e) => Invalidate();
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            BackColor = Color.Transparent;

            _popupControl = new CheckedComboPopupControl(this);
            _dropdown = new DropDownHost
            {
                Content = _popupControl
            };
            _dropdown.Closed += (s, e) =>
            {
                _isDroppedDown = false;
                Invalidate();
            };
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            _popupControl?.Invalidate();
            Invalidate();
        }

        public void AddItem(object value, string displayText, bool isChecked = false)
        {
            _items.Add(new CheckedItem(value, displayText, isChecked));
            Invalidate();
        }

        public void SetItemChecked(int index, bool isChecked)
        {
            if (index < 0 || index >= _items.Count) return;
            if (_items[index].IsChecked != isChecked)
            {
                _items[index].IsChecked = isChecked;
                IsModified = true;
                ItemCheck?.Invoke(this, new ItemCheckEventArgs(index, isChecked, _items[index].Value));
                CheckedChanged?.Invoke(this, EventArgs.Empty);
                EditValueChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }

        public void CheckAll(bool isChecked)
        {
            bool anyChanged = false;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].IsChecked != isChecked)
                {
                    _items[i].IsChecked = isChecked;
                    ItemCheck?.Invoke(this, new ItemCheckEventArgs(i, isChecked, _items[i].Value));
                    anyChanged = true;
                }
            }
            if (anyChanged)
            {
                IsModified = true;
                CheckedChanged?.Invoke(this, EventArgs.Empty);
                EditValueChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }

        public string GetDisplayText()
        {
            var checkedList = new List<string>();
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].IsChecked) checkedList.Add(_items[i].DisplayText);
            }

            return CheckedComboHelper.FormatDisplayText(checkedList, Placeholder, SummaryFormat, 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var colors = CurrentPalette;
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

            // Draw Background
            using (var path = CreateRoundedRectanglePath(bounds, 4))
            {
                using (var brush = new SolidBrush(colors.Surface))
                {
                    g.FillPath(brush, path);
                }

                Color borderColor = _isDroppedDown || _isFocused
                    ? colors.Primary
                    : (_isHovered ? colors.PrimaryHover : colors.Border);

                using (var pen = new Pen(borderColor, _isDroppedDown || _isFocused ? 1.5f : 1.0f))
                {
                    g.DrawPath(pen, path);
                }
            }

            // Draw Chevron Arrow
            int arrowX = Width - 24;
            int arrowY = Height / 2;
            using (var pen = new Pen(_isHovered || _isDroppedDown ? colors.Primary : colors.TextSecondary, 1.8f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                if (_isDroppedDown)
                {
                    g.DrawLine(pen, arrowX, arrowY + 2, arrowX + 4, arrowY - 2);
                    g.DrawLine(pen, arrowX + 4, arrowY - 2, arrowX + 8, arrowY + 2);
                }
                else
                {
                    g.DrawLine(pen, arrowX, arrowY - 2, arrowX + 4, arrowY + 2);
                    g.DrawLine(pen, arrowX + 4, arrowY + 2, arrowX + 8, arrowY - 2);
                }
            }

            // Draw Display Text or Tokens
            if (_displayMode == CheckedComboDisplayMode.Tokens)
            {
                DrawTokens(g, colors);
            }
            else
            {
                DrawTextSummary(g, colors);
            }
        }

        private void DrawTextSummary(Graphics g, ZeroThemePalette colors)
        {
            string text = GetDisplayText();
            bool isMuted = text == _placeholder;
            using (var brush = new SolidBrush(isMuted ? colors.TextSecondary : colors.TextPrimary))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                var textRect = new Rectangle(12, 0, Width - 42, Height);
                g.DrawString(text, Font, brush, textRect, sf);
            }
        }

        private void DrawTokens(Graphics g, ZeroThemePalette colors)
        {
            _tokenBounds.Clear();
            _closeBounds.Clear();
            _tokenItemIndices.Clear();
            _overflowBounds = Rectangle.Empty;
            _overflowCount = 0;

            var checkedIndices = CheckedIndices;
            if (checkedIndices.Count == 0)
            {
                using (var brush = new SolidBrush(colors.TextSecondary))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter
                    };
                    var textRect = new Rectangle(12, 0, Width - 42, Height);
                    g.DrawString(Placeholder, Font, brush, textRect, sf);
                }
                return;
            }

            int curX = 8;
            int curY = (Height - _tokenHeight) / 2;
            int maxAvailableWidth = Width - 32;

            for (int k = 0; k < checkedIndices.Count; k++)
            {
                int itemIdx = checkedIndices[k];
                string itemText = _items[itemIdx].DisplayText;
                var size = g.MeasureString(itemText, Font);
                int tokenW = (int)Math.Ceiling(size.Width) + 24;
                int remainingItems = checkedIndices.Count - k;

                if (curX + tokenW > maxAvailableWidth)
                {
                    _overflowCount = remainingItems;
                    string overflowText = $"+{_overflowCount} more";
                    var pillSize = g.MeasureString(overflowText, Font);
                    int pillW = (int)Math.Ceiling(pillSize.Width) + 12;

                    if (curX + pillW > maxAvailableWidth && _tokenBounds.Count > 0)
                    {
                        pillW = Math.Max(24, maxAvailableWidth - curX);
                    }

                    var pillRect = new Rectangle(curX, curY, pillW, _tokenHeight);
                    _overflowBounds = pillRect;

                    using (var pillPath = CreateRoundedRectanglePath(pillRect, 4))
                    {
                        Color pillBg = _isHoveredOverflow ? Color.FromArgb(60, colors.Primary) : Color.FromArgb(30, colors.Primary);
                        using (var pillBrush = new SolidBrush(pillBg))
                        {
                            g.FillPath(pillBrush, pillPath);
                        }
                        using (var pillPen = new Pen(colors.Primary, 1f))
                        {
                            g.DrawPath(pillPen, pillPath);
                        }
                    }

                    using (var pillTextBrush = new SolidBrush(colors.Primary))
                    {
                        var sf = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center,
                            Trimming = StringTrimming.EllipsisCharacter
                        };
                        g.DrawString(overflowText, Font, pillTextBrush, pillRect, sf);
                    }

                    break;
                }

                var tokenRect = new Rectangle(curX, curY, tokenW, _tokenHeight);
                var closeRect = new Rectangle(curX + tokenW - 16, curY + (_tokenHeight - 10) / 2, 10, 10);

                _tokenBounds.Add(tokenRect);
                _closeBounds.Add(closeRect);
                _tokenItemIndices.Add(itemIdx);

                // Chip background & border
                using (var tPath = CreateRoundedRectanglePath(tokenRect, 4))
                {
                    using (var tBrush = new SolidBrush(Color.FromArgb(28, colors.Primary)))
                    {
                        g.FillPath(tBrush, tPath);
                    }
                    using (var tPen = new Pen(Color.FromArgb(90, colors.Primary), 1f))
                    {
                        g.DrawPath(tPen, tPath);
                    }
                }

                // Chip Text
                using (var brush = new SolidBrush(colors.TextPrimary))
                {
                    var textRect = new Rectangle(tokenRect.X + 6, tokenRect.Y, tokenRect.Width - 22, tokenRect.Height);
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter
                    };
                    g.DrawString(itemText, Font, brush, textRect, sf);
                }

                // Close '✕'
                bool isCloseHovered = (_tokenBounds.Count - 1) == _hoveredCloseIndex;
                Color xColor = isCloseHovered ? colors.Danger : colors.TextSecondary;
                using (var pen = new Pen(xColor, 1.4f))
                {
                    g.DrawLine(pen, closeRect.X + 1, closeRect.Y + 1, closeRect.Right - 1, closeRect.Bottom - 1);
                    g.DrawLine(pen, closeRect.Right - 1, closeRect.Y + 1, closeRect.X + 1, closeRect.Bottom - 1);
                }

                curX += tokenW + _tokenSpacing;
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
            _hoveredCloseIndex = -1;
            _isHoveredOverflow = false;
            Cursor = Cursors.Hand;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_displayMode == CheckedComboDisplayMode.Tokens)
            {
                int prevHover = _hoveredCloseIndex;
                bool prevPillHover = _isHoveredOverflow;

                _hoveredCloseIndex = -1;
                _isHoveredOverflow = false;

                for (int i = 0; i < _closeBounds.Count; i++)
                {
                    if (_closeBounds[i].Contains(e.Location))
                    {
                        _hoveredCloseIndex = i;
                        break;
                    }
                }

                if (_hoveredCloseIndex == -1 && !_overflowBounds.IsEmpty && _overflowBounds.Contains(e.Location))
                {
                    _isHoveredOverflow = true;
                }

                if (prevHover != _hoveredCloseIndex || prevPillHover != _isHoveredOverflow)
                {
                    Invalidate();
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_displayMode == CheckedComboDisplayMode.Tokens)
                {
                    for (int i = 0; i < _closeBounds.Count; i++)
                    {
                        if (_closeBounds[i].Contains(e.Location))
                        {
                            int itemIndex = _tokenItemIndices[i];
                            SetItemChecked(itemIndex, false);
                            return;
                        }
                    }
                }

                Focus();
                ToggleDropDown();
            }
        }

        public void ToggleDropDown()
        {
            if (_isDroppedDown)
            {
                _dropdown.Close();
            }
            else
            {
                OpenDropDown();
            }
        }

        public void OpenDropDown()
        {
            if (ReadOnly || !Enabled) return;
            if (_dropdown.Visible) return;

            _isDroppedDown = true;
            _popupControl.RefreshItems();

            int popupH = Math.Min(320, Math.Max(120, _items.Count * _itemHeight + 70));
            _popupControl.Size = new Size(Math.Max(Width, 240), popupH);

            _dropdown.ShowDropDown(this, Math.Max(Width, 240), popupH);
            Invalidate();
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // Internal Popup Control hosting search bar, Select-All toggle, and virtual items list
        private class CheckedComboPopupControl : Control
        {
            private readonly ZCheckedComboBox _owner;
            private readonly TextBox _searchBox;
            private readonly List<CheckedItem> _filteredItems = new List<CheckedItem>();
            private int _hoveredIndex = -1;
            private int _scrollY = 0;

            public CheckedComboPopupControl(ZCheckedComboBox owner)
            {
                _owner = owner;
                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw, true);
                DoubleBuffered = true;

                _searchBox = new TextBox
                {
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = new Font("Segoe UI", 9f),
                    Location = new Point(8, 8),
                    Width = 220
                };
                _searchBox.TextChanged += (s, e) => FilterItems();
                Controls.Add(_searchBox);
            }

            public void RefreshItems()
            {
                _searchBox.Text = string.Empty;
                FilterItems();
            }

            private void FilterItems()
            {
                _filteredItems.Clear();
                string query = _searchBox.Text.Trim();
                for (int i = 0; i < _owner._items.Count; i++)
                {
                    if (string.IsNullOrEmpty(query) || _owner._items[i].DisplayText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _filteredItems.Add(_owner._items[i]);
                    }
                }
                Invalidate();
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                _searchBox.Width = Width - 16;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                var colors = _owner.CurrentPalette;
                g.Clear(colors.Surface);

                // Draw border around popup
                using (var pen = new Pen(colors.Border))
                {
                    g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                }

                // Select-All row
                int selectAllY = 36;
                int itemH = _owner._itemHeight;

                bool allChecked = _owner._items.Count > 0;
                for (int i = 0; i < _owner._items.Count; i++)
                {
                    if (!_owner._items[i].IsChecked) { allChecked = false; break; }
                }

                DrawCheckboxItem(g, 0, selectAllY, Width, itemH, Localizer.GetString(StringId.CheckedComboSelectAll), allChecked, _hoveredIndex == -2);

                // Separator line
                using (var pen = new Pen(colors.Border))
                {
                    g.DrawLine(pen, 8, selectAllY + itemH, Width - 8, selectAllY + itemH);
                }

                // Items list
                int startY = selectAllY + itemH + 2;
                for (int i = 0; i < _filteredItems.Count; i++)
                {
                    int itemY = startY + i * itemH - _scrollY;
                    if (itemY + itemH < startY || itemY > Height) continue;
                    DrawCheckboxItem(g, 0, itemY, Width, itemH, _filteredItems[i].DisplayText, _filteredItems[i].IsChecked, _hoveredIndex == i);
                }
            }

            private void DrawCheckboxItem(Graphics g, int x, int y, int w, int h, string text, bool isChecked, bool isHovered)
            {
                var colors = _owner.CurrentPalette;
                if (isHovered)
                {
                    using (var brush = new SolidBrush(colors.Hover))
                    {
                        g.FillRectangle(brush, x + 2, y, w - 4, h);
                    }
                }

                // Draw Checkbox Box
                int cbSize = 16;
                int cbX = x + 12;
                int cbY = y + (h - cbSize) / 2;
                var cbRect = new Rectangle(cbX, cbY, cbSize, cbSize);

                if (isChecked)
                {
                    using (var brush = new SolidBrush(colors.Primary))
                    {
                        g.FillRectangle(brush, cbRect);
                    }
                    using (var pen = new Pen(Color.White, 1.8f))
                    {
                        g.DrawLine(pen, cbX + 3, cbY + 8, cbX + 6, cbY + 11);
                        g.DrawLine(pen, cbX + 6, cbY + 11, cbX + 12, cbY + 4);
                    }
                }
                else
                {
                    using (var pen = new Pen(colors.Border, 1.2f))
                    {
                        g.DrawRectangle(pen, cbRect);
                    }
                }

                // Draw text
                using (var brush = new SolidBrush(colors.TextPrimary))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter
                    };
                    var textRect = new Rectangle(cbX + cbSize + 8, y, w - (cbX + cbSize + 16), h);
                    g.DrawString(text, Font, brush, textRect, sf);
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int selectAllY = 36;
                int itemH = _owner._itemHeight;

                if (e.Y >= selectAllY && e.Y < selectAllY + itemH)
                {
                    _hoveredIndex = -2;
                }
                else if (e.Y >= selectAllY + itemH + 2)
                {
                    int index = (e.Y - (selectAllY + itemH + 2) + _scrollY) / itemH;
                    _hoveredIndex = (index >= 0 && index < _filteredItems.Count) ? index : -1;
                }
                else
                {
                    _hoveredIndex = -1;
                }
                Invalidate();
            }

            protected override void OnMouseClick(MouseEventArgs e)
            {
                base.OnMouseClick(e);
                int selectAllY = 36;
                int itemH = _owner._itemHeight;

                if (e.Y >= selectAllY && e.Y < selectAllY + itemH)
                {
                    bool allChecked = true;
                    for (int i = 0; i < _owner._items.Count; i++)
                    {
                        if (!_owner._items[i].IsChecked) { allChecked = false; break; }
                    }
                    _owner.CheckAll(!allChecked);
                    Invalidate();
                }
                else if (e.Y >= selectAllY + itemH + 2)
                {
                    int index = (e.Y - (selectAllY + itemH + 2) + _scrollY) / itemH;
                    if (index >= 0 && index < _filteredItems.Count)
                    {
                        var item = _filteredItems[index];
                        int originalIdx = _owner._items.IndexOf(item);
                        _owner.SetItemChecked(originalIdx, !item.IsChecked);
                        Invalidate();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Legacy alias for CheckedComboBoxEdit.
    /// Preserved for 100% backward compatibility.
    /// </summary>
    [Obsolete("ZeroCheckedComboBox is deprecated. Please use CheckedComboBoxEdit instead.")]
    [ToolboxItem(false)]
    public class ZeroCheckedComboBox : CheckedComboBoxEdit
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZCheckedComboBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("CheckedComboBoxEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZCheckedComboBox instead.")]
    [ToolboxItem(false)]
    public class CheckedComboBoxEdit : ZCheckedComboBox
    {
    }

    #endregion
}
