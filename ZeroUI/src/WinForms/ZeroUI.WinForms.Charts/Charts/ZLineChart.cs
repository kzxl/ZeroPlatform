using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using ZeroUI.WinForms.Charts.Model;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// Specialized convenience Line, Spline, and Area trend chart control.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Charts & Analytics")]
    [Description("Specialized Line and Area trend chart")]
    public class ZLineChart : ZChart
    {
        private bool _isCurved = true;
        private bool _isArea = true;

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool IsCurved
        {
            get => _isCurved;
            set
            {
                _isCurved = value;
                UpdateChartType();
            }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool IsArea
        {
            get => _isArea;
            set
            {
                _isArea = value;
                UpdateChartType();
            }
        }

        public ZLineChart()
        {
            ChartType = ChartType.SplineArea;
        }

        private void UpdateChartType()
        {
            if (_isArea)
            {
                ChartType = _isCurved ? ChartType.SplineArea : ChartType.Area;
            }
            else
            {
                ChartType = _isCurved ? ChartType.Spline : ChartType.Line;
            }
        }

        public ChartSeries AddTrendSeries(string name, IEnumerable<double> values, IEnumerable<string>? categories = null, Color? color = null)
        {
            var series = AddSeries(name, color);
            series.AddPoints(values, categories);
            Invalidate();
            return series;
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZLineChart"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroLineChart is deprecated and will be removed in 5 release cycles. Please migrate to ZLineChart instead.")]
    [ToolboxItem(false)]
    public class ZeroLineChart : ZLineChart
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
    /// Legacy alias for <see cref="ZLineChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("LineChart is deprecated and will be removed in 5 release cycles. Please migrate to ZLineChart instead.")]
    [ToolboxItem(false)]
    public class LineChart : ZLineChart
    {
    }

    #endregion
}
