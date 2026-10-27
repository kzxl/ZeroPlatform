using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Common;
using ZeroUI.Core.LifeSciences;
using ZeroUI.Core.Rendering;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.LifeSciences
{
    /// <summary>
    /// 96/384-Well ANSI/SLAS standard microplate reader assay heatmap visualizer.
    /// Features optical density (OD) absorbance heatmaps, standard curve dilution series,
    /// well selection, replicate statistics, and outlier detection.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Life Sciences")]
    [Description("ANSI/SLAS 96/384-well microplate absorbance/fluorescence reader heatmap with well selection")]
    public class ZMicroplateReader : VisualControlBase
    {
        private readonly MicroplateEngine _engine;
        private MicroplateWell? _selectedWell;

        public event EventHandler? SelectedWellChanged;

        protected override bool AutoAnimate => true;

        public ZMicroplateReader() : this(PlateFormat.Wells96)
        {
        }

        public ZMicroplateReader(PlateFormat format)
        {
            _engine = new MicroplateEngine(format);
            Size = new Size(780, 440);
            MouseDown += OnPlateMouseDown;
        }

        #region Public Properties

        [Category("Microplate")]
        [Description("Access to the underlying microplate assay computation engine")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public MicroplateEngine Engine => _engine;

        [Category("Microplate")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public MicroplateWell? SelectedWell
        {
            get => _selectedWell;
            set
            {
                if (_selectedWell != value)
                {
                    if (_selectedWell != null) _selectedWell.IsSelected = false;
                    _selectedWell = value;
                    if (_selectedWell != null) _selectedWell.IsSelected = true;
                    SelectedWellChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        #endregion

        private void OnPlateMouseDown(object? sender, MouseEventArgs e)
        {
            int gridLeft = 40;
            int gridTop = 64;
            int gridWidth = Width - gridLeft - 180;
            int gridHeight = Height - gridTop - 20;

            if (gridWidth <= 0 || gridHeight <= 0) return;

            int rows = _engine.RowCount;
            int cols = _engine.ColCount;
            float cellW = (float)gridWidth / cols;
            float cellH = (float)gridHeight / rows;

            if (e.X >= gridLeft && e.X <= gridLeft + gridWidth &&
                e.Y >= gridTop && e.Y <= gridTop + gridHeight)
            {
                int c = (int)((e.X - gridLeft) / cellW);
                int r = (int)((e.Y - gridTop) / cellH);

                if (r >= 0 && r < rows && c >= 0 && c < cols)
                {
                    SelectedWell = _engine.Wells[r, c];
                }
            }
        }

        protected override void OnDrawVisual(Graphics g, Rectangle bounds, ZeroThemePalette palette)
        {
            // Header Banner
            int headerH = DrawHeader(g, bounds, palette);

            // Layout
            int gridLeft = bounds.X + 36;
            int gridTop = bounds.Y + headerH + 6;
            int rightPanelW = Math.Min(160, Math.Max(120, (int)(bounds.Width * 0.32f)));
            int gridWidth = bounds.Width - 36 - rightPanelW - 14;
            int gridHeight = bounds.Height - headerH - 22;

            if (gridWidth < 120 || gridHeight < 100) return;

            Rectangle gridRect = new Rectangle(gridLeft, gridTop, gridWidth, gridHeight);
            Rectangle sideRect = new Rectangle(gridLeft + gridWidth + 12, gridTop, rightPanelW, gridHeight);

            DrawMicroplateGrid(g, gridRect, palette);
            DrawSidebar(g, sideRect, palette);
        }

        private int DrawHeader(Graphics g, Rectangle bounds, ZeroThemePalette theme)
        {
            using (var fontTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var fontSmall = new Font("Segoe UI", 8.5f))
            using (var fontBold = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                Size titleSize = TextRenderer.MeasureText(g, _engine.AssayTitle, fontTitle);
                string rangeStr = $"Range: {_engine.MinValue:F3}-{_engine.MaxValue:F3} OD | Blank, Std, Ctrl";
                string formatText = _engine.Format == PlateFormat.Wells96 ? "96-WELL" : "384-WELL";

                int badgeW = 90;
                int badgeH = 24;

                if (bounds.Width >= 520)
                {
                    TextRenderer.DrawText(g, _engine.AssayTitle, fontTitle, new Point(bounds.X + 20, bounds.Y + 12), theme.TextPrimary);
                    TextRenderer.DrawText(g, rangeStr, fontSmall, new Point(bounds.X + 20, bounds.Y + 34), theme.TextSecondary);

                    Rectangle pillRect = new Rectangle(bounds.Right - badgeW - 20, bounds.Y + 14, badgeW, badgeH);
                    using (var pillBrush = new SolidBrush(Color.FromArgb(25, 59, 130, 246)))
                    using (var pillPen = new Pen(Color.FromArgb(59, 130, 246), 1f))
                    {
                        g.FillRectangle(pillBrush, pillRect);
                        g.DrawRectangle(pillPen, pillRect);
                    }
                    TextRenderer.DrawText(g, formatText, fontBold, pillRect, Color.FromArgb(59, 130, 246), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    return 56;
                }
                else
                {
                    Rectangle titleRect = new Rectangle(bounds.X + 20, bounds.Y + 8, bounds.Width - badgeW - 44, 22);
                    TextRenderer.DrawText(g, _engine.AssayTitle, fontTitle, titleRect, theme.TextPrimary,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

                    Rectangle pillRect = new Rectangle(bounds.Right - badgeW - 16, bounds.Y + 8, badgeW, badgeH);
                    using (var pillBrush = new SolidBrush(Color.FromArgb(25, 59, 130, 246)))
                    using (var pillPen = new Pen(Color.FromArgb(59, 130, 246), 1f))
                    {
                        g.FillRectangle(pillBrush, pillRect);
                        g.DrawRectangle(pillPen, pillRect);
                    }
                    TextRenderer.DrawText(g, formatText, fontBold, pillRect, Color.FromArgb(59, 130, 246), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                    Rectangle rangeRect = new Rectangle(bounds.X + 20, bounds.Y + 32, bounds.Width - 40, 18);
                    TextRenderer.DrawText(g, rangeStr, fontSmall, rangeRect, theme.TextSecondary,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    return 54;
                }
            }
        }

        private void DrawMicroplateGrid(Graphics g, Rectangle rect, ZeroThemePalette theme)
        {
            // Plate Frame border
            using (var frameBrush = new SolidBrush(Color.FromArgb(10, theme.TextPrimary)))
            using (var framePen = new Pen(theme.Border, 1.5f))
            {
                g.FillRectangle(frameBrush, rect);
                g.DrawRectangle(framePen, rect);
            }

            int rows = _engine.RowCount;
            int cols = _engine.ColCount;
            float cellW = (float)rect.Width / cols;
            float cellH = (float)rect.Height / rows;
            float wellDiameter = Math.Min(cellW, cellH) * 0.72f;

            double minVal = _engine.MinValue;
            double maxVal = _engine.MaxValue;
            double valSpan = Math.Max(0.001, maxVal - minVal);

            using (var fontHeader = new Font("Segoe UI", cellW < 18 ? 6f : 7f, FontStyle.Bold))
            {
                // Column Labels (1..12 or 1..24) along top with adaptive skipping when narrow
                int colStep = cellW < 14 ? 4 : cellW < 22 ? 2 : 1;
                for (int c = 0; c < cols; c++)
                {
                    if (c % colStep != 0 && c != cols - 1) continue;

                    float cx = rect.X + c * cellW + cellW / 2;
                    Rectangle colRect = new Rectangle((int)cx - 10, rect.Y - 16, 20, 14);
                    TextRenderer.DrawText(g, $"{c + 1}", fontHeader, colRect, theme.TextSecondary, TextFormatFlags.HorizontalCenter);
                }

                // Row Labels (A..H or A..P) along left
                for (int r = 0; r < rows; r++)
                {
                    float cy = rect.Y + r * cellH + cellH / 2;
                    char rChar = (char)('A' + r);
                    Rectangle rowRect = new Rectangle(rect.X - 22, (int)cy - 8, 18, 16);
                    TextRenderer.DrawText(g, rChar.ToString(), fontHeader, rowRect, theme.TextSecondary, TextFormatFlags.HorizontalCenter);
                }

                // Wells
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        var well = _engine.Wells[r, c];
                        float cx = rect.X + c * cellW + cellW / 2;
                        float cy = rect.Y + r * cellH + cellH / 2;
                        RectangleF wellRect = new RectangleF(cx - wellDiameter / 2, cy - wellDiameter / 2, wellDiameter, wellDiameter);

                        // Heatmap color interpolation via Core ColorScaleHelper
                        float norm = (float)Math.Max(0.0, Math.Min(1.0, (well.Value - minVal) / valSpan));
                        (byte rVal, byte gVal, byte bVal) = ColorScaleHelper.InterpolateHeatmapRgb(norm);
                        Color wellColor = Color.FromArgb(rVal, gVal, bVal);

                        using (var wBrush = new SolidBrush(wellColor))
                        {
                            g.FillEllipse(wBrush, wellRect);
                        }

                        // Outlier ring
                        if (well.IsOutlier)
                        {
                            using (var outPen = new Pen(Color.FromArgb(239, 68, 68), 2f))
                            {
                                g.DrawEllipse(outPen, wellRect);
                            }
                        }

                        // Selection indicator
                        if (well.IsSelected)
                        {
                            using (var selPen = new Pen(Color.White, 2.5f))
                            {
                                g.DrawEllipse(selPen, cx - wellDiameter / 2 - 2, cy - wellDiameter / 2 - 2, wellDiameter + 4, wellDiameter + 4);
                            }
                        }
                    }
                }
            }
        }

        private void DrawSidebar(Graphics g, Rectangle rect, ZeroThemePalette theme)
        {
            using (var boxBrush = new SolidBrush(Color.FromArgb(12, theme.TextPrimary)))
            using (var borderPen = new Pen(theme.Border, 1f))
            using (var fontTitle = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var fontLabel = new Font("Segoe UI", 8f))
            using (var fontVal = new Font("Segoe UI", 9f, FontStyle.Bold))
            {
                g.FillRectangle(boxBrush, rect);
                g.DrawRectangle(borderPen, rect);

                TextRenderer.DrawText(g, "WELL INSPECTION", fontTitle, new Point(rect.X + 12, rect.Y + 10), theme.TextSecondary);

                int curY = rect.Y + 34;
                if (_selectedWell != null)
                {
                    TextRenderer.DrawText(g, $"Well ID: {_selectedWell.WellId}", fontVal, new Point(rect.X + 12, curY), theme.TextPrimary);
                    curY += 22;
                    TextRenderer.DrawText(g, $"Type: {_selectedWell.SampleType}", fontLabel, new Point(rect.X + 12, curY), theme.TextSecondary);
                    curY += 22;
                    TextRenderer.DrawText(g, $"OD450: {_selectedWell.Value:F3}", fontVal, new Point(rect.X + 12, curY), Color.FromArgb(59, 130, 246));
                    curY += 22;
                    TextRenderer.DrawText(g, $"Conc: {_selectedWell.Concentration:F1} ng/mL", fontLabel, new Point(rect.X + 12, curY), theme.TextPrimary);

                    if (_selectedWell.IsOutlier)
                    {
                        curY += 24;
                        TextRenderer.DrawText(g, "[FLAGGED OUTLIER]", fontVal, new Point(rect.X + 12, curY), Color.FromArgb(239, 68, 68));
                    }
                }
                else
                {
                    TextRenderer.DrawText(g, "Click a well to view", fontLabel, new Point(rect.X + 12, curY), theme.TextSecondary);
                    TextRenderer.DrawText(g, "assay telemetry & conc.", fontLabel, new Point(rect.X + 12, curY + 18), theme.TextSecondary);
                }

                // Legend at bottom of sidebar
                int legY = rect.Bottom - 110;
                TextRenderer.DrawText(g, "HEATMAP SCALE", fontTitle, new Point(rect.X + 12, legY), theme.TextSecondary);
                legY += 20;

                Rectangle legBar = new Rectangle(rect.X + 12, legY, rect.Width - 24, 12);
                using (var legBrush = new LinearGradientBrush(legBar, Color.FromArgb(30, 58, 138), Color.FromArgb(239, 68, 68), LinearGradientMode.Horizontal))
                {
                    g.FillRectangle(legBrush, legBar);
                }
                legY += 16;
                TextRenderer.DrawText(g, "Low OD", fontLabel, new Point(rect.X + 12, legY), theme.TextSecondary);
                TextRenderer.DrawText(g, "High OD", fontLabel, new Point(rect.Right - 55, legY), theme.TextSecondary);
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZMicroplateReader"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("MicroplateReader is deprecated and will be removed in 5 release cycles. Please migrate to ZMicroplateReader instead.")]
    [ToolboxItem(false)]
    public class MicroplateReader : ZMicroplateReader
    {
    }

    #endregion
}
