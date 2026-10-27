using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.WinForms.Editors;
using ProcessNewValueEventArgs = ZeroUI.WinForms.Editors.ProcessNewValueEventArgs;

namespace ZeroUI.WinForms.DataGrid.Repositories
{
    /// <summary>
    /// In-place simple autocomplete LookUp editor repository item for GridControl cells.
    /// Embeds a fast virtualized dropdown lookup box inside table cells,
    /// enabling keyboard-driven autocomplete selection for catalog/enum lists.
    /// </summary>
    public class RepositoryItemLookUpEdit : IRepositoryItemWinForms
    {
        public virtual string EditorTypeName => "LookUpEdit";

        public List<LookUpItem> Items { get; } = new List<LookUpItem>();
        public string Placeholder { get; set; } = "Search item...";
        public bool ShowAddNewButton { get; set; } = false;
        public string AddNewButtonText { get; set; } = "+ Add New Record";
        public string? NullText { get; set; }

        public event EventHandler<ProcessNewValueEventArgs>? ProcessNewValue;

        public virtual Control CreateInPlaceEditor()
        {
            var lue = new ZLookUpEdit
            {
                Placeholder = Placeholder,
                ShowAddNewButton = ShowAddNewButton,
                AddNewButtonText = AddNewButtonText
            };

            if (Items.Count > 0)
            {
                lue.SetItems(Items);
            }

            if (ProcessNewValue != null)
            {
                lue.ProcessNewValue += (s, e) => ProcessNewValue.Invoke(this, e);
            }

            return lue;
        }

        public virtual string FormatValue(object? rawValue)
        {
            if (rawValue == null) return NullText ?? string.Empty;
            string rawStr = rawValue.ToString() ?? string.Empty;

            var found = Items.Find(x => string.Equals(x.Key, rawStr, StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(x.DisplayText, rawStr, StringComparison.OrdinalIgnoreCase));
            return found?.DisplayText ?? rawStr;
        }

        public virtual bool ParseEditValue(ReadOnlySpan<char> text, out object? parsedValue)
        {
            string str = text.ToString();
            var found = Items.Find(x => string.Equals(x.DisplayText, str, StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(x.Key, str, StringComparison.OrdinalIgnoreCase));
            parsedValue = found?.Key ?? str;
            return true;
        }
    }
}
