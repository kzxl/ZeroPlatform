namespace ZeroUI.Core.Data
{
    /// <summary>
    /// Optional high-performance contract for virtual data sources that support direct, typed row comparisons
    /// without string allocations, cell buffer overhead, or boxing during sorting.
    /// </summary>
    public interface IZeroSortableSource : IZeroVirtualSource
    {
        /// <summary>
        /// Compares two model rows directly by column index.
        /// Returns &lt; 0 if rowA &lt; rowB, 0 if rowA == rowB, &gt; 0 if rowA &gt; rowB.
        /// </summary>
        int CompareRows(int rowA, int rowB, int columnIndex);
    }
}
