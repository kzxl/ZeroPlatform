using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;
using ZeroUI.WinForms.Rendering;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Searchable autocomplete text box with both synchronous and asynchronous suggestion support.
    /// Features debounced typing, customizable list presentation, and IZeroEditor compliance.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultEvent("ItemSelected")]
    [DefaultProperty("Text")]
    [Description("Searchable autocomplete text box with async support")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroTextBox.bmp")]
    public class ZAutoComplete : ControlBase, IZeroEditor
    {
        private readonly TextBox _innerTextBox;
        private readonly DropDownHost _dropdown;
        private readonly AutoCompleteListControl _listControl;
        private readonly System.Windows.Forms.Timer _debounceTimer;
        private CancellationTokenSource? _cts;

        private Rectangle _clearButtonRect;
        private bool _hoverOnClear = false;
        private bool _isFocused = false;
        private bool _isHovered = false;

        private string _placeholderText = "Type to search...";
        private int _debounceMs = 200;
        private int _maxSuggestions = 10;
        private int _minSearchLength = 1;
        private bool _showClearButton = true;
        private AutoCompleteItem? _selectedItem;

        public event EventHandler<AutoCompleteItemSelectedEventArgs>? ItemSelected;
        public event EventHandler? EditValueChanged;

        [Category("Appearance")]
        [DefaultValue("Type to search...")]
        [Description("Placeholder text shown when the input is empty")]
        public string PlaceholderText
        {
            get => _placeholderText;
            set
            {
                _placeholderText = value ?? "";
                Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(200)]
        [Description("Delay in milliseconds before executing the search query")]
        public int DebounceMilliseconds
        {
            get => _debounceMs;
            set
            {
                _debounceMs = Math.Max(50, value);
                if (_debounceTimer != null) _debounceTimer.Interval = _debounceMs;
            }
        }

        [Category("Behavior")]
        [DefaultValue(10)]
        [Description("Maximum number of suggestions to display")]
        public int MaxSuggestions
        {
            get => _maxSuggestions;
            set => _maxSuggestions = Math.Max(1, value);
        }

        [Category("Behavior")]
        [DefaultValue(1)]
        [Description("Minimum text length to trigger a search")]
        public int MinSearchLength
        {
            get => _minSearchLength;
            set => _minSearchLength = Math.Max(0, value);
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        [Description("Determines whether the clear (✕) button is shown when there is text")]
        public bool ShowClearButton
        {
            get => _showClearButton;
            set
            {
                _showClearButton = value;
                Invalidate();
            }
        }

        [Browsable(false)]
        public IEnumerable<AutoCompleteItem>? ItemsSource { get; set; }

        [Browsable(false)]
        public Func<string, CancellationToken, Task<IEnumerable<AutoCompleteItem>>>? AsyncItemsSource { get; set; }

        [Browsable(false)]
        public AutoCompleteItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem != value)
                {
                    _selectedItem = value;
                    IsModified = true;
                    if (_selectedItem != null)
                    {
                        _innerTextBox.Text = _selectedItem.DisplayText;
                    }
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
                    if (_selectedItem != null)
                    {
                        ItemSelected?.Invoke(this, new AutoCompleteItemSelectedEventArgs(_selectedItem, _innerTextBox.Text));
                    }
                }
            }
        }

        [Category("Data")]
        [DefaultValue("")]
        [Description("Property name used for displaying text in simple object binding")]
        public string DisplayMember { get; set; } = "";

        [Browsable(false)]
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.AllowNull]
#endif
        public override string Text
        {
            get => _innerTextBox?.Text ?? string.Empty;
            set
            {
                if (_innerTextBox != null)
                    _innerTextBox.Text = value ?? string.Empty;
            }
        }

        // IZeroEditor Implementation
        [Category("Behavior")]
        [DefaultValue(false)]
        public bool IsModified { get; set; } = false;

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool ReadOnly
        {
            get => _innerTextBox?.ReadOnly ?? false;
            set
            {
                if (_innerTextBox != null)
                {
                    _innerTextBox.ReadOnly = value;
                    Invalidate();
                }
            }
        }

        [Browsable(false)]
        public object? EditValue
        {
            get => SelectedItem?.Tag ?? SelectedItem?.DisplayText;
            set
            {
                // This is a minimal implementation, mapping simple values back might require data binding logic.
                // Assuming it's simple string matching for now.
                if (value == null)
                {
                    SelectedItem = null;
                    _innerTextBox.Text = string.Empty;
                }
                else
                {
                    string str = value.ToString() ?? "";
                    if (ItemsSource != null)
                    {
                        var match = ItemsSource.FirstOrDefault(x => x.DisplayText == str || (x.Tag != null && x.Tag.Equals(value)));
                        if (match != null)
                        {
                            SelectedItem = match;
                            return;
                        }
                    }
                    _innerTextBox.Text = str;
                }
            }
        }

        public void Reset()
        {
            SelectedItem = null;
            _innerTextBox.Text = string.Empty;
            IsModified = false;
        }

        public void Clear() => Reset();

        public ZAutoComplete()
        {
            Size = new Size(260, 36);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            _innerTextBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = Font,
                BackColor = CurrentPalette.Surface,
                ForeColor = CurrentPalette.TextPrimary,
                Location = new Point(32, (Height - 18) / 2),
                Width = Width - 60
            };

            _innerTextBox.TextChanged += InnerTextBox_TextChanged;
            _innerTextBox.KeyDown += InnerTextBox_KeyDown;
            _innerTextBox.GotFocus += (s, e) => { _isFocused = true; Invalidate(); };
            _innerTextBox.LostFocus += (s, e) => { _isFocused = false; Invalidate(); };

            Controls.Add(_innerTextBox);

            _listControl = new AutoCompleteListControl(this);
            _dropdown = new DropDownHost { Content = _listControl };

            _debounceTimer = new System.Windows.Forms.Timer { Interval = _debounceMs };
            _debounceTimer.Tick += DebounceTimer_Tick;

            ZeroUIConfig.CornerStyleChanged += (s, e) => Invalidate();
            ZeroUIConfig.FontChanged += (s, e) =>
            {
                Font = ZeroUIConfig.DefaultFont;
                _innerTextBox.Font = ZeroUIConfig.DefaultFont;
                Invalidate();
            };

            UpdateTheme();
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            UpdateTheme();
        }

        private void UpdateTheme()
        {
            if (_innerTextBox == null) return;
            var palette = CurrentPalette;
            _innerTextBox.BackColor = palette.Surface;
            _innerTextBox.ForeColor = palette.TextPrimary;
            if (_listControl != null)
            {
                _listControl.BackColor = palette.CardBackground;
                _listControl.Invalidate();
            }
            Invalidate();
        }

        private void InnerTextBox_TextChanged(object? sender, EventArgs e)
        {
            _selectedItem = null; // Typing invalidates selected item
            _debounceTimer.Stop();
            _debounceTimer.Start();
            Invalidate();
        }

        private void InnerTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                if (_dropdown.Visible)
                {
                    _dropdown.Close();
                    e.Handled = true;
                }
                else if (ShowClearButton && !string.IsNullOrEmpty(_innerTextBox.Text))
                {
                    Clear();
                    e.Handled = true;
                }
            }
            else if (e.KeyCode == Keys.Down)
            {
                if (_dropdown.Visible)
                {
                    _listControl.SelectNext();
                    e.Handled = true;
                }
                else
                {
                    PerformSearch(_innerTextBox.Text);
                    e.Handled = true;
                }
            }
            else if (e.KeyCode == Keys.Up)
            {
                if (_dropdown.Visible)
                {
                    _listControl.SelectPrevious();
                    e.Handled = true;
                }
            }
            else if (e.KeyCode == Keys.Enter)
            {
                if (_dropdown.Visible)
                {
                    _listControl.CommitCurrentSelection();
                    e.Handled = true;
                }
            }
        }

        private async void DebounceTimer_Tick(object? sender, EventArgs e)
        {
            _debounceTimer.Stop();
            string query = _innerTextBox.Text;
            await PerformSearch(query);
        }

        private async Task PerformSearch(string query)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            if (query.Length < MinSearchLength)
            {
                _dropdown.Close();
                return;
            }

            try
            {
                IEnumerable<AutoCompleteItem> results = Array.Empty<AutoCompleteItem>();

                if (AsyncItemsSource != null)
                {
                    results = await AsyncItemsSource(query, token);
                }
                else if (ItemsSource != null)
                {
                    results = AutoCompleteFilterHelper.Filter(ItemsSource, query, MaxSuggestions);
                }

                if (token.IsCancellationRequested) return;

                var list = results.Take(MaxSuggestions).ToList();
                if (list.Count > 0)
                {
                    _listControl.UpdateFilteredList(list, query);
                    int popupHeight = Math.Min(300, list.Count * 36 + 8);
                    if (!_dropdown.Visible)
                    {
                        _dropdown.ShowDropDown(this, Width, popupHeight);
                    }
                    else
                    {
                        _listControl.Size = new Size(Width, popupHeight);
                        _dropdown.Size = new Size(Width, popupHeight);
                    }
                }
                else
                {
                    _dropdown.Close();
                }
            }
            catch (OperationCanceledException)
            {
                // Ignored
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Search failed: {ex.Message}");
            }
        }

        internal void CommitItem(AutoCompleteItem item)
        {
            SelectedItem = item;
            _dropdown.Close();
            _innerTextBox.SelectionStart = _innerTextBox.Text.Length;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_innerTextBox != null)
            {
                int btnSpace = ShowClearButton ? 30 : 0;
                _innerTextBox.Location = new Point(32, (Height - _innerTextBox.Height) / 2);
                _innerTextBox.Width = Math.Max(10, Width - 32 - btnSpace - 10);
            }
            _clearButtonRect = new Rectangle(Width - 28, (Height - 18) / 2, 18, 18);
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
            _hoverOnClear = false;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool overClear = ShowClearButton && _clearButtonRect.Contains(e.Location) && _innerTextBox.Text.Length > 0;
            if (_hoverOnClear != overClear)
            {
                _hoverOnClear = overClear;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (ShowClearButton && _clearButtonRect.Contains(e.Location) && _innerTextBox.Text.Length > 0)
            {
                Clear();
                _innerTextBox.Focus();
            }
            else
            {
                _innerTextBox.Focus();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = CurrentPalette;
            Color parentBg = ZeroUIConfig.GetParentBackground(this, palette.Background);

            using (var brushParent = new SolidBrush(parentBg))
            {
                g.FillRectangle(brushParent, ClientRectangle);
            }

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int effRadius = ZeroUIConfig.GetEffectiveRadius(6);

            Color borderColor = _isFocused ? palette.Primary : (_isHovered ? palette.TextSecondary : palette.Border);
            
            using (var path = ZeroUIConfig.CreateRoundedRectangle(rect, effRadius))
            {
                using var bgBrush = new SolidBrush(palette.Surface);
                g.FillPath(bgBrush, path);

                using var borderPen = new Pen(borderColor, _isFocused ? 1.5f : 1f);
                g.DrawPath(borderPen, path);
            }

            // Search Icon
            using (var iconFont = new Font("Segoe UI Emoji", 9f))
            using (var brushIcon = new SolidBrush(palette.TextSecondary))
            {
                g.DrawString("🔍", iconFont, brushIcon, 10, (Height - 18) / 2);
            }

            // Clear Button
            if (ShowClearButton && _innerTextBox.Text.Length > 0)
            {
                if (_hoverOnClear)
                {
                    using var clearBg = new SolidBrush(palette.Hover);
                    g.FillEllipse(clearBg, _clearButtonRect);
                }

                Color clearCol = _hoverOnClear ? palette.Danger : palette.TextSecondary;
                using var clearFont = ZeroFontCache.Get("Segoe UI", 7.5f, FontStyle.Bold);
                using var brushClear = new SolidBrush(clearCol);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("✕", clearFont, brushClear, _clearButtonRect, sf);
            }

            // Placeholder Text
            if (_innerTextBox.Text.Length == 0 && !_isFocused)
            {
                Rectangle phRect = new Rectangle(32, 0, Width - 60, Height);
                TextRenderer.DrawText(
                    g,
                    PlaceholderText,
                    Font,
                    phRect,
                    palette.TextSecondary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _debounceTimer?.Dispose();
                _cts?.Cancel();
                _cts?.Dispose();
            }
            base.Dispose(disposing);
        }

        private class AutoCompleteListControl : Control
        {
            private readonly ZAutoComplete _owner;
            private List<AutoCompleteItem> _items = new List<AutoCompleteItem>();
            private string _highlightQuery = "";
            private int _selectedIndex = -1;
            private int _hoveredIndex = -1;
            private int _scrollOffset = 0;
            private int _itemHeight = 36;
            private readonly VScrollBar _vScrollBar;

            public AutoCompleteListControl(ZAutoComplete owner)
            {
                _owner = owner;
                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw, true);

                Font = new Font("Segoe UI", 9f);
                
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
                    int delta = e.Delta > 0 ? -1 : 1;
                    int newVal = Math.Max(0, Math.Min(_vScrollBar.Maximum, _scrollOffset + delta));
                    if (newVal != _scrollOffset)
                    {
                        _scrollOffset = newVal;
                        _vScrollBar.Value = _scrollOffset;
                        Invalidate();
                    }
                };
            }

            public void UpdateFilteredList(List<AutoCompleteItem> items, string query)
            {
                _items = items;
                _highlightQuery = query;
                _selectedIndex = items.Count > 0 ? 0 : -1;
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
                if (index < 0) return;
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

                if (_items.Count == 0) return;

                int clientW = _vScrollBar.Visible ? Width - _vScrollBar.Width : Width;
                int start = _scrollOffset;
                int maxVis = Height / _itemHeight;
                int end = Math.Min(_items.Count, start + maxVis + 1);

                using var fontReg = ZeroFontCache.Get(Font.FontFamily.Name, 9f, FontStyle.Regular);
                using var fontBold = ZeroFontCache.Get(Font.FontFamily.Name, 9f, FontStyle.Bold);
                using var fontSub = ZeroFontCache.Get(Font.FontFamily.Name, 8f, FontStyle.Regular);
                using var fontCat = ZeroFontCache.Get(Font.FontFamily.Name, 7.5f, FontStyle.Regular);
                using var fontGlyph = ZeroFontCache.Get("Segoe UI Emoji", 9f, FontStyle.Regular);

                for (int i = start; i < end; i++)
                {
                    var item = _items[i];
                    int y = (i - start) * _itemHeight;
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

                    int textX = 12;

                    // Glyph
                    if (!string.IsNullOrEmpty(item.Glyph))
                    {
                        using var brushGlyph = new SolidBrush(palette.TextSecondary);
                        g.DrawString(item.Glyph, fontGlyph, brushGlyph, textX, y + 8);
                        var szGlyph = g.MeasureString(item.Glyph, fontGlyph);
                        textX += (int)szGlyph.Width + 4;
                    }

                    // DisplayText with Highlighting
                    using (var brushText = new SolidBrush(isSel ? palette.Primary : palette.TextPrimary))
                    {
                        if (!string.IsNullOrEmpty(_highlightQuery) && item.DisplayText.IndexOf(_highlightQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            int idx = item.DisplayText.IndexOf(_highlightQuery, StringComparison.OrdinalIgnoreCase);
                            string before = item.DisplayText.Substring(0, idx);
                            string match = item.DisplayText.Substring(idx, _highlightQuery.Length);
                            string after = item.DisplayText.Substring(idx + _highlightQuery.Length);

                            float currX = textX;
                            if (before.Length > 0)
                            {
                                g.DrawString(before, fontReg, brushText, currX, y + 8);
                                currX += g.MeasureString(before, fontReg).Width - 4; // slight overlap correction
                            }
                            
                            using var brushMatch = new SolidBrush(isSel ? palette.Primary : palette.TextPrimary);
                            g.DrawString(match, fontBold, brushMatch, currX, y + 8);
                            currX += g.MeasureString(match, fontBold).Width - 4;

                            if (after.Length > 0)
                            {
                                g.DrawString(after, fontReg, brushText, currX, y + 8);
                                currX += g.MeasureString(after, fontReg).Width;
                            }
                            textX = (int)currX + 8;
                        }
                        else
                        {
                            g.DrawString(item.DisplayText, fontReg, brushText, textX, y + 8);
                            textX += (int)g.MeasureString(item.DisplayText, fontReg).Width + 8;
                        }
                    }

                    // Category Pill
                    if (!string.IsNullOrEmpty(item.Category))
                    {
                        var catSz = g.MeasureString(item.Category, fontCat);
                        int pillW = (int)catSz.Width + 8;
                        int pillH = 16;
                        var pillRect = new Rectangle(clientW - pillW - 12, y + (_itemHeight - pillH) / 2, pillW, pillH);

                        using var pillBg = new SolidBrush(Color.FromArgb(30, palette.Info));
                        using var pillPath = ZeroUIConfig.CreateRoundedRectangle(pillRect, 3);
                        g.FillPath(pillBg, pillPath);

                        using var pillText = new SolidBrush(palette.Info);
                        var sfPill = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(item.Category, fontCat, pillText, pillRect, sfPill);
                    }

                    // SubText
                    if (!string.IsNullOrEmpty(item.SubText))
                    {
                        var subRect = new Rectangle(textX, y + 9, clientW - textX - 40, 16); // basic bounding
                        TextRenderer.DrawText(g, item.SubText, fontSub, subRect, palette.TextSecondary,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                    }
                }
            }
        }
    }

    [Obsolete("Use ZAutoComplete instead.")]
    [ToolboxItem(false)]
    public class ZeroAutoComplete : ZAutoComplete { }
}
