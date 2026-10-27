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
    public enum ZeroButtonStyle
    {
        Primary,
        Secondary,
        Success,
        Danger,
        Ghost
    }

    /// <summary>
    /// Modern theme-aware flat button control for ZeroUI.
    /// Canonical drop-in replacement for standard <see cref="System.Windows.Forms.Button"/>.
    /// Supports stateful hover, click animations, rounded corners, and badge counters.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroButton.bmp")]
    [Category("ZeroUI - Editors")]
    [DefaultEvent("Click")]
    [DefaultProperty("Text")]
    [Description("Modern theme-aware flat button adhering to the canonical Z-prefix standard")]
    public class ZButton : ControlBase, IButtonControl
    {
        private ZeroButtonStyle _style = ZeroButtonStyle.Primary;
        private int _borderRadius = 6;
        private string? _badgeText;
        private bool _isHovered = false;
        private bool _isPressed = false;
        private DialogResult _dialogResult = DialogResult.None;
        private bool _isDefault = false;

        public ZButton()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(130, 36);
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;

            ZeroUIConfig.CornerStyleChanged += (s, e) =>
            {
                UpdateRegion();
                Invalidate();
            };
            ZeroUIConfig.FontChanged += (s, e) =>
            {
                Font = new Font(ZeroUIConfig.DefaultFont.FontFamily, 9.5f, FontStyle.Bold);
                UpdateRegion();
                Invalidate();
            };
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            BackColor = Color.Transparent;
            Invalidate();
        }

        [Category("Appearance")]
        [DefaultValue(ZeroButtonStyle.Primary)]
        public ZeroButtonStyle ButtonStyle
        {
            get => _style;
            set { _style = value; Invalidate(); }
        }

        private int _baseBorderRadius = 6;

        [Category("Appearance")]
        [DefaultValue(6)]
        public int BorderRadius
        {
            get => _borderRadius;
            set
            {
                _baseBorderRadius = (int)Math.Round(value / DpiScale);
                _borderRadius = Math.Max(0, value);
                UpdateRegion();
                Invalidate();
            }
        }

        protected override void OnApplyDpiScaling(float scaleFactor, float factorRatio)
        {
            base.OnApplyDpiScaling(scaleFactor, factorRatio);
            _borderRadius = Math.Max(0, (int)Math.Round(_baseBorderRadius * scaleFactor));
            UpdateRegion();
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        public string? BadgeText
        {
            get => _badgeText;
            set { _badgeText = value; Invalidate(); }
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
            _isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isPressed = false;
            Invalidate();
        }

        [Category("Behavior")]
        [DefaultValue(DialogResult.None)]
        public DialogResult DialogResult
        {
            get => _dialogResult;
            set => _dialogResult = value;
        }

        public void NotifyDefault(bool value)
        {
            if (_isDefault != value)
            {
                _isDefault = value;
                Invalidate();
            }
        }

        public void PerformClick()
        {
            if (CanSelect)
            {
                OnClick(EventArgs.Empty);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (_dialogResult != DialogResult.None && FindForm() is Form parentForm)
            {
                parentForm.DialogResult = _dialogResult;
            }
        }

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

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            var (bg, fg, border) = GetColors();
            int effRadius = ZeroUIConfig.GetEffectiveRadius(_borderRadius);

            // 1. Draw Button Body & Border
            if (effRadius > 0)
            {
                using var path = CreateRoundedRectangle(new Rectangle(0, 0, Width, Height), effRadius);
                using (var brush = new SolidBrush(bg))
                {
                    g.FillPath(brush, path);
                }

                if (border != Color.Transparent)
                {
                    using var pen = new Pen(border, 1f) { Alignment = PenAlignment.Inset };
                    g.DrawPath(pen, path);
                }
            }
            else
            {
                using (var brush = new SolidBrush(bg))
                {
                    g.FillRectangle(brush, 0, 0, Width, Height);
                }

                if (border != Color.Transparent)
                {
                    using var pen = new Pen(border, 1f) { Alignment = PenAlignment.Inset };
                    g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                }
            }

            // 2. Draw Optional Badge
            int badgeSpace = 0;
            if (!string.IsNullOrEmpty(_badgeText))
            {
                Size badgeSize = TextRenderer.MeasureText(_badgeText, Font);
                int badgeW = Math.Max(18, badgeSize.Width + 6);
                int badgeH = 18;
                badgeSpace = badgeW + 8;

                var badgeRect = new Rectangle(Width - badgeW - 8, (Height - badgeH) / 2, badgeW, badgeH);
                using var badgePath = CreateRoundedRectangle(badgeRect, 9);
                using var badgeBg = new SolidBrush(Color.FromArgb(220, 38, 38));
                g.FillPath(badgeBg, badgePath);

                TextRenderer.DrawText(
                    g,
                    _badgeText,
                    ZeroFontCache.Get("Segoe UI", 7.5f, FontStyle.Bold),
                    badgeRect,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }

            // 3. Draw Button Text (offset if badge exists so they never collide!)
            int hPad = Width <= 36 ? 2 : 6;
            var textRect = new Rectangle(hPad, 0, Math.Max(0, Width - (hPad * 2) - (badgeSpace > 0 ? badgeSpace - 4 : 0)), Height);
            TextRenderer.DrawText(
                g,
                Text,
                Font,
                textRect,
                fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        private (Color bg, Color fg, Color border) GetColors()
        {
            var palette = CurrentPalette;

            if (!Enabled)
            {
                Color disabledBg = EffectiveSkin.IsDark ? Color.FromArgb(40, 44, 60) : Color.FromArgb(229, 231, 235);
                return (disabledBg, palette.TextSecondary, Color.Transparent);
            }

            return _style switch
            {
                ZeroButtonStyle.Primary => _isPressed
                    ? (palette.PrimaryHover, Color.White, Color.Transparent)
                    : _isHovered
                        ? (palette.PrimaryHover, Color.White, Color.Transparent)
                        : (palette.Primary, Color.White, Color.Transparent),

                ZeroButtonStyle.Secondary => _isPressed
                    ? (palette.Hover, palette.TextPrimary, palette.Primary)
                    : _isHovered
                        ? (palette.Hover, palette.TextPrimary, palette.Border)
                        : (palette.Surface, palette.TextPrimary, palette.Border),

                ZeroButtonStyle.Success => _isPressed
                    ? (palette.Success, Color.White, Color.Transparent)
                    : _isHovered
                        ? (palette.Success, Color.White, Color.Transparent)
                        : (palette.Success, Color.White, Color.Transparent),

                ZeroButtonStyle.Danger => _isPressed
                    ? (palette.Danger, Color.White, Color.Transparent)
                    : _isHovered
                        ? (palette.Danger, Color.White, Color.Transparent)
                        : (palette.Danger, Color.White, Color.Transparent),

                ZeroButtonStyle.Ghost => _isPressed
                    ? (palette.Hover, palette.TextPrimary, Color.Transparent)
                    : _isHovered
                        ? (palette.Hover, palette.TextPrimary, Color.Transparent)
                        : (Color.Transparent, palette.TextPrimary, Color.Transparent),

                _ => (palette.Primary, Color.White, Color.Transparent)
            };
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius) =>
            ZeroUIConfig.CreateRoundedRectangle(rect, radius);
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZButton"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SimpleButton is deprecated and will be removed in 5 release cycles. Please migrate to ZButton instead.")]
    [ToolboxItem(false)]
    public class SimpleButton : ZButton
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZButton"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroButton is deprecated and will be removed in 5 release cycles. Please migrate to ZButton instead.")]
    [ToolboxItem(false)]
    public class ZeroButton : ZButton
    {
    }

    #endregion
}
