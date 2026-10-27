using System;

namespace ZeroUI.Core.Range
{
    /// <summary>
    /// Specifies the data domain type handled by the range control.
    /// </summary>
    public enum RangeDataType
    {
        /// <summary>
        /// Continuous or discrete real numbers (double).
        /// </summary>
        Numeric = 0,

        /// <summary>
        /// Temporal timestamps (DateTime).
        /// </summary>
        DateTime = 1
    }

    /// <summary>
    /// Visual style of the background distribution graph.
    /// </summary>
    public enum RangeGraphType
    {
        /// <summary>
        /// Smooth area polygon fill beneath line.
        /// </summary>
        Area = 0,

        /// <summary>
        /// Vertical bar histogram buckets.
        /// </summary>
        Histogram = 1,

        /// <summary>
        /// Minimalist line sparkline.
        /// </summary>
        Line = 2
    }

    /// <summary>
    /// Granularity scale for ruler divisions and snap points.
    /// </summary>
    public enum RangeInterval
    {
        None = 0,
        Auto = 1,
        Year = 2,
        Quarter = 3,
        Month = 4,
        Day = 5,
        Hour = 6,
        Minute = 7,
        Second = 8,
        NumericStep = 9
    }

    /// <summary>
    /// Hit-test zones identified during interactive mouse pointer tracking.
    /// </summary>
    public enum RangeHitTestResult
    {
        None = 0,
        LeftThumb = 1,
        RightThumb = 2,
        SelectionRange = 3,
        Background = 4
    }
}
