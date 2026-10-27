using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Range
{
    /// <summary>
    /// Contract implemented by data-providing or data-consuming controls (Grid, Chart, Historian)
    /// to seamlessly bind with a RangeControl.
    /// </summary>
    public interface IRangeControlClient
    {
        /// <summary>
        /// Retrieves the data domain type (Numeric or DateTime).
        /// </summary>
        RangeDataType DataType { get; }

        /// <summary>
        /// Retrieves the absolute total range boundaries from the client's dataset.
        /// </summary>
        (double Start, double End) GetTotalRangeBounds();

        /// <summary>
        /// Retrieves background distribution points (histogram bars, area points, sparkline).
        /// </summary>
        IEnumerable<RangeDataPoint> GetDataPoints();

        /// <summary>
        /// Invoked when the user adjusts the selected range on the RangeControl.
        /// Allows the client to filter its view, update rows, or scrub charts.
        /// </summary>
        void OnRangeSelectionChanged(double start, double end);
    }
}
