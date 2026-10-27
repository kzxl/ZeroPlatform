using System;

using ZeroUI.WinForms.Icons;using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Navigation
{
    public enum AccordionExpandMode
    {
        MultipleGroups,
        SingleGroup
    }

    public class AccordionItem
    {
        public string Text { get; set; } = "";
        public string? Glyph { get; set; }
        public string? BadgeText { get; set; }
        public Color? BadgeColor { get; set; }
        public object? Tag { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool IsVisible { get; set; } = true;

        internal Rectangle Bounds;
        public event EventHandler? Click;

        public AccordionItem() { }

        public AccordionItem(string text, string? glyph = null, EventHandler? onClick = null, string? badge = null)
        {
            Text = text;
            Glyph = glyph;
            BadgeText = badge;
            if (onClick != null) Click += onClick;
        }

        internal void OnClick() => Click?.Invoke(this, EventArgs.Empty);
    }

    public class AccordionGroup
    {
        public string Text { get; set; } = "";
        public string? Glyph { get; set; }
        public bool IsExpanded { get; set; } = true;
        public string? BadgeText { get; set; }
        public Color? BadgeColor { get; set; }
        public List<AccordionItem> Items { get; } = new List<AccordionItem>();
        public object? Tag { get; set; }

        internal Rectangle Bounds;
        internal Rectangle ChevronBounds;

        public AccordionGroup() { }

        public AccordionGroup(string text, string? glyph = null, bool isExpanded = true)
        {
            Text = text;
            Glyph = glyph;
            IsExpanded = isExpanded;
        }

        public AccordionItem AddItem(string text, string? glyph = null, EventHandler? onClick = null, string? badge = null)
        {
            var item = new AccordionItem(text, glyph, onClick, badge);
            Items.Add(item);
            return item;
        }
    }

    /// <summary>
    /// High-performance 100% Single-HWND Accordion & Hierarchy Navigation Control for ZeroUI.
    /// Renders the entire tree, chevron animations, search filter, and badges on a single GDI+ surface with 0 child Win32 window handles.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Overlays")]
    [Description("Multi-level collapsible accordion navigation menu with search filtering and badges.")]
    [ToolboxBitmap(typeof(ZeroIcons), "AccordionControl.bmp")]
    public class ZAccordion : Control, IZeroDpiScalable
    {
        private readonly List<AccordionGroup> _groups = new List<AccordionGroup>();
        private AccordionExpandMode _expandMode = AccordionExpandMode.MultipleGroups;

        private bool _showSearchBox = true;
        private string _searchPlaceholder = "Search navigation...";
        private string _searchText = "";
        private Rectangle _searchBoxRect;
        private Rectangle _clearSearchRect;
        private bool _isSearchFocused = false;

        private int _baseGroupHeaderHeight = 40;
        private int _baseItemHeight = 32;
        private int _baseWidth = 260;
        private int _groupHeaderHeight = 40;
        private int _itemHeight = 32;
        private int _scrollY = 0;
        private int _totalContentHeight = 0;
        private float _currentDpiScale = 1.0f;

        private object? _hoveredElement; // Can be AccordionGroup or AccordionItem
        private AccordionItem? _selectedItem;

        public event EventHandler<AccordionItem>? SelectedItemChanged;

        [Browsable(false)]
        public float DpiScale => _currentDpiScale;

        public void ApplyDpiScaling(float scaleFactor)
        {
            if (scaleFactor <= 0f) scaleFactor = 1.0f;
            _currentDpiScale = scaleFactor;

            _groupHeaderHeight = (int)Math.Round(_baseGroupHeaderHeight * scaleFactor);
            _itemHeight = (int)Math.Round(_baseItemHeight * scaleFactor);
            Width = (int)Math.Round(_baseWidth * scaleFactor);
            Font = new Font(Font.FontFamily, 9.25f * scaleFactor, Font.Style);
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            float factor = ZeroDpi.GetScaleFactor(this);
            if (Math.Abs(factor - _currentDpiScale) > 0.001f)
            {
                ApplyDpiScaling(factor);
            }
        }

        public ZAccordion()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.Selectable, true);

            DoubleBuffered = true;
            Width = _baseWidth;
            Height = 500;
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        [Category("Data")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public List<AccordionGroup> Groups => _groups;

        [Category("Behavior")]
        [DefaultValue(AccordionExpandMode.MultipleGroups)]
        public AccordionExpandMode ExpandMode
        {
            get => _expandMode;
            set => _expandMode = value;
        }

        [Category("Search")]
        [DefaultValue(true)]
        public bool ShowSearchBox
        {
            get => _showSearchBox;
            set
            {
                if (_showSearchBox != value)
                {
                    _showSearchBox = value;
                    Invalidate();
                }
            }
        }

        [Category("Search")]
        [DefaultValue("Search navigation...")]
        public string SearchPlaceholder
        {
            get => _searchPlaceholder;
            set
            {
                _searchPlaceholder = value ?? "";
                Invalidate();
            }
        }

        [Category("Behavior")]
        [Browsable(false)]
        public AccordionItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem != value)
                {
                    _selectedItem = value;
                    Invalidate();
                    if (_selectedItem != null) SelectedItemChanged?.Invoke(this, _selectedItem);
                }
            }
        }

        public AccordionGroup AddGroup(string text, string? glyph = null, bool isExpanded = true)
        {
            var grp = new AccordionGroup(text, glyph, isExpanded);
            _groups.Add(grp);
            Invalidate();
            return grp;
        }

        public void Clear()
        {
            _groups.Clear();
            _selectedItem = null;
            Invalidate();
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int maxScroll = Math.Max(0, _totalContentHeight - Height);
            _scrollY = Math.Max(0, Math.Min(maxScroll, _scrollY - (e.Delta / 120 * 40)));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = ZeroTheme.Palette;

            // 1. Fill Background
            using (var bgBrush = new SolidBrush(palette.Surface))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            int currentY = 8;

            // 2. Draw Search Box
            if (_showSearchBox)
            {
                _searchBoxRect = new Rectangle(12, currentY, Width - 24, 32);
                DrawSearchBox(g, palette);
                currentY += 40;
            }
            else
            {
                _searchBoxRect = Rectangle.Empty;
            }

            int contentStartY = currentY;
            currentY -= _scrollY;

            // 3. Layout and Draw Groups & Items
            using var clip = new Region(new Rectangle(0, contentStartY, Width, Height - contentStartY));
            var oldClip = g.Clip;
            g.Clip = clip;

            string filter = _searchText.Trim().ToLowerInvariant();

            for (int gi = 0; gi < _groups.Count; gi++)
            {
                var group = _groups[gi];

                // Filter logic: if search filter is active, check if group or any item matches
                bool matchesGroup = string.IsNullOrEmpty(filter) || group.Text.ToLowerInvariant().Contains(filter);
                bool hasMatchingChildren = false;

                for (int ii = 0; ii < group.Items.Count; ii++)
                {
                    if (string.IsNullOrEmpty(filter) || group.Items[ii].Text.ToLowerInvariant().Contains(filter))
                    {
                        hasMatchingChildren = true;
                        break;
                    }
                }

                if (!matchesGroup && !hasMatchingChildren) continue;

                bool forceExpanded = !string.IsNullOrEmpty(filter) && hasMatchingChildren;
                bool isExpanded = forceExpanded || group.IsExpanded;

                // Group Header
                group.Bounds = new Rectangle(8, currentY, Width - 16, _groupHeaderHeight);
                DrawGroupHeader(g, group, palette, isExpanded);
                currentY += _groupHeaderHeight + 2;

                // Group Items
                if (isExpanded)
                {
                    for (int ii = 0; ii < group.Items.Count; ii++)
                    {
                        var item = group.Items[ii];
                        if (!string.IsNullOrEmpty(filter) && !item.Text.ToLowerInvariant().Contains(filter))
                        {
                            continue;
                        }

                        item.Bounds = new Rectangle(16, currentY, Width - 32, _itemHeight);
                        DrawItem(g, item, palette);
                        currentY += _itemHeight + 2;
                    }
                }

                currentY += 4; // Spacing between groups
            }

            _totalContentHeight = (currentY + _scrollY) - contentStartY;
            g.Clip = oldClip;

            // Draw Right Border
            using (var borderPen = new Pen(palette.Border, 1f))
            {
                g.DrawLine(borderPen, Width - 1, 0, Width - 1, Height);
            }
        }

        private void DrawSearchBox(Graphics g, ZeroThemePalette palette)
        {
            Color boxBg = palette.Background;
            Color boxBorder = _isSearchFocused ? palette.Primary : palette.Border;

            using (var path = ZeroUIConfig.CreateRoundedRectangle(_searchBoxRect, 6))
            {
                using var bgBrush = new SolidBrush(boxBg);
                g.FillPath(bgBrush, path);
                using var borderPen = new Pen(boxBorder, 1f);
                g.DrawPath(borderPen, path);
            }

            // Search Icon
            Rectangle iconRect = new Rectangle(_searchBoxRect.Left + 8, _searchBoxRect.Top, 18, _searchBoxRect.Height);
            TextRenderer.DrawText(g, "🔍", ZeroFontCache.Get("Segoe UI", 9f), iconRect, palette.TextSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            // Text or Placeholder
            Rectangle textRect = new Rectangle(_searchBoxRect.Left + 28, _searchBoxRect.Top, _searchBoxRect.Width - 48, _searchBoxRect.Height);
            if (string.IsNullOrEmpty(_searchText))
            {
                TextRenderer.DrawText(g, _searchPlaceholder, Font, textRect, palette.TextSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
            else
            {
                TextRenderer.DrawText(g, _searchText, Font, textRect, palette.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                // Clear button (✕)
                _clearSearchRect = new Rectangle(_searchBoxRect.Right - 22, _searchBoxRect.Top + 6, 18, 18);
                TextRenderer.DrawText(g, "✕", Font, _clearSearchRect, palette.TextSecondary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private void DrawGroupHeader(Graphics g, AccordionGroup group, ZeroThemePalette palette, bool isExpanded)
        {
            bool isHovered = (_hoveredElement == group);

            if (isHovered)
            {
                using var path = ZeroUIConfig.CreateRoundedRectangle(group.Bounds, 6);
                using var hoverBrush = new SolidBrush(palette.Hover);
                g.FillPath(hoverBrush, path);
            }

            int x = group.Bounds.Left + 8;

            // Glyph
            if (!string.IsNullOrEmpty(group.Glyph))
            {
                Rectangle glyphRect = new Rectangle(x, group.Bounds.Top, 22, group.Bounds.Height);
                TextRenderer.DrawText(g, group.Glyph, ZeroFontCache.Get("Segoe UI", 10.5f), glyphRect, palette.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                x += 24;
            }

            // Text
            using var titleFont = new Font(Font.FontFamily, Font.Size, FontStyle.Bold);
            Rectangle textRect = new Rectangle(x, group.Bounds.Top, group.Bounds.Width - x - 50, group.Bounds.Height);
            TextRenderer.DrawText(g, group.Text, titleFont, textRect, palette.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // Badge
            if (!string.IsNullOrEmpty(group.BadgeText))
            {
                Color bColor = group.BadgeColor ?? palette.Primary;
                Size bSize = TextRenderer.MeasureText(g, group.BadgeText, Font);
                Rectangle bRect = new Rectangle(group.Bounds.Right - 42 - bSize.Width, group.Bounds.Top + (group.Bounds.Height - 18) / 2, bSize.Width + 8, 18);
                using var bPath = ZeroUIConfig.CreateRoundedRectangle(bRect, 9);
                using var bBrush = new SolidBrush(bColor);
                g.FillPath(bBrush, bPath);
                TextRenderer.DrawText(g, group.BadgeText, ZeroFontCache.Get(Font.FontFamily.Name, 7.5f, FontStyle.Bold), bRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            // Chevron (▼ or ▶)
            group.ChevronBounds = new Rectangle(group.Bounds.Right - 22, group.Bounds.Top, 18, group.Bounds.Height);
            string chevron = isExpanded ? "▼" : "▶";
            TextRenderer.DrawText(g, chevron, ZeroFontCache.Get("Segoe UI", 7.5f), group.ChevronBounds, palette.TextSecondary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void DrawItem(Graphics g, AccordionItem item, ZeroThemePalette palette)
        {
            bool isSelected = (item == _selectedItem);
            bool isHovered = (_hoveredElement == item);

            using var path = ZeroUIConfig.CreateRoundedRectangle(item.Bounds, 6);

            if (isSelected)
            {
                using var selBrush = new SolidBrush(palette.Primary);
                g.FillPath(selBrush, path);
            }
            else if (isHovered)
            {
                using var hoverBrush = new SolidBrush(palette.Hover);
                g.FillPath(hoverBrush, path);
            }

            Color textColor = isSelected ? Color.White : (item.IsEnabled ? palette.TextPrimary : palette.TextSecondary);

            int x = item.Bounds.Left + 10;

            // Glyph
            if (!string.IsNullOrEmpty(item.Glyph))
            {
                Rectangle glyphRect = new Rectangle(x, item.Bounds.Top, 18, item.Bounds.Height);
                TextRenderer.DrawText(g, item.Glyph, ZeroFontCache.Get("Segoe UI", 9f), glyphRect, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                x += 20;
            }

            // Text
            Rectangle textRect = new Rectangle(x, item.Bounds.Top, item.Bounds.Width - x - 40, item.Bounds.Height);
            TextRenderer.DrawText(g, item.Text, Font, textRect, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // Badge
            if (!string.IsNullOrEmpty(item.BadgeText))
            {
                Color bColor = isSelected ? Color.FromArgb(255, 255, 255, 40) : (item.BadgeColor ?? palette.Primary);
                Color bTextColor = isSelected ? Color.White : Color.White;

                Size bSize = TextRenderer.MeasureText(g, item.BadgeText, Font);
                Rectangle bRect = new Rectangle(item.Bounds.Right - 8 - bSize.Width, item.Bounds.Top + (item.Bounds.Height - 16) / 2, bSize.Width + 8, 16);
                using var bPath = ZeroUIConfig.CreateRoundedRectangle(bRect, 8);
                using var bBrush = new SolidBrush(bColor);
                g.FillPath(bBrush, bPath);
                TextRenderer.DrawText(g, item.BadgeText, ZeroFontCache.Get(Font.FontFamily.Name, 7f, FontStyle.Bold), bRect, bTextColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            object? hit = null;

            for (int gi = 0; gi < _groups.Count; gi++)
            {
                var g = _groups[gi];
                if (g.Bounds.Contains(e.Location))
                {
                    hit = g;
                    break;
                }

                if (g.IsExpanded || !string.IsNullOrEmpty(_searchText))
                {
                    for (int ii = 0; ii < g.Items.Count; ii++)
                    {
                        var it = g.Items[ii];
                        if (it.Bounds.Contains(e.Location))
                        {
                            hit = it;
                            break;
                        }
                    }
                }
                if (hit != null) break;
            }

            if (_hoveredElement != hit)
            {
                _hoveredElement = hit;
                Cursor = hit != null ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredElement != null)
            {
                _hoveredElement = null;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (_showSearchBox)
            {
                if (!_clearSearchRect.IsEmpty && _clearSearchRect.Contains(e.Location))
                {
                    _searchText = "";
                    Invalidate();
                    return;
                }

                if (_searchBoxRect.Contains(e.Location))
                {
                    _isSearchFocused = true;
                    Focus();
                    Invalidate();
                    return;
                }
                else
                {
                    _isSearchFocused = false;
                }
            }

            if (_hoveredElement is AccordionGroup grp)
            {
                if (_expandMode == AccordionExpandMode.SingleGroup && !grp.IsExpanded)
                {
                    // Collapse all others
                    for (int i = 0; i < _groups.Count; i++)
                    {
                        _groups[i].IsExpanded = false;
                    }
                    grp.IsExpanded = true;
                }
                else
                {
                    grp.IsExpanded = !grp.IsExpanded;
                }
                Invalidate();
            }
            else if (_hoveredElement is AccordionItem item && item.IsEnabled)
            {
                SelectedItem = item;
                item.OnClick();
            }
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);

            if (_isSearchFocused && _showSearchBox)
            {
                if (e.KeyChar == (char)Keys.Back)
                {
                    if (_searchText.Length > 0)
                    {
                        _searchText = _searchText.Substring(0, _searchText.Length - 1);
                        Invalidate();
                    }
                    e.Handled = true;
                }
                else if (!char.IsControl(e.KeyChar))
                {
                    _searchText += e.KeyChar;
                    Invalidate();
                    e.Handled = true;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ZeroTheme.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }
    }

    [Obsolete("Use AccordionItem instead.")]
    public class ZeroAccordionItem : AccordionItem
    {
        public ZeroAccordionItem() : base() { }
        public ZeroAccordionItem(string text, string? glyph = null, EventHandler? onClick = null, string? badge = null)
            : base(text, glyph, onClick, badge) { }
    }

    [Obsolete("Use AccordionGroup instead.")]
    public class ZeroAccordionGroup : AccordionGroup
    {
        public ZeroAccordionGroup() : base() { }
        public ZeroAccordionGroup(string text, string? glyph = null, bool isExpanded = true)
            : base(text, glyph, isExpanded) { }
    }

    [Obsolete("Use AccordionControl instead.")]
    [ToolboxItem(false)]
    public class ZeroAccordion : AccordionControl
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZAccordion"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("AccordionControl is deprecated and will be removed in 5 release cycles. Please migrate to ZAccordion instead.")]
    [ToolboxItem(false)]
    public class AccordionControl : ZAccordion
    {
    }

    #endregion
}
