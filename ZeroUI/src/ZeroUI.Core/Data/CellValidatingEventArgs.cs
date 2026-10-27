using System;
using System.ComponentModel;

namespace ZeroUI.Core.Data
{
    /// <summary>
    /// Event arguments raised before committing an in-place cell edit, allowing cancellation and error messaging.
    /// </summary>
    public class CellValidatingEventArgs : CancelEventArgs
    {
        public int VisualRowIndex { get; }
        public int ModelRowIndex { get; }
        public int ColumnIndex { get; }
        public string OldValue { get; }
        public string NewValue { get; }
        public string? ErrorMessage { get; set; }

        public CellValidatingEventArgs(int visualRowIndex, int modelRowIndex, int columnIndex, string oldValue, string newValue)
        {
            VisualRowIndex = visualRowIndex;
            ModelRowIndex = modelRowIndex;
            ColumnIndex = columnIndex;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}
