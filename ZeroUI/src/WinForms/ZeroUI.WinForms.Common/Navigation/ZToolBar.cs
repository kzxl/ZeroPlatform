using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Navigation
{
    public enum ToolbarItemType

    {
        Button,
        Separator,
        Spacer,
        Dropdown
    }

    public abstract class ToolbarItem
    {
        public string Text { get; set; } = "";
        public string? Glyph { get; set; }
        public string? Tooltip { get; set; }
        public string? ShortcutText { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool IsVisible { get; set; } = true;
        public object? Tag { get; set; }

        public int? Width { get; set; }
        public int MinWidth { get; set; } = 36;
        public bool AutoWidth { get; set; } = true;
        public Padding? CustomPadding { get; set; }

        public Rectangle Bounds { get; internal set; }

        public event EventHandler? Click;

        internal void OnClick() => Click?.Invoke(this, EventArgs.Empty);
    }

    public class ToolbarButton : ToolbarItem
    {
        public bool IsPrimary { get; set; }
        public bool IsDanger { get; set; }
        public int? BadgeCount { get; set; }
        public bool ShowBadgeDot { get; set; }
        public Color? BadgeColor { get; set; }
        public Color? BackColor { get; set; }
        public Color? ForeColor { get; set; }
        public bool IsSelected { get; set; }

        public ToolbarButton() { }

        public ToolbarButton(string text, string? glyph = null, EventHandler? onClick = null, string? shortcut = null)
        {
            Text = text;
            Glyph = glyph;
            ShortcutText = shortcut;
            if (onClick != null) Click += onClick;
        }
    }

    public class ToolbarSeparator : ToolbarItem
    {
        public ToolbarSeparator()
        {
            IsEnabled = false;
        }
    }

    public class ToolbarSpacer : ToolbarItem
    {
        public ToolbarSpacer()
        {
            IsEnabled = false;
        }
    }

    public class ToolbarDropdown : ToolbarItem
    {
        public event EventHandler? DropdownOpened;

        public ToolbarDropdown() { }

        public ToolbarDropdown(string text, string? glyph = null, EventHandler? onDropdown = null)
        {
            Text = text;
            Glyph = glyph;
            if (onDropdown != null) DropdownOpened += onDropdown;
        }

        internal void OnDropdown() => DropdownOpened?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Modern single-HWND flat enterprise action toolbar for ZeroUI.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Overlays")]
    [Description("Flat enterprise action toolbar with buttons, dividers, and elastic spacers")]
    [ToolboxBitmap(typeof(ZeroIcons), "ToolbarControl.bmp")]
    public class ZToolBar : Control, IZeroDpiScalable
    {
        private readonly List<ToolbarItem> _items = new List<ToolbarItem>();
        private Color _borderColor = Color.FromArgb(229, 231, 235);
        private int _baseHeight = 44;
        private int _baseItemHeight = 32;
        private float _currentDpiScale = 1.0f;
        private int _itemHeight = 32;
        private ToolbarItem? _hoveredItem;
        private ToolbarItem? _pressedItem;
        private readonly ToolTip _toolTip = new ToolTip();

        /// <summary>
        /// Gets the active High-DPI Per-Monitor V2 scale factor applied to this Toolbar.
        /// </summary>
        [Browsable(false)]
        public float DpiScale => _currentDpiScale;

        /// <summary>
        /// Applies High-DPI Per-Monitor V2 scaling to Toolbar metrics (Height, ItemHeight, and layout).
        /// </summary>
        public void ApplyDpiScaling(float scaleFactor)
        {
            if (scaleFactor <= 0f) scaleFactor = 1.0f;
            _currentDpiScale = scaleFactor;

            Height = Math.Max(28, (int)Math.Round(_baseHeight * scaleFactor));
            _itemHeight = Math.Max(20, (int)Math.Round(_baseItemHeight * scaleFactor));
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

        public ZToolBar()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);

            Dock = DockStyle.Top;
            Height = 44;
            BackColor = ZeroTheme.Colors.Surface;
            _borderColor = ZeroTheme.Colors.Border;
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            Padding = new Padding(8, 6, 8, 6);

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(object? sender, EventArgs e)
        {
            BackColor = ZeroTheme.Colors.Surface;
            _borderColor = ZeroTheme.Colors.Border;
            Invalidate();
        }

        [Browsable(false)]
        public List<ToolbarItem> Items => _items;

        [Category("Appearance")]
        public Color BorderColor
        {
            get => _borderColor;
            set { _borderColor = value; Invalidate(); }
        }

        [Category("Layout")]
        [DefaultValue(32)]
        public int ItemHeight
        {
            get => _itemHeight;
            set
            {
                _baseItemHeight = (int)Math.Round(value / _currentDpiScale);
                _itemHeight = Math.Max(20, value);
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        [Description("Renders items as a contiguous connected segmented pill toolbar with vertical dividers.")]
        public bool IsSegmented { get; set; } = false;

        public ToolbarButton AddButton(string text, string? glyph = null, EventHandler? onClick = null, string? shortcut = null)
        {
            var btn = new ToolbarButton(text, glyph, onClick, shortcut);
            _items.Add(btn);
            Invalidate();
            return btn;
        }

        public void AddSeparator()
        {
            _items.Add(new ToolbarSeparator());
            Invalidate();
        }

        public void AddSpacer()
        {
            _items.Add(new ToolbarSpacer());
            Invalidate();
        }

        public ToolbarDropdown AddDropdown(string text, string? glyph = null, EventHandler? onDropdown = null)
        {
            var dd = new ToolbarDropdown(text, glyph, onDropdown);
            _items.Add(dd);
            Invalidate();
            return dd;
        }

        public void Clear()
        {
            _items.Clear();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // 1. Draw Background & Bottom Border
            using (var bgBrush = new SolidBrush(BackColor))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }
            if (!IsSegmented)
            {
                using var borderPen = new Pen(_borderColor, 1f);
                g.DrawLine(borderPen, 0, Height - 1, Width, Height - 1);
            }

            // 2. Measure & Layout Items
            LayoutItems(g);

            // 3. Draw Items
            int centerY = Height / 2;
            var palette = ZeroTheme.Colors;

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (!item.IsVisible || item is ToolbarSpacer) continue;

                if (item is ToolbarSeparator)
                {
                    int sepX = item.Bounds.X + (item.Bounds.Width / 2);
                    using var sepPen = new Pen(_borderColor, 1f);
                    g.DrawLine(sepPen, sepX, centerY - 10, sepX, centerY + 10);
                    continue;
                }

                bool isHovered = (item == _hoveredItem && item.IsEnabled);
                bool isPressed = (item == _pressedItem && item.IsEnabled);
                var btn = item as ToolbarButton;

                // Determine background color
                Color? customBg = btn?.BackColor;
                Color btnBg = Color.Transparent;

                if (customBg.HasValue)
                {
                    btnBg = customBg.Value;
                    if (isHovered && !isPressed) btnBg = ControlPaint.Light(btnBg, 0.15f);
                    if (isPressed) btnBg = ControlPaint.Dark(btnBg, 0.15f);
                }
                else if (btn != null && btn.IsPrimary)
                {
                    btnBg = isPressed ? palette.PrimaryHover : (isHovered ? palette.PrimaryHover : palette.Primary);
                }
                else if (btn != null && btn.IsSelected)
                {
                    btnBg = ZeroTheme.IsDark ? Color.FromArgb(50, 62, 95) : Color.FromArgb(238, 242, 255);
                }
                else if (isPressed || isHovered)
                {
                    btnBg = palette.Hover;
                }

                if (btnBg != Color.Transparent)
                {
                    int radius = IsSegmented ? 0 : 6;
                    if (radius > 0)
                    {
                        using var path = CreateRoundedRectangle(item.Bounds, radius);
                        using var brush = new SolidBrush(btnBg);
                        g.FillPath(brush, path);
                    }
                    else
                    {
                        using var brush = new SolidBrush(btnBg);
                        g.FillRectangle(brush, item.Bounds);
                    }
                }

                // If segmented mode, draw vertical divider to the right of item
                if (IsSegmented && i < _items.Count - 1 && !(_items[i + 1] is ToolbarSpacer))
                {
                    using var divPen = new Pen(Color.FromArgb(40, ZeroTheme.Colors.Border), 1f);
                    g.DrawLine(divPen, item.Bounds.Right, item.Bounds.Top + 4, item.Bounds.Right, item.Bounds.Bottom - 4);
                }

                // Text & Content Color
                Color textColor = !item.IsEnabled ? palette.TextSecondary
                    : (btn?.ForeColor ?? ((btn != null && (btn.IsPrimary || (btn.BackColor.HasValue && btn.BackColor.Value.GetBrightness() < 0.55f)))
                        ? Color.White : palette.TextPrimary));

                int padL = item.CustomPadding?.Left ?? 12;
                int contentX = item.Bounds.Left + padL;

                // Draw Glyph
                if (!string.IsNullOrEmpty(item.Glyph))
                {
                    Rectangle glyphRect = new Rectangle(contentX, item.Bounds.Top, 20, item.Bounds.Height);
                    TextRenderer.DrawText(g, item.Glyph, ZeroFontCache.Get("Segoe UI", 10.5f, FontStyle.Regular), glyphRect, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    contentX += 22;
                }

                // Draw Text (Guaranteed not to cut off)
                if (!string.IsNullOrEmpty(item.Text))
                {
                    Size textSize = TextRenderer.MeasureText(g, item.Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
                    int availTextWidth = Math.Max(textSize.Width + 6, item.Bounds.Right - contentX - (btn?.ShowBadgeDot == true ? 20 : (btn?.BadgeCount > 0 ? 30 : 6)));
                    Rectangle textRect = new Rectangle(contentX, item.Bounds.Top, availTextWidth, item.Bounds.Height);
                    TextRenderer.DrawText(g, item.Text, Font, textRect, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    contentX += textSize.Width + 6;
                }

                // Draw Dropdown Chevron (▾)
                if (item is ToolbarDropdown)
                {
                    Rectangle chevRect = new Rectangle(contentX, item.Bounds.Top, 14, item.Bounds.Height);
                    TextRenderer.DrawText(g, "▾", ZeroFontCache.Get("Segoe UI", 9f, FontStyle.Regular), chevRect, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    contentX += 14;
                }

                // Draw Badge Dot (Dirty indicator / Unsaved changes)
                if (btn != null && btn.ShowBadgeDot)
                {
                    int dotSize = 10;
                    int dotX = item.Bounds.Right - (item.CustomPadding?.Right ?? 10) - dotSize;
                    int dotY = item.Bounds.Top + (item.Bounds.Height - dotSize) / 2;
                    Color dotColor = btn.BadgeColor ?? Color.FromArgb(239, 68, 68);

                    using var dotBrush = new SolidBrush(dotColor);
                    using var dotBorder = new Pen(Color.White, 1.2f);
                    g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
                    g.DrawEllipse(dotBorder, dotX, dotY, dotSize, dotSize);
                }
                // Draw Badge Count
                else if (btn != null && btn.BadgeCount.HasValue && btn.BadgeCount.Value > 0)
                {
                    string badgeStr = btn.BadgeCount.Value > 99 ? "99+" : btn.BadgeCount.Value.ToString();
                    using var badgeFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
                    Size badgeSize = TextRenderer.MeasureText(g, badgeStr, badgeFont);
                    int badgeW = Math.Max(16, badgeSize.Width + 6);
                    int badgeH = 16;
                    Rectangle badgeRect = new Rectangle(item.Bounds.Right - badgeW - 6, item.Bounds.Top + 4, badgeW, badgeH);

                    using var bPath = CreateRoundedRectangle(badgeRect, 8);
                    using var bBrush = new SolidBrush(btn.BadgeColor ?? Color.FromArgb(239, 68, 68));
                    g.FillPath(bBrush, bPath);

                    TextRenderer.DrawText(g, badgeStr, badgeFont, badgeRect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }

            // If Segmented, draw outer capsule border around all visible items
            if (IsSegmented && _items.Any(it => it.IsVisible && !(it is ToolbarSpacer)))
            {
                var visibleItems = _items.Where(it => it.IsVisible && !(it is ToolbarSpacer)).ToList();
                int minX = visibleItems.Min(it => it.Bounds.Left);
                int maxX = visibleItems.Max(it => it.Bounds.Right);
                var segBounds = new Rectangle(minX, visibleItems.First().Bounds.Top, maxX - minX, _itemHeight);

                using var segPath = CreateRoundedRectangle(segBounds, 7);
                using var segPen = new Pen(_borderColor, 1.2f);
                g.DrawPath(segPen, segPath);
            }
        }

        private void LayoutItems(Graphics g)
        {
            int centerY = Height / 2;
            int top = centerY - (_itemHeight / 2);

            int leftX = Padding.Left;
            int rightX = Width - Padding.Right;

            // Find if there is a Spacer
            int spacerIndex = -1;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] is ToolbarSpacer)
                {
                    spacerIndex = i;
                    break;
                }
            }

            int itemGap = IsSegmented ? 0 : 4;

            // Measure and place left items
            int leftLimit = (spacerIndex == -1) ? _items.Count : spacerIndex;
            for (int i = 0; i < leftLimit; i++)
            {
                var item = _items[i];
                if (!item.IsVisible) continue;

                int itemW = MeasureItemWidth(g, item);
                item.Bounds = new Rectangle(leftX, top, itemW, _itemHeight);
                leftX += itemW + itemGap;
            }

            // Measure and place right items
            if (spacerIndex != -1)
            {
                for (int i = _items.Count - 1; i > spacerIndex; i--)
                {
                    var item = _items[i];
                    if (!item.IsVisible) continue;

                    int itemW = MeasureItemWidth(g, item);
                    if (rightX - itemW < leftX + 8)
                    {
                        item.Bounds = Rectangle.Empty;
                        continue;
                    }
                    rightX -= itemW;
                    item.Bounds = new Rectangle(rightX, top, itemW, _itemHeight);
                    rightX -= itemGap;
                }
            }
        }

        private int MeasureItemWidth(Graphics g, ToolbarItem item)
        {
            if (item is ToolbarSeparator) return 12;
            if (item is ToolbarSpacer) return 0;

            if (!item.AutoWidth && item.Width.HasValue && item.Width.Value > 0)
            {
                return Math.Max(item.MinWidth, item.Width.Value);
            }

            int padL = item.CustomPadding?.Left ?? 12;
            int padR = item.CustomPadding?.Right ?? 12;
            int w = padL + padR;

            if (!string.IsNullOrEmpty(item.Glyph))
            {
                w += 22;
            }

            if (!string.IsNullOrEmpty(item.Text))
            {
                Size s = TextRenderer.MeasureText(g, item.Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
                w += s.Width + 8; // Extra safety buffer so text is never truncated to "L..."
            }

            if (item is ToolbarDropdown)
            {
                w += 16;
            }

            if (item is ToolbarButton btn)
            {
                if (btn.ShowBadgeDot)
                {
                    w += 18;
                }
                else if (btn.BadgeCount.HasValue && btn.BadgeCount.Value > 0)
                {
                    string badgeStr = btn.BadgeCount.Value > 99 ? "99+" : btn.BadgeCount.Value.ToString();
                    using var badgeFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
                    Size badgeSize = TextRenderer.MeasureText(g, badgeStr, badgeFont);
                    int badgeW = Math.Max(16, badgeSize.Width + 6);
                    w += badgeW + 8;
                }
            }

            return Math.Max(item.MinWidth, w);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            ToolbarItem? found = null;
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item.IsVisible && item.IsEnabled && !(item is ToolbarSeparator) && !(item is ToolbarSpacer))
                {
                    if (item.Bounds.Contains(e.Location))
                    {
                        found = item;
                        break;
                    }
                }
            }

            if (_hoveredItem != found)
            {
                _hoveredItem = found;
                Cursor = found != null ? Cursors.Hand : Cursors.Default;
                Invalidate();

                if (found != null && !string.IsNullOrEmpty(found.Tooltip))
                {
                    _toolTip.SetToolTip(this, found.Tooltip);
                }
                else
                {
                    _toolTip.SetToolTip(this, null);
                }
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredItem != null)
            {
                _hoveredItem = null;
                _toolTip.SetToolTip(this, null);
                Cursor = Cursors.Default;
                Invalidate();
            }
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
            if (e.Button == MouseButtons.Left && _pressedItem != null)
            {
                var clicked = _pressedItem;
                _pressedItem = null;
                Invalidate();

                if (clicked.Bounds.Contains(e.Location))
                {
                    if (clicked is ToolbarDropdown dd)
                    {
                        dd.OnDropdown();
                    }
                    clicked.OnClick();
                }
            }
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius) =>
            ZeroUIConfig.CreateRoundedRectangle(rect, radius);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ZeroTheme.ThemeChanged -= OnThemeChanged;
                _toolTip.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    [Obsolete("Use ToolbarItemType instead.")]
    public enum ZeroToolbarItemType
    {
        Button = ToolbarItemType.Button,
        Separator = ToolbarItemType.Separator,
        Spacer = ToolbarItemType.Spacer,
        Dropdown = ToolbarItemType.Dropdown
    }

    [Obsolete("Use ToolbarItem instead.")]
    public abstract class ZeroToolbarItem : ToolbarItem
    {
    }

    [Obsolete("Use ToolbarButton instead.")]
    public class ZeroToolbarButton : ToolbarButton
    {
        public ZeroToolbarButton() : base() { }
        public ZeroToolbarButton(string text, string? glyph = null, EventHandler? onClick = null, string? shortcut = null)
            : base(text, glyph, onClick, shortcut) { }
    }

    [Obsolete("Use ToolbarSeparator instead.")]
    public class ZeroToolbarSeparator : ToolbarSeparator
    {
    }

    [Obsolete("Use ToolbarSpacer instead.")]
    public class ZeroToolbarSpacer : ToolbarSpacer
    {
    }

    [Obsolete("Use ToolbarDropdown instead.")]
    public class ZeroToolbarDropdown : ToolbarDropdown
    {
        public ZeroToolbarDropdown() : base() { }
        public ZeroToolbarDropdown(string text, string? glyph = null, EventHandler? onDropdown = null)
            : base(text, glyph, onDropdown) { }
    }

    [Obsolete("Use ToolbarControl instead.")]
    [ToolboxItem(false)]
    public class ZeroToolbar : ToolbarControl
    {
        public new ZeroToolbarButton AddButton(string text, string? glyph = null, EventHandler? onClick = null, string? shortcut = null)
        {
            var btn = new ZeroToolbarButton(text, glyph, onClick, shortcut);
            Items.Add(btn);
            Invalidate();
            return btn;
        }

        public new ZeroToolbarDropdown AddDropdown(string text, string? glyph = null, EventHandler? onDropdown = null)
        {
            var dd = new ZeroToolbarDropdown(text, glyph, onDropdown);
            Items.Add(dd);
            Invalidate();
            return dd;
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZToolBar"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ToolbarControl is deprecated and will be removed in 5 release cycles. Please migrate to ZToolBar instead.")]
    [ToolboxItem(false)]
    public class ToolbarControl : ZToolBar
    {
    }

    #endregion
}
