using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Data;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// High-performance Single-Visual cross-tab OLAP Pivot Grid / Matrix for WPF.
    /// Directly renders multi-dimensional aggregations, row/column dimensions, and grand totals.
    /// </summary>
    public class ZPivotMatrix : FrameworkElement
    {
        private readonly PivotDataEngine _engine = new PivotDataEngine();
        private int _headerHeight = 32;
        private int _rowHeight = 28;
        private int _rowHeaderWidth = 160;
        private int _columnWidth = 110;
        private int _scrollX = 0;
        private int _scrollY = 0;

        private int _selectedRow = -1;
        private int _selectedCol = -1;
        private int _hoveredRow = -1;
        private int _hoveredCol = -1;

        public PivotDataEngine Engine => _engine;

        public int RowHeaderWidth
        {
            get => _rowHeaderWidth;
            set { _rowHeaderWidth = Math.Max(80, value); InvalidateVisual(); }
        }

        public int ColumnWidth
        {
            get => _columnWidth;
            set { _columnWidth = Math.Max(60, value); InvalidateVisual(); }
        }

        public ZPivotMatrix()
        {
            ClipToBounds = true;
            Focusable = true;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public void RefreshData()
        {
            InvalidateVisual();
        }

#if NETFRAMEWORK
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush);
        }
#else
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush, pixelsPerDip);
        }
#endif

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 0 || height <= 0) return;

#if NETFRAMEWORK
            double dpi = 1.0;
#else
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
#endif

            // Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, new Rect(0, 0, width, height));

            int rowCount = _engine.RowKeys.Count;
            int colCount = _engine.ColumnKeys.Count;

            if (rowCount == 0 || colCount == 0)
            {
                var ph = CreateFormattedText("No multi-dimensional pivot data configured", ZeroWpfTheme.RegularTypeface, 13.0, ZeroWpfTheme.TextMuted, dpi);
                dc.DrawText(ph, new Point((width - ph.Width) / 2.0, (height - ph.Height) / 2.0));
                return;
            }

            int totalRows = rowCount + 1; // +1 for Grand Total row
            int totalCols = colCount + 1; // +1 for Grand Total col

            int clientH = (int)Math.Max(0, height - _headerHeight);
            int startRow = Math.Max(0, _scrollY / _rowHeight);
            int visibleRowCount = (clientH / _rowHeight) + 2;
            int endRow = Math.Min(totalRows - 1, startRow + visibleRowCount);

            var measure = _engine.Measures.Count > 0 ? _engine.Measures[0] : null;

            // 1. Render Cells (Unpinned body)
            double currentY = _headerHeight + (startRow * _rowHeight) - _scrollY;

            for (int r = startRow; r <= endRow && r < totalRows; r++)
            {
                if (currentY >= height) break;

                bool isGrandTotalRow = (r == rowCount);
                Brush rowBg = isGrandTotalRow
                    ? ZeroWpfTheme.BgInput
                    : ((r % 2 == 1) ? ZeroWpfTheme.BgInput : ZeroWpfTheme.BgCard);

                // Row background across available width
                dc.DrawRectangle(rowBg, null, new Rect(0, currentY, width, _rowHeight));

                // Draw cells across columns
                double cellX = _rowHeaderWidth - _scrollX;

                for (int c = 0; c < totalCols; c++)
                {
                    if (cellX + _columnWidth > _rowHeaderWidth && cellX < width)
                    {
                        bool isGrandTotalCol = (c == colCount);
                        bool isCellSelected = (r == _selectedRow && c == _selectedCol);
                        bool isCellHovered = (r == _hoveredRow && c == _hoveredCol && !isCellSelected);

                        if (isCellSelected)
                        {
                            dc.DrawRectangle(ZeroWpfTheme.SelectionBackground, null, new Rect(cellX, currentY, _columnWidth, _rowHeight));
                        }
                        else if (isCellHovered)
                        {
                            dc.DrawRectangle(ZeroWpfTheme.BgHover, null, new Rect(cellX, currentY, _columnWidth, _rowHeight));
                        }
                        else if (isGrandTotalRow || isGrandTotalCol)
                        {
                            // Subtle highlight for totals
                            dc.DrawRectangle(ZeroWpfTheme.BgActive, null, new Rect(cellX, currentY, _columnWidth, _rowHeight));
                        }

                        // Cell value
                        double val = 0;
                        if (isGrandTotalRow && isGrandTotalCol) val = _engine.GrandTotal;
                        else if (isGrandTotalRow) val = _engine.ColumnGrandTotals[c];
                        else if (isGrandTotalCol) val = _engine.RowGrandTotals[r];
                        else val = _engine.Cells[r, c];

                        string valText = measure != null ? measure.FormatValue(val) : val.ToString("N2");
                        Typeface tf = (isGrandTotalRow || isGrandTotalCol) ? ZeroWpfTheme.BoldTypeface : ZeroWpfTheme.RegularTypeface;
                        Brush textBrush = isCellSelected
                            ? ZeroWpfTheme.SelectionForeground
                            : ((isGrandTotalRow || isGrandTotalCol) ? ZeroWpfTheme.PrimaryAccent : ZeroWpfTheme.TextPrimary);

                        var ft = CreateFormattedText(valText, tf, 11.5, textBrush, dpi);
                        double tx = cellX + _columnWidth - ft.Width - 8.0;
                        double ty = currentY + (_rowHeight - ft.Height) / 2.0;
                        dc.DrawText(ft, new Point(tx, ty));

                        // Column right grid line
                        dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(cellX + _columnWidth - 0.5, currentY), new Point(cellX + _columnWidth - 0.5, currentY + _rowHeight));
                    }

                    cellX += _columnWidth;
                }

                // Row bottom grid line
                dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(0, currentY + _rowHeight - 0.5), new Point(width, currentY + _rowHeight - 0.5));
                currentY += _rowHeight;
            }

            // 2. Render Pinned Row Headers on left
            double rhY = _headerHeight + (startRow * _rowHeight) - _scrollY;
            for (int r = startRow; r <= endRow && r < totalRows; r++)
            {
                if (rhY >= height) break;

                bool isGrandTotalRow = (r == rowCount);
                string rTitle = isGrandTotalRow ? "Grand Total" : _engine.RowKeys[r];

                Brush rhBg = isGrandTotalRow ? ZeroWpfTheme.BgActive : ZeroWpfTheme.BgCard;
                dc.DrawRectangle(rhBg, null, new Rect(0, rhY, _rowHeaderWidth, _rowHeight));

                Typeface rtf = isGrandTotalRow ? ZeroWpfTheme.BoldTypeface : ZeroWpfTheme.RegularTypeface;
                Brush rTextBrush = isGrandTotalRow ? ZeroWpfTheme.PrimaryAccent : ZeroWpfTheme.TextPrimary;

                var ft = CreateFormattedText(rTitle, rtf, 11.5, rTextBrush, dpi);
                dc.DrawText(ft, new Point(10, rhY + (_rowHeight - ft.Height) / 2.0));

                dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(0, rhY + _rowHeight - 0.5), new Point(_rowHeaderWidth, rhY + _rowHeight - 0.5));
                rhY += _rowHeight;
            }

            // Pinned row header vertical separator
            dc.DrawLine(new Pen(ZeroWpfTheme.PrimaryAccent, 1.5), new Point(_rowHeaderWidth - 0.5, 0), new Point(_rowHeaderWidth - 0.5, height));

            // 3. Render Top Column Headers
            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, new Rect(0, 0, width, _headerHeight));
            dc.DrawLine(ZeroWpfTheme.BorderPen, new Point(0, _headerHeight - 0.5), new Point(width, _headerHeight - 0.5));

            // Top-left corner box
            string cornerLabel = (measure != null ? $"{measure.Name} ({measure.SummaryType})" : "Measure");
            var ftCorner = CreateFormattedText(cornerLabel, ZeroWpfTheme.BoldTypeface, 11.0, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(ftCorner, new Point(10, (_headerHeight - ftCorner.Height) / 2.0));

            // Column Header titles
            double chX = _rowHeaderWidth - _scrollX;
            for (int c = 0; c < totalCols; c++)
            {
                if (chX + _columnWidth > _rowHeaderWidth && chX < width)
                {
                    bool isGrandTotalCol = (c == colCount);
                    string colTitle = isGrandTotalCol ? "Total" : _engine.ColumnKeys[c];

                    var ft = CreateFormattedText(colTitle, ZeroWpfTheme.BoldTypeface, 11.5, isGrandTotalCol ? ZeroWpfTheme.PrimaryAccent : ZeroWpfTheme.TextPrimary, dpi);
                    double cx = chX + (_columnWidth - ft.Width) / 2.0;
                    double cy = (_headerHeight - ft.Height) / 2.0;
                    dc.DrawText(ft, new Point(cx, cy));

                    dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(chX + _columnWidth - 0.5, 4), new Point(chX + _columnWidth - 0.5, _headerHeight - 4));
                }

                chX += _columnWidth;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point pt = e.GetPosition(this);

            int prevR = _hoveredRow;
            int prevC = _hoveredCol;

            if (pt.Y > _headerHeight && pt.X > _rowHeaderWidth)
            {
                _hoveredRow = (int)((pt.Y - _headerHeight + _scrollY) / _rowHeight);
                _hoveredCol = (int)((pt.X - _rowHeaderWidth + _scrollX) / _columnWidth);
            }
            else
            {
                _hoveredRow = -1;
                _hoveredCol = -1;
            }

            if (prevR != _hoveredRow || prevC != _hoveredCol)
            {
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredRow = -1;
            _hoveredCol = -1;
            InvalidateVisual();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            Point pt = e.GetPosition(this);

            if (pt.Y > _headerHeight && pt.X > _rowHeaderWidth)
            {
                _selectedRow = (int)((pt.Y - _headerHeight + _scrollY) / _rowHeight);
                _selectedCol = (int)((pt.X - _rowHeaderWidth + _scrollX) / _columnWidth);
                InvalidateVisual();
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            int delta = (e.Delta / 120) * _rowHeight * 3;
            _scrollY = Math.Max(0, _scrollY - delta);
            InvalidateVisual();
        }
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged += InvalidateVisual;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged -= InvalidateVisual;
    }
}

    /// <summary>
    /// Backward-compatibility alias for <see cref="PivotMatrix"/>.
    /// </summary>
    [Obsolete("ZeroPivotGrid in Industrial is deprecated. Use PivotMatrix or ZeroUI.Wpf.PivotGrid.PivotGridControl instead.")]
    public class ZeroPivotGrid : PivotMatrix
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZPivotMatrix"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PivotMatrix is deprecated and will be removed in 5 release cycles. Please migrate to ZPivotMatrix instead.")]
    public class PivotMatrix : ZPivotMatrix { }

    /// <summary>
    /// Legacy alias for <see cref="ZPivotMatrix"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroPivotMatrix is deprecated and will be removed in 5 release cycles. Please migrate to ZPivotMatrix instead.")]
    public class ZeroPivotMatrix : ZPivotMatrix { }

    #endregion

}
