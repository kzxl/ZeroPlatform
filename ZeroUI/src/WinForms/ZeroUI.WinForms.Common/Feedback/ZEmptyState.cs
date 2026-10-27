using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Feedback
{
    /// <summary>
    /// Clean, themed empty state placeholder for lists, grids, and search views,
    /// complete with vector/emoji glyph, title, description, and call-to-action button.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Feedback")]
    [DefaultProperty("Title")]
    [DefaultEvent("ActionClicked")]
    [Description("Empty state placeholder with glyph, description, and action button")]
    public class ZEmptyState : ControlBase
    {
        private string _glyph = "📭";
        private string _title = "No Data Available";
        private string _description = "There are no records to display at this time.";
        private string _actionText = "Refresh";

        private Rectangle _actionButtonRect = Rectangle.Empty;
        private bool _isActionHovered = false;

        public event EventHandler? ActionClicked;

        [Category("Appearance")]
        [DefaultValue("📭")]
        [Description("Emoji or glyph icon character displayed prominently.")]
        public string Glyph
        {
            get => _glyph;
            set { _glyph = value ?? string.Empty; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("No Data Available")]
        [Description("Primary headline message.")]
        public string Title
        {
            get => _title;
            set { _title = value ?? string.Empty; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("There are no records to display at this time.")]
        [Description("Detailed explanatory text.")]
        public string Description
        {
            get => _description;
            set { _description = value ?? string.Empty; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("Refresh")]
        [Description("Label for the action button (null or empty hides the button).")]
        public string ActionText
        {
            get => _actionText;
            set { _actionText = value ?? string.Empty; Invalidate(); }
        }

        [Browsable(false)]
        public bool ShowAction => !string.IsNullOrWhiteSpace(_actionText);

        public ZEmptyState()
        {
            Size = new Size(320, 220);
            BackColor = Color.Transparent;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool wasHovered = _isActionHovered;
            _isActionHovered = ShowAction && _actionButtonRect.Contains(e.Location);
            Cursor = _isActionHovered ? Cursors.Hand : Cursors.Default;

            if (wasHovered != _isActionHovered)
            {
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isActionHovered)
            {
                _isActionHovered = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (ShowAction && _actionButtonRect.Contains(e.Location))
            {
                ActionClicked?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var bounds = ClientRectangle;
            var c = ZeroTheme.Colors;

            int centerX = bounds.Width / 2;
            int totalContentH = 48 + 24 + 32 + (ShowAction ? 36 : 0);
            int startY = Math.Max(10, (bounds.Height - totalContentH) / 2);

            // 1. Glyph
            if (!string.IsNullOrEmpty(_glyph))
            {
                var glyphFont = ZeroFontCache.Get(28f);
                var glyphSize = g.MeasureString(_glyph, glyphFont);
                var glyphRect = new Rectangle(centerX - (int)(glyphSize.Width / 2), startY, (int)glyphSize.Width + 2, 44);

                using var glyphBrush = new SolidBrush(c.TextMuted);
                g.DrawString(_glyph, glyphFont, glyphBrush, glyphRect, new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                });
                startY += 48;
            }

            // 2. Title
            if (!string.IsNullOrEmpty(_title))
            {
                var titleFont = ZeroFontCache.Get(11f, FontStyle.Bold);
                var titleRect = new Rectangle(16, startY, bounds.Width - 32, 24);

                using var titleBrush = new SolidBrush(c.TextPrimary);
                g.DrawString(_title, titleFont, titleBrush, titleRect, new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                });
                startY += 26;
            }

            // 3. Description
            if (!string.IsNullOrEmpty(_description))
            {
                var descFont = ZeroFontCache.Get(8.5f);
                var descRect = new Rectangle(20, startY, bounds.Width - 40, 36);

                using var descBrush = new SolidBrush(c.TextMuted);
                g.DrawString(_description, descFont, descBrush, descRect, new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Near
                });
                startY += 38;
            }

            // 4. Action Button
            if (ShowAction)
            {
                var btnFont = ZeroFontCache.Get(8.5f, FontStyle.Bold);
                var textSize = g.MeasureString(_actionText, btnFont);
                int btnW = Math.Max(88, (int)textSize.Width + 28);
                int btnH = 28;

                _actionButtonRect = new Rectangle(centerX - (btnW / 2), startY + 4, btnW, btnH);

                var btnBgColor = _isActionHovered ? c.PrimaryHover : c.Primary;
                using var btnBgBrush = new SolidBrush(btnBgColor);
                FillRoundedRect(g, btnBgBrush, _actionButtonRect, 4);

                using var btnTextBrush = new SolidBrush(Color.White);
                g.DrawString(_actionText, btnFont, btnTextBrush, _actionButtonRect, new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                });
            }
            else
            {
                _actionButtonRect = Rectangle.Empty;
            }
        }

        private static void FillRoundedRect(Graphics g, Brush brush, Rectangle rect, int radius)
        {
            using var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            g.FillPath(brush, path);
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            Invalidate();
        }
    }

    [Obsolete("ZeroEmptyState is deprecated and will be removed in 5 release cycles. Please migrate to ZEmptyState instead.")]
    [ToolboxItem(false)]
    public class ZeroEmptyState : ZEmptyState { }
}
