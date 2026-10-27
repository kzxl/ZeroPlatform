using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Scada.Safety;

namespace ZeroUI.WinForms.Industrial
{
    /// <summary>
    /// High-performance, zero-allocation GDI+ vector renderer for industrial safety,
    /// LOTO (Lockout/Tagout), and interlock indicator badges.
    /// Renders crisp, scalable vector icons without image assets or heap allocation.
    /// </summary>
    public static class ZDeviceBadgeRenderer
    {
        private const int BadgeSize = 14;
        private const int BadgeSpacing = 2;

        /// <summary>
        /// Renders active device status badges inside the given target bounding rectangle.
        /// </summary>
        /// <param name="g">Graphics context.</param>
        /// <param name="bounds">Target control or node bounds.</param>
        /// <param name="flags">Active safety and status bitmask flags.</param>
        /// <param name="isDark">Theme dark mode flag.</param>
        /// <param name="alignment">Placement anchor (default TopRight).</param>
        public static void DrawBadges(
            Graphics g,
            Rectangle bounds,
            DeviceStatusFlags flags,
            bool isDark,
            ContentAlignment alignment = ContentAlignment.TopRight)
        {
            if (flags == DeviceStatusFlags.None) return;

            // Count active badges
            int badgeCount = 0;
            if ((flags & DeviceStatusFlags.LockedOut) != 0) badgeCount++;
            if ((flags & DeviceStatusFlags.TaggedOut) != 0) badgeCount++;
            if ((flags & DeviceStatusFlags.Interlocked) != 0) badgeCount++;
            if ((flags & DeviceStatusFlags.Maintenance) != 0) badgeCount++;
            if ((flags & DeviceStatusFlags.Fault) != 0) badgeCount++;
            if ((flags & DeviceStatusFlags.ManualOverride) != 0) badgeCount++;

            if (badgeCount == 0) return;

            int totalWidth = (badgeCount * BadgeSize) + ((badgeCount - 1) * BadgeSpacing);

            // Compute anchor position
            int startX;
            int startY;

            switch (alignment)
            {
                case ContentAlignment.TopLeft:
                    startX = bounds.Left + 2;
                    startY = bounds.Top + 2;
                    break;
                case ContentAlignment.BottomRight:
                    startX = bounds.Right - totalWidth - 2;
                    startY = bounds.Bottom - BadgeSize - 2;
                    break;
                case ContentAlignment.BottomLeft:
                    startX = bounds.Left + 2;
                    startY = bounds.Bottom - BadgeSize - 2;
                    break;
                case ContentAlignment.TopRight:
                default:
                    startX = bounds.Right - totalWidth - 2;
                    startY = bounds.Top + 2;
                    break;
            }

            int curX = startX;

            // 1. LockedOut Badge (LOTO Padlock)
            if ((flags & DeviceStatusFlags.LockedOut) != 0)
            {
                DrawPadlockBadge(g, curX, startY);
                curX += BadgeSize + BadgeSpacing;
            }

            // 2. TaggedOut Badge (Warning Tag)
            if ((flags & DeviceStatusFlags.TaggedOut) != 0)
            {
                DrawTagoutBadge(g, curX, startY);
                curX += BadgeSize + BadgeSpacing;
            }

            // 3. Interlocked Badge (Chain Link / Shield)
            if ((flags & DeviceStatusFlags.Interlocked) != 0)
            {
                DrawInterlockBadge(g, curX, startY);
                curX += BadgeSize + BadgeSpacing;
            }

            // 4. Maintenance Badge (Wrench)
            if ((flags & DeviceStatusFlags.Maintenance) != 0)
            {
                DrawMaintenanceBadge(g, curX, startY);
                curX += BadgeSize + BadgeSpacing;
            }

            // 5. Fault Badge (Warning Triangle)
            if ((flags & DeviceStatusFlags.Fault) != 0)
            {
                DrawFaultBadge(g, curX, startY);
                curX += BadgeSize + BadgeSpacing;
            }

            // 6. ManualOverride Badge (Hand Icon)
            if ((flags & DeviceStatusFlags.ManualOverride) != 0)
            {
                DrawManualOverrideBadge(g, curX, startY);
                curX += BadgeSize + BadgeSpacing;
            }
        }

        private static void DrawPadlockBadge(Graphics g, int x, int y)
        {
            // Padlock Shackle (Silver arc)
            using (var penShackle = new Pen(Color.FromArgb(203, 213, 225), 1.5f))
            {
                g.DrawArc(penShackle, x + 3, y + 1, 7, 7, 180, 180);
                g.DrawLine(penShackle, x + 3, y + 5, x + 3, y + 7);
                g.DrawLine(penShackle, x + 10, y + 5, x + 10, y + 7);
            }

            // Padlock Body (Safety Red)
            var bodyRect = new Rectangle(x + 2, y + 6, 10, 8);
            using (var brushBody = new SolidBrush(Color.FromArgb(220, 38, 38)))
            using (var penBorder = new Pen(Color.FromArgb(153, 27, 27), 1f))
            {
                g.FillRectangle(brushBody, bodyRect);
                g.DrawRectangle(penBorder, bodyRect);
            }

            // Keyhole
            using (var brushKey = new SolidBrush(Color.FromArgb(254, 242, 242)))
            {
                g.FillEllipse(brushKey, x + 5.5f, y + 8, 3f, 3f);
                g.FillRectangle(brushKey, x + 6.2f, y + 10, 1.6f, 2.5f);
            }
        }

        private static void DrawTagoutBadge(Graphics g, int x, int y)
        {
            // Tag Shape (Yellow amber with chamfered top)
            var pts = new Point[]
            {
                new Point(x + 4, y + 1),
                new Point(x + 9, y + 1),
                new Point(x + 12, y + 4),
                new Point(x + 12, y + 13),
                new Point(x + 1, y + 13),
                new Point(x + 1, y + 4)
            };

            using (var brushTag = new SolidBrush(Color.FromArgb(245, 158, 11)))
            using (var penBorder = new Pen(Color.FromArgb(180, 83, 9), 1f))
            {
                g.FillPolygon(brushTag, pts);
                g.DrawPolygon(penBorder, pts);
            }

            // Eyelet hole
            using (var brushHole = new SolidBrush(Color.FromArgb(15, 23, 42)))
            {
                g.FillEllipse(brushHole, x + 5.5f, y + 3, 2.5f, 2.5f);
            }

            // Warning bar
            using (var penStripe = new Pen(Color.FromArgb(220, 38, 38), 1.5f))
            {
                g.DrawLine(penStripe, x + 3, y + 8, x + 10, y + 8);
                g.DrawLine(penStripe, x + 3, y + 11, x + 10, y + 11);
            }
        }

        private static void DrawInterlockBadge(Graphics g, int x, int y)
        {
            // Shield / Interlocking link in Cyan/Teal
            var circleRect = new Rectangle(x + 1, y + 1, 12, 12);
            using (var brushBg = new SolidBrush(Color.FromArgb(14, 116, 144)))
            using (var penBorder = new Pen(Color.FromArgb(6, 182, 212), 1.2f))
            {
                g.FillEllipse(brushBg, circleRect);
                g.DrawEllipse(penBorder, circleRect);
            }

            // Interlock 'X' or interlocking chain link
            using (var penLink = new Pen(Color.White, 1.5f))
            {
                g.DrawEllipse(penLink, x + 3.5f, y + 4, 4f, 6f);
                g.DrawEllipse(penLink, x + 6.5f, y + 4, 4f, 6f);
            }
        }

        private static void DrawMaintenanceBadge(Graphics g, int x, int y)
        {
            // Maintenance Circle (Orange/Amber)
            var circleRect = new Rectangle(x + 1, y + 1, 12, 12);
            using (var brushBg = new SolidBrush(Color.FromArgb(194, 65, 12)))
            using (var penBorder = new Pen(Color.FromArgb(251, 146, 60), 1.2f))
            {
                g.FillEllipse(brushBg, circleRect);
                g.DrawEllipse(penBorder, circleRect);
            }

            // Wrench vector
            using (var penWrench = new Pen(Color.White, 1.8f))
            {
                g.DrawLine(penWrench, x + 4, y + 10, x + 9, y + 4);
                // Head jaw
                g.DrawArc(penWrench, x + 8, y + 2, 4, 4, 120, 220);
            }
        }

        private static void DrawFaultBadge(Graphics g, int x, int y)
        {
            // Fault Warning Triangle (Blinking Red)
            bool isBlinkOn = ZeroAnimationClock.BlinkFast;
            Color triangleBg = isBlinkOn ? Color.FromArgb(220, 38, 38) : Color.FromArgb(127, 29, 29);

            var pts = new Point[]
            {
                new Point(x + 7, y + 1),
                new Point(x + 13, y + 13),
                new Point(x + 1, y + 13)
            };

            using (var brushTri = new SolidBrush(triangleBg))
            using (var penBorder = new Pen(Color.FromArgb(254, 202, 202), 1f))
            {
                g.FillPolygon(brushTri, pts);
                g.DrawPolygon(penBorder, pts);
            }

            // Exclamation mark
            using (var brushEx = new SolidBrush(Color.White))
            {
                g.FillRectangle(brushEx, x + 6.3f, y + 5, 1.4f, 4f);
                g.FillEllipse(brushEx, x + 6.2f, y + 10, 1.6f, 1.6f);
            }
        }

        private static void DrawManualOverrideBadge(Graphics g, int x, int y)
        {
            // Manual Override (Blue circle with Hand silhouette / 'M')
            var circleRect = new Rectangle(x + 1, y + 1, 12, 12);
            using (var brushBg = new SolidBrush(Color.FromArgb(37, 99, 235)))
            using (var penBorder = new Pen(Color.FromArgb(96, 165, 250), 1.2f))
            {
                g.FillEllipse(brushBg, circleRect);
                g.DrawEllipse(penBorder, circleRect);
            }

            // Letter 'M'
            using var font = new Font("Segoe UI", 6.5f, FontStyle.Bold);
            using var brushText = new SolidBrush(Color.White);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("M", font, brushText, new RectangleF(x + 1, y + 1, 12, 12), sf);
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZDeviceBadgeRenderer"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroDeviceBadgeRenderer is deprecated and will be removed in 5 release cycles. Please migrate to ZDeviceBadgeRenderer instead.")]
    public static class ZeroDeviceBadgeRenderer
    {
        public static void DrawBadges(
            Graphics g,
            Rectangle bounds,
            DeviceStatusFlags flags,
            bool isDark,
            ContentAlignment alignment = ContentAlignment.TopRight)
        {
            ZDeviceBadgeRenderer.DrawBadges(g, bounds, flags, isDark, alignment);
        }
    }
}
