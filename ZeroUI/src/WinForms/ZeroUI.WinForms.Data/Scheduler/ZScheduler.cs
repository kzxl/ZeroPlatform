using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.WinForms.Theme;
using ZeroUI.WinForms.Rendering;

namespace ZeroUI.WinForms.Data
{
    /// <summary>
    /// Custom painted scheduler control for WinForms.
    /// </summary>
    [ToolboxBitmap(typeof(MonthCalendar))]
    public class ZScheduler : Control
    {
        private IEnumerable<SchedulerEvent>? _eventSource;
        private SchedulerView _activeView = SchedulerView.Month;
        private DateTime _currentDate = DateTime.Today;
        private VScrollBar _vScrollBar;

#pragma warning disable CS0067
        public event EventHandler<SchedulerEventArgs>? EventCreated;
        public event EventHandler<SchedulerEventArgs>? EventModified;
        public event EventHandler<SchedulerEventArgs>? EventDeleted;
        public event EventHandler<SchedulerEventArgs>? EventDoubleClicked;
#pragma warning restore CS0067
        public event EventHandler<DateChangedEventArgs>? DateChanged;

        [Category("ZeroUI")]
        public IEnumerable<SchedulerEvent>? EventSource
        {
            get => _eventSource;
            set { _eventSource = value; Invalidate(); }
        }

        [Category("ZeroUI")]
        public SchedulerView ActiveView
        {
            get => _activeView;
            set { _activeView = value; Invalidate(); }
        }

        [Category("ZeroUI")]
        public DateTime CurrentDate
        {
            get => _currentDate;
            set
            {
                if (_currentDate != value)
                {
                    var old = _currentDate;
                    _currentDate = value;
                    DateChanged?.Invoke(this, new DateChangedEventArgs(old, value));
                    Invalidate();
                }
            }
        }

        [Category("ZeroUI")]
        public DayOfWeek FirstDayOfWeek { get; set; } = DayOfWeek.Sunday;

        [Category("ZeroUI")]
        public TimeSpan WorkDayStart { get; set; } = TimeSpan.FromHours(8);

        [Category("ZeroUI")]
        public TimeSpan WorkDayEnd { get; set; } = TimeSpan.FromHours(17);

        [Category("ZeroUI")]
        public bool AllowCreate { get; set; } = true;
        [Category("ZeroUI")]
        public bool AllowEdit { get; set; } = true;
        [Category("ZeroUI")]
        public bool AllowDragDrop { get; set; } = false;
        [Category("ZeroUI")]
        public bool AllowResize { get; set; } = false;
        [Category("ZeroUI")]
        public bool ShowNavigationBar { get; set; } = true;
        [Category("ZeroUI")]
        public bool ShowAllDayArea { get; set; } = true;
        [Category("ZeroUI")]
        public bool HighlightCurrentTime { get; set; } = true;

        [Browsable(false)]
        public Func<SchedulerEvent, Color>? EventColorResolver { get; set; }

        public ZScheduler()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            
            _vScrollBar = new VScrollBar { Visible = false };
            _vScrollBar.Scroll += (s, e) => Invalidate();
            Controls.Add(_vScrollBar);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _vScrollBar.Location = new Point(Width - _vScrollBar.Width, ShowNavigationBar ? 40 : 0);
            _vScrollBar.Height = Height - (ShowNavigationBar ? 40 : 0);
        }

        public void NavigateToday() => CurrentDate = DateTime.Today;
        public void NavigateNext() => CurrentDate = _activeView == SchedulerView.Month ? CurrentDate.AddMonths(1) : CurrentDate.AddDays(_activeView == SchedulerView.Week ? 7 : 1);
        public void NavigatePrevious() => CurrentDate = _activeView == SchedulerView.Month ? CurrentDate.AddMonths(-1) : CurrentDate.AddDays(_activeView == SchedulerView.Week ? -7 : -1);

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using var backBrush = new SolidBrush(ZeroTheme.Colors.Background);
            g.FillRectangle(backBrush, ClientRectangle);

            int yOffset = 0;
            if (ShowNavigationBar)
            {
                yOffset = 40;
                DrawNavigationBar(g);
            }

            var viewRect = new Rectangle(0, yOffset, Width - (_vScrollBar.Visible ? _vScrollBar.Width : 0), Height - yOffset);
            
            switch (ActiveView)
            {
                case SchedulerView.Month:
                    DrawMonthView(g, viewRect);
                    break;
                case SchedulerView.Day:
                case SchedulerView.Week:
                case SchedulerView.WorkWeek:
                case SchedulerView.Agenda:
                    // Basic placeholder for other views
                    using (var font = ZeroFontCache.Get(10, FontStyle.Regular))
                    using (var brush = new SolidBrush(ZeroTheme.Colors.TextPrimary))
                    {
                        g.DrawString($"View: {ActiveView}", font, brush, viewRect.X + 10, viewRect.Y + 10);
                    }
                    break;
            }
        }

        private void DrawNavigationBar(Graphics g)
        {
            var rect = new Rectangle(0, 0, Width, 40);
            using var brush = new SolidBrush(ZeroTheme.Colors.Surface);
            g.FillRectangle(brush, rect);
            using var borderPen = new Pen(ZeroTheme.Colors.Border);
            g.DrawLine(borderPen, 0, 39, Width, 39);

            using var font = ZeroFontCache.Get(12, FontStyle.Bold);
            using var textBrush = new SolidBrush(ZeroTheme.Colors.TextPrimary);
            g.DrawString(CurrentDate.ToString("MMMM yyyy"), font, textBrush, new RectangleF(10, 10, 200, 20));
        }

        private void DrawMonthView(Graphics g, Rectangle rect)
        {
            using var borderPen = new Pen(ZeroTheme.Colors.Border);
            
            int cellW = rect.Width / 7;
            int cellH = rect.Height / 6;

            for (int r = 0; r < 6; r++)
            {
                for (int c = 0; c < 7; c++)
                {
                    int x = rect.X + c * cellW;
                    int y = rect.Y + r * cellH;
                    g.DrawRectangle(borderPen, x, y, cellW, cellH);
                }
            }
        }
    }

    [Obsolete("Use ZScheduler instead")]
    [ToolboxItem(false)]
    public class ZeroSchedulerControl : ZScheduler { }
}
