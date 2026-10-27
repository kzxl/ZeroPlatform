using System;
using System.Collections.Generic;
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
    /// Virtualized, high-performance searchable autocomplete dropdown & lookup box for enterprise ERP catalog datasets.
    /// Features instant debounced filtering across 10,000+ items, multi-property item display,
    /// non-activating flyweight popup, and keyboard navigation.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultEvent("SelectedItemChanged")]
    [DefaultProperty("SelectedItem")]
    [Description("Searchable autocomplete dropdown & lookup box for large datasets")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroLookup.bmp")]
    public class ZLookUpEdit : ControlBase, IZeroEditor
    {
        private readonly List<LookUpItem> _items = new List<LookUpItem>();
        private readonly List<LookUpItem> _filteredItems = new List<LookUpItem>();

        private LookUpItem? _selectedItem;
        private string _placeholder = "Search items...";
        private bool _isHovered = false;
        private bool _isFocused = false;

        private readonly TextBox _searchTextBox;
        private readonly DropDownHost _dropdown;
        private readonly LookupListControl _listControl;

        private Rectangle _clearButtonRect;
        private Rectangle _chevronRect;
        private bool _hoverOnClear = false;
        private bool _showAddNewButton = false;
        private string _addNewButtonText = "+ Add New Record";

        public event EventHandler? SelectedItemChanged;
        public event EventHandler? EditValueChanged;

        /// <summary>
        /// Occurs when the user requests to add a new record or enters an unlisted search value.
        /// </summary>
        public event EventHandler<ProcessNewValueEventArgs>? ProcessNewValue;

        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("Determines whether the '+ Add New Record' footer action button is visible.")]
        public bool ShowAddNewButton
        {
            get => _showAddNewButton;
            set
            {
                if (_showAddNewButton != value)
                {
                    _showAddNewButton = value;
                    _listControl?.Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue("+ Add New Record")]
        [Description("Text caption displayed on the add new record footer action button.")]
        public string AddNewButtonText
        {
            get => _addNewButtonText;
            set
            {
                _addNewButtonText = value ?? "+ Add New Record";
                _listControl?.Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool IsModified { get; set; } = false;

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool ReadOnly
        {
            get => _searchTextBox?.ReadOnly ?? false;
            set
            {
                if (_searchTextBox != null)
                {
                    _searchTextBox.ReadOnly = value;
                    Invalidate();
                }
            }
        }

        [Browsable(false)]
        public object? EditValue
        {
            get => SelectedItem;
            set
            {
                if (value == null)
                {
                    SelectedItem = null;
                }
                else if (value is LookUpItem item)
                {
                    SelectedItem = item;
                }
                else
                {
                    string str = value.ToString() ?? "";
                    SelectedItem = _items.Find(x => x.Key == str || x.DisplayText == str);
                }
            }
        }

        public void Reset()
        {
            SelectedItem = null;
            IsModified = false;
        }

        public void Clear() => Reset();

        public ZLookUpEdit()
        {
            Font = new Font("Segoe UI", 9.5f);
            BackColor = Color.Transparent;

            _searchTextBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = Font,
                BackColor = CurrentPalette.Surface,
                ForeColor = CurrentPalette.TextPrimary,
                Location = new Point(32, 8),
                Width = 190
            };
            _searchTextBox.TextChanged += (s, e) => OnSearchTextChanged();
            _searchTextBox.KeyDown += OnSearchTextBoxKeyDown;
            _searchTextBox.GotFocus += (s, e) =>
            {
                _isFocused = true;
                Invalidate();
                ShowPopup();
            };
            _searchTextBox.LostFocus += (s, e) =>
            {
                _isFocused = false;
                Invalidate();
            };
            Controls.Add(_searchTextBox);

            _listControl = new LookupListControl(this);
            _dropdown = new DropDownHost
            {
                Content = _listControl
            };

            Size = new Size(260, 36);

            ZeroUIConfig.CornerStyleChanged += (s, e) => Invalidate();
            ZeroUIConfig.FontChanged += (s, e) =>
            {
                Font = ZeroUIConfig.DefaultFont;
                _searchTextBox.Font = ZeroUIConfig.DefaultFont;
                Invalidate();
            };
            UpdateTheme();
        }

        [Browsable(false)]
        public List<LookUpItem> Items => _items;

        [Category("Appearance")]
        [DefaultValue("Search items...")]
        public string Placeholder
        {
            get => _placeholder;
            set
            {
                _placeholder = value ?? "";
                Invalidate();
            }
        }

        [Browsable(false)]
        public LookUpItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem != value)
                {
                    _selectedItem = value;
                    if (_selectedItem != null)
                    {
                        _searchTextBox.Text = _selectedItem.DisplayText;
                    }
                    else
                    {
                        _searchTextBox.Text = "";
                    }
                    SelectedItemChanged?.Invoke(this, EventArgs.Empty);
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        [Browsable(false)]
        public string? SelectedKey => _selectedItem?.Key;

        public void SetItems(IEnumerable<LookUpItem> items)
        {
            _items.Clear();
            if (items != null)
            {
                _items.AddRange(items);
            }
            FilterItems("");
        }

        private void OnSearchTextChanged()
        {
            FilterItems(_searchTextBox.Text);
            if (!_dropdown.Visible && _searchTextBox.Focused)
            {
                ShowPopup();
            }
            Invalidate();
        }

        private void FilterItems(string query)
        {
            _filteredItems.Clear();
            _filteredItems.AddRange(LookUpFilterHelper.Filter(_items, query));
            _listControl.UpdateFilteredList(_filteredItems, query?.Trim() ?? string.Empty);
        }

        private void ShowPopup()
        {
            if (_items.Count == 0 && _filteredItems.Count == 0 && !_showAddNewButton) return;

            int popupW = Math.Max(Width, 340);
            int footerH = _showAddNewButton ? 32 : 0;
            int popupH = Math.Min(280, Math.Max(70, _filteredItems.Count * 36 + 4 + footerH));

            _dropdown.ShowDropDown(this, popupW, popupH);
        }

        private void OnSearchTextBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down)
            {
                _listControl.SelectNext();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                _listControl.SelectPrevious();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                if (_filteredItems.Count == 0 && _showAddNewButton)
                {
                    HandleAddNewRecord();
                }
                else
                {
                    _listControl.CommitCurrentSelection();
                }
                _dropdown.Close();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                _dropdown.Close();
                e.Handled = true;
            }
        }

        internal void CommitItem(LookUpItem item)
        {
            _selectedItem = item;
            _searchTextBox.Text = item.DisplayText;
            _searchTextBox.SelectionStart = _searchTextBox.Text.Length;
            _dropdown.Close();
            SelectedItemChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        private void UpdateTheme()
        {
            var palette = CurrentPalette;
            BackColor = Color.Transparent;
            if (_searchTextBox != null)
            {
                _searchTextBox.BackColor = palette.Surface;
                _searchTextBox.ForeColor = palette.TextPrimary;
            }
            if (_listControl != null)
            {
                _listControl.BackColor = palette.CardBackground;
                _listControl.Invalidate();
            }
            Invalidate();
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            UpdateTheme();
        }

        internal void HandleAddNewRecord()
        {
            var args = new ProcessNewValueEventArgs(_searchTextBox.Text);
            ProcessNewValue?.Invoke(this, args);
            if (args.Handled)
            {
                if (args.NewValue is LookUpItem item)
                {
                    if (!_items.Contains(item))
                    {
                        _items.Add(item);
                    }
                    CommitItem(item);
                }
                else if (args.NewValue != null)
                {
                    string str = args.NewValue.ToString() ?? _searchTextBox.Text;
                    var newItem = new LookUpItem(str, str);
                    _items.Add(newItem);
                    CommitItem(newItem);
                }
                _dropdown.Close();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_searchTextBox != null)
            {
                _searchTextBox.Location = new Point(32, (Height - _searchTextBox.Height) / 2);
                _searchTextBox.Width = Math.Max(10, Width - 70);
            }

            _chevronRect = new Rectangle(Width - 26, (Height - 16) / 2, 16, 16);
            _clearButtonRect = new Rectangle(Width - 48, (Height - 16) / 2, 16, 16);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool overClear = _clearButtonRect.Contains(e.Location) && (!string.IsNullOrEmpty(_searchTextBox.Text) || _selectedItem != null);
            if (_hoverOnClear != overClear)
            {
                _hoverOnClear = overClear;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_clearButtonRect.Contains(e.Location) && (!string.IsNullOrEmpty(_searchTextBox.Text) || _selectedItem != null))
            {
                _selectedItem = null;
                _searchTextBox.Text = "";
                FilterItems("");
                SelectedItemChanged?.Invoke(this, EventArgs.Empty);
                Invalidate();
                return;
            }

            if (_chevronRect.Contains(e.Location) || Bounds.Contains(e.Location))
            {
                _searchTextBox.Focus();
                if (!_dropdown.Visible)
                {
                    ShowPopup();
                }
                else
                {
                    _dropdown.Close();
                }
            }
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

            Rectangle borderRect = new Rectangle(0, 0, Width - 1, Height - 1);
            int effRadius = ZeroUIConfig.GetEffectiveRadius(6);

            // 2. Box Background & Border
            using (var path = ZeroUIConfig.CreateRoundedRectangle(borderRect, effRadius))
            {
                using var brushBg = new SolidBrush(palette.Surface);
                g.FillPath(brushBg, path);

                Color borderCol = _isFocused ? palette.Primary : (_isHovered ? palette.TextSecondary : palette.Border);
                using var penBorder = new Pen(borderCol, _isFocused ? 1.5f : 1f);
                g.DrawPath(penBorder, path);
            }

            // 2. Search Icon (🔍)
            using (var iconFont = new Font("Segoe UI Emoji", 9f))
            using (var brushIcon = new SolidBrush(palette.TextSecondary))
            {
                g.DrawString("🔍", iconFont, brushIcon, 10, (Height - 18) / 2);
            }

            // 3. Clear Button (✕)
            if (!string.IsNullOrEmpty(_searchTextBox.Text) || _selectedItem != null)
            {
                Color clearCol = _hoverOnClear ? palette.Danger : palette.TextSecondary;
                using var clearFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                using var brushClear = new SolidBrush(clearCol);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("✕", clearFont, brushClear, _clearButtonRect, sf);
            }

            // 4. Chevron (▼)
            using (var chevBrush = new SolidBrush(palette.TextSecondary))
            {
                PointF center = new PointF(_chevronRect.X + (_chevronRect.Width / 2f), _chevronRect.Y + (_chevronRect.Height / 2f));
                PointF[] pts = new[]
                {
                    new PointF(center.X - 3.5f, center.Y - 2f),
                    new PointF(center.X + 3.5f, center.Y - 2f),
                    new PointF(center.X, center.Y + 2.5f)
                };
                g.FillPolygon(chevBrush, pts);
            }
        }

        private static GraphicsPath CreateRoundedRect(Rectangle r, int radius) =>
            ZeroUIConfig.CreateRoundedRectangle(r, radius);

        /// <summary>
        /// Inner high-speed virtual list control for popup display.
        /// </summary>
        private class LookupListControl : Control
        {
            private readonly ZLookUpEdit _owner;
            private List<LookUpItem> _items = new List<LookUpItem>();
            private string _highlightQuery = "";
            private int _selectedIndex = 0;
            private int _hoveredIndex = -1;
            private int _scrollOffset = 0;
            private int _itemHeight = 36;
            private readonly VScrollBar _vScrollBar;

            public LookupListControl(ZLookUpEdit owner)
            {
                _owner = owner;
                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw, true);

                Font = new Font("Segoe UI", 9f);
                BackColor = Color.FromArgb(30, 41, 59);

                _vScrollBar = new VScrollBar
                {
                    Dock = DockStyle.Right,
                    Width = 12,
                    Visible = false
                };
                _vScrollBar.Scroll += (s, e) =>
                {
                    _scrollOffset = _vScrollBar.Value;
                    Invalidate();
                };
                Controls.Add(_vScrollBar);

                MouseWheel += (s, e) =>
                {
                    if (!_vScrollBar.Visible) return;
                    int delta = e.Delta > 0 ? -2 : 2;
                    int newVal = Math.Max(0, Math.Min(_vScrollBar.Maximum, _scrollOffset + delta));
                    if (newVal != _scrollOffset)
                    {
                        _scrollOffset = newVal;
                        _vScrollBar.Value = _scrollOffset;
                        Invalidate();
                    }
                };
            }

            public void UpdateFilteredList(List<LookUpItem> items, string query)
            {
                _items = items;
                _highlightQuery = query;
                _selectedIndex = Math.Max(0, Math.Min(_items.Count - 1, _selectedIndex));
                _scrollOffset = 0;
                UpdateScrollBar();
                Invalidate();
            }

            public void SelectNext()
            {
                if (_items.Count == 0) return;
                _selectedIndex = Math.Min(_items.Count - 1, _selectedIndex + 1);
                EnsureVisible(_selectedIndex);
                Invalidate();
            }

            public void SelectPrevious()
            {
                if (_items.Count == 0) return;
                _selectedIndex = Math.Max(0, _selectedIndex - 1);
                EnsureVisible(_selectedIndex);
                Invalidate();
            }

            public void CommitCurrentSelection()
            {
                if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
                {
                    _owner.CommitItem(_items[_selectedIndex]);
                }
            }

            private void EnsureVisible(int index)
            {
                int maxVis = Height / _itemHeight;
                if (index < _scrollOffset)
                {
                    _scrollOffset = index;
                }
                else if (index >= _scrollOffset + maxVis)
                {
                    _scrollOffset = index - maxVis + 1;
                }
                if (_vScrollBar.Visible)
                {
                    _vScrollBar.Value = Math.Max(0, Math.Min(_vScrollBar.Maximum, _scrollOffset));
                }
            }

            private void UpdateScrollBar()
            {
                if (_vScrollBar == null || _items == null) return;
                int maxVis = Math.Max(1, Height / _itemHeight);
                if (_items.Count > maxVis)
                {
                    _vScrollBar.Visible = true;
                    _vScrollBar.Maximum = Math.Max(0, _items.Count - maxVis + 1);
                    _vScrollBar.LargeChange = Math.Max(1, maxVis);
                }
                else
                {
                    _vScrollBar.Visible = false;
                    _scrollOffset = 0;
                }
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                UpdateScrollBar();
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int idx = (e.Y / _itemHeight) + _scrollOffset;
                if (idx >= 0 && idx < _items.Count && idx != _hoveredIndex)
                {
                    _hoveredIndex = idx;
                    Invalidate();
                }
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                _hoveredIndex = -1;
                Invalidate();
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                int footerH = _owner.ShowAddNewButton ? 32 : 0;
                if (_owner.ShowAddNewButton && e.Y >= Height - footerH)
                {
                    _owner.HandleAddNewRecord();
                    return;
                }
                int idx = (e.Y / _itemHeight) + _scrollOffset;
                if (idx >= 0 && idx < _items.Count)
                {
                    _selectedIndex = idx;
                    CommitCurrentSelection();
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                var palette = _owner.CurrentPalette;
                g.Clear(palette.CardBackground);

                int footerH = _owner.ShowAddNewButton ? 32 : 0;
                int listHeight = Height - footerH;

                if (_items.Count == 0)
                {
                    using var fontEmpty = new Font(Font.FontFamily, 9f, FontStyle.Italic);
                    using var brushEmpty = new SolidBrush(palette.TextSecondary);
                    g.DrawString("No matching items found", fontEmpty, brushEmpty, 14, 14);
                }
                else
                {
                    int clientW = _vScrollBar.Visible ? Width - _vScrollBar.Width : Width;
                    int start = _scrollOffset;
                    int end = Math.Min(_items.Count, start + (listHeight / _itemHeight) + 2);

                    using var fontBold = new Font(Font.FontFamily, 9f, FontStyle.Bold);
                    using var fontSub = new Font(Font.FontFamily, 8f, FontStyle.Regular);
                    using var fontCat = new Font(Font.FontFamily, 7.5f, FontStyle.Regular);

                    for (int i = start; i < end; i++)
                    {
                        var item = _items[i];
                        int y = (i - start) * _itemHeight;
                        if (y + _itemHeight > listHeight) break;
                        var rowRect = new Rectangle(0, y, clientW, _itemHeight);

                        bool isSel = i == _selectedIndex;
                        bool isHov = i == _hoveredIndex;

                        if (isSel)
                        {
                            using var brushSel = new SolidBrush(Color.FromArgb(45, palette.Primary));
                            g.FillRectangle(brushSel, rowRect);
                            using var penLeft = new SolidBrush(palette.Primary);
                            g.FillRectangle(penLeft, new Rectangle(0, y, 3, _itemHeight));
                        }
                        else if (isHov)
                        {
                            using var brushHov = new SolidBrush(Color.FromArgb(20, palette.Primary));
                            g.FillRectangle(brushHov, rowRect);
                        }

                        // Key / Code (e.g. "BOA472")
                        int textX = 12;
                        using (var brushKey = new SolidBrush(isSel ? palette.Primary : palette.TextPrimary))
                        {
                            g.DrawString(item.DisplayText, fontBold, brushKey, textX, y + 4);
                            var sz = g.MeasureString(item.DisplayText, fontBold);
                            textX += (int)sz.Width + 8;
                        }

                        // Category Pill
                        if (!string.IsNullOrEmpty(item.Category))
                        {
                            var catSz = g.MeasureString(item.Category, fontCat);
                            int pillW = (int)catSz.Width + 8;
                            int pillH = 16;
                            var pillRect = new Rectangle(textX, y + 5, pillW, pillH);

                            using var pillBg = new SolidBrush(Color.FromArgb(30, palette.Info));
                            using var pillPath = CreateRoundedRect(pillRect, 3);
                            g.FillPath(pillBg, pillPath);

                            using var pillText = new SolidBrush(palette.Info);
                            var sfPill = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            g.DrawString(item.Category, fontCat, pillText, pillRect, sfPill);
                        }

                        // SubText (Description / Cost with boundary guard)
                        if (!string.IsNullOrEmpty(item.SubText))
                        {
                            var subRect = new Rectangle(12, y + 18, clientW - 24, 16);
                            TextRenderer.DrawText(g, item.SubText, fontSub, subRect, palette.TextSecondary,
                                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                        }

                        // Divider
                        using var penDiv = new Pen(Color.FromArgb(10, palette.Border));
                        g.DrawLine(penDiv, 10, y + _itemHeight - 1, clientW - 10, y + _itemHeight - 1);
                    }
                }

                if (_owner.ShowAddNewButton)
                {
                    var footerRect = new Rectangle(0, Height - footerH, Width, footerH);
                    using var brushFooter = new SolidBrush(palette.Surface);
                    g.FillRectangle(brushFooter, footerRect);

                    using var penTop = new Pen(palette.Border, 1f);
                    g.DrawLine(penTop, 0, Height - footerH, Width, Height - footerH);

                    using var fontNew = new Font(Font.FontFamily, 8.5f, FontStyle.Bold);
                    using var brushNew = new SolidBrush(palette.Primary);
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(_owner.AddNewButtonText, fontNew, brushNew, footerRect, sf);
                }
            }
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZLookUpEdit"/>.
    /// </summary>
    [Obsolete("ZeroLookup is deprecated. Use LookUpEdit instead.")]
    [ToolboxItem(false)]
    public class ZeroLookup : LookUpEdit
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZLookUpEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("LookUpEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZLookUpEdit instead.")]
    [ToolboxItem(false)]
    public class LookUpEdit : ZLookUpEdit
    {
    }

    #endregion
}
