using System;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Trend indicator direction for KPI metric cards and analytical summary dashboards.
    /// </summary>
    public enum TrendDirection
    {
        None,
        Up,
        Down
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="TrendDirection"/>.
    /// </summary>
    [Obsolete("ZeroTrendDirection is deprecated. Use TrendDirection instead.")]
    public enum ZeroTrendDirection
    {
        None,
        Up,
        Down
    }
}
