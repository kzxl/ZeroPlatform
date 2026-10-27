using System;
using System.Drawing;

namespace ZeroUI.WinForms.Charts.Model
{
    /// <summary>
    /// Represents a single discrete data point in a chart series or circular slice.
    /// </summary>
    public class ChartPoint
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
        public string? FormattedValue { get; set; }
        public Color? ColorOverride { get; set; }
        public object? Tag { get; set; }

        public ChartPoint() { }

        public ChartPoint(string label, double value, Color? colorOverride = null)
        {
            Label = label ?? string.Empty;
            Value = value;
            ColorOverride = colorOverride;
        }

        public ChartPoint(double value, string? formattedValue = null)
        {
            Value = value;
            FormattedValue = formattedValue;
        }

        public override string ToString() => $"{Label}: {Value}";
    }

    /// <summary>
    /// Legacy alias for <see cref="ChartPoint"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroChartPoint is deprecated. Please use ChartPoint instead.")]
    public class ZeroChartPoint : ChartPoint
    {
        public ZeroChartPoint() : base() { }
        public ZeroChartPoint(string label, double value, Color? colorOverride = null) : base(label, value, colorOverride) { }
        public ZeroChartPoint(double value, string? formattedValue = null) : base(value, formattedValue) { }
    }
}
