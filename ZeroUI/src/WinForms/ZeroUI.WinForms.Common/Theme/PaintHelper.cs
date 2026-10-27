using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Theme
{
    /// <summary>
    /// High-performance GDI+ graphics routines and styling helpers for ZeroUI WinForms controls.
    /// Standardizes anti-aliased card backgrounds, status badges, focus rings, and borders.
    /// </summary>
    public static class PaintHelper
    {
        /// <summary>
        /// Draws a smooth anti-aliased card surface with rounded corners and border.
        /// </summary>
        public static void DrawCard(Graphics g, Rectangle bounds, Color backColor, Color borderColor, int cornerRadius = 4)
        {
            if (g == null || bounds.Width <= 0 || bounds.Height <= 0) return;

            var oldSmoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (cornerRadius > 0)
            {
                using var path = CreateRoundedRectangle(bounds, cornerRadius);
                using var brush = new SolidBrush(backColor);
                g.FillPath(brush, path);

                if (borderColor != Color.Transparent)
                {
                    using var pen = new Pen(borderColor, 1f);
                    g.DrawPath(pen, path);
                }
            }
            else
            {
                using var brush = new SolidBrush(backColor);
                g.FillRectangle(brush, bounds);

                if (borderColor != Color.Transparent)
                {
                    using var pen = new Pen(borderColor, 1f);
                    g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
                }
            }

            g.SmoothingMode = oldSmoothing;
        }

        /// <summary>
        /// Draws a standardized industrial status pill badge (e.g. NORMAL, FAULT, ACTIVE).
        /// </summary>
        public static void DrawStatusBadge(Graphics g, Rectangle bounds, string text, Font font, Color statusColor, Color textColor, int cornerRadius = 3)
        {
            if (g == null || bounds.Width <= 0 || bounds.Height <= 0) return;

            var oldSmoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Semi-transparent badge background (20% opacity)
            Color pillFill = Color.FromArgb(40, statusColor);
            using (var path = CreateRoundedRectangle(bounds, cornerRadius))
            {
                using (var fillBrush = new SolidBrush(pillFill))
                {
                    g.FillPath(fillBrush, path);
                }
                using (var borderPen = new Pen(statusColor, 1f))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            TextRenderer.DrawText(
                g,
                text,
                font,
                bounds,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            g.SmoothingMode = oldSmoothing;
        }

        /// <summary>
        /// Draws an accessibility-compliant high-visibility focus ring.
        /// </summary>
        public static void DrawFocusRing(Graphics g, Rectangle bounds, Color focusColor, int cornerRadius = 4)
        {
            if (g == null || bounds.Width <= 0 || bounds.Height <= 0) return;

            var oldSmoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle focusRect = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2);
            using var path = CreateRoundedRectangle(focusRect, Math.Max(1, cornerRadius - 1));
            using var pen = new Pen(focusColor, 1.5f) { DashStyle = DashStyle.Dash };
            g.DrawPath(pen, path);

            g.SmoothingMode = oldSmoothing;
        }

        /// <summary>
        /// Draws a standardized industrial container card box with subtle theme-aware alpha fill and a header title.
        /// </summary>
        public static void DrawCardBox(Graphics g, Rectangle bounds, string title, Font titleFont, ZeroThemePalette palette)
        {
            if (g == null || bounds.Width <= 0 || bounds.Height <= 0) return;

            using (var boxBrush = new SolidBrush(Color.FromArgb(12, palette.TextPrimary)))
            using (var borderPen = new Pen(palette.Border, 1f))
            {
                g.FillRectangle(boxBrush, bounds);
                g.DrawRectangle(borderPen, bounds);
                if (!string.IsNullOrEmpty(title) && titleFont != null)
                {
                    Rectangle titleRect = new Rectangle(bounds.X + 14, bounds.Y + 8, Math.Max(10, bounds.Width - 28), 18);
                    TextRenderer.DrawText(g, title, titleFont, titleRect, palette.TextSecondary,
                        TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
                }
            }
        }

        /// <summary>
        /// Draws a standardized KPI summary cell with a secondary small label and a prominent value.
        /// </summary>
        public static void DrawKpiCell(Graphics g, Rectangle bounds, string label, string value, Color valueColor, Font labelFont, Font valueFont, ZeroThemePalette palette)
        {
            if (g == null || bounds.Width <= 0 || bounds.Height <= 0) return;

            if (!string.IsNullOrEmpty(label) && labelFont != null)
            {
                Rectangle lblRect = new Rectangle(bounds.X + 10, bounds.Y + 5, Math.Max(10, bounds.Width - 16), 16);
                TextRenderer.DrawText(g, label, labelFont, lblRect, palette.TextSecondary,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
            }
            if (!string.IsNullOrEmpty(value) && valueFont != null)
            {
                Rectangle valRect = new Rectangle(bounds.X + 10, bounds.Y + 22, Math.Max(10, bounds.Width - 16), Math.Max(16, bounds.Height - 24));
                TextRenderer.DrawText(g, value, valueFont, valRect, valueColor,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
            }
        }

        /// <summary>
        /// Draws a two-column key-value data row aligned across a card panel with overlap prevention.
        /// </summary>
        public static void DrawDataRow(Graphics g, Rectangle bounds, string label, string value, Color labelColor, Color valueColor, Font labelFont, Font valueFont)
        {
            if (g == null || bounds.Width <= 0 || bounds.Height <= 0) return;

            Size valSize = (!string.IsNullOrEmpty(value) && valueFont != null)
                ? TextRenderer.MeasureText(g, value, valueFont)
                : Size.Empty;

            if (!string.IsNullOrEmpty(label) && labelFont != null)
            {
                int maxLabelW = Math.Max(10, bounds.Width - valSize.Width - 8);
                Rectangle labelRect = new Rectangle(bounds.X, bounds.Y, maxLabelW, bounds.Height);
                TextRenderer.DrawText(g, label, labelFont, labelRect, labelColor,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
            }

            if (valSize.Width > 0 && valueFont != null)
            {
                int valX = Math.Max(bounds.X, bounds.Right - valSize.Width);
                int valW = Math.Min(valSize.Width, bounds.Right - valX);
                Rectangle valRect = new Rectangle(valX, bounds.Y, valW, bounds.Height);
                TextRenderer.DrawText(g, value, valueFont, valRect, valueColor,
                    TextFormatFlags.Right | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
            }
        }

        /// <summary>
        /// Draws a small circular status LED indicator with an adjacent caption.
        /// </summary>
        public static void DrawLedIndicator(Graphics g, int x, int y, Color ledColor, string label, Font font)
        {
            if (g == null) return;

            using (var brush = new SolidBrush(ledColor))
            {
                g.FillEllipse(brush, x, y, 10, 10);
            }

            if (!string.IsNullOrEmpty(label) && font != null)
            {
                TextRenderer.DrawText(g, label, font, new Point(x + 14, y - 2), ledColor);
            }
        }

        /// <summary>
        /// Creates a GraphicsPath for a rounded rectangle.
        /// </summary>
        public static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            int diameter = radius * 2;
            Size size = new Size(diameter, diameter);
            Rectangle arc = new Rectangle(rect.Location, size);

            // Top-left
            path.AddArc(arc, 180, 90);

            // Top-right
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);

            // Bottom-right
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // Bottom-left
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="PaintHelper"/>.
    /// </summary>
    public static class ZeroPaintHelper
    {
        public static void DrawCard(Graphics g, Rectangle bounds, Color backColor, Color borderColor, int cornerRadius = 4)
            => PaintHelper.DrawCard(g, bounds, backColor, borderColor, cornerRadius);

        public static void DrawStatusBadge(Graphics g, Rectangle bounds, string text, Font font, Color statusColor, Color textColor, int cornerRadius = 3)
            => PaintHelper.DrawStatusBadge(g, bounds, text, font, statusColor, textColor, cornerRadius);

        public static void DrawFocusRing(Graphics g, Rectangle bounds, Color focusColor, int cornerRadius = 4)
            => PaintHelper.DrawFocusRing(g, bounds, focusColor, cornerRadius);

        public static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
            => PaintHelper.CreateRoundedRectangle(rect, radius);
    }
}
