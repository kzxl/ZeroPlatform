using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Data;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// High-performance industrial Gantt timeline schedule control for WPF.
    /// Eliminates WPF Visual Tree overhead by rendering task bars, dependencies,
    /// and milestones directly via single-visual DrawingContext.
    /// </summary>
    public class ZGanttControl : FrameworkElement
    {
        private readonly ObservableCollection<GanttTaskItem> _tasks = new ObservableCollection<GanttTaskItem>();
        private DateTime _projectStart = DateTime.Today.AddDays(-2);
        private DateTime _projectEnd = DateTime.Today.AddDays(28);

        private int _headerHeight = 44;
        private int _rowHeight = 32;
        private int _taskListWidth = 260;
        private double _pixelsPerDay = 36.0;

        private int _scrollX = 0;
        private int _scrollY = 0;
        private int _selectedTaskIndex = -1;
        private int _hoveredTaskIndex = -1;

        public ObservableCollection<GanttTaskItem> Tasks => _tasks;

        public DateTime ProjectStart
        {
            get => _projectStart;
            set { _projectStart = value; InvalidateVisual(); }
        }

        public DateTime ProjectEnd
        {
            get => _projectEnd;
            set { _projectEnd = value; InvalidateVisual(); }
        }

        public int TaskListWidth
        {
            get => _taskListWidth;
            set { _taskListWidth = Math.Max(120, value); InvalidateVisual(); }
        }

        public double PixelsPerDay
        {
            get => _pixelsPerDay;
            set { _pixelsPerDay = Math.Max(10.0, Math.Min(120.0, value)); InvalidateVisual(); }
        }

        public ZGanttControl()
        {
            ClipToBounds = true;
            Focusable = true;
            _tasks.CollectionChanged += (s, e) => InvalidateVisual();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

#if NETFRAMEWORK
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush);
        }
#else
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush, pixelsPerDip);
        }
#endif

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 0 || height <= 0) return;

#if NETFRAMEWORK
            double dpi = 1.0;
#else
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
#endif

            // Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, new Rect(0, 0, width, height));

            int totalTasks = _tasks.Count;
            int clientH = (int)Math.Max(0, height - _headerHeight);
            int startRow = Math.Max(0, _scrollY / _rowHeight);
            int visibleRowCount = (clientH / _rowHeight) + 2;
            int endRow = Math.Min(totalTasks - 1, startRow + visibleRowCount);

            double timelineX = _taskListWidth;
            double timelineW = Math.Max(0, width - timelineX);

            // 1. Render Timeline Grid Lines & Today Marker
            int totalDays = Math.Max(1, (int)(_projectEnd - _projectStart).TotalDays);

            for (int d = 0; d <= totalDays; d++)
            {
                DateTime day = _projectStart.AddDays(d);
                double x = timelineX + (d * _pixelsPerDay) - _scrollX;
                if (x < timelineX || x > width) continue;

                // Weekend shading
                if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday)
                {
                    dc.DrawRectangle(ZeroWpfTheme.BgInput, null, new Rect(x, _headerHeight, _pixelsPerDay, clientH));
                }

                dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(x, _headerHeight), new Point(x, height));
            }

            // Today vertical marker line
            double todayDays = (DateTime.Today - _projectStart).TotalDays;
            double todayX = timelineX + (todayDays * _pixelsPerDay) - _scrollX;
            if (todayX >= timelineX && todayX <= width)
            {
                var todayPen = new Pen(ZeroWpfTheme.DangerAccent, 1.8);
                todayPen.DashStyle = DashStyles.Dash;
                todayPen.Freeze();
                dc.DrawLine(todayPen, new Point(todayX, _headerHeight), new Point(todayX, height));
            }

            // 2. Render Row Backgrounds and Task Bars
            var taskBarRects = new Dictionary<int, Rect>();
            double currentY = _headerHeight + (startRow * _rowHeight) - _scrollY;

            for (int r = startRow; r <= endRow && r < totalTasks; r++)
            {
                if (currentY >= height) break;

                var task = _tasks[r];
                bool isSelected = (r == _selectedTaskIndex);
                bool isHovered = (r == _hoveredTaskIndex && !isSelected);

                Brush rowBg = isSelected ? ZeroWpfTheme.SelectionBackground :
                              isHovered ? ZeroWpfTheme.BgHover :
                              ((r % 2 == 1) ? ZeroWpfTheme.BgInput : ZeroWpfTheme.BgCard);

                // Row background across timeline
                dc.DrawRectangle(rowBg, null, new Rect(timelineX, currentY, timelineW, _rowHeight));
                dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(timelineX, currentY + _rowHeight - 0.5), new Point(width, currentY + _rowHeight - 0.5));

                // Compute task bar position
                double taskStartDays = (task.StartDate - _projectStart).TotalDays;
                double taskEndDays = (task.EndDate - _projectStart).TotalDays;
                double barX = timelineX + (taskStartDays * _pixelsPerDay) - _scrollX;
                double barW = Math.Max(12.0, (taskEndDays - taskStartDays) * _pixelsPerDay);
                double barH = _rowHeight - 10.0;
                double barY = currentY + 5.0;

                Rect barRect = new Rect(barX, barY, barW, barH);
                taskBarRects[task.Id] = barRect;

                if (task.IsMilestone)
                {
                    // Render milestone rotated diamond
                    double centerX = barX;
                    double centerY = currentY + _rowHeight / 2.0;
                    double size = 8.0;

                    var pathGeom = new StreamGeometry();
                    using (var ctx = pathGeom.Open())
                    {
                        ctx.BeginFigure(new Point(centerX, centerY - size), true, true);
                        ctx.LineTo(new Point(centerX + size, centerY), true, false);
                        ctx.LineTo(new Point(centerX, centerY + size), true, false);
                        ctx.LineTo(new Point(centerX - size, centerY), true, false);
                    }
                    pathGeom.Freeze();

                    dc.DrawGeometry(ZeroWpfTheme.WarningAccent, new Pen(Brushes.White, 1.2), pathGeom);

                    // Milestone label
                    var mft = CreateFormattedText(task.Name, ZeroWpfTheme.BoldTypeface, 11.0, ZeroWpfTheme.WarningAccent, dpi);
                    dc.DrawText(mft, new Point(centerX + size + 6.0, centerY - mft.Height / 2.0));
                }
                else
                {
                    // Render task bar rounded rect
                    byte a = (byte)((task.BarColor >> 24) & 0xFF);
                    byte rc = (byte)((task.BarColor >> 16) & 0xFF);
                    byte g = (byte)((task.BarColor >> 8) & 0xFF);
                    byte b = (byte)(task.BarColor & 0xFF);
                    if (a == 0) a = 255;
                    var barBrush = new SolidColorBrush(Color.FromArgb(a, rc, g, b));
                    barBrush.Freeze();

                    // Track
                    dc.DrawRoundedRectangle(ZeroWpfTheme.BgActive, null, barRect, 4, 4);

                    // Progress fill
                    float clampedProgress = Math.Max(0.0f, Math.Min(1.0f, task.Progress));
                    if (clampedProgress > 0)
                    {
                        Rect fillRect = new Rect(barX, barY, barW * clampedProgress, barH);
                        dc.DrawRoundedRectangle(barBrush, null, fillRect, 4, 4);
                    }

                    dc.DrawRoundedRectangle(null, new Pen(barBrush, 1.0), barRect, 4, 4);

                    // Task title or progress text inside or beside bar
                    string progressText = $"{task.Name} ({(int)(clampedProgress * 100)}%)";
                    var ftBar = CreateFormattedText(progressText, ZeroWpfTheme.BoldTypeface, 10.5, Brushes.White, dpi);
                    if (barW > ftBar.Width + 12)
                    {
                        dc.DrawText(ftBar, new Point(barX + 8.0, barY + (barH - ftBar.Height) / 2.0));
                    }
                    else
                    {
                        var ftBeside = CreateFormattedText(progressText, ZeroWpfTheme.RegularTypeface, 10.5, ZeroWpfTheme.TextPrimary, dpi);
                        dc.DrawText(ftBeside, new Point(barX + barW + 8.0, barY + (barH - ftBeside.Height) / 2.0));
                    }
                }

                currentY += _rowHeight;
            }

            // 3. Render Dependency Links (Predecessors)
            var arrowPen = new Pen(ZeroWpfTheme.PrimaryAccent, 1.5);
            arrowPen.Freeze();

            for (int r = 0; r < totalTasks; r++)
            {
                var task = _tasks[r];
                if (!taskBarRects.TryGetValue(task.Id, out var targetRect)) continue;

                for (int p = 0; p < task.PredecessorIds.Count; p++)
                {
                    int predId = task.PredecessorIds[p];
                    if (taskBarRects.TryGetValue(predId, out var predRect))
                    {
                        // Draw orthogonal link from pred right to target left
                        double pX = predRect.Right;
                        double pY = predRect.Y + predRect.Height / 2.0;
                        double tX = targetRect.Left;
                        double tY = targetRect.Y + targetRect.Height / 2.0;

                        double midX = pX + (tX - pX) / 2.0;
                        if (tX < pX) midX = pX + 12;

                        var linkGeom = new StreamGeometry();
                        using (var ctx = linkGeom.Open())
                        {
                            ctx.BeginFigure(new Point(pX, pY), false, false);
                            ctx.LineTo(new Point(midX, pY), true, false);
                            ctx.LineTo(new Point(midX, tY), true, false);
                            ctx.LineTo(new Point(tX - 4.0, tY), true, false);
                        }
                        linkGeom.Freeze();
                        dc.DrawGeometry(null, arrowPen, linkGeom);

                        // Arrowhead
                        var arrowGeom = new StreamGeometry();
                        using (var ctx = arrowGeom.Open())
                        {
                            ctx.BeginFigure(new Point(tX, tY), true, true);
                            ctx.LineTo(new Point(tX - 5.0, tY - 3.5), true, false);
                            ctx.LineTo(new Point(tX - 5.0, tY + 3.5), true, false);
                        }
                        arrowGeom.Freeze();
                        dc.DrawGeometry(ZeroWpfTheme.PrimaryAccent, null, arrowGeom);
                    }
                }
            }

            // 4. Render Left Task List (Pinned)
            double tlY = _headerHeight + (startRow * _rowHeight) - _scrollY;
            for (int r = startRow; r <= endRow && r < totalTasks; r++)
            {
                if (tlY >= height) break;

                var task = _tasks[r];
                bool isSelected = (r == _selectedTaskIndex);
                bool isHovered = (r == _hoveredTaskIndex && !isSelected);

                Brush tlBg = isSelected ? ZeroWpfTheme.SelectionBackground :
                             isHovered ? ZeroWpfTheme.BgHover :
                             ((r % 2 == 1) ? ZeroWpfTheme.BgInput : ZeroWpfTheme.BgCard);

                dc.DrawRectangle(tlBg, null, new Rect(0, tlY, _taskListWidth, _rowHeight));

                if (isSelected)
                {
                    dc.DrawRectangle(ZeroWpfTheme.PrimaryAccent, null, new Rect(0, tlY, 3.5, _rowHeight));
                }

                // Task ID
                var ftId = CreateFormattedText($"#{task.Id}", ZeroWpfTheme.BoldTypeface, 11.0, ZeroWpfTheme.TextMuted, dpi);
                dc.DrawText(ftId, new Point(8, tlY + (_rowHeight - ftId.Height) / 2.0));

                // Task Name
                Brush nameBrush = isSelected ? ZeroWpfTheme.SelectionForeground : ZeroWpfTheme.TextPrimary;
                var ftName = CreateFormattedText(task.Name, ZeroWpfTheme.BoldTypeface, 11.5, nameBrush, dpi);
                dc.DrawText(ftName, new Point(48, tlY + (_rowHeight - ftName.Height) / 2.0));

                // Duration
                string durText = $"{task.Duration.TotalDays:F0}d";
                var ftDur = CreateFormattedText(durText, ZeroWpfTheme.RegularTypeface, 11.0, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(ftDur, new Point(_taskListWidth - ftDur.Width - 10, tlY + (_rowHeight - ftDur.Height) / 2.0));

                dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(0, tlY + _rowHeight - 0.5), new Point(_taskListWidth, tlY + _rowHeight - 0.5));
                tlY += _rowHeight;
            }

            // Task list right separator
            dc.DrawLine(new Pen(ZeroWpfTheme.PrimaryAccent, 1.5), new Point(_taskListWidth - 0.5, 0), new Point(_taskListWidth - 0.5, height));

            // 5. Render Timeline Header
            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, new Rect(0, 0, width, _headerHeight));
            dc.DrawLine(ZeroWpfTheme.BorderPen, new Point(0, _headerHeight - 0.5), new Point(width, _headerHeight - 0.5));

            // Task List Header
            var ftTlHdr = CreateFormattedText("Task Breakdown", ZeroWpfTheme.BoldTypeface, 12.0, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(ftTlHdr, new Point(12, (_headerHeight - ftTlHdr.Height) / 2.0));

            // Timeline Days Header
            for (int d = 0; d <= totalDays; d++)
            {
                DateTime day = _projectStart.AddDays(d);
                double x = timelineX + (d * _pixelsPerDay) - _scrollX;
                if (x < timelineX - 20 || x > width) continue;

                string dayNum = day.Day.ToString();
                string dayName = day.ToString("ddd", CultureInfo.InvariantCulture);

                var ftDay = CreateFormattedText(dayNum, ZeroWpfTheme.BoldTypeface, 11.0, ZeroWpfTheme.TextPrimary, dpi);
                var ftDName = CreateFormattedText(dayName, ZeroWpfTheme.RegularTypeface, 9.5, ZeroWpfTheme.TextMuted, dpi);

                dc.DrawText(ftDay, new Point(x + (_pixelsPerDay - ftDay.Width) / 2.0, 6));
                dc.DrawText(ftDName, new Point(x + (_pixelsPerDay - ftDName.Width) / 2.0, 22));

                dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(x + _pixelsPerDay - 0.5, 4), new Point(x + _pixelsPerDay - 0.5, _headerHeight - 4));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point pt = e.GetPosition(this);

            int prevHover = _hoveredTaskIndex;
            if (pt.Y > _headerHeight)
            {
                _hoveredTaskIndex = (int)((pt.Y - _headerHeight + _scrollY) / _rowHeight);
                if (_hoveredTaskIndex >= _tasks.Count) _hoveredTaskIndex = -1;
            }
            else
            {
                _hoveredTaskIndex = -1;
            }

            if (prevHover != _hoveredTaskIndex) InvalidateVisual();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredTaskIndex = -1;
            InvalidateVisual();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            Point pt = e.GetPosition(this);

            if (pt.Y > _headerHeight)
            {
                _selectedTaskIndex = (int)((pt.Y - _headerHeight + _scrollY) / _rowHeight);
                if (_selectedTaskIndex >= _tasks.Count) _selectedTaskIndex = -1;
                InvalidateVisual();
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                // Zoom pixels per day
                double factor = e.Delta > 0 ? 1.15 : 0.85;
                PixelsPerDay *= factor;
            }
            else
            {
                // Vertical scroll
                int delta = (e.Delta / 120) * _rowHeight * 3;
                _scrollY = Math.Max(0, _scrollY - delta);
                InvalidateVisual();
            }
        }
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged += InvalidateVisual;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged -= InvalidateVisual;
    }
}

    /// <summary>
    /// Backward-compatibility alias for <see cref="GanttControl"/>.
    /// </summary>
    [Obsolete("ZeroGanttChart is deprecated. Use GanttControl instead.")]
    public class ZeroGanttChart : GanttControl
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Convenience alias for <see cref="ZGanttControl"/>.
    /// </summary>
    public class ZGantt : ZGanttControl { }

    /// <summary>
    /// Legacy alias for <see cref="ZGanttControl"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("GanttControl is deprecated and will be removed in 5 release cycles. Please migrate to ZGanttControl instead.")]
    public class GanttControl : ZGanttControl { }

    /// <summary>
    /// Legacy alias for <see cref="ZGanttControl"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroGanttControl is deprecated and will be removed in 5 release cycles. Please migrate to ZGanttControl instead.")]
    public class ZeroGanttControl : ZGanttControl { }

    #endregion

}
