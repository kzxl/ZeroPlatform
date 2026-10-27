using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Common;
using ZeroUI.Core.LifeSciences;
using ZeroUI.Core.Rendering;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.LifeSciences
{
    /// <summary>
    /// 96/384-Well ANSI/SLAS standard microplate reader assay heatmap visualizer in WPF.
    /// Features optical density (OD) absorbance heatmaps, standard curve dilution series,
    /// well selection, replicate statistics, and outlier detection.
    /// </summary>
    public class ZMicroplateReader : ZeroWpfVisualBase
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
            MouseDown += OnPlateMouseDown;
        }

        #region Properties

        public MicroplateEngine Engine => _engine;

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
                    InvalidateVisual();
                }
            }
        }

        #endregion

        private void OnPlateMouseDown(object sender, MouseButtonEventArgs e)
        {
            Point pt = e.GetPosition(this);
            double gridLeft = 40;
            double gridTop = 64;
            double rightPanelW = 160;
            double gridWidth = ActualWidth - gridLeft - rightPanelW - 20;
            double gridHeight = ActualHeight - gridTop - 20;

            if (gridWidth <= 0 || gridHeight <= 0) return;

            int rows = _engine.RowCount;
            int cols = _engine.ColCount;
            double cellW = gridWidth / cols;
            double cellH = gridHeight / rows;

            if (pt.X >= gridLeft && pt.X <= gridLeft + gridWidth &&
                pt.Y >= gridTop && pt.Y <= gridTop + gridHeight)
            {
                int c = (int)((pt.X - gridLeft) / cellW);
                int r = (int)((pt.Y - gridTop) / cellH);

                if (r >= 0 && r < rows && c >= 0 && c < cols)
                {
                    SelectedWell = _engine.Wells[r, c];
                }
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 240 || h < 140)
                return;

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, new Rect(0, 0, w, h));

            // Header Banner
            DrawHeader(dc, w, dpi);

            // Layout
            double gridLeft = 40;
            double gridTop = 64;
            double rightPanelW = 160;
            double gridWidth = w - gridLeft - rightPanelW - 20;
            double gridHeight = h - gridTop - 20;

            if (gridWidth < 120 || gridHeight < 80) return;

            Rect gridRect = new Rect(gridLeft, gridTop, gridWidth, gridHeight);
            Rect sideRect = new Rect(gridLeft + gridWidth + 14, gridTop, rightPanelW, gridHeight);

            DrawMicroplateGrid(dc, gridRect, dpi);
            DrawSidebar(dc, sideRect, dpi);
        }

        private void DrawHeader(DrawingContext dc, double w, double dpi)
        {
            var titleText = CreateFormattedText(_engine.AssayTitle, ZeroWpfTheme.BoldTypeface, 13, ZeroWpfTheme.TextPrimary, dpi);
            dc.DrawText(titleText, new Point(20, 14));

            string rangeStr = $"Range: {_engine.MinValue:F3} - {_engine.MaxValue:F3} OD | Replicates: Blank, Std, Ctrl, Unk";
            var rangeText = CreateFormattedText(rangeStr, ZeroWpfTheme.RegularTypeface, 10.5, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(rangeText, new Point(20, 36));

            // Format Pill
            string formatText = _engine.Format == PlateFormat.Wells96 ? "96-WELL (8x12)" : "384-WELL (16x24)";
            Rect pillRect = new Rect(w - 140, 14, 120, 24);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(25, 59, 130, 246)),
                new Pen(new SolidColorBrush(Color.FromRgb(59, 130, 246)), 1), pillRect, 4, 4);

            var fText = CreateFormattedText(formatText, ZeroWpfTheme.BoldTypeface, 10, new SolidColorBrush(Color.FromRgb(59, 130, 246)), dpi);
            dc.DrawText(fText, new Point(pillRect.X + (pillRect.Width - fText.Width) / 2, pillRect.Y + 4));
        }

        private void DrawMicroplateGrid(DrawingContext dc, Rect rect, double dpi)
        {
            dc.DrawRoundedRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, rect, 4, 4);

            int rows = _engine.RowCount;
            int cols = _engine.ColCount;
            double cellW = rect.Width / cols;
            double cellH = rect.Height / rows;
            double wellDiameter = Math.Min(cellW, cellH) * 0.72;

            double minVal = _engine.MinValue;
            double maxVal = _engine.MaxValue;
            double valSpan = Math.Max(0.001, maxVal - minVal);

            // Column Labels
            for (int c = 0; c < cols; c++)
            {
                double cx = rect.X + c * cellW + cellW / 2;
                var colTxt = CreateFormattedText($"{c + 1}", ZeroWpfTheme.BoldTypeface, 9, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(colTxt, new Point(cx - colTxt.Width / 2, rect.Y - 18));
            }

            // Row Labels
            for (int r = 0; r < rows; r++)
            {
                double cy = rect.Y + r * cellH + cellH / 2;
                char rChar = (char)('A' + r);
                var rowTxt = CreateFormattedText(rChar.ToString(), ZeroWpfTheme.BoldTypeface, 9, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(rowTxt, new Point(rect.X - 16, cy - rowTxt.Height / 2));
            }

            // Wells
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    var well = _engine.Wells[r, c];
                    double cx = rect.X + c * cellW + cellW / 2;
                    double cy = rect.Y + r * cellH + cellH / 2;

                    float norm = (float)Math.Max(0.0, Math.Min(1.0, (well.Value - minVal) / valSpan));
                    (byte rVal, byte gVal, byte bVal) = ColorScaleHelper.InterpolateHeatmapRgb(norm);
                    Color wellCol = Color.FromRgb(rVal, gVal, bVal);

                    dc.DrawEllipse(new SolidColorBrush(wellCol), null, new Point(cx, cy), wellDiameter / 2, wellDiameter / 2);

                    if (well.IsOutlier)
                    {
                        dc.DrawEllipse(null, new Pen(ZeroWpfTheme.DangerAccent, 2), new Point(cx, cy), wellDiameter / 2, wellDiameter / 2);
                    }

                    if (well.IsSelected)
                    {
                        dc.DrawEllipse(null, new Pen(Brushes.White, 2.5), new Point(cx, cy), wellDiameter / 2 + 2, wellDiameter / 2 + 2);
                    }
                }
            }
        }

        private void DrawSidebar(DrawingContext dc, Rect rect, double dpi)
        {
            dc.DrawRoundedRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, rect, 6, 6);

            var titleText = CreateFormattedText("WELL INSPECTION", ZeroWpfTheme.BoldTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(titleText, new Point(rect.X + 12, rect.Y + 10));

            double curY = rect.Y + 34;
            if (_selectedWell != null)
            {
                var idTxt = CreateFormattedText($"Well ID: {_selectedWell.WellId}", ZeroWpfTheme.BoldTypeface, 11, ZeroWpfTheme.TextPrimary, dpi);
                dc.DrawText(idTxt, new Point(rect.X + 12, curY));

                curY += 22;
                var typeTxt = CreateFormattedText($"Type: {_selectedWell.SampleType}", ZeroWpfTheme.RegularTypeface, 10, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(typeTxt, new Point(rect.X + 12, curY));

                curY += 22;
                var valTxt = CreateFormattedText($"OD450: {_selectedWell.Value:F3}", ZeroWpfTheme.BoldTypeface, 11, new SolidColorBrush(Color.FromRgb(59, 130, 246)), dpi);
                dc.DrawText(valTxt, new Point(rect.X + 12, curY));

                curY += 22;
                var concTxt = CreateFormattedText($"Conc: {_selectedWell.Concentration:F1} ng/mL", ZeroWpfTheme.RegularTypeface, 10, ZeroWpfTheme.TextPrimary, dpi);
                dc.DrawText(concTxt, new Point(rect.X + 12, curY));

                if (_selectedWell.IsOutlier)
                {
                    curY += 24;
                    var outTxt = CreateFormattedText("[FLAGGED OUTLIER]", ZeroWpfTheme.BoldTypeface, 10, ZeroWpfTheme.DangerAccent, dpi);
                    dc.DrawText(outTxt, new Point(rect.X + 12, curY));
                }
            }
            else
            {
                var hint1 = CreateFormattedText("Click a well to view", ZeroWpfTheme.RegularTypeface, 10, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(hint1, new Point(rect.X + 12, curY));

                var hint2 = CreateFormattedText("assay telemetry & conc.", ZeroWpfTheme.RegularTypeface, 10, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(hint2, new Point(rect.X + 12, curY + 16));
            }

            // Legend at bottom
            double legY = rect.Bottom - 110;
            var legTitle = CreateFormattedText("HEATMAP SCALE", ZeroWpfTheme.BoldTypeface, 9.5, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(legTitle, new Point(rect.X + 12, legY));

            legY += 20;
            Rect legBar = new Rect(rect.X + 12, legY, rect.Width - 24, 12);
            LinearGradientBrush legBrush = new LinearGradientBrush(Color.FromRgb(30, 58, 138), Color.FromRgb(239, 68, 68), 0.0);
            dc.DrawRoundedRectangle(legBrush, null, legBar, 2, 2);

            legY += 16;
            var lowTxt = CreateFormattedText("Low OD", ZeroWpfTheme.RegularTypeface, 9, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(lowTxt, new Point(rect.X + 12, legY));

            var highTxt = CreateFormattedText("High OD", ZeroWpfTheme.RegularTypeface, 9, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(highTxt, new Point(rect.Right - 12 - highTxt.Width, legY));
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZMicroplateReader"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("MicroplateReader is deprecated and will be removed in 5 release cycles. Please migrate to ZMicroplateReader instead.")]
    public class MicroplateReader : ZMicroplateReader { }

    /// <summary>
    /// Legacy alias for <see cref="ZMicroplateReader"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroMicroplateReader is deprecated and will be removed in 5 release cycles. Please migrate to ZMicroplateReader instead.")]
    public class ZeroMicroplateReader : ZMicroplateReader { }

    #endregion

}
