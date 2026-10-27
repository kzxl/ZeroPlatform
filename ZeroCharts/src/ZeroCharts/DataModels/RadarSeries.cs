using System;
using System.Collections.Generic;
using System.Drawing;
using ZeroCharts.Axis;

namespace ZeroCharts.DataModels
{
    /// <summary>
    /// Value along a single radial axis in a Radar/Spider chart.
    /// </summary>
    public class RadarItem
    {
        public string AxisName { get; set; }
        public double Value { get; set; }
        public double MaxValue { get; set; }

        public double Ratio => MaxValue > 0 ? Math.Max(0.0, Math.Min(1.0, Value / MaxValue)) : 0.0;

        public RadarItem(string axisName, double value, double maxValue = 100.0)
        {
            AxisName = axisName ?? string.Empty;
            Value = value;
            MaxValue = maxValue > 0 ? maxValue : 100.0;
        }
    }

    /// <summary>
    /// Series for plotting multi-dimensional KPI performance metrics on a polar radar grid.
    /// </summary>
    public class RadarSeries
    {
        public string Name { get; set; }
        public Color StrokeColor { get; set; }
        public Color FillColor { get; set; }
        public byte FillAlpha { get; set; } = 70;
        public float StrokeThickness { get; set; } = 2.0f;
        public bool IsVisible { get; set; } = true;

        private readonly List<RadarItem> _items = new List<RadarItem>();
        public IReadOnlyList<RadarItem> Items => _items;
        public int Count => _items.Count;

        public RadarSeries(string name = "Radar KPI", Color? strokeColor = null, Color? fillColor = null)
        {
            Name = name;
            StrokeColor = strokeColor ?? ChartPalette.Cyan;
            FillColor = fillColor ?? StrokeColor;
        }

        public void Add(string axisName, double value, double maxValue = 100.0)
        {
            _items.Add(new RadarItem(axisName, value, maxValue));
        }

        public void Clear()
        {
            _items.Clear();
        }

        /// <summary>
        /// Computes screen Cartesian coordinate (X, Y) for the specified radial item.
        /// </summary>
        public PointF GetCartesianPoint(int index, float centerX, float centerY, float radius)
        {
            if (index < 0 || index >= _items.Count || _items.Count == 0)
                return new PointF(centerX, centerY);

            double angleStep = (2.0 * Math.PI) / _items.Count;
            // Start at top (-PI/2) and rotate clockwise
            double angle = (-Math.PI * 0.5) + (index * angleStep);

            float r = (float)(radius * _items[index].Ratio);
            float px = centerX + (float)(Math.Cos(angle) * r);
            float py = centerY + (float)(Math.Sin(angle) * r);

            return new PointF(px, py);
        }
    }
}
