using System;
using System.Collections.Generic;
using System.Drawing;

namespace ZeroCharts.DataModels
{
    /// <summary>
    /// Represents a 2D coordinate point on a chart surface.
    /// </summary>
    public readonly struct ChartPoint : IEquatable<ChartPoint>
    {
        public double X { get; }
        public double Y { get; }

        public ChartPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(ChartPoint other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object? obj) => obj is ChartPoint other && Equals(other);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
        public override string ToString() => $"({X:F2}, {Y:F2})";
    }

    /// <summary>
    /// High-performance line series supporting streaming telemetry and LTTB downsampling.
    /// </summary>
    public class LineSeries
    {
        private readonly List<ChartPoint> _points = new List<ChartPoint>();

        public string Title { get; set; }
        public Color StrokeColor { get; set; } = Color.FromArgb(0, 168, 255); // Cyber Sapphire
        public float StrokeThickness { get; set; } = 1.5f;
        public bool IsVisible { get; set; } = true;

        public IReadOnlyList<ChartPoint> Points => _points;
        public int Count => _points.Count;

        public double MinX { get; private set; } = double.MaxValue;
        public double MaxX { get; private set; } = double.MinValue;
        public double MinY { get; private set; } = double.MaxValue;
        public double MaxY { get; private set; } = double.MinValue;

        public LineSeries(string title = "Telemetry")
        {
            Title = title;
        }

        public void Add(double x, double y)
        {
            _points.Add(new ChartPoint(x, y));
            if (x < MinX) MinX = x;
            if (x > MaxX) MaxX = x;
            if (y < MinY) MinY = y;
            if (y > MaxY) MaxY = y;
        }

        public void AddRange(IEnumerable<ChartPoint> points)
        {
            if (points == null) return;
            foreach (var pt in points)
            {
                Add(pt.X, pt.Y);
            }
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
        /// Largest-Triangle-Three-Buckets (LTTB) downsampling down to target point threshold.
        /// </summary>
        public List<ChartPoint> DownsampleLttb(int threshold)
        {
            if (threshold >= _points.Count || threshold <= 2)
            {
                return new List<ChartPoint>(_points);
            }

            var sampled = new List<ChartPoint>(threshold);
            int dataLength = _points.Count;

            // Bucket size. Leave room for start and end points
            double every = (double)(dataLength - 2) / (threshold - 2);

            int a = 0; // Initially point a is the first point
            sampled.Add(_points[a]);

            for (int i = 0; i < threshold - 2; i++)
            {
                // Calculate point average for next bucket (c)
                double avgX = 0;
                double avgY = 0;
                int avgRangeStart = (int)Math.Floor((i + 1) * every) + 1;
                int avgRangeEnd = (int)Math.Floor((i + 2) * every) + 1;
                if (avgRangeEnd >= dataLength) avgRangeEnd = dataLength;

                int avgRangeLength = avgRangeEnd - avgRangeStart;
                if (avgRangeLength <= 0) avgRangeLength = 1;

                for (int j = avgRangeStart; j < avgRangeEnd; j++)
                {
                    avgX += _points[j].X;
                    avgY += _points[j].Y;
                }
                avgX /= avgRangeLength;
                avgY /= avgRangeLength;

                // Get the range for this bucket
                int rangeOffs = (int)Math.Floor(i * every) + 1;
                int rangeTo = (int)Math.Floor((i + 1) * every) + 1;

                // Point a
                double pointAx = _points[a].X;
                double pointAy = _points[a].Y;

                double maxArea = -1;
                int maxAreaIndex = rangeOffs;

                for (int j = rangeOffs; j < rangeTo; j++)
                {
                    // Calculate triangle area over points (a, point[j], avg)
                    double area = Math.Abs((pointAx - avgX) * (_points[j].Y - pointAy) -
                                           (pointAx - _points[j].X) * (avgY - pointAy)) * 0.5;
                    if (area > maxArea)
                    {
                        maxArea = area;
                        maxAreaIndex = j;
                    }
                }

                sampled.Add(_points[maxAreaIndex]);
                a = maxAreaIndex; // Next a is this bucket's chosen point
            }

            sampled.Add(_points[dataLength - 1]); // Always add last point
            return sampled;
        }
    }
}
