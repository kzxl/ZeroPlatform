using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using ZeroUI.WinForms.Charts.Model;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// Specialized convenience Pie and Donut chart control for categorical distributions.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Charts & Analytics")]
    [Description("Specialized Pie and Donut distribution chart")]
    public class ZPieChart : ZChart
    {
        private bool _isDonut = true;

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool IsDonut
        {
            get => _isDonut;
            set
            {
                _isDonut = value;
                ChartType = _isDonut ? ChartType.Donut : ChartType.Pie;
                Invalidate();
            }
        }

        public ZPieChart()
        {
            ChartType = ChartType.Donut;
        }

        public ZPieChart AddSlice(string label, double value, Color? color = null)
        {
            if (Series.Count == 0)
            {
                AddSeries("Default");
            }

            var assignedColor = color ?? ChartPalette.GetColor(Series[0].Points.Count);
            Series[0].AddPoint(label, value, assignedColor);
            Invalidate();
            return this;
        }

        public void SetSlices(IEnumerable<(string Label, double Value, Color? Color)> slices)
        {
            Clear();
            var series = AddSeries("Distribution");
            int idx = 0;
            foreach (var slice in slices)
            {
                var col = slice.Color ?? ChartPalette.GetColor(idx++);
                series.AddPoint(slice.Label, slice.Value, col);
            }
            Invalidate();
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZPieChart"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroPieChart is deprecated and will be removed in 5 release cycles. Please migrate to ZPieChart instead.")]
    [ToolboxItem(false)]
    public class ZeroPieChart : ZPieChart
    {
        public new ZeroUI.WinForms.Charts.Model.ZeroChartLegendPosition LegendPosition
        {
            get => (ZeroUI.WinForms.Charts.Model.ZeroChartLegendPosition)base.LegendPosition;
            set => base.LegendPosition = (ZeroUI.WinForms.Charts.Model.ChartLegendPosition)value;
        }

        public new ZeroUI.WinForms.Charts.Model.ZeroChartType ChartType
        {
            get => (ZeroUI.WinForms.Charts.Model.ZeroChartType)base.ChartType;
            set => base.ChartType = (ZeroUI.WinForms.Charts.Model.ChartType)value;
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZPieChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PieChart is deprecated and will be removed in 5 release cycles. Please migrate to ZPieChart instead.")]
    [ToolboxItem(false)]
    public class PieChart : ZPieChart
    {
    }

    #endregion
}
