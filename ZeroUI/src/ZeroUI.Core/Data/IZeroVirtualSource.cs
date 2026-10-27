namespace ZeroUI.Core.Data
{
    /// <summary>
    /// Contract for supplying virtual data to ZeroUI controls without boxing or allocations.
    /// </summary>
    public interface IZeroVirtualSource
    {
        int TotalRowCount { get; }
        int TotalColumnCount { get; }
        void GetCellValue(int rowIndex, int columnIndex, ref CellValueBuffer buffer);
    }
}
