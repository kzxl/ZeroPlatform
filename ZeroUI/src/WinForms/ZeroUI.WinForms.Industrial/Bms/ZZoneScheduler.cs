using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Bms;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Bms
{
    /// <summary>
    /// Multi-Zone 7-Day 24-Hour Time Schedule Matrix Control.
    /// Visualizes weekly occupancy calendar windows, heating/cooling comfort setpoint bands,
    /// and holiday overrides with interactive block inspection.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - BMS & HVAC")]
    [Description("Multi-Zone 7-day 24-hour occupancy calendar schedule matrix")]
    public class ZZoneScheduler : VisualControlBase
    {
        private readonly ZoneSchedulerEngine _engine = new ZoneSchedulerEngine();
        private int _selectedZoneIndex = 0;
        private ScheduleTimeBlock? _hoveredBlock;
        private Point _lastMousePos;

        private static readonly string[] DayNames = new[]
        {
            "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"
        };

        private static readonly DayOfWeek[] DayMap = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
        };

        public ZZoneScheduler()
        {
            Size = new Size(740, 300);
        }

        #region Public Properties

        [Category("BMS")]
        [Description("Access to the underlying pure zone schedule computation engine")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ZoneSchedulerEngine Engine => _engine;

        [Category("BMS")]
        [DefaultValue(0)]
        public int SelectedZoneIndex
        {
            get => _selectedZoneIndex;
            set
            {
                if (_selectedZoneIndex != value && value >= 0 && value < _engine.Schedules.Count)
                {
                    _selectedZoneIndex = value;
                    Invalidate();
                }
            }
        }

        [Browsable(false)]
        public ZoneSchedule? CurrentSchedule
        {
            get
            {
                if (_selectedZoneIndex >= 0 && _selectedZoneIndex < _engine.Schedules.Count)
                    return _engine.Schedules[_selectedZoneIndex];
                return null;
            }
        }

        #endregion

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _lastMousePos = e.Location;

            var hit = HitTestBlock(e.Location);
            if (hit != _hoveredBlock)
            {
                _hoveredBlock = hit;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredBlock != null)
            {
                _hoveredBlock = null;
                Invalidate();
            }
        }

        private ScheduleTimeBlock? HitTestBlock(Point pt)
        {
            var schedule = CurrentSchedule;
            if (schedule == null) return null;

            int gridLeft = 60;
            int gridTop = 50;
            int gridWidth = Width - gridLeft - 20;
            int rowHeight = (Height - gridTop - 40) / 7;

            if (gridWidth <= 0 || rowHeight <= 0) return null;

            for (int r = 0; r < 7; r++)
            {
                DayOfWeek day = DayMap[r];
                int rowTop = gridTop + r * rowHeight;
                int rowBottom = rowTop + rowHeight;

                if (pt.Y >= rowTop && pt.Y < rowBottom)
                {
                    for (int i = 0; i < schedule.Blocks.Count; i++)
                    {
                        var b = schedule.Blocks[i];
                        if (b.Day == day)
                        {
                            float xStart = gridLeft + (float)(b.StartTime.TotalHours / 24.0 * gridWidth);
                            float xEnd = gridLeft + (float)(b.EndTime.TotalHours / 24.0 * gridWidth);

                            if (pt.X >= xStart && pt.X <= xEnd)
                                return b;
                        }
                    }
                }
            }

            return null;
        }

        protected override void OnDrawVisual(Graphics g, Rectangle bounds, ZeroThemePalette palette)
        {
            var schedule = CurrentSchedule;
            string zoneTitle = schedule != null ? $"{schedule.ZoneId} — {schedule.ZoneName}" : "Zone Schedule";

            // Title Header
            using (var fontTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var fontSmall = new Font("Segoe UI", 8f))
            {
                Rectangle titleRect = new Rectangle(bounds.X + 16, bounds.Y + 10, bounds.Width - 32, 22);
                TextRenderer.DrawText(g, zoneTitle, fontTitle, titleRect, palette.TextPrimary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            int gridLeft = bounds.X + 60;
            int gridTop = bounds.Y + 50;
            int gridWidth = bounds.Width - 60 - 20;
            int gridHeight = bounds.Height - 50 - 40;

            if (gridWidth < 200 || gridHeight < 100)
                return;

            int rowHeight = gridHeight / 7;

            // Hours Header (00:00 to 24:00)
            DrawHoursHeader(g, gridLeft, gridTop - 18, gridWidth, palette);

            // 7-Day Rows Background and Gridlines
            DrawGrid(g, gridLeft, gridTop, gridWidth, rowHeight, palette);

            // Schedule Blocks
            if (schedule != null)
            {
                DrawScheduleBlocks(g, schedule, gridLeft, gridTop, gridWidth, rowHeight, palette);
            }

            // Legend Footer
            DrawLegend(g, gridLeft, bounds.Bottom - 26, palette);

            // Tooltip if hovering a block
            if (_hoveredBlock != null)
            {
                DrawBlockTooltip(g, _hoveredBlock, _lastMousePos, bounds, palette);
            }
        }

        private void DrawHoursHeader(Graphics g, int left, int top, int width, ZeroThemePalette theme)
        {
            using (var font = new Font("Segoe UI", 7.5f))
            {
                int stepHours = width < 320 ? 6 : width < 480 ? 4 : 2;
                for (int h = 0; h <= 24; h += stepHours)
                {
                    float x = left + (h / 24.0f) * width;
                    string timeStr = $"{h:00}:00";
                    var sz = TextRenderer.MeasureText(g, timeStr, font);
                    TextRenderer.DrawText(g, timeStr, font, new Point((int)x - sz.Width / 2, top), theme.TextSecondary);
                }
            }
        }

        private void DrawGrid(Graphics g, int left, int top, int width, int rowH, ZeroThemePalette theme)
        {
            using (var fontDay = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var linePen = new Pen(theme.Border, 1f))
            using (var hourPen = new Pen(Color.FromArgb(20, theme.TextSecondary), 1f) { DashStyle = DashStyle.Dot })
            {
                for (int r = 0; r < 7; r++)
                {
                    int y = top + r * rowH;

                    // Day label
                    TextRenderer.DrawText(g, DayNames[r], fontDay, new Point(16, y + (rowH - 16) / 2), theme.TextPrimary);

                    // Row outline
                    g.DrawRectangle(linePen, left, y, width, rowH - 2);

                    // Vertical 2-hour divider marks
                    for (int h = 2; h < 24; h += 2)
                    {
                        float x = left + (h / 24.0f) * width;
                        g.DrawLine(hourPen, x, y, x, y + rowH - 2);
                    }
                }
            }
        }

        private void DrawScheduleBlocks(Graphics g, ZoneSchedule schedule, int left, int top, int width, int rowH, ZeroThemePalette theme)
        {
            for (int r = 0; r < 7; r++)
            {
                DayOfWeek day = DayMap[r];
                int y = top + r * rowH + 2;
                int h = rowH - 6;

                for (int i = 0; i < schedule.Blocks.Count; i++)
                {
                    var b = schedule.Blocks[i];
                    if (b.Day == day)
                    {
                        float xStart = left + (float)(b.StartTime.TotalHours / 24.0 * width);
                        float xEnd = left + (float)(b.EndTime.TotalHours / 24.0 * width);
                        float blockW = Math.Max(4, xEnd - xStart);

                        RectangleF blockRect = new RectangleF(xStart, y, blockW, h);

                        Color blockColor = b.Mode switch
                        {
                            ZoneOccupancyMode.Occupied => Color.FromArgb(34, 197, 94), // Emerald
                            ZoneOccupancyMode.Standby => Color.FromArgb(245, 158, 11), // Amber
                            ZoneOccupancyMode.HolidayOverride => Color.FromArgb(168, 85, 247), // Purple
                            _ => Color.FromArgb(100, 116, 139)
                        };

                        using (var brush = new SolidBrush(Color.FromArgb(180, blockColor)))
                        using (var pen = new Pen(blockColor, b == _hoveredBlock ? 2f : 1f))
                        {
                            g.FillRectangle(brush, blockRect);
                            g.DrawRectangle(pen, blockRect.X, blockRect.Y, blockRect.Width, blockRect.Height);
                        }

                        // Label inside block if wide enough
                        if (blockW > 50)
                        {
                            using (var font = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                            {
                                string caption = $"{b.CoolingSetpointC:F0}°C / {b.HeatingSetpointC:F0}°C";
                                TextRenderer.DrawText(g, caption, font,
                                    new Rectangle((int)xStart, y, (int)blockW, h),
                                    Color.White,
                                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                            }
                        }
                    }
                }
            }
        }

        private void DrawLegend(Graphics g, int left, int y, ZeroThemePalette theme)
        {
            using (var font = new Font("Segoe UI", 8f))
            {
                int curX = left;
                DrawLegendItem(g, ref curX, y, Color.FromArgb(34, 197, 94), "Occupied (Comfort)", font, theme);
                DrawLegendItem(g, ref curX, y, Color.FromArgb(245, 158, 11), "Standby", font, theme);
                DrawLegendItem(g, ref curX, y, Color.FromArgb(100, 116, 139), "Unoccupied", font, theme);
                DrawLegendItem(g, ref curX, y, Color.FromArgb(168, 85, 247), "Holiday Override", font, theme);
            }
        }

        private void DrawLegendItem(Graphics g, ref int curX, int y, Color color, string text, Font font, ZeroThemePalette theme)
        {
            using (var brush = new SolidBrush(color))
            {
                g.FillRectangle(brush, curX, y + 2, 12, 10);
            }
            curX += 16;
            var sz = TextRenderer.MeasureText(g, text, font);
            TextRenderer.DrawText(g, text, font, new Point(curX, y), theme.TextSecondary);
            curX += sz.Width + 24;
        }

        private void DrawBlockTooltip(Graphics g, ScheduleTimeBlock block, Point mouse, Rectangle bounds, ZeroThemePalette theme)
        {
            using (var fontBold = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var font = new Font("Segoe UI", 8f))
            {
                string line1 = $"{block.Day} {block.StartTime:hh\\:mm} – {block.EndTime:hh\\:mm}";
                string line2 = $"Mode: {block.Mode.ToString().ToUpperInvariant()}";
                string line3 = $"Cool: {block.CoolingSetpointC:F1}°C | Heat: {block.HeatingSetpointC:F1}°C";

                int tipW = 190;
                int tipH = 58;
                int tipX = Math.Min(bounds.Right - tipW - 8, mouse.X + 12);
                int tipY = Math.Min(bounds.Bottom - tipH - 8, mouse.Y + 12);

                Rectangle tipRect = new Rectangle(tipX, tipY, tipW, tipH);
                using (var tipBg = new SolidBrush(Color.FromArgb(240, 17, 19, 31)))
                using (var tipBorder = new Pen(Color.FromArgb(56, 189, 248), 1f))
                {
                    g.FillRectangle(tipBg, tipRect);
                    g.DrawRectangle(tipBorder, tipRect);
                }

                TextRenderer.DrawText(g, line1, fontBold, new Point(tipX + 6, tipY + 4), Color.White);
                TextRenderer.DrawText(g, line2, font, new Point(tipX + 6, tipY + 22), Color.FromArgb(56, 189, 248));
                TextRenderer.DrawText(g, line3, font, new Point(tipX + 6, tipY + 38), Color.FromArgb(203, 213, 225));
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZZoneScheduler"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZoneScheduler is deprecated and will be removed in 5 release cycles. Please migrate to ZZoneScheduler instead.")]
    [ToolboxItem(false)]
    public class ZoneScheduler : ZZoneScheduler
    {
    }

    #endregion
}
