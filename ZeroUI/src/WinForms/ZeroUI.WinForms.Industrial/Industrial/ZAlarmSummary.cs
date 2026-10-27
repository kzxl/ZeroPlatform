using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ZeroUI.Core.Scada;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    /// <summary>
    /// ISA-18.2 compliant alarm summary control.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("ISA-18.2 compliant industrial alarm summary grid")]
    public class ZAlarmSummary : Control
    {
        private readonly List<AlarmRecord> _alarms = new List<AlarmRecord>();
        private readonly VScrollBar _vScrollBar;
        private readonly System.Windows.Forms.Timer _flashTimer;
        
        private int _selectedRowIndex = -1;
        private int _hoveredRowIndex = -1;
        private bool _flashState = false;

        private const int SummaryBarHeight = 40;
        private const int ToolbarHeight = 36;
        private const int HeaderHeight = 30;
        private const int RowHeight = 28;

        public event EventHandler<AlarmActionEventArgs>? AlarmAcknowledged;
        public event EventHandler<AlarmActionEventArgs>? AlarmShelved;
        public event EventHandler<AlarmSelectedEventArgs>? AlarmDoubleClicked;

        [Category("Data")]
        public string AlarmSource { get; set; } = string.Empty;

        [Category("Filter")]
        [DefaultValue(true)]
        public bool ShowAcknowledged { get; set; } = true;

        [Category("Filter")]
        [DefaultValue(true)]
        public bool ShowSuppressed { get; set; } = true;

        [Category("Filter")]
        [DefaultValue(true)]
        public bool ShowShelved { get; set; } = true;

        [Category("Filter")]
        [DefaultValue(AlarmPriority.Diagnostic)]
        public AlarmPriority MinPriority { get; set; } = AlarmPriority.Diagnostic;

        [Category("Filter")]
        public string AreaFilter { get; set; } = string.Empty;

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool FlashOnNew { get; set; } = true;

        [Category("Behavior")]
        [DefaultValue(1000)]
        public int RefreshIntervalMs { get; set; } = 1000;

        public int ActiveCount => _alarms.Count(a => a.State == AlarmState.Active);
        public int UnacknowledgedCount => _alarms.Count(a => a.State == AlarmState.Active && a.AcknowledgedTime == null);

        public ZAlarmSummary()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.Selectable, true);

            Size = new Size(800, 400);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f);

            _vScrollBar = new VScrollBar
            {
                Dock = DockStyle.Right,
                Visible = false
            };
            _vScrollBar.ValueChanged += (s, e) => Invalidate();
            Controls.Add(_vScrollBar);

            _flashTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _flashTimer.Tick += (s, e) =>
            {
                _flashState = !_flashState;
                if (FlashOnNew && UnacknowledgedCount > 0)
                {
                    Invalidate();
                }
            };
            _flashTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _flashTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        public void SetAlarms(IEnumerable<AlarmRecord> alarms)
        {
            _alarms.Clear();
            foreach (var a in alarms)
            {
                bool include = true;
                if (!ShowAcknowledged && a.State == AlarmState.Acknowledged) include = false;
                if (!ShowSuppressed && a.State == AlarmState.Suppressed) include = false;
                if (!ShowShelved && a.State == AlarmState.Shelved) include = false;
                if (a.Priority > MinPriority) include = false;
                if (!string.IsNullOrEmpty(AreaFilter) && a.Area != AreaFilter) include = false;

                if (include) _alarms.Add(a);
            }

            UpdateScrollbar();
            Invalidate();
        }

        private void UpdateScrollbar()
        {
            int totalHeader = SummaryBarHeight + ToolbarHeight + HeaderHeight;
            int visibleRows = Math.Max(1, (Height - totalHeader) / RowHeight);
            if (_alarms.Count > visibleRows)
            {
                _vScrollBar.Maximum = _alarms.Count - visibleRows + 9; // Windows forms scrollbar adjustment
                _vScrollBar.Visible = true;
            }
            else
            {
                _vScrollBar.Value = 0;
                _vScrollBar.Visible = false;
            }
        }

        public override void Refresh()
        {
            base.Refresh();
            Invalidate();
        }

        public void AcknowledgeSelected()
        {
            if (_selectedRowIndex >= 0 && _selectedRowIndex < _alarms.Count)
            {
                var a = _alarms[_selectedRowIndex];
                if (a.State == AlarmState.Active && a.AcknowledgedTime == null)
                {
                    a.State = AlarmState.Acknowledged;
                    a.AcknowledgedTime = DateTime.Now;
                    a.AcknowledgedBy = "Operator";
                    AlarmAcknowledged?.Invoke(this, new AlarmActionEventArgs(a));
                    Invalidate();
                }
            }
        }

        public void AcknowledgeAll()
        {
            foreach (var a in _alarms.Where(x => x.State == AlarmState.Active && x.AcknowledgedTime == null))
            {
                a.State = AlarmState.Acknowledged;
                a.AcknowledgedTime = DateTime.Now;
                a.AcknowledgedBy = "Operator";
                AlarmAcknowledged?.Invoke(this, new AlarmActionEventArgs(a));
            }
            Invalidate();
        }

        public void ShelveSelected(TimeSpan duration)
        {
            if (_selectedRowIndex >= 0 && _selectedRowIndex < _alarms.Count)
            {
                var a = _alarms[_selectedRowIndex];
                a.State = AlarmState.Shelved;
                AlarmShelved?.Invoke(this, new AlarmActionEventArgs(a));
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            int totalHeader = SummaryBarHeight + ToolbarHeight + HeaderHeight;

            if (e.Y > totalHeader)
            {
                int rowIndex = _vScrollBar.Value + (e.Y - totalHeader) / RowHeight;
                if (rowIndex >= 0 && rowIndex < _alarms.Count)
                {
                    _selectedRowIndex = rowIndex;
                    Invalidate();

                    if (e.Clicks == 2)
                    {
                        var a = _alarms[rowIndex];
                        AlarmDoubleClicked?.Invoke(this, new AlarmSelectedEventArgs(a));
                    }
                }
            }
            else if (e.Y > SummaryBarHeight && e.Y <= SummaryBarHeight + ToolbarHeight)
            {
                // Basic Toolbar Clicks
                if (e.X > 100 && e.X < 200) AcknowledgeSelected();
                if (e.X > 210 && e.X < 310) AcknowledgeAll();
                if (e.X > 320 && e.X < 420) ShelveSelected(TimeSpan.FromHours(1));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int totalHeader = SummaryBarHeight + ToolbarHeight + HeaderHeight;
            if (e.Y > totalHeader)
            {
                int row = _vScrollBar.Value + (e.Y - totalHeader) / RowHeight;
                if (row != _hoveredRowIndex)
                {
                    _hoveredRowIndex = row;
                    Invalidate();
                }
            }
            else
            {
                _hoveredRowIndex = -1;
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredRowIndex = -1;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            bool isDark = ZeroTheme.IsDark;

            Color bg = isDark ? Color.FromArgb(15, 23, 42) : Color.White;
            Color panelBg = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(241, 245, 249);
            Color border = isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240);
            Color textCol = isDark ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);

            g.Clear(bg);

            // 1. Summary Bar
            using (var brush = new SolidBrush(panelBg))
                g.FillRectangle(brush, 0, 0, Width, SummaryBarHeight);
            
            DrawSummaryBar(g, textCol);

            // 2. Toolbar
            using (var brush = new SolidBrush(bg))
                g.FillRectangle(brush, 0, SummaryBarHeight, Width, ToolbarHeight);
            DrawToolbar(g, textCol, border);

            // 3. Grid Header
            int headerY = SummaryBarHeight + ToolbarHeight;
            using (var brush = new SolidBrush(panelBg))
                g.FillRectangle(brush, 0, headerY, Width, HeaderHeight);
            
            using (var p = new Pen(border))
            {
                g.DrawLine(p, 0, headerY, Width, headerY);
                g.DrawLine(p, 0, headerY + HeaderHeight, Width, headerY + HeaderHeight);
            }

            DrawGridHeader(g, headerY, textCol);

            // 4. Grid Rows
            int contentY = headerY + HeaderHeight;
            int visibleRows = Math.Max(1, (Height - contentY) / RowHeight);
            int startIdx = _vScrollBar.Value;
            int endIdx = Math.Min(_alarms.Count, startIdx + visibleRows + 1);
            int contentWidth = Width - (_vScrollBar.Visible ? _vScrollBar.Width : 0);

            for (int i = startIdx; i < endIdx; i++)
            {
                var a = _alarms[i];
                int rowY = contentY + (i - startIdx) * RowHeight;
                DrawRow(g, a, i, rowY, contentWidth, isDark, border, textCol);
            }
        }

        public int CountByPriority(AlarmPriority priority) => _alarms.Count(a => a.Priority == priority);

        private void DrawSummaryBar(Graphics g, Color textCol)
        {
            var colors = new[] { Color.Red, Color.Orange, Color.Gold, Color.DodgerBlue, Color.Gray };
            var names = new[] { "Critical", "High", "Medium", "Low", "Diagnostic" };
            
            float cellW = Width / 5f;
            for(int i=0; i<5; i++)
            {
                float x = i * cellW;
                using (var brush = new SolidBrush(colors[i]))
                    g.FillRectangle(brush, x + 5, 5, 10, SummaryBarHeight - 10);
                
                int count = CountByPriority((AlarmPriority)(i + 1));
                using (var font = new Font(Font.FontFamily, 10f, FontStyle.Bold))
                using (var brush = new SolidBrush(textCol))
                {
                    g.DrawString($"{names[i]}: {count}", font, brush, x + 25, 10);
                }
            }
        }

        private void DrawToolbar(Graphics g, Color textCol, Color border)
        {
            using (var font = new Font(Font.FontFamily, 9f))
            using (var brush = new SolidBrush(textCol))
            using (var p = new Pen(border))
            {
                g.DrawString("Filter", font, brush, 10, SummaryBarHeight + 8);
                g.DrawRectangle(p, 100, SummaryBarHeight + 5, 100, 26);
                g.DrawString("Ack Selected", font, brush, 110, SummaryBarHeight + 8);
                g.DrawRectangle(p, 210, SummaryBarHeight + 5, 100, 26);
                g.DrawString("Ack All", font, brush, 230, SummaryBarHeight + 8);
                g.DrawRectangle(p, 320, SummaryBarHeight + 5, 100, 26);
                g.DrawString("Shelve", font, brush, 340, SummaryBarHeight + 8);
            }
        }

        private void DrawGridHeader(Graphics g, int y, Color textCol)
        {
            string[] cols = { "!", "Tag Name", "Description", "Value / Limit", "Area", "State", "Time", "Duration" };
            int[] widths = { 30, 150, 200, 100, 80, 80, 100, 80 };
            
            int x = 10;
            using (var font = new Font(Font.FontFamily, 9f, FontStyle.Bold))
            using (var brush = new SolidBrush(textCol))
            {
                for (int i = 0; i < cols.Length; i++)
                {
                    g.DrawString(cols[i], font, brush, x, y + 6);
                    x += widths[i];
                }
            }
        }

        private void DrawRow(Graphics g, AlarmRecord a, int index, int rowY, int width, bool isDark, Color border, Color textCol)
        {
            var rowRect = new Rectangle(0, rowY, width, RowHeight);
            bool isSelected = index == _selectedRowIndex;
            bool isHovered = index == _hoveredRowIndex;

            bool isUnack = a.State == AlarmState.Active && a.AcknowledgedTime == null;
            bool flash = isUnack && FlashOnNew && _flashState;

            if (isSelected)
            {
                using (var brush = new SolidBrush(isDark ? Color.FromArgb(30, 58, 138) : Color.FromArgb(219, 234, 254)))
                    g.FillRectangle(brush, rowRect);
            }
            else if (isHovered)
            {
                using (var brush = new SolidBrush(isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(248, 250, 252)))
                    g.FillRectangle(brush, rowRect);
            }

            using (var p = new Pen(border))
                g.DrawLine(p, 0, rowY + RowHeight, width, rowY + RowHeight);

            Color priColor = a.Priority switch
            {
                AlarmPriority.Critical => Color.Red,
                AlarmPriority.High => Color.Orange,
                AlarmPriority.Medium => Color.Gold,
                AlarmPriority.Low => Color.DodgerBlue,
                _ => Color.Gray
            };

            if (flash) priColor = Color.White;

            using (var brush = new SolidBrush(priColor))
                g.FillRectangle(brush, 0, rowY + 2, 5, RowHeight - 4);

            string[] cols = {
                ((int)a.Priority).ToString(),
                a.TagName,
                a.Description,
                $"{a.Value} / {a.Limit}",
                a.Area ?? "",
                a.State.ToString(),
                a.ActivatedTime.ToString("HH:mm:ss"),
                (a.ClearedTime ?? DateTime.Now).Subtract(a.ActivatedTime).ToString(@"hh\:mm\:ss")
            };
            
            int[] widths = { 30, 150, 200, 100, 80, 80, 100, 80 };
            
            int x = 10;
            using (var font = new Font(Font.FontFamily, 9f))
            using (var brush = new SolidBrush(textCol))
            {
                for (int i = 0; i < cols.Length; i++)
                {
                    g.DrawString(cols[i], font, brush, x, rowY + 6);
                    x += widths[i];
                }
            }
        }
    }
    
    #region Backward Compatibility Shims

    [Obsolete("AlarmSummary is deprecated. Please migrate to ZAlarmSummary instead.")]
    [ToolboxItem(false)]
    public class AlarmSummary : ZAlarmSummary { }

    [Obsolete("ZeroAlarmSummary is deprecated. Please migrate to ZAlarmSummary instead.")]
    [ToolboxItem(false)]
    public class ZeroAlarmSummary : ZAlarmSummary { }

    #endregion
}
