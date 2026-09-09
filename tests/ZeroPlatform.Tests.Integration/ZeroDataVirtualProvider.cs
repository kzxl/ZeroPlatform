using System;
using System.Linq;
using ZeroData.Core;
using ZeroUI.Core.Data;

namespace ZeroPlatform.Tests.Integration
{
    /// <summary>
    /// Zero-copy virtual data source bridging ZeroData DataFrame into ZeroUI virtualized grids.
    /// </summary>
    public class ZeroDataVirtualProvider : IZeroVirtualSource
    {
        private readonly DataFrame _dataFrame;
        private readonly string[] _columnNames;

        public DataFrame DataFrame => _dataFrame;
        public int TotalRowCount => _dataFrame.RowCount;
        public int TotalColumnCount => _columnNames.Length;

        public ZeroDataVirtualProvider(DataFrame dataFrame)
        {
            _dataFrame = dataFrame ?? throw new ArgumentNullException(nameof(dataFrame));
            _columnNames = dataFrame.ColumnNames.ToArray();
        }

        public void GetCellValue(int rowIndex, int columnIndex, ref CellValueBuffer buffer)
        {
            if (rowIndex < 0 || rowIndex >= _dataFrame.RowCount || columnIndex < 0 || columnIndex >= _columnNames.Length)
            {
                buffer.Reset();
                return;
            }

            var col = _dataFrame[_columnNames[columnIndex]];
            object? val = col.GetValue(rowIndex);
            string s = val?.ToString() ?? string.Empty;
            buffer.Text = s.AsSpan();
        }
    }
}
