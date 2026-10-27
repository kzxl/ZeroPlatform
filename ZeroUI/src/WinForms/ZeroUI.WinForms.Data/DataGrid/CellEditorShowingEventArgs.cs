using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace ZeroUI.WinForms.DataGrid
{
    /// <summary>
    /// Event arguments raised before an in-place cell editor is displayed, allowing customization or provision of a custom editor control.
    /// </summary>
    public class CellEditorShowingEventArgs : CancelEventArgs
    {
        public int VisualRowIndex { get; }
        public int ModelRowIndex { get; }
        public int ColumnIndex { get; }
        public Control? CustomEditor { get; set; }

        public CellEditorShowingEventArgs(int visualRowIndex, int modelRowIndex, int columnIndex)
        {
            VisualRowIndex = visualRowIndex;
            ModelRowIndex = modelRowIndex;
            ColumnIndex = columnIndex;
        }
    }
}
