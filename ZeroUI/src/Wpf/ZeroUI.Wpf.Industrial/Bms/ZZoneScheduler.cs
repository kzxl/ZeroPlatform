using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Bms;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Bms
{
    /// <summary>
    /// Multi-Zone 7-Day 24-Hour Time Schedule Matrix Control for WPF.
    /// Visualizes weekly occupancy calendar windows, heating/cooling setpoint bands,
    /// and holiday overrides with interactive block inspection.
    /// </summary>
    public class ZZoneScheduler : ZeroWpfVisualBase
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
            MouseMove += OnWpfMouseMove;
            MouseLeave += OnWpfMouseLeave;
        }

        #region Properties

        public ZoneSchedulerEngine Engine => _engine;

        public int SelectedZoneIndex
        {
            get => _selectedZoneIndex;
            set
            {
                if (_selectedZoneIndex != value && value >= 0 && value < _engine.Schedules.Count)
                {
                    _selectedZoneIndex = value;
                    InvalidateVisual();
                }
            }
        }

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



        private void OnWpfMouseMove(object sender, MouseEventArgs e)
        {
            _lastMousePos = e.GetPosition(this);
            var hit = HitTestBlock(_lastMousePos);
            if (hit != _hoveredBlock)
            {
                _hoveredBlock = hit;
                InvalidateVisual();
            }
        }

        private void OnWpfMouseLeave(object sender, MouseEventArgs e)
        {
            if (_hoveredBlock != null)
            {
                _hoveredBlock = null;
                InvalidateVisual();
            }
        }

        private ScheduleTimeBlock? HitTestBlock(Point pt)
        {
            var schedule = CurrentSchedule;
            if (schedule == null) return null;

            double gridLeft = 50;
            double gridTop = 44;
            double gridWidth = ActualWidth - gridLeft - 16;
            double rowHeight = (ActualHeight - gridTop - 36) / 7.0;

            if (gridWidth <= 0 || rowHeight <= 0) return null;

            for (int r = 0; r < 7; r++)
            {
                DayOfWeek day = DayMap[r];
                double rowTop = gridTop + r * rowHeight;
                double rowBottom = rowTop + rowHeight;

                if (pt.Y >= rowTop && pt.Y < rowBottom)
                {
                    for (int i = 0; i < schedule.Blocks.Count; i++)
                    {
                        var b = schedule.Blocks[i];
                        if (b.Day == day)
                        {
                            double xStart = gridLeft + (b.StartTime.TotalHours / 24.0 * gridWidth);
                            double xEnd = gridLeft + (b.EndTime.TotalHours / 24.0 * gridWidth);

                            if (pt.X >= xStart && pt.X <= xEnd)
                                return b;
                        }
                    }
                }
            }

            return null;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 120 || h < 80)
                return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Canvas Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, new Rect(0, 0, w, h));

            var schedule = CurrentSchedule;
            string title = schedule != null ? $"{schedule.ZoneId} — {schedule.ZoneName}" : "Zone Schedule";

            var titleFt = CreateFormattedText(title, ZeroWpfTheme.BoldTypeface, 11.0, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(titleFt, new Point(14, 10));

            double gridLeft = 50;
            double gridTop = 40;
            double gridWidth = w - gridLeft - 16;
            double gridHeight = h - gridTop - 32;

            if (gridWidth < 150 || gridHeight < 70)
                return;

            double rowH = gridHeight / 7.0;

            // Hours Header
            DrawHoursHeader(dc, gridLeft, gridTop - 16, gridWidth, dpi);

            // 7 Days Grid
            DrawGrid(dc, gridLeft, gridTop, gridWidth, rowH, dpi);

            // Blocks
            if (schedule != null)
            {
                DrawScheduleBlocks(dc, schedule, gridLeft, gridTop, gridWidth, rowH, dpi);
            }

            // Legend
            DrawLegend(dc, gridLeft, h - 22, dpi);

            // Tooltip
            if (_hoveredBlock != null)
            {
                DrawBlockTooltip(dc, _hoveredBlock, _lastMousePos, w, h, dpi);
            }
        }

        private void DrawHoursHeader(DrawingContext dc, double left, double top, double width, double dpi)
        {
            for (int h = 0; h <= 24; h += 3)
            {
                double x = left + (h / 24.0) * width;
                var ft = CreateFormattedText($"{h:00}:00", ZeroWpfTheme.RegularTypeface, 8.0, ZeroWpfTheme.TextMuted, dpi);
                dc.DrawText(ft, new Point(x - ft.Width / 2, top));
            }
        }

        private void DrawGrid(DrawingContext dc, double left, double top, double width, double rowH, double dpi)
        {
            Pen rowPen = ZeroWpfTheme.BorderPen;
            Pen hourPen = new Pen(ZeroWpfTheme.BorderSubtle, 1.0) { DashStyle = DashStyles.Dot };
            hourPen.Freeze();

            for (int r = 0; r < 7; r++)
            {
                double y = top + r * rowH;

                var dayFt = CreateFormattedText(DayNames[r], ZeroWpfTheme.BoldTypeface, 9.0, ZeroWpfTheme.TextPrimary, dpi);
                dc.DrawText(dayFt, new Point(14, y + (rowH - dayFt.Height) / 2));

                dc.DrawRectangle(null, rowPen, new Rect(left, y, width, rowH - 2));

                for (int h = 3; h < 24; h += 3)
                {
                    double x = left + (h / 24.0) * width;
                    dc.DrawLine(hourPen, new Point(x, y), new Point(x, y + rowH - 2));
                }
            }
        }

        private void DrawScheduleBlocks(DrawingContext dc, ZoneSchedule schedule, double left, double top, double width, double rowH, double dpi)
        {
            for (int r = 0; r < 7; r++)
            {
                DayOfWeek day = DayMap[r];
                double y = top + r * rowH + 2;
                double h = rowH - 6;

                for (int i = 0; i < schedule.Blocks.Count; i++)
                {
                    var b = schedule.Blocks[i];
                    if (b.Day == day)
                    {
                        double xStart = left + (b.StartTime.TotalHours / 24.0 * width);
                        double xEnd = left + (b.EndTime.TotalHours / 24.0 * width);
                        double blockW = Math.Max(3, xEnd - xStart);

                        Brush brush = b.Mode switch
                        {
                            ZoneOccupancyMode.Occupied => Brushes.MediumSeaGreen,
                            ZoneOccupancyMode.Standby => Brushes.Orange,
                            ZoneOccupancyMode.HolidayOverride => Brushes.MediumPurple,
                            _ => Brushes.SlateGray
                        };

                        Rect rect = new Rect(xStart, y, blockW, h);
                        dc.DrawRectangle(brush, b == _hoveredBlock ? new Pen(Brushes.White, 1.5) : null, rect);

                        if (blockW > 55)
                        {
                            var ft = CreateFormattedText($"{b.CoolingSetpointC:F0}°C / {b.HeatingSetpointC:F0}°C", ZeroWpfTheme.BoldTypeface, 8.0, Brushes.White, dpi);
                            dc.DrawText(ft, new Point(xStart + (blockW - ft.Width) / 2, y + (h - ft.Height) / 2));
                        }
                    }
                }
            }
        }

        private void DrawLegend(DrawingContext dc, double left, double y, double dpi)
        {
            double curX = left;
            DrawLegendItem(dc, ref curX, y, Brushes.MediumSeaGreen, "Occupied (Comfort)", dpi);
            DrawLegendItem(dc, ref curX, y, Brushes.Orange, "Standby", dpi);
            DrawLegendItem(dc, ref curX, y, Brushes.SlateGray, "Unoccupied", dpi);
            DrawLegendItem(dc, ref curX, y, Brushes.MediumPurple, "Holiday Override", dpi);
        }

        private void DrawLegendItem(DrawingContext dc, ref double curX, double y, Brush brush, string label, double dpi)
        {
            dc.DrawRectangle(brush, null, new Rect(curX, y + 2, 10, 10));
            curX += 14;
            var ft = CreateFormattedText(label, ZeroWpfTheme.RegularTypeface, 8.0, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(ft, new Point(curX, y));
            curX += ft.Width + 18;
        }

        private void DrawBlockTooltip(DrawingContext dc, ScheduleTimeBlock block, Point mouse, double w, double h, double dpi)
        {
            string line1 = $"{block.Day} {block.StartTime:hh\\:mm} – {block.EndTime:hh\\:mm}";
            string line2 = $"Mode: {block.Mode}";
            string line3 = $"Cool: {block.CoolingSetpointC:F1}°C | Heat: {block.HeatingSetpointC:F1}°C";

            double tipW = 180;
            double tipH = 54;
            double tipX = Math.Min(w - tipW - 8, mouse.X + 12);
            double tipY = Math.Min(h - tipH - 8, mouse.Y + 12);

            Rect tipRect = new Rect(tipX, tipY, tipW, tipH);
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, new Pen(Brushes.SkyBlue, 1.0), tipRect);

            var ft1 = CreateFormattedText(line1, ZeroWpfTheme.BoldTypeface, 8.5, Brushes.White, dpi);
            dc.DrawText(ft1, new Point(tipX + 6, tipY + 4));

            var ft2 = CreateFormattedText(line2, ZeroWpfTheme.BoldTypeface, 8.0, Brushes.SkyBlue, dpi);
            dc.DrawText(ft2, new Point(tipX + 6, tipY + 20));

            var ft3 = CreateFormattedText(line3, ZeroWpfTheme.RegularTypeface, 8.0, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(ft3, new Point(tipX + 6, tipY + 36));
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZZoneScheduler"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZoneScheduler is deprecated and will be removed in 5 release cycles. Please migrate to ZZoneScheduler instead.")]
    public class ZoneScheduler : ZZoneScheduler { }

    /// <summary>
    /// Legacy alias for <see cref="ZZoneScheduler"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroZoneScheduler is deprecated and will be removed in 5 release cycles. Please migrate to ZZoneScheduler instead.")]
    public class ZeroZoneScheduler : ZZoneScheduler { }

    #endregion

}
