using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Buttons
{
    public enum BadgePosition
    {
        TopRight,
        TopLeft,
        BottomRight,
        BottomLeft
    }

    public enum BadgeIconType
    {
        None,
        Bell,
        Message,
        Task,
        Warning,
        Custom
    }

    /// <summary>
    /// Interactive action button equipped with an overlapping floating notification badge (Pill or Dot).
    /// Ideal for ERP application headers, notification hubs, task queues, and messaging centers.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Buttons")]
    [DefaultEvent("Click")]
    [Description("Action button with overlapping floating notification badge and vector icons")]
    public class ZBadgeButton : ControlBase
    {
        private int _badgeValue = 0;
        private string? _badgeText;
        private bool _isDot = false;
        private bool _hideWhenZero = true;
        private BadgePosition _badgePosition = BadgePosition.TopRight;
        private BadgeIconType _iconType = BadgeIconType.Bell;
        private Image? _customIcon;
        private Color? _customBadgeColor;
        private int _borderRadius = 8;

        private bool _isHovered;
        private bool _isPressed;

        public ZBadgeButton()
        {
            Size = new Size(40, 40);
            Font = new Font("Segoe UI", 9f);
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
        }

        #region Properties

        [Category("Badge")]
        [DefaultValue(0)]
        [Description("Numeric notification count. Displays '99+' if greater than 99.")]
        public int BadgeValue
        {
            get => _badgeValue;
            set
            {
                _badgeValue = value;
                Invalidate();
            }
        }

        [Category("Badge")]
        [DefaultValue(null)]
        [Description("Custom text string displayed inside the badge. Overrides BadgeValue when set.")]
        public string? BadgeText
        {
            get => _badgeText;
            set
            {
                _badgeText = value;
                Invalidate();
            }
        }

        [Category("Badge")]
        [DefaultValue(false)]
        [Description("When true, renders a small indicator dot without text/number.")]
        public bool IsDot
        {
            get => _isDot;
            set
            {
                _isDot = value;
                Invalidate();
            }
        }

        [Category("Badge")]
        [DefaultValue(true)]
        [Description("Automatically hides the badge when BadgeValue is 0 and BadgeText is empty.")]
        public bool HideWhenZero
        {
            get => _hideWhenZero;
            set
            {
                _hideWhenZero = value;
                Invalidate();
            }
        }

        [Category("Badge")]
        [DefaultValue(BadgePosition.TopRight)]
        public BadgePosition BadgePosition
        {
            get => _badgePosition;
            set
            {
                _badgePosition = value;
                Invalidate();
            }
        }

        [Category("Badge")]
        [DefaultValue(typeof(Color), "")]
        public Color CustomBadgeColor
        {
            get => _customBadgeColor ?? Color.Empty;
            set
            {
                _customBadgeColor = value.IsEmpty ? null : (Color?)value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(BadgeIconType.Bell)]
        public BadgeIconType IconType
        {
            get => _iconType;
            set
            {
                _iconType = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        public Image? CustomIcon
        {
            get => _customIcon;
            set
            {
                _customIcon = value;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(8)]
        public int BorderRadius
        {
            get => _borderRadius;
            set
            {
                _borderRadius = Math.Max(0, value);
                Invalidate();
            }
        }

        #endregion

        #region Mouse Interaction

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
            if (_isPressed)
            {
                _isPressed = false;
                Invalidate();
            }
        }

        #endregion

        #region Painting

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var pal = CurrentPalette;

            // 1. Button Background & Border
            Color btnBg = _isPressed ? pal.Hover
                : _isHovered ? pal.Hover
                : pal.Surface;

            Color btnBorder = _isHovered ? pal.Border : Color.Transparent;

            var bodyRect = new Rectangle(2, 2, Width - 5, Height - 5);
            using (var path = ZeroUIConfig.CreateRoundedRectangle(bodyRect, _borderRadius))
            {
                using (var brush = new SolidBrush(btnBg))
                {
                    g.FillPath(brush, path);
                }

                if (btnBorder != Color.Transparent)
                {
                    using (var pen = new Pen(btnBorder, 1f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            // 2. Draw Icon or Text
            Color iconColor = _isHovered ? pal.Primary : pal.TextPrimary;
            if (_customIcon != null)
            {
                int ix = (Width - _customIcon.Width) / 2;
                int iy = (Height - _customIcon.Height) / 2;
                g.DrawImage(_customIcon, ix, iy);
            }
            else if (_iconType != BadgeIconType.None)
            {
                DrawVectorIcon(g, bodyRect, iconColor);
            }
            else if (!string.IsNullOrEmpty(Text))
            {
                TextRenderer.DrawText(g, Text, Font, bodyRect, iconColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            // 3. Draw Notification Badge
            bool shouldShowBadge = !_hideWhenZero || _badgeValue > 0 || !string.IsNullOrEmpty(_badgeText) || _isDot;
            if (shouldShowBadge)
            {
                DrawBadge(g, pal);
            }
        }

        private void DrawVectorIcon(Graphics g, Rectangle bounds, Color color)
        {
            int cx = bounds.X + bounds.Width / 2;
            int cy = bounds.Y + bounds.Height / 2;

            using (var pen = new Pen(color, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                switch (_iconType)
                {
                    case BadgeIconType.Bell:
                        // Bell Dome
                        g.DrawArc(pen, cx - 6, cy - 8, 12, 12, 180, 180);
                        g.DrawLine(pen, cx - 6, cy - 2, cx - 8, cy + 3);
                        g.DrawLine(pen, cx + 6, cy - 2, cx + 8, cy + 3);
                        g.DrawLine(pen, cx - 8, cy + 3, cx + 8, cy + 3);
                        // Clapper
                        g.DrawArc(pen, cx - 2, cy + 4, 4, 3, 0, 180);
                        break;

                    case BadgeIconType.Message:
                        // Envelope
                        var envRect = new Rectangle(cx - 8, cy - 6, 16, 12);
                        g.DrawRectangle(pen, envRect);
                        g.DrawLine(pen, envRect.Left, envRect.Top, cx, cy);
                        g.DrawLine(pen, envRect.Right, envRect.Top, cx, cy);
                        break;

                    case BadgeIconType.Task:
                        // Clipboard / Checklist
                        g.DrawRectangle(pen, cx - 7, cy - 7, 14, 15);
                        g.DrawLine(pen, cx - 4, cy - 2, cx - 2, cy);
                        g.DrawLine(pen, cx - 2, cy, cx + 4, cy - 5);
                        g.DrawLine(pen, cx - 4, cy + 3, cx + 4, cy + 3);
                        break;

                    case BadgeIconType.Warning:
                        // Triangle
                        var p1 = new Point(cx, cy - 7);
                        var p2 = new Point(cx - 7, cy + 6);
                        var p3 = new Point(cx + 7, cy + 6);
                        g.DrawPolygon(pen, new[] { p1, p2, p3 });
                        g.DrawLine(pen, cx, cy - 2, cx, cy + 2);
                        g.DrawEllipse(pen, cx - 0.5f, cy + 4, 1, 1);
                        break;
                }
            }
        }

        private void DrawBadge(Graphics g, ZeroThemePalette pal)
        {
            Color badgeBgColor = _customBadgeColor ?? pal.Danger;

            if (_isDot)
            {
                // Simple dot
                int dotSize = 8;
                int dx = Width - dotSize - 3;
                int dy = 3;
                using (var b = new SolidBrush(badgeBgColor))
                using (var p = new Pen(pal.Surface, 1.5f))
                {
                    g.FillEllipse(b, dx, dy, dotSize, dotSize);
                    g.DrawEllipse(p, dx, dy, dotSize, dotSize);
                }
                return;
            }

            string displayStr = _badgeText ?? (_badgeValue > 99 ? "99+" : _badgeValue.ToString());
            using (var badgeFont = new Font("Segoe UI", 7f, FontStyle.Bold))
            {
                Size measured = TextRenderer.MeasureText(displayStr, badgeFont);
                int bw = Math.Max(16, measured.Width + 4);
                int bh = 15;

                int bx = _badgePosition == BadgePosition.TopRight || _badgePosition == BadgePosition.BottomRight
                    ? Width - bw - 1
                    : 1;

                int by = _badgePosition == BadgePosition.TopRight || _badgePosition == BadgePosition.TopLeft
                    ? 1
                    : Height - bh - 1;

                var badgeRect = new Rectangle(bx, by, bw, bh);
                using (var path = ZeroUIConfig.CreateRoundedRectangle(badgeRect, bh / 2))
                using (var brush = new SolidBrush(badgeBgColor))
                using (var borderPen = new Pen(pal.Surface, 1.5f))
                {
                    g.FillPath(brush, path);
                    g.DrawPath(borderPen, path);
                }

                TextRenderer.DrawText(g, displayStr, badgeFont, badgeRect, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }

        #endregion
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZBadgeButton"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("BadgeButton is deprecated and will be removed in 5 release cycles. Please migrate to ZBadgeButton instead.")]
    [ToolboxItem(false)]
    public class BadgeButton : ZBadgeButton
    {
    }

    #endregion
}
