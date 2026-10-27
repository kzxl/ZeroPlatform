using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Scada.Safety;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// High-performance, zero-allocation WPF vector renderer for industrial safety,
    /// LOTO (Lockout/Tagout), and interlock indicator badges.
    /// Renders crisp, scalable vector badges without image assets or heap allocation.
    /// </summary>
    public static class DeviceBadgeRenderer
    {
        private const double BadgeSize = 14;
        private const double BadgeSpacing = 2;

        // Cached frozen pens & brushes for zero-allocation rendering
        private static readonly Pen ShacklePen;
        private static readonly Brush PadlockBodyBrush;
        private static readonly Pen PadlockBorderPen;
        private static readonly Brush KeyholeBrush;

        private static readonly Brush TagBrush;
        private static readonly Pen TagBorderPen;
        private static readonly Brush TagHoleBrush;
        private static readonly Pen TagStripePen;

        private static readonly Brush InterlockBrush;
        private static readonly Pen InterlockBorderPen;
        private static readonly Pen InterlockLinkPen;

        private static readonly Brush MaintBrush;
        private static readonly Pen MaintBorderPen;
        private static readonly Pen MaintWrenchPen;

        private static readonly Brush FaultDarkBrush;
        private static readonly Brush FaultLightBrush;
        private static readonly Pen FaultBorderPen;
        private static readonly Brush WhiteBrush;

        private static readonly Brush ManualBrush;
        private static readonly Pen ManualBorderPen;
        private static readonly Typeface BadgeFont;

        static DeviceBadgeRenderer()
        {
            ShacklePen = new Pen(new SolidColorBrush(Color.FromRgb(203, 213, 225)), 1.5);
            ShacklePen.Freeze();

            PadlockBodyBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38));
            PadlockBodyBrush.Freeze();

            PadlockBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(153, 27, 27)), 1.0);
            PadlockBorderPen.Freeze();

            KeyholeBrush = new SolidColorBrush(Color.FromRgb(254, 242, 242));
            KeyholeBrush.Freeze();

            TagBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            TagBrush.Freeze();

            TagBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(180, 83, 9)), 1.0);
            TagBorderPen.Freeze();

            TagHoleBrush = new SolidColorBrush(Color.FromRgb(15, 23, 42));
            TagHoleBrush.Freeze();

            TagStripePen = new Pen(new SolidColorBrush(Color.FromRgb(220, 38, 38)), 1.5);
            TagStripePen.Freeze();

            InterlockBrush = new SolidColorBrush(Color.FromRgb(14, 116, 144));
            InterlockBrush.Freeze();

            InterlockBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(6, 182, 212)), 1.2);
            InterlockBorderPen.Freeze();

            InterlockLinkPen = new Pen(Brushes.White, 1.5);
            InterlockLinkPen.Freeze();

            MaintBrush = new SolidColorBrush(Color.FromRgb(194, 65, 12));
            MaintBrush.Freeze();

            MaintBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(251, 146, 60)), 1.2);
            MaintBorderPen.Freeze();

            MaintWrenchPen = new Pen(Brushes.White, 1.8);
            MaintWrenchPen.Freeze();

            FaultDarkBrush = new SolidColorBrush(Color.FromRgb(127, 29, 29));
            FaultDarkBrush.Freeze();

            FaultLightBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38));
            FaultLightBrush.Freeze();

            FaultBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(254, 202, 202)), 1.0);
            FaultBorderPen.Freeze();

            WhiteBrush = Brushes.White;

            ManualBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            ManualBrush.Freeze();

            ManualBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(96, 165, 250)), 1.2);
            ManualBorderPen.Freeze();

            BadgeFont = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        }

        /// <summary>
        /// Renders active device status badges inside the given target bounding rectangle.
        /// </summary>
        public static void DrawBadges(
            DrawingContext dc,
            Rect bounds,
            DeviceStatusFlags flags,
            double dpi = 1.0)
        {
            if (flags == DeviceStatusFlags.None) return;

            int badgeCount = 0;
            if ((flags & DeviceStatusFlags.LockedOut) != 0) badgeCount++;
            if ((flags & DeviceStatusFlags.TaggedOut) != 0) badgeCount++;
            if ((flags & DeviceStatusFlags.Interlocked) != 0) badgeCount++;
            if ((flags & DeviceStatusFlags.Maintenance) != 0) badgeCount++;
            if ((flags & DeviceStatusFlags.Fault) != 0) badgeCount++;
            if ((flags & DeviceStatusFlags.ManualOverride) != 0) badgeCount++;

            if (badgeCount == 0) return;

            double totalWidth = (badgeCount * BadgeSize) + ((badgeCount - 1) * BadgeSpacing);
            double startX = bounds.Right - totalWidth - 2;
            double startY = bounds.Top + 2;

            double curX = startX;

            // 1. LockedOut Badge (LOTO Padlock)
            if ((flags & DeviceStatusFlags.LockedOut) != 0)
            {
                DrawPadlockBadge(dc, curX, startY);
                curX += BadgeSize + BadgeSpacing;
            }

            // 2. TaggedOut Badge (Warning Tag)
            if ((flags & DeviceStatusFlags.TaggedOut) != 0)
            {
                DrawTagoutBadge(dc, curX, startY);
                curX += BadgeSize + BadgeSpacing;
            }

            // 3. Interlocked Badge
            if ((flags & DeviceStatusFlags.Interlocked) != 0)
            {
                DrawInterlockBadge(dc, curX, startY);
                curX += BadgeSize + BadgeSpacing;
            }

            // 4. Maintenance Badge
            if ((flags & DeviceStatusFlags.Maintenance) != 0)
            {
                DrawMaintenanceBadge(dc, curX, startY);
                curX += BadgeSize + BadgeSpacing;
            }

            // 5. Fault Badge
            if ((flags & DeviceStatusFlags.Fault) != 0)
            {
                DrawFaultBadge(dc, curX, startY);
                curX += BadgeSize + BadgeSpacing;
            }

            // 6. ManualOverride Badge
            if ((flags & DeviceStatusFlags.ManualOverride) != 0)
            {
                DrawManualOverrideBadge(dc, curX, startY, dpi);
                curX += BadgeSize + BadgeSpacing;
            }
        }

        private static void DrawPadlockBadge(DrawingContext dc, double x, double y)
        {
            // Shackle
            StreamGeometry shackleGeom = new StreamGeometry();
            using (var ctx = shackleGeom.Open())
            {
                ctx.BeginFigure(new Point(x + 3.5, y + 6.5), false, false);
                ctx.LineTo(new Point(x + 3.5, y + 4.5), true, false);
                ctx.ArcTo(new Point(x + 10.5, y + 4.5), new Size(3.5, 3.5), 0, false, SweepDirection.Clockwise, true, false);
                ctx.LineTo(new Point(x + 10.5, y + 6.5), true, false);
            }
            shackleGeom.Freeze();
            dc.DrawGeometry(null, ShacklePen, shackleGeom);

            // Body
            var bodyRect = new Rect(x + 2, y + 6, 10, 8);
            dc.DrawRoundedRectangle(PadlockBodyBrush, PadlockBorderPen, bodyRect, 1.5, 1.5);

            // Keyhole
            dc.DrawEllipse(KeyholeBrush, null, new Point(x + 7, y + 9), 1.5, 1.5);
            dc.DrawRectangle(KeyholeBrush, null, new Rect(x + 6.3, y + 9.5, 1.4, 2.5));
        }

        private static void DrawTagoutBadge(DrawingContext dc, double x, double y)
        {
            StreamGeometry tagGeom = new StreamGeometry();
            using (var ctx = tagGeom.Open())
            {
                ctx.BeginFigure(new Point(x + 4, y + 1), true, true);
                ctx.LineTo(new Point(x + 9, y + 1), true, false);
                ctx.LineTo(new Point(x + 12, y + 4), true, false);
                ctx.LineTo(new Point(x + 12, y + 13), true, false);
                ctx.LineTo(new Point(x + 1, y + 13), true, false);
                ctx.LineTo(new Point(x + 1, y + 4), true, false);
            }
            tagGeom.Freeze();
            dc.DrawGeometry(TagBrush, TagBorderPen, tagGeom);

            // Eyelet
            dc.DrawEllipse(TagHoleBrush, null, new Point(x + 6.5, y + 3.5), 1.25, 1.25);

            // Warning stripes
            dc.DrawLine(TagStripePen, new Point(x + 3, y + 8), new Point(x + 10, y + 8));
            dc.DrawLine(TagStripePen, new Point(x + 3, y + 11), new Point(x + 10, y + 11));
        }

        private static void DrawInterlockBadge(DrawingContext dc, double x, double y)
        {
            dc.DrawEllipse(InterlockBrush, InterlockBorderPen, new Point(x + 7, y + 7), 6, 6);
            dc.DrawEllipse(null, InterlockLinkPen, new Point(x + 5.5, y + 7), 2, 3);
            dc.DrawEllipse(null, InterlockLinkPen, new Point(x + 8.5, y + 7), 2, 3);
        }

        private static void DrawMaintenanceBadge(DrawingContext dc, double x, double y)
        {
            dc.DrawEllipse(MaintBrush, MaintBorderPen, new Point(x + 7, y + 7), 6, 6);
            dc.DrawLine(MaintWrenchPen, new Point(x + 4, y + 10), new Point(x + 9, y + 4));
        }

        private static void DrawFaultBadge(DrawingContext dc, double x, double y)
        {
            bool isBlinkOn = ZeroAnimationClock.BlinkFast;
            Brush triBrush = isBlinkOn ? FaultLightBrush : FaultDarkBrush;

            StreamGeometry triGeom = new StreamGeometry();
            using (var ctx = triGeom.Open())
            {
                ctx.BeginFigure(new Point(x + 7, y + 1), true, true);
                ctx.LineTo(new Point(x + 13, y + 13), true, false);
                ctx.LineTo(new Point(x + 1, y + 13), true, false);
            }
            triGeom.Freeze();
            dc.DrawGeometry(triBrush, FaultBorderPen, triGeom);

            dc.DrawRectangle(WhiteBrush, null, new Rect(x + 6.3, y + 5, 1.4, 4));
            dc.DrawEllipse(WhiteBrush, null, new Point(x + 7, y + 11), 0.8, 0.8);
        }

        private static void DrawManualOverrideBadge(DrawingContext dc, double x, double y, double dpi)
        {
            dc.DrawEllipse(ManualBrush, ManualBorderPen, new Point(x + 7, y + 7), 6, 6);
            var mText = new FormattedText("M", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, BadgeFont, 7.5, WhiteBrush, dpi);
            dc.DrawText(mText, new Point(x + 3.2, y + 1.8));
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="DeviceBadgeRenderer"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroDeviceBadgeRenderer is deprecated. Please use DeviceBadgeRenderer instead.")]
    public static class ZeroDeviceBadgeRenderer
    {
        public static void DrawBadges(
            DrawingContext dc,
            Rect bounds,
            DeviceStatusFlags flags,
            double dpi = 1.0)
        {
            DeviceBadgeRenderer.DrawBadges(dc, bounds, flags, dpi);
        }
    }
}
