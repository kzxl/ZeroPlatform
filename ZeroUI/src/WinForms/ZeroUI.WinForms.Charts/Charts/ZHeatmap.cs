using System;

using ZeroUI.WinForms.Icons;using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Charts
{
    public enum HeatmapPaletteMode
    {
        Industrial,
        Viridis,
        CoolWarm,
        Emerald
    }

    public class HeatmapCellEventArgs : EventArgs
    {
        public int RowIndex { get; }
        public int ColumnIndex { get; }
        public string RowLabel { get; }
        public string ColumnLabel { get; }
        public float Value { get; }

        public HeatmapCellEventArgs(int r, int c, string rLabel, string cLabel, float val)
        {
            RowIndex = r;
            ColumnIndex = c;
            RowLabel = rLabel;
            ColumnLabel = cLabel;
            Value = val;
        }
    }

    /// <summary>
    /// Industrial 2D Matrix Heatmap control for telemetry, line throughput, wafer thermal maps,
    /// and machine load distribution with multi-stop color gradients and hover inspection.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [DefaultEvent("CellClicked")]
    [Description("2D Matrix Heatmap for machine throughput, thermal maps, and load distribution")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroHeatmap.bmp")]
    public class ZHeatmap : Control
    {
        private string[] _xLabels = Array.Empty<string>();
        private string[] _yLabels = Array.Empty<string>();
        private float[,]? _data;

        private float _minValue = 0f;
        private float _maxValue = 100f;
        private bool _autoMinMax = true;

        private HeatmapPaletteMode _paletteMode = HeatmapPaletteMode.Industrial;
        private bool _showValues = true;
        private bool _showLegend = true;
        private string _valueFormat = "{0:0}";
        private int _cellPadding = 2;
        private int _cellRadius = 3;

        private int _hoveredRow = -1;
        private int _hoveredCol = -1;
        private Point _mousePos;

        public event EventHandler<HeatmapCellEventArgs>? CellClicked;
        public event EventHandler<HeatmapCellEventArgs>? CellHovered;

        public ZHeatmap()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(580, 320);
            Font = new Font("Segoe UI", 8.5f);
            BackColor = Color.FromArgb(15, 23, 42); // Obsidian Dark

            InitializeSampleData();
            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        private void InitializeSampleData()
        {
            _yLabels = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            _xLabels = new[] { "00:00", "02:00", "04:00", "06:00", "08:00", "10:00", "12:00", "14:00", "16:00", "18:00", "20:00", "22:00" };

            var rand = new Random(42);
            _data = new float[_yLabels.Length, _xLabels.Length];
            for (int r = 0; r < _yLabels.Length; r++)
            {
                for (int c = 0; c < _xLabels.Length; c++)
                {
                    // Simulated production curve
                    float baseVal = (c >= 4 && c <= 10) ? 75f : 20f;
                    _data[r, c] = (float)Math.Max(5, Math.Min(100, baseVal + rand.Next(-15, 25)));
                }
            }
            RecalculateMinMax();
        }

        [Browsable(false)]
        public float[,]? Data
        {
            get => _data;
            set
            {
                _data = value;
                if (_autoMinMax) RecalculateMinMax();
                Invalidate();
            }
        }

        [Category("Data")]
        public string[] XLabels
        {
            get => _xLabels;
            set
            {
                _xLabels = value ?? Array.Empty<string>();
                Invalidate();
            }
        }

        [Category("Data")]
        public string[] YLabels
        {
            get => _yLabels;
            set
            {
                _yLabels = value ?? Array.Empty<string>();
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue(HeatmapPaletteMode.Industrial)]
        public HeatmapPaletteMode PaletteMode
        {
            get => _paletteMode;
            set
            {
                if (_paletteMode != value)
                {
                    _paletteMode = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowValues
        {
            get => _showValues;
            set
            {
                if (_showValues != value)
                {
                    _showValues = value;
                    Invalidate();
                }
            }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowLegend
        {
            get => _showLegend;
            set
            {
                if (_showLegend != value)
                {
                    _showLegend = value;
                    Invalidate();
                }
            }
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool AutoMinMax
        {
            get => _autoMinMax;
            set
            {
                _autoMinMax = value;
                if (_autoMinMax) RecalculateMinMax();
                Invalidate();
            }
        }

        [Category("Data")]
        public float MinValue
        {
            get => _minValue;
            set
            {
                _minValue = value;
                _autoMinMax = false;
                Invalidate();
            }
        }

        [Category("Data")]
        public float MaxValue
        {
            get => _maxValue;
            set
            {
                _maxValue = value;
                _autoMinMax = false;
                Invalidate();
            }
        }

        public void SetMatrixData(string[] xLabels, string[] yLabels, float[,] data)
        {
            _xLabels = xLabels ?? Array.Empty<string>();
            _yLabels = yLabels ?? Array.Empty<string>();
            _data = data;
            if (_autoMinMax) RecalculateMinMax();
            Invalidate();
        }

        private void RecalculateMinMax()
        {
            if (_data == null || _data.Length == 0) return;

            float min = float.MaxValue;
            float max = float.MinValue;

            int rows = _data.GetLength(0);
            int cols = _data.GetLength(1);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float v = _data[r, c];
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            }

            if (min >= max)
            {
                min = 0f;
                max = 100f;
            }

            _minValue = min;
            _maxValue = max;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _mousePos = e.Location;

            GetCellAt(e.X, e.Y, out int row, out int col);
            if (row != _hoveredRow || col != _hoveredCol)
            {
                _hoveredRow = row;
                _hoveredCol = col;
                Invalidate();

                if (_hoveredRow >= 0 && _hoveredCol >= 0 && _data != null)
                {
                    string rLabel = (_hoveredRow < _yLabels.Length) ? _yLabels[_hoveredRow] : $"R{_hoveredRow}";
                    string cLabel = (_hoveredCol < _xLabels.Length) ? _xLabels[_hoveredCol] : $"C{_hoveredCol}";
                    CellHovered?.Invoke(this, new HeatmapCellEventArgs(_hoveredRow, _hoveredCol, rLabel, cLabel, _data[_hoveredRow, _hoveredCol]));
                }
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredRow = -1;
            _hoveredCol = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_hoveredRow >= 0 && _hoveredCol >= 0 && _data != null)
            {
                string rLabel = (_hoveredRow < _yLabels.Length) ? _yLabels[_hoveredRow] : $"R{_hoveredRow}";
                string cLabel = (_hoveredCol < _xLabels.Length) ? _xLabels[_hoveredCol] : $"C{_hoveredCol}";
                CellClicked?.Invoke(this, new HeatmapCellEventArgs(_hoveredRow, _hoveredCol, rLabel, cLabel, _data[_hoveredRow, _hoveredCol]));
            }
        }

        private void GetCellAt(int mouseX, int mouseY, out int row, out int col)
        {
            row = -1;
            col = -1;

            if (_data == null || _yLabels.Length == 0 || _xLabels.Length == 0) return;

            int leftMargin = 55;
            int topMargin = 25;
            int bottomMargin = _showLegend ? 40 : 15;
            int rightMargin = 20;

            int plotW = Width - leftMargin - rightMargin;
            int plotH = Height - topMargin - bottomMargin;

            if (mouseX < leftMargin || mouseX >= leftMargin + plotW ||
                mouseY < topMargin || mouseY >= topMargin + plotH)
            {
                return;
            }

            int cols = _xLabels.Length;
            int rows = _yLabels.Length;

            float cellW = (float)plotW / cols;
            float cellH = (float)plotH / rows;

            col = (int)((mouseX - leftMargin) / cellW);
            row = (int)((mouseY - topMargin) / cellH);

            if (col < 0 || col >= cols) col = -1;
            if (row < 0 || row >= rows) row = -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = ZeroTheme.Colors;
            g.Clear(palette.Background);

            if (_data == null || _xLabels.Length == 0 || _yLabels.Length == 0)
            {
                using var brushEmpty = new SolidBrush(palette.TextSecondary);
                g.DrawString("No Matrix Data Available", Font, brushEmpty, 20, 20);
                return;
            }

            int leftMargin = 55;
            int topMargin = 25;
            int bottomMargin = _showLegend ? 42 : 15;
            int rightMargin = 20;

            int plotW = Width - leftMargin - rightMargin;
            int plotH = Height - topMargin - bottomMargin;

            int cols = _xLabels.Length;
            int rows = _yLabels.Length;

            float cellW = (float)plotW / cols;
            float cellH = (float)plotH / rows;

            var fontLabel = ZeroFontCache.Get(8f, FontStyle.Regular);
            var fontValue = ZeroFontCache.Get(7.5f, FontStyle.Bold);
            using var brushLabel = new SolidBrush(palette.TextSecondary);
            using var brushCell = new SolidBrush(Color.Empty);
            using var brushVal = new SolidBrush(Color.Empty);

            // 1. Draw X Header Labels (Top, smart step when narrow to prevent overlap)
            int step = cellW < 20 ? 4 : (cellW < 30 ? 2 : 1);
            for (int c = 0; c < cols; c++)
            {
                if (c % step != 0 && c != cols - 1) continue;
                float x = leftMargin + (c * cellW) + (cellW / 2f);
                g.DrawString(_xLabels[c], fontLabel, brushLabel, x, topMargin - 4, ZeroStringFormats.CenterFar);
            }

            // 2. Draw Y Header Labels (Left)
            for (int r = 0; r < rows; r++)
            {
                float y = topMargin + (r * cellH) + (cellH / 2f);
                g.DrawString(_yLabels[r], fontLabel, brushLabel, leftMargin - 6, y, ZeroStringFormats.FarCenter);
            }

            // 3. Render Matrix Cells
            RectangleF hoveredRect = RectangleF.Empty;
            float hoveredVal = 0f;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float val = _data[r, c];
                    float norm = (_maxValue > _minValue) ? (val - _minValue) / (_maxValue - _minValue) : 0.5f;
                    norm = Math.Max(0f, Math.Min(1f, norm));

                    Color cellColor = InterpolateColor(norm, _paletteMode);

                    float x = leftMargin + (c * cellW) + _cellPadding;
                    float y = topMargin + (r * cellH) + _cellPadding;
                    float w = cellW - (_cellPadding * 2);
                    float h = cellH - (_cellPadding * 2);

                    var cellRect = new RectangleF(x, y, w, h);

                    brushCell.Color = cellColor;
                    if (_cellRadius <= 0)
                    {
                        g.FillRectangle(brushCell, cellRect);
                    }
                    else
                    {
                        using var path = CreateRoundedRect(cellRect, _cellRadius);
                        g.FillPath(brushCell, path);
                    }

                    // Value text
                    if (_showValues && w >= 22 && h >= 16)
                    {
                        Color textC = (norm > 0.6f && _paletteMode != HeatmapPaletteMode.Emerald) ? Color.White : Color.FromArgb(240, 240, 240);
                        if (_paletteMode == HeatmapPaletteMode.Viridis && norm > 0.7f) textC = Color.FromArgb(20, 20, 20);

                        brushVal.Color = textC;
                        g.DrawString(string.Format(_valueFormat, val), fontValue, brushVal, cellRect, ZeroStringFormats.Center);
                    }

                    if (r == _hoveredRow && c == _hoveredCol)
                    {
                        hoveredRect = cellRect;
                        hoveredVal = val;
                    }
                }
            }

            // 4. Hover Highlight & Tooltip
            if (!hoveredRect.IsEmpty && _hoveredRow >= 0 && _hoveredCol >= 0)
            {
                using (var penHover = new Pen(Color.White, 2f))
                {
                    if (_cellRadius <= 0)
                    {
                        g.DrawRectangle(penHover, hoveredRect.X, hoveredRect.Y, hoveredRect.Width, hoveredRect.Height);
                    }
                    else
                    {
                        using var pathHover = CreateRoundedRect(hoveredRect, _cellRadius);
                        g.DrawPath(penHover, pathHover);
                    }
                }

                // Draw floating inspection pill near mouse
                string rText = (_hoveredRow < _yLabels.Length) ? _yLabels[_hoveredRow] : "";
                string cText = (_hoveredCol < _xLabels.Length) ? _xLabels[_hoveredCol] : "";
                string tip = $"{rText} • {cText}: {hoveredVal:0.0} ({((hoveredVal - _minValue) / Math.Max(1f, _maxValue - _minValue) * 100):0}%)";

                var fontTip = ZeroFontCache.Get(8f, FontStyle.Bold);
                var tipSz = g.MeasureString(tip, fontTip);
                int tipW = (int)tipSz.Width + 16;
                int tipH = 22;
                int tipX = Math.Min(Width - tipW - 8, Math.Max(8, _mousePos.X - (tipW / 2)));
                int tipY = (_mousePos.Y < 50) ? _mousePos.Y + 20 : _mousePos.Y - tipH - 12;

                var tipRect = new Rectangle(tipX, tipY, tipW, tipH);
                using (var brushTipBg = new SolidBrush(Color.FromArgb(230, 15, 23, 42)))
                using (var penTip = new Pen(palette.Primary, 1.2f))
                using (var pathTip = CreateRoundedRect(tipRect, 4))
                {
                    g.FillPath(brushTipBg, pathTip);
                    g.DrawPath(penTip, pathTip);
                }

                using var brushTipText = new SolidBrush(Color.White);
                g.DrawString(tip, fontTip, brushTipText, tipRect, ZeroStringFormats.Center);
            }

            // 5. Draw Bottom Color Gradient Legend
            if (_showLegend)
            {
                int legendY = Height - 24;
                int legendW = Math.Min(240, plotW);
                int legendX = leftMargin + (plotW - legendW) / 2;
                int legendH = 10;

                var legRect = new Rectangle(legendX, legendY, legendW, legendH);

                // Paint gradient bar using cached interpolation colors
                using (var legBrush = new LinearGradientBrush(
                    legRect,
                    InterpolateColor(0f, _paletteMode),
                    InterpolateColor(1f, _paletteMode),
                    LinearGradientMode.Horizontal))
                {
                    var cb = new ColorBlend(5);
                    cb.Colors = new[]
                    {
                        InterpolateColor(0f, _paletteMode),
                        InterpolateColor(0.25f, _paletteMode),
                        InterpolateColor(0.5f, _paletteMode),
                        InterpolateColor(0.75f, _paletteMode),
                        InterpolateColor(1f, _paletteMode)
                    };
                    cb.Positions = new[] { 0f, 0.25f, 0.5f, 0.75f, 1f };
                    legBrush.InterpolationColors = cb;

                    g.FillRectangle(legBrush, legRect);
                }

                using (var penLeg = new Pen(palette.Border, 1f))
                {
                    g.DrawRectangle(penLeg, legRect);
                }

                // Legend labels (Min, Mid, Max)
                var fontLeg = ZeroFontCache.Get(7.5f, FontStyle.Regular);
                g.DrawString($"{_minValue:0}", fontLeg, brushLabel, legendX - 4, legendY - 1, ZeroStringFormats.FarNear);
                g.DrawString($"{((_minValue + _maxValue) / 2f):0}", fontLeg, brushLabel, legendX + (legendW / 2f), legendY + legendH + 2, ZeroStringFormats.CenterNear);
                g.DrawString($"{_maxValue:0}", fontLeg, brushLabel, legendX + legendW + 4, legendY - 1, ZeroStringFormats.NearNear);
            }
        }

        private static Color InterpolateColor(float t, HeatmapPaletteMode mode)
        {
            t = Math.Max(0f, Math.Min(1f, t));

            switch (mode)
            {
                case HeatmapPaletteMode.Viridis:
                    // Purple -> Teal -> Yellow
                    if (t < 0.5f)
                    {
                        float segT = t / 0.5f;
                        return LerpColor(Color.FromArgb(68, 1, 84), Color.FromArgb(33, 145, 140), segT);
                    }
                    else
                    {
                        float segT = (t - 0.5f) / 0.5f;
                        return LerpColor(Color.FromArgb(33, 145, 140), Color.FromArgb(253, 231, 37), segT);
                    }

                case HeatmapPaletteMode.CoolWarm:
                    // Blue -> Ice -> Crimson
                    if (t < 0.5f)
                    {
                        float segT = t / 0.5f;
                        return LerpColor(Color.FromArgb(59, 130, 246), Color.FromArgb(226, 232, 240), segT);
                    }
                    else
                    {
                        float segT = (t - 0.5f) / 0.5f;
                        return LerpColor(Color.FromArgb(226, 232, 240), Color.FromArgb(239, 68, 68), segT);
                    }

                case HeatmapPaletteMode.Emerald:
                    // Slate -> Mint -> Bright Emerald
                    return LerpColor(Color.FromArgb(15, 30, 35), Color.FromArgb(16, 185, 129), t);

                case HeatmapPaletteMode.Industrial:
                default:
                    // Slate -> Cyan -> Green -> Amber -> Red
                    if (t < 0.25f)
                    {
                        return LerpColor(Color.FromArgb(30, 41, 59), Color.FromArgb(6, 182, 212), t / 0.25f);
                    }
                    else if (t < 0.50f)
                    {
                        return LerpColor(Color.FromArgb(6, 182, 212), Color.FromArgb(16, 185, 129), (t - 0.25f) / 0.25f);
                    }
                    else if (t < 0.75f)
                    {
                        return LerpColor(Color.FromArgb(16, 185, 129), Color.FromArgb(245, 158, 11), (t - 0.50f) / 0.25f);
                    }
                    else
                    {
                        return LerpColor(Color.FromArgb(245, 158, 11), Color.FromArgb(239, 68, 68), (t - 0.75f) / 0.25f);
                    }
            }
        }

        private static Color LerpColor(Color c1, Color c2, float t)
        {
            int r = (int)(c1.R + (c2.R - c1.R) * t);
            int g = (int)(c1.G + (c2.G - c1.G) * t);
            int b = (int)(c1.B + (c2.B - c1.B) * t);
            return Color.FromArgb(Math.Max(0, Math.Min(255, r)), Math.Max(0, Math.Min(255, g)), Math.Max(0, Math.Min(255, b)));
        }

        private static GraphicsPath CreateRoundedRect(RectangleF r, int radius) =>
            ZeroUIConfig.CreateRoundedRectangleF(r, radius);
    
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

    /// <summary>
    /// Legacy alias for <see cref="ZHeatmap"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroHeatmap is deprecated and will be removed in 5 release cycles. Please migrate to ZHeatmap instead.")]
    [ToolboxItem(false)]
    public class ZeroHeatmap : ZHeatmap
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZHeatmap"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("Heatmap is deprecated and will be removed in 5 release cycles. Please migrate to ZHeatmap instead.")]
    [ToolboxItem(false)]
    public class Heatmap : ZHeatmap
    {
    }

    #endregion
}
