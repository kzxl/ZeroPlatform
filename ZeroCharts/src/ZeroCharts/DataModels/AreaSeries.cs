using System;
using System.Collections.Generic;
using System.Drawing;
using ZeroCharts.Axis;

namespace ZeroCharts.DataModels
{
    /// <summary>
    /// Series for rendering filled area charts, visualizing cumulative volume, sensor envelopes, and trends.
    /// </summary>
    public class AreaSeries
    {
        public string Name { get; set; }
        public Color StrokeColor { get; set; }
        public Color FillColor { get; set; }
        public byte FillAlpha { get; set; } = 85;
        public float StrokeThickness { get; set; } = 2.0f;
        public double Baseline { get; set; } = 0.0;
        public bool UseGradient { get; set; } = true;
        public bool IsVisible { get; set; } = true;

        private readonly List<ChartPoint> _points = new List<ChartPoint>();
        public IReadOnlyList<ChartPoint> Points => _points;
        public int Count => _points.Count;

        public double MinX { get; private set; } = double.MaxValue;
        public double MaxX { get; private set; } = double.MinValue;
        public double MinY { get; private set; } = double.MaxValue;
        public double MaxY { get; private set; } = double.MinValue;

        public AreaSeries(string name = "Area Series", Color? strokeColor = null, Color? fillColor = null)
        {
            Name = name;
            StrokeColor = strokeColor ?? ChartPalette.Cyan;
            FillColor = fillColor ?? StrokeColor;
        }

        public void Add(double x, double y)
        {
            _points.Add(new ChartPoint(x, y));

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
        /// Downsamples large datasets via Largest-Triangle-Three-Buckets (LTTB) algorithm.
        /// </summary>
        public List<ChartPoint> DownsampleLttb(int threshold)
        {
            if (threshold >= _points.Count || threshold <= 2)
            {
                return new List<ChartPoint>(_points);
            }

            var sampled = new List<ChartPoint>(threshold);
            int dataLength = _points.Count;
            double bucketSize = (double)(dataLength - 2) / (threshold - 2);

            int a = 0;
            sampled.Add(_points[a]);

            for (int i = 0; i < threshold - 2; i++)
            {
                double avgRangeStart = Math.Floor((i + 1) * bucketSize) + 1;
                double avgRangeEnd = Math.Min(Math.Floor((i + 2) * bucketSize) + 1, dataLength);

                double avgX = 0;
                double avgY = 0;
                double avgCount = avgRangeEnd - avgRangeStart;

                for (int j = (int)avgRangeStart; j < (int)avgRangeEnd; j++)
                {
                    avgX += _points[j].X;
                    avgY += _points[j].Y;
                }
                avgX /= avgCount;
                avgY /= avgCount;

                int rangeOffs = (int)Math.Floor(i * bucketSize) + 1;
                int rangeTo = (int)Math.Min(Math.Floor((i + 1) * bucketSize) + 1, dataLength);

                double pointAx = _points[a].X;
                double pointAy = _points[a].Y;

                double maxArea = -1;
                int nextA = rangeOffs;

                for (int j = rangeOffs; j < rangeTo; j++)
                {
                    double area = Math.Abs(
                        (pointAx - avgX) * (_points[j].Y - pointAy) -
                        (pointAx - _points[j].X) * (avgY - pointAy)
                    ) * 0.5;

                    if (area > maxArea)
                    {
                        maxArea = area;
                        nextA = j;
                    }
                }

                sampled.Add(_points[nextA]);
                a = nextA;
            }

            sampled.Add(_points[dataLength - 1]);
            return sampled;
        }
    }
}
