using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ZeroUI.Core.Data;

namespace ZeroUI.WinForms.DataGrid
{
    /// <summary>
    /// Event arguments for dynamically providing child detail data or controls when expanding a Master row.
    /// </summary>
    public sealed class MasterRowGetChildDataEventArgs : EventArgs
    {
        public int VisualRow { get; }
        public int ModelRow { get; }
        public object? MasterRowData { get; }

        /// <summary>
        /// Provides a child virtual data source to be displayed in the nested detail grid.
        /// </summary>
        public IZeroVirtualSource? ChildDataSource { get; set; }

        /// <summary>
        /// Optional column definitions for the nested detail grid. If null, auto-inferred or inherited.
        /// </summary>
        public List<ZeroColumn>? ChildColumns { get; set; }

        /// <summary>
        /// Alternatively provides a fully custom Control (e.g. customized GridControl, TabControl, or UserControl)
        /// to host inside the expanded detail container.
        /// </summary>
        public Control? ChildControl { get; set; }

        /// <summary>
        /// Specifies whether this master row has child records. If false, expand button is dimmed or suppressed.
        /// </summary>
        public bool HasChildren { get; set; } = true;

        public MasterRowGetChildDataEventArgs(int visualRow, int modelRow, object? masterRowData)
        {
            VisualRow = visualRow;
            ModelRow = modelRow;
            MasterRowData = masterRowData;
        }
    }

    /// <summary>
    /// Event arguments for master row expanding event with cancellation support.
    /// </summary>
    public sealed class MasterRowExpandingEventArgs : EventArgs
    {
        public int VisualRow { get; }
        public int ModelRow { get; }
        public bool Cancel { get; set; } = false;

        public MasterRowExpandingEventArgs(int visualRow, int modelRow)
        {
            VisualRow = visualRow;
            ModelRow = modelRow;
        }
    }

    /// <summary>
    /// Event arguments for master row collapsed event.
    /// </summary>
    public sealed class MasterRowCollapsedEventArgs : EventArgs
    {
        public int VisualRow { get; }
        public int ModelRow { get; }

        public MasterRowCollapsedEventArgs(int visualRow, int modelRow)
        {
            VisualRow = visualRow;
            ModelRow = modelRow;
        }
    }
}
