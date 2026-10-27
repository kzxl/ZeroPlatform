using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Navigation
{
    public class SideNavItem
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "Nav Item";
        public string Icon { get; set; } = "📌";
        public string Category { get; set; } = "";
        public int BadgeCount { get; set; } = 0;
        public Color? BadgeColor { get; set; }
        public Control? AssociatedView { get; set; }

        internal Rectangle Bounds { get; set; }

        public SideNavItem() { }

        public SideNavItem(string id, string title, string icon, string category = "", int badgeCount = 0, Control? view = null)
        {
            Id = id;
            Title = title;
            Icon = icon;
            Category = category;
            BadgeCount = badgeCount;
            AssociatedView = view;
        }
    }

    public class SideNavEventArgs : EventArgs
    {
        public SideNavItem Item { get; }
        public int Index { get; }

        public SideNavEventArgs(SideNavItem item, int index)
        {
            Item = item;
            Index = index;
        }
    }

    /// <summary>
    /// Modern Enterprise Sidebar Navigation control for WinForms applications.
    /// Supports brand header, category section grouping, badges, collapsible rail (230px ⇄ 64px),
    /// and automated view switching for seamless dashboard modularity.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Overlays & Navigation")]
    [DefaultEvent("ItemSelected")]
    [Description("Enterprise Sidebar Navigation with brand header, categorized items, and collapsible rail")]
    [ToolboxBitmap(typeof(ZeroIcons), "SideNavControl.bmp")]
    public class ZSideNav : Control, IZeroDpiScalable
    {
        private readonly List<SideNavItem> _items = new List<SideNavItem>();
        private int _selectedIndex = 0;
        private int _hoveredIndex = -1;
        private bool _isCollapsed = false;
        private int _baseExpandedWidth = 230;
        private int _baseCollapsedWidth = 64;
        private int _baseHeaderHeight = 56;
        private int _baseItemHeight = 40;
        private int _baseCatHeight = 24;
        private int _expandedWidth = 230;
        private int _collapsedWidth = 64;
        private float _currentDpiScale = 1.0f;

        private string _brandLogo = "⚡";
        private string _brandTitle = "ZeroUI Suite";
        private string _brandSubtitle = "Enterprise Workstation";

        private Panel? _contentContainer;
        private readonly ToolTip _toolTip = new ToolTip();
        private int _lastTooltipIndex = -1;

        public event EventHandler<SideNavEventArgs>? ItemSelected;
        public event EventHandler? CollapseChanged;

        [Browsable(false)]
        public float DpiScale => _currentDpiScale;

        public void ApplyDpiScaling(float scaleFactor)
        {
            if (scaleFactor <= 0f) scaleFactor = 1.0f;
            _currentDpiScale = scaleFactor;

            _expandedWidth = (int)Math.Round(_baseExpandedWidth * scaleFactor);
            _collapsedWidth = (int)Math.Round(_baseCollapsedWidth * scaleFactor);
            Width = _isCollapsed ? _collapsedWidth : _expandedWidth;
            Font = new Font(Font.FontFamily, 9.5f * scaleFactor, Font.Style);
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

        public ZSideNav()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Dock = DockStyle.Left;
            Width = _expandedWidth;
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            BackColor = Color.Transparent;

            ZeroTheme.ThemeChanged += OnThemeChanged;
            ZeroUIConfig.CornerStyleChanged += (s, e) => Invalidate();
        }

        #region Public Properties

        [Category("Data")]
        [Browsable(false)]
        public List<SideNavItem> Items => _items;

        [Category("Appearance")]
        [DefaultValue("⚡")]
        public string BrandLogo
        {
            get => _brandLogo;
            set { _brandLogo = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("ZeroUI Suite")]
        public string BrandTitle
        {
            get => _brandTitle;
            set { _brandTitle = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("Enterprise Workstation")]
        public string BrandSubtitle
        {
            get => _brandSubtitle;
            set { _brandSubtitle = value ?? ""; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool IsCollapsed
        {
            get => _isCollapsed;
            set
            {
                if (_isCollapsed != value)
                {
                    _isCollapsed = value;
                    Width = _isCollapsed ? _collapsedWidth : _expandedWidth;
                    CollapseChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(230)]
        public int ExpandedWidth
        {
            get => _expandedWidth;
            set
            {
                if (_expandedWidth != value && value >= 160)
                {
                    _expandedWidth = value;
                    if (!_isCollapsed) Width = _expandedWidth;
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(0)]
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_items.Count == 0)
                {
                    _selectedIndex = -1;
                    return;
                }

                int clamped = Math.Max(0, Math.Min(_items.Count - 1, value));
                if (_selectedIndex != clamped)
                {
                    _selectedIndex = clamped;
                    SyncAssociatedView();
                    ItemSelected?.Invoke(this, new SideNavEventArgs(_items[_selectedIndex], _selectedIndex));
                    Invalidate();
                }
            }
        }

        [Browsable(false)]
        public SideNavItem? SelectedItem => (_selectedIndex >= 0 && _selectedIndex < _items.Count) ? _items[_selectedIndex] : null;

        #endregion

        #region Public API

        public SideNavItem AddItem(string id, string title, string icon, string category = "", int badgeCount = 0, Control? view = null)
        {
            var item = new SideNavItem(id, title, icon, category, badgeCount, view);
            _items.Add(item);
            if (_contentContainer != null && view != null)
            {
                view.Dock = DockStyle.Fill;
                view.Visible = (_items.Count - 1 == _selectedIndex);
                if (!_contentContainer.Controls.Contains(view))
                {
                    _contentContainer.Controls.Add(view);
                }
            }
            Invalidate();
            return item;
        }

        public void BindContentContainer(Panel container)
        {
            _contentContainer = container;
            _contentContainer.SuspendLayout();
            for (int i = 0; i < _items.Count; i++)
            {
                var view = _items[i].AssociatedView;
                if (view != null)
                {
                    view.Dock = DockStyle.Fill;
                    view.Visible = (i == _selectedIndex);
                    if (!_contentContainer.Controls.Contains(view))
                    {
                        _contentContainer.Controls.Add(view);
                    }
                }
            }
            _contentContainer.ResumeLayout(true);
        }

        public void ToggleCollapse()
        {
            IsCollapsed = !IsCollapsed;
        }

        #endregion

        private void SyncAssociatedView()
        {
            if (_contentContainer == null) return;
            _contentContainer.SuspendLayout();
            for (int i = 0; i < _items.Count; i++)
            {
                var view = _items[i].AssociatedView;
                if (view != null)
                {
                    view.Visible = (i == _selectedIndex);
                }
            }
            _contentContainer.ResumeLayout(true);
        }

        #region Mouse Interaction

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int prevHover = _hoveredIndex;
            _hoveredIndex = -1;

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Bounds.Contains(e.Location))
                {
                    _hoveredIndex = i;
                    break;
                }
            }

            if (prevHover != _hoveredIndex)
            {
                Cursor = (_hoveredIndex >= 0 || e.Y <= (int)Math.Round(_baseHeaderHeight * _currentDpiScale)) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }

            // Show tooltip when collapsed
            if (_isCollapsed && _hoveredIndex >= 0 && _hoveredIndex != _lastTooltipIndex)
            {
                _lastTooltipIndex = _hoveredIndex;
                _toolTip.SetToolTip(this, _items[_hoveredIndex].Title);
            }
            else if (_hoveredIndex < 0 && _lastTooltipIndex >= 0)
            {
                _lastTooltipIndex = -1;
                _toolTip.Hide(this);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredIndex = -1;
            _lastTooltipIndex = -1;
            _toolTip.Hide(this);
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            // Toggle collapse when clicking brand header area
            int headerH = (int)Math.Round(_baseHeaderHeight * _currentDpiScale);
            if (e.Y <= headerH)
            {
                ToggleCollapse();
                return;
            }

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Bounds.Contains(e.Location))
                {
                    SelectedIndex = i;
                    return;
                }
            }
        }

        #endregion

        #region Rendering Engine

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = ZeroTheme.Colors;
            int w = Width;
            int h = Height;

            // 1. Sidebar Background & Right Dividing Border
            using (var bgBrush = new SolidBrush(palette.HeaderBackground))
            {
                g.FillRectangle(bgBrush, 0, 0, w, h);
            }
            using (var borderPen = new Pen(palette.Border, 1f))
            {
                g.DrawLine(borderPen, w - 1, 0, w - 1, h);
            }

            // 2. Brand Header (Top 56px)
            int headerH = (int)Math.Round(_baseHeaderHeight * _currentDpiScale);
            var logoFont = ZeroFontCache.Get("Segoe UI Emoji", 15f * _currentDpiScale, FontStyle.Regular);
            var titleFont = ZeroFontCache.Get("Segoe UI", 10.5f * _currentDpiScale, FontStyle.Bold);
            var subFont = ZeroFontCache.Get("Segoe UI", 7.5f * _currentDpiScale, FontStyle.Regular);
            using (var logoBrush = new SolidBrush(palette.Primary))
            using (var titleBrush = new SolidBrush(palette.TextPrimary))
            using (var subBrush = new SolidBrush(palette.TextSecondary))
            {
                if (!_isCollapsed)
                {
                    g.DrawString(_brandLogo, logoFont, logoBrush, 14 * _currentDpiScale, 12 * _currentDpiScale);
                    g.DrawString(_brandTitle, titleFont, titleBrush, 44 * _currentDpiScale, 10 * _currentDpiScale);
                    g.DrawString(_brandSubtitle, subFont, subBrush, 45 * _currentDpiScale, 30 * _currentDpiScale);
                }
                else
                {
                    var logoSz = g.MeasureString(_brandLogo, logoFont);
                    g.DrawString(_brandLogo, logoFont, logoBrush, (w - logoSz.Width) / 2f, 14 * _currentDpiScale);
                }
            }

            using (var sepPen = new Pen(palette.Border, 1f))
            {
                g.DrawLine(sepPen, 8 * _currentDpiScale, headerH, w - 8 * _currentDpiScale, headerH);
            }

            // 3. Render Items & Category Sections
            int curY = headerH + (int)Math.Round(10 * _currentDpiScale);
            string lastCategory = "";
            int itemH = (int)Math.Round(_baseItemHeight * _currentDpiScale);
            int paddingX = _isCollapsed ? (int)Math.Round(6 * _currentDpiScale) : (int)Math.Round(10 * _currentDpiScale);
            int itemW = w - (paddingX * 2);

            var catFont = ZeroFontCache.Get("Segoe UI", 7.5f * _currentDpiScale, FontStyle.Bold);
            var itemFont = ZeroFontCache.Get("Segoe UI", 9.2f * _currentDpiScale, FontStyle.Regular);
            var itemBold = ZeroFontCache.Get("Segoe UI", 9.2f * _currentDpiScale, FontStyle.Bold);
            var iconFont = ZeroFontCache.Get("Segoe UI Emoji", 11f * _currentDpiScale, FontStyle.Regular);
            var badgeFont = ZeroFontCache.Get("Segoe UI", 7.5f * _currentDpiScale, FontStyle.Bold);

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];

                // Category Section Header (only in expanded mode)
                if (!_isCollapsed && !string.IsNullOrEmpty(item.Category) && item.Category != lastCategory)
                {
                    lastCategory = item.Category;
                    using var catBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
                    g.DrawString(item.Category.ToUpperInvariant(), catFont, catBrush, 16 * _currentDpiScale, curY + 4 * _currentDpiScale);
                    curY += (int)Math.Round(_baseCatHeight * _currentDpiScale);
                }

                item.Bounds = new Rectangle(paddingX, curY, itemW, itemH);
                bool isSelected = (i == _selectedIndex);
                bool isHovered = (i == _hoveredIndex);

                int effRadius = ZeroUIConfig.GetEffectiveRadius((int)Math.Round(6 * _currentDpiScale));

                // Item Shape Background
                if (isSelected)
                {
                    using var selBrush = new SolidBrush(Color.FromArgb(24, palette.Primary));
                    using var selPath = ZeroUIConfig.CreateRoundedRectangle(item.Bounds, effRadius);
                    g.FillPath(selBrush, selPath);

                    // Left active indicator pill
                    using var indBrush = new SolidBrush(palette.Primary);
                    using var indPath = ZeroUIConfig.CreateRoundedRectangle(new Rectangle(paddingX + (int)Math.Round(2 * _currentDpiScale), curY + (int)Math.Round(6 * _currentDpiScale), (int)Math.Round(3 * _currentDpiScale), itemH - (int)Math.Round(12 * _currentDpiScale)), (int)Math.Round(2 * _currentDpiScale));
                    g.FillPath(indBrush, indPath);
                }
                else if (isHovered)
                {
                    using var hovBrush = new SolidBrush(Color.FromArgb(12, palette.Primary));
                    using var hovPath = ZeroUIConfig.CreateRoundedRectangle(item.Bounds, effRadius);
                    g.FillPath(hovBrush, hovPath);
                }

                // Render Icon
                int iconX = _isCollapsed ? (w - (int)Math.Round(20 * _currentDpiScale)) / 2 : paddingX + (int)Math.Round(14 * _currentDpiScale);
                Color iconColor = isSelected ? palette.Primary : (isHovered ? palette.TextPrimary : palette.TextSecondary);
                using (var iconBrush = new SolidBrush(iconColor))
                {
                    g.DrawString(item.Icon, iconFont, iconBrush, iconX, curY + (itemH - (int)Math.Round(20 * _currentDpiScale)) / 2);
                }

                // Render Text & Badge (only in expanded mode)
                if (!_isCollapsed)
                {
                    int textX = paddingX + (int)Math.Round(42 * _currentDpiScale);
                    Color textColor = isSelected ? palette.Primary : (isHovered ? palette.TextPrimary : palette.TextSecondary);
                    var curFont = isSelected ? itemBold : itemFont;

                    using (var textBrush = new SolidBrush(textColor))
                    using (var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, LineAlignment = StringAlignment.Center })
                    {
                        int textMaxW = item.Bounds.Right - textX - (item.BadgeCount > 0 ? (int)Math.Round(36 * _currentDpiScale) : (int)Math.Round(8 * _currentDpiScale));
                        RectangleF textRect = new RectangleF(textX, curY, textMaxW, itemH);
                        g.DrawString(item.Title, curFont, textBrush, textRect, sf);
                    }

                    // Badge Pill
                    if (item.BadgeCount > 0)
                    {
                        string bStr = item.BadgeCount > 99 ? "99+" : item.BadgeCount.ToString();
                        var bSz = g.MeasureString(bStr, badgeFont);
                        int bW = Math.Max((int)Math.Round(18 * _currentDpiScale), (int)bSz.Width + (int)Math.Round(8 * _currentDpiScale));
                        int bH = (int)Math.Round(16 * _currentDpiScale);
                        int bX = item.Bounds.Right - bW - (int)Math.Round(8 * _currentDpiScale);
                        var bRect = new Rectangle(bX, curY + (itemH - bH) / 2, bW, bH);

                        Color bColor = item.BadgeColor ?? palette.Danger;
                        using var bBrush = new SolidBrush(bColor);
                        using var bPath = ZeroUIConfig.CreateRoundedRectangle(bRect, (int)Math.Round(8 * _currentDpiScale));
                        g.FillPath(bBrush, bPath);

                        using var bTextBrush = new SolidBrush(Color.White);
                        var sfBadge = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(bStr, badgeFont, bTextBrush, bRect, sfBadge);
                    }
                }

                curY += itemH + (int)Math.Round(4 * _currentDpiScale);
            }

            // 4. Bottom Rail Footer: Collapse hint or status
            int footerH = (int)Math.Round(36 * _currentDpiScale);
            int footerY = h - footerH;
            using (var sepPen = new Pen(palette.Border, 1f))
            {
                g.DrawLine(sepPen, 8 * _currentDpiScale, footerY, w - 8 * _currentDpiScale, footerY);
            }

            var footFont = ZeroFontCache.Get("Segoe UI", 7.5f * _currentDpiScale, FontStyle.Regular);
            using (var footBrush = new SolidBrush(palette.TextSecondary))
            {
                if (!_isCollapsed)
                {
                    g.DrawString("v2.4 Enterprise • Ready", footFont, footBrush, 14 * _currentDpiScale, footerY + 10 * _currentDpiScale);
                }
                else
                {
                    g.DrawString("⚡", footFont, footBrush, (w - 12 * _currentDpiScale) / 2f, footerY + 10 * _currentDpiScale);
                }
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
                _toolTip.Dispose();
            }
            base.Dispose(disposing);
        }
    
    private void OnThemeChanged(object sender, EventArgs e) => Invalidate();

}

    [Obsolete("Use SideNavItem instead.")]
    public class ZeroSideNavItem : SideNavItem
    {
        public ZeroSideNavItem() : base() { }
        public ZeroSideNavItem(string id, string title, string icon, string category = "", int badgeCount = 0, Control? view = null)
            : base(id, title, icon, category, badgeCount, view) { }
    }

    [Obsolete("Use SideNavEventArgs instead.")]
    public class ZeroSideNavEventArgs : SideNavEventArgs
    {
        public ZeroSideNavEventArgs(SideNavItem item, int index) : base(item, index) { }
    }

    [Obsolete("Use SideNavControl instead.")]
    [ToolboxItem(false)]
    public class ZeroSideNav : SideNavControl
    {
    }

    /// <summary>
    /// Alias for <see cref="ZSideNav"/> for 100% cross-platform parity with WPF SideNav.
    /// </summary>
    [ToolboxItem(true)]
    public class SideNav : ZSideNav
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSideNav"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SideNavControl is deprecated and will be removed in 5 release cycles. Please migrate to ZSideNav instead.")]
    [ToolboxItem(false)]
    public class SideNavControl : ZSideNav
    {
    }

    #endregion
}
