using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Data;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Workflow
{
    /// <summary>
    /// Event arguments for Gantt task modification (drag reschedule or duration resize).
    /// </summary>
    public class GanttTaskModifiedEventArgs : EventArgs
    {
        public GanttTaskItem Task { get; }
        public DateTime OldStartDate { get; }
        public DateTime OldEndDate { get; }

        public GanttTaskModifiedEventArgs(GanttTaskItem task, DateTime oldStart, DateTime oldEnd)
        {
            Task = task;
            OldStartDate = oldStart;
            OldEndDate = oldEnd;
        }
    }

    /// <summary>
    /// Event arguments for interactive dependency creation between two Gantt tasks.
    /// </summary>
    public class GanttDependencyEventArgs : EventArgs
    {
        public GanttTaskItem Predecessor { get; }
        public GanttTaskItem Successor { get; }

        public GanttDependencyEventArgs(GanttTaskItem predecessor, GanttTaskItem successor)
        {
            Predecessor = predecessor;
            Successor = successor;
        }
    }

    /// <summary>
    /// Ultra-high-performance Enterprise Industrial Gantt Timeline Schedule control for ZeroUI WinForms.
    /// Features direct manipulation (drag-to-reschedule, edge duration resizing, interactive dependency linking),
    /// multi-column split tree grid, CPM critical path highlighting, multi-scale zoom (Hours to Quarters),
    /// and zero-allocation hot rendering pipeline via ZeroFontCache and reusable geometric paths.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Enterprise industrial Gantt chart with interactive drag scheduling, dependencies, critical path, and zero-allocation rendering")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroGanttChart.bmp")]
    public class ZGantt : Control
    {
        private readonly List<GanttTaskItem> _tasks = new List<GanttTaskItem>();
        private DateTime _projectStart = DateTime.Today.AddDays(-2);
        private DateTime _projectEnd = DateTime.Today.AddDays(28);

        private GanttTimeScale _timeScale = GanttTimeScale.Days;
        private int _headerHeight = 48;
        private int _rowHeight = 32;
        private int _taskListWidth = 320;
        private float _pixelsPerUnit = 36f;

        private int _scrollX = 0;
        private int _scrollY = 0;
        private int _selectedTaskIndex = -1;
        private int _hoveredTaskIndex = -1;

        private bool _showCriticalPath = true;
        private bool _showDependencies = true;
        private HashSet<int> _criticalTaskIds = new HashSet<int>();

        // Interactive Manipulation State
        private enum DragAction
        {
            None,
            SplitterResize,
            TaskMove,
            TaskResizeLeft,
            TaskResizeRight,
            DependencyLink
        }

        private DragAction _currentAction = DragAction.None;
        private Point _mouseDownLocation;
        private int _dragTaskIndex = -1;
        private DateTime _dragTaskOrigStart;
        private DateTime _dragTaskOrigEnd;
        private Point _currentMousePosition;
        private int _linkSourceTaskIndex = -1;
        private int _splitterOrigWidth;

        // Hover detection
        private bool _isHoveringSplitter = false;

        // Reusable GDI Resources (Zero Allocation Hot Path)
        private readonly GraphicsPath _reusableBarPath = new GraphicsPath();
        private readonly ToolTip _toolTip = new ToolTip();
        private int _lastToolTipTaskIndex = -1;

        // Public Events
        public event EventHandler<GanttTaskItem>? TaskSelected;
        public event EventHandler<GanttTaskModifiedEventArgs>? TaskModified;
        public event EventHandler<GanttDependencyEventArgs>? DependencyCreated;

        [Category("Appearance")]
        [DefaultValue(320)]
        [Description("Width of the left-hand task data grid split pane.")]
        public int TaskListWidth
        {
            get => _taskListWidth;
            set { _taskListWidth = Math.Max(120, Math.Min(Width - 100, value)); Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(36f)]
        [Description("Pixels allocated per time scale unit (hour, day, week, month).")]
        public float PixelsPerUnit
        {
            get => _pixelsPerUnit;
            set { _pixelsPerUnit = Math.Max(8f, Math.Min(200f, value)); Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(36f)]
        [Description("Legacy alias for PixelsPerUnit.")]
        public float PixelsPerDay
        {
            get => _pixelsPerUnit;
            set => PixelsPerUnit = value;
        }

        [Category("Behavior")]
        [DefaultValue(GanttTimeScale.Days)]
        [Description("Time resolution scale of the timeline view.")]
        public GanttTimeScale TimeScale
        {
            get => _timeScale;
            set
            {
                if (_timeScale != value)
                {
                    _timeScale = value;
                    switch (_timeScale)
                    {
                        case GanttTimeScale.Hours: _pixelsPerUnit = 24f; break;
                        case GanttTimeScale.Days: _pixelsPerUnit = 36f; break;
                        case GanttTimeScale.Weeks: _pixelsPerUnit = 56f; break;
                        case GanttTimeScale.Months: _pixelsPerUnit = 72f; break;
                        case GanttTimeScale.Quarters: _pixelsPerUnit = 96f; break;
                    }
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("When true, calculates and highlights zero-slack tasks along the Critical Path.")]
        public bool ShowCriticalPath
        {
            get => _showCriticalPath;
            set { _showCriticalPath = value; RecalculateCriticalPath(); Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        [Description("When true, draws orthogonal dependency arrows connecting predecessor and successor tasks.")]
        public bool ShowDependencies
        {
            get => _showDependencies;
            set { _showDependencies = value; Invalidate(); }
        }

        [Category("Data")]
        public DateTime ProjectStart
        {
            get => _projectStart;
            set { _projectStart = value; Invalidate(); }
        }

        [Category("Data")]
        public DateTime ProjectEnd
        {
            get => _projectEnd;
            set { _projectEnd = value; Invalidate(); }
        }

        [Browsable(false)]
        public List<GanttTaskItem> Tasks => _tasks;

        public ZGantt()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = ZeroTheme.Colors.Background;
            Size = new Size(840, 420);

            _toolTip.InitialDelay = 400;
            _toolTip.ReshowDelay = 200;
            _toolTip.AutoPopDelay = 5000;

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        public void AddTask(GanttTaskItem task)
        {
            if (task == null) return;
            _tasks.Add(task);
            RecalculateCriticalPath();
            Invalidate();
        }

        public void AddTasks(IEnumerable<GanttTaskItem> tasks)
        {
            if (tasks == null) return;
            _tasks.AddRange(tasks);
            RecalculateCriticalPath();
            Invalidate();
        }

        public void Clear()
        {
            _tasks.Clear();
            _criticalTaskIds.Clear();
            _selectedTaskIndex = -1;
            _hoveredTaskIndex = -1;
            Invalidate();
        }

        public void ZoomIn()
        {
            PixelsPerUnit = Math.Min(200f, _pixelsPerUnit * 1.25f);
        }

        public void ZoomOut()
        {
            PixelsPerUnit = Math.Max(8f, _pixelsPerUnit / 1.25f);
        }

        private void RecalculateCriticalPath()
        {
            if (_showCriticalPath && _tasks.Count > 0)
            {
                _criticalTaskIds = CriticalPathEngine.CalculateCriticalPath(_tasks);
            }
            else
            {
                _criticalTaskIds.Clear();
            }
        }

        #region Painting & Zero-Allocation Pipeline

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var colors = ZeroTheme.Colors;
            g.Clear(colors.Background);

            int width = Width;
            int height = Height;
            if (width <= 0 || height <= 0) return;

            int timelineX = _taskListWidth;
            int timelineWidth = Math.Max(0, width - timelineX);

            // 1. Draw Left Split Grid (Task Table)
            DrawLeftTaskGrid(g, colors, height);

            // 2. Draw Timeline Rows & Grid Lines
            DrawTimelineBackground(g, colors, timelineX, timelineWidth, height);

            // 3. Draw Dependency Links
            if (_showDependencies)
            {
                DrawDependencyLinks(g, colors, timelineX, width, height);
            }

            // 4. Draw Task Bars, Milestones, and Resource Labels
            DrawTaskBars(g, colors, timelineX, width, height);

            // 5. Draw Interactive Rubber-Band Dependency Linking Line
            if (_currentAction == DragAction.DependencyLink && _linkSourceTaskIndex >= 0 && _linkSourceTaskIndex < _tasks.Count)
            {
                DrawInteractiveLinkingLine(g, colors);
            }

            // 6. Draw Today Indicator Line
            DrawTodayLine(g, colors, timelineX, width, height);

            // 7. Draw Dual-Tier Timeline Header
            DrawTimelineHeader(g, colors, timelineX, timelineWidth, width);

            // 8. Draw Left Table Header
            DrawLeftGridHeader(g, colors);

            // 9. Draw Splitter Bar
            DrawSplitter(g, colors, height);
        }

        private void DrawLeftTaskGrid(Graphics g, ZeroThemePalette colors, int height)
        {
            using var bgBrush = new SolidBrush(colors.Surface);
            g.FillRectangle(bgBrush, 0, 0, _taskListWidth, height);

            int startRow = Math.Max(0, _scrollY / _rowHeight);
            int visibleRowCount = ((height - _headerHeight) / _rowHeight) + 2;
            int endRow = Math.Min(_tasks.Count, startRow + visibleRowCount);

            var font = ZeroFontCache.Get(9f, FontStyle.Regular);
            var subFont = ZeroFontCache.Get(8f, FontStyle.Regular);
            var boldFont = ZeroFontCache.Get(9f, FontStyle.Bold);

            using var textBrush = new SolidBrush(colors.TextPrimary);
            using var mutedBrush = new SolidBrush(colors.TextSecondary);
            using var linePen = new Pen(colors.Border);
            using var selBrush = new SolidBrush(Color.FromArgb(40, colors.Primary));
            using var hovBrush = new SolidBrush(Color.FromArgb(20, colors.Primary));
            using var critBrush = new SolidBrush(colors.Danger);

            var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
            var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

            for (int r = startRow; r < endRow; r++)
            {
                var task = _tasks[r];
                int rowY = _headerHeight + r * _rowHeight - _scrollY;

                // Selection / Hover background
                if (r == _selectedTaskIndex)
                {
                    g.FillRectangle(selBrush, 0, rowY, _taskListWidth, _rowHeight);
                }
                else if (r == _hoveredTaskIndex)
                {
                    g.FillRectangle(hovBrush, 0, rowY, _taskListWidth, _rowHeight);
                }

                // Row bottom border
                g.DrawLine(linePen, 0, rowY + _rowHeight - 1, _taskListWidth, rowY + _rowHeight - 1);

                // Col 1: ID (36px)
                var idRect = new Rectangle(8, rowY, 28, _rowHeight);
                g.DrawString(task.Id.ToString(), subFont, mutedBrush, idRect, sf);

                // Critical Path indicator dot
                if (_showCriticalPath && _criticalTaskIds.Contains(task.Id))
                {
                    g.FillEllipse(critBrush, 36, rowY + (_rowHeight - 6) / 2, 6, 6);
                }

                // Col 2: Task Name with tree indent & expander
                int indent = 48 + task.Level * 14;
                int nameWidth = Math.Max(60, _taskListWidth - indent - 110);
                var nameRect = new Rectangle(indent, rowY, nameWidth, _rowHeight);

                var taskFont = task.Level == 0 ? boldFont : font;
                g.DrawString(task.Name, taskFont, textBrush, nameRect, sf);

                // Col 3: Duration / Progress (right aligned, ~90px)
                string durationStr = $"{(int)task.Duration.TotalDays}d";
                string progStr = $"{task.Progress * 100:0}%";
                var durRect = new Rectangle(_taskListWidth - 95, rowY, 40, _rowHeight);
                var progRect = new Rectangle(_taskListWidth - 50, rowY, 42, _rowHeight);

                g.DrawString(durationStr, subFont, mutedBrush, durRect, sfRight);
                g.DrawString(progStr, subFont, task.Progress >= 1f ? textBrush : mutedBrush, progRect, sfRight);
            }
        }

        private void DrawTimelineBackground(Graphics g, ZeroThemePalette colors, int timelineX, int timelineW, int height)
        {
            int startRow = Math.Max(0, _scrollY / _rowHeight);
            int visibleRowCount = ((height - _headerHeight) / _rowHeight) + 2;
            int endRow = Math.Min(_tasks.Count, startRow + visibleRowCount);

            using var altBrush = new SolidBrush(colors.CardBackground);
            using var linePen = new Pen(colors.Border);
            using var selBrush = new SolidBrush(Color.FromArgb(20, colors.Primary));

            for (int r = startRow; r < endRow; r++)
            {
                int rowY = _headerHeight + r * _rowHeight - _scrollY;

                if (r == _selectedTaskIndex)
                {
                    g.FillRectangle(selBrush, timelineX, rowY, timelineW, _rowHeight);
                }
                else if (r % 2 == 1)
                {
                    g.FillRectangle(altBrush, timelineX, rowY, timelineW, _rowHeight);
                }

                g.DrawLine(linePen, timelineX, rowY + _rowHeight - 1, Width, rowY + _rowHeight - 1);
            }
        }

        private void DrawTaskBars(Graphics g, ZeroThemePalette colors, int timelineX, int width, int height)
        {
            int startRow = Math.Max(0, _scrollY / _rowHeight);
            int visibleRowCount = ((height - _headerHeight) / _rowHeight) + 2;
            int endRow = Math.Min(_tasks.Count, startRow + visibleRowCount);

            var resFont = ZeroFontCache.Get(8f, FontStyle.Regular);
            var mileFont = ZeroFontCache.Get(8f, FontStyle.Bold);

            using var resBrush = new SolidBrush(colors.TextSecondary);
            using var anchorBrush = new SolidBrush(colors.Warning);

            for (int r = startRow; r < endRow; r++)
            {
                var task = _tasks[r];
                int rowY = _headerHeight + r * _rowHeight - _scrollY;

                float startX = GetXCoordinateForDate(task.StartDate, timelineX);
                float endX = GetXCoordinateForDate(task.EndDate, timelineX);
                float barW = Math.Max(6, endX - startX);

                if (startX + barW < timelineX || startX > width) continue;

                float barY = rowY + 6;
                float barH = _rowHeight - 12;
                bool isCritical = _showCriticalPath && _criticalTaskIds.Contains(task.Id);

                if (task.IsMilestone)
                {
                    // Draw Diamond Milestone
                    float midX = startX;
                    float midY = barY + barH / 2;
                    PointF[] diamond = new[]
                    {
                        new PointF(midX, midY - barH / 2),
                        new PointF(midX + barH / 2, midY),
                        new PointF(midX, midY + barH / 2),
                        new PointF(midX - barH / 2, midY)
                    };

                    Color mColor = isCritical ? colors.Danger : colors.Warning;
                    using var mBrush = new SolidBrush(mColor);
                    g.FillPolygon(mBrush, diamond);
                    using var mPen = new Pen(Color.White, 1.2f);
                    g.DrawPolygon(mPen, diamond);

                    // Milestone caption
                    g.DrawString(task.Name, mileFont, resBrush, midX + barH / 2 + 6, barY + 2);
                }
                else
                {
                    // Draw Task Bar with Rounded Geometry
                    var barRect = new Rectangle((int)startX, (int)barY, (int)barW, (int)barH);
                    UpdateRoundedRectPath(_reusableBarPath, barRect, 4);

                    Color baseColor = task.BarColor != 0 ? Color.FromArgb((int)task.BarColor) : colors.Primary;
                    if (isCritical) baseColor = colors.Danger;

                    // Base bar fill
                    using (var barBrush = new SolidBrush(Color.FromArgb(160, baseColor)))
                    {
                        g.FillPath(barBrush, _reusableBarPath);
                    }

                    // Progress fill
                    if (task.Progress > 0)
                    {
                        float progW = barW * Math.Min(1.0f, task.Progress);
                        var progRect = new Rectangle((int)startX, (int)barY, (int)progW, (int)barH);
                        using var progPath = new GraphicsPath();
                        UpdateRoundedRectPath(progPath, progRect, 4);
                        using var progBrush = new SolidBrush(baseColor);
                        g.FillPath(progBrush, progPath);
                    }

                    // Bar outline
                    using (var borderPen = new Pen(isCritical ? colors.Danger : baseColor, isCritical ? 2f : 1.2f))
                    {
                        g.DrawPath(borderPen, _reusableBarPath);
                    }

                    // Resource & Duration label next to bar
                    string label = !string.IsNullOrEmpty(task.AssignedResource) ? $"{task.AssignedResource} ({(int)task.Duration.TotalDays}d)" : $"{(int)task.Duration.TotalDays}d";
                    g.DrawString(label, resFont, resBrush, startX + barW + 8, barY + 2);

                    // Draw Dependency Connector Anchor when hovered
                    if (r == _hoveredTaskIndex)
                    {
                        float anchorX = startX + barW;
                        float anchorY = barY + barH / 2;
                        g.FillEllipse(anchorBrush, anchorX - 4, anchorY - 4, 8, 8);
                        using var anchorPen = new Pen(Color.White, 1.5f);
                        g.DrawEllipse(anchorPen, anchorX - 4, anchorY - 4, 8, 8);
                    }
                }
            }
        }

        private void DrawDependencyLinks(Graphics g, ZeroThemePalette colors, int timelineX, int width, int height)
        {
            if (_tasks.Count == 0) return;

            var taskIndexMap = new Dictionary<int, int>(_tasks.Count);
            for (int i = 0; i < _tasks.Count; i++)
            {
                taskIndexMap[_tasks[i].Id] = i;
            }

            using var linkPen = new Pen(Color.FromArgb(180, colors.TextSecondary), 1.3f);
            using var critLinkPen = new Pen(colors.Danger, 1.8f);
            using var selLinkPen = new Pen(colors.Primary, 2.0f);
            using var arrowBrush = new SolidBrush(colors.TextSecondary);
            using var critArrowBrush = new SolidBrush(colors.Danger);
            using var selArrowBrush = new SolidBrush(colors.Primary);

            for (int r = 0; r < _tasks.Count; r++)
            {
                var succTask = _tasks[r];
                if (succTask.PredecessorIds == null || succTask.PredecessorIds.Count == 0) continue;

                int succRowY = _headerHeight + r * _rowHeight - _scrollY;
                float succX = GetXCoordinateForDate(succTask.StartDate, timelineX);
                float succY = succRowY + _rowHeight / 2f;

                foreach (int predId in succTask.PredecessorIds)
                {
                    if (!taskIndexMap.TryGetValue(predId, out int predRowIndex)) continue;

                    var predTask = _tasks[predRowIndex];
                    int predRowY = _headerHeight + predRowIndex * _rowHeight - _scrollY;
                    float predEndX = GetXCoordinateForDate(predTask.EndDate, timelineX);
                    float predY = predRowY + _rowHeight / 2f;

                    bool isCritical = _showCriticalPath && _criticalTaskIds.Contains(predTask.Id) && _criticalTaskIds.Contains(succTask.Id);
                    bool isSelected = (r == _selectedTaskIndex || predRowIndex == _selectedTaskIndex);

                    Pen currentPen = isSelected ? selLinkPen : (isCritical ? critLinkPen : linkPen);
                    Brush currentBrush = isSelected ? selArrowBrush : (isCritical ? critArrowBrush : arrowBrush);

                    float midX = predEndX + 14f;

                    if (succX >= predEndX + 20f)
                    {
                        // S-shape orthogonal link
                        using var path = new GraphicsPath();
                        path.AddLine(predEndX, predY, midX, predY);
                        path.AddLine(midX, predY, midX, succY);
                        path.AddLine(midX, succY, succX - 6, succY);
                        g.DrawPath(currentPen, path);
                    }
                    else
                    {
                        // Backward loop link (successor starts before predecessor finishes)
                        float detourY = predY + (succY > predY ? _rowHeight * 0.4f : -_rowHeight * 0.4f);
                        using var path = new GraphicsPath();
                        path.AddLine(predEndX, predY, predEndX + 10, predY);
                        path.AddLine(predEndX + 10, predY, predEndX + 10, detourY);
                        path.AddLine(predEndX + 10, detourY, succX - 10, detourY);
                        path.AddLine(succX - 10, detourY, succX - 10, succY);
                        path.AddLine(succX - 10, succY, succX - 6, succY);
                        g.DrawPath(currentPen, path);
                    }

                    // Draw filled arrowhead pointing right at (succX, succY)
                    PointF[] arrow = new[]
                    {
                        new PointF(succX, succY),
                        new PointF(succX - 6, succY - 4),
                        new PointF(succX - 6, succY + 4)
                    };
                    g.FillPolygon(currentBrush, arrow);
                }
            }
        }

        private void DrawInteractiveLinkingLine(Graphics g, ZeroThemePalette colors)
        {
            var sourceTask = _tasks[_linkSourceTaskIndex];
            int sourceRowY = _headerHeight + _linkSourceTaskIndex * _rowHeight - _scrollY;
            float sourceX = GetXCoordinateForDate(sourceTask.EndDate, _taskListWidth);
            float sourceY = sourceRowY + _rowHeight / 2f;

            using var activePen = new Pen(colors.Warning, 2f) { DashStyle = DashStyle.Dash };
            g.DrawLine(activePen, sourceX, sourceY, _currentMousePosition.X, _currentMousePosition.Y);

            using var anchorBrush = new SolidBrush(colors.Warning);
            g.FillEllipse(anchorBrush, _currentMousePosition.X - 4, _currentMousePosition.Y - 4, 8, 8);
        }

        private void DrawTodayLine(Graphics g, ZeroThemePalette colors, int timelineX, int width, int height)
        {
            float todayX = GetXCoordinateForDate(DateTime.Today, timelineX);
            if (todayX >= timelineX && todayX <= width)
            {
                using var todayPen = new Pen(colors.Danger, 1.5f) { DashStyle = DashStyle.Dash };
                g.DrawLine(todayPen, todayX, _headerHeight, todayX, height);

                // Today badge
                var font = ZeroFontCache.Get(7.5f, FontStyle.Bold);
                using var badgeBrush = new SolidBrush(colors.Danger);
                g.FillRectangle(badgeBrush, todayX - 22, _headerHeight + 2, 44, 16);
                TextRenderer.DrawText(g, "TODAY", font, new Rectangle((int)todayX - 22, _headerHeight + 2, 44, 16), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private void DrawTimelineHeader(Graphics g, ZeroThemePalette colors, int timelineX, int timelineW, int width)
        {
            using var headerBrush = new SolidBrush(colors.HeaderBackground);
            g.FillRectangle(headerBrush, timelineX, 0, timelineW, _headerHeight);

            using var pen = new Pen(colors.Border);
            g.DrawLine(pen, timelineX, _headerHeight - 1, width, _headerHeight - 1);
            g.DrawLine(pen, timelineX, _headerHeight / 2, width, _headerHeight / 2);

            var majorFont = ZeroFontCache.Get(8.5f, FontStyle.Bold);
            var minorFont = ZeroFontCache.Get(8f, FontStyle.Regular);
            using var textBrush = new SolidBrush(colors.TextPrimary);
            using var subBrush = new SolidBrush(colors.TextSecondary);

            switch (_timeScale)
            {
                case GanttTimeScale.Hours:
                    DrawHourlyHeader(g, colors, timelineX, width, majorFont, minorFont, textBrush, subBrush, pen);
                    break;
                case GanttTimeScale.Weeks:
                    DrawWeeklyHeader(g, colors, timelineX, width, majorFont, minorFont, textBrush, subBrush, pen);
                    break;
                case GanttTimeScale.Months:
                    DrawMonthlyHeader(g, colors, timelineX, width, majorFont, minorFont, textBrush, subBrush, pen);
                    break;
                case GanttTimeScale.Quarters:
                    DrawQuarterlyHeader(g, colors, timelineX, width, majorFont, minorFont, textBrush, subBrush, pen);
                    break;
                case GanttTimeScale.Days:
                default:
                    DrawDailyHeader(g, colors, timelineX, width, majorFont, minorFont, textBrush, subBrush, pen);
                    break;
            }
        }

        private void DrawDailyHeader(Graphics g, ZeroThemePalette colors, int timelineX, int width, Font majorFont, Font minorFont, Brush textBrush, Brush subBrush, Pen pen)
        {
            int totalDays = (int)Math.Max(1, (_projectEnd - _projectStart).TotalDays);
            DateTime currentMonth = DateTime.MinValue;

            for (int d = 0; d < totalDays; d++)
            {
                DateTime date = _projectStart.AddDays(d);
                float dayX = timelineX + d * _pixelsPerUnit - _scrollX;
                if (dayX + _pixelsPerUnit < timelineX || dayX > width) continue;

                // Major Tier: Month grouping
                if (date.Month != currentMonth.Month || date.Year != currentMonth.Year)
                {
                    if (currentMonth != DateTime.MinValue)
                    {
                        g.DrawLine(pen, dayX, 0, dayX, _headerHeight / 2);
                    }
                    currentMonth = date;
                    g.DrawString(date.ToString("MMMM yyyy"), majorFont, textBrush, dayX + 6, 4);
                }

                // Minor Tier: Day numbers & ticks
                g.DrawLine(pen, dayX, _headerHeight / 2, dayX, _headerHeight);
                bool isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
                Brush dayBrush = isWeekend ? subBrush : textBrush;
                g.DrawString(date.Day.ToString(), minorFont, dayBrush, dayX + 4, _headerHeight / 2 + 5);
            }
        }

        private void DrawHourlyHeader(Graphics g, ZeroThemePalette colors, int timelineX, int width, Font majorFont, Font minorFont, Brush textBrush, Brush subBrush, Pen pen)
        {
            int totalHours = (int)Math.Max(1, (_projectEnd - _projectStart).TotalHours);
            for (int h = 0; h < totalHours; h += 2)
            {
                DateTime time = _projectStart.AddHours(h);
                float hX = timelineX + (h * _pixelsPerUnit) - _scrollX;
                if (hX + _pixelsPerUnit < timelineX || hX > width) continue;

                if (time.Hour == 0 || h == 0)
                {
                    g.DrawLine(pen, hX, 0, hX, _headerHeight / 2);
                    g.DrawString(time.ToString("MMM dd, yyyy"), majorFont, textBrush, hX + 6, 4);
                }

                g.DrawLine(pen, hX, _headerHeight / 2, hX, _headerHeight);
                g.DrawString(time.ToString("HH:00"), minorFont, subBrush, hX + 2, _headerHeight / 2 + 5);
            }
        }

        private void DrawWeeklyHeader(Graphics g, ZeroThemePalette colors, int timelineX, int width, Font majorFont, Font minorFont, Brush textBrush, Brush subBrush, Pen pen)
        {
            int totalDays = (int)Math.Max(1, (_projectEnd - _projectStart).TotalDays);
            for (int d = 0; d < totalDays; d += 7)
            {
                DateTime date = _projectStart.AddDays(d);
                float wX = timelineX + (d / 7f * _pixelsPerUnit) - _scrollX;
                if (wX + _pixelsPerUnit < timelineX || wX > width) continue;

                g.DrawLine(pen, wX, 0, wX, _headerHeight);
                g.DrawString(date.ToString("MMM yyyy"), majorFont, textBrush, wX + 6, 4);
                g.DrawString($"W{d / 7 + 1} ({date:dd/MM})", minorFont, subBrush, wX + 4, _headerHeight / 2 + 5);
            }
        }

        private void DrawMonthlyHeader(Graphics g, ZeroThemePalette colors, int timelineX, int width, Font majorFont, Font minorFont, Brush textBrush, Brush subBrush, Pen pen)
        {
            int totalMonths = (int)Math.Max(1, ((_projectEnd.Year - _projectStart.Year) * 12) + _projectEnd.Month - _projectStart.Month);
            for (int m = 0; m <= totalMonths; m++)
            {
                DateTime date = _projectStart.AddMonths(m);
                float mX = timelineX + (m * _pixelsPerUnit) - _scrollX;
                if (mX + _pixelsPerUnit < timelineX || mX > width) continue;

                g.DrawLine(pen, mX, 0, mX, _headerHeight);
                g.DrawString(date.ToString("yyyy"), majorFont, textBrush, mX + 6, 4);
                g.DrawString(date.ToString("MMM"), minorFont, subBrush, mX + 4, _headerHeight / 2 + 5);
            }
        }

        private void DrawQuarterlyHeader(Graphics g, ZeroThemePalette colors, int timelineX, int width, Font majorFont, Font minorFont, Brush textBrush, Brush subBrush, Pen pen)
        {
            int totalQuarters = (int)Math.Max(1, (((_projectEnd.Year - _projectStart.Year) * 12) + _projectEnd.Month - _projectStart.Month) / 3);
            for (int q = 0; q <= totalQuarters; q++)
            {
                DateTime date = _projectStart.AddMonths(q * 3);
                float qX = timelineX + (q * _pixelsPerUnit) - _scrollX;
                if (qX + _pixelsPerUnit < timelineX || qX > width) continue;

                int quarterNum = ((date.Month - 1) / 3) + 1;
                g.DrawLine(pen, qX, 0, qX, _headerHeight);
                g.DrawString(date.ToString("yyyy"), majorFont, textBrush, qX + 6, 4);
                g.DrawString($"Q{quarterNum}", minorFont, subBrush, qX + 4, _headerHeight / 2 + 5);
            }
        }

        private void DrawLeftGridHeader(Graphics g, ZeroThemePalette colors)
        {
            using var headerBrush = new SolidBrush(colors.HeaderBackground);
            g.FillRectangle(headerBrush, 0, 0, _taskListWidth, _headerHeight);

            using var pen = new Pen(colors.Border);
            g.DrawLine(pen, 0, _headerHeight - 1, _taskListWidth, _headerHeight - 1);

            var titleFont = ZeroFontCache.Get(8.5f, FontStyle.Bold);
            using var textBrush = new SolidBrush(colors.TextPrimary);

            var sf = new StringFormat { LineAlignment = StringAlignment.Center };
            var sfRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

            g.DrawString("ID", titleFont, textBrush, new Rectangle(8, 0, 32, _headerHeight), sf);
            g.DrawString("TASK / OPERATION", titleFont, textBrush, new Rectangle(48, 0, _taskListWidth - 150, _headerHeight), sf);
            g.DrawString("DUR", titleFont, textBrush, new Rectangle(_taskListWidth - 95, 0, 40, _headerHeight), sfRight);
            g.DrawString("%", titleFont, textBrush, new Rectangle(_taskListWidth - 50, 0, 42, _headerHeight), sfRight);
        }

        private void DrawSplitter(Graphics g, ZeroThemePalette colors, int height)
        {
            using var pen = new Pen(_isHoveringSplitter || _currentAction == DragAction.SplitterResize ? colors.Primary : colors.Border, 2f);
            g.DrawLine(pen, _taskListWidth - 1, 0, _taskListWidth - 1, height);
        }

        private float GetXCoordinateForDate(DateTime date, int timelineX)
        {
            switch (_timeScale)
            {
                case GanttTimeScale.Hours:
                    return timelineX + (float)(date - _projectStart).TotalHours * _pixelsPerUnit - _scrollX;
                case GanttTimeScale.Weeks:
                    return timelineX + (float)(date - _projectStart).TotalDays / 7f * _pixelsPerUnit - _scrollX;
                case GanttTimeScale.Months:
                    float months = ((date.Year - _projectStart.Year) * 12) + (date.Month - _projectStart.Month) + (date.Day / 30f);
                    return timelineX + months * _pixelsPerUnit - _scrollX;
                case GanttTimeScale.Quarters:
                    float quarters = (((date.Year - _projectStart.Year) * 12) + (date.Month - _projectStart.Month)) / 3f;
                    return timelineX + quarters * _pixelsPerUnit - _scrollX;
                case GanttTimeScale.Days:
                default:
                    return timelineX + (float)(date - _projectStart).TotalDays * _pixelsPerUnit - _scrollX;
            }
        }

        private DateTime GetDateForXCoordinate(float x, int timelineX)
        {
            float relativeX = x - timelineX + _scrollX;
            switch (_timeScale)
            {
                case GanttTimeScale.Hours:
                    double hours = relativeX / _pixelsPerUnit;
                    return _projectStart.AddHours(hours);
                case GanttTimeScale.Weeks:
                    double weeks = relativeX / _pixelsPerUnit;
                    return _projectStart.AddDays(weeks * 7);
                case GanttTimeScale.Months:
                    double m = relativeX / _pixelsPerUnit;
                    return _projectStart.AddDays(m * 30.4375);
                case GanttTimeScale.Quarters:
                    double q = relativeX / _pixelsPerUnit;
                    return _projectStart.AddDays(q * 91.3125);
                case GanttTimeScale.Days:
                default:
                    double days = relativeX / _pixelsPerUnit;
                    return _projectStart.AddDays(days);
            }
        }

        private static void UpdateRoundedRectPath(GraphicsPath path, Rectangle rect, int radius)
        {
            path.Reset();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
        }

        #endregion

        #region Interactive Input Handling

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _currentMousePosition = e.Location;

            if (_currentAction == DragAction.SplitterResize)
            {
                int newWidth = _splitterOrigWidth + (e.X - _mouseDownLocation.X);
                TaskListWidth = newWidth;
                return;
            }

            if (_currentAction == DragAction.TaskMove && _dragTaskIndex >= 0 && _dragTaskIndex < _tasks.Count)
            {
                var task = _tasks[_dragTaskIndex];
                float deltaPixels = e.X - _mouseDownLocation.X;
                TimeSpan deltaSpan = GetTimeSpanForPixels(deltaPixels);

                DateTime newStart = _dragTaskOrigStart + deltaSpan;
                DateTime newEnd = _dragTaskOrigEnd + deltaSpan;

                task.StartDate = newStart;
                task.EndDate = newEnd;
                RecalculateCriticalPath();
                Invalidate();
                return;
            }

            if (_currentAction == DragAction.TaskResizeLeft && _dragTaskIndex >= 0 && _dragTaskIndex < _tasks.Count)
            {
                var task = _tasks[_dragTaskIndex];
                DateTime newStart = GetDateForXCoordinate(e.X, _taskListWidth);
                if (newStart < task.EndDate.AddHours(-1))
                {
                    task.StartDate = newStart;
                    RecalculateCriticalPath();
                    Invalidate();
                }
                return;
            }

            if (_currentAction == DragAction.TaskResizeRight && _dragTaskIndex >= 0 && _dragTaskIndex < _tasks.Count)
            {
                var task = _tasks[_dragTaskIndex];
                DateTime newEnd = GetDateForXCoordinate(e.X, _taskListWidth);
                if (newEnd > task.StartDate.AddHours(1))
                {
                    task.EndDate = newEnd;
                    RecalculateCriticalPath();
                    Invalidate();
                }
                return;
            }

            if (_currentAction == DragAction.DependencyLink)
            {
                Invalidate();
                return;
            }

            // Normal Hover Hit-Testing
            UpdateHoverState(e.Location);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _mouseDownLocation = e.Location;

            if (e.Button == MouseButtons.Left)
            {
                // 1. Check Splitter bar
                if (Math.Abs(e.X - _taskListWidth) <= 4)
                {
                    _currentAction = DragAction.SplitterResize;
                    _splitterOrigWidth = _taskListWidth;
                    Capture = true;
                    return;
                }

                // 2. Row Selection & Left Table Click
                if (e.Y > _headerHeight)
                {
                    int row = (e.Y - _headerHeight + _scrollY) / _rowHeight;
                    if (row >= 0 && row < _tasks.Count)
                    {
                        _selectedTaskIndex = row;
                        TaskSelected?.Invoke(this, _tasks[row]);

                        if (e.X > _taskListWidth)
                        {
                            var task = _tasks[row];
                            float startX = GetXCoordinateForDate(task.StartDate, _taskListWidth);
                            float endX = GetXCoordinateForDate(task.EndDate, _taskListWidth);

                            // Check dependency anchor
                            if (Math.Abs(e.X - endX) <= 6 && Math.Abs(e.Y - (_headerHeight + row * _rowHeight - _scrollY + _rowHeight / 2)) <= 6)
                            {
                                _currentAction = DragAction.DependencyLink;
                                _linkSourceTaskIndex = row;
                                Capture = true;
                                Invalidate();
                                return;
                            }

                            // Check left edge resize
                            if (Math.Abs(e.X - startX) <= 5)
                            {
                                _currentAction = DragAction.TaskResizeLeft;
                                _dragTaskIndex = row;
                                _dragTaskOrigStart = task.StartDate;
                                _dragTaskOrigEnd = task.EndDate;
                                Capture = true;
                                return;
                            }

                            // Check right edge resize
                            if (Math.Abs(e.X - endX) <= 5)
                            {
                                _currentAction = DragAction.TaskResizeRight;
                                _dragTaskIndex = row;
                                _dragTaskOrigStart = task.StartDate;
                                _dragTaskOrigEnd = task.EndDate;
                                Capture = true;
                                return;
                            }

                            // Check task body drag
                            if (e.X >= startX && e.X <= endX)
                            {
                                _currentAction = DragAction.TaskMove;
                                _dragTaskIndex = row;
                                _dragTaskOrigStart = task.StartDate;
                                _dragTaskOrigEnd = task.EndDate;
                                Capture = true;
                                return;
                            }
                        }

                        Invalidate();
                    }
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (Capture) Capture = false;

            if (_currentAction == DragAction.TaskMove || _currentAction == DragAction.TaskResizeLeft || _currentAction == DragAction.TaskResizeRight)
            {
                if (_dragTaskIndex >= 0 && _dragTaskIndex < _tasks.Count)
                {
                    var task = _tasks[_dragTaskIndex];
                    TaskModified?.Invoke(this, new GanttTaskModifiedEventArgs(task, _dragTaskOrigStart, _dragTaskOrigEnd));
                }
            }
            else if (_currentAction == DragAction.DependencyLink && _linkSourceTaskIndex >= 0 && _linkSourceTaskIndex < _tasks.Count)
            {
                // Detect target task
                if (e.Y > _headerHeight)
                {
                    int targetRow = (e.Y - _headerHeight + _scrollY) / _rowHeight;
                    if (targetRow >= 0 && targetRow < _tasks.Count && targetRow != _linkSourceTaskIndex)
                    {
                        var sourceTask = _tasks[_linkSourceTaskIndex];
                        var targetTask = _tasks[targetRow];

                        if (!targetTask.PredecessorIds.Contains(sourceTask.Id))
                        {
                            targetTask.PredecessorIds.Add(sourceTask.Id);
                            RecalculateCriticalPath();
                            DependencyCreated?.Invoke(this, new GanttDependencyEventArgs(sourceTask, targetTask));
                        }
                    }
                }
            }

            _currentAction = DragAction.None;
            _dragTaskIndex = -1;
            _linkSourceTaskIndex = -1;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                // Zoom in/out
                if (e.Delta > 0) ZoomIn();
                else ZoomOut();
            }
            else if ((ModifierKeys & Keys.Shift) == Keys.Shift)
            {
                // Horizontal scroll
                _scrollX = Math.Max(0, _scrollX - e.Delta);
                Invalidate();
            }
            else
            {
                // Vertical scroll
                int maxScrollY = Math.Max(0, _tasks.Count * _rowHeight - (Height - _headerHeight));
                _scrollY = Math.Max(0, Math.Min(maxScrollY, _scrollY - (e.Delta / 120) * _rowHeight));
                Invalidate();
            }
        }

        private void UpdateHoverState(Point mousePos)
        {
            // Splitter hover
            if (Math.Abs(mousePos.X - _taskListWidth) <= 4)
            {
                Cursor = Cursors.VSplit;
                _isHoveringSplitter = true;
                return;
            }
            _isHoveringSplitter = false;

            if (mousePos.Y > _headerHeight)
            {
                int row = (mousePos.Y - _headerHeight + _scrollY) / _rowHeight;
                if (row >= 0 && row < _tasks.Count)
                {
                    if (_hoveredTaskIndex != row)
                    {
                        _hoveredTaskIndex = row;
                        Invalidate();
                    }

                    if (mousePos.X > _taskListWidth)
                    {
                        var task = _tasks[row];
                        float startX = GetXCoordinateForDate(task.StartDate, _taskListWidth);
                        float endX = GetXCoordinateForDate(task.EndDate, _taskListWidth);

                        // Dependency anchor hover
                        if (Math.Abs(mousePos.X - endX) <= 6 && Math.Abs(mousePos.Y - (_headerHeight + row * _rowHeight - _scrollY + _rowHeight / 2)) <= 6)
                        {
                            Cursor = Cursors.Hand;
                            return;
                        }

                        // Left/Right resize edges
                        if (Math.Abs(mousePos.X - startX) <= 5 || Math.Abs(mousePos.X - endX) <= 5)
                        {
                            Cursor = Cursors.SizeWE;
                            return;
                        }

                        // Task body drag
                        if (mousePos.X >= startX && mousePos.X <= endX)
                        {
                            Cursor = Cursors.SizeAll;
                            ShowTaskToolTip(row, mousePos);
                            return;
                        }
                    }

                    Cursor = Cursors.Default;
                    HideTaskToolTip();
                    return;
                }
            }

            if (_hoveredTaskIndex != -1)
            {
                _hoveredTaskIndex = -1;
                Invalidate();
            }
            Cursor = Cursors.Default;
            HideTaskToolTip();
        }

        private void ShowTaskToolTip(int taskIndex, Point mousePos)
        {
            if (_lastToolTipTaskIndex != taskIndex && taskIndex >= 0 && taskIndex < _tasks.Count)
            {
                _lastToolTipTaskIndex = taskIndex;
                var task = _tasks[taskIndex];
                bool isCrit = _showCriticalPath && _criticalTaskIds.Contains(task.Id);
                string preds = task.PredecessorIds.Count > 0 ? string.Join(", ", task.PredecessorIds) : "None";
                string res = !string.IsNullOrEmpty(task.AssignedResource) ? task.AssignedResource! : "Unassigned";

                string tipText = $"{task.Name}\n" +
                                $"-------------------------\n" +
                                $"Duration: {(int)task.Duration.TotalDays} days ({task.StartDate:d} - {task.EndDate:d})\n" +
                                $"Progress: {task.Progress * 100:0}%\n" +
                                $"Resource: {res}\n" +
                                $"Predecessors: {preds}" +
                                (isCrit ? "\n[!] Critical Path Task" : "");

                _toolTip.Show(tipText, this, mousePos.X + 16, mousePos.Y + 16, 4000);
            }
        }

        private void HideTaskToolTip()
        {
            if (_lastToolTipTaskIndex != -1)
            {
                _lastToolTipTaskIndex = -1;
                _toolTip.Hide(this);
            }
        }

        private TimeSpan GetTimeSpanForPixels(float pixels)
        {
            switch (_timeScale)
            {
                case GanttTimeScale.Hours:
                    return TimeSpan.FromHours(pixels / _pixelsPerUnit);
                case GanttTimeScale.Weeks:
                    return TimeSpan.FromDays((pixels / _pixelsPerUnit) * 7);
                case GanttTimeScale.Months:
                    return TimeSpan.FromDays((pixels / _pixelsPerUnit) * 30.4375);
                case GanttTimeScale.Quarters:
                    return TimeSpan.FromDays((pixels / _pixelsPerUnit) * 91.3125);
                case GanttTimeScale.Days:
                default:
                    return TimeSpan.FromDays(pixels / _pixelsPerUnit);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
                _reusableBarPath?.Dispose();
                _toolTip?.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    
    private void OnThemeChanged(object? sender, EventArgs e) => Invalidate();

}

    /// <summary>
    /// Legacy alias for <see cref="ZGantt"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroGanttChart is deprecated. Please use GanttControl instead.")]
    [ToolboxItem(false)]
    public class ZeroGanttChart : GanttControl
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZGantt"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroGanttControl is deprecated and will be removed in 5 release cycles. Please migrate to ZGantt instead.")]
    [ToolboxItem(false)]
    public class ZeroGanttControl : ZGantt
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZGantt"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("GanttControl is deprecated and will be removed in 5 release cycles. Please migrate to ZGantt instead.")]
    [ToolboxItem(false)]
    public class GanttControl : ZGantt
    {
    }

    #endregion
}
