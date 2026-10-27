using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using ZeroUI.WinForms.Charts.Model;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// Specialized convenience Bar and Column chart control for metric comparisons.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Charts & Analytics")]
    [Description("Specialized Column and Bar comparison chart")]
    public class ZBarChart : ZChart
    {
        private bool _isHorizontal = false;
        private bool _isStacked = false;

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool IsHorizontal
        {
            get => _isHorizontal;
            set
            {
                _isHorizontal = value;
                UpdateChartType();
            }
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        public bool IsStacked
        {
            get => _isStacked;
            set
            {
                _isStacked = value;
                UpdateChartType();
            }
        }

        public ZBarChart()
        {
            ChartType = ChartType.Column;
        }

        private void UpdateChartType()
        {
            if (_isHorizontal)
            {
                ChartType = _isStacked ? ChartType.StackedBar : ChartType.Bar;
            }
            else
            {
                ChartType = _isStacked ? ChartType.StackedColumn : ChartType.Column;
            }
        }

        public ChartSeries SetData(string seriesName, IEnumerable<string> categories, IEnumerable<double> values, Color? color = null)
        {
            var series = AddSeries(seriesName, color);
            series.AddPoints(values, categories);
            Invalidate();
            return series;
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZBarChart"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroBarChart is deprecated and will be removed in 5 release cycles. Please migrate to ZBarChart instead.")]
    [ToolboxItem(false)]
    public class ZeroBarChart : ZBarChart
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
    /// Legacy alias for <see cref="ZBarChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("BarChart is deprecated and will be removed in 5 release cycles. Please migrate to ZBarChart instead.")]
    [ToolboxItem(false)]
    public class BarChart : ZBarChart
    {
    }

    #endregion
}
