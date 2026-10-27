using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Editors
{
    /// <summary>
    /// High-performance Single-HWND SplitButton control separating default action from contextual dropdown menu.
    /// Supports asymmetric sub-hover tracking, seamless GDI+ vector splitting, and theme synchronization.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "SplitButton.bmp")]
    [Category("ZeroUI - Editors")]
    [DefaultEvent("ActionClick")]
    [DefaultProperty("Text")]
    [Description("Modern split button with independent action and dropdown zones in a single HWND")]
    public class ZSplitButton : ControlBase
    {
        private string? _iconGlyph;
        private int _splitWidth = 28;
        private int _borderRadius = 6;
        private ZeroButtonStyle _buttonStyle = ZeroButtonStyle.Primary;
        private ContextMenuStrip? _dropDownMenu;

        private bool _isActionHovered = false;
        private bool _isDropDownHovered = false;
        private bool _isActionPressed = false;
        private bool _isDropDownPressed = false;

        public event EventHandler? ActionClick;
        public event EventHandler? DropDownClick;

        public ZSplitButton()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(140, 36);
            Font = new Font("Segoe UI", 9.25f, FontStyle.Regular);
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;

            ZeroUIConfig.CornerStyleChanged += (s, e) =>
            {
                UpdateRegion();
                Invalidate();
            };
            ZeroUIConfig.FontChanged += (s, e) =>
            {
                Font = new Font(ZeroUIConfig.DefaultFont.FontFamily, 9.25f, FontStyle.Regular);
                UpdateRegion();
                Invalidate();
            };
        }

        #region Properties

        [Category("Appearance")]
        [DefaultValue(ZeroButtonStyle.Primary)]
        public ZeroButtonStyle ButtonStyle
        {
            get => _buttonStyle;
            set
            {
                if (_buttonStyle != value)
                {
                    _buttonStyle = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        public string? IconGlyph
        {
            get => _iconGlyph;
            set
            {
                if (_iconGlyph != value)
                {
                    _iconGlyph = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(28)]
        public int SplitWidth
        {
            get => _splitWidth;
            set
            {
                _splitWidth = Math.Max(20, value);
                Invalidate();
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

        [Category("Behavior")]
        [DefaultValue(null)]
        public ContextMenuStrip? DropDownMenu
        {
            get => _dropDownMenu;
            set => _dropDownMenu = value;
        }

        #endregion

        #region Layout & Hit Testing

        private Rectangle ActionBounds => new Rectangle(0, 0, Math.Max(1, Width - _splitWidth), Height);
        private Rectangle DropDownBounds => new Rectangle(Math.Max(0, Width - _splitWidth), 0, _splitWidth, Height);

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool inAction = ActionBounds.Contains(e.Location);
            bool inDrop = DropDownBounds.Contains(e.Location);

            if (_isActionHovered != inAction || _isDropDownHovered != inDrop)
            {
                _isActionHovered = inAction;
                _isDropDownHovered = inDrop;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isActionHovered = false;
            _isDropDownHovered = false;
            _isActionPressed = false;
            _isDropDownPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (ActionBounds.Contains(e.Location))
                {
                    _isActionPressed = true;
                    Invalidate();
                }
                else if (DropDownBounds.Contains(e.Location))
                {
                    _isDropDownPressed = true;
                    Invalidate();
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_isActionPressed && ActionBounds.Contains(e.Location))
                {
                    _isActionPressed = false;
                    Invalidate();
                    ActionClick?.Invoke(this, EventArgs.Empty);
                    OnClick(EventArgs.Empty);
                }
                else if (_isDropDownPressed && DropDownBounds.Contains(e.Location))
                {
                    _isDropDownPressed = false;
                    Invalidate();
                    DropDownClick?.Invoke(this, EventArgs.Empty);
                    ShowDropDown();
                }
                else
                {
                    _isActionPressed = false;
                    _isDropDownPressed = false;
                    Invalidate();
                }
            }
        }

        public void ShowDropDown()
        {
            if (_dropDownMenu != null && !DesignMode)
            {
                _dropDownMenu.Show(this, new Point(0, Height));
            }
        }

        #endregion

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRegion();
            Invalidate();
        }

        private void UpdateRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            int effRadius = ZeroUIConfig.GetEffectiveRadius(_borderRadius);
            if (effRadius > 0)
            {
                using var path = CreateRoundedRectangle(new Rectangle(0, 0, Width, Height), effRadius);
                Region = new Region(path);
            }
            else
            {
                Region = null;
            }
        }

        #region Paint Pipeline

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            BackColor = Color.Transparent;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            var palette = CurrentPalette;
            int effRadius = ZeroUIConfig.GetEffectiveRadius(_borderRadius);
            var (baseBg, fg, border) = GetBaseColors(palette);

            // 1. Draw Outer Button Container
            if (effRadius > 0)
            {
                using var path = CreateRoundedRectangle(new Rectangle(0, 0, Width, Height), effRadius);
                using (var brush = new SolidBrush(baseBg))
                {
                    g.FillPath(brush, path);
                }

                // 2. Highlight Sub-zones if hovered or pressed (clipped to path to prevent corner bleed)
                if (Enabled)
                {
                    int splitX = Width - _splitWidth;
                    var clipState = g.Save();
                    g.SetClip(path);

                    if (_isActionHovered || _isActionPressed)
                    {
                        Color actionHighlight = _isActionPressed ? Color.FromArgb(40, 0, 0, 0) : Color.FromArgb(20, 255, 255, 255);
                        using var hBrush = new SolidBrush(actionHighlight);
                        g.FillRectangle(hBrush, new RectangleF(0, 0, splitX, Height));
                    }

                    if (_isDropDownHovered || _isDropDownPressed)
                    {
                        Color dropHighlight = _isDropDownPressed ? Color.FromArgb(40, 0, 0, 0) : Color.FromArgb(20, 255, 255, 255);
                        using var hBrush = new SolidBrush(dropHighlight);
                        g.FillRectangle(hBrush, new RectangleF(splitX, 0, _splitWidth, Height));
                    }

                    // 3. Draw 1px Divider
                    Color divColor = _buttonStyle == ZeroButtonStyle.Secondary
                        ? palette.Border
                        : Color.FromArgb(80, 255, 255, 255);

                    using var divPen = new Pen(divColor, 1f);
                    g.DrawLine(divPen, splitX, 4, splitX, Height - 5);

                    g.Restore(clipState);
                }

                // 4. Draw Outer Border
                if (border != Color.Transparent)
                {
                    using var pen = new Pen(border, 1f) { Alignment = PenAlignment.Inset };
                    g.DrawPath(pen, path);
                }
            }
            else
            {
                using (var brush = new SolidBrush(baseBg))
                {
                    g.FillRectangle(brush, 0, 0, Width, Height);
                }

                if (Enabled)
                {
                    int splitX = Width - _splitWidth;

                    if (_isActionHovered || _isActionPressed)
                    {
                        Color actionHighlight = _isActionPressed ? Color.FromArgb(40, 0, 0, 0) : Color.FromArgb(20, 255, 255, 255);
                        using var hBrush = new SolidBrush(actionHighlight);
                        g.FillRectangle(hBrush, new Rectangle(0, 0, splitX, Height));
                    }

                    if (_isDropDownHovered || _isDropDownPressed)
                    {
                        Color dropHighlight = _isDropDownPressed ? Color.FromArgb(40, 0, 0, 0) : Color.FromArgb(20, 255, 255, 255);
                        using var hBrush = new SolidBrush(dropHighlight);
                        g.FillRectangle(hBrush, new Rectangle(splitX, 0, _splitWidth, Height));
                    }

                    Color divColor = _buttonStyle == ZeroButtonStyle.Secondary
                        ? palette.Border
                        : Color.FromArgb(80, 255, 255, 255);

                    using var divPen = new Pen(divColor, 1f);
                    g.DrawLine(divPen, splitX, 4, splitX, Height - 5);
                }

                if (border != Color.Transparent)
                {
                    using var pen = new Pen(border, 1f) { Alignment = PenAlignment.Inset };
                    g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                }
            }

            // 5. Draw Action Zone Content (Icon + Text)
            string actionText = string.IsNullOrEmpty(_iconGlyph)
                ? Text
                : $"{_iconGlyph}  {Text}".Trim();

            Rectangle actionTextRect = new Rectangle(4, 0, Width - _splitWidth - 8, Height);
            TextRenderer.DrawText(
                g,
                actionText,
                Font,
                actionTextRect,
                fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            // 6. Draw DropDown Chevron ▾
            Rectangle dropTextRect = new Rectangle(Width - _splitWidth, 0, _splitWidth, Height);
            TextRenderer.DrawText(
                g,
                "▾",
                ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Bold),
                dropTextRect,
                fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        private (Color bg, Color fg, Color border) GetBaseColors(ZeroThemePalette palette)
        {
            if (!Enabled)
            {
                Color disabledBg = EffectiveSkin.IsDark ? Color.FromArgb(40, 44, 60) : Color.FromArgb(229, 231, 235);
                return (disabledBg, palette.TextSecondary, Color.Transparent);
            }

            return _buttonStyle switch
            {
                ZeroButtonStyle.Primary => (palette.Primary, Color.White, Color.Transparent),
                ZeroButtonStyle.Secondary => (palette.Surface, palette.TextPrimary, palette.Border),
                ZeroButtonStyle.Success => (palette.Success, Color.White, Color.Transparent),
                ZeroButtonStyle.Danger => (palette.Danger, Color.White, Color.Transparent),
                ZeroButtonStyle.Ghost => (Color.Transparent, palette.TextPrimary, Color.Transparent),
                _ => (palette.Primary, Color.White, Color.Transparent)
            };
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius) =>
            ZeroUIConfig.CreateRoundedRectangle(rect, radius);

        #endregion
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSplitButton"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SplitButton is deprecated and will be removed in 5 release cycles. Please migrate to ZSplitButton instead.")]
    [ToolboxItem(false)]
    public class SplitButton : ZSplitButton
    {
    }

    #endregion
}
