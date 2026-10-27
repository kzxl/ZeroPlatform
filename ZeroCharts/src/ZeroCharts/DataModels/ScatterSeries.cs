using System;
using System.Collections.Generic;
using System.Drawing;
using ZeroCharts.Axis;

namespace ZeroCharts.DataModels
{
    /// <summary>
    /// Geometric glyph shapes for scatter markers.
    /// </summary>
    public enum MarkerShape
    {
        Circle,
        Square,
        Diamond,
        Triangle,
        Cross
    }

    /// <summary>
    /// Individual data point within a ScatterSeries.
    /// </summary>
    public class ScatterPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public float Radius { get; set; }
        public Color? CustomColor { get; set; }
        public string? Label { get; set; }

        public ScatterPoint(double x, double y, float radius = 4.5f, Color? customColor = null, string? label = null)
        {
            X = x;
            Y = y;
            Radius = radius;
            CustomColor = customColor;
            Label = label;
        }
    }

    /// <summary>
    /// Series for plotting multi-variable scatter, bubble, and correlation charts for QC and metrology.
    /// </summary>
    public class ScatterSeries
    {
        public string Name { get; set; }
        public MarkerShape Shape { get; set; } = MarkerShape.Circle;
        public Color DefaultColor { get; set; }
        public float DefaultRadius { get; set; } = 5.0f;
        public bool IsVisible { get; set; } = true;

        private readonly List<ScatterPoint> _points = new List<ScatterPoint>();
        public IReadOnlyList<ScatterPoint> Points => _points;
        public int Count => _points.Count;

        public double MinX { get; private set; } = double.MaxValue;
        public double MaxX { get; private set; } = double.MinValue;
        public double MinY { get; private set; } = double.MaxValue;
        public double MaxY { get; private set; } = double.MinValue;

        public ScatterSeries(string name = "Scatter Series", Color? defaultColor = null, MarkerShape shape = MarkerShape.Circle)
        {
            Name = name;
            DefaultColor = defaultColor ?? ChartPalette.Amber;
            Shape = shape;
        }

        public void Add(double x, double y, float radius = 5.0f, Color? customColor = null, string? label = null)
        {
            var pt = new ScatterPoint(x, y, radius, customColor, label);
            _points.Add(pt);

            if (x < MinX) MinX = x;
            if (x > MaxX) MaxX = x;
            if (y < MinY) MinY = y;
            if (y > MaxY) MaxY = y;
        }

        public void Clear()
        {
            _points.Clear();
            MinX = double.MaxValue;
            MaxX = double.MinValue;
            MinY = double.MaxValue;
            MaxY = double.MinValue;
        }

        /// <summary>
        /// Finds the nearest scatter point to the specified data coordinates.
        /// </summary>
        public ScatterPoint? FindNearest(double targetX, double targetY, double maxDistanceSq)
        {
            ScatterPoint? nearest = null;
            double bestDistSq = maxDistanceSq;

            for (int i = 0; i < _points.Count; i++)
            {
                var pt = _points[i];
                double dx = pt.X - targetX;
                double dy = pt.Y - targetY;
                double distSq = dx * dx + dy * dy;

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    nearest = pt;
                }
            }

            return nearest;
        }
    }
}
