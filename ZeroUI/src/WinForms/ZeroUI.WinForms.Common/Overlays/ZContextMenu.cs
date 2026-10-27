using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Icons;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Overlays
{
    /// <summary>
    /// Modern anti-aliased context menu with rounded highlight pills, shortcut keys,
    /// danger action styling, checkable items, submenus, interactive editors (Search, Toggle, Slider, Combo, Numeric),
    /// and 100% theme reactivity.
    /// Can be assigned directly to any WinForms control's ContextMenuStrip property.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Overlays")]
    [Description("Modern anti-aliased context menu strip with pill highlights, interactive editors, and theme support")]
    public class ZContextMenu : ContextMenuStrip
    {
        public ZContextMenu()
        {
            Renderer = new ContextMenuRenderer();
            ShowImageMargin = false;
            ShowCheckMargin = false;
            DropShadowEnabled = true;
            Font = ZeroFontCache.Get("Segoe UI", 9.25f, FontStyle.Regular);
            Padding = new Padding(4, 6, 4, 6);
            BackColor = ZeroTheme.Colors.CardBackground;

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        #region Actions & Submenus

        public MenuItemControl AddAction(string text, Action onClick, string? shortcut = null, string? icon = null)
        {
            var item = new MenuItemControl(text, onClick)
            {
                ShortcutHint = shortcut,
                Glyph = icon
            };
            Items.Add(item);
            return item;
        }

        public MenuItemControl AddAction(string text, Action onClick, IconKey icon, string? shortcut = null)
        {
            var item = new MenuItemControl(text, onClick)
            {
                ShortcutHint = shortcut,
                Icon = icon
            };
            Items.Add(item);
            return item;
        }

        public MenuItemControl AddDangerAction(string text, Action onClick, string? shortcut = null, string? icon = null)
        {
            var item = new MenuItemControl(text, onClick)
            {
                ShortcutHint = shortcut,
                Glyph = icon,
                IsDanger = true
            };
            Items.Add(item);
            return item;
        }

        public MenuItemControl AddDangerAction(string text, Action onClick, IconKey icon, string? shortcut = null)
        {
            var item = new MenuItemControl(text, onClick)
            {
                ShortcutHint = shortcut,
                Icon = icon,
                IsDanger = true
            };
            Items.Add(item);
            return item;
        }

        public MenuItemControl AddCheckable(string text, bool isChecked, Action<bool> onToggle, string? icon = null)
        {
            var item = new MenuItemControl(text, null)
            {
                CheckOnClick = true,
                Checked = isChecked,
                Glyph = icon
            };
            item.CheckedChanged += (s, e) => onToggle(item.Checked);
            Items.Add(item);
            return item;
        }

        public MenuItemControl AddCheckable(string text, bool isChecked, Action<bool> onToggle, IconKey icon)
        {
            var item = new MenuItemControl(text, null)
            {
                CheckOnClick = true,
                Checked = isChecked,
                Icon = icon
            };
            item.CheckedChanged += (s, e) => onToggle(item.Checked);
            Items.Add(item);
            return item;
        }

        public ToolStripSeparator AddSeparator()
        {
            var sep = new ToolStripSeparator();
            Items.Add(sep);
            return sep;
        }

        public MenuItemControl AddSubMenu(string text, string? icon = null)
        {
            var item = new MenuItemControl(text, null)
            {
                Glyph = icon,
                DropDown = new ZContextMenu()
            };
            Items.Add(item);
            return item;
        }

        public MenuItemControl AddSubMenu(string text, IconKey icon)
        {
            var item = new MenuItemControl(text, null)
            {
                Icon = icon,
                DropDown = new ZContextMenu()
            };
            Items.Add(item);
            return item;
        }

        #endregion

        #region Interactive Editors (Search, Toggle, Slider, ComboBox, Numeric, Custom)

        /// <summary>
        /// Adds an embedded search box with vector search icon, debounce filtering, and quick-clear action.
        /// </summary>
        public MenuItemSearch AddSearch(string placeholder = "Search...", Action<string>? onSearch = null, int debounceMs = 250, int width = 220)
        {
            var searchItem = new MenuItemSearch(placeholder, onSearch, debounceMs, width);
            Items.Add(searchItem);
            return searchItem;
        }

        /// <summary>
        /// Adds an embedded iOS/Fluent style toggle switch item with title and optional description subtitle.
        /// </summary>
        public MenuItemToggle AddToggle(string text, bool isChecked = false, Action<bool>? onToggle = null, string? subtitle = null, int width = 220)
        {
            var toggleItem = new MenuItemToggle(text, isChecked, onToggle, subtitle, width);
            Items.Add(toggleItem);
            return toggleItem;
        }

        /// <summary>
        /// Adds an embedded continuous horizontal slider item with title, live value readout, and custom unit.
        /// </summary>
        public MenuItemSlider AddSlider(string title, int min = 0, int max = 100, int current = 50, Action<int>? onValueChanged = null, string unit = "", int width = 220)
        {
            var sliderItem = new MenuItemSlider(title, min, max, current, onValueChanged, unit, width);
            Items.Add(sliderItem);
            return sliderItem;
        }

        /// <summary>
        /// Adds an embedded ComboBox dropdown selector item for category/preset selection.
        /// </summary>
        public MenuItemComboBox AddComboBox(string label, object[]? items, int selectedIndex = -1, Action<object?>? onSelectionChanged = null, int width = 220)
        {
            var comboItem = new MenuItemComboBox(label, items, selectedIndex, onSelectionChanged, width);
            Items.Add(comboItem);
            return comboItem;
        }

        /// <summary>
        /// Adds an embedded numeric stepper with precision minus/plus buttons.
        /// </summary>
        public MenuItemNumeric AddNumeric(string label, decimal min = 0, decimal max = 100, decimal current = 1, decimal step = 1, Action<decimal>? onValueChanged = null, int width = 220)
        {
            var numItem = new MenuItemNumeric(label, min, max, current, step, onValueChanged, width);
            Items.Add(numItem);
            return numItem;
        }

        /// <summary>
        /// Adds an arbitrary custom control hosted safely inside the context menu.
        /// </summary>
        public MenuItemControlHost<T> AddCustom<T>(T control, Padding? margin = null) where T : Control
        {
            var host = new MenuItemControlHost<T>(control);
            if (margin.HasValue) host.Margin = margin.Value;
            Items.Add(host);
            return host;
        }

        #endregion
    
    private void OnThemeChanged(object sender, EventArgs e)
    {
                BackColor = ZeroTheme.Colors.CardBackground;
                Invalidate();
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

    /// <summary>
    /// Custom MenuItem supporting danger state, glyph emojis, vector IconKey, shortcut hints, and badge tags.
    /// </summary>
    public class MenuItemControl : ToolStripMenuItem
    {
        public bool IsDanger { get; set; } = false;
        public string? Glyph { get; set; }
        public IconKey? Icon { get; set; }
        public string? ShortcutHint { get; set; }
        public string? BadgeText { get; set; }
        public Color? BadgeColor { get; set; }

        public MenuItemControl() : base() { }

        public MenuItemControl(string text, Action? onClick) : base(text)
        {
            if (onClick != null) Click += (s, e) => onClick();
        }

        public override Size GetPreferredSize(Size constrainingSize)
        {
            var baseSize = base.GetPreferredSize(constrainingSize);
            int w = baseSize.Width + 36;
            if (!string.IsNullOrEmpty(ShortcutHint)) w += 65;
            if (!string.IsNullOrEmpty(BadgeText)) w += 40;
            if (!string.IsNullOrEmpty(Glyph) || Icon.HasValue) w += 24;
            return new Size(Math.Max(180, w), Math.Max(30, baseSize.Height + 6));
        }

        public MenuItemControl AddSubAction(string text, Action onClick, string? shortcut = null, string? icon = null)
        {
            var item = new MenuItemControl(text, onClick)
            {
                ShortcutHint = shortcut,
                Glyph = icon
            };
            DropDownItems.Add(item);
            return item;
        }

        public MenuItemControl AddSubAction(string text, Action onClick, IconKey icon, string? shortcut = null)
        {
            var item = new MenuItemControl(text, onClick)
            {
                ShortcutHint = shortcut,
                Icon = icon
            };
            DropDownItems.Add(item);
            return item;
        }
    }

    /// <summary>
    /// Custom ToolStripRenderer rendering anti-aliased rounded pills, theme borders, and typography.
    /// </summary>
    public class ContextMenuRenderer : ToolStripRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var palette = ZeroTheme.Colors;
            using var brushBg = new SolidBrush(palette.CardBackground);
            g.FillRectangle(brushBg, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var palette = ZeroTheme.Colors;
            using var penBorder = new Pen(palette.Border, 1f);
            Rectangle r = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            g.DrawRectangle(penBorder, r);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var palette = ZeroTheme.Colors;
            var item = e.Item;
            var zeroItem = item as MenuItemControl;

            if (item.Selected && item.Enabled)
            {
                Rectangle r = new Rectangle(4, 2, item.Width - 8, item.Height - 4);
                using var path = CreateRoundedRect(r, 5);

                Color hoverBg = (zeroItem != null && zeroItem.IsDanger)
                    ? Color.FromArgb(40, palette.Danger)
                    : Color.FromArgb(25, palette.Primary);

                using var brushHov = new SolidBrush(hoverBg);
                g.FillPath(brushHov, path);

                // Subtle left pill accent on hover
                Color accentBarColor = (zeroItem != null && zeroItem.IsDanger) ? palette.Danger : palette.Primary;
                using var brushBar = new SolidBrush(accentBarColor);
                g.FillRectangle(brushBar, new Rectangle(r.X, r.Y + 3, 3, r.Height - 6));
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = ZeroTheme.Colors;
            var item = e.Item;
            var zeroItem = item as MenuItemControl;

            // Determine text color
            Color textColor;
            if (!item.Enabled)
            {
                textColor = Color.FromArgb(100, palette.TextSecondary);
            }
            else if (zeroItem != null && zeroItem.IsDanger)
            {
                textColor = item.Selected ? palette.Danger : Color.FromArgb(248, 113, 113); // Soft red
            }
            else if (item.Selected)
            {
                textColor = palette.Primary;
            }
            else
            {
                textColor = palette.TextPrimary;
            }

            int curX = 12;

            // 1. Draw Checkmark (if checked)
            if (item is ToolStripMenuItem tsmi && tsmi.Checked)
            {
                var fontCheck = ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Bold);
                using var brushCheck = new SolidBrush(palette.Primary);
                g.DrawString("✔", fontCheck, brushCheck, curX, (item.Height - 16) / 2);
                curX += 18;
            }

            // 2. Draw Vector Icon or Glyph (if specified)
            if (zeroItem != null && zeroItem.Icon.HasValue)
            {
                var iconRect = new Rectangle(curX, (item.Height - 16) / 2, 16, 16);
                ZeroIcon.Draw(g, zeroItem.Icon.Value, iconRect, textColor);
                curX += 22;
            }
            else if (zeroItem != null && !string.IsNullOrEmpty(zeroItem.Glyph))
            {
                var fontGlyph = ZeroFontCache.Get("Segoe UI Emoji", 9.5f, FontStyle.Regular);
                using var brushGlyph = new SolidBrush(textColor);
                g.DrawString(zeroItem.Glyph, fontGlyph, brushGlyph, curX, (item.Height - 18) / 2);
                curX += 22;
            }

            // 3. Draw Item Text
            var fontText = ZeroFontCache.Get(item.Font.FontFamily.Name, 9f, FontStyle.Regular);
            using (var brushText = new SolidBrush(textColor))
            {
                g.DrawString(item.Text, fontText, brushText, curX, (item.Height - 16) / 2);
            }

            // 4. Draw Badge Tag (if any)
            if (zeroItem != null && !string.IsNullOrEmpty(zeroItem.BadgeText))
            {
                int textW = (int)g.MeasureString(item.Text, fontText).Width;
                int badgeX = curX + textW + 8;
                int badgeW = 28;
                var badgeRect = new Rectangle(badgeX, (item.Height - 16) / 2, badgeW, 16);

                Color bColor = zeroItem.BadgeColor ?? palette.Primary;
                using var brushBadgeBg = new SolidBrush(Color.FromArgb(30, bColor));
                using var pathBadge = CreateRoundedRect(badgeRect, 3);
                g.FillPath(brushBadgeBg, pathBadge);

                var fontBadge = ZeroFontCache.Get(item.Font.FontFamily.Name, 7.5f, FontStyle.Bold);
                using var brushBadgeText = new SolidBrush(bColor);
                var sfB = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(zeroItem.BadgeText, fontBadge, brushBadgeText, badgeRect, sfB);
            }

            // 5. Draw Right-aligned Shortcut Hint
            string? shortcut = zeroItem?.ShortcutHint ?? (item as ToolStripMenuItem)?.ShortcutKeyDisplayString;
            if (!string.IsNullOrEmpty(shortcut))
            {
                var fontShort = ZeroFontCache.Get(item.Font.FontFamily.Name, 8f, FontStyle.Regular);
                using var brushShort = new SolidBrush(palette.TextSecondary);
                var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                Rectangle shortRect = new Rectangle(item.Width - 100, 0, 88, item.Height);
                g.DrawString(shortcut, fontShort, brushShort, shortRect, sf);
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var g = e.Graphics;
            var palette = ZeroTheme.Colors;

            int y = e.Item.Height / 2;
            using var penDiv = new Pen(Color.FromArgb(40, palette.Border), 1f);
            g.DrawLine(penDiv, 12, y, e.Item.Width - 12, y);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var palette = ZeroTheme.Colors;
            Color arrowColor = (e.Item != null && e.Item.Selected) ? palette.Primary : palette.TextSecondary;

            var fontArrow = ZeroFontCache.Get("Segoe UI", 9f, FontStyle.Bold);
            using var brushArrow = new SolidBrush(arrowColor);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("›", fontArrow, brushArrow, e.ArrowRectangle, sf);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            // Do not render legacy XP image stripe
        }

        private static GraphicsPath CreateRoundedRect(Rectangle r, int radius) =>
            ZeroUIConfig.CreateRoundedRectangle(r, radius);
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZContextMenu"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ContextMenuControl is deprecated and will be removed in 5 release cycles. Please migrate to ZContextMenu instead.")]
    [ToolboxItem(false)]
    public class ContextMenuControl : ZContextMenu
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZContextMenu"/>.
    /// </summary>
    [Obsolete("ZeroContextMenu is deprecated. Please use ZContextMenu instead.")]
    [ToolboxItem(false)]
    public class ZeroContextMenu : ZContextMenu
    {
    }

    [Obsolete("Use MenuItemControl instead.")]
    public class ZeroMenuItem : MenuItemControl
    {
        public ZeroMenuItem() : base() { }
        public ZeroMenuItem(string text, Action? onClick) : base(text, onClick) { }
    }

    [Obsolete("Use ContextMenuRenderer instead.")]
    public class ZeroContextMenuRenderer : ContextMenuRenderer
    {
    }

    #endregion
}
