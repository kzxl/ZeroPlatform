using System;

using ZeroUI.WinForms.Icons;using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using ZeroUI.Core.Localization;
using ZeroUI.Core.Pivot;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.PivotGrid
{
    /// <summary>
    /// High-performance multidimensional cross-tab OLAP reporting control for ZeroUI WinForms.
    /// Provides hierarchical row/column header grouping, measure aggregations, sub-totals,
    /// grand totals, and single-HWND vector GDI+ virtualized rendering.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - DataGrid & Reporting")]
    [DefaultEvent("CellClick")]
    [Description("Multidimensional cross-tab OLAP reporting grid with dynamic grouping, aggregations, and grand totals")]
    [ToolboxBitmap(typeof(ZeroIcons), "PivotGridControl.bmp")]
    [Designer("ZeroUI.WinForms.Design.Data.ZPivotGridDesigner, ZeroUI.WinForms.Design")]
    public class ZPivotGrid : Control
    {
        private readonly PivotEngine _engine = new PivotEngine();
        private PivotResultModel? _model;

        private int _headerAreaHeight = 36;
        private int _rowHeaderWidth = 140;
        private int _columnHeaderHeight = 28;
        private int _cellHeight = 26;
        private int _cellWidth = 110;

        private int _scrollX = 0;
        private int _scrollY = 0;

        private readonly VScrollBar _vScrollBar;
        private readonly HScrollBar _hScrollBar;

        private PivotFieldChooserDialog? _fieldChooserDialog;
        private Rectangle _fieldListButtonRect;
        private bool _hoverFieldListBtn = false;

        public event EventHandler? DataRecalculated;

        [Browsable(false)]
        public PivotEngine Engine => _engine;

        [Browsable(false)]
        public List<PivotGridField> Fields => _engine.Fields;

        [Browsable(false)]
        public PivotResultModel? Model => _model;

        [Category("Data")]
        [Description("The enumerable data source feeding the OLAP cross-tab summarization model")]
        public object? DataSource
        {
            get => _engine.DataSource;
            set
            {
                _engine.DataSource = value as IEnumerable;
                RefreshData();
            }
        }

        [Category("Appearance")]
        [DefaultValue(140)]
        public int RowHeaderWidth
        {
            get => _rowHeaderWidth;
            set { _rowHeaderWidth = Math.Max(60, value); Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(110)]
        public int CellWidth
        {
            get => _cellWidth;
            set { _cellWidth = Math.Max(50, value); Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(26)]
        public int CellHeight
        {
            get => _cellHeight;
            set { _cellHeight = Math.Max(20, value); Invalidate(); }
        }

        public ZPivotGrid()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = ZeroTheme.Colors.Surface;


            _vScrollBar = new VScrollBar
            {
                Dock = DockStyle.Right,
                Visible = false
            };
            _vScrollBar.ValueChanged += (s, e) => { _scrollY = _vScrollBar.Value; Invalidate(); };
            Controls.Add(_vScrollBar);

            _hScrollBar = new HScrollBar
            {
                Dock = DockStyle.Bottom,
                Visible = false
            };
            _hScrollBar.ValueChanged += (s, e) => { _scrollX = _hScrollBar.Value; Invalidate(); };
            Controls.Add(_hScrollBar);

            ZeroTheme.ThemeChanged += OnThemeChanged;

            Localizer.CultureChanged += (s, e) =>
            {
                RefreshData();
            };
        }

        public PivotGridField AddField(string fieldName, PivotArea area, string? caption = null, PivotSummaryType summaryType = PivotSummaryType.Sum)
        {
            var field = _engine.AddField(fieldName, area, caption, summaryType);
            RefreshData();
            return field;
        }

        public void RefreshData()
        {
            _model = _engine.Calculate();
            UpdateScrollBars();
            _fieldChooserDialog?.PopulateFields();
            DataRecalculated?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        public void ShowFieldList()
        {
            if (_fieldChooserDialog == null || _fieldChooserDialog.IsDisposed)
            {
                _fieldChooserDialog = new PivotFieldChooserDialog(this);
            }
            if (!_fieldChooserDialog.Visible)
            {
                _fieldChooserDialog.Show(this);
            }
            else
            {
                _fieldChooserDialog.BringToFront();
            }
        }

        public void HideFieldList()
        {
            _fieldChooserDialog?.Hide();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool hoverBtn = _fieldListButtonRect.Contains(e.Location);
            if (hoverBtn != _hoverFieldListBtn)
            {
                _hoverFieldListBtn = hoverBtn;
                Cursor = _hoverFieldListBtn ? Cursors.Hand : Cursors.Default;
                Invalidate(new Rectangle(Width - 120, 0, 120, _headerAreaHeight));
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (e.Button == MouseButtons.Left && _fieldListButtonRect.Contains(e.Location))
            {
                ShowFieldList();
                return;
            }

            if (e.Button == MouseButtons.Right)
            {
                var menu = new ContextMenuStrip();
                menu.Items.Add("Show Field List...", null, (s, ev) => ShowFieldList());
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("Recalculate Summary", null, (s, ev) => RefreshData());
                menu.Show(this, e.Location);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollBars();
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_vScrollBar.Visible)
            {
                int delta = -(e.Delta / 120) * _cellHeight * 2;
                int newVal = Math.Max(0, Math.Min(_vScrollBar.Maximum - _vScrollBar.LargeChange + 1, _vScrollBar.Value + delta));
                _vScrollBar.Value = newVal;
                _scrollY = newVal;
                Invalidate();
            }
        }

        private void UpdateScrollBars()
        {
            if (_vScrollBar == null || _hScrollBar == null) return;
            if (_model == null)
            {
                _vScrollBar.Visible = false;
                _hScrollBar.Visible = false;
                return;
            }

            // Total columns = RowHeaders + Matrix Columns + GrandTotal Column
            int totalContentWidth = _rowHeaderWidth + (_model.ColumnCount + 1) * _cellWidth;
            // Total rows = Matrix Rows + GrandTotal Row
            int totalContentHeight = (_model.RowCount + 1) * _cellHeight;

            int viewWidth = ClientSize.Width - (_vScrollBar.Visible ? _vScrollBar.Width : 0) - _rowHeaderWidth;
            int viewHeight = ClientSize.Height - (_hScrollBar.Visible ? _hScrollBar.Height : 0) - _headerAreaHeight - _columnHeaderHeight;

            int matrixWidth = (_model.ColumnCount + 1) * _cellWidth;

            if (matrixWidth > viewWidth && viewWidth > 0)
            {
                _hScrollBar.Visible = true;
                _hScrollBar.Maximum = matrixWidth;
                _hScrollBar.LargeChange = Math.Max(1, viewWidth);
            }
            else
            {
                _hScrollBar.Visible = false;
                _scrollX = 0;
            }

            if (totalContentHeight > viewHeight && viewHeight > 0)
            {
                _vScrollBar.Visible = true;
                _vScrollBar.Maximum = totalContentHeight;
                _vScrollBar.LargeChange = Math.Max(1, viewHeight);
            }
            else
            {
                _vScrollBar.Visible = false;
                _scrollY = 0;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var colors = ZeroTheme.Colors;
            g.Clear(colors.Surface);

            // 1. Draw Field Area Strip at the top
            DrawFieldAreaStrip(g, colors);

            if (_model == null || (_model.RowCount == 0 && _model.ColumnCount == 0))
            {
                using (var brush = new SolidBrush(colors.TextSecondary))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    string emptyMsg = Localizer.GetString(StringId.PivotDropDataFields);
                    g.DrawString(emptyMsg, Font, brush, new Rectangle(0, _headerAreaHeight, Width, Height - _headerAreaHeight), sf);
                }
                return;
            }

            int matrixTop = _headerAreaHeight + _columnHeaderHeight;
            int clientRight = Width - (_vScrollBar.Visible ? _vScrollBar.Width : 0);
            int clientBottom = Height - (_hScrollBar.Visible ? _hScrollBar.Height : 0);

            // Clip region for scrollable data cells
            var dataClip = new Rectangle(_rowHeaderWidth, matrixTop, clientRight - _rowHeaderWidth, clientBottom - matrixTop);

            // 2. Draw Column Headers (Top axis, horizontal scroll only)
            DrawColumnHeaders(g, colors, clientRight);

            // 3. Draw Row Headers (Left axis, vertical scroll only)
            DrawRowHeaders(g, colors, clientBottom, matrixTop);

            // 4. Draw Intersection Data Matrix Cells (2D Scrollable)
            var prevClip = g.Clip;
            g.SetClip(dataClip);
            DrawDataCells(g, colors, matrixTop);
            g.Clip = prevClip;

            // 5. Draw Top-Left Corner Block
            using (var brush = new SolidBrush(colors.HeaderBackground))
            {
                var cornerRect = new Rectangle(0, _headerAreaHeight, _rowHeaderWidth, _columnHeaderHeight);
                g.FillRectangle(brush, cornerRect);
                using (var pen = new Pen(colors.Border))
                {
                    g.DrawRectangle(pen, cornerRect);
                }

                string rowTitle = _model.RowFields.Count > 0 ? _model.RowFields[0].Caption : "Rows";
                string colTitle = _model.ColumnFields.Count > 0 ? _model.ColumnFields[0].Caption : "Columns";
                using (var tBrush = new SolidBrush(colors.TextSecondary))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                    g.DrawString($"{rowTitle} \\ {colTitle}", ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Bold), tBrush, new Rectangle(6, _headerAreaHeight, _rowHeaderWidth - 12, _columnHeaderHeight), sf);
                }
            }

            // Outer border
            using (var borderPen = new Pen(colors.Border))
            {
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }
        }

        private void DrawFieldAreaStrip(Graphics g, ZeroThemePalette colors)
        {
            var stripRect = new Rectangle(0, 0, Width, _headerAreaHeight);
            using (var bgBrush = new SolidBrush(colors.Background))
            {
                g.FillRectangle(bgBrush, stripRect);
            }
            using (var pen = new Pen(colors.Border))
            {
                g.DrawLine(pen, 0, _headerAreaHeight - 1, Width, _headerAreaHeight - 1);
            }

            int curX = 8;
            for (int i = 0; i < _engine.Fields.Count; i++)
            {
                var f = _engine.Fields[i];
                if (!f.Visible) continue;

                Color badgeBg = f.Area switch
                {
                    PivotArea.RowArea => colors.Primary,
                    PivotArea.ColumnArea => colors.Info,
                    PivotArea.DataArea => colors.Success,
                    _ => colors.HeaderBackground
                };

                string tag = f.Area switch
                {
                    PivotArea.RowArea => "Row",
                    PivotArea.ColumnArea => "Col",
                    PivotArea.DataArea => $"{f.SummaryType}",
                    _ => "Filter"
                };

                string text = $"{f.Caption} ({tag})";
                var size = g.MeasureString(text, Font);
                int badgeWidth = (int)size.Width + 16;
                var badgeRect = new Rectangle(curX, 6, badgeWidth, 24);

                using (var brush = new SolidBrush(badgeBg))
                using (var path = CreateRoundedRectangle(badgeRect, 4))
                {
                    g.FillPath(brush, path);
                }

                using (var tBrush = new SolidBrush(f.Area == PivotArea.FilterArea ? colors.TextPrimary : Color.White))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(text, ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Bold), tBrush, badgeRect, sf);
                }

                curX += badgeWidth + 6;
                if (curX > Width - 120) break;
            }

            // Draw Field List Button
            _fieldListButtonRect = new Rectangle(Width - 104, 6, 96, 24);
            using (var bPath = CreateRoundedRectangle(_fieldListButtonRect, 4))
            {
                using (var bBrush = new SolidBrush(_hoverFieldListBtn ? colors.HeaderBackground : colors.Surface))
                {
                    g.FillPath(bBrush, bPath);
                }
                using (var bPen = new Pen(_hoverFieldListBtn ? colors.Primary : colors.Border, 1f))
                {
                    g.DrawPath(bPen, bPath);
                }
            }
            using (var tBrush = new SolidBrush(_hoverFieldListBtn ? colors.Primary : colors.TextPrimary))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("⚙ Field List", ZeroFontCache.Get("Segoe UI", 8.2f, FontStyle.Bold), tBrush, _fieldListButtonRect, sf);
            }
        }

        private void DrawColumnHeaders(Graphics g, ZeroThemePalette colors, int clientRight)
        {
            if (_model == null) return;

            var colClip = new Rectangle(_rowHeaderWidth, _headerAreaHeight, clientRight - _rowHeaderWidth, _columnHeaderHeight);
            var prevClip = g.Clip;
            g.SetClip(colClip);

            int startX = _rowHeaderWidth - _scrollX;

            for (int c = 0; c < _model.ColumnCount; c++)
            {
                int x = startX + c * _cellWidth;
                var cellRect = new Rectangle(x, _headerAreaHeight, _cellWidth, _columnHeaderHeight);

                using (var brush = new SolidBrush(colors.HeaderBackground))
                {
                    g.FillRectangle(brush, cellRect);
                }
                using (var pen = new Pen(colors.Border))
                {
                    g.DrawRectangle(pen, cellRect);
                }

                string title = _model.ColumnKeys[c].ToString();
                using (var brush = new SolidBrush(colors.TextPrimary))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
                    g.DrawString(title, ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Bold), brush, cellRect, sf);
                }
            }

            // Grand Total Column Header
            int gtX = startX + _model.ColumnCount * _cellWidth;
            var gtRect = new Rectangle(gtX, _headerAreaHeight, _cellWidth, _columnHeaderHeight);
            using (var brush = new SolidBrush(colors.HeaderBackground))
            {
                g.FillRectangle(brush, gtRect);
            }
            using (var pen = new Pen(colors.Border))
            {
                g.DrawRectangle(pen, gtRect);
            }
            using (var brush = new SolidBrush(colors.Primary))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(Localizer.GetString(StringId.PivotGrandTotal), ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Bold), brush, gtRect, sf);
            }

            g.Clip = prevClip;
        }

        private void DrawRowHeaders(Graphics g, ZeroThemePalette colors, int clientBottom, int matrixTop)
        {
            if (_model == null) return;

            var rowClip = new Rectangle(0, matrixTop, _rowHeaderWidth, clientBottom - matrixTop);
            var prevClip = g.Clip;
            g.SetClip(rowClip);

            int startY = matrixTop - _scrollY;

            for (int r = 0; r < _model.RowCount; r++)
            {
                int y = startY + r * _cellHeight;
                var cellRect = new Rectangle(0, y, _rowHeaderWidth, _cellHeight);

                using (var brush = new SolidBrush(colors.HeaderBackground))
                {
                    g.FillRectangle(brush, cellRect);
                }
                using (var pen = new Pen(colors.Border))
                {
                    g.DrawRectangle(pen, cellRect);
                }

                string title = _model.RowKeys[r].ToString();
                using (var brush = new SolidBrush(colors.TextPrimary))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
                    g.DrawString(title, Font, brush, new Rectangle(8, y, _rowHeaderWidth - 12, _cellHeight), sf);
                }
            }

            // Grand Total Row Header
            int gtY = startY + _model.RowCount * _cellHeight;
            var gtRect = new Rectangle(0, gtY, _rowHeaderWidth, _cellHeight);
            using (var brush = new SolidBrush(colors.HeaderBackground))
            {
                g.FillRectangle(brush, gtRect);
            }
            using (var pen = new Pen(colors.Border))
            {
                g.DrawRectangle(pen, gtRect);
            }
            using (var brush = new SolidBrush(colors.Primary))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                g.DrawString(Localizer.GetString(StringId.PivotGrandTotal), ZeroFontCache.Get("Segoe UI", 9f, FontStyle.Bold), brush, new Rectangle(8, gtY, _rowHeaderWidth - 12, _cellHeight), sf);
            }

            g.Clip = prevClip;
        }

        private void DrawDataCells(Graphics g, ZeroThemePalette colors, int matrixTop)
        {
            if (_model == null) return;

            int startX = _rowHeaderWidth - _scrollX;
            int startY = matrixTop - _scrollY;

            for (int r = 0; r < _model.RowCount; r++)
            {
                int y = startY + r * _cellHeight;

                for (int c = 0; c < _model.ColumnCount; c++)
                {
                    int x = startX + c * _cellWidth;
                    var cellRect = new Rectangle(x, y, _cellWidth, _cellHeight);

                    using (var brush = new SolidBrush(colors.Surface))
                    {
                        g.FillRectangle(brush, cellRect);
                    }
                    using (var pen = new Pen(colors.Border))
                    {
                        g.DrawRectangle(pen, cellRect);
                    }

                    object? val = _model.GetCellValue(r, c);
                    string text = _model.FormatValue(val);

                    if (!string.IsNullOrEmpty(text))
                    {
                        using (var brush = new SolidBrush(colors.TextPrimary))
                        {
                            var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                            g.DrawString(text, Font, brush, new Rectangle(x + 4, y, _cellWidth - 10, _cellHeight), sf);
                        }
                    }
                }

                // Row Total (Rightmost column for this row)
                int rTotX = startX + _model.ColumnCount * _cellWidth;
                var rTotRect = new Rectangle(rTotX, y, _cellWidth, _cellHeight);
                using (var brush = new SolidBrush(colors.Background))
                {
                    g.FillRectangle(brush, rTotRect);
                }
                using (var pen = new Pen(colors.Border))
                {
                    g.DrawRectangle(pen, rTotRect);
                }

                object? rTotVal = _model.GetRowTotal(r);
                string rTotText = _model.FormatValue(rTotVal);
                using (var brush = new SolidBrush(colors.TextPrimary))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                    g.DrawString(rTotText, ZeroFontCache.Get("Segoe UI", 9f, FontStyle.Bold), brush, new Rectangle(rTotX + 4, y, _cellWidth - 10, _cellHeight), sf);
                }
            }

            // Bottom Grand Total Row (Columns Totals)
            int botY = startY + _model.RowCount * _cellHeight;
            for (int c = 0; c < _model.ColumnCount; c++)
            {
                int x = startX + c * _cellWidth;
                var cTotRect = new Rectangle(x, botY, _cellWidth, _cellHeight);
                using (var brush = new SolidBrush(colors.Background))
                {
                    g.FillRectangle(brush, cTotRect);
                }
                using (var pen = new Pen(colors.Border))
                {
                    g.DrawRectangle(pen, cTotRect);
                }

                object? cTotVal = _model.GetColumnTotal(c);
                string cTotText = _model.FormatValue(cTotVal);
                using (var brush = new SolidBrush(colors.TextPrimary))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                    g.DrawString(cTotText, ZeroFontCache.Get("Segoe UI", 9f, FontStyle.Bold), brush, new Rectangle(x + 4, botY, _cellWidth - 10, _cellHeight), sf);
                }
            }

            // Bottom-Right Grand Total of Grand Totals
            int gtX = startX + _model.ColumnCount * _cellWidth;
            var grandTotalRect = new Rectangle(gtX, botY, _cellWidth, _cellHeight);
            using (var brush = new SolidBrush(colors.Primary))
            {
                g.FillRectangle(brush, grandTotalRect);
            }
            using (var pen = new Pen(colors.Border))
            {
                g.DrawRectangle(pen, grandTotalRect);
            }

            object? gtVal = _model.GetGrandTotal();
            string gtText = _model.FormatValue(gtVal);
            using (var brush = new SolidBrush(Color.White))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                g.DrawString(gtText, ZeroFontCache.Get("Segoe UI", 9.5f, FontStyle.Bold), brush, new Rectangle(gtX + 4, botY, _cellWidth - 10, _cellHeight), sf);
            }
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            var arc = new Rectangle(rect.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    
    private void OnThemeChanged(object sender, EventArgs e)
    {
                BackColor = ZeroTheme.Colors.Surface;
                Invalidate();
            }

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
    /// Backward-compatibility alias for <see cref="ZPivotGrid"/>.
    /// </summary>
    [Obsolete("ZeroPivotGrid is deprecated. Please use PivotGridControl instead.")]
    [ToolboxItem(false)]
    public class ZeroPivotGrid : PivotGridControl
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZPivotGrid"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PivotGridControl is deprecated and will be removed in 5 release cycles. Please migrate to ZPivotGrid instead.")]
    [ToolboxItem(false)]
    public class PivotGridControl : ZPivotGrid
    {
    }

    #endregion
}
