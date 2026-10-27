using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroUI.Core.Security;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Security
{
    /// <summary>
    /// Industrial operation log and audit trail viewer control for compliance tracking (21 CFR Part 11, ISA-88).
    /// Supports multi-dimensional filtering, severity highlights, and streaming CSV exports.
    /// </summary>
    [ToolboxItem(true)]
    public class ZOperationLogViewer : ControlBase
    {
        private IOperationLogger? _logger;
        private readonly List<OperationLogEntry> _entries = new List<OperationLogEntry>();

        private int _scrollY;
        private int _rowHeight = 32;
        private int _headerHeight = 40;

        public event EventHandler? Refreshed;

        public IReadOnlyList<OperationLogEntry> Entries => _entries;

        public ZOperationLogViewer()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            Size = new Size(850, 420);
        }

        public async Task LoadLogsAsync(IOperationLogger logger, OperationLogFilter? filter = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var logs = await _logger.QueryLogsAsync(filter);

            _entries.Clear();
            _entries.AddRange(logs);
            _scrollY = 0;
            Invalidate();
            Refreshed?.Invoke(this, EventArgs.Empty);
        }

        public void ExportToCsv(string filePath)
        {
            using var sw = new StreamWriter(filePath, false, Encoding.UTF8);
            sw.WriteLine("TimestampUtc,Username,Action,Category,TargetResource,OldValue,NewValue,Level,Details");

            foreach (var log in _entries)
            {
                sw.WriteLine($"\"{log.TimestampUtc:O}\",\"{log.Username}\",\"{log.Action}\",\"{log.Category}\",\"{log.TargetResource}\",\"{log.OldValue}\",\"{log.NewValue}\",\"{log.Level}\",\"{log.Details}\"");
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int maxScroll = Math.Max(0, _entries.Count * _rowHeight - (Height - _headerHeight));
            _scrollY = Math.Max(0, Math.Min(maxScroll, _scrollY - e.Delta));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var skin = EffectiveSkin;
            bool isDark = skin.IsDark;

            Color backColor = isDark ? Color.FromArgb(20, 24, 33) : Color.FromArgb(255, 255, 255);
            Color headerBack = isDark ? Color.FromArgb(30, 36, 49) : Color.FromArgb(243, 244, 246);
            Color rowAltBack = isDark ? Color.FromArgb(25, 30, 42) : Color.FromArgb(249, 250, 251);
            Color borderColor = isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(229, 231, 235);
            Color textColor = isDark ? Color.FromArgb(241, 245, 249) : Color.FromArgb(15, 23, 42);
            Color subColor = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);

            g.Clear(backColor);

            int colTime = 140;
            int colUser = 100;
            int colAction = 140;
            int colCategory = 90;
            int colTarget = 150;
            int colDelta = 130;

            using var font = new Font(Font.FontFamily, 8.5f);
            using var boldFont = new Font(Font.FontFamily, 8.5f, FontStyle.Bold);
            using var borderPen = new Pen(borderColor, 1f);
            using var textBrush = new SolidBrush(textColor);
            using var subBrush = new SolidBrush(subColor);

            // Paint rows
            int startY = _headerHeight - _scrollY;
            for (int r = 0; r < _entries.Count; r++)
            {
                int y = startY + r * _rowHeight;
                if (y + _rowHeight < _headerHeight || y > Height) continue;

                var entry = _entries[r];
                if (r % 2 == 1)
                {
                    using var rowBrush = new SolidBrush(rowAltBack);
                    g.FillRectangle(rowBrush, 0, y, Width, _rowHeight);
                }

                int rx = 10;
                string timeStr = entry.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                g.DrawString(timeStr, font, subBrush, rx, y + 8); rx += colTime;
                g.DrawString(entry.Username ?? "—", boldFont, textBrush, rx, y + 8); rx += colUser;
                g.DrawString(entry.Action, font, textBrush, rx, y + 8); rx += colAction;
                g.DrawString(entry.Category, font, subBrush, rx, y + 8); rx += colCategory;
                g.DrawString(entry.TargetResource ?? "—", font, textBrush, rx, y + 8); rx += colTarget;

                string delta = (entry.OldValue != null || entry.NewValue != null)
                    ? $"{entry.OldValue ?? "—"} → {entry.NewValue ?? "—"}"
                    : "—";
                g.DrawString(delta, font, subBrush, rx, y + 8); rx += colDelta;

                // Level tag badge
                Color lvlColor = entry.Level switch
                {
                    OperationLogLevel.SecurityAudit => Color.FromArgb(168, 85, 247),
                    OperationLogLevel.Warning => Color.FromArgb(245, 158, 11),
                    OperationLogLevel.Error or OperationLogLevel.Critical => Color.FromArgb(239, 68, 68),
                    _ => Color.FromArgb(56, 189, 248)
                };
                using var lvlBrush = new SolidBrush(lvlColor);
                g.DrawString(entry.Level.ToString(), boldFont, lvlBrush, rx, y + 8);

                g.DrawLine(borderPen, 0, y + _rowHeight, Width, y + _rowHeight);
            }

            // Fixed header
            using (var hBrush = new SolidBrush(headerBack))
            {
                g.FillRectangle(hBrush, 0, 0, Width, _headerHeight);
            }
            g.DrawLine(borderPen, 0, _headerHeight, Width, _headerHeight);

            int hx = 10;
            g.DrawString("Timestamp", boldFont, textBrush, hx, 12); hx += colTime;
            g.DrawString("Operator", boldFont, textBrush, hx, 12); hx += colUser;
            g.DrawString("Action", boldFont, textBrush, hx, 12); hx += colAction;
            g.DrawString("Category", boldFont, textBrush, hx, 12); hx += colCategory;
            g.DrawString("Resource", boldFont, textBrush, hx, 12); hx += colTarget;
            g.DrawString("Old → New", boldFont, textBrush, hx, 12); hx += colDelta;
            g.DrawString("Severity", boldFont, textBrush, hx, 12);
        }
    }
}
