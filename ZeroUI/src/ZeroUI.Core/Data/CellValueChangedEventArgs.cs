using System;

namespace ZeroUI.Core.Data
{
    /// <summary>
    /// Event arguments raised when a cell value is committed through in-place editing.
    /// </summary>
    public class CellValueChangedEventArgs : EventArgs
    {
        public int VisualRowIndex { get; }
        public int ModelRowIndex { get; }
        public int ColumnIndex { get; }
        public string OldValue { get; }
        public string NewValue { get; }

        public CellValueChangedEventArgs(int visualRowIndex, int modelRowIndex, int columnIndex, string oldValue, string newValue)
        {
            VisualRowIndex = visualRowIndex;
            ModelRowIndex = modelRowIndex;
            ColumnIndex = columnIndex;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}
