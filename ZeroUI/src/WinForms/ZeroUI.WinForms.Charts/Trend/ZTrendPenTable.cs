using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// Real-time statistical metrics table displaying channel summaries (Live, Min, Max, Mean, StdDev)
    /// and channel visibility toggles for the TrendPlot.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Charts & Analytics")]
    public class ZTrendPenTable : Control
    {
        private ZTrendPlot? _plot;
        private int _rowHeight = 28;
        private int _headerHeight = 26;

        public ZTrendPlot? Plot
        {
            get => _plot;
            set
            {
                if (_plot != value)
                {
                    _plot = value;
                    Invalidate();
                }
            }
        }

        public ZTrendPenTable()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);

            Size = new Size(600, 120);
            Font = new Font("Segoe UI", 8.5f);
            BackColor = Color.FromArgb(15, 23, 42);

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            bool isDark = ZeroTheme.IsDark;
            Color bgColor = isDark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(248, 250, 252);
            Color headerBg = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(226, 232, 240);
            Color gridLine = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(226, 232, 240);
            Color textColor = isDark ? Color.FromArgb(226, 232, 240) : Color.FromArgb(30, 41, 59);
            Color mutedText = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);

            g.Clear(bgColor);

            if (_plot == null || _plot.Pens.Count == 0)
            {
                using var b = new SolidBrush(mutedText);
                g.DrawString("No trend pens attached.", Font, b, 12, 12);
                return;
            }

            // Columns layout:
            // 0: Vis (32px), 1: Color (24px), 2: Name (150px), 3: Live (75px), 4: Min (65px),
            // 5: Max (65px), 6: Mean (65px), 7: StdDev (65px), 8: Unit (50px), 9: Scale (80px)
            int[] colWidths = { 34, 26, 150, 75, 65, 65, 65, 65, 50, 80 };
            string[] headers = { "Vis", "Pen", "Channel Name", "Latest", "Min", "Max", "Mean", "StdDev", "Unit", "Axis Scale" };

            // 1. Draw Table Header
            using (var hBrush = new SolidBrush(headerBg))
            {
                g.FillRectangle(hBrush, 0, 0, Width, _headerHeight);
            }

            using var hFont = new Font(Font.FontFamily, 8f, FontStyle.Bold);
            using var hTextBrush = new SolidBrush(textColor);
            int curX = 0;

            for (int c = 0; c < headers.Length; c++)
            {
                int w = colWidths[c];
                var r = new Rectangle(curX, 0, w, _headerHeight);
                var sf = new StringFormat
                {
                    Alignment = c >= 3 && c <= 7 ? StringAlignment.Far : StringAlignment.Near,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(headers[c], hFont, hTextBrush, r, sf);
                curX += w;
            }

            // 2. Draw Pen Rows
            var pens = _plot.Pens;
            using var rowFont = new Font(Font.FontFamily, 8.5f, FontStyle.Regular);
            using var gridPen = new Pen(gridLine, 1f);

            DateTime start = _plot.StartTime;
            DateTime end = _plot.EndTime;

            for (int r = 0; r < pens.Count; r++)
            {
                var pen = pens[r];
                int y = _headerHeight + r * _rowHeight;
                if (y > Height) break;

                var stats = pen.GetVisibleStatistics(start, end);
                var (min, max) = pen.GetEffectiveRange(start, end);

                curX = 0;
                for (int c = 0; c < headers.Length; c++)
                {
                    int w = colWidths[c];
                    var cellRect = new Rectangle(curX, y, w, _rowHeight);

                    switch (c)
                    {
                        case 0: // Checkbox Vis
                            int cbSize = 14;
                            int cbX = cellRect.X + (w - cbSize) / 2;
                            int cbY = cellRect.Y + (_rowHeight - cbSize) / 2;
                            using (var cbBorder = new Pen(pen.Color, 1.5f))
                            {
                                g.DrawRectangle(cbBorder, cbX, cbY, cbSize, cbSize);
                            }
                            if (pen.IsVisible)
                            {
                                using var cbFill = new SolidBrush(pen.Color);
                                g.FillRectangle(cbFill, cbX + 3, cbY + 3, cbSize - 5, cbSize - 5);
                            }
                            break;

                        case 1: // Color Swatch
                            int swSize = 10;
                            int swX = cellRect.X + (w - swSize) / 2;
                            int swY = cellRect.Y + (_rowHeight - swSize) / 2;
                            using (var swBrush = new SolidBrush(pen.Color))
                            {
                                g.FillEllipse(swBrush, swX, swY, swSize, swSize);
                            }
                            break;

                        case 2: // Channel Name
                            using (var b = new SolidBrush(pen.IsVisible ? textColor : mutedText))
                            {
                                var sf = new StringFormat { LineAlignment = StringAlignment.Center };
                                g.DrawString(pen.Name, rowFont, b, cellRect, sf);
                            }
                            break;

                        case 3: // Latest
                            using (var b = new SolidBrush(pen.Color))
                            {
                                var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                                g.DrawString(pen.LatestValue.ToString("F1"), hFont, b, cellRect, sf);
                            }
                            break;

                        case 4: // Min
                            using (var b = new SolidBrush(textColor))
                            {
                                var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                                g.DrawString(stats.Count > 0 ? stats.Min.ToString("F1") : "-", rowFont, b, cellRect, sf);
                            }
                            break;

                        case 5: // Max
                            using (var b = new SolidBrush(textColor))
                            {
                                var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                                g.DrawString(stats.Count > 0 ? stats.Max.ToString("F1") : "-", rowFont, b, cellRect, sf);
                            }
                            break;

                        case 6: // Mean
                            using (var b = new SolidBrush(textColor))
                            {
                                var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                                g.DrawString(stats.Count > 0 ? stats.Average.ToString("F1") : "-", rowFont, b, cellRect, sf);
                            }
                            break;

                        case 7: // StdDev
                            using (var b = new SolidBrush(mutedText))
                            {
                                var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                                g.DrawString(stats.Count > 0 ? stats.StdDev.ToString("F2") : "-", rowFont, b, cellRect, sf);
                            }
                            break;

                        case 8: // Unit
                            using (var b = new SolidBrush(mutedText))
                            {
                                var sf = new StringFormat { LineAlignment = StringAlignment.Center };
                                g.DrawString(pen.Unit, rowFont, b, cellRect, sf);
                            }
                            break;

                        case 9: // Axis Scale
                            using (var b = new SolidBrush(mutedText))
                            {
                                var sf = new StringFormat { LineAlignment = StringAlignment.Center };
                                string scaleText = pen.AutoScale ? $"Auto [{min:F0}..{max:F0}]" : $"Fixed [{pen.ManualMin:F0}..{pen.ManualMax:F0}]";
                                g.DrawString(scaleText, rowFont, b, cellRect, sf);
                            }
                            break;
                    }

                    curX += w;
                }

                // Row bottom separator
                g.DrawLine(gridPen, 0, y + _rowHeight, Width, y + _rowHeight);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_plot == null || _plot.Pens.Count == 0) return;

            if (e.Y > _headerHeight)
            {
                int rowIndex = (e.Y - _headerHeight) / _rowHeight;
                if (rowIndex >= 0 && rowIndex < _plot.Pens.Count)
                {
                    // Toggle visibility
                    var pen = _plot.Pens[rowIndex];
                    pen.IsVisible = !pen.IsVisible;
                    Invalidate();
                    _plot.Invalidate();
                }
            }
        }
    
    private void OnThemeChanged(object sender, EventArgs e) => Invalidate();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }

}

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZTrendPenTable"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("TrendPenTable is deprecated and will be removed in 5 release cycles. Please migrate to ZTrendPenTable instead.")]
    [ToolboxItem(false)]
    public class TrendPenTable : ZTrendPenTable
    {
    }

    #endregion
}
