using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ZeroUI.Core.Data;
using ZeroUI.WinForms.Editors;

namespace ZeroUI.WinForms.DataGrid.Repositories
{
    /// <summary>
    /// In-place multi-column GridLookup editor repository item for GridControl cells.
    /// Embeds a virtual multi-column searchable DataGrid dropdown inside table cells,
    /// enabling fast selection of complex entities (e.g. Materials, Suppliers, Cost Centers).
    /// </summary>
    public class RepositoryItemGridLookupEdit : IRepositoryItemWinForms
    {
        public virtual string EditorTypeName => "GridLookupEdit";

        public IZeroVirtualSource? DataSource { get; set; }
        public List<ZeroColumn> Columns { get; } = new List<ZeroColumn>();

        public string DisplayMember { get; set; } = "Name";
        public string ValueMember { get; set; } = "Id";
        public string Placeholder { get; set; } = "Select item...";

        public int PopupWidth { get; set; } = 480;
        public int PopupHeight { get; set; } = 300;

        public bool ShowAddNewButton { get; set; } = false;
        public string AddNewButtonText { get; set; } = "+ Add New Record";
        public string? NullText { get; set; }

        public event EventHandler<ProcessNewValueEventArgs>? ProcessNewValue;

        public void SetDataSource<T>(IList<T> items)
        {
            DataSource = new ListSource<T>(items);
        }

        public virtual Control CreateInPlaceEditor()
        {
            var gle = new ZGridLookupEdit
            {
                DisplayMember = DisplayMember,
                ValueMember = ValueMember,
                Placeholder = Placeholder,
                ShowAddNewButton = ShowAddNewButton,
                AddNewButtonText = AddNewButtonText
            };

            if (Columns.Count > 0)
            {
                gle.Grid.Columns.Clear();
                foreach (var col in Columns)
                {
                    gle.Grid.Columns.Add(new ZeroColumn(col.FieldName, col.HeaderText, col.Width, col.Alignment));
                }
            }

            if (DataSource != null)
            {
                gle.Grid.DataSource = DataSource;
            }

            if (ProcessNewValue != null)
            {
                gle.ProcessNewValue += (s, e) => ProcessNewValue.Invoke(this, e);
            }

            return gle;
        }

        public virtual string FormatValue(object? rawValue)
        {
            if (rawValue == null) return NullText ?? string.Empty;
            return rawValue.ToString() ?? string.Empty;
        }

        public virtual bool ParseEditValue(ReadOnlySpan<char> text, out object? parsedValue)
        {
            parsedValue = text.ToString();
            return true;
        }
    }
}
