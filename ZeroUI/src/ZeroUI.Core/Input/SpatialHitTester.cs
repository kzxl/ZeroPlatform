using System;
using ZeroUI.Core.Layout;

namespace ZeroUI.Core.Input
{
    public enum HitRegion : byte
    {
        None = 0,
        Header = 1,
        ColumnResizeGrip = 2,
        Cell = 3,
        RowIndicator = 4,
        Footer = 5,
        AutoFilterRow = 6
    }

    public readonly struct HitTestResult
    {
        public readonly HitRegion Region;
        public readonly int RowIndex;
        public readonly int ColumnIndex;
        public readonly int ResizeColumnIndex;
        public readonly CellBounds Bounds;

        public HitTestResult(HitRegion region, int rowIndex, int columnIndex, int resizeColIndex, in CellBounds bounds)
        {
            Region = region;
            RowIndex = rowIndex;
            ColumnIndex = columnIndex;
            ResizeColumnIndex = resizeColIndex;
            Bounds = bounds;
        }

        public static HitTestResult Empty => new HitTestResult(HitRegion.None, -1, -1, -1, default);
    }

    public static class SpatialHitTester
    {
        private const int GripTolerance = 4; // +/- 4px around column boundary

        public static HitTestResult HitTest(
            int clientX,
            int clientY,
            int headerHeight,
            int defaultRowHeight,
            int scrollX,
            int scrollY,
            int[] columnWidths,
            int totalCols,
            int totalRows)
        {
            return HitTest(
                clientX,
                clientY,
                headerHeight,
                defaultRowHeight,
                scrollX,
                scrollY,
                columnWidths,
                null,
                totalCols,
                totalRows,
                0,
                0,
                0,
                0);
        }

        public static HitTestResult HitTest(
            int clientX,
            int clientY,
            int headerHeight,
            int defaultRowHeight,
            int scrollX,
            int scrollY,
            int[] columnWidths,
            bool[]? isPinned,
            int totalCols,
            int totalRows,
            int footerHeight = 0,
            int clientHeight = 0,
            int pinnedOffset = 0,
            int autoFilterRowHeight = 0)
        {
            if (clientX < 0 || clientY < 0 || totalCols <= 0 || totalRows <= 0)
            {
                return HitTestResult.Empty;
            }

            // Check Footer Region
            if (footerHeight > 0 && clientHeight > 0 && clientY >= clientHeight - footerHeight)
            {
                return new HitTestResult(HitRegion.Footer, -1, -1, -1, new CellBounds(0, clientHeight - footerHeight, clientX, footerHeight));
            }

            int pinnedWidth = pinnedOffset;
            if (isPinned != null)
            {
                for (int c = 0; c < totalCols; c++)
                {
                    if (isPinned[c]) pinnedWidth += columnWidths[c];
                }
            }

            bool inPinnedZone = clientX < pinnedWidth;

            // 1. Check Header Region
            if (clientY < headerHeight)
            {
                if (pinnedOffset > 0 && clientX < pinnedOffset)
                {
                    return new HitTestResult(HitRegion.RowIndicator, -1, -1, -1, new CellBounds(0, 0, pinnedOffset, headerHeight));
                }

                if (inPinnedZone && isPinned != null)
                {
                    int curX = pinnedOffset;
                    for (int c = 0; c < totalCols; c++)
                    {
                        if (!isPinned[c]) continue;
                        int colW = columnWidths[c];
                        int colRight = curX + colW;

                        if (Math.Abs(clientX - colRight) <= GripTolerance)
                        {
                            return new HitTestResult(HitRegion.ColumnResizeGrip, -1, c, c, new CellBounds(curX, 0, colW, headerHeight));
                        }
                        if (clientX >= curX && clientX < colRight)
                        {
                            return new HitTestResult(HitRegion.Header, -1, c, -1, new CellBounds(curX, 0, colW, headerHeight));
                        }
                        curX = colRight;
                    }
                }
                else
                {
                    int curX = (isPinned != null ? pinnedWidth : 0) - scrollX;
                    for (int c = 0; c < totalCols; c++)
                    {
                        if (isPinned != null && isPinned[c]) continue;
                        int colW = columnWidths[c];
                        int colRight = curX + colW;

                        if (Math.Abs(clientX - colRight) <= GripTolerance)
                        {
                            return new HitTestResult(HitRegion.ColumnResizeGrip, -1, c, c, new CellBounds(curX, 0, colW, headerHeight));
                        }
                        if (clientX >= curX && clientX < colRight)
                        {
                            return new HitTestResult(HitRegion.Header, -1, c, -1, new CellBounds(curX, 0, colW, headerHeight));
                        }
                        curX = colRight;
                    }
                }
                return HitTestResult.Empty;
            }

            // 2. Check Auto Filter Row Region
            if (autoFilterRowHeight > 0 && clientY >= headerHeight && clientY < headerHeight + autoFilterRowHeight)
            {
                if (pinnedOffset > 0 && clientX < pinnedOffset)
                {
                    return new HitTestResult(HitRegion.RowIndicator, -1, -1, -1, new CellBounds(0, headerHeight, pinnedOffset, autoFilterRowHeight));
                }

                if (inPinnedZone && isPinned != null)
                {
                    int curX = pinnedOffset;
                    for (int c = 0; c < totalCols; c++)
                    {
                        if (!isPinned[c]) continue;
                        int colW = columnWidths[c];
                        int colRight = curX + colW;

                        if (clientX >= curX && clientX < colRight)
                        {
                            return new HitTestResult(HitRegion.AutoFilterRow, -1, c, -1, new CellBounds(curX, headerHeight, colW, autoFilterRowHeight));
                        }
                        curX = colRight;
                    }
                }
                else
                {
                    int curX = (isPinned != null ? pinnedWidth : 0) - scrollX;
                    for (int c = 0; c < totalCols; c++)
                    {
                        if (isPinned != null && isPinned[c]) continue;
                        int colW = columnWidths[c];
                        int colRight = curX + colW;

                        if (clientX >= curX && clientX < colRight)
                        {
                            return new HitTestResult(HitRegion.AutoFilterRow, -1, c, -1, new CellBounds(curX, headerHeight, colW, autoFilterRowHeight));
                        }
                        curX = colRight;
                    }
                }
                return HitTestResult.Empty;
            }

            // 3. Check Data Cells Region
            int topOffset = headerHeight + autoFilterRowHeight;
            int dataY = clientY - topOffset;
            int visualRow = (dataY + scrollY) / defaultRowHeight;

            if (visualRow < 0 || visualRow >= totalRows)
            {
                return HitTestResult.Empty;
            }

            int cellY = topOffset + (visualRow * defaultRowHeight) - scrollY;

            if (pinnedOffset > 0 && clientX < pinnedOffset)
            {
                return new HitTestResult(HitRegion.RowIndicator, visualRow, -1, -1, new CellBounds(0, cellY, pinnedOffset, defaultRowHeight));
            }

            if (inPinnedZone && isPinned != null)
            {
                int curX = pinnedOffset;
                for (int c = 0; c < totalCols; c++)
                {
                    if (!isPinned[c]) continue;
                    int colW = columnWidths[c];
                    int colRight = curX + colW;

                    if (clientX >= curX && clientX < colRight)
                    {
                        return new HitTestResult(HitRegion.Cell, visualRow, c, -1, new CellBounds(curX, cellY, colW, defaultRowHeight));
                    }
                    curX = colRight;
                }
            }
            else
            {
                int curX = (isPinned != null ? pinnedWidth : 0) - scrollX;
                for (int c = 0; c < totalCols; c++)
                {
                    if (isPinned != null && isPinned[c]) continue;
                    int colW = columnWidths[c];
                    int colRight = curX + colW;

                    if (clientX >= curX && clientX < colRight)
                    {
                        return new HitTestResult(HitRegion.Cell, visualRow, c, -1, new CellBounds(curX, cellY, colW, defaultRowHeight));
                    }
                    curX = colRight;
                }
            }

            return HitTestResult.Empty;
        }
    }
}
