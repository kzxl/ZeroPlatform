using System;

namespace ZeroUI.WinForms.Charts.Model
{
    /// <summary>
    /// Supported visualization types for ZeroChart and specialized chart controls.
    /// </summary>
    public enum ChartType
    {
        /// <summary>Vertical grouped column chart.</summary>
        Column,

        /// <summary>Vertical stacked column chart.</summary>
        StackedColumn,

        /// <summary>Horizontal grouped bar chart.</summary>
        Bar,

        /// <summary>Horizontal stacked bar chart.</summary>
        StackedBar,

        /// <summary>Standard linear point-to-point line chart.</summary>
        Line,

        /// <summary>Smooth Catmull-Rom spline curve line chart.</summary>
        Spline,

        /// <summary>Linear area chart with translucent gradient fill under the line.</summary>
        Area,

        /// <summary>Smooth curved spline area chart with gradient fill.</summary>
        SplineArea,

        /// <summary>Circular pie chart with proportional sectors.</summary>
        Pie,

        /// <summary>Donut chart with hollow center for KPI summaries and statistics.</summary>
        Donut,

        /// <summary>Multi-axis radar or spider chart for multi-dimensional performance benchmarking.</summary>
        Radar,

        /// <summary>Financial OHLC candlestick chart with volume histogram and trend overlays.</summary>
        Candlestick,

        /// <summary>Process pipeline and conversion funnel chart with stage drop-off metrics.</summary>
        Funnel,

        /// <summary>Cumulative variance bridge waterfall chart with floating positive and negative steps.</summary>
        Waterfall
    }

    /// <summary>
    /// Position of the chart legend.
    /// </summary>
    public enum ChartLegendPosition
    {
        None,
        Top,
        Bottom,
        Right
    }

    /// <summary>
    /// Interactive Crosshair HUD operating modes for ZeroChart.
    /// </summary>
    public enum CrosshairMode
    {
        None,
        VerticalOnly,
        HorizontalOnly,
        Both
    }

    /// <summary>
    /// Visual styling for Statistical Process Control (SPC) limit belts.
    /// </summary>
    public enum SpcBeltStyle
    {
        /// <summary>Translucent traffic-light zones (Green = Normal, Amber = Warning, Red = Alarm).</summary>
        TrafficLight,
        /// <summary>Monochrome subtle banded shading.</summary>
        Subtle,
        /// <summary>Boundary lines only without solid area fill.</summary>
        LinesOnly
    }

    /// <summary>
    /// Legacy alias for <see cref="ChartType"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroChartType is deprecated. Please use ChartType instead.")]
    public enum ZeroChartType
    {
        Column = ChartType.Column,
        StackedColumn = ChartType.StackedColumn,
        Bar = ChartType.Bar,
        StackedBar = ChartType.StackedBar,
        Line = ChartType.Line,
        Spline = ChartType.Spline,
        Area = ChartType.Area,
        SplineArea = ChartType.SplineArea,
        Pie = ChartType.Pie,
        Donut = ChartType.Donut,
        Radar = ChartType.Radar,
        Candlestick = ChartType.Candlestick,
        Funnel = ChartType.Funnel,
        Waterfall = ChartType.Waterfall
    }

    /// <summary>
    /// Legacy alias for <see cref="ChartLegendPosition"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroChartLegendPosition is deprecated. Please use ChartLegendPosition instead.")]
    public enum ZeroChartLegendPosition
    {
        None = ChartLegendPosition.None,
        Top = ChartLegendPosition.Top,
        Bottom = ChartLegendPosition.Bottom,
        Right = ChartLegendPosition.Right
    }
}
