using System;

using ZeroUI.WinForms.Icons;using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Feedback
{
    public enum ZeroAlertSeverity

    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Modern dismissible alert banner for factory floor stoppage, defect alarms, and system broadcasts.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [DefaultProperty("Title")]
    [DefaultEvent("Closed")]
    [Description("Dismissible alert banner for factory floor stoppages and system notifications")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroAlertBanner.bmp")]
    public class ZAlertBanner : Control
    {

        private ZeroAlertSeverity _severity = ZeroAlertSeverity.Warning;
        private string _title = "Alert Title";
        private string _message = "Alert detailed notification message.";
        private bool _isClosable = true;
        private Rectangle _closeRect;
        private bool _isCloseHovered = false;

        public event EventHandler? Closed;

        public ZAlertBanner()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(500, 48);
            Dock = DockStyle.Top;
            Font = new Font("Segoe UI", 9f);

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        private void OnThemeChanged(object? sender, EventArgs e) => Invalidate();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ZeroTheme.ThemeChanged -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }

        [Category("Appearance")]
        [DefaultValue(ZeroAlertSeverity.Warning)]
        public ZeroAlertSeverity Severity
        {
            get => _severity;
            set { _severity = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("Alert Title")]
        public string Title
        {
            get => _title;
            set { _title = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("Alert detailed notification message.")]
        public string Message
        {
            get => _message;
            set { _message = value; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool IsClosable
        {
            get => _isClosable;
            set { _isClosable = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var (bgCol, borderCol, iconCol, iconChar) = GetThemeColors(_severity);

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // 1. Draw Background & Rounded Border
            using (var path = CreateRoundedRectangle(rect, 6))
            {
                using var bgBrush = new SolidBrush(bgCol);
                g.FillPath(bgBrush, path);

                using var borderPen = new Pen(borderCol, 1f);
                g.DrawPath(borderPen, path);
            }

            // 2. Draw Severity Icon
            int iconX = 14;
            int centerY = Height / 2;
            Rectangle iconRect = new Rectangle(iconX, centerY - 10, 20, 20);
            TextRenderer.DrawText(
                g,
                iconChar,
                ZeroFontCache.Get("Segoe UI", 11f, FontStyle.Bold),
                iconRect,
                iconCol,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            // 3. Draw Title & Message
            int textLeft = iconX + 26;
            int textRight = _isClosable ? Width - 36 : Width - 14;
            int textWidth = Math.Max(20, textRight - textLeft);

            using var titleFont = new Font("Segoe UI", 9f, FontStyle.Bold);
            Size titleSize = TextRenderer.MeasureText(g, _title, titleFont);
            var palette = ZeroTheme.Colors;

            if (string.IsNullOrEmpty(_message))
            {
                Rectangle singleRect = new Rectangle(textLeft, 0, textWidth, Height);
                TextRenderer.DrawText(g, _title, titleFont, singleRect, palette.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
            else
            {
                // Title and message inline or stacked
                Rectangle titleRect = new Rectangle(textLeft, 0, titleSize.Width + 4, Height);
                TextRenderer.DrawText(g, _title + ":", titleFont, titleRect, palette.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                int msgLeft = titleRect.Right + 4;
                using var msgFont = new Font("Segoe UI", 9f, FontStyle.Regular);
                Rectangle msgRect = new Rectangle(msgLeft, 0, textRight - msgLeft, Height);
                TextRenderer.DrawText(g, _message, msgFont, msgRect, palette.TextSecondary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            // 4. Draw Close Button
            if (_isClosable)
            {
                _closeRect = new Rectangle(Width - 28, centerY - 10, 20, 20);
                Color closeColor = _isCloseHovered ? palette.TextPrimary : palette.TextSecondary;
                TextRenderer.DrawText(g, "✕", ZeroFontCache.Get("Segoe UI", 9f, FontStyle.Bold), _closeRect, closeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                _closeRect = Rectangle.Empty;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_closeRect.IsEmpty && _closeRect.Contains(e.Location))
            {
                if (!_isCloseHovered)
                {
                    _isCloseHovered = true;
                    Cursor = Cursors.Hand;
                    Invalidate(_closeRect);
                }
            }
            else if (_isCloseHovered)
            {
                _isCloseHovered = false;
                Cursor = Cursors.Default;
                Invalidate(_closeRect);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isCloseHovered)
            {
                _isCloseHovered = false;
                Cursor = Cursors.Default;
                Invalidate(_closeRect);
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left && !_closeRect.IsEmpty && _closeRect.Contains(e.Location))
            {
                Visible = false;
                Closed?.Invoke(this, EventArgs.Empty);
            }
        }

        private static (Color bg, Color border, Color icon, string glyph) GetThemeColors(ZeroAlertSeverity severity)
        {
            bool isDark = ZeroTheme.IsDark;
            return severity switch
            {
                ZeroAlertSeverity.Success => isDark
                    ? (Color.FromArgb(35, 16, 185, 129), Color.FromArgb(16, 185, 129), Color.FromArgb(52, 211, 153), "✔")
                    : (Color.FromArgb(246, 255, 237), Color.FromArgb(183, 235, 143), Color.FromArgb(82, 196, 26), "✔"),
                ZeroAlertSeverity.Warning => isDark
                    ? (Color.FromArgb(35, 245, 158, 11), Color.FromArgb(245, 158, 11), Color.FromArgb(251, 191, 36), "⚠")
                    : (Color.FromArgb(255, 251, 230), Color.FromArgb(255, 229, 143), Color.FromArgb(250, 173, 20), "⚠"),
                ZeroAlertSeverity.Error => isDark
                    ? (Color.FromArgb(35, 239, 68, 68), Color.FromArgb(239, 68, 68), Color.FromArgb(248, 113, 113), "✖")
                    : (Color.FromArgb(255, 242, 240), Color.FromArgb(255, 204, 199), Color.FromArgb(255, 77, 79), "✖"),
                _ => isDark
                    ? (Color.FromArgb(35, 59, 130, 246), Color.FromArgb(59, 130, 246), Color.FromArgb(96, 165, 250), "ℹ")
                    : (Color.FromArgb(230, 244, 255), Color.FromArgb(145, 202, 255), Color.FromArgb(22, 119, 255), "ℹ")
            };
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius) =>
            ZeroUIConfig.CreateRoundedRectangle(rect, radius);
    }

    /// <summary>
    /// Legacy alias for <see cref="ZAlertBanner"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroAlertBanner is deprecated and will be removed in 5 release cycles. Please migrate to ZAlertBanner instead.")]
    [ToolboxItem(false)]
    public class ZeroAlertBanner : ZAlertBanner
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZAlertBanner"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("AlertBanner is deprecated and will be removed in 5 release cycles. Please migrate to ZAlertBanner instead.")]
    [ToolboxItem(false)]
    public class AlertBanner : ZAlertBanner
    {
    }

    #endregion
}
