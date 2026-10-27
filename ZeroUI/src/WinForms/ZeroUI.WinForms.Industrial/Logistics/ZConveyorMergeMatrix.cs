using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Logistics;
using ZeroUI.Core.Rendering;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Logistics
{
    /// <summary>
    /// High-speed parcel sorting line and conveyor merge matrix visualizer.
    /// Renders animated conveyor belts, infrared photo-eye beam sensors, dynamic parcels,
    /// high-speed divert chutes, and real-time sorting throughput (PPH).
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Logistics & Warehouse")]
    [Description("High-speed parcel sorting conveyor merge visualizer with photo-eyes, divert chutes, and PPH meter")]
    public class ZConveyorMergeMatrix : VisualControlBase
    {
        private readonly ConveyorMergeEngine _engine = new ConveyorMergeEngine();
        private string _lineName = "Main Infeed & Merge Line 1";

        protected override bool AutoAnimate => true;

        protected override void OnAnimationTick(double delta, long frame)
        {
            _engine.AdvanceParcels(delta);
        }

        public ZConveyorMergeMatrix()
        {
            Size = new Size(760, 300);
        }

        #region Public Properties

        [Category("Conveyor")]
        [Description("Access to the underlying parcel sorting and conveyor merge engine")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ConveyorMergeEngine Engine => _engine;

        [Category("Conveyor")]
        [DefaultValue("Main Infeed & Merge Line 1")]
        public string LineName
        {
            get => _lineName;
            set
            {
                _lineName = value;
                Invalidate();
            }
        }

        [Category("Conveyor")]
        [DefaultValue(90.0)]
        [Description("Main conveyor belt velocity in meters per minute")]
        public double MainSpeedMpm
        {
            get => _engine.MainSpeedMpm;
            set
            {
                _engine.MainSpeedMpm = Math.Max(0, value);
                Invalidate();
            }
        }

        #endregion

        protected override void OnDrawVisual(Graphics g, Rectangle bounds, ZeroThemePalette palette)
        {
            // Top Header: Title, Velocity, Throughput PPH
            int headerH = DrawHeader(g, bounds, palette);

            // Main Conveyor Belt Area
            int beltLeft = bounds.X + 24;
            int beltTop = bounds.Y + headerH + 24;
            int beltWidth = bounds.Width - 48;
            int beltHeight = 54;

            if (beltWidth < 180)
                return;

            Rectangle mainBeltRect = new Rectangle(beltLeft, beltTop, beltWidth, beltHeight);

            // 1. Draw Divert Chutes (Branching upwards from belt)
            DrawDivertChutes(g, mainBeltRect, palette);

            // 2. Draw Merge Feeder (Branching from bottom left into belt)
            DrawMergeFeeder(g, mainBeltRect, palette);

            // 3. Draw Main Conveyor Belt Bed
            DrawConveyorBelt(g, mainBeltRect, palette);

            // 4. Draw Photo-eye Optical Sensors
            DrawPhotoEyeSensors(g, mainBeltRect, palette);

            // 5. Draw Traveling Parcels
            DrawParcels(g, mainBeltRect, palette);
        }

        private int DrawHeader(Graphics g, Rectangle bounds, ZeroThemePalette theme)
        {
            using (var fontTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var fontSmall = new Font("Segoe UI", 8.5f))
            using (var fontBold = new Font("Segoe UI", 9f, FontStyle.Bold))
            {
                string pphStr = $"{_engine.HourlyThroughputPph:N0} PPH";
                string speedStr = $"Speed: {_engine.MainSpeedMpm:F0} m/min | Parcels: {_engine.Parcels.Count}";
                int badgeW = 96;
                int badgeH = 24;

                if (bounds.Width >= 540)
                {
                    TextRenderer.DrawText(g, _lineName, fontTitle, new Point(bounds.X + 20, bounds.Y + 14), theme.TextPrimary);

                    int hudX = bounds.Right - 360;
                    TextRenderer.DrawText(g, speedStr, fontSmall, new Point(hudX, bounds.Y + 16), theme.TextSecondary);

                    Rectangle pphBadge = new Rectangle(bounds.Right - badgeW - 20, bounds.Y + 12, badgeW, badgeH);
                    using (var badgeBrush = new SolidBrush(Color.FromArgb(30, 34, 197, 94)))
                    using (var badgePen = new Pen(Color.FromArgb(34, 197, 94), 1f))
                    {
                        g.FillRectangle(badgeBrush, pphBadge);
                        g.DrawRectangle(badgePen, pphBadge);
                    }
                    TextRenderer.DrawText(g, pphStr, fontBold, pphBadge, Color.FromArgb(34, 197, 94), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    return 48;
                }
                else
                {
                    Rectangle titleRect = new Rectangle(bounds.X + 20, bounds.Y + 8, bounds.Width - badgeW - 40, 22);
                    TextRenderer.DrawText(g, _lineName, fontTitle, titleRect, theme.TextPrimary,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    Rectangle pphBadge = new Rectangle(bounds.Right - badgeW - 16, bounds.Y + 8, badgeW, badgeH);
                    using (var badgeBrush = new SolidBrush(Color.FromArgb(30, 34, 197, 94)))
                    using (var badgePen = new Pen(Color.FromArgb(34, 197, 94), 1f))
                    {
                        g.FillRectangle(badgeBrush, pphBadge);
                        g.DrawRectangle(badgePen, pphBadge);
                    }
                    TextRenderer.DrawText(g, pphStr, fontBold, pphBadge, Color.FromArgb(34, 197, 94), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                    Rectangle speedRect = new Rectangle(bounds.X + 20, bounds.Y + 32, bounds.Width - 40, 18);
                    TextRenderer.DrawText(g, speedStr, fontSmall, speedRect, theme.TextSecondary,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    return 54;
                }
            }
        }

        private void DrawConveyorBelt(Graphics g, Rectangle rect, ZeroThemePalette theme)
        {
            using (var beltBrush = new SolidBrush(Color.FromArgb(30, 41, 59)))
            using (var framePen = new Pen(Color.FromArgb(100, 116, 139), 2f))
            {
                g.FillRectangle(beltBrush, rect);
                g.DrawRectangle(framePen, rect);

                // Animated belt rollers / slats
                float phase = ZeroAnimationClock.FluidPhase;
                int slatSpacing = 24;
                float offset = phase * slatSpacing;

                using (var slatPen = new Pen(Color.FromArgb(51, 65, 85), 1.5f))
                {
                    for (float x = rect.Left + offset; x < rect.Right; x += slatSpacing)
                    {
                        g.DrawLine(slatPen, x, rect.Top + 2, x, rect.Bottom - 2);
                    }
                }
            }
        }

        private void DrawDivertChutes(Graphics g, Rectangle belt, ZeroThemePalette theme)
        {
            using (var font = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var chutePen = new Pen(Color.FromArgb(71, 85, 105), 1.5f))
            {
                for (int i = 0; i < _engine.Chutes.Count; i++)
                {
                    var ch = _engine.Chutes[i];
                    float cx = belt.Left + (float)(ch.PositionRatio * belt.Width);

                    // Chute spur geometry angled up-right
                    PointF[] chutePoly = new[]
                    {
                        new PointF(cx - 16, belt.Top),
                        new PointF(cx + 16, belt.Top),
                        new PointF(cx + 42, belt.Top - 45),
                        new PointF(cx + 10, belt.Top - 45)
                    };

                    Color chuteColor = ch.IsDiverting ? Color.FromArgb(245, 158, 11) : theme.Surface;
                    using (var fillBrush = new SolidBrush(chuteColor))
                    {
                        g.FillPolygon(fillBrush, chutePoly);
                        g.DrawPolygon(chutePen, chutePoly);
                    }

                    // Divert count label
                    string countStr = $"Divert {ch.ChuteId}: {ch.DivertedCount}";
                    TextRenderer.DrawText(g, countStr, font, new Point((int)cx - 14, belt.Top - 60), theme.TextSecondary);
                }
            }
        }

        private void DrawMergeFeeder(Graphics g, Rectangle belt, ZeroThemePalette theme)
        {
            // Feeder merging from bottom left into belt at ~30%
            float mergeX = belt.Left + belt.Width * 0.30f;
            PointF[] mergePoly = new[]
            {
                new PointF(belt.Left + 20, belt.Bottom + 50),
                new PointF(belt.Left + 60, belt.Bottom + 50),
                new PointF(mergeX, belt.Bottom),
                new PointF(mergeX - 30, belt.Bottom)
            };

            using (var mergeBrush = new SolidBrush(Color.FromArgb(25, 41, 59)))
            using (var mergePen = new Pen(Color.FromArgb(100, 116, 139), 1.5f))
            {
                g.FillPolygon(mergeBrush, mergePoly);
                g.DrawPolygon(mergePen, mergePoly);
            }

            using (var font = new Font("Segoe UI", 7.5f))
            {
                TextRenderer.DrawText(g, "Feeder Infeed", font, new Point(belt.Left + 24, belt.Bottom + 54), theme.TextSecondary);
            }
        }

        private void DrawPhotoEyeSensors(Graphics g, Rectangle belt, ZeroThemePalette theme)
        {
            using (var font = new Font("Segoe UI", 7f))
            {
                for (int i = 0; i < _engine.Sensors.Count; i++)
                {
                    var pe = _engine.Sensors[i];
                    float px = belt.Left + (float)(pe.PositionRatio * belt.Width);

                    Color beamColor = pe.IsJamAlert
                        ? (ZeroAnimationClock.BlinkFast ? Color.FromArgb(239, 68, 68) : Color.FromArgb(127, 29, 29))
                        : (pe.IsBlocked ? Color.FromArgb(239, 68, 68) : Color.FromArgb(34, 197, 94));

                    // Sensor brackets (top and bottom)
                    using (var bracketBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
                    {
                        g.FillRectangle(bracketBrush, px - 3, belt.Top - 8, 6, 8);
                        g.FillRectangle(bracketBrush, px - 3, belt.Bottom, 6, 8);
                    }

                    // Optical beam
                    using (var beamPen = new Pen(beamColor, 2f))
                    {
                        g.DrawLine(beamPen, px, belt.Top, px, belt.Bottom);
                    }

                    // Sensor tag
                    TextRenderer.DrawText(g, pe.Name, font, new Point((int)px - 18, belt.Bottom + 12), beamColor);
                }
            }
        }

        private void DrawParcels(Graphics g, Rectangle belt, ZeroThemePalette theme)
        {
            using (var font = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            {
                for (int i = 0; i < _engine.Parcels.Count; i++)
                {
                    var p = _engine.Parcels[i];
                    float px = belt.Left + (float)(p.PositionRatio * belt.Width);
                    float py = belt.Top + 8;
                    float pw = 36f;
                    float ph = belt.Height - 16;

                    RectangleF pRect = new RectangleF(px - pw / 2, py, pw, ph);

                    Color cartonColor = p.IsDiverted ? Color.FromArgb(245, 158, 11) : Color.FromArgb(56, 189, 248);

                    using (var cartonBrush = new SolidBrush(cartonColor))
                    using (var cartonPen = new Pen(Color.FromArgb(2, 132, 199), 1f))
                    {
                        g.FillRectangle(cartonBrush, pRect);
                        g.DrawRectangle(cartonPen, pRect.X, pRect.Y, pRect.Width, pRect.Height);
                    }

                    // Carton destination badge
                    string code = $"C{p.DestinationChuteId}";
                    TextRenderer.DrawText(g, code, font, new Point((int)px - 10, (int)py + 10), Color.White);
                }
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZConveyorMergeMatrix"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ConveyorMergeMatrix is deprecated and will be removed in 5 release cycles. Please migrate to ZConveyorMergeMatrix instead.")]
    [ToolboxItem(false)]
    public class ConveyorMergeMatrix : ZConveyorMergeMatrix
    {
    }

    #endregion
}
