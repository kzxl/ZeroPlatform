using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Ribbon
{
    public class RibbonItemClickEventArgs : EventArgs
    {
        public RibbonItem Item { get; }
        public RibbonGroup Group { get; }
        public RibbonPage Page { get; }

        public RibbonItemClickEventArgs(RibbonItem item, RibbonGroup group, RibbonPage page)
        {
            Item = item;
            Group = group;
            Page = page;
        }
    }

    public class ApprovalVisibilityChangedEventArgs : EventArgs
    {
        public string GroupKey { get; }
        public bool IsVisible { get; }

        public ApprovalVisibilityChangedEventArgs(string groupKey, bool isVisible)
        {
            GroupKey = groupKey;
            IsVisible = isVisible;
        }
    }

    /// <summary>
    /// Modern Enterprise Lightweight Ribbon Control for ZeroUI WinForms.
    /// Delivers 100% visual parity with enterprise ERP ribbon bars, single-HWND canvas rendering,
    /// dynamic approval queues, and instant visibility customization.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Navigation")]
    [DefaultEvent("ItemClick")]
    [Description("Modern high-performance enterprise Ribbon control with built-in approval dropdown queues.")]
    public class ZRibbon : ControlBase
    {
        private readonly List<RibbonPage> _pages = new List<RibbonPage>();
        private readonly ToolTip _toolTip = new ToolTip();
        private string? _userInfo;
        private int _selectedPageIndex = 0;
        private bool _isMinimized = false;
        private int _expandedHeight = 132;
        private int _tabHeight = 28;

        // Hit testing caches
        private readonly List<Rectangle> _tabRects = new List<Rectangle>();
        private readonly List<(Rectangle Rect, RibbonItem Item, RibbonGroup Group)> _itemHitAreas = new List<(Rectangle, RibbonItem, RibbonGroup)>();
        private readonly List<(Rectangle Rect, RibbonApprovalGroup Group)> _approvalHitAreas = new List<(Rectangle, RibbonApprovalGroup)>();
        private readonly List<(Rectangle Rect, RibbonApprovalItem Item, RibbonApprovalGroup Group)> _approvalItemHitAreas = new List<(Rectangle, RibbonApprovalItem, RibbonApprovalGroup)>();
        private Rectangle _optionsButtonRect;
        private Rectangle _minimizeButtonRect;

        // Interaction states
        private int _hoveredTabIndex = -1;
        private RibbonItem? _hoveredItem;
        private RibbonItem? _pressedItem;
        private RibbonApprovalGroup? _hoveredApprovalGroup;
        private RibbonApprovalItem? _hoveredApprovalItem;
        private bool _hoverOptionsButton;
        private bool _hoverMinimizeButton;

        private RibbonApprovalDisplayMode _approvalDisplayMode = RibbonApprovalDisplayMode.Auto;

        /// <summary>
        /// Controls whether approval groups expand individual task items directly onto the ribbon canvas
        /// or collapse into compact dropdown buttons. Default: Auto (expands if width permits).
        /// </summary>
        [Category("ZeroUI - Behavior")]
        [DefaultValue(RibbonApprovalDisplayMode.Auto)]
        [Description("Controls whether approval queues expand task items directly or collapse into dropdown buttons.")]
        public RibbonApprovalDisplayMode ApprovalDisplayMode
        {
            get => _approvalDisplayMode;
            set
            {
                if (_approvalDisplayMode != value)
                {
                    _approvalDisplayMode = value;
                    Invalidate();
                }
            }
        }

        public event EventHandler<RibbonItemClickEventArgs>? ItemClick;
        public event EventHandler<RibbonApprovalItem>? ApprovalItemClick;
        public event EventHandler<ApprovalVisibilityChangedEventArgs>? ApprovalVisibilityChanged;
        public event EventHandler? SelectedPageChanged;
        public event EventHandler? MinimizedChanged;

        public ZRibbon()
        {
            Dock = DockStyle.Top;
            Height = _expandedHeight;
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 9f);
            _toolTip.InitialDelay = 350;
            _toolTip.ReshowDelay = 150;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Properties

        [Browsable(false)]
        public List<RibbonPage> Pages => _pages;

        [Category("Appearance")]
        [DefaultValue(null)]
        [Description("User profile or status text displayed on the right edge of the tab header.")]
        public string? UserInfo
        {
            get => _userInfo;
            set
            {
                if (_userInfo != value)
                {
                    _userInfo = value;
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool IsMinimized
        {
            get => _isMinimized;
            set
            {
                if (_isMinimized != value)
                {
                    _isMinimized = value;
                    Height = _isMinimized ? _tabHeight : _expandedHeight;
                    Invalidate();
                    MinimizedChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(132)]
        public int ExpandedHeight
        {
            get => _expandedHeight;
            set
            {
                _expandedHeight = Math.Max(90, value);
                if (!_isMinimized) Height = _expandedHeight;
            }
        }

        [Category("Behavior")]
        [DefaultValue(0)]
        public int SelectedPageIndex
        {
            get => _selectedPageIndex;
            set
            {
                if (value >= 0 && value < _pages.Count && _selectedPageIndex != value)
                {
                    _selectedPageIndex = value;
                    Invalidate();
                    SelectedPageChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Browsable(false)]
        public RibbonPage? SelectedPage => (_selectedPageIndex >= 0 && _selectedPageIndex < _pages.Count)
            ? _pages[_selectedPageIndex]
            : null;

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("Controls whether the visibility options dropdown button is displayed for approval groups.")]
        public bool ShowApprovalOptionsButton { get; set; } = true;

        [Category("Appearance")]
        [DefaultValue("Display Options ⯆")]
        [Description("Custom text or localized label for the approval visibility options dropdown button.")]
        public string ApprovalOptionsText { get; set; } = "Display Options ⯆";

        private IRibbonStateStore? _stateStore;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IRibbonStateStore? StateStore
        {
            get => _stateStore;
            set
            {
                _stateStore = value;
                ApplyStateStore();
            }
        }

        /// <summary>
        /// Global resolver for approval item status dot color.
        /// Fallbacks to item-level resolver, group-level resolver, and finally theme palette defaults.
        /// </summary>
        [Browsable(false)]
        public Func<RibbonApprovalItem, Color>? ApprovalStatusColorResolver { get; set; }

        public void ApplyStateStore()
        {
            if (_stateStore == null) return;
            foreach (var page in _pages)
            {
                foreach (var ag in page.ApprovalGroups)
                {
                    bool isVis = _stateStore.LoadGroupVisibility(ag.GroupKey, ag.Visible);
                    ag.Visible = isVis;
                    var rg = page.Groups.Find(g => g.ApprovalGroup == ag);
                    if (rg != null) rg.Visible = isVis;
                }
            }
            Invalidate();
        }

        #endregion

        #region Fluent API

        public RibbonPage AddPage(string text)
        {
            var page = new RibbonPage(text);
            _pages.Add(page);
            Invalidate();
            return page;
        }

        /// <summary>
        /// Updates the approval count for a specific task key across all approval groups.
        /// </summary>
        public void UpdateApprovalCount(string key, int count)
        {
            bool updated = false;
            foreach (var p in _pages)
            {
                foreach (var ag in p.ApprovalGroups)
                {
                    var item = ag.Items.Find(i => i.Key == key);
                    if (item != null && item.Count != count)
                    {
                        item.Count = count;
                        updated = true;
                    }
                }
            }

            if (updated)
            {
                if (InvokeRequired) BeginInvoke((Action)Invalidate);
                else Invalidate();
            }
        }

        /// <summary>
        /// Bulk updates approval counts from a collection of key-count tuples.
        /// </summary>
        public void UpdateApprovalCounts(IEnumerable<(string Key, int Count)> counts)
        {
            if (counts == null) return;
            foreach (var (key, count) in counts)
            {
                foreach (var p in _pages)
                {
                    foreach (var ag in p.ApprovalGroups)
                    {
                        var item = ag.Items.Find(i => i.Key == key);
                        if (item != null) item.Count = count;
                    }
                }
            }
            if (InvokeRequired) BeginInvoke((Action)Invalidate);
            else Invalidate();
        }

        /// <summary>
        /// Universally binds approval counts from any strongly typed DTO, entity, or domain model collection.
        /// Eliminates manual mapping boilerplate in business controllers/views.
        /// </summary>
        public void BindApprovalCounts<TSource>(
            IEnumerable<TSource> source,
            Func<TSource, string> keySelector,
            Func<TSource, int> countSelector)
        {
            if (source == null || keySelector == null || countSelector == null) return;
            var list = new List<(string Key, int Count)>();
            foreach (var item in source)
            {
                string key = keySelector(item);
                if (!string.IsNullOrEmpty(key))
                {
                    list.Add((key, countSelector(item)));
                }
            }
            UpdateApprovalCounts(list);
        }

        public void SetApprovalGroupVisibility(string groupKey, bool isVisible)
        {
            foreach (var p in _pages)
            {
                var ag = p.ApprovalGroups.Find(g => g.GroupKey == groupKey);
                if (ag != null) ag.Visible = isVisible;

                var rg = p.Groups.Find(g => g.ApprovalGroup != null && g.ApprovalGroup.GroupKey == groupKey);
                if (rg != null) rg.Visible = isVisible;
            }
            Invalidate();
        }

        public RibbonItem? FindItem(string idOrText)
        {
            foreach (var page in _pages)
            {
                var item = page.FindItem(idOrText);
                if (item != null) return item;
            }
            return null;
        }

        public RibbonApprovalGroup? GetApprovalGroup(string groupKey)
        {
            foreach (var page in _pages)
            {
                var ag = page.GetApprovalGroup(groupKey);
                if (ag != null) return ag;
            }
            return null;
        }

        public RibbonApprovalItem? GetApprovalItem(string key)
        {
            foreach (var page in _pages)
            {
                foreach (var ag in page.ApprovalGroups)
                {
                    var item = ag.GetItem(key);
                    if (item != null) return item;
                }
            }
            return null;
        }

        #endregion

        #region Painting & Rendering

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var pal = CurrentPalette;

            _tabRects.Clear();
            _itemHitAreas.Clear();
            _approvalHitAreas.Clear();
            _approvalItemHitAreas.Clear();

            // 1. Render Tab Header Bar (Top 28px)
            var tabHeaderRect = new Rectangle(0, 0, Width, _tabHeight);
            using (var tabBgBrush = new SolidBrush(pal.HeaderBackground))
            {
                g.FillRectangle(tabBgBrush, tabHeaderRect);
            }

            int tabX = 12;
            for (int i = 0; i < _pages.Count; i++)
            {
                var page = _pages[i];
                if (!page.Visible) continue;

                Size tabTextSize = TextRenderer.MeasureText(page.Text, Font);
                int tabW = tabTextSize.Width + 20;
                var tabRect = new Rectangle(tabX, 2, tabW, _tabHeight - 2);
                _tabRects.Add(tabRect);

                bool isSelected = (i == _selectedPageIndex);
                bool isHover = (i == _hoveredTabIndex);

                if (isSelected)
                {
                    // Active Tab Background (merges with body)
                    using (var b = new SolidBrush(pal.Surface))
                    {
                        g.FillRectangle(b, tabRect);
                    }
                    using (var p = new Pen(pal.Border, 1f))
                    {
                        g.DrawLine(p, tabRect.Left, tabRect.Top, tabRect.Right, tabRect.Top);
                        g.DrawLine(p, tabRect.Left, tabRect.Top, tabRect.Left, tabRect.Bottom);
                        g.DrawLine(p, tabRect.Right, tabRect.Top, tabRect.Right, tabRect.Bottom);
                    }
                }
                else if (isHover)
                {
                    using (var b = new SolidBrush(pal.Hover))
                    {
                        g.FillRectangle(b, tabRect);
                    }
                }

                Color tabTextCol = isSelected ? pal.Primary : pal.TextPrimary;
                TextRenderer.DrawText(g, page.Text, Font, tabRect, tabTextCol,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                tabX += tabW + 4;
            }

            // Minimize Toggle Button (Top Right)
            int minBtnSize = 22;
            _minimizeButtonRect = new Rectangle(Width - minBtnSize - 8, 3, minBtnSize, minBtnSize);
            Color minBg = _hoverMinimizeButton ? pal.Hover : Color.Transparent;
            if (minBg != Color.Transparent)
            {
                using (var b = new SolidBrush(minBg))
                {
                    g.FillRectangle(b, _minimizeButtonRect);
                }
            }

            string minGlyph = _isMinimized ? "v" : "^";
            using (var minFont = new Font("Segoe UI", 8f, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, minGlyph, minFont, _minimizeButtonRect, pal.TextSecondary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            // User profile or status text (Top Right, before minimize button)
            if (!string.IsNullOrEmpty(_userInfo))
            {
                int userW = TextRenderer.MeasureText(_userInfo, Font).Width + 16;
                var userRect = new Rectangle(_minimizeButtonRect.Left - userW - 6, 2, userW, _tabHeight - 4);
                TextRenderer.DrawText(g, _userInfo, Font, userRect, pal.TextSecondary,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            // Divider between Tab Header and Body
            using (var divPen = new Pen(pal.Border, 1f))
            {
                g.DrawLine(divPen, 0, _tabHeight - 1, Width, _tabHeight - 1);
            }

            if (_isMinimized) return;

            // 2. Render Ribbon Page Body
            var bodyRect = new Rectangle(0, _tabHeight, Width, Height - _tabHeight);
            using (var bodyBrush = new SolidBrush(pal.Surface))
            {
                g.FillRectangle(bodyBrush, bodyRect);
            }

            var activePage = SelectedPage;
            if (activePage == null) return;

            int curX = 8;
            int groupContentY = _tabHeight + 4;
            int groupContentH = Height - _tabHeight - 24;

            // 1. Calculate space required by right-aligned widgets/approval groups
            bool hasApprovalGroups = (activePage.ApprovalGroups.Count > 0);
            int optW = 0;
            if (hasApprovalGroups && ShowApprovalOptionsButton)
            {
                Size optSize = TextRenderer.MeasureText(ApprovalOptionsText, Font);
                optW = Math.Max(115, optSize.Width + 24);
            }

            int compactApprovalsWidth = 0;
            int expandedApprovalsWidth = 0;
            if (hasApprovalGroups)
            {
                if (ShowApprovalOptionsButton)
                {
                    compactApprovalsWidth += optW + 8;
                    expandedApprovalsWidth += optW + 8;
                }
                foreach (var ag in activePage.ApprovalGroups)
                {
                    if (!ag.Visible) continue;
                    Size titleSize = TextRenderer.MeasureText(ag.Title, Font);
                    compactApprovalsWidth += Math.Max(120, titleSize.Width + 36) + 6;
                    expandedApprovalsWidth += MeasureExpandedApprovalGroupWidth(g, ag) + 8;
                }
            }

            int functionalGroupsWidth = MeasureFunctionalGroupsWidth(g, activePage);
            int availableWidth = Width - minBtnSize - 16 - functionalGroupsWidth;

            bool isExpandedApprovals = false;
            if (hasApprovalGroups)
            {
                if (ApprovalDisplayMode == RibbonApprovalDisplayMode.Expanded)
                {
                    isExpandedApprovals = true;
                }
                else if (ApprovalDisplayMode == RibbonApprovalDisplayMode.Compact)
                {
                    isExpandedApprovals = false;
                }
                else // Auto
                {
                    isExpandedApprovals = (availableWidth >= expandedApprovalsWidth);
                }
            }

            int rightNeeded = hasApprovalGroups
                ? (isExpandedApprovals ? expandedApprovalsWidth : compactApprovalsWidth)
                : 0;
            int maxGroupX = Width - minBtnSize - 16 - rightNeeded;

            // 2. Render Standard Functional Groups
            foreach (var grp in activePage.Groups)
            {
                if (!grp.Visible || grp.ApprovalGroup != null) continue;
                if (curX >= maxGroupX) break;

                int grpStartX = curX;
                curX = RenderStandardGroup(g, grp, curX, groupContentY, groupContentH, pal, maxGroupX);

                // Group caption at bottom
                int grpW = curX - grpStartX;
                if (grpW > 0)
                {
                    var captionRect = new Rectangle(grpStartX, Height - 18, grpW, 16);
                    using (var captionFont = new Font(Font.FontFamily, 7.5f))
                    {
                        TextRenderer.DrawText(g, grp.Text, captionFont, captionRect, pal.TextSecondary,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    }

                    // Vertical Separator line
                    using (var sepPen = new Pen(pal.Border, 1f))
                    {
                        g.DrawLine(sepPen, curX + 2, _tabHeight + 6, curX + 2, Height - 6);
                    }
                    curX += 6;
                }
            }

            // 3. Render Approval Groups if configured on the active page
            int rightX = Width - minBtnSize - 16;
            if (hasApprovalGroups)
            {
                // Options button: configurable label & visibility
                if (ShowApprovalOptionsButton)
                {
                    int optH = 26;
                    _optionsButtonRect = new Rectangle(rightX - optW, groupContentY + 18, optW, optH);
                    rightX -= (optW + 8);

                    Color optBg = _hoverOptionsButton ? pal.Hover : pal.Surface;
                    using (var b = new SolidBrush(optBg))
                    using (var p = new Pen(pal.Border, 1f))
                    {
                        g.FillRectangle(b, _optionsButtonRect);
                        g.DrawRectangle(p, _optionsButtonRect);
                    }
                    TextRenderer.DrawText(g, ApprovalOptionsText, Font, _optionsButtonRect, pal.TextPrimary,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else
                {
                    _optionsButtonRect = Rectangle.Empty;
                }

                if (isExpandedApprovals)
                {
                    // Render Expanded Mode: Items displayed directly on the ribbon canvas in columns (up to 3 rows per col)
                    for (int i = activePage.ApprovalGroups.Count - 1; i >= 0; i--)
                    {
                        var ag = activePage.ApprovalGroups[i];
                        if (!ag.Visible) continue;

                        int agW = MeasureExpandedApprovalGroupWidth(g, ag);
                        int agX = rightX - agW;
                        var agGroupRect = new Rectangle(agX, groupContentY, agW, groupContentH);
                        rightX -= (agW + 8);

                        // Vertical separator to the right
                        using (var sepPen = new Pen(pal.Border, 1f))
                        {
                            g.DrawLine(sepPen, agGroupRect.Right + 4, _tabHeight + 6, agGroupRect.Right + 4, Height - 6);
                        }

                        // Group Title at bottom
                        var captionRect = new Rectangle(agX, Height - 18, agW, 16);
                        using (var captionFont = new Font(Font.FontFamily, 7.5f))
                        {
                            TextRenderer.DrawText(g, ag.Title, captionFont, captionRect, pal.TextSecondary,
                                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                        }

                        // Child items in columns (up to 3 rows per col)
                        int cols = Math.Max(1, (int)Math.Ceiling(ag.Items.Count / 3.0));
                        int colW = (agW - 6) / cols;

                        using (var itemFont = new Font("Segoe UI", 8.25f))
                        {
                            for (int itemIdx = 0; itemIdx < ag.Items.Count; itemIdx++)
                            {
                                var item = ag.Items[itemIdx];
                                int col = itemIdx / 3;
                                int row = itemIdx % 3;

                                int itemX = agX + 2 + col * colW;
                                int itemY = groupContentY + 2 + row * 23;
                                int itemH = 21;
                                var itemRect = new Rectangle(itemX, itemY, colW - 2, itemH);
                                _approvalItemHitAreas.Add((itemRect, item, ag));

                                bool isHover = (item == _hoveredApprovalItem);
                                if (isHover)
                                {
                                    using (var hb = new SolidBrush(pal.Hover))
                                    using (var hp = new Pen(pal.Border, 1f))
                                    {
                                        g.FillRectangle(hb, itemRect);
                                        g.DrawRectangle(hp, itemRect);
                                    }
                                }

                                // Status dot (Green if Count == 0, Red/Danger if Count > 0)
                                int dotSize = 8;
                                int dotX = itemRect.X + 4;
                                int dotY = itemRect.Y + (itemRect.Height - dotSize) / 2;
                                var dotRect = new Rectangle(dotX, dotY, dotSize, dotSize);
                                Color dotColor = item.StatusColor
                                    ?? item.StatusColorResolver?.Invoke(item)
                                    ?? (item.Count > 0 ? pal.Danger : Color.FromArgb(16, 185, 129));

                                using (var db = new SolidBrush(dotColor))
                                {
                                    g.FillEllipse(db, dotRect);
                                }

                                // Item text: "Name: Count"
                                string txt = item.GetDisplayText();
                                var txtRect = new Rectangle(dotRect.Right + 5, itemRect.Y, itemRect.Width - (dotSize + 7), itemRect.Height);
                                TextRenderer.DrawText(g, txt, itemFont, txtRect, pal.TextPrimary,
                                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                            }
                        }
                    }
                }
                else
                {
                    // Render Compact Mode: Single dropdown button with badge count
                    for (int i = activePage.ApprovalGroups.Count - 1; i >= 0; i--)
                    {
                        var ag = activePage.ApprovalGroups[i];
                        if (!ag.Visible) continue;

                        Size titleSize = TextRenderer.MeasureText(ag.Title, Font);
                        int agW = Math.Max(120, titleSize.Width + 36);
                        int agH = 34;

                        var agRect = new Rectangle(rightX - agW, groupContentY + 14, agW, agH);
                        _approvalHitAreas.Add((agRect, ag));
                        rightX -= (agW + 6);

                        bool isHover = (ag == _hoveredApprovalGroup);
                        Color agBg = isHover ? pal.Hover : pal.HeaderBackground;

                        using (var b = new SolidBrush(agBg))
                        using (var p = new Pen(isHover ? pal.Primary : pal.Border, 1f))
                        {
                            g.FillRectangle(b, agRect);
                            g.DrawRectangle(p, agRect);
                        }

                        // Title + Arrow
                        string displayText = $"{ag.Title} ⯆";
                        TextRenderer.DrawText(g, displayText, Font, new Rectangle(agRect.X + 6, agRect.Y, agRect.Width - 30, agRect.Height),
                            pal.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                        // Badge circle indicating total count
                        int badgeSize = 18;
                        var badgeRect = new Rectangle(agRect.Right - badgeSize - 6, agRect.Y + (agRect.Height - badgeSize) / 2, badgeSize, badgeSize);
                        Color badgeColor = ag.BadgeColorResolver?.Invoke(ag.TotalCount)
                            ?? (ag.TotalCount > 0 ? pal.Danger : Color.FromArgb(16, 185, 129));

                        using (var bb = new SolidBrush(badgeColor))
                        {
                            g.FillEllipse(bb, badgeRect);
                        }
                        using (var bf = new Font("Segoe UI", 7f, FontStyle.Bold))
                        {
                            TextRenderer.DrawText(g, ag.TotalCount.ToString(), bf, badgeRect, Color.White,
                                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                        }
                    }
                }
            }
            else
            {
                _optionsButtonRect = Rectangle.Empty;
            }

            // Outer bottom border
            using (var bPen = new Pen(pal.Border, 1f))
            {
                g.DrawLine(bPen, 0, Height - 1, Width, Height - 1);
            }
        }

        private int MeasureExpandedApprovalGroupWidth(Graphics g, RibbonApprovalGroup ag)
        {
            if (ag.Items.Count == 0) return 90;
            int cols = Math.Max(1, (int)Math.Ceiling(ag.Items.Count / 3.0));
            int maxItemW = 55;
            using (var itemFont = new Font("Segoe UI", 8.25f))
            {
                foreach (var item in ag.Items)
                {
                    string txt = item.GetDisplayText();
                    Size sz = TextRenderer.MeasureText(g, txt, itemFont);
                    if (sz.Width > maxItemW) maxItemW = sz.Width;
                }
            }
            int colW = maxItemW + 20; // 8px dot + padding
            Size titleSz = TextRenderer.MeasureText(g, ag.Title, Font);
            return Math.Max(cols * colW + 10, titleSz.Width + 16);
        }

        private int MeasureFunctionalGroupsWidth(Graphics g, RibbonPage page)
        {
            int total = 12;
            foreach (var grp in page.Groups)
            {
                if (!grp.Visible || grp.ApprovalGroup != null) continue;
                total += MeasureGroupWidth(g, grp) + 6;
            }
            return total;
        }

        private int MeasureGroupWidth(Graphics g, RibbonGroup grp)
        {
            int w = 0;
            int smallInCol = 0;
            int maxSmallW = 0;

            foreach (var item in grp.Items)
            {
                if (!item.Visible) continue;
                if (item.Style == RibbonItemStyle.Large)
                {
                    if (smallInCol > 0)
                    {
                        w += Math.Max(120, maxSmallW) + 4;
                        smallInCol = 0;
                        maxSmallW = 0;
                    }
                    w += 76;
                }
                else
                {
                    Size s = TextRenderer.MeasureText(g, item.Text, Font);
                    int itemW = s.Width + 34;
                    if (itemW > maxSmallW) maxSmallW = itemW;
                    smallInCol++;
                    if (smallInCol == 3)
                    {
                        w += Math.Max(120, maxSmallW) + 4;
                        smallInCol = 0;
                        maxSmallW = 0;
                    }
                }
            }
            if (smallInCol > 0)
            {
                w += Math.Max(120, maxSmallW) + 4;
            }

            Size titleSize = TextRenderer.MeasureText(g, grp.Text, Font);
            return Math.Max(w, titleSize.Width + 12);
        }

        private int RenderStandardGroup(Graphics g, RibbonGroup grp, int startX, int contentY, int contentH, ZeroThemePalette pal, int maxGroupX)
        {
            int x = startX;
            int colY = contentY;
            int smallColStartX = -1;
            int smallInCol = 0;

            foreach (var item in grp.Items)
            {
                if (!item.Visible) continue;

                if (item.Style == RibbonItemStyle.Large)
                {
                    // Flush pending small button column if any
                    if (smallInCol > 0)
                    {
                        x += 135 + 4;
                        smallInCol = 0;
                        smallColStartX = -1;
                    }

                    int w = 76;
                    int h = contentH;
                    if (x + w > maxGroupX) break; // Overflow protection
                    var itemRect = new Rectangle(x, contentY, w, h);
                    _itemHitAreas.Add((itemRect, item, grp));

                    bool isHover = (item == _hoveredItem);
                    bool isPressed = (item == _pressedItem);
                    bool isChecked = item.Checked;

                    Color bg = isPressed ? pal.PrimaryHover
                        : isChecked ? Color.FromArgb(36, pal.Primary)
                        : isHover ? pal.Hover
                        : Color.Transparent;

                    if (bg != Color.Transparent)
                    {
                        using (var b = new SolidBrush(bg))
                        {
                            g.FillRectangle(b, itemRect);
                        }
                    }

                    if (isChecked)
                    {
                        using (var p = new Pen(pal.Primary, 1f))
                        {
                            g.DrawRectangle(p, itemRect.X, itemRect.Y, itemRect.Width - 1, itemRect.Height - 1);
                        }
                    }

                    // Icon (32x32)
                    int iconY = contentY + 6;
                    int iconX = x + (w - 32) / 2;
                    if (item.Icon != null)
                    {
                        g.DrawImage(item.Icon, new Rectangle(iconX, iconY, 32, 32));
                    }
                    else
                    {
                        // Fallback vector icon
                        using (var p = new Pen(isChecked || isHover ? pal.Primary : pal.TextPrimary, 1.8f))
                        {
                            g.DrawRectangle(p, iconX + 4, iconY + 4, 24, 24);
                        }
                    }

                    // Capsule Badge if present
                    if (!string.IsNullOrEmpty(item.BadgeText))
                    {
                        using (var bf = new Font("Segoe UI", 7f, FontStyle.Bold))
                        {
                            Size bSize = TextRenderer.MeasureText(item.BadgeText, bf);
                            int bw = Math.Max(16, bSize.Width + 6);
                            int bh = 14;
                            var bRect = new Rectangle(x + w - bw - 4, contentY + 3, bw, bh);
                            Color bColor = item.BadgeColor ?? pal.Danger;
                            using (var bb = new SolidBrush(bColor))
                            using (var path = CreateRoundedRectanglePath(bRect, 4))
                            {
                                g.FillPath(bb, path);
                            }
                            TextRenderer.DrawText(g, item.BadgeText, bf, bRect, Color.White,
                                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                        }
                    }

                    // Text
                    var textRect = new Rectangle(x + 2, contentY + 40, w - 4, h - 42);
                    using (var f = new Font(Font.FontFamily, 8f))
                    {
                        TextRenderer.DrawText(g, item.Text, f, textRect, pal.TextPrimary,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
                    }

                    x += w + 2;
                }
                else
                {
                    // Small Button (Stacked vertically up to 3 per column)
                    if (smallColStartX == -1)
                    {
                        if (x + 135 > maxGroupX) break; // Overflow protection
                        smallColStartX = x;
                        colY = contentY;
                    }

                    int smW = 135;
                    int smH = 22;
                    var smRect = new Rectangle(smallColStartX, colY, smW, smH);
                    _itemHitAreas.Add((smRect, item, grp));

                    bool isHover = (item == _hoveredItem);
                    bool isChecked = item.Checked;

                    Color bg = isChecked ? Color.FromArgb(36, pal.Primary)
                        : isHover ? pal.Hover
                        : Color.Transparent;

                    if (bg != Color.Transparent)
                    {
                        using (var b = new SolidBrush(bg))
                        {
                            g.FillRectangle(b, smRect);
                        }
                    }

                    if (isChecked)
                    {
                        using (var p = new Pen(pal.Primary, 1f))
                        {
                            g.DrawRectangle(p, smRect.X, smRect.Y, smRect.Width - 1, smRect.Height - 1);
                        }
                    }

                    // 16x16 icon
                    if (item.Icon != null)
                    {
                        g.DrawImage(item.Icon, new Rectangle(smRect.X + 4, smRect.Y + 3, 16, 16));
                    }
                    else
                    {
                        using (var b = new SolidBrush(isChecked || isHover ? pal.Primary : pal.TextSecondary))
                        {
                            g.FillEllipse(b, smRect.X + 6, smRect.Y + 7, 6, 6);
                        }
                    }

                    // Text
                    int textMaxW = smRect.Width - 26;
                    if (!string.IsNullOrEmpty(item.BadgeText))
                    {
                        textMaxW -= 28;
                    }

                    var textRect = new Rectangle(smRect.X + 24, smRect.Y, textMaxW, smRect.Height);
                    using (var f = new Font(Font.FontFamily, 8.5f))
                    {
                        TextRenderer.DrawText(g, item.Text, f, textRect, pal.TextPrimary,
                            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    }

                    // Small capsule badge
                    if (!string.IsNullOrEmpty(item.BadgeText))
                    {
                        using (var bf = new Font("Segoe UI", 7f, FontStyle.Bold))
                        {
                            Size bSize = TextRenderer.MeasureText(item.BadgeText, bf);
                            int bw = Math.Max(16, bSize.Width + 4);
                            int bh = 13;
                            var bRect = new Rectangle(smRect.Right - bw - 4, smRect.Y + (smRect.Height - bh) / 2, bw, bh);
                            Color bColor = item.BadgeColor ?? pal.Danger;
                            using (var bb = new SolidBrush(bColor))
                            using (var path = CreateRoundedRectanglePath(bRect, 3))
                            {
                                g.FillPath(bb, path);
                            }
                            TextRenderer.DrawText(g, item.BadgeText, bf, bRect, Color.White,
                                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                        }
                    }

                    colY += smH + 1;
                    smallInCol++;
                    if (smallInCol >= 3)
                    {
                        x += smW + 4;
                        smallColStartX = -1;
                        smallInCol = 0;
                    }
                }
            }

            if (smallInCol > 0)
            {
                x += 135 + 4;
            }

            return x;
        }

        #endregion

        #region Mouse & Interaction Handling

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int prevTab = _hoveredTabIndex;
            RibbonItem? prevItem = _hoveredItem;
            RibbonApprovalGroup? prevAg = _hoveredApprovalGroup;
            RibbonApprovalItem? prevAppItem = _hoveredApprovalItem;
            bool prevOpt = _hoverOptionsButton;
            bool prevMin = _hoverMinimizeButton;

            _hoveredTabIndex = -1;
            _hoveredItem = null;
            _hoveredApprovalGroup = null;
            _hoveredApprovalItem = null;
            _hoverOptionsButton = !_optionsButtonRect.IsEmpty && _optionsButtonRect.Contains(e.Location);
            _hoverMinimizeButton = _minimizeButtonRect.Contains(e.Location);

            // Tab hover
            if (e.Y < _tabHeight)
            {
                for (int i = 0; i < _tabRects.Count; i++)
                {
                    if (_tabRects[i].Contains(e.Location))
                    {
                        _hoveredTabIndex = i;
                        break;
                    }
                }
            }
            else if (!_isMinimized)
            {
                // Item hover
                foreach (var area in _itemHitAreas)
                {
                    if (area.Rect.Contains(e.Location))
                    {
                        _hoveredItem = area.Item;
                        break;
                    }
                }

                // Approval item hover (expanded mode)
                foreach (var area in _approvalItemHitAreas)
                {
                    if (area.Rect.Contains(e.Location))
                    {
                        _hoveredApprovalItem = area.Item;
                        break;
                    }
                }

                // Approval group hover (compact mode)
                foreach (var area in _approvalHitAreas)
                {
                    if (area.Rect.Contains(e.Location))
                    {
                        _hoveredApprovalGroup = area.Group;
                        break;
                    }
                }
            }

            if (_hoveredApprovalItem != null || _hoveredItem != null || _hoveredApprovalGroup != null ||
                _hoverOptionsButton || _hoverMinimizeButton || _hoveredTabIndex >= 0)
            {
                Cursor = Cursors.Hand;
            }
            else
            {
                Cursor = Cursors.Default;
            }

            if (prevTab != _hoveredTabIndex || prevItem != _hoveredItem || prevAg != _hoveredApprovalGroup ||
                prevAppItem != _hoveredApprovalItem || prevOpt != _hoverOptionsButton || prevMin != _hoverMinimizeButton)
            {
                if (_hoveredItem != prevItem)
                {
                    if (_hoveredItem != null && !string.IsNullOrEmpty(_hoveredItem.ToolTipText))
                    {
                        _toolTip.SetToolTip(this, _hoveredItem.ToolTipText);
                    }
                    else
                    {
                        _toolTip.SetToolTip(this, null);
                    }
                }
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredTabIndex = -1;
            _hoveredItem = null;
            _hoveredApprovalGroup = null;
            _hoveredApprovalItem = null;
            _hoverOptionsButton = false;
            _hoverMinimizeButton = false;
            Cursor = Cursors.Default;
            _toolTip.SetToolTip(this, null);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && _hoveredItem != null)
            {
                _pressedItem = _hoveredItem;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_pressedItem != null)
            {
                _pressedItem = null;
                Invalidate();
            }

            if (e.Button == MouseButtons.Left)
            {
                // Minimize button
                if (_minimizeButtonRect.Contains(e.Location))
                {
                    IsMinimized = !IsMinimized;
                    return;
                }

                // Tab Click
                if (_hoveredTabIndex >= 0 && _hoveredTabIndex != _selectedPageIndex)
                {
                    SelectedPageIndex = _hoveredTabIndex;
                    return;
                }

                // Approval Item Click (Expanded Mode)
                if (_hoveredApprovalItem != null)
                {
                    var clicked = _hoveredApprovalItem;
                    clicked.ClickAction?.Invoke();
                    ApprovalItemClick?.Invoke(this, clicked);
                    Invalidate();
                    return;
                }

                // Ribbon Item Click
                if (_hoveredItem != null)
                {
                    var clickedItem = _hoveredItem;
                    var activePage = SelectedPage;
                    var area = _itemHitAreas.Find(a => a.Item == clickedItem);

                    clickedItem.PerformClick();
                    if (activePage != null && area.Group != null)
                    {
                        ItemClick?.Invoke(this, new RibbonItemClickEventArgs(clickedItem, area.Group, activePage));
                    }

                    if (clickedItem.DropDownMenu != null)
                    {
                        var screenLoc = PointToScreen(new Point(area.Rect.Left, area.Rect.Bottom + 2));
                        clickedItem.DropDownMenu.Show(screenLoc);
                    }

                    Invalidate();
                    return;
                }

                // Approval Group Dropdown Click
                if (_hoveredApprovalGroup != null)
                {
                    var area = _approvalHitAreas.Find(a => a.Group == _hoveredApprovalGroup);
                    ShowApprovalDropdown(area.Rect, _hoveredApprovalGroup);
                    return;
                }

                // Options Button Click
                if (!_optionsButtonRect.IsEmpty && _optionsButtonRect.Contains(e.Location))
                {
                    ShowVisibilityOptionsDropdown(_optionsButtonRect);
                    return;
                }
            }
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            // Double-clicking tab header toggles minimize state (like MS Office)
            var mousePos = PointToClient(Cursor.Position);
            if (mousePos.Y < _tabHeight)
            {
                IsMinimized = !IsMinimized;
            }
        }

        #endregion

        #region Dropdown Menus (Approvals & Visibility)

        private void ShowApprovalDropdown(Rectangle anchorRect, RibbonApprovalGroup ag)
        {
            var menu = new ContextMenuStrip
            {
                Font = new Font("Segoe UI", 9.5f)
            };

            var pal = CurrentPalette;
            menu.Renderer = new ToolStripProfessionalRenderer(new ZeroMenuColorTable(pal));

            foreach (var item in ag.Items)
            {
                // Decoupled status dot color resolving
                Color dotColor = item.StatusColor
                    ?? item.StatusColorResolver?.Invoke(item)
                    ?? ag.StatusColorResolver?.Invoke(item)
                    ?? ApprovalStatusColorResolver?.Invoke(item)
                    ?? (item.Count > 0 ? pal.Danger : Color.FromArgb(59, 130, 246));

                var dotImage = CreateDotImage(dotColor, 12);

                string text = $"  {item.GetDisplayText()}";
                var menuItem = new ToolStripMenuItem(text, dotImage)
                {
                    Font = item.Count > 0 ? new Font(menu.Font, FontStyle.Bold) : menu.Font
                };

                menuItem.Click += (s, e) =>
                {
                    item.ClickAction?.Invoke();
                    ApprovalItemClick?.Invoke(this, item);
                };

                menu.Items.Add(menuItem);
            }

            var screenLoc = PointToScreen(new Point(anchorRect.Left, anchorRect.Bottom + 2));
            menu.Show(screenLoc);
        }

        private void ShowVisibilityOptionsDropdown(Rectangle anchorRect)
        {
            var menu = new ContextMenuStrip
            {
                Font = new Font("Segoe UI", 9f)
            };

            var activePage = SelectedPage;
            if (activePage == null) return;

            foreach (var ag in activePage.ApprovalGroups)
            {
                var chkItem = new ToolStripMenuItem(ag.Title)
                {
                    Checked = ag.Visible,
                    CheckOnClick = true
                };

                chkItem.CheckedChanged += (s, e) =>
                {
                    ag.Visible = chkItem.Checked;
                    var rg = activePage.Groups.Find(g => g.ApprovalGroup == ag);
                    if (rg != null) rg.Visible = chkItem.Checked;

                    // Automatically persist state if store configured
                    _stateStore?.SaveGroupVisibility(ag.GroupKey, ag.Visible);

                    Invalidate();
                    ApprovalVisibilityChanged?.Invoke(this, new ApprovalVisibilityChangedEventArgs(ag.GroupKey, ag.Visible));
                };

                menu.Items.Add(chkItem);
            }

            var screenLoc = PointToScreen(new Point(anchorRect.Left, anchorRect.Bottom + 2));
            menu.Show(screenLoc);
        }

        private static Bitmap CreateDotImage(Color color, int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var b = new SolidBrush(color))
                {
                    g.FillEllipse(b, 1, 1, size - 2, size - 2);
                }
            }
            return bmp;
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
        {
            int d = Math.Max(1, radius * 2);
            var path = new GraphicsPath();
            int r = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2);
            if (r <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            d = r * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        #endregion
    }

    internal class ZeroMenuColorTable : ProfessionalColorTable
    {
        private readonly ZeroThemePalette _pal;
        public ZeroMenuColorTable(ZeroThemePalette pal) => _pal = pal;
        public override Color MenuItemSelected => _pal.Hover;
        public override Color ToolStripDropDownBackground => _pal.Surface;
        public override Color MenuBorder => _pal.Border;
        public override Color MenuItemBorder => Color.Transparent;
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZRibbon"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("RibbonControl is deprecated and will be removed in 5 release cycles. Please migrate to ZRibbon instead.")]
    [ToolboxItem(false)]
    public class RibbonControl : ZRibbon
    {
    }

    #endregion
}
