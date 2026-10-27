using System;
using System.Collections.Generic;
using System.Drawing;

namespace ZeroUI.WinForms.Charts.Model
{
    /// <summary>
    /// Represents a data series containing multiple data points, styling, and visibility state.
    /// </summary>
    public class ChartSeries
    {
        public string Name { get; set; } = "Series";
        public List<ChartPoint> Points { get; } = new List<ChartPoint>();
        public Color Color { get; set; } = Color.FromArgb(79, 70, 229); // Default Indigo 600
        public float StrokeWidth { get; set; } = 2.5f;
        public float FillOpacity { get; set; } = 0.25f;
        public bool IsVisible { get; set; } = true;
        public ChartType? ChartTypeOverride { get; set; }

        public ChartSeries() { }

        public ChartSeries(string name, Color color)
        {
            Name = name ?? "Series";
            Color = color;
        }

        public ChartSeries(string name, Color color, IEnumerable<double> values, IEnumerable<string>? labels = null)
        {
            Name = name ?? "Series";
            Color = color;
            AddPoints(values, labels);
        }

        public ChartSeries AddPoint(string label, double value, Color? colorOverride = null)
        {
            Points.Add(new ChartPoint(label, value, colorOverride));
            return this;
        }

        public ChartSeries AddPoints(IEnumerable<double> values, IEnumerable<string>? labels = null)
        {
            if (values == null) return this;
            using var valEnum = values.GetEnumerator();
            using var lblEnum = labels?.GetEnumerator();

            int index = 1;
            while (valEnum.MoveNext())
            {
                string label = (lblEnum != null && lblEnum.MoveNext()) ? lblEnum.Current : $"Item {index}";
                Points.Add(new ChartPoint(label, valEnum.Current));
                index++;
            }
            return this;
        }

        public void Clear() => Points.Clear();
    }

    /// <summary>
    /// Legacy alias for <see cref="ChartSeries"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroChartSeries is deprecated. Please use ChartSeries instead.")]
    public class ZeroChartSeries : ChartSeries
    {
        public ZeroChartSeries() : base() { }
        public ZeroChartSeries(string name, Color color) : base(name, color) { }
        public ZeroChartSeries(string name, Color color, IEnumerable<double> values, IEnumerable<string>? labels = null)
            : base(name, color, values, labels) { }
    }
}
