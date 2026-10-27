namespace ZeroUI.Core.Data
{
    /// <summary>
    /// Contract for supplying and committing editable cell values in ZeroUI DataGrid.
    /// </summary>
    public interface IZeroEditableSource : IZeroVirtualSource
    {
        /// <summary>
        /// Determines whether the specified cell is allowed to be edited.
        /// </summary>
        bool IsCellEditable(int rowIndex, int columnIndex);

        /// <summary>
        /// Commits the new textual value from the in-place editor into the underlying data model.
        /// Returns true if commit was successful, false if validation/parsing failed.
        /// </summary>
        bool SetCellValue(int rowIndex, int columnIndex, string textValue);
    }
}
