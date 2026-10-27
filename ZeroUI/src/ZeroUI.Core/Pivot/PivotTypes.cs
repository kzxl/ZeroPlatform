namespace ZeroUI.Core.Pivot
{
    /// <summary>
    /// Specifies the location of a field in a multidimensional PivotGrid.
    /// </summary>
    public enum PivotArea
    {
        /// <summary>
        /// Field is placed in the row axis (leftmost hierarchical tree).
        /// </summary>
        RowArea = 0,

        /// <summary>
        /// Field is placed in the column axis (top hierarchical header).
        /// </summary>
        ColumnArea = 1,

        /// <summary>
        /// Field is placed in the data values area for mathematical aggregation.
        /// </summary>
        DataArea = 2,

        /// <summary>
        /// Field is hidden in the filter header pool for high-level slice-and-dice filtering.
        /// </summary>
        FilterArea = 3
    }

    /// <summary>
    /// Specifies the mathematical aggregation function applied to data values in the cell intersections.
    /// </summary>
    public enum PivotSummaryType
    {
        /// <summary>
        /// Calculates the sum of all numeric values.
        /// </summary>
        Sum = 0,

        /// <summary>
        /// Counts the total number of non-null records.
        /// </summary>
        Count = 1,

        /// <summary>
        /// Calculates the arithmetic mean (Sum / Count).
        /// </summary>
        Average = 2,

        /// <summary>
        /// Finds the minimum value.
        /// </summary>
        Min = 3,

        /// <summary>
        /// Finds the maximum value.
        /// </summary>
        Max = 4
    }

    /// <summary>
    /// Specifies sorting direction for header categories.
    /// </summary>
    public enum PivotSortOrder
    {
        Ascending = 0,
        Descending = 1
    }
}
