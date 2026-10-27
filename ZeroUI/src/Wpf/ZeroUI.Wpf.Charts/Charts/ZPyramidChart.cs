using System;
using ZeroUI.Core.Data;

namespace ZeroUI.Wpf.Charts
{
    /// <summary>
    /// Hierarchical tapered pyramid chart for organizational tiers, demographic bands,
    /// and bottom-up manufacturing yield aggregations in WPF.
    /// </summary>
    public class ZPyramidChart : ZFunnelChart
    {
        public ZPyramidChart()
        {
            Mode = FunnelChartMode.Pyramid;
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZPyramidChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PyramidChart is deprecated and will be removed in 5 release cycles. Please migrate to ZPyramidChart instead.")]
    public class PyramidChart : ZPyramidChart { }

    /// <summary>
    /// Legacy alias for <see cref="ZPyramidChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroPyramidChart is deprecated and will be removed in 5 release cycles. Please migrate to ZPyramidChart instead.")]
    public class ZeroPyramidChart : ZPyramidChart { }

    #endregion

}
