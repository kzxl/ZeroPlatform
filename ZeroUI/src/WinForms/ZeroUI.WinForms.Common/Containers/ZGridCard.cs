using System;

using ZeroUI.WinForms.Icons;using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Containers
{
    /// <summary>
    /// Column definition for the lightweight embedded grid in ZeroGridCard.
    /// </summary>
    public class GridCardColumn
    {
        public string HeaderText { get; set; }
        public int Width { get; set; }
        public HorizontalAlignment Alignment { get; set; }
        public bool IsAlertZero { get; set; }

        public GridCardColumn(string headerText, int width = 100, HorizontalAlignment alignment = HorizontalAlignment.Left, bool isAlertZero = false)
        {
            HeaderText = headerText;
            Width = width;
            Alignment = alignment;
            IsAlertZero = isAlertZero;
        }
    }

    /// <summary>
    /// Row data item for the lightweight embedded grid in ZeroGridCard.
    /// </summary>
    public class GridCardRow
    {
        public List<object?> Cells { get; } = new List<object?>();
        public Color? HighlightColor { get; set; }

        public GridCardRow(params object?[] cells)
        {
            if (cells != null) Cells.AddRange(cells);
        }
    }

    /// <summary>
    /// High-performance Process Step Card with an integrated lightweight Vector DataGrid and Footer Status Action.
    /// Combines step headers, partlist/BOM sub-details, auto-formatted numeric columns, out-of-stock highlights, and flat scrollbars.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroGridCard.bmp")]
    public class ZGridCard : Control
    {
        private readonly List<GridCardColumn> _columns = new List<GridCardColumn>();
        private readonly List<GridCardRow> _rows = new List<GridCardRow>();

        private int? _stepNumber = 1;
        private string? _stepText;
        private Color _badgeColor = Color.FromArgb(22, 119, 255); // Indigo/Blue
        private string _title = "Board Information";
        private string? _subtitle = "Partlist Usage: 026MC02RP2.0";
        private string? _statusTag = "4 Items";
        private Color _statusTagColor = Color.FromArgb(16, 185, 129);

        private string? _footerText = "Dispatch Info: By Status";
        private Color _footerTextColor = Color.FromArgb(22, 119, 255);
        private string? _summaryText = "Total Stock: 1,368 pcs";

        private int _scrollY = 0;
        private int _hoverRow = -1;
        private bool _isFooterHovered = false;
        private Rectangle _footerRect;

        public event EventHandler? FooterClicked;
        public event EventHandler<int>? RowClicked;

        public ZGridCard()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            Size = new Size(580, 240);

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        [Category("Appearance")]
        [DefaultValue(1)]
        public int? StepNumber
        {
            get => _stepNumber;
            set { _stepNumber = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(null)]
        public string? StepText
        {
            get => _stepText;
            set { _stepText = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color BadgeColor
        {
            get => _badgeColor;
            set { _badgeColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("Board Information")]
        public string Title
        {
            get => _title;
            set { _title = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("Partlist Usage: 026MC02RP2.0")]
        public string? Subtitle
        {
            get => _subtitle;
            set { _subtitle = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("4 Items")]
        public string? StatusTag
        {
            get => _statusTag;
            set { _statusTag = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color StatusTagColor
        {
            get => _statusTagColor;
            set { _statusTagColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("Dispatch Info: By Status")]
        public string? FooterText
        {
            get => _footerText;
            set { _footerText = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color FooterTextColor
        {
            get => _footerTextColor;
            set { _footerTextColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("Total Stock: 1,368 pcs")]
        public string? SummaryText
        {
            get => _summaryText;
            set { _summaryText = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool AutoStretchLastColumn { get; set; } = false;

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowCellBorders { get; set; } = true;

        [Browsable(false)]
        public List<GridCardColumn> Columns => _columns;

        [Browsable(false)]
        public List<GridCardRow> Rows => _rows;

        public void AddColumn(string headerText, int width = 100, HorizontalAlignment alignment = HorizontalAlignment.Left, bool isAlertZero = false)
        {
            _columns.Add(new GridCardColumn(headerText, width, alignment, isAlertZero));
            Invalidate();
        }

        public void AddRow(params object?[] values)
        {
            _rows.Add(new GridCardRow(values));
            Invalidate();
        }

        public void ClearRows()
        {
            _rows.Clear();
            _scrollY = 0;
            Invalidate();
        }

        public void ClearColumns()
        {
            _columns.Clear();
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int rowH = 26;
            int maxScroll = Math.Max(0, _rows.Count * rowH - GetGridBodyHeight());
            _scrollY = Math.Max(0, Math.Min(maxScroll, _scrollY - (e.Delta / 120) * rowH * 2));
            Invalidate();
        }

        private int GetGridBodyHeight()
        {
            int headerH = string.IsNullOrEmpty(_subtitle) ? 44 : 58;
            int colHeaderH = 26;
            int footerH = 30;
            return Math.Max(10, Height - headerH - colHeaderH - footerH - 12);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // 1. Check Footer link hover
            bool prevFootHover = _isFooterHovered;
            _isFooterHovered = _footerRect.Contains(e.Location) && !string.IsNullOrEmpty(_footerText);
            if (prevFootHover != _isFooterHovered)
            {
                Cursor = _isFooterHovered ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }

            // 2. Check Row hover
            int headerH = string.IsNullOrEmpty(_subtitle) ? 44 : 58;
            int colHeaderH = 26;
            int gridTop = headerH + colHeaderH + 6;
            int gridBodyH = GetGridBodyHeight();

            if (e.Y >= gridTop && e.Y <= gridTop + gridBodyH && e.X >= 16 && e.X <= Width - 16)
            {
                int rowH = 26;
                int rIdx = (e.Y - gridTop + _scrollY) / rowH;
                if (rIdx >= 0 && rIdx < _rows.Count)
                {
                    if (_hoverRow != rIdx)
                    {
                        _hoverRow = rIdx;
                        Invalidate();
                    }
                    return;
                }
            }

            if (_hoverRow != -1)
            {
                _hoverRow = -1;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverRow != -1 || _isFooterHovered)
            {
                _hoverRow = -1;
                _isFooterHovered = false;
                Cursor = Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Left)
            {
                if (_isFooterHovered)
                {
                    FooterClicked?.Invoke(this, EventArgs.Empty);
                    return;
                }

                if (_hoverRow >= 0 && _hoverRow < _rows.Count)
                {
                    RowClicked?.Invoke(this, _hoverRow);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = ZeroTheme.Colors;
            var cardRect = new Rectangle(0, 0, Width - 1, Height - 1);

            // 0. Card Background & Outer Rounded Border
            using (var path = CreateRoundedRect(cardRect, 8))
            using (var bgBrush = new SolidBrush(palette.Surface))
            using (var borderPen = new Pen(palette.Border, 1f))
            {
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
            }

            // 1. Header Section
            int headerH = string.IsNullOrEmpty(_subtitle) ? 44 : 58;
            float curX = 14f;

            // Step Badge
            string badgeLabel = _stepText ?? (_stepNumber.HasValue ? _stepNumber.Value.ToString() : "");
            if (!string.IsNullOrEmpty(badgeLabel))
            {
                var badgeRect = new RectangleF(curX, 12f, 22f, 22f);
                using (var bPath = CreateRoundedRect(badgeRect, 5))
                using (var bBrush = new SolidBrush(_badgeColor))
                using (var numBrush = new SolidBrush(Color.White))
                {
                    var numFont = ZeroFontCache.Get(8.5f, FontStyle.Bold);
                    g.FillPath(bBrush, bPath);
                    g.DrawString(badgeLabel, numFont, numBrush, badgeRect, ZeroStringFormats.Center);
                }
                curX += 28f;
            }

            // Title
            using (var titleBrush = new SolidBrush(palette.TextPrimary))
            {
                var titleFont = ZeroFontCache.Get(9.5f, FontStyle.Bold);
                g.DrawString(_title, titleFont, titleBrush, curX, 13f);
            }

            // Subtitle
            if (!string.IsNullOrEmpty(_subtitle))
            {
                using var subBrush = new SolidBrush(palette.TextSecondary);
                var subFont = ZeroFontCache.Get(8f, FontStyle.Regular);
                g.DrawString(_subtitle, subFont, subBrush, curX, 33f);
            }

            // Header Right Status Tag
            if (!string.IsNullOrEmpty(_statusTag))
            {
                var tagFont = ZeroFontCache.Get(8f, FontStyle.Bold);
                var tagSize = g.MeasureString(_statusTag, tagFont);
                float tagW = tagSize.Width + 14f;
                float tagH = 20f;
                float tagX = Width - tagW - 14f;
                float tagY = 13f;

                var tagRect = new RectangleF(tagX, tagY, tagW, tagH);
                using var tPath = CreateRoundedRect(tagRect, 4);
                using var tBg = new SolidBrush(Color.FromArgb(30, _statusTagColor));
                using var tPen = new Pen(Color.FromArgb(120, _statusTagColor), 1f);
                using var tText = new SolidBrush(_statusTagColor);

                g.FillPath(tBg, tPath);
                g.DrawPath(tPen, tPath);
                g.DrawString(_statusTag, tagFont, tText, tagRect, ZeroStringFormats.Center);
            }

            // 2. Embedded Grid Header
            int colHeaderH = 26;
            int gridLeft = 14;
            int gridRight = Width - 14;
            int gridWidth = gridRight - gridLeft;
            int gridHeaderY = headerH + 4;

            var colHeaderRect = new RectangleF(gridLeft, gridHeaderY, gridWidth, colHeaderH);
            Color chBg = ZeroTheme.IsDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(241, 245, 249);
            using (var chBrush = new SolidBrush(chBg))
            using (var chPen = new Pen(palette.Border, 1f))
            {
                g.FillRectangle(chBrush, colHeaderRect);
                g.DrawLine(chPen, gridLeft, gridHeaderY + colHeaderH, gridRight, gridHeaderY + colHeaderH);
                g.DrawLine(chPen, gridLeft, gridHeaderY, gridRight, gridHeaderY);
            }

            // Draw Column Headers
            float colX = gridLeft;
            using (var chTextBrush = new SolidBrush(palette.TextPrimary))
            using (var colLinePen = new Pen(Color.FromArgb(80, palette.Border), 1f))
            {
                var chFont = ZeroFontCache.Get(8.5f, FontStyle.Bold);
                for (int c = 0; c < _columns.Count; c++)
                {
                    var col = _columns[c];
                    float cW = col.Width;
                    if (AutoStretchLastColumn && c == _columns.Count - 1 && colX + cW < gridRight)
                    {
                        cW = gridRight - colX;
                    }

                    var cRect = new RectangleF(colX + 8, gridHeaderY, cW - 16, colHeaderH);
                    var sf = col.Alignment switch
                    {
                        HorizontalAlignment.Right => ZeroStringFormats.FarCenter,
                        HorizontalAlignment.Center => ZeroStringFormats.Center,
                        _ => ZeroStringFormats.NearCenter
                    };

                    g.DrawString(col.HeaderText, chFont, chTextBrush, cRect, sf);

                    if (ShowCellBorders && (c < _columns.Count - 1 || colX + cW < gridRight))
                    {
                        g.DrawLine(colLinePen, colX + cW, gridHeaderY + 4, colX + cW, gridHeaderY + colHeaderH - 4);
                    }
                    colX += cW;
                }
            }

            // 3. Embedded Grid Rows
            int gridBodyY = gridHeaderY + colHeaderH;
            int gridBodyH = GetGridBodyHeight();
            var clipRect = new Rectangle(gridLeft, gridBodyY, gridWidth, gridBodyH);

            var oldClip = g.Clip;
            g.SetClip(clipRect);

            int rowH = 26;
            int totalContentH = _rows.Count * rowH;

            var rowFont = ZeroFontCache.Get(8.5f, FontStyle.Regular);
            var rowBoldFont = ZeroFontCache.Get(8.5f, FontStyle.Bold);
            using (var textPrimaryBrush = new SolidBrush(palette.TextPrimary))
            using (var alertRedBrush = new SolidBrush(palette.Danger))
            using (var linePen = new Pen(Color.FromArgb(40, palette.Border), 1f))
            using (var hovBrush = new SolidBrush(Color.FromArgb(20, palette.Primary)))
            using (var altBrush = new SolidBrush(ZeroTheme.IsDark ? Color.FromArgb(20, 255, 255, 255) : Color.FromArgb(248, 250, 252)))
            {
                for (int r = 0; r < _rows.Count; r++)
                {
                    var row = _rows[r];
                    float rY = gridBodyY + (r * rowH) - _scrollY;

                    if (rY + rowH < gridBodyY || rY > gridBodyY + gridBodyH) continue;

                    // Zebra striping & hover
                    var rowRect = new RectangleF(gridLeft, rY, gridWidth, rowH);
                    if (r == _hoverRow)
                    {
                        g.FillRectangle(hovBrush, rowRect);
                    }
                    else if (r % 2 == 1)
                    {
                        g.FillRectangle(altBrush, rowRect);
                    }

                    // Render Cells
                    float rColX = gridLeft;
                    for (int c = 0; c < _columns.Count; c++)
                    {
                        var col = _columns[c];
                        float cW = col.Width;
                        if (AutoStretchLastColumn && c == _columns.Count - 1 && rColX + cW < gridRight)
                        {
                            cW = gridRight - rColX;
                        }

                        object? val = c < row.Cells.Count ? row.Cells[c] : null;
                        string valStr = FormatCellValue(val);

                        bool isZeroAlert = false;
                        if (col.IsAlertZero)
                        {
                            if (val is int iv && iv == 0) isZeroAlert = true;
                            else if (val is double dv && dv == 0.0) isZeroAlert = true;
                            else if (valStr == "0") isZeroAlert = true;
                        }

                        var cellRect = new RectangleF(rColX + 8, rY, cW - 16, rowH);
                        var sf = col.Alignment switch
                        {
                            HorizontalAlignment.Right => ZeroStringFormats.FarCenter,
                            HorizontalAlignment.Center => ZeroStringFormats.Center,
                            _ => ZeroStringFormats.NearCenter
                        };

                        Brush drawBrush = isZeroAlert ? alertRedBrush : textPrimaryBrush;
                        Font drawFont = isZeroAlert ? rowBoldFont : rowFont;

                        g.DrawString(valStr, drawFont, drawBrush, cellRect, sf);

                        if (ShowCellBorders && (c < _columns.Count - 1 || rColX + cW < gridRight))
                        {
                            g.DrawLine(linePen, rColX + cW, rY + 3, rColX + cW, rY + rowH - 4);
                        }

                        rColX += cW;
                    }

                    // Bottom row divider
                    g.DrawLine(linePen, gridLeft, rY + rowH - 1, gridRight, rY + rowH - 1);
                }
            }

            g.Clip = oldClip;

            // 4. Flat Vector Scrollbar (if rows overflow)
            if (totalContentH > gridBodyH)
            {
                int sbW = 6;
                int sbX = gridRight - sbW - 2;
                int sbY = gridBodyY + 2;
                int sbH = gridBodyH - 4;

                float thumbRatio = Math.Max(0.15f, (float)gridBodyH / totalContentH);
                float thumbH = sbH * thumbRatio;
                float thumbY = sbY + (float)_scrollY / (totalContentH - gridBodyH) * (sbH - thumbH);

                var thumbRect = new RectangleF(sbX, thumbY, sbW, thumbH);
                using var thumbBrush = new SolidBrush(Color.FromArgb(120, palette.TextSecondary));
                using var tPath = CreateRoundedRect(thumbRect, 3);
                g.FillPath(thumbBrush, tPath);
            }

            // 5. Footer Section
            int footerY = Height - 28;
            if (!string.IsNullOrEmpty(_footerText))
            {
                var footFont = ZeroFontCache.Get(8f, _isFooterHovered ? FontStyle.Underline : FontStyle.Regular);
                using var footBrush = new SolidBrush(_isFooterHovered ? Color.FromArgb(29, 78, 216) : _footerTextColor);
                var fSz = g.MeasureString(_footerText, footFont);
                _footerRect = new Rectangle(14, footerY - 2, (int)fSz.Width + 8, (int)fSz.Height + 4);
                g.DrawString(_footerText, footFont, footBrush, 14, footerY);
            }

            if (!string.IsNullOrEmpty(_summaryText))
            {
                var sumFont = ZeroFontCache.Get(8f, FontStyle.Bold);
                using var sumBrush = new SolidBrush(palette.TextSecondary);
                var sSz = g.MeasureString(_summaryText, sumFont);
                g.DrawString(_summaryText, sumFont, sumBrush, Width - sSz.Width - 14, footerY);
            }
        }

        private static string FormatCellValue(object? val)
        {
            if (val == null) return "--";
            if (val is int iv) return iv.ToString("N0");
            if (val is long lv) return lv.ToString("N0");
            if (val is double dv) return dv.ToString("N0");
            if (val is decimal mv) return mv.ToString("N0");
            return val.ToString() ?? "";
        }

        private static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2f;
            if (rect.Width < d || rect.Height < d)
            {
                path.AddRectangle(rect);
                return path;
            }

            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
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

    /// <summary>
    /// Legacy alias for <see cref="ZGridCard"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroGridCard is deprecated and will be removed in 5 release cycles. Please migrate to ZGridCard instead.")]
    [ToolboxItem(false)]
    public class ZeroGridCard : ZGridCard
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZGridCard"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("GridCard is deprecated and will be removed in 5 release cycles. Please migrate to ZGridCard instead.")]
    [ToolboxItem(false)]
    public class GridCard : ZGridCard
    {
    }

    #endregion
}
