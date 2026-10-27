using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Buttons
{
    public partial class ZFloatingActionButton
    {
        #region Painting

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            int diameter = Math.Min(Width, Height) - 1;
            if (diameter <= 2) return;

            var circleBounds = new Rectangle(0, 0, diameter, diameter);

            // 1. Resolve Background Palette
            Color cStart = _gradientStart ?? (UseDefaultSkin ? Color.FromArgb(99, 102, 241) : ZeroTheme.Colors.Primary);
            Color cEnd = _gradientEnd ?? (UseDefaultSkin ? Color.FromArgb(139, 92, 246) : ZeroTheme.Colors.PrimaryHover);

            if (_isHovered)
            {
                cStart = LightenColor(cStart, 0.15f);
                cEnd = LightenColor(cEnd, 0.15f);
            }
            if (_isPressed)
            {
                cStart = DarkenColor(cStart, 0.12f);
                cEnd = DarkenColor(cEnd, 0.12f);
            }

            // 2. Fill Circular Gradient Surface
            using (var circlePath = new GraphicsPath())
            {
                circlePath.AddEllipse(circleBounds);

                if (_useGradient)
                {
                    using (var brush = new LinearGradientBrush(circleBounds, cStart, cEnd, 45f))
                    {
                        g.FillPath(brush, circlePath);
                    }
                }
                else
                {
                    using (var brush = new SolidBrush(cStart))
                    {
                        g.FillPath(brush, circlePath);
                    }
                }

                // Inner Glassmorphism Highlight Ring
                using (var pen = new Pen(Color.FromArgb(180, 255, 255, 255), 1.5f))
                {
                    g.DrawPath(pen, circlePath);
                }
            }

            // 3. Render Glyphs
            DrawGlyph(g, circleBounds);

            // 4. Render Badge Overlay (if present)
            if (!string.IsNullOrEmpty(_badgeText))
            {
                DrawBadge(g, circleBounds);
            }
        }

        private void DrawGlyph(Graphics g, Rectangle bounds)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;

            switch (_iconType)
            {
                case ZFloatingActionIcon.AiSparkles:
                    using (var brush = new SolidBrush(_iconColor))
                    {
                        DrawSparkle(g, brush, cx - 2f, cy - 1f, bounds.Width * 0.22f);
                        DrawSparkle(g, brush, cx + bounds.Width * 0.18f, cy - bounds.Height * 0.18f, bounds.Width * 0.11f);
                    }
                    break;

                case ZFloatingActionIcon.Chat:
                    DrawChatBubble(g, bounds);
                    break;

                case ZFloatingActionIcon.Plus:
                    using (var pen = new Pen(_iconColor, Math.Max(2.2f, bounds.Width * 0.055f)))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        float arm = bounds.Width * 0.18f;
                        g.DrawLine(pen, new PointF(cx - arm, cy), new PointF(cx + arm, cy));
                        g.DrawLine(pen, new PointF(cx, cy - arm), new PointF(cx, cy + arm));
                    }
                    break;

                case ZFloatingActionIcon.Vector:
                    if (_vectorIconKey.HasValue)
                    {
                        float sz = bounds.Width * 0.44f;
                        var r = new RectangleF(cx - sz / 2f, cy - sz / 2f, sz, sz);
                        ZeroVectorIcons.Draw(g, _vectorIconKey.Value, r, _iconColor);
                    }
                    break;

                case ZFloatingActionIcon.Custom:
                    if (_customIcon != null)
                    {
                        int sz = (int)(bounds.Width * 0.48f);
                        g.DrawImage(_customIcon, new Rectangle((int)(cx - sz / 2), (int)(cy - sz / 2), sz, sz));
                    }
                    break;
            }
        }

        private void DrawSparkle(Graphics g, Brush brush, float cx, float cy, float radius)
        {
            var points = new PointF[8];
            float inner = radius * 0.35f;

            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4.0;
                float r = (i % 2 == 0) ? radius : inner;
                points[i] = new PointF((float)(cx + r * Math.Cos(angle)), (float)(cy + r * Math.Sin(angle)));
            }

            g.FillPolygon(brush, points);
        }

        private void DrawChatBubble(Graphics g, Rectangle bounds)
        {
            float cx = bounds.X + bounds.Width / 2f;
            float cy = bounds.Y + bounds.Height / 2f;
            float w = bounds.Width * 0.46f;
            float h = bounds.Height * 0.36f;

            using (var path = new GraphicsPath())
            {
                float x = cx - w / 2f;
                float y = cy - h / 2f - 2f;
                float r = 6f;

                path.AddArc(x, y, r, r, 180, 90);
                path.AddArc(x + w - r, y, r, r, 270, 90);
                path.AddArc(x + w - r, y + h - r, r, r, 0, 90);
                // Tail
                path.AddLine(x + w * 0.5f, y + h, x + w * 0.3f, y + h + 5f);
                path.AddLine(x + w * 0.3f, y + h + 5f, x + w * 0.35f, y + h);
                path.AddArc(x, y + h - r, r, r, 90, 90);
                path.CloseFigure();

                using (var brush = new SolidBrush(_iconColor))
                {
                    g.FillPath(brush, path);
                }

                // 3 Small dots inside chat bubble
                using (var dotBrush = new SolidBrush(_gradientStart ?? Color.FromArgb(99, 102, 241)))
                {
                    float dotR = 2.2f;
                    float midY = y + h * 0.5f;
                    g.FillEllipse(dotBrush, new RectangleF(cx - 6f - dotR, midY - dotR, dotR * 2, dotR * 2));
                    g.FillEllipse(dotBrush, new RectangleF(cx - dotR, midY - dotR, dotR * 2, dotR * 2));
                    g.FillEllipse(dotBrush, new RectangleF(cx + 6f - dotR, midY - dotR, dotR * 2, dotR * 2));
                }
            }
        }

        private void DrawBadge(Graphics g, Rectangle bounds)
        {
            using (var font = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            {
                var text = _badgeText ?? string.Empty;
                var size = g.MeasureString(text, font);
                float bw = Math.Max(16f, size.Width + 6f);
                float bh = 16f;
                float bx = bounds.Right - bw - 2f;
                float by = bounds.Top + 2f;

                using (var badgePath = new GraphicsPath())
                {
                    badgePath.AddArc(bx, by, bh, bh, 90, 180);
                    badgePath.AddArc(bx + bw - bh, by, bh, bh, 270, 180);
                    badgePath.CloseFigure();

                    using (var brush = new SolidBrush(_badgeColor))
                    {
                        g.FillPath(brush, badgePath);
                    }
                    using (var pen = new Pen(Color.White, 1.2f))
                    {
                        g.DrawPath(pen, badgePath);
                    }
                }

                using (var textBrush = new SolidBrush(Color.White))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString(text, font, textBrush, new RectangleF(bx, by, bw, bh), sf);
                }
            }
        }

        private static Color LightenColor(Color c, float amount)
        {
            return Color.FromArgb(
                c.A,
                Math.Min(255, (int)(c.R + (255 - c.R) * amount)),
                Math.Min(255, (int)(c.G + (255 - c.G) * amount)),
                Math.Min(255, (int)(c.B + (255 - c.B) * amount))
            );
        }

        private static Color DarkenColor(Color c, float amount)
        {
            return Color.FromArgb(
                c.A,
                Math.Max(0, (int)(c.R * (1f - amount))),
                Math.Max(0, (int)(c.G * (1f - amount))),
                Math.Max(0, (int)(c.B * (1f - amount)))
            );
        }

        #endregion
    }
}
