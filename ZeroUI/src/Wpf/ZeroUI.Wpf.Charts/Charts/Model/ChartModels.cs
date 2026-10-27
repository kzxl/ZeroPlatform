using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace ZeroUI.Wpf.Charts.Model
{
    public enum ChartType
    {
        Column,
        Bar,
        Line,
        Spline,
        Area,
        SplineArea,
        AreaSpline,
        Candlestick,
        Pie,
        Donut
    }

    [Obsolete("ZeroChartType is deprecated. Use ChartType instead.")]
    public enum ZeroChartType
    {
        Column = ChartType.Column,
        Bar = ChartType.Bar,
        Line = ChartType.Line,
        Spline = ChartType.Spline,
        Area = ChartType.Area,
        SplineArea = ChartType.SplineArea,
        AreaSpline = ChartType.AreaSpline,
        Candlestick = ChartType.Candlestick,
        Pie = ChartType.Pie,
        Donut = ChartType.Donut
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
        TrafficLight,
        Subtle,
        LinesOnly
    }

    public class ChartPoint
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
        public string? FormattedValue { get; set; }
        public Color? ColorOverride { get; set; }

        public ChartPoint() { }

        public ChartPoint(string label, double value, Color? colorOverride = null)
        {
            Label = label ?? string.Empty;
            Value = value;
            ColorOverride = colorOverride;
        }

        public override string ToString() => $"{Label}: {Value}";
    }

    [Obsolete("ZeroChartPoint is deprecated. Use ChartPoint instead.")]
    public class ZeroChartPoint : ChartPoint
    {
        public ZeroChartPoint() { }
        public ZeroChartPoint(string label, double value, Color? colorOverride = null) : base(label, value, colorOverride) { }
    }

    public class CandlePoint
    {
        public DateTime Time { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }

        public bool IsBullish => Close >= Open;

        public CandlePoint(DateTime time, double open, double high, double low, double close, double volume = 0)
        {
            Time = time;
            Open = open;
            High = high;
            Low = low;
            Close = close;
            Volume = volume;
        }
    }

    [Obsolete("ZeroCandlePoint is deprecated. Use CandlePoint instead.")]
    public class ZeroCandlePoint : CandlePoint
    {
        public ZeroCandlePoint(DateTime time, double open, double high, double low, double close, double volume = 0)
            : base(time, open, high, low, close, volume) { }
    }

    public class ChartSeries
    {
        public string Name { get; set; } = "Series";
        public List<ChartPoint> Points { get; } = new List<ChartPoint>();
        public Color Color { get; set; } = Color.FromRgb(129, 140, 248); // Indigo / PrimaryAccent
        public double StrokeThickness { get; set; } = 2.0;
        public double FillOpacity { get; set; } = 0.25;
        public bool IsVisible { get; set; } = true;
        public ChartType? TypeOverride { get; set; }

        public ChartSeries() { }

        public ChartSeries(string name, Color color)
        {
            Name = name;
            Color = color;
        }

        public ChartSeries AddPoint(string label, double value, Color? colorOverride = null)
        {
            Points.Add(new ChartPoint(label, value, colorOverride));
            return this;
        }
    }

    [Obsolete("ZeroChartSeries is deprecated. Use ChartSeries instead.")]
    public class ZeroChartSeries : ChartSeries
    {
        public ZeroChartSeries() { }
        public ZeroChartSeries(string name, Color color) : base(name, color) { }
    }
}
