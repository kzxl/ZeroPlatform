using System;

namespace ZeroUI.Core.Charts
{
    /// <summary>
    /// Supported visualization styles for ZeroUI charts.
    /// </summary>
    public enum ChartType
    {
        Column,
        Bar,
        Line,
        Spline,
        Area,
        SplineArea,
        Candlestick,
        Pie,
        Donut,
        Scatter,
        Radar,
        Funnel,
        Pyramid,
        BoxPlot,
        Waterfall
    }

    /// <summary>
    /// Interactive Crosshair HUD operating modes for ZeroUI charts.
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
        TrafficLight,
        Subtle,
        LinesOnly
    }

    /// <summary>
    /// Placement position for chart legends.
    /// </summary>
    public enum ChartLegendPosition
    {
        None,
        Top,
        Bottom,
        Left,
        Right
    }

    /// <summary>
    /// Chart orientation mode.
    /// </summary>
    public enum ChartOrientation
    {
        Vertical,
        Horizontal
    }
}
