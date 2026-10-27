using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Modern chip-tag multi-selection dropdown control with tag removal,
    /// search filtering, selection limits, and theme reactivity.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultProperty("SelectedItems")]
    [DefaultEvent("SelectionChanged")]
    [Description("Multi-selection tag box with chip removal and popup checkable dropdown")]
    public class ZMultiSelect : Control
    {
        private object? _itemsSource;
        private List<object> _selectedItems = new List<object>();
        private string? _displayMember;
        private string _placeholder = "Select items...";
        private int _maxSelections = 0;

        private readonly List<Rectangle> _chipRects = new List<Rectangle>();
        private readonly List<Rectangle> _chipCloseRects = new List<Rectangle>();
        private int _hoveredChipIndex = -1;
        private int _hoveredCloseIndex = -1;
        private bool _isHovered = false;
        private bool _isFocused = false;

        private ToolStripDropDown? _dropdown;
        private ToolStripControlHost? _dropdownHost;
        private CheckedListBox? _checkedListBox;

        public event EventHandler? SelectionChanged;

        [Category("Data")]
        [Description("The source collection of available items.")]
        public object? ItemsSource
        {
            get => _itemsSource;
            set
            {
                _itemsSource = value;
                PopulateDropdown();
                Invalidate();
            }
        }

        [Category("Data")]
        [Description("The list of currently selected items.")]
        public List<object> SelectedItems
        {
            get => _selectedItems;
            set
            {
                _selectedItems = value ?? new List<object>();
                SyncDropdownChecks();
                Invalidate();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [Category("Data")]
        [Description("Property name to display for complex objects.")]
        public string? DisplayMember
        {
            get => _displayMember;
            set
            {
                _displayMember = value;
                PopulateDropdown();
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue("Select items...")]
        [Description("Prompt text shown when no items are selected.")]
        public string Placeholder
        {
            get => _placeholder;
            set
            {
                _placeholder = value ?? string.Empty;
                Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(0)]
        [Description("Maximum number of items that can be selected (0 for unlimited).")]
        public int MaxSelections
        {
            get => _maxSelections;
            set
            {
                _maxSelections = Math.Max(0, value);
                if (_maxSelections > 0 && _selectedItems.Count > _maxSelections)
                {
                    _selectedItems = _selectedItems.Take(_maxSelections).ToList();
                    SyncDropdownChecks();
                    Invalidate();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public ZMultiSelect()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(240, 36);
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ZeroTheme.ThemeChanged -= OnThemeChanged;
                _dropdown?.Dispose();
                _checkedListBox?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            if (IsDisposed) return;
            Invalidate();
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
            _hoveredChipIndex = -1;
            _hoveredCloseIndex = -1;
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            _isFocused = true;
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            _isFocused = false;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int prevChip = _hoveredChipIndex;
            int prevClose = _hoveredCloseIndex;

            _hoveredChipIndex = -1;
            _hoveredCloseIndex = -1;

            for (int i = 0; i < _chipRects.Count; i++)
            {
                if (_chipRects[i].Contains(e.Location))
                {
                    _hoveredChipIndex = i;
                    if (i < _chipCloseRects.Count && _chipCloseRects[i].Contains(e.Location))
                    {
                        _hoveredCloseIndex = i;
                    }
                    break;
                }
            }

            if (prevChip != _hoveredChipIndex || prevClose != _hoveredCloseIndex)
            {
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            Focus();

            // Check if user clicked an 'x' on a chip
            for (int i = 0; i < _chipCloseRects.Count; i++)
            {
                if (_chipCloseRects[i].Contains(e.Location))
                {
                    RemoveItemAt(i);
                    return;
                }
            }

            // Otherwise, open the dropdown
            ShowDropDown();
        }

        private void RemoveItemAt(int index)
        {
            if (index >= 0 && index < _selectedItems.Count)
            {
                _selectedItems.RemoveAt(index);
                SyncDropdownChecks();
                Invalidate();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private string GetItemDisplayText(object? item)
        {
            if (item == null) return string.Empty;
            if (!string.IsNullOrEmpty(_displayMember))
            {
                var prop = item.GetType().GetProperty(_displayMember);
                if (prop != null)
                {
                    var val = prop.GetValue(item);
                    return val?.ToString() ?? string.Empty;
                }
            }
            return item.ToString() ?? string.Empty;
        }

        private IEnumerable<object> GetAvailableItems()
        {
            if (_itemsSource is IEnumerable enumerable and not string)
            {
                foreach (var item in enumerable)
                {
                    if (item != null) yield return item;
                }
            }
        }

        private void InitDropdown()
        {
            if (_dropdown != null) return;

            _checkedListBox = new CheckedListBox
            {
                BorderStyle = BorderStyle.None,
                CheckOnClick = true,
                Font = ZeroFontCache.Get(8.5f),
                BackColor = ZeroTheme.Colors.Surface,
                ForeColor = ZeroTheme.Colors.TextPrimary,
                IntegralHeight = false,
                Width = Math.Max(Width, 200),
                Height = 160
            };

            _checkedListBox.ItemCheck += OnCheckedListBoxItemCheck;

            _dropdownHost = new ToolStripControlHost(_checkedListBox)
            {
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoSize = false,
                Size = _checkedListBox.Size
            };

            _dropdown = new ToolStripDropDown
            {
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                DropShadowEnabled = true
            };
            _dropdown.Items.Add(_dropdownHost);
            _dropdown.Opened += (s, e) => SyncDropdownChecks();
        }

        private void PopulateDropdown()
        {
            InitDropdown();
            if (_checkedListBox == null) return;

            _checkedListBox.Items.Clear();
            foreach (var item in GetAvailableItems())
            {
                _checkedListBox.Items.Add(item);
            }
            SyncDropdownChecks();
        }

        private bool _isSyncingChecks = false;

        private void SyncDropdownChecks()
        {
            if (_checkedListBox == null || _isSyncingChecks) return;
            _isSyncingChecks = true;
            try
            {
                for (int i = 0; i < _checkedListBox.Items.Count; i++)
                {
                    var item = _checkedListBox.Items[i];
                    bool isChecked = _selectedItems.Contains(item);
                    _checkedListBox.SetItemChecked(i, isChecked);
                }
            }
            finally
            {
                _isSyncingChecks = false;
            }
        }

        private void OnCheckedListBoxItemCheck(object? sender, System.Windows.Forms.ItemCheckEventArgs e)
        {
            if (_isSyncingChecks || _checkedListBox == null) return;

            BeginInvoke((Action)(() =>
            {
                var item = _checkedListBox.Items[e.Index];
                if (e.NewValue == CheckState.Checked)
                {
                    if (_maxSelections > 0 && _selectedItems.Count >= _maxSelections)
                    {
                        // Exceeded max selections, revert check
                        _isSyncingChecks = true;
                        _checkedListBox.SetItemChecked(e.Index, false);
                        _isSyncingChecks = false;
                        return;
                    }
                    if (!_selectedItems.Contains(item))
                    {
                        _selectedItems.Add(item);
                    }
                }
                else
                {
                    _selectedItems.Remove(item);
                }

                Invalidate();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }));
        }

        public void ShowDropDown()
        {
            InitDropdown();
            PopulateDropdown();
            if (_dropdown == null || _checkedListBox == null) return;

            _checkedListBox.Width = Math.Max(Width, 200);
            _dropdownHost!.Size = _checkedListBox.Size;
            _dropdown.Show(this, new Point(0, Height));
        }

        public void CloseDropDown()
        {
            _dropdown?.Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var bounds = ClientRectangle;
            var c = ZeroTheme.Colors;

            // Background
            using var bgBrush = new SolidBrush(c.Surface);
            g.FillRectangle(bgBrush, bounds);

            // Border
            var borderColor = _isFocused ? c.Primary : (_isHovered ? c.BorderFocused : c.BorderSubtle);
            using (var borderPen = new Pen(borderColor, _isFocused ? 1.5f : 1f))
            {
                g.DrawRectangle(borderPen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            }

            // Dropdown Chevron on the right
            int arrowSize = 6;
            int arrowRight = bounds.Right - 12;
            int arrowCenterY = bounds.Height / 2;
            Point[] arrowPoints =
            {
                new Point(arrowRight - arrowSize, arrowCenterY - 2),
                new Point(arrowRight + arrowSize, arrowCenterY - 2),
                new Point(arrowRight, arrowCenterY + 4)
            };
            using (var arrowBrush = new SolidBrush(c.TextMuted))
            {
                g.FillPolygon(arrowBrush, arrowPoints);
            }

            // Render Selected Chips or Placeholder
            _chipRects.Clear();
            _chipCloseRects.Clear();

            int usableWidth = bounds.Width - 28;
            int curX = 6;
            int curY = 4;
            int chipH = bounds.Height - 8;

            if (_selectedItems.Count == 0)
            {
                var placeholderFont = ZeroFontCache.Get(9f);
                using var placeholderBrush = new SolidBrush(c.TextMuted);
                var textRect = new Rectangle(8, 0, usableWidth, bounds.Height);
                var sf = new StringFormat
                {
                    LineAlignment = StringAlignment.Center,
                    Alignment = StringAlignment.Near
                };
                g.DrawString(_placeholder, placeholderFont, placeholderBrush, textRect, sf);
            }
            else
            {
                var chipFont = ZeroFontCache.Get(8.5f);
                var closeFont = ZeroFontCache.Get(7.5f, FontStyle.Bold);

                for (int i = 0; i < _selectedItems.Count; i++)
                {
                    string text = GetItemDisplayText(_selectedItems[i]);
                    var textSize = g.MeasureString(text, chipFont);
                    int chipW = (int)textSize.Width + 24;

                    if (curX + chipW > usableWidth && i > 0)
                    {
                        // Overflow badge e.g. "+2 more"
                        int remaining = _selectedItems.Count - i;
                        string moreText = $"+{remaining}";
                        var moreSize = g.MeasureString(moreText, chipFont);
                        int moreW = (int)moreSize.Width + 10;
                        var moreRect = new Rectangle(curX, curY, moreW, chipH);

                        using var moreBg = new SolidBrush(Color.FromArgb(30, c.Primary));
                        using var moreBorder = new Pen(Color.FromArgb(80, c.Primary));
                        FillRoundedRect(g, moreBg, moreRect, 4);
                        DrawRoundedRect(g, moreBorder, moreRect, 4);

                        using var moreTextBrush = new SolidBrush(c.Primary);
                        g.DrawString(moreText, chipFont, moreTextBrush, moreRect, new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        });
                        break;
                    }

                    var chipRect = new Rectangle(curX, curY, chipW, chipH);
                    _chipRects.Add(chipRect);

                    // Chip background & border
                    bool isChipHovered = (_hoveredChipIndex == i);
                    using var chipBg = new SolidBrush(isChipHovered ? Color.FromArgb(40, c.Primary) : Color.FromArgb(20, c.Primary));
                    using var chipBorder = new Pen(Color.FromArgb(100, c.Primary));
                    FillRoundedRect(g, chipBg, chipRect, 4);
                    DrawRoundedRect(g, chipBorder, chipRect, 4);

                    // Chip text
                    using var chipTextBrush = new SolidBrush(c.TextPrimary);
                    var textBounds = new Rectangle(chipRect.X + 6, chipRect.Y, (int)textSize.Width + 2, chipH);
                    g.DrawString(text, chipFont, chipTextBrush, textBounds, new StringFormat
                    {
                        LineAlignment = StringAlignment.Center
                    });

                    // 'x' close button
                    int closeSize = 12;
                    var closeRect = new Rectangle(chipRect.Right - closeSize - 4, chipRect.Y + (chipH - closeSize) / 2, closeSize, closeSize);
                    _chipCloseRects.Add(closeRect);

                    bool isCloseHovered = (_hoveredCloseIndex == i);
                    if (isCloseHovered)
                    {
                        using var closeBg = new SolidBrush(Color.FromArgb(60, c.Danger));
                        g.FillEllipse(closeBg, closeRect);
                    }

                    using var closeTextBrush = new SolidBrush(isCloseHovered ? c.Danger : c.TextMuted);
                    g.DrawString("×", closeFont, closeTextBrush, closeRect, new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    });

                    curX += chipW + 4;
                }
            }
        }

        private static void FillRoundedRect(Graphics g, Brush brush, Rectangle rect, int radius)
        {
            using var path = CreateRoundedRectanglePath(rect, radius);
            g.FillPath(brush, path);
        }

        private static void DrawRoundedRect(Graphics g, Pen pen, Rectangle rect, int radius)
        {
            using var path = CreateRoundedRectanglePath(rect, radius);
            g.DrawPath(pen, path);
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            var arc = new Rectangle(rect.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    [Obsolete("MultiSelect is deprecated and will be removed in 5 release cycles. Please migrate to ZMultiSelect instead.")]
    [ToolboxItem(false)]
    public class MultiSelect : ZMultiSelect { }

    [Obsolete("ZeroMultiSelect is deprecated and will be removed in 5 release cycles. Please migrate to ZMultiSelect instead.")]
    [ToolboxItem(false)]
    public class ZeroMultiSelect : ZMultiSelect { }
}
