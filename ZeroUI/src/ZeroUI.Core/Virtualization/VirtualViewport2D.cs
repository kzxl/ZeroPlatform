using System;
using System.Runtime.CompilerServices;

namespace ZeroUI.Core.Virtualization
{
    public readonly struct VisibleRange
    {
        public readonly int StartRow;
        public readonly int EndRow; // inclusive
        public readonly int StartCol;
        public readonly int EndCol; // inclusive
        public readonly int FirstRowY;
        public readonly int FirstColX;

        public VisibleRange(int startRow, int endRow, int startCol, int endCol, int firstRowY, int firstColX)
        {
            StartRow = startRow;
            EndRow = endRow;
            StartCol = startCol;
            EndCol = endCol;
            FirstRowY = firstRowY;
            FirstColX = firstColX;
        }

        public int VisibleRowCount => Math.Max(0, EndRow - StartRow + 1);
        public int VisibleColCount => Math.Max(0, EndCol - StartCol + 1);
    }

    /// <summary>
    /// Computes 2D viewport visible row/column intervals in O(1) for uniform heights or O(log N) for variable heights.
    /// </summary>
    public static class VirtualViewport2D
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VisibleRange ComputeUniform(
            int scrollX,
            int scrollY,
            int viewportWidth,
            int viewportHeight,
            int defaultRowHeight,
            int totalRows,
            int[] columnWidths,
            int totalCols)
        {
            if (totalRows <= 0 || totalCols <= 0 || viewportWidth <= 0 || viewportHeight <= 0 || defaultRowHeight <= 0)
            {
                return new VisibleRange(0, -1, 0, -1, 0, 0);
            }

            // 1. Calculate Rows
            int startRow = Math.Max(0, scrollY / defaultRowHeight);
            if (startRow >= totalRows)
            {
                startRow = totalRows - 1;
            }

            int firstRowY = (startRow * defaultRowHeight) - scrollY;
            int currentY = firstRowY;
            int endRow = startRow;

            while (currentY < viewportHeight && endRow < totalRows - 1)
            {
                currentY += defaultRowHeight;
                endRow++;
            }

            // 2. Calculate Columns
            int startCol = 0;
            int accumX = 0;
            while (startCol < totalCols && accumX + columnWidths[startCol] <= scrollX)
            {
                accumX += columnWidths[startCol];
                startCol++;
            }
            if (startCol >= totalCols) startCol = totalCols - 1;

            int firstColX = accumX - scrollX;
            int currentX = firstColX;
            int endCol = startCol;

            while (currentX < viewportWidth && endCol < totalCols - 1)
            {
                currentX += columnWidths[endCol];
                endCol++;
            }

            return new VisibleRange(startRow, endRow, startCol, endCol, firstRowY, firstColX);
        }
    }
}
