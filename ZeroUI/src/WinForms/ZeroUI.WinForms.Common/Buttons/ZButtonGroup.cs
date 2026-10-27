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
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// Modern connected button group (Action Cluster / Action Strip) with single-HWND rendering,
    /// seamless border geometry, toggle grouping, and theme reactivity.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ButtonGroup.bmp")]
    [Category("ZeroUI - Editors")]
    [DefaultEvent("ItemClick")]
    [DefaultProperty("Items")]
    [Description("Seamless connected button cluster with single-HWND rendering, toggle grouping, and theme reactivity")]
    public class ZButtonGroup : ControlBase
    {
        private readonly ButtonGroupModel _model = new ButtonGroupModel();
        private readonly List<Rectangle> _itemBounds = new List<Rectangle>();
        private readonly ToolTip _toolTip = new ToolTip();
        private int _hoveredIndex = -1;
        private int _pressedIndex = -1;
        private int _borderRadius = 6;
        private ButtonGroupSizeMode _sizeMode = ButtonGroupSizeMode.AutoFit;
        private int _itemPaddingHorizontal = 16;
        private string? _currentToolTipText;

        public event EventHandler<ButtonGroupItemEventArgs>? ItemClick;
        public event EventHandler<ButtonGroupItem>? SelectionChanged;

        public ZButtonGroup()
        {
            Size = new Size(280, 36);
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            Cursor = Cursors.Hand;

            _model.ItemClicked += (s, e) => ItemClick?.Invoke(this, e);
            _model.SelectionChanged += (s, item) => SelectionChanged?.Invoke(this, item);
            _model.ItemsChanged += (s, e) =>
            {
                if (AutoSize)
                {
                    Size = GetPreferredSize(Size.Empty);
                }
                RecalculateLayout();
                Invalidate();
            };

            ZeroUIConfig.CornerStyleChanged += (s, e) =>
            {
                UpdateRegion();
                Invalidate();
            };
            ZeroUIConfig.FontChanged += (s, e) =>
            {
                Font = new Font(ZeroUIConfig.DefaultFont.FontFamily, 9f, FontStyle.Regular);
                if (AutoSize)
                {
                    Size = GetPreferredSize(Size.Empty);
                }
                RecalculateLayout();
                UpdateRegion();
                Invalidate();
            };
        }

        #region Properties

        [Browsable(true)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        [DefaultValue(false)]
        public override bool AutoSize
        {
            get => base.AutoSize;
            set
            {
                base.AutoSize = value;
                if (value)
                {
                    Size = GetPreferredSize(Size.Empty);
                }
            }
        }

        [Browsable(false)]
        public ButtonGroupModel Model => _model;

        [Category("Behavior")]
        [DefaultValue(ButtonGroupSelectionMode.None)]
        public ButtonGroupSelectionMode SelectionMode
        {
            get => _model.SelectionMode;
            set
            {
                if (_model.SelectionMode != value)
                {
                    _model.SelectionMode = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(ButtonGroupSizeMode.AutoFit)]
        public ButtonGroupSizeMode SizeMode
        {
            get => _sizeMode;
            set
            {
                if (_sizeMode != value)
                {
                    _sizeMode = value;
                    RecalculateLayout();
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(6)]
        public int BorderRadius
        {
            get => _borderRadius;
            set
            {
                _borderRadius = Math.Max(0, value);
                UpdateRegion();
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(16)]
        public int ItemPaddingHorizontal
        {
            get => _itemPaddingHorizontal;
            set
            {
                _itemPaddingHorizontal = Math.Max(4, value);
                if (AutoSize)
                {
                    Size = GetPreferredSize(Size.Empty);
                }
                RecalculateLayout();
                Invalidate();
            }
        }

        [Browsable(false)]
        public IReadOnlyList<ButtonGroupItem> Items => _model.Items;

        [Browsable(false)]
        public ButtonGroupItem? SelectedItem => _model.SelectedItem;

        #endregion

        #region Sizing

        public override Size GetPreferredSize(Size proposedSize)
        {
            if (_model.Count == 0)
            {
                return new Size(64, Height > 0 ? Height : 36);
            }

            using var bmp = new Bitmap(1, 1);
            using var g = Graphics.FromImage(bmp);

            int totalW = 0;
            for (int i = 0; i < _model.Count; i++)
            {
                var item = _model[i];
                if (!item.IsVisible) continue;
                totalW += MeasureItemWidth(g, item);
            }

            int h = Height > 0 ? Height : 36;
            return new Size(Math.Max(32, totalW + 2), h);
        }

        private int MeasureItemWidth(Graphics g, ButtonGroupItem item)
        {
            if (item.Type == ButtonGroupItemType.Separator)
            {
                return 6;
            }

            string displayText = string.IsNullOrEmpty(item.IconGlyph)
                ? item.Text
                : $"{item.IconGlyph} {item.Text}".Trim();

            int pad = item.CustomPaddingHorizontal ?? _itemPaddingHorizontal;
            int textW = 0;
            if (!string.IsNullOrEmpty(displayText))
            {
                Size size = TextRenderer.MeasureText(g, displayText, Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoClipping);
                textW = size.Width + 4; // ClearType grid safety padding
            }

            int w = textW + (pad * 2);

            if (item.IconImage != null)
            {
                w += 20; // 16px icon + 4px spacing
            }

            if (!string.IsNullOrEmpty(item.BadgeText))
            {
                Size badgeSize = TextRenderer.MeasureText(g, item.BadgeText, ZeroFontCache.Get("Segoe UI", 7.5f, FontStyle.Bold));
                w += Math.Max(18, badgeSize.Width + 8) + 6;
            }
            else if (item.ShowBadgeDot)
            {
                w += 10;
            }

            if (item.Type == ButtonGroupItemType.DropDown)
            {
                w += 14;
            }

            return Math.Max(item.MinWidth > 0 ? item.MinWidth : 32, w);
        }

        #endregion

        #region Builder API

        public ButtonGroupItem AddButton(string id, string text, string? iconGlyph = null, ButtonGroupItemStyle style = ButtonGroupItemStyle.Secondary, Action<ButtonGroupItem>? action = null)
        {
            var item = new ButtonGroupItem(id, text, iconGlyph, ButtonGroupItemType.Push, action)
            {
                Style = style
            };
            _model.Add(item);
            return item;
        }

        public ButtonGroupItem AddToggle(string id, string text, string? iconGlyph = null, bool isChecked = false, Action<ButtonGroupItem>? action = null)
        {
            var item = new ButtonGroupItem(id, text, iconGlyph, ButtonGroupItemType.Toggle, action)
            {
                IsChecked = isChecked,
                Style = ButtonGroupItemStyle.Secondary
            };
            _model.Add(item);
            return item;
        }

        public ButtonGroupItem AddDropDown(string id, string text, string? iconGlyph = null, Action<ButtonGroupItem>? action = null)
        {
            var item = new ButtonGroupItem(id, text, iconGlyph, ButtonGroupItemType.DropDown, action);
            _model.Add(item);
            return item;
        }

        public ButtonGroupItem AddDropDown(string id, string text, string? iconGlyph, object? dropDownMenu, Action<ButtonGroupItem>? action = null)
        {
            var item = new ButtonGroupItem(id, text, iconGlyph, ButtonGroupItemType.DropDown, action, dropDownMenu);
            _model.Add(item);
            return item;
        }

        public ButtonGroupItem AddSeparator()
        {
            var item = new ButtonGroupItem(Guid.NewGuid().ToString("N"), string.Empty, null, ButtonGroupItemType.Separator);
            _model.Add(item);
            return item;
        }

        public void Clear()
        {
            _model.Clear();
        }

        #endregion

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalculateLayout();
            UpdateRegion();
            Invalidate();
        }

        private void UpdateRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            int effRadius = ZeroUIConfig.GetEffectiveRadius(_borderRadius);
            if (effRadius > 0)
            {
                using var path = CreateRoundedRectangle(new Rectangle(0, 0, Width, Height), effRadius, effRadius, effRadius, effRadius);
                Region = new Region(path);
            }
            else
            {
                Region = null;
            }
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }

        private void RecalculateLayout()
        {
            _itemBounds.Clear();
            if (_model.Count == 0 || Width <= 0 || Height <= 0) return;

            int visibleCount = 0;
            for (int i = 0; i < _model.Count; i++)
            {
                if (_model[i].IsVisible) visibleCount++;
            }
            if (visibleCount == 0) return;

            int currentX = 0;
            int totalAvailableWidth = Width - 1;

            if (_sizeMode == ButtonGroupSizeMode.EqualWidth)
            {
                int itemW = Math.Max(1, totalAvailableWidth / visibleCount);
                for (int i = 0; i < _model.Count; i++)
                {
                    var item = _model[i];
                    if (!item.IsVisible)
                    {
                        _itemBounds.Add(Rectangle.Empty);
                        continue;
                    }

                    int w = (i == _model.Count - 1) ? Math.Max(1, totalAvailableWidth - currentX) : itemW;
                    _itemBounds.Add(new Rectangle(currentX, 0, Math.Max(1, w), Height - 1));
                    currentX += w;
                }
            }
            else
            {
                // AutoFit or Fill
                using var bmp = new Bitmap(1, 1);
                using var g = Graphics.FromImage(bmp);

                var measuredWidths = new int[_model.Count];
                int totalMeasuredWidth = 0;

                for (int i = 0; i < _model.Count; i++)
                {
                    var item = _model[i];
                    if (!item.IsVisible)
                    {
                        measuredWidths[i] = 0;
                        continue;
                    }

                    measuredWidths[i] = MeasureItemWidth(g, item);
                    totalMeasuredWidth += measuredWidths[i];
                }

                if (_sizeMode == ButtonGroupSizeMode.Fill && totalMeasuredWidth > 0 && totalMeasuredWidth < totalAvailableWidth)
                {
                    // Stretch proportionally to fill available width
                    float scaleFactor = (float)totalAvailableWidth / totalMeasuredWidth;
                    for (int i = 0; i < _model.Count; i++)
                    {
                        var item = _model[i];
                        if (!item.IsVisible)
                        {
                            _itemBounds.Add(Rectangle.Empty);
                            continue;
                        }

                        int w = (int)(measuredWidths[i] * scaleFactor);
                        if (i == _model.Count - 1)
                        {
                            w = Math.Max(1, totalAvailableWidth - currentX);
                        }

                        _itemBounds.Add(new Rectangle(currentX, 0, Math.Max(1, w), Height - 1));
                        currentX += w;
                    }
                }
                else
                {
                    // AutoFit: keep exact natural content width without artificial inflation
                    for (int i = 0; i < _model.Count; i++)
                    {
                        var item = _model[i];
                        if (!item.IsVisible)
                        {
                            _itemBounds.Add(Rectangle.Empty);
                            continue;
                        }

                        int w = measuredWidths[i];
                        _itemBounds.Add(new Rectangle(currentX, 0, Math.Max(1, w), Height - 1));
                        currentX += w;
                    }
                }
            }
        }

        #region Mouse Interaction & Hit-Testing

        private int HitTest(int x, int y)
        {
            for (int i = 0; i < _itemBounds.Count; i++)
            {
                if (_itemBounds[i].Contains(x, y))
                {
                    var item = _model[i];
                    if (item.IsVisible && item.IsEnabled && item.Type != ButtonGroupItemType.Separator)
                    {
                        return i;
                    }
                    return -1;
                }
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = HitTest(e.X, e.Y);
            if (_hoveredIndex != idx)
            {
                _hoveredIndex = idx;
                Invalidate();

                // Handle tooltip update
                if (_hoveredIndex >= 0 && _hoveredIndex < _model.Count)
                {
                    string? tip = _model[_hoveredIndex].ToolTip;
                    if (!string.IsNullOrEmpty(tip) && tip != _currentToolTipText)
                    {
                        _currentToolTipText = tip;
                        _toolTip.SetToolTip(this, tip);
                    }
                }
                else
                {
                    _currentToolTipText = null;
                    _toolTip.SetToolTip(this, null);
                }
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredIndex = -1;
            _pressedIndex = -1;
            _currentToolTipText = null;
            _toolTip.SetToolTip(this, null);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _pressedIndex = HitTest(e.X, e.Y);
                if (_pressedIndex >= 0)
                {
                    Invalidate();
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && _pressedIndex >= 0)
            {
                int releasedIndex = HitTest(e.X, e.Y);
                if (releasedIndex == _pressedIndex)
                {
                    var item = _model[releasedIndex];
                    if (item.Type == ButtonGroupItemType.DropDown && item.DropDownMenu is ContextMenuStrip cms)
                    {
                        var bounds = _itemBounds[releasedIndex];
                        cms.Show(this, new Point(bounds.Left, bounds.Bottom));
                    }

                    _model.TriggerClick(releasedIndex);
                }
                _pressedIndex = -1;
                Invalidate();
            }
        }

        #endregion

        #region Paint Pipeline

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = CurrentPalette;

            if (_model.Count == 0 || _itemBounds.Count != _model.Count) return;

            Rectangle groupRect = new Rectangle(0, 0, Width, Height);
            int effRadius = ZeroUIConfig.GetEffectiveRadius(_borderRadius);

            // 1. Draw Group Outer Background & Clip Path
            using (var groupPath = CreateRoundedRectangle(groupRect, effRadius, effRadius, effRadius, effRadius))
            {
                using var bgBrush = new SolidBrush(palette.Surface);
                g.FillPath(bgBrush, groupPath);

                var oldClip = g.Clip;
                g.SetClip(groupPath, CombineMode.Intersect);

                // 3. Render Each Item
                int firstVisible = -1, lastVisible = -1;
                for (int i = 0; i < _model.Count; i++)
                {
                    if (_model[i].IsVisible)
                    {
                        if (firstVisible < 0) firstVisible = i;
                        lastVisible = i;
                    }
                }

                for (int i = 0; i < _model.Count; i++)
                {
                    var item = _model[i];
                    if (!item.IsVisible) continue;
                    var bounds = _itemBounds[i];
                    if (bounds.IsEmpty) continue;

                    bool isFirst = (i == firstVisible);
                    bool isLast = (i == lastVisible);
                    bool isHovered = (_hoveredIndex == i);
                    bool isPressed = (_pressedIndex == i);

                    if (item.Type == ButtonGroupItemType.Separator)
                    {
                        // Draw vertical separator
                        int lineX = bounds.X + bounds.Width / 2;
                        using var sepPen = new Pen(palette.Border, 1f);
                        g.DrawLine(sepPen, lineX, bounds.Y + 6, lineX, bounds.Bottom - 6);
                        continue;
                    }

                    // Compute background color
                    var (itemBg, itemFg) = GetItemColors(item, isHovered, isPressed, palette);

                    if (itemBg != Color.Transparent)
                    {
                        using var itemBrush = new SolidBrush(itemBg);
                        g.FillRectangle(itemBrush, bounds);
                    }

                    // Draw Content: IconImage + Glyph + Text
                    int contentLeft = bounds.X + 4;
                    int contentWidth = bounds.Width - 8;

                    if (!string.IsNullOrEmpty(item.BadgeText))
                    {
                        contentWidth -= 24;
                    }

                    if (item.IconImage is Image img)
                    {
                        int imgY = bounds.Y + (bounds.Height - 16) / 2;
                        g.DrawImage(img, new Rectangle(contentLeft, imgY, 16, 16));
                        contentLeft += 20;
                        contentWidth = Math.Max(0, contentWidth - 20);
                    }

                    string textToDraw = string.IsNullOrEmpty(item.IconGlyph)
                        ? item.Text
                        : $"{item.IconGlyph}  {item.Text}".Trim();

                    if (item.Type == ButtonGroupItemType.DropDown)
                    {
                        textToDraw += " ▾";
                    }

                    Rectangle textRect = new Rectangle(contentLeft, bounds.Y, contentWidth, bounds.Height);

                    TextRenderer.DrawText(
                        g,
                        textToDraw,
                        item.IsChecked ? ZeroFontCache.Get(Font.FontFamily.Name, Font.Size, FontStyle.Bold) : Font,
                        textRect,
                        itemFg,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

                    // Draw Optional Badge Dot (small notification/dirty indicator)
                    if (item.ShowBadgeDot)
                    {
                        Color dotColor = ParseHexColor(item.BadgeColorHex, Color.FromArgb(239, 68, 68));
                        int dotSize = 7;
                        int dotX = bounds.Right - 10;
                        int dotY = bounds.Y + 5;

                        // Subtle outer ring to separate from background
                        using var ringPen = new Pen(palette.Surface, 1.2f);
                        g.DrawEllipse(ringPen, dotX - 0.5f, dotY - 0.5f, dotSize + 1, dotSize + 1);

                        using var dotBrush = new SolidBrush(dotColor);
                        g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
                    }

                    // Draw Optional Badge Pill with Text
                    if (!string.IsNullOrEmpty(item.BadgeText))
                    {
                        Color badgeBgColor = ParseHexColor(item.BadgeColorHex, Color.FromArgb(220, 38, 38));
                        Size badgeSize = TextRenderer.MeasureText(item.BadgeText, ZeroFontCache.Get("Segoe UI", 7.5f, FontStyle.Bold));
                        int badgeW = Math.Max(18, badgeSize.Width + 8);
                        int badgeH = 16;
                        Rectangle badgeRect = new Rectangle(bounds.Right - badgeW - 6, (bounds.Height - badgeH) / 2, badgeW, badgeH);

                        using var badgePath = CreateRoundedRectangle(badgeRect, 8, 8, 8, 8);
                        using var badgeBg = new SolidBrush(badgeBgColor);
                        g.FillPath(badgeBg, badgePath);

                        TextRenderer.DrawText(
                            g,
                            item.BadgeText,
                            ZeroFontCache.Get("Segoe UI", 7.5f, FontStyle.Bold),
                            badgeRect,
                            Color.White,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                    }

                    // Draw 1px divider between items (if not last item and next item is not a separator)
                    if (!isLast && item.Type != ButtonGroupItemType.Separator && (i + 1 < _model.Count && _model[i + 1].Type != ButtonGroupItemType.Separator))
                    {
                        using var divPen = new Pen(palette.Border, 1f);
                        g.DrawLine(divPen, bounds.Right, bounds.Y + 4, bounds.Right, bounds.Bottom - 4);
                    }
                }

                g.Clip = oldClip;

                // 4. Draw Outer Group Border cleanly over clipped items
                using var borderPen = new Pen(palette.Border, 1f) { Alignment = PenAlignment.Inset };
                g.DrawPath(borderPen, groupPath);
            }
        }

        private static Color ParseHexColor(string? hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            try
            {
                return ColorTranslator.FromHtml(hex);
            }
            catch
            {
                return fallback;
            }
        }

        private (Color bg, Color fg) GetItemColors(ButtonGroupItem item, bool isHovered, bool isPressed, ZeroThemePalette palette)
        {
            if (!item.IsEnabled || !Enabled)
            {
                return (Color.Transparent, palette.TextSecondary);
            }

            if (item.IsChecked)
            {
                // Active / Checked Toggle
                return (palette.Primary, Color.White);
            }

            if (isPressed)
            {
                return (palette.Hover, palette.Primary);
            }

            if (isHovered)
            {
                return (palette.Hover, palette.TextPrimary);
            }

            // Check custom colors if set
            Color? customBg = !string.IsNullOrEmpty(item.CustomBackColorHex) ? ParseHexColor(item.CustomBackColorHex, Color.Empty) : null;
            Color? customFg = !string.IsNullOrEmpty(item.CustomForeColorHex) ? ParseHexColor(item.CustomForeColorHex, Color.Empty) : null;

            if (customBg.HasValue && customBg.Value != Color.Empty)
            {
                return (customBg.Value, customFg ?? palette.TextPrimary);
            }

            // Normal idle state based on item.Style
            var (baseBg, baseFg) = item.Style switch
            {
                ButtonGroupItemStyle.Primary => (palette.Primary, Color.White),
                ButtonGroupItemStyle.Success => (palette.Success, Color.White),
                ButtonGroupItemStyle.Danger => (palette.Danger, Color.White),
                ButtonGroupItemStyle.Ghost => (Color.Transparent, palette.TextPrimary),
                _ => (Color.Transparent, palette.TextPrimary)
            };

            if (customFg.HasValue && customFg.Value != Color.Empty)
            {
                baseFg = customFg.Value;
            }

            return (baseBg, baseFg);
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int topLeft, int topRight, int bottomRight, int bottomLeft)
        {
            var path = new GraphicsPath();
            int x = rect.X, y = rect.Y, w = rect.Width, h = rect.Height;

            if (w <= 0 || h <= 0) return path;

            // Top-left
            if (topLeft > 0)
                path.AddArc(x, y, topLeft * 2, topLeft * 2, 180, 90);
            else
                path.AddLine(x, y, x, y);

            // Top-right
            if (topRight > 0)
                path.AddArc(x + w - topRight * 2, y, topRight * 2, topRight * 2, 270, 90);
            else
                path.AddLine(x + w, y, x + w, y);

            // Bottom-right
            if (bottomRight > 0)
                path.AddArc(x + w - bottomRight * 2, y + h - bottomRight * 2, bottomRight * 2, bottomRight * 2, 0, 90);
            else
                path.AddLine(x + w, y + h, x + w, y + h);

            // Bottom-left
            if (bottomLeft > 0)
                path.AddArc(x, y + h - bottomLeft * 2, bottomLeft * 2, bottomLeft * 2, 90, 90);
            else
                path.AddLine(x, y + h, x, y + h);

            path.CloseFigure();
            return path;
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZButtonGroup"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ButtonGroup is deprecated and will be removed in 5 release cycles. Please migrate to ZButtonGroup instead.")]
    [ToolboxItem(false)]
    public class ButtonGroup : ZButtonGroup
    {
    }

    #endregion
}
