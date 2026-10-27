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
    public enum TabStyle
    {
        Underline,
        Pill,
        Card
    }

    public enum TabOrientation
    {
        Horizontal,
        Vertical
    }

    /// <summary>
    /// Represents an individual tab page container inside TabControlEx.
    /// Inherits from Panel to allow hosting child controls with zero layout constraints.
    /// </summary>
    public class TabPageEx : Panel
    {
        public string Title { get; set; } = "New Tab";
        public string Icon { get; set; } = "";
        public int BadgeCount { get; set; } = 0;
        public Color? BadgeColor { get; set; }
        public bool Closable { get; set; } = false;

        /// <summary>
        /// Optional delegate to lazily initialize tab contents when this page is first selected.
        /// </summary>
        public Action<TabPageEx>? LazyInitializer { get; set; }

        /// <summary>
        /// Gets or sets whether the tab page content has already been initialized.
        /// </summary>
        public bool IsInitialized { get; set; } = false;

        internal Rectangle HeaderBounds { get; set; }
        internal Rectangle CloseButtonBounds { get; set; }

        public TabPageEx()
        {
            Dock = DockStyle.Fill;
            Visible = false;
            AutoScroll = true;
        }

        public TabPageEx(string title, string icon = "") : this()
        {
            Title = title;
            Icon = icon;
        }

        public TabPageEx(string title, string icon, Action<TabPageEx> lazyInitializer) : this(title, icon)
        {
            LazyInitializer = lazyInitializer;
        }
    }

    /// <summary>
    /// Modern anti-aliased flat TabControl and container for ZeroUI.
    /// Supports Horizontal & Vertical orientations, Underline/Pill styles,
    /// tab notification badges, icons, and 100% seamless Obsidian Dark / Clean Light theming.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Overlays & Navigation")]
    [DefaultEvent("SelectedIndexChanged")]
    [DefaultProperty("SelectedIndex")]
    [Description("Modern flat TabControl container with Horizontal/Vertical orientations, Underline/Pill styles and notification badges")]
    [ToolboxBitmap(typeof(ZeroIcons), "TabControlEx.bmp")]
    public class TabControlEx : Control, IZeroDpiScalable
    {
        private readonly List<TabPageEx> _tabPages = new List<TabPageEx>();
        private readonly Panel _contentContainer;

        private int _selectedIndex = -1;
        private int _hoveredIndex = -1;
        private int _hoveredCloseIndex = -1;
        private int _baseTabHeight = 42;
        private int _baseTabWidth = 260;
        private float _currentDpiScale = 1.0f;
        private int _tabHeight = 42;
        private int _tabWidth = 260;
        private TabStyle _tabStyle = TabStyle.Underline;
        private TabOrientation _orientation = TabOrientation.Horizontal;
        private bool _showHeader = true;

        // Tab header scrolling state
        private int _scrollOffset = 0;
        private int _maxScrollOffset = 0;
        private bool _canScrollLeft = false;
        private bool _canScrollRight = false;
        private Rectangle _btnScrollLeftRect = Rectangle.Empty;
        private Rectangle _btnScrollRightRect = Rectangle.Empty;
        private bool _hoveredScrollLeft = false;
        private bool _hoveredScrollRight = false;
        private const int ScrollStep = 140;
        private const int NavButtonsWidth = 56;

        public event EventHandler? SelectedIndexChanged;
        public event EventHandler<TabPageEx>? TabClosed;

        /// <summary>
        /// Gets the active High-DPI Per-Monitor V2 scale factor applied to this TabControl.
        /// </summary>
        [Browsable(false)]
        public float DpiScale => _currentDpiScale;

        /// <summary>
        /// Applies High-DPI Per-Monitor V2 scaling to TabControl metrics (TabWidth, TabHeight, and container layout).
        /// </summary>
        public void ApplyDpiScaling(float scaleFactor)
        {
            if (scaleFactor <= 0f) scaleFactor = 1.0f;
            _currentDpiScale = scaleFactor;

            _tabHeight = Math.Max(24, (int)Math.Round(_baseTabHeight * scaleFactor));
            _tabWidth = Math.Max(40, (int)Math.Round(_baseTabWidth * scaleFactor));

            UpdateContainerBounds();
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

        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            base.ScaleControl(factor, specified);
            float effectiveFactor = factor.Height > 0f ? factor.Height : factor.Width;
            if (effectiveFactor > 0f && Math.Abs(effectiveFactor - 1.0f) > 0.001f)
            {
                ApplyDpiScaling(_currentDpiScale * effectiveFactor);
            }
        }

        public TabControlEx()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            BackColor = Color.Transparent;

            _contentContainer = new Panel
            {
                Dock = DockStyle.None,
                BackColor = Color.Transparent
            };
            Controls.Add(_contentContainer);

            Size = new Size(500, 350);
            UpdateContainerBounds();

            ZeroTheme.ThemeChanged += OnThemeChanged;
            ZeroUIConfig.CornerStyleChanged += (s, e) => Invalidate();
            ZeroUIConfig.FontChanged += (s, e) =>
            {
                Font = ZeroUIConfig.DefaultFont;
                Invalidate();
            };
        }

        [Browsable(false)]
        public List<TabPageEx> TabPages => _tabPages;

        [Category("Appearance")]
        [DefaultValue(TabOrientation.Horizontal)]
        public TabOrientation Orientation
        {
            get => _orientation;
            set
            {
                if (_orientation != value)
                {
                    _orientation = value;
                    UpdateContainerBounds();
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowHeader
        {
            get => _showHeader;
            set
            {
                if (_showHeader != value)
                {
                    _showHeader = value;
                    UpdateContainerBounds();
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(200)]
        public int TabWidth
        {
            get => _tabWidth;
            set
            {
                if (_tabWidth != value && value >= 30)
                {
                    _baseTabWidth = (int)Math.Round(value / _currentDpiScale);
                    _tabWidth = value;
                    if (_orientation == TabOrientation.Vertical)
                    {
                        UpdateContainerBounds();
                    }
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(42)]
        public int TabHeight
        {
            get => _tabHeight;
            set
            {
                if (_tabHeight != value && value >= 16)
                {
                    _baseTabHeight = (int)Math.Round(value / _currentDpiScale);
                    _tabHeight = value;
                    if (_orientation == TabOrientation.Horizontal)
                    {
                        UpdateContainerBounds();
                    }
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(TabStyle.Underline)]
        public TabStyle TabStyle
        {
            get => _tabStyle;
            set
            {
                if (_tabStyle != value)
                {
                    _tabStyle = value;
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(-1)]
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_tabPages.Count == 0)
                {
                    _selectedIndex = -1;
                    return;
                }

                int clamped = Math.Max(0, Math.Min(_tabPages.Count - 1, value));
                if (_selectedIndex != clamped)
                {
                    _selectedIndex = clamped;
                    UpdateActiveTabContent();
                    EnsureTabVisible(_selectedIndex);
                    SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        [Browsable(false)]
        public TabPageEx? SelectedTab
        {
            get => (_selectedIndex >= 0 && _selectedIndex < _tabPages.Count) ? _tabPages[_selectedIndex] : null;
            set
            {
                if (value == null)
                {
                    SelectedIndex = -1;
                }
                else
                {
                    int index = _tabPages.IndexOf(value);
                    if (index >= 0)
                    {
                        SelectedIndex = index;
                    }
                }
            }
        }

        public TabPageEx AddTab(string title, string icon = "", int badgeCount = 0)
        {
            var page = new TabPageEx(title, icon) { BadgeCount = badgeCount };
            AddTab(page);
            return page;
        }

        public TabPageEx AddLazyTab(string title, string icon, Action<TabPageEx> initializer, int badgeCount = 0)
        {
            var page = new TabPageEx(title, icon, initializer) { BadgeCount = badgeCount };
            AddTab(page);
            return page;
        }

        public void AddTab(TabPageEx page)
        {
            _tabPages.Add(page);
            _contentContainer.Controls.Add(page);
            if (_selectedIndex == -1)
            {
                SelectedIndex = 0;
            }
            Invalidate();
        }

        public void RemoveTab(TabPageEx page)
        {
            int idx = _tabPages.IndexOf(page);
            if (idx >= 0)
            {
                _tabPages.RemoveAt(idx);
                _contentContainer.Controls.Remove(page);
                page.Dispose();

                if (_selectedIndex >= _tabPages.Count)
                {
                    _selectedIndex = _tabPages.Count - 1;
                }
                UpdateActiveTabContent();
                TabClosed?.Invoke(this, page);
                Invalidate();
            }
        }

        private void UpdateContainerBounds()
        {
            if (_contentContainer == null) return;

            if (!_showHeader)
            {
                _contentContainer.Location = new Point(0, 0);
                _contentContainer.Size = new Size(Width, Height);
                return;
            }

            if (_orientation == TabOrientation.Vertical)
            {
                _contentContainer.Location = new Point(_tabWidth, 0);
                _contentContainer.Size = new Size(Math.Max(0, Width - _tabWidth), Height);
            }
            else
            {
                _contentContainer.Location = new Point(0, _tabHeight);
                _contentContainer.Size = new Size(Width, Math.Max(0, Height - _tabHeight));
            }
        }

        private void UpdateActiveTabContent()
        {
            _contentContainer.SuspendLayout();
            for (int i = 0; i < _tabPages.Count; i++)
            {
                bool active = (i == _selectedIndex);
                var page = _tabPages[i];
                page.Visible = active;
                if (active)
                {
                    if (!page.IsInitialized && page.LazyInitializer != null)
                    {
                        page.IsInitialized = true;
                        try
                        {
                            page.LazyInitializer(page);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[TabControlEx] Error initializing lazy tab '{page.Title}': {ex}");
                        }
                    }
                    page.BringToFront();
                }
            }
            _contentContainer.ResumeLayout(true);
        }

        public void EnsureTabVisible(int index)
        {
            if (index < 0 || index >= _tabPages.Count || _orientation != TabOrientation.Horizontal)
                return;

            int targetLeft = 12;
            using var g = CreateGraphics();
            var fontTab = ZeroFontCache.Get(9.2f, FontStyle.Regular);
            var fontTabActive = ZeroFontCache.Get(9.2f, FontStyle.Bold);

            int targetWidth = 0;
            for (int i = 0; i <= index; i++)
            {
                var page = _tabPages[i];
                bool isSelected = (i == _selectedIndex);
                var activeFont = isSelected ? fontTabActive : fontTab;
                var textSz = g.MeasureString(page.Title, activeFont);
                int itemW = (int)textSz.Width + 24;
                if (!string.IsNullOrEmpty(page.Icon)) itemW += 20;
                if (page.BadgeCount > 0) itemW += 24;
                if (page.Closable) itemW += 18;

                if (i == index)
                {
                    targetWidth = itemW;
                    break;
                }
                targetLeft += itemW + 4;
            }

            int visibleWidth = Math.Max(100, Width - NavButtonsWidth);
            if (targetLeft < _scrollOffset)
            {
                _scrollOffset = Math.Max(0, targetLeft - 12);
                Invalidate();
            }
            else if (targetLeft + targetWidth > _scrollOffset + visibleWidth)
            {
                _scrollOffset = Math.Max(0, (targetLeft + targetWidth + 16) - visibleWidth);
                Invalidate();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateContainerBounds();
            if (_selectedIndex >= 0)
            {
                EnsureTabVisible(_selectedIndex);
            }
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_orientation == TabOrientation.Horizontal && e.Y <= _tabHeight && _maxScrollOffset > 0)
            {
                int delta = (e.Delta > 0) ? -ScrollStep : ScrollStep;
                int newOffset = Math.Max(0, Math.Min(_maxScrollOffset, _scrollOffset + delta));
                if (newOffset != _scrollOffset)
                {
                    _scrollOffset = newOffset;
                    Invalidate();
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_showHeader) return;
            if (_orientation == TabOrientation.Horizontal && e.Y > _tabHeight) return;
            if (_orientation == TabOrientation.Vertical && e.X > _tabWidth) return;

            // Check navigation buttons hover in horizontal mode
            if (_orientation == TabOrientation.Horizontal && !_btnScrollLeftRect.IsEmpty)
            {
                bool hovLeft = _btnScrollLeftRect.Contains(e.Location) && _canScrollLeft;
                bool hovRight = _btnScrollRightRect.Contains(e.Location) && _canScrollRight;
                if (hovLeft != _hoveredScrollLeft || hovRight != _hoveredScrollRight)
                {
                    _hoveredScrollLeft = hovLeft;
                    _hoveredScrollRight = hovRight;
                    Cursor = (hovLeft || hovRight) ? Cursors.Hand : Cursors.Default;
                    Invalidate();
                }
                if (e.X >= Width - NavButtonsWidth)
                {
                    return;
                }
            }

            int hov = -1;
            int hovClose = -1;
            int visibleWidth = (_btnScrollLeftRect.IsEmpty) ? Width : Math.Max(0, Width - NavButtonsWidth);

            for (int i = 0; i < _tabPages.Count; i++)
            {
                var bounds = _tabPages[i].HeaderBounds;
                if (_orientation == TabOrientation.Horizontal && bounds.Right > visibleWidth + 2)
                    continue;

                if (bounds.Contains(e.Location))
                {
                    hov = i;
                    if (_tabPages[i].Closable && _tabPages[i].CloseButtonBounds.Contains(e.Location))
                    {
                        hovClose = i;
                    }
                    break;
                }
            }

            if (_hoveredIndex != hov || _hoveredCloseIndex != hovClose)
            {
                _hoveredIndex = hov;
                _hoveredCloseIndex = hovClose;
                Cursor = (hovClose >= 0) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!_showHeader) return;
            if (_hoveredIndex != -1 || _hoveredCloseIndex != -1 || _hoveredScrollLeft || _hoveredScrollRight)
            {
                _hoveredIndex = -1;
                _hoveredCloseIndex = -1;
                _hoveredScrollLeft = false;
                _hoveredScrollRight = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!_showHeader) return;
            if (e.Button != MouseButtons.Left) return;
            if (_orientation == TabOrientation.Horizontal && e.Y > _tabHeight) return;
            if (_orientation == TabOrientation.Vertical && e.X > _tabWidth) return;

            // Handle scroll buttons click
            if (_orientation == TabOrientation.Horizontal && !_btnScrollLeftRect.IsEmpty)
            {
                if (_btnScrollLeftRect.Contains(e.Location) && _canScrollLeft)
                {
                    _scrollOffset = Math.Max(0, _scrollOffset - ScrollStep);
                    Invalidate();
                    return;
                }
                if (_btnScrollRightRect.Contains(e.Location) && _canScrollRight)
                {
                    _scrollOffset = Math.Min(_maxScrollOffset, _scrollOffset + ScrollStep);
                    Invalidate();
                    return;
                }
                if (e.X >= Width - NavButtonsWidth)
                {
                    return;
                }
            }

            int visibleWidth = (_btnScrollLeftRect.IsEmpty) ? Width : Math.Max(0, Width - NavButtonsWidth);

            for (int i = 0; i < _tabPages.Count; i++)
            {
                var bounds = _tabPages[i].HeaderBounds;
                if (_orientation == TabOrientation.Horizontal && bounds.Right > visibleWidth + 2)
                    continue;

                if (bounds.Contains(e.Location))
                {
                    if (_tabPages[i].Closable && _tabPages[i].CloseButtonBounds.Contains(e.Location))
                    {
                        RemoveTab(_tabPages[i]);
                        return;
                    }

                    SelectedIndex = i;
                    return;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!_showHeader) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = ZeroTheme.Colors;

            if (_orientation == TabOrientation.Vertical)
            {
                PaintVerticalTabs(g, palette);
            }
            else
            {
                PaintHorizontalTabs(g, palette);
            }
        }

        private void PaintHorizontalTabs(Graphics g, ZeroThemePalette palette)
        {
            // 1. Tab Header Bar Background
            var headerRect = new Rectangle(0, 0, Width, _tabHeight);
            using (var brushHeader = new SolidBrush(palette.HeaderBackground))
            {
                g.FillRectangle(brushHeader, headerRect);
            }

            // Bottom border of tab strip
            using (var penBottom = new Pen(palette.Border, 1f))
            {
                g.DrawLine(penBottom, 0, _tabHeight - 1, Width, _tabHeight - 1);
            }

            if (_tabPages.Count == 0) return;

            var fontTab = ZeroFontCache.Get(9.2f, FontStyle.Regular);
            var fontTabActive = ZeroFontCache.Get(9.2f, FontStyle.Bold);
            var fontIcon = ZeroFontCache.Get("Segoe UI Emoji", 9.5f, FontStyle.Regular);
            var fontBadge = ZeroFontCache.Get(7.5f, FontStyle.Bold);

            // Pre-calculate tab widths to determine scroll necessity
            int totalTabsWidth = 12;
            int[] itemWidths = new int[_tabPages.Count];
            for (int i = 0; i < _tabPages.Count; i++)
            {
                var page = _tabPages[i];
                bool isSelected = (i == _selectedIndex);
                var activeFont = isSelected ? fontTabActive : fontTab;
                var textSz = g.MeasureString(page.Title, activeFont);
                int itemW = (int)textSz.Width + 24;
                if (!string.IsNullOrEmpty(page.Icon)) itemW += 20;
                if (page.BadgeCount > 0) itemW += 24;
                if (page.Closable) itemW += 18;
                itemWidths[i] = itemW;
                totalTabsWidth += itemW + 4;
            }
            totalTabsWidth += 12;

            bool needScroll = totalTabsWidth > Width;
            int visibleWidth = needScroll ? Math.Max(0, Width - NavButtonsWidth) : Width;
            _maxScrollOffset = needScroll ? Math.Max(0, totalTabsWidth - visibleWidth) : 0;
            _scrollOffset = Math.Max(0, Math.Min(_maxScrollOffset, _scrollOffset));

            _canScrollLeft = needScroll && (_scrollOffset > 0);
            _canScrollRight = needScroll && (_scrollOffset < _maxScrollOffset);

            if (needScroll)
            {
                int btnY = (_tabHeight - 26) / 2;
                _btnScrollLeftRect = new Rectangle(Width - NavButtonsWidth + 4, btnY, 22, 26);
                _btnScrollRightRect = new Rectangle(Width - NavButtonsWidth + 30, btnY, 22, 26);
            }
            else
            {
                _btnScrollLeftRect = Rectangle.Empty;
                _btnScrollRightRect = Rectangle.Empty;
            }

            // Clip tabs rendering within visible header area
            var origClip = g.Clip;
            g.SetClip(new Rectangle(0, 0, visibleWidth, _tabHeight));

            int curX = 12 - _scrollOffset;

            for (int i = 0; i < _tabPages.Count; i++)
            {
                var page = _tabPages[i];
                bool isSelected = i == _selectedIndex;
                bool isHovered = i == _hoveredIndex;
                int itemW = itemWidths[i];

                page.HeaderBounds = new Rectangle(curX, 0, itemW, _tabHeight);

                // Only render if within visible viewport
                if (curX + itemW >= -20 && curX <= visibleWidth + 20)
                {
                    // Draw Tab Shape based on style
                    if (_tabStyle == TabStyle.Pill)
                    {
                        int pillH = _tabHeight - 12;
                        var pillRect = new Rectangle(curX + 2, 6, itemW - 4, pillH);
                        int effRadius = ZeroUIConfig.GetEffectiveRadius(6);
                        if (isSelected)
                        {
                            using var brushPill = new SolidBrush(palette.Primary);
                            using var pathPill = CreateRoundedRect(pillRect, effRadius);
                            g.FillPath(brushPill, pathPill);
                        }
                        else if (isHovered)
                        {
                            using var brushPillHov = new SolidBrush(Color.FromArgb(20, palette.Primary));
                            using var pathPillHov = CreateRoundedRect(pillRect, effRadius);
                            g.FillPath(brushPillHov, pathPillHov);
                        }
                    }
                    else if (_tabStyle == TabStyle.Card)
                    {
                        if (isSelected)
                        {
                            var cardRect = new Rectangle(curX, 4, itemW, _tabHeight - 4);
                            int effRadius = ZeroUIConfig.GetEffectiveRadius(6);
                            using var brushCard = new SolidBrush(palette.Background);
                            using var pathCard = CreateTopRoundedRect(cardRect, effRadius);
                            g.FillPath(brushCard, pathCard);
                            using var penCard = new Pen(palette.Border, 1f);
                            g.DrawPath(penCard, pathCard);
                        }
                    }
                    else // Underline
                    {
                        if (isHovered && !isSelected)
                        {
                            using var brushHov = new SolidBrush(Color.FromArgb(10, palette.Primary));
                            g.FillRectangle(brushHov, page.HeaderBounds);
                        }

                        if (isSelected)
                        {
                            int barH = 3;
                            var barRect = new Rectangle(curX + 6, _tabHeight - barH, itemW - 12, barH);
                            using var brushBar = new SolidBrush(palette.Primary);
                            using var pathBar = CreateRoundedRect(barRect, 2);
                            g.FillPath(brushBar, pathBar);
                        }
                    }

                    int innerX = curX + 12;

                    // Draw Icon
                    if (!string.IsNullOrEmpty(page.Icon))
                    {
                        using var brushIcon = new SolidBrush(isSelected && _tabStyle == TabStyle.Pill ? Color.White : palette.TextPrimary);
                        g.DrawString(page.Icon, fontIcon, brushIcon, innerX, (_tabHeight - 18) / 2);
                        innerX += 20;
                    }

                    // Draw Tab Title
                    Color textCol;
                    if (_tabStyle == TabStyle.Pill && isSelected) textCol = Color.White;
                    else if (isSelected) textCol = palette.Primary;
                    else if (isHovered) textCol = palette.TextPrimary;
                    else textCol = palette.TextSecondary;

                    var activeFont = isSelected ? fontTabActive : fontTab;
                    using (var brushText = new SolidBrush(textCol))
                    {
                        g.DrawString(page.Title, activeFont, brushText, innerX, (_tabHeight - 18) / 2);
                        var textSz = g.MeasureString(page.Title, activeFont);
                        innerX += (int)textSz.Width + 6;
                    }

                    // Draw Badge
                    if (page.BadgeCount > 0)
                    {
                        string bStr = page.BadgeCount > 99 ? "99+" : page.BadgeCount.ToString();
                        var bSz = g.MeasureString(bStr, fontBadge);
                        int bW = Math.Max(18, (int)bSz.Width + 8);
                        int bH = 16;
                        var bRect = new Rectangle(innerX, (_tabHeight - bH) / 2, bW, bH);

                        Color bColor = page.BadgeColor ?? palette.Danger;
                        using var brushBadge = new SolidBrush(bColor);
                        using var pathBadge = CreateRoundedRect(bRect, 8);
                        g.FillPath(brushBadge, pathBadge);

                        using var brushBadgeText = new SolidBrush(Color.White);
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(bStr, fontBadge, brushBadgeText, bRect, sf);
                        innerX += bW + 6;
                    }

                    // Draw Close Button (✕)
                    if (page.Closable)
                    {
                        page.CloseButtonBounds = new Rectangle(innerX, (_tabHeight - 14) / 2, 14, 14);
                        bool hovClose = _hoveredCloseIndex == i;

                        Color closeC = hovClose ? palette.Danger : palette.TextSecondary;
                        using var brushClose = new SolidBrush(closeC);
                        var sfClose = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("✕", fontBadge, brushClose, page.CloseButtonBounds, sfClose);
                    }
                }

                curX += itemW + 4;
            }

            g.Clip = origClip;

            // Render navigation arrows if header overflows
            if (needScroll)
            {
                var navAreaRect = new Rectangle(Width - NavButtonsWidth, 0, NavButtonsWidth, _tabHeight);
                using (var brushNavBg = new SolidBrush(palette.HeaderBackground))
                {
                    g.FillRectangle(brushNavBg, navAreaRect);
                }
                using (var penSep = new Pen(palette.Border, 1f))
                {
                    g.DrawLine(penSep, Width - NavButtonsWidth, 4, Width - NavButtonsWidth, _tabHeight - 5);
                }

                DrawScrollButton(g, _btnScrollLeftRect, "◀", _canScrollLeft, _hoveredScrollLeft, palette);
                DrawScrollButton(g, _btnScrollRightRect, "▶", _canScrollRight, _hoveredScrollRight, palette);
            }
        }

        private void DrawScrollButton(Graphics g, Rectangle rect, string arrow, bool enabled, bool hovered, ZeroThemePalette palette)
        {
            if (rect.IsEmpty) return;

            Color bgColor = hovered && enabled ? Color.FromArgb(40, palette.Primary) : Color.Transparent;
            Color textColor = enabled ? (hovered ? palette.Primary : palette.TextPrimary) : Color.FromArgb(100, palette.TextSecondary);

            if (bgColor != Color.Transparent)
            {
                using var brushBg = new SolidBrush(bgColor);
                using var pathBg = CreateRoundedRect(rect, 4);
                g.FillPath(brushBg, pathBg);
            }

            var fontArrow = ZeroFontCache.Get(8.5f, FontStyle.Bold);
            using var brushText = new SolidBrush(textColor);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(arrow, fontArrow, brushText, rect, sf);
        }

        private void PaintVerticalTabs(Graphics g, ZeroThemePalette palette)
        {
            // 1. Vertical Sidebar Background
            var sidebarRect = new Rectangle(0, 0, _tabWidth, Height);
            using (var brushHeader = new SolidBrush(palette.HeaderBackground))
            {
                g.FillRectangle(brushHeader, sidebarRect);
            }

            // Right border separating sidebar from content
            using (var penRight = new Pen(palette.Border, 1f))
            {
                g.DrawLine(penRight, _tabWidth - 1, 0, _tabWidth - 1, Height);
            }

            if (_tabPages.Count == 0) return;

            int curY = 8;
            int itemW = _tabWidth - 16;
            int itemH = Math.Max(36, _tabHeight);

            var fontTab = ZeroFontCache.Get(9.2f, FontStyle.Regular);
            var fontTabActive = ZeroFontCache.Get(9.2f, FontStyle.Bold);
            var fontIcon = ZeroFontCache.Get("Segoe UI Emoji", 10.5f, FontStyle.Regular);
            var fontBadge = ZeroFontCache.Get(7.5f, FontStyle.Bold);

            for (int i = 0; i < _tabPages.Count; i++)
            {
                var page = _tabPages[i];
                bool isSelected = i == _selectedIndex;
                bool isHovered = i == _hoveredIndex;

                var activeFont = isSelected ? fontTabActive : fontTab;
                page.HeaderBounds = new Rectangle(8, curY, itemW, itemH);

                int effRadius = ZeroUIConfig.GetEffectiveRadius(6);

                // Draw item background based on selection/hover
                if (isSelected)
                {
                    if (_tabStyle == TabStyle.Pill)
                    {
                        using var brushPill = new SolidBrush(palette.Primary);
                        using var pathPill = CreateRoundedRect(page.HeaderBounds, effRadius);
                        g.FillPath(brushPill, pathPill);
                    }
                    else // Underline or Card
                    {
                        using var brushSel = new SolidBrush(Color.FromArgb(20, palette.Primary));
                        using var pathSel = CreateRoundedRect(page.HeaderBounds, effRadius);
                        g.FillPath(brushSel, pathSel);

                        // Left vertical accent indicator
                        using var brushAccent = new SolidBrush(palette.Primary);
                        using var pathAccent = CreateRoundedRect(new Rectangle(0, curY + 4, 4, itemH - 8), 2);
                        g.FillPath(brushAccent, pathAccent);
                    }
                }
                else if (isHovered)
                {
                    using var brushHov = new SolidBrush(Color.FromArgb(12, palette.Primary));
                    using var pathHov = CreateRoundedRect(page.HeaderBounds, effRadius);
                    g.FillPath(brushHov, pathHov);
                }

                int innerX = page.HeaderBounds.X + 10;
                int textY = curY + (itemH - 18) / 2;

                // Draw Icon
                if (!string.IsNullOrEmpty(page.Icon))
                {
                    Color iconCol = (isSelected && _tabStyle == TabStyle.Pill) ? Color.White : (isSelected ? palette.Primary : palette.TextPrimary);
                    using var brushIcon = new SolidBrush(iconCol);
                    g.DrawString(page.Icon, fontIcon, brushIcon, innerX, textY - 1);
                    innerX += 24;
                }

                // Draw Title Text
                Color textCol;
                if (_tabStyle == TabStyle.Pill && isSelected) textCol = Color.White;
                else if (isSelected) textCol = palette.Primary;
                else if (isHovered) textCol = palette.TextPrimary;
                else textCol = palette.TextSecondary;

                int maxTextW = page.HeaderBounds.Right - innerX - (page.BadgeCount > 0 ? 36 : 8);
                RectangleF textRect = new RectangleF(innerX, textY, maxTextW, 20);

                using (var brushText = new SolidBrush(textCol))
                using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(page.Title, activeFont, brushText, textRect, sf);
                }

                // Draw Badge (Aligned to right edge of tab)
                if (page.BadgeCount > 0)
                {
                    string bStr = page.BadgeCount > 99 ? "99+" : page.BadgeCount.ToString();
                    var bSz = g.MeasureString(bStr, fontBadge);
                    int bW = Math.Max(18, (int)bSz.Width + 8);
                    int bH = 16;
                    int bX = page.HeaderBounds.Right - bW - 8;
                    var bRect = new Rectangle(bX, curY + (itemH - bH) / 2, bW, bH);

                    Color bColor = page.BadgeColor ?? palette.Danger;
                    using var brushBadge = new SolidBrush(bColor);
                    using var pathBadge = CreateRoundedRect(bRect, 8);
                    g.FillPath(brushBadge, pathBadge);

                    using var brushBadgeText = new SolidBrush(Color.White);
                    var sfBadge = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(bStr, fontBadge, brushBadgeText, bRect, sfBadge);
                }

                curY += itemH + 4;
            }
        }

        private static GraphicsPath CreateRoundedRect(Rectangle r, int radius) =>
            ZeroUIConfig.CreateRoundedRectangle(r, radius);

        private static GraphicsPath CreateTopRoundedRect(Rectangle r, int radius) =>
            ZeroUIConfig.CreateTopRoundedRectangle(r, radius);
    
    private void OnThemeChanged(object sender, EventArgs e) => Invalidate();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }

}

    [Obsolete("Use TabStyle instead.")]
    public enum ZeroTabStyle
    {
        Underline = TabStyle.Underline,
        Pill = TabStyle.Pill,
        Card = TabStyle.Card
    }

    [Obsolete("Use TabOrientation instead.")]
    public enum ZeroTabOrientation
    {
        Horizontal = TabOrientation.Horizontal,
        Vertical = TabOrientation.Vertical
    }

    /// <summary>
    /// Modern anti-aliased flat TabControl and container for ZeroUI.
    /// Canonical drop-in replacement for standard <see cref="System.Windows.Forms.TabControl"/>.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Overlays & Navigation")]
    [DefaultEvent("SelectedIndexChanged")]
    [DefaultProperty("SelectedIndex")]
    [Description("Modern theme-aware TabControl adhering to the canonical Z-prefix standard")]
    [ToolboxBitmap(typeof(ZeroIcons), "TabControlEx.bmp")]
    [Designer("ZeroUI.WinForms.Design.Common.ZTabControlDesigner, ZeroUI.WinForms.Design")]
    public class ZTabControl : TabControlEx
    {
    }

    /// <summary>
    /// Represents an individual tab page container inside <see cref="ZTabControl"/>.
    /// </summary>
    public class ZTabPage : TabPageEx
    {
        public ZTabPage() : base() { }
        public ZTabPage(string title, string icon = "") : base(title, icon) { }
        public ZTabPage(string title, string icon, Action<TabPageEx> lazyInitializer) : base(title, icon, lazyInitializer) { }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    [Obsolete("ZeroTabPage is deprecated and will be removed in 5 release cycles. Please migrate to ZTabPage instead.")]
    public class ZeroTabPage : TabPageEx
    {
        public ZeroTabPage() : base() { }
        public ZeroTabPage(string title, string icon = "") : base(title, icon) { }
        public ZeroTabPage(string title, string icon, Action<ZeroTabPage> lazyInitializer) : base(title, icon, p => lazyInitializer((ZeroTabPage)p)) { }
    }

    [Obsolete("ZeroTabControl is deprecated and will be removed in 5 release cycles. Please migrate to ZTabControl instead.")]
    [ToolboxItem(false)]
    public class ZeroTabControl : TabControlEx
    {
        public new ZeroTabStyle TabStyle
        {
            get => (ZeroTabStyle)base.TabStyle;
            set => base.TabStyle = (TabStyle)value;
        }

        public new ZeroTabOrientation Orientation
        {
            get => (ZeroTabOrientation)base.Orientation;
            set => base.Orientation = (TabOrientation)value;
        }
    }

    #endregion
}
