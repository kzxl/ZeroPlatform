using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Virtualization;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.DataGrid
{
    public partial class ZGrid
    {
        private readonly struct FormattedTextCacheKey : IEquatable<FormattedTextCacheKey>
        {
            public readonly string Text;
            public readonly Typeface Typeface;
            public readonly double FontSize;
            public readonly Brush Brush;
            public readonly double Dpi;

            public FormattedTextCacheKey(string text, Typeface typeface, double fontSize, Brush brush, double dpi)
            {
                Text = text;
                Typeface = typeface;
                FontSize = fontSize;
                Brush = brush;
                Dpi = dpi;
            }

            public bool Equals(FormattedTextCacheKey other)
            {
                return Text == other.Text &&
                       FontSize.Equals(other.FontSize) &&
                       Dpi.Equals(other.Dpi) &&
                       ReferenceEquals(Typeface, other.Typeface) &&
                       ReferenceEquals(Brush, other.Brush);
            }

            public override bool Equals(object? obj) => obj is FormattedTextCacheKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Text != null ? Text.GetHashCode() : 0;
                    hash = (hash * 397) ^ (Typeface != null ? Typeface.GetHashCode() : 0);
                    hash = (hash * 397) ^ FontSize.GetHashCode();
                    hash = (hash * 397) ^ (Brush != null ? Brush.GetHashCode() : 0);
                    hash = (hash * 397) ^ Dpi.GetHashCode();
                    return hash;
                }
            }
        }

        private readonly Dictionary<FormattedTextCacheKey, FormattedText> _formattedTextCache = new Dictionary<FormattedTextCacheKey, FormattedText>(256);


        private int GetMaxBandDepth()
        {
            int maxDepth = 0;
            for (int i = 0; i < _bands.Count; i++)
            {
                int d = _bands[i].GetMaxDepth();
                if (d > maxDepth) maxDepth = d;
            }
            return maxDepth;
        }

        public int GetPinnedColumnsWidth()
        {
            int total = 0;
            for (int i = 0; i < _columns.Count; i++)
            {
                if (_columns[i].IsVisible && _columns[i].IsPinned) total += _columns[i].Width;
            }
            return total;
        }

        public int GetUnpinnedColumnsWidth()
        {
            int total = 0;
            for (int i = 0; i < _columns.Count; i++)
            {
                if (_columns[i].IsVisible && !_columns[i].IsPinned) total += _columns[i].Width;
            }
            return total;
        }

        public bool[] GetColumnPinnedFlags()
        {
            bool[] flags = new bool[_columns.Count];
            for (int i = 0; i < _columns.Count; i++)
            {
                flags[i] = _columns[i].IsVisible && _columns[i].IsPinned;
            }
            return flags;
        }

        public int GetTotalColumnsWidth()
        {
            int total = 0;
            for (int i = 0; i < _columns.Count; i++)
            {
                if (_columns[i].IsVisible) total += _columns[i].Width;
            }
            return total;
        }

        private int[] GetVisibleColumnWidths()
        {
            int[] widths = new int[_columns.Count];
            for (int i = 0; i < _columns.Count; i++)
            {
                widths[i] = _columns[i].IsVisible ? _columns[i].Width : 0;
            }
            return widths;
        }

        private int GetMaxScrollY()
        {
            int footerH = ShowFooter ? _footerHeight : 0;
            int totalRows = VisualRowCount;
            int totalH = totalRows * _rowHeight;
            int clientH = (int)Math.Max(0, ActualHeight - TotalTopOffset - footerH);
            return Math.Max(0, totalH - clientH);
        }

        private int GetMaxScrollX()
        {
            int unpinnedW = GetUnpinnedColumnsWidth();
            int pinnedW = GetPinnedColumnsWidth();
            int scrollableW = (int)Math.Max(0, ActualWidth - pinnedW - ScrollBarThickness);
            return Math.Max(0, unpinnedW - scrollableW);
        }


        private static bool IsTruthy(ReadOnlySpan<char> span)
        {
            if (span.IsEmpty) return false;
            if (span.Length == 1)
            {
                char c = span[0];
                return c == '1' || c == 't' || c == 'T' || c == 'y' || c == 'Y';
            }
            if (span.Length == 4)
            {
                return (span[0] == 't' || span[0] == 'T') &&
                       (span[1] == 'r' || span[1] == 'R') &&
                       (span[2] == 'u' || span[2] == 'U') &&
                       (span[3] == 'e' || span[3] == 'E');
            }
            if (span.Length == 3)
            {
                return (span[0] == 'y' || span[0] == 'Y') &&
                       (span[1] == 'e' || span[1] == 'E') &&
                       (span[2] == 's' || span[2] == 'S');
            }
            return false;
        }

        private void DrawVectorCheckBox(DrawingContext dc, double x, double y, double width, double height, bool isChecked, bool isSelected)
        {
            double cbSize = 16.0;
            double cbX = x + (width - cbSize) / 2.0;
            double cbY = y + (height - cbSize) / 2.0;
            var rect = new Rect(cbX, cbY, cbSize, cbSize);

            if (isChecked)
            {
                dc.DrawRoundedRectangle(ZeroWpfTheme.PrimaryAccent, null, rect, 3.0, 3.0);

                var checkGeometry = new StreamGeometry();
                using (var ctx = checkGeometry.Open())
                {
                    ctx.BeginFigure(new Point(cbX + 3.5, cbY + 8.0), false, false);
                    ctx.LineTo(new Point(cbX + 6.5, cbY + 11.5), true, false);
                    ctx.LineTo(new Point(cbX + 12.5, cbY + 4.5), true, false);
                }
                checkGeometry.Freeze();
                dc.DrawGeometry(null, WhiteCheckPen, checkGeometry);
            }
            else
            {
                Brush bg = isSelected ? ZeroWpfTheme.SelectionBackground : ZeroWpfTheme.BgInput;
                dc.DrawRoundedRectangle(bg, ZeroWpfTheme.BorderPen, rect, 3.0, 3.0);
            }
        }

        private void DrawDataBar(DrawingContext dc, double x, double y, double width, double height, float percent, Brush? barBrush = null)
        {
            float clamped = Math.Max(0.0f, Math.Min(1.0f, percent));
            double barH = Math.Max(6.0, height - 10.0);
            double barMaxW = Math.Max(0.0, width - 16.0);
            double barFillW = barMaxW * clamped;
            double barX = x + 8.0;
            double barY = y + (height - barH) / 2.0;

            // Track
            dc.DrawRoundedRectangle(ZeroWpfTheme.BgInput, null, new Rect(barX, barY, barMaxW, barH), 2.0, 2.0);
            // Fill
            if (barFillW > 0)
            {
                Brush brush = barBrush ?? ZeroWpfTheme.PrimaryAccent;
                dc.DrawRoundedRectangle(brush, null, new Rect(barX, barY, barFillW, barH), 2.0, 2.0);
            }
        }

        private void DrawSparkline(DrawingContext dc, double x, double y, double width, double height, ReadOnlySpan<float> values, SparklineType type, Brush? strokeBrush = null)
        {
            if (values.Length < 2) return;

            double padX = 6.0;
            double padY = 4.0;
            double availW = Math.Max(0.0, width - (padX * 2.0));
            double availH = Math.Max(0.0, height - (padY * 2.0));
            if (availW <= 0 || availH <= 0) return;

            float min = values[0];
            float max = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] < min) min = values[i];
                if (values[i] > max) max = values[i];
            }
            float range = (max - min <= 0.0001f) ? 1.0f : (max - min);

            Brush brush = strokeBrush ?? ZeroWpfTheme.PrimaryAccent;
            Pen pen = new Pen(brush, 1.5);
            pen.Freeze();

            if (type == SparklineType.Bar)
            {
                double barSlotW = availW / values.Length;
                double barWidth = Math.Max(1.0, barSlotW - 2.0);
                for (int i = 0; i < values.Length; i++)
                {
                    float norm = (values[i] - min) / range;
                    double bh = Math.Max(2.0, norm * availH);
                    double bx = x + padX + (i * barSlotW) + 1.0;
                    double by = y + padY + availH - bh;
                    dc.DrawRectangle(brush, null, new Rect(bx, by, barWidth, bh));
                }
            }
            else
            {
                // Line or Area
                var geom = new StreamGeometry();
                using (var ctx = geom.Open())
                {
                    double stepX = availW / (values.Length - 1);
                    double firstY = y + padY + availH - (((values[0] - min) / range) * availH);
                    ctx.BeginFigure(new Point(x + padX, firstY), type == SparklineType.Area, type == SparklineType.Area);

                    for (int i = 1; i < values.Length; i++)
                    {
                        double px = x + padX + (i * stepX);
                        double py = y + padY + availH - (((values[i] - min) / range) * availH);
                        ctx.LineTo(new Point(px, py), true, false);
                    }

                    if (type == SparklineType.Area)
                    {
                        ctx.LineTo(new Point(x + padX + availW, y + padY + availH), true, false);
                        ctx.LineTo(new Point(x + padX, y + padY + availH), true, false);
                    }
                }
                geom.Freeze();

                if (type == SparklineType.Area)
                {
                    var areaBrush = brush.Clone();
                    areaBrush.Opacity = 0.25;
                    areaBrush.Freeze();
                    dc.DrawGeometry(areaBrush, pen, geom);
                }
                else
                {
                    dc.DrawGeometry(null, pen, geom);
                }
            }
        }

        private FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            var key = new FormattedTextCacheKey(text, typeface, fontSize, brush, pixelsPerDip);
            if (_formattedTextCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            if (_formattedTextCache.Count > 512)
            {
                _formattedTextCache.Clear();
            }

            #if NETFRAMEWORK
            var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush);
            #else
            var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush, pixelsPerDip);
            #endif

            _formattedTextCache[key] = ft;
            return ft;
        }

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

            // 1. Render Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, new Rect(0, 0, width, height));

            int totalCols = _columns.Count;
            int totalRows = VisualRowCount;
            int[] colWidths = GetVisibleColumnWidths();
            int pinnedW = GetPinnedColumnsWidth();
            int footerH = ShowFooter ? _footerHeight : 0;
            int effHeaderH = EffectiveHeaderHeight;
            int groupPanelH = ShowGroupPanel ? _groupPanelHeight : 0;
            int totalTopOffset = groupPanelH + effHeaderH;
            int bandDepth = (_bands.Count > 0) ? GetMaxBandDepth() : 0;
            int bandH = bandDepth * _headerHeight;
            int clientDataH = (int)Math.Max(0, height - totalTopOffset - footerH);

            // 2. Render Virtual Cells
            if (_dataSource != null && totalRows > 0 && totalCols > 0)
            {
                int startRow = Math.Max(0, _scrollY / _rowHeight);
                int visibleRowCount = (clientDataH / _rowHeight) + 2;
                int endRow = Math.Min(totalRows - 1, startRow + visibleRowCount);

                CellValueBuffer cellBuffer = new CellValueBuffer();
                double currentY = totalTopOffset + (startRow * _rowHeight) - _scrollY;

                for (int r = startRow; r <= endRow && r < totalRows; r++)
                {
                    if (currentY >= totalTopOffset + clientDataH) break;

                    if (_groupedMap.HasGrouping)
                    {
                        var rowEntry = _groupedMap[r];
                        if (rowEntry.IsGroup)
                        {
                            var groupInfo = _groupedMap.GetGroupInfo(rowEntry.GroupId);
                            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, new Rect(0, currentY, width - ScrollBarThickness, _rowHeight));
                            double indent = rowEntry.Level * 18.0;
                            string expandIcon = rowEntry.IsExpanded ? "▼ " : "▶ ";
                            string colHeader = (groupInfo != null && groupInfo.ColumnIndex >= 0 && groupInfo.ColumnIndex < _columns.Count)
                                ? _columns[groupInfo.ColumnIndex].HeaderText
                                : "Group";
                            string groupText = $"{expandIcon}{colHeader}: {(groupInfo?.GroupKey ?? string.Empty)} ({groupInfo?.TotalDataRowCount ?? 0} items)";

                            var ftGroup = CreateFormattedText(groupText, ZeroWpfTheme.BoldTypeface, 12.0, ZeroWpfTheme.PrimaryAccent, dpi);
                            dc.DrawText(ftGroup, new Point(8 + indent, currentY + (_rowHeight - ftGroup.Height) / 2.0));

                            if (groupInfo != null && !string.IsNullOrEmpty(groupInfo.FormattedSummaryText))
                            {
                                var ftSummary = CreateFormattedText(groupInfo.FormattedSummaryText!, ZeroWpfTheme.BoldTypeface, 11.5, ZeroWpfTheme.TextSecondary, dpi);
                                double sX = 8 + indent + ftGroup.Width + 18.0;
                                if (sX + ftSummary.Width < width - ScrollBarThickness)
                                {
                                    dc.DrawText(ftSummary, new Point(sX, currentY + (_rowHeight - ftSummary.Height) / 2.0));
                                }
                            }

                            dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(0, currentY + _rowHeight - 0.5), new Point(width - ScrollBarThickness, currentY + _rowHeight - 0.5));
                            currentY += _rowHeight;
                            continue;
                        }
                    }

                    int modelRow = GetModelRowIndex(r);
                    if (modelRow < 0)
                    {
                        currentY += _rowHeight;
                        continue;
                    }

                    bool isSelected = IsVisualRowSelected(r);
                    bool isHovered = (r == _hoveredVisualRow && !isSelected);

                    Brush rowBrush = isSelected ? ZeroWpfTheme.SelectionBackground :
                                     isHovered ? ZeroWpfTheme.BgHover :
                                     ((r % 2 == 1) ? ZeroWpfTheme.BgInput : ZeroWpfTheme.BgCard);

                    // Row background
                    dc.DrawRectangle(rowBrush, null, new Rect(0, currentY, width - ScrollBarThickness, _rowHeight));

                    if (isSelected)
                    {
                        // Left active indicator strip
                        dc.DrawRectangle(ZeroWpfTheme.PrimaryAccent, null, new Rect(0, currentY, 3.5, _rowHeight));
                    }

                    // (A) Draw Unpinned Cells
                    double unpinnedX = pinnedW - _scrollX;
                    for (int c = 0; c < totalCols; c++)
                    {
                        if (!_columns[c].IsVisible || _columns[c].IsPinned) continue;
                        int colW = colWidths[c];
                        if (colW <= 0) continue;

                        if (unpinnedX + colW > pinnedW && unpinnedX < width - ScrollBarThickness)
                        {
                            cellBuffer.Reset();
                            cellBuffer.Alignment = _columns[c].Alignment;
                            _dataSource.GetCellValue(modelRow, c, ref cellBuffer);

                            bool isBlockSelected = (_selectionMode == ZeroGridSelectionMode.Block && !_selectedBlock.IsEmpty && _selectedBlock.Contains(r, c));
                            if (isBlockSelected && !isSelected)
                            {
                                dc.DrawRectangle(ZeroWpfTheme.SelectionBackground, null, new Rect(unpinnedX, currentY, colW, _rowHeight));
                            }

                            bool isMerged = false;
                            if (_columns[c].AllowCellMerge && r > startRow)
                            {
                                int prevModel = GetModelRowIndex(r - 1);
                                if (prevModel >= 0)
                                {
                                    CellValueBuffer prevBuf = new CellValueBuffer();
                                    _dataSource.GetCellValue(prevModel, c, ref prevBuf);
                                    if (prevBuf.Text.SequenceEqual(cellBuffer.Text))
                                    {
                                        isMerged = true;
                                    }
                                }
                            }

                            if (cellBuffer.HasCustomBackground && !isSelected && !isBlockSelected)
                            {
                                byte a = (byte)((cellBuffer.BackColor >> 24) & 0xFF);
                                byte b = (byte)((cellBuffer.BackColor >> 16) & 0xFF);
                                byte g = (byte)((cellBuffer.BackColor >> 8) & 0xFF);
                                byte rCol = (byte)(cellBuffer.BackColor & 0xFF);
                                if (a == 0) a = 255;
                                var customBrush = new SolidColorBrush(Color.FromArgb(a, rCol, g, b));
                                dc.DrawRectangle(customBrush, null, new Rect(unpinnedX, currentY, colW, _rowHeight));
                            }

                            if (_columns[c].ColumnType == GridColumnType.Boolean)
                            {
                                bool isChecked = IsTruthy(cellBuffer.Text);
                                DrawVectorCheckBox(dc, unpinnedX, currentY, colW, _rowHeight, isChecked, isSelected);
                            }
                            else if (cellBuffer.DataBarPercent >= 0.0f)
                            {
                                DrawDataBar(dc, unpinnedX, currentY, colW, _rowHeight, cellBuffer.DataBarPercent);
                                var textSpan = cellBuffer.Text;
                                if (!textSpan.IsEmpty)
                                {
                                    string text = cellBuffer.Text.ToString();
                                    Brush textBrush = (isSelected || isBlockSelected) ? ZeroWpfTheme.SelectionForeground : ZeroWpfTheme.TextPrimary;
                                    Typeface tf = (isSelected || isBlockSelected) ? ZeroWpfTheme.BoldTypeface : ZeroWpfTheme.RegularTypeface;
                                    var ft = CreateFormattedText(text, tf, 12.0, textBrush, dpi);
                                    double textX = unpinnedX + 8;
                                    if (cellBuffer.Alignment == CellAlignment.Right) textX = unpinnedX + colW - ft.Width - 8;
                                    else if (cellBuffer.Alignment == CellAlignment.Center) textX = unpinnedX + (colW - ft.Width) / 2.0;
                                    double textY = currentY + (_rowHeight - ft.Height) / 2.0;
                                    dc.DrawText(ft, new Point(textX, textY));
                                }
                            }
                            else if (!cellBuffer.SparklineValues.IsEmpty || _columns[c].Sparkline != SparklineType.None)
                            {
                                SparklineType sType = _columns[c].Sparkline != SparklineType.None ? _columns[c].Sparkline : SparklineType.Line;
                                DrawSparkline(dc, unpinnedX, currentY, colW, _rowHeight, cellBuffer.SparklineValues, sType);
                            }
                            else if (!isMerged)
                            {
                                var textSpan = cellBuffer.Text;
                                if (!textSpan.IsEmpty)
                                {
                                    string text = cellBuffer.Text.ToString();
                                    Brush textBrush = (isSelected || isBlockSelected) ? ZeroWpfTheme.SelectionForeground : ZeroWpfTheme.TextPrimary;
                                    Typeface tf = (isSelected || isBlockSelected) ? ZeroWpfTheme.BoldTypeface : ZeroWpfTheme.RegularTypeface;

                                    if (!isSelected && !isBlockSelected && cellBuffer.TextColor != 0)
                                    {
                                        byte tr = (byte)(cellBuffer.TextColor & 0xFF);
                                        byte tg = (byte)((cellBuffer.TextColor >> 8) & 0xFF);
                                        byte tb = (byte)((cellBuffer.TextColor >> 16) & 0xFF);
                                        var customTextBrush = new SolidColorBrush(Color.FromRgb(tr, tg, tb));
                                        customTextBrush.Freeze();
                                        textBrush = customTextBrush;
                                    }

                                    var ft = CreateFormattedText(text, tf, 12.0, textBrush, dpi);
                                    double textX = unpinnedX + 8;
                                    if (cellBuffer.Alignment == CellAlignment.Right) textX = unpinnedX + colW - ft.Width - 8;
                                    else if (cellBuffer.Alignment == CellAlignment.Center) textX = unpinnedX + (colW - ft.Width) / 2.0;

                                    double textY = currentY + (_rowHeight - ft.Height) / 2.0;
                                    dc.DrawText(ft, new Point(textX, textY));
                                }
                            }

                            if (isBlockSelected)
                            {
                                var blockPen = new Pen(ZeroWpfTheme.PrimaryAccent, 1.5);
                                if (r == _selectedBlock.TopRow)
                                    dc.DrawLine(blockPen, new Point(unpinnedX, currentY), new Point(unpinnedX + colW, currentY));
                                if (r == _selectedBlock.BottomRow)
                                    dc.DrawLine(blockPen, new Point(unpinnedX, currentY + _rowHeight), new Point(unpinnedX + colW, currentY + _rowHeight));
                                if (c == _selectedBlock.LeftColumn)
                                    dc.DrawLine(blockPen, new Point(unpinnedX, currentY), new Point(unpinnedX, currentY + _rowHeight));
                                if (c == _selectedBlock.RightColumn)
                                    dc.DrawLine(blockPen, new Point(unpinnedX + colW, currentY), new Point(unpinnedX + colW, currentY + _rowHeight));
                            }

                            dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(unpinnedX + colW - 0.5, currentY), new Point(unpinnedX + colW - 0.5, currentY + _rowHeight));
                        }

                        unpinnedX += colW;
                    }

                    // (B) Draw Pinned Cells on top
                    double pinnedX = 0;
                    for (int c = 0; c < totalCols; c++)
                    {
                        if (!_columns[c].IsVisible || !_columns[c].IsPinned) continue;
                        int colW = colWidths[c];
                        if (colW <= 0) continue;

                        cellBuffer.Reset();
                        cellBuffer.Alignment = _columns[c].Alignment;
                        _dataSource.GetCellValue(modelRow, c, ref cellBuffer);

                        bool isBlockSelected = (_selectionMode == ZeroGridSelectionMode.Block && !_selectedBlock.IsEmpty && _selectedBlock.Contains(r, c));
                        if (isBlockSelected && !isSelected)
                        {
                            dc.DrawRectangle(ZeroWpfTheme.SelectionBackground, null, new Rect(pinnedX, currentY, colW, _rowHeight));
                        }

                        bool isMerged = false;
                        if (_columns[c].AllowCellMerge && r > startRow)
                        {
                            int prevModel = GetModelRowIndex(r - 1);
                            if (prevModel >= 0)
                            {
                                CellValueBuffer prevBuf = new CellValueBuffer();
                                _dataSource.GetCellValue(prevModel, c, ref prevBuf);
                                if (prevBuf.Text.SequenceEqual(cellBuffer.Text))
                                {
                                    isMerged = true;
                                }
                            }
                        }

                        if (cellBuffer.HasCustomBackground && !isSelected && !isBlockSelected)
                        {
                            byte a = (byte)((cellBuffer.BackColor >> 24) & 0xFF);
                            byte b = (byte)((cellBuffer.BackColor >> 16) & 0xFF);
                            byte g = (byte)((cellBuffer.BackColor >> 8) & 0xFF);
                            byte rCol = (byte)(cellBuffer.BackColor & 0xFF);
                            if (a == 0) a = 255;
                            var customBrush = new SolidColorBrush(Color.FromArgb(a, rCol, g, b));
                            dc.DrawRectangle(customBrush, null, new Rect(pinnedX, currentY, colW, _rowHeight));
                        }

                        if (_columns[c].ColumnType == GridColumnType.Boolean)
                        {
                            bool isChecked = IsTruthy(cellBuffer.Text);
                            DrawVectorCheckBox(dc, pinnedX, currentY, colW, _rowHeight, isChecked, isSelected);
                        }
                        else if (cellBuffer.DataBarPercent >= 0.0f)
                        {
                            DrawDataBar(dc, pinnedX, currentY, colW, _rowHeight, cellBuffer.DataBarPercent);
                            var textSpan = cellBuffer.Text;
                            if (!textSpan.IsEmpty)
                            {
                                string text = cellBuffer.Text.ToString();
                                Brush textBrush = (isSelected || isBlockSelected) ? ZeroWpfTheme.SelectionForeground : ZeroWpfTheme.TextPrimary;
                                Typeface tf = (isSelected || isBlockSelected) ? ZeroWpfTheme.BoldTypeface : ZeroWpfTheme.RegularTypeface;
                                var ft = CreateFormattedText(text, tf, 12.0, textBrush, dpi);
                                double textX = pinnedX + 8;
                                if (cellBuffer.Alignment == CellAlignment.Right) textX = pinnedX + colW - ft.Width - 8;
                                else if (cellBuffer.Alignment == CellAlignment.Center) textX = pinnedX + (colW - ft.Width) / 2.0;
                                double textY = currentY + (_rowHeight - ft.Height) / 2.0;
                                dc.DrawText(ft, new Point(textX, textY));
                            }
                        }
                        else if (!cellBuffer.SparklineValues.IsEmpty || _columns[c].Sparkline != SparklineType.None)
                        {
                            SparklineType sType = _columns[c].Sparkline != SparklineType.None ? _columns[c].Sparkline : SparklineType.Line;
                            DrawSparkline(dc, pinnedX, currentY, colW, _rowHeight, cellBuffer.SparklineValues, sType);
                        }
                        else if (!isMerged)
                        {
                            var textSpan = cellBuffer.Text;
                            if (!textSpan.IsEmpty)
                            {
                                string text = cellBuffer.Text.ToString();
                                Brush textBrush = (isSelected || isBlockSelected) ? ZeroWpfTheme.SelectionForeground : ZeroWpfTheme.TextPrimary;
                                Typeface tf = (isSelected || isBlockSelected) ? ZeroWpfTheme.BoldTypeface : ZeroWpfTheme.RegularTypeface;

                                if (!isSelected && !isBlockSelected && cellBuffer.TextColor != 0)
                                {
                                    byte tr = (byte)(cellBuffer.TextColor & 0xFF);
                                    byte tg = (byte)((cellBuffer.TextColor >> 8) & 0xFF);
                                    byte tb = (byte)((cellBuffer.TextColor >> 16) & 0xFF);
                                    var customTextBrush = new SolidColorBrush(Color.FromRgb(tr, tg, tb));
                                    customTextBrush.Freeze();
                                    textBrush = customTextBrush;
                                }

                                var ft = CreateFormattedText(text, tf, 12.0, textBrush, dpi);
                                double textX = pinnedX + 8;
                                if (cellBuffer.Alignment == CellAlignment.Right) textX = pinnedX + colW - ft.Width - 8;
                                else if (cellBuffer.Alignment == CellAlignment.Center) textX = pinnedX + (colW - ft.Width) / 2.0;

                                double textY = currentY + (_rowHeight - ft.Height) / 2.0;
                                dc.DrawText(ft, new Point(textX, textY));
                            }
                        }

                        if (isBlockSelected)
                        {
                            var blockPen = new Pen(ZeroWpfTheme.PrimaryAccent, 1.5);
                            if (r == _selectedBlock.TopRow)
                                dc.DrawLine(blockPen, new Point(pinnedX, currentY), new Point(pinnedX + colW, currentY));
                            if (r == _selectedBlock.BottomRow)
                                dc.DrawLine(blockPen, new Point(pinnedX, currentY + _rowHeight), new Point(pinnedX + colW, currentY + _rowHeight));
                            if (c == _selectedBlock.LeftColumn)
                                dc.DrawLine(blockPen, new Point(pinnedX, currentY), new Point(pinnedX, currentY + _rowHeight));
                            if (c == _selectedBlock.RightColumn)
                                dc.DrawLine(blockPen, new Point(pinnedX + colW, currentY), new Point(pinnedX + colW, currentY + _rowHeight));
                        }

                        dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(pinnedX + colW - 0.5, currentY), new Point(pinnedX + colW - 0.5, currentY + _rowHeight));

                        pinnedX += colW;
                    }

                    // Row horizontal border
                    dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(0, currentY + _rowHeight - 0.5), new Point(width - ScrollBarThickness, currentY + _rowHeight - 0.5));
                    currentY += _rowHeight;
                }

                if (pinnedW > 0)
                {
                    dc.DrawLine(new Pen(ZeroWpfTheme.PrimaryAccent, 2.0), new Point(pinnedW - 1, totalTopOffset), new Point(pinnedW - 1, height - footerH));
                }
            }
            else
            {
                // Empty state
                var emptyTitle = CreateFormattedText("No records to display", ZeroWpfTheme.BoldTypeface, 14.0, ZeroWpfTheme.TextSecondary, dpi);
                var emptySub = CreateFormattedText("Try adjusting your filters or loading sample data", ZeroWpfTheme.RegularTypeface, 12.0, ZeroWpfTheme.TextMuted, dpi);

                double midY = (totalTopOffset + height - footerH) / 2.0 - 20;
                dc.DrawText(emptyTitle, new Point((width - emptyTitle.Width) / 2.0, midY));
                dc.DrawText(emptySub, new Point((width - emptySub.Width) / 2.0, midY + 24));
            }

            // 3. Render Header Row (Always pinned on top)
            if (ShowGroupPanel && groupPanelH > 0)
            {
                dc.DrawRectangle(ZeroWpfTheme.BgInput, null, new Rect(0, 0, width, groupPanelH));
                dc.DrawLine(ZeroWpfTheme.BorderPen, new Point(0, groupPanelH - 0.5), new Point(width, groupPanelH - 0.5));

                _groupChipBounds.Clear();

                if (_groupColumnIndices.Length == 0)
                {
                    var phText = CreateFormattedText("Drag a column header here to group by that column", ZeroWpfTheme.RegularTypeface, 11.5, ZeroWpfTheme.TextMuted, dpi);
                    dc.DrawText(phText, new Point(14, (groupPanelH - phText.Height) / 2.0));
                }
                else
                {
                    double chipX = 12;
                    for (int i = 0; i < _groupColumnIndices.Length; i++)
                    {
                        int colIdx = _groupColumnIndices[i];
                        string colName = (colIdx >= 0 && colIdx < _columns.Count) ? _columns[colIdx].HeaderText : $"Col {colIdx}";
                        var cft = CreateFormattedText(colName, ZeroWpfTheme.BoldTypeface, 11.0, ZeroWpfTheme.TextPrimary, dpi);
                        double chipW = cft.Width + 32;
                        double chipY = 4;
                        double chipH = groupPanelH - 8;
                        Rect chipRect = new Rect(chipX, chipY, chipW, chipH);
                        Rect closeRect = new Rect(chipX + chipW - 18, chipY, 16, chipH);

                        dc.DrawRoundedRectangle(ZeroWpfTheme.BgCard, new Pen(ZeroWpfTheme.PrimaryAccent, 1.2), chipRect, 4, 4);
                        dc.DrawText(cft, new Point(chipX + 8, chipY + (chipH - cft.Height) / 2.0));

                        var xft = CreateFormattedText("✕", ZeroWpfTheme.BoldTypeface, 10.5, ZeroWpfTheme.TextSecondary, dpi);
                        dc.DrawText(xft, new Point(closeRect.X + (closeRect.Width - xft.Width) / 2.0, chipY + (chipH - xft.Height) / 2.0));

                        _groupChipBounds.Add(new GroupChipInfo { ColumnIndex = colIdx, ChipRect = chipRect, CloseRect = closeRect });
                        chipX += chipW + 8;
                    }
                }
            }

            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, new Rect(0, groupPanelH, width, effHeaderH));
            dc.DrawLine(ZeroWpfTheme.BorderPen, new Point(0, totalTopOffset - 0.5), new Point(width, totalTopOffset - 0.5));

            if (_bands.Count > 0)
            {
                var bandEntries = GridBand.ComputeLayout(_bands, (int)(pinnedW - _scrollX), groupPanelH, _headerHeight, bandDepth);
                for (int b = 0; b < bandEntries.Count; b++)
                {
                    var entry = bandEntries[b];
                    if (entry.X + entry.Width > 0 && entry.X < width)
                    {
                        Rect bRect = new Rect(entry.X, entry.Y, entry.Width, entry.Height);
                        dc.DrawRectangle(ZeroWpfTheme.BgInput, null, bRect);
                        var bft = CreateFormattedText(entry.Band.Title, ZeroWpfTheme.BoldTypeface, 12.0, ZeroWpfTheme.TextPrimary, dpi);
                        dc.DrawText(bft, new Point(entry.X + (entry.Width - bft.Width) / 2.0, entry.Y + (entry.Height - bft.Height) / 2.0));
                        dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(entry.X + entry.Width - 0.5, entry.Y), new Point(entry.X + entry.Width - 0.5, entry.Y + entry.Height));
                        dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(entry.X, entry.Y + entry.Height - 0.5), new Point(entry.X + entry.Width, entry.Y + entry.Height - 0.5));
                    }
                }
            }

            _columnFilterButtonBounds.Clear();

            // (A) Draw Unpinned Headers
            double unpinnedHdrX = pinnedW - _scrollX;
            for (int c = 0; c < totalCols; c++)
            {
                if (!_columns[c].IsVisible || _columns[c].IsPinned) continue;
                int colW = colWidths[c];
                if (colW <= 0) continue;

                if (unpinnedHdrX + colW > pinnedW && unpinnedHdrX < width - ScrollBarThickness)
                {
                    string headerText = _columns[c].HeaderText;
                    if (_isSorting && _sortingColumnIndex == c) headerText += " ⏳";
                    else if (_columns[c].SortOrder == SortDirection.Ascending) headerText += " ▲";
                    else if (_columns[c].SortOrder == SortDirection.Descending) headerText += " ▼";

                    var hft = CreateFormattedText(headerText, ZeroWpfTheme.BoldTypeface, 12.0, ZeroWpfTheme.TextPrimary, dpi);
                    double hTextX = unpinnedHdrX + 8;
                    if (_columns[c].Alignment == CellAlignment.Right) hTextX = unpinnedHdrX + colW - hft.Width - 24;
                    else if (_columns[c].Alignment == CellAlignment.Center) hTextX = unpinnedHdrX + (colW - hft.Width) / 2.0;

                    double hTextY = groupPanelH + bandH + (_headerHeight - hft.Height) / 2.0;
                    dc.DrawText(hft, new Point(hTextX, hTextY));

                    if (_columns[c].AllowFiltering)
                    {
                        Rect filterBtnRect = new Rect(unpinnedHdrX + colW - 20, groupPanelH + bandH + (_headerHeight - 14) / 2.0, 16, 14);
                        _columnFilterButtonBounds[c] = filterBtnRect;

                        bool isFiltered = IsColumnFiltered(c);
                        Brush fBrush = isFiltered ? ZeroWpfTheme.PrimaryAccent : ZeroWpfTheme.TextMuted;
                        var ftFilter = CreateFormattedText("▼", ZeroWpfTheme.BoldTypeface, 8.5, fBrush, dpi);
                        dc.DrawText(ftFilter, new Point(filterBtnRect.X + (filterBtnRect.Width - ftFilter.Width) / 2.0, filterBtnRect.Y + (filterBtnRect.Height - ftFilter.Height) / 2.0));
                    }

                    dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(unpinnedHdrX + colW - 0.5, groupPanelH + bandH + 4), new Point(unpinnedHdrX + colW - 0.5, groupPanelH + bandH + _headerHeight - 4));
                }

                unpinnedHdrX += colW;
            }

            // (B) Draw Pinned Headers
            double pinnedHdrX = 0;
            for (int c = 0; c < totalCols; c++)
            {
                if (!_columns[c].IsVisible || !_columns[c].IsPinned) continue;
                int colW = colWidths[c];
                if (colW <= 0) continue;

                string headerText = _columns[c].HeaderText;
                if (_isSorting && _sortingColumnIndex == c) headerText += " ⏳";
                else if (_columns[c].SortOrder == SortDirection.Ascending) headerText += " ▲";
                else if (_columns[c].SortOrder == SortDirection.Descending) headerText += " ▼";

                var hft = CreateFormattedText(headerText, ZeroWpfTheme.BoldTypeface, 12.0, ZeroWpfTheme.TextPrimary, dpi);
                double hTextX = pinnedHdrX + 8;
                if (_columns[c].Alignment == CellAlignment.Right) hTextX = pinnedHdrX + colW - hft.Width - 24;
                else if (_columns[c].Alignment == CellAlignment.Center) hTextX = pinnedHdrX + (colW - hft.Width) / 2.0;

                double hTextY = groupPanelH + bandH + (_headerHeight - hft.Height) / 2.0;
                dc.DrawText(hft, new Point(hTextX, hTextY));

                if (_columns[c].AllowFiltering)
                {
                    Rect filterBtnRect = new Rect(pinnedHdrX + colW - 20, groupPanelH + bandH + (_headerHeight - 14) / 2.0, 16, 14);
                    _columnFilterButtonBounds[c] = filterBtnRect;

                    bool isFiltered = IsColumnFiltered(c);
                    Brush fBrush = isFiltered ? ZeroWpfTheme.PrimaryAccent : ZeroWpfTheme.TextMuted;
                    var ftFilter = CreateFormattedText("▼", ZeroWpfTheme.BoldTypeface, 8.5, fBrush, dpi);
                    dc.DrawText(ftFilter, new Point(filterBtnRect.X + (filterBtnRect.Width - ftFilter.Width) / 2.0, filterBtnRect.Y + (filterBtnRect.Height - ftFilter.Height) / 2.0));
                }

                dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(pinnedHdrX + colW - 0.5, groupPanelH + bandH + 4), new Point(pinnedHdrX + colW - 0.5, groupPanelH + bandH + _headerHeight - 4));

                pinnedHdrX += colW;
            }

            if (pinnedW > 0)
            {
                dc.DrawLine(new Pen(ZeroWpfTheme.PrimaryAccent, 2.0), new Point(pinnedW - 1, groupPanelH), new Point(pinnedW - 1, totalTopOffset));
            }

            if (_isDraggingHeader && _draggedHeaderCol >= 0 && _draggedHeaderCol < _columns.Count)
            {
                string dragText = _columns[_draggedHeaderCol].HeaderText;
                var dft = CreateFormattedText(dragText, ZeroWpfTheme.BoldTypeface, 11.5, Brushes.White, dpi);
                double badgeW = dft.Width + 24;
                double badgeH = 26;
                Rect badgeRect = new Rect(_currentMousePos.X - badgeW / 2.0, _currentMousePos.Y - badgeH / 2.0, badgeW, badgeH);
                dc.DrawRoundedRectangle(ZeroWpfTheme.PrimaryAccent, new Pen(Brushes.White, 1.0), badgeRect, 4, 4);
                dc.DrawText(dft, new Point(badgeRect.X + 12, badgeRect.Y + (badgeH - dft.Height) / 2.0));

                if (_allowColumnReordering && _reorderDropTargetIndex >= 0)
                {
                    double indicatorX = GetColumnHeaderScreenX(_reorderDropTargetIndex);
                    double indicatorY = groupPanelH;
                    double indicatorH = EffectiveHeaderHeight;
                    Pen indicatorPen = new Pen(ZeroWpfTheme.PrimaryAccent, 2.5);
                    indicatorPen.Freeze();
                    dc.DrawLine(indicatorPen, new Point(indicatorX, indicatorY), new Point(indicatorX, indicatorY + indicatorH));

                    StreamGeometry markerGeo = new StreamGeometry();
                    using (var ctx = markerGeo.Open())
                    {
                        ctx.BeginFigure(new Point(indicatorX - 4, indicatorY), true, true);
                        ctx.LineTo(new Point(indicatorX + 4, indicatorY), true, false);
                        ctx.LineTo(new Point(indicatorX, indicatorY + 5), true, false);

                        ctx.BeginFigure(new Point(indicatorX - 4, indicatorY + indicatorH), true, true);
                        ctx.LineTo(new Point(indicatorX + 4, indicatorY + indicatorH), true, false);
                        ctx.LineTo(new Point(indicatorX, indicatorY + indicatorH - 5), true, false);
                    }
                    markerGeo.Freeze();
                    dc.DrawGeometry(ZeroWpfTheme.PrimaryAccent, null, markerGeo);
                }
            }

            // 4. Render Footer Summary Bar (if enabled)
            if (ShowFooter && footerH > 0)
            {
                double footerY = height - footerH;
                dc.DrawRectangle(ZeroWpfTheme.BgCard, null, new Rect(0, footerY, width, footerH));
                dc.DrawLine(ZeroWpfTheme.BorderPen, new Point(0, footerY), new Point(width, footerY));

                // (A) Unpinned Footers
                double unpinnedFootX = pinnedW - _scrollX;
                for (int c = 0; c < totalCols; c++)
                {
                    if (!_columns[c].IsVisible || _columns[c].IsPinned) continue;
                    int colW = colWidths[c];
                    if (colW <= 0) continue;

                    if (unpinnedFootX + colW > pinnedW && unpinnedFootX < width - ScrollBarThickness)
                    {
                        string sText = GetColumnSummaryText(c);
                        if (!string.IsNullOrEmpty(sText))
                        {
                            var sft = CreateFormattedText(sText, ZeroWpfTheme.BoldTypeface, 12.0, ZeroWpfTheme.TextPrimary, dpi);
                            double sX = unpinnedFootX + 8;
                            if (_columns[c].Alignment == CellAlignment.Right) sX = unpinnedFootX + colW - sft.Width - 8;
                            else if (_columns[c].Alignment == CellAlignment.Center) sX = unpinnedFootX + (colW - sft.Width) / 2.0;

                            double sY = footerY + (footerH - sft.Height) / 2.0;
                            dc.DrawText(sft, new Point(sX, sY));
                        }
                        dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(unpinnedFootX + colW - 0.5, footerY + 4), new Point(unpinnedFootX + colW - 0.5, footerY + footerH - 4));
                    }
                    unpinnedFootX += colW;
                }

                // (B) Pinned Footers
                double pinnedFootX = 0;
                for (int c = 0; c < totalCols; c++)
                {
                    if (!_columns[c].IsVisible || !_columns[c].IsPinned) continue;
                    int colW = colWidths[c];
                    if (colW <= 0) continue;

                    string sText = GetColumnSummaryText(c);
                    if (!string.IsNullOrEmpty(sText))
                    {
                        var sft = CreateFormattedText(sText, ZeroWpfTheme.BoldTypeface, 12.0, ZeroWpfTheme.TextPrimary, dpi);
                        double sX = pinnedFootX + 8;
                        if (_columns[c].Alignment == CellAlignment.Right) sX = pinnedFootX + colW - sft.Width - 8;
                        else if (_columns[c].Alignment == CellAlignment.Center) sX = pinnedFootX + (colW - sft.Width) / 2.0;

                        double sY = footerY + (footerH - sft.Height) / 2.0;
                        dc.DrawText(sft, new Point(sX, sY));
                    }
                    dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(pinnedFootX + colW - 0.5, footerY + 4), new Point(pinnedFootX + colW - 0.5, footerY + footerH - 4));

                    pinnedFootX += colW;
                }

                if (pinnedW > 0)
                {
                    dc.DrawLine(new Pen(ZeroWpfTheme.PrimaryAccent, 2.0), new Point(pinnedW - 1, footerY), new Point(pinnedW - 1, height));
                }
            }

            // 5. Render Slim Modern ScrollBar
            RenderSlimScrollBar(dc, width, height);
        }


        private void RenderSlimScrollBar(DrawingContext dc, double width, double height)
        {
            int footerH = ShowFooter ? _footerHeight : 0;
            int totalRows = VisualRowCount;
            int totalH = totalRows * _rowHeight;
            int topOffset = TotalTopOffset;
            double clientH = Math.Max(0, height - topOffset - footerH);

            if (totalH <= clientH || clientH <= 0) return;

            double trackX = width - ScrollBarThickness;
            double trackY = topOffset;
            double trackH = clientH;

            // Track background
            dc.DrawRectangle(ZeroWpfTheme.BgInput, null, new Rect(trackX, trackY, ScrollBarThickness, trackH));

            // Thumb
            double thumbH = Math.Max(20, (clientH / totalH) * trackH);
            double maxScroll = totalH - clientH;
            double thumbRatio = Math.Min(1.0, Math.Max(0.0, (double)_scrollY / maxScroll));
            double thumbY = trackY + thumbRatio * (trackH - thumbH);

            Brush thumbBrush = (_isDraggingVThumb || _isVThumbHovered) ? ZeroWpfTheme.PrimaryAccent : ZeroWpfTheme.TextMuted;
            dc.DrawRoundedRectangle(thumbBrush, null, new Rect(trackX + 1, thumbY, ScrollBarThickness - 2, thumbH), 3, 3);
        }
    }
}
