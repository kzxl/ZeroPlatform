using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Scada;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    public enum AlarmFilterMode
    {
        All,
        ActiveOnly,
        UnacknowledgedOnly,
        ShelvedOnly
    }

    /// <summary>
    /// High-performance ISA-18.2 compliant industrial alarm grid control.
    /// Provides real-time event filtering (Active/Ack/Shelved), color-coded severity badges,
    /// and operator acknowledgment interaction directly tied to ScadaAlarmEngine.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroAlarmGrid.bmp")]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("High-performance ISA-18.2 compliant industrial alarm grid")]
    public class ZAlarmGrid : Control
    {
        private readonly List<ScadaAlarmRecord> _filteredAlarms = new List<ScadaAlarmRecord>();
        private AlarmFilterMode _filterMode = AlarmFilterMode.ActiveOnly;
        private int _selectedRowIndex = -1;
        private int _hoveredRowIndex = -1;
        private readonly VScrollBar _vScrollBar;
        private string _operatorName = "Operator";

        private const int HeaderHeight = 36;
        private const int RowHeight = 28;

        [Category("Operator Context")]
        [DefaultValue("Operator")]
        public string OperatorName
        {
            get => _operatorName;
            set => _operatorName = value ?? "Operator";
        }

        [Category("Filter")]
        [DefaultValue(AlarmFilterMode.ActiveOnly)]
        public AlarmFilterMode FilterMode
        {
            get => _filterMode;
            set
            {
                _filterMode = value;
                ReloadAlarms();
            }
        }

        public ZAlarmGrid()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.Selectable, true);

            Size = new Size(600, 260);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f);

            _vScrollBar = new VScrollBar
            {
                Dock = DockStyle.Right,
                Visible = false
            };
            _vScrollBar.ValueChanged += (s, e) => Invalidate();
            Controls.Add(_vScrollBar);

            ScadaAlarmEngine.AlarmStateChanged += OnAlarmEngineChanged;
            ReloadAlarms();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ScadaAlarmEngine.AlarmStateChanged -= OnAlarmEngineChanged;
            }
            base.Dispose(disposing);
        }

        private void OnAlarmEngineChanged(ScadaAlarmRecord record)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(ReloadAlarms));
            }
            else
            {
                ReloadAlarms();
            }
        }

        public void ReloadAlarms()
        {
            _filteredAlarms.Clear();
            var all = ScadaAlarmEngine.GetAllAlarms();

            for (int i = 0; i < all.Count; i++)
            {
                var a = all[i];
                bool include = _filterMode switch
                {
                    AlarmFilterMode.All => true,
                    AlarmFilterMode.ActiveOnly => a.IsActive,
                    AlarmFilterMode.UnacknowledgedOnly => a.NeedsAck,
                    AlarmFilterMode.ShelvedOnly => a.State == ScadaAlarmState.Shelved,
                    _ => true
                };

                if (include)
                {
                    _filteredAlarms.Add(a);
                }
            }

            // Update Scrollbar
            int visibleRows = Math.Max(1, (Height - HeaderHeight) / RowHeight);
            if (_filteredAlarms.Count > visibleRows)
            {
                _vScrollBar.Maximum = _filteredAlarms.Count - visibleRows;
                _vScrollBar.Visible = true;
            }
            else
            {
                _vScrollBar.Value = 0;
                _vScrollBar.Visible = false;
            }

            Invalidate();
        }

        public void AcknowledgeSelected()
        {
            if (_selectedRowIndex >= 0 && _selectedRowIndex < _filteredAlarms.Count)
            {
                var rec = _filteredAlarms[_selectedRowIndex];
                ScadaAlarmEngine.Acknowledge(rec.Id, _operatorName);
            }
        }

        public void AcknowledgeAll()
        {
            ScadaAlarmEngine.AcknowledgeAll(_operatorName);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (e.Y > HeaderHeight)
            {
                int rowIndex = _vScrollBar.Value + (e.Y - HeaderHeight) / RowHeight;
                if (rowIndex >= 0 && rowIndex < _filteredAlarms.Count)
                {
                    _selectedRowIndex = rowIndex;
                    Invalidate();

                    // Double click to acknowledge
                    if (e.Clicks == 2)
                    {
                        AcknowledgeSelected();
                    }
                }
            }
            else
            {
                // Filter Tab Clicking in Header
                float tabW = 85f;
                int tabIdx = (int)(e.X / tabW);
                if (tabIdx >= 0 && tabIdx < 4)
                {
                    FilterMode = (AlarmFilterMode)tabIdx;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.Y > HeaderHeight)
            {
                int row = _vScrollBar.Value + (e.Y - HeaderHeight) / RowHeight;
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

            Color gridBg = isDark ? Color.FromArgb(15, 23, 42) : Color.White;
            Color headerBg = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(241, 245, 249);
            Color borderCol = isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240);
            Color textCol = isDark ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);

            // 1. Grid Background
            g.Clear(gridBg);

            // 2. Header Bar & Filter Tabs
            using (var hBrush = new SolidBrush(headerBg))
            using (var bPen = new Pen(borderCol, 1f))
            {
                g.FillRectangle(hBrush, 0, 0, Width, HeaderHeight);
                g.DrawLine(bPen, 0, HeaderHeight, Width, HeaderHeight);
            }

            string[] tabs = { "ALL", "ACTIVE", "UNACK", "SHELVED" };
            float tabW = 85f;
            using (var tabFont = new Font(Font.FontFamily, 8f, FontStyle.Bold))
            {
                for (int i = 0; i < 4; i++)
                {
                    bool isTabActive = (int)_filterMode == i;
                    var tabRect = new RectangleF(i * tabW, 0, tabW, HeaderHeight);

                    if (isTabActive)
                    {
                        using (var selTabBrush = new SolidBrush(isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240)))
                        using (var lineBrush = new SolidBrush(Color.FromArgb(59, 130, 246)))
                        {
                            g.FillRectangle(selTabBrush, tabRect);
                            g.FillRectangle(lineBrush, tabRect.X, tabRect.Bottom - 3f, tabRect.Width, 3f);
                        }
                    }

                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    using (var tBrush = new SolidBrush(isTabActive ? Color.FromArgb(59, 130, 246) : textCol))
                    {
                        g.DrawString(tabs[i], tabFont, tBrush, tabRect, sf);
                    }
                }
            }

            // 3. Render Alarm Rows
            int visibleRows = Math.Max(1, (Height - HeaderHeight) / RowHeight);
            int startIdx = _vScrollBar.Value;
            int endIdx = Math.Min(_filteredAlarms.Count, startIdx + visibleRows + 1);

            int contentWidth = Width - (_vScrollBar.Visible ? _vScrollBar.Width : 0);

            for (int i = startIdx; i < endIdx; i++)
            {
                var a = _filteredAlarms[i];
                int rowY = HeaderHeight + (i - startIdx) * RowHeight;
                var rowRect = new Rectangle(0, rowY, contentWidth, RowHeight);

                bool isSelected = i == _selectedRowIndex;
                bool isHovered = i == _hoveredRowIndex;

                if (isSelected)
                {
                    using (var selBrush = new SolidBrush(isDark ? Color.FromArgb(30, 58, 138) : Color.FromArgb(219, 234, 254)))
                    {
                        g.FillRectangle(selBrush, rowRect);
                    }
                }
                else if (isHovered)
                {
                    using (var hovBrush = new SolidBrush(isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(248, 250, 252)))
                    {
                        g.FillRectangle(hovBrush, rowRect);
                    }
                }

                // Row bottom separator line
                using (var pen = new Pen(borderCol, 1f))
                {
                    g.DrawLine(pen, 0, rowY + RowHeight, contentWidth, rowY + RowHeight);
                }

                // Severity Pill Badge
                Color sevColor = a.Severity switch
                {
                    ScadaAlarmSeverity.Critical => Color.FromArgb(239, 68, 68),
                    ScadaAlarmSeverity.High => Color.FromArgb(249, 115, 22),
                    ScadaAlarmSeverity.Medium => Color.FromArgb(245, 158, 11),
                    ScadaAlarmSeverity.Low => Color.FromArgb(59, 130, 246),
                    _ => Color.FromArgb(100, 116, 139)
                };

                var badgeRect = new Rectangle(8, rowY + 5, 55, 18);
                using (var badgeBrush = new SolidBrush(sevColor))
                using (var badgeTextBrush = new SolidBrush(Color.White))
                using (var badgeFont = new Font(Font.FontFamily, 7f, FontStyle.Bold))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.FillRectangle(badgeBrush, badgeRect);
                    g.DrawString(a.Severity.ToString().ToUpperInvariant(), badgeFont, badgeTextBrush, badgeRect, sf);
                }

                // Columns: Timestamp, Tag, Description, State
                using (var cellFont = new Font(Font.FontFamily, 8.5f, FontStyle.Regular))
                using (var cellBrush = new SolidBrush(textCol))
                {
                    string timeStr = a.ActiveTimestamp.ToLocalTime().ToString("HH:mm:ss");
                    g.DrawString(timeStr, cellFont, cellBrush, 70, rowY + 6);

                    using (var boldFont = new Font(Font.FontFamily, 8.5f, FontStyle.Bold))
                    {
                        g.DrawString(a.TagPath, boldFont, cellBrush, 135, rowY + 6);
                    }

                    g.DrawString(a.Description, cellFont, cellBrush, 240, rowY + 6);

                    // State pill/string
                    string stateStr = a.State switch
                    {
                        ScadaAlarmState.ActiveUnacknowledged => "UNACK",
                        ScadaAlarmState.ActiveAcknowledged => "ACKED",
                        ScadaAlarmState.ClearedUnacknowledged => "CLR-UNACK",
                        ScadaAlarmState.Shelved => "SHELVED",
                        _ => "NORM"
                    };
                    g.DrawString(stateStr, cellFont, cellBrush, contentWidth - 85, rowY + 6);
                }
            }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZAlarmGrid"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroAlarmGrid is deprecated and will be removed in 5 release cycles. Please migrate to ZAlarmGrid instead.")]
    [ToolboxItem(false)]
    public class ZeroAlarmGrid : ZAlarmGrid
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZAlarmGrid"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("AlarmGrid is deprecated and will be removed in 5 release cycles. Please migrate to ZAlarmGrid instead.")]
    [ToolboxItem(false)]
    public class AlarmGrid : ZAlarmGrid
    {
    }

    #endregion
}
