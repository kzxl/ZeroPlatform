using System.ComponentModel;
using System.Drawing;
using ZeroUI.Core.Data;
using ZeroUI.WinForms.Icons;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// Hierarchical tapered pyramid chart for organizational tiers, demographic bands,
    /// and bottom-up manufacturing yield aggregations.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Charts")]
    [DefaultProperty("Stages")]
    [DefaultEvent("SelectedStageChanged")]
    [Description("Hierarchical pyramid chart visualizing tiered structures and cumulative yield metrics")]
    [ToolboxBitmap(typeof(ZeroIcons), "PyramidChart.bmp")]
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
    [System.Obsolete("PyramidChart is deprecated and will be removed in 5 release cycles. Please migrate to ZPyramidChart instead.")]
    [ToolboxItem(false)]
    public class PyramidChart : ZPyramidChart
    {
    }

    #endregion
}
