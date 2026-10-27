using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroUI.Core.Analytics
{
    /// <summary>
    /// Represents an individual data point in 2D or 3D (Bubble) cartesian space.
    /// </summary>
    public class ScatterDataPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Size { get; set; } = 10.0;
        public string? Label { get; set; }
        public uint? ColorRgba { get; set; }
        public object? Tag { get; set; }

        public ScatterDataPoint() { }

        public ScatterDataPoint(double x, double y, double size = 10.0, string? label = null, uint? colorRgba = null)
        {
            X = x;
            Y = y;
            Size = size;
            Label = label;
            ColorRgba = colorRgba;
        }

        public override string ToString() => $"({X:F2}, {Y:F2}) [Size={Size:F1}]";
    }

    /// <summary>
    /// Represents a dataset series in a scatter or bubble chart.
    /// </summary>
    public class ScatterSeries
    {
        public string Name { get; set; } = "Series";
        public List<ScatterDataPoint> Points { get; set; } = new List<ScatterDataPoint>();
        public uint? ColorRgba { get; set; }
        public bool ShowRegressionLine { get; set; } = false;
        public bool IsBubble { get; set; } = false;

        public ScatterSeries() { }

        public ScatterSeries(string name, IEnumerable<ScatterDataPoint>? points = null, uint? colorRgba = null)
        {
            Name = name ?? "Series";
            if (points != null) Points.AddRange(points);
            ColorRgba = colorRgba;
        }
    }

    /// <summary>
    /// Encapsulates ordinary least squares (OLS) linear regression metrics: y = mx + b.
    /// </summary>
    public class RegressionLineResult
    {
        public double Slope { get; }
        public double Intercept { get; }
        public double RSquared { get; }
        public double MinX { get; }
        public double MaxX { get; }

        public double StartY => Slope * MinX + Intercept;
        public double EndY => Slope * MaxX + Intercept;

        public RegressionLineResult(double slope, double intercept, double rSquared, double minX, double maxX)
        {
            Slope = slope;
            Intercept = intercept;
            RSquared = rSquared;
            MinX = minX;
            MaxX = maxX;
        }

        public double Predict(double x) => Slope * x + Intercept;

        public override string ToString() => $"y = {Slope:F3}x + {Intercept:F3} (R² = {RSquared:F3})";
    }

    /// <summary>
    /// Coordinate boundaries and scales for plotting cartesian scatter / bubble points.
    /// </summary>
    public class ScatterPlotBounds
    {
        public double MinX { get; }
        public double MaxX { get; }
        public double MinY { get; }
        public double MaxY { get; }
        public double MinSize { get; }
        public double MaxSize { get; }

        public ScatterPlotBounds(double minX, double maxX, double minY, double maxY, double minSize, double maxSize)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
            MinSize = minSize;
            MaxSize = maxSize;
        }
    }

    /// <summary>
    /// Core calculation engine implementing coordinate scaling, boundary determination,
    /// and Ordinary Least Squares linear regression for scatter and bubble plots.
    /// </summary>
    public static class ScatterPlotEngine
    {
        /// <summary>
        /// Computes the OLS linear regression line (y = mx + b) and R-squared coefficient.
        /// </summary>
        public static RegressionLineResult? ComputeLinearRegression(IEnumerable<ScatterDataPoint> points)
        {
            if (points == null) return null;
            var list = points.Where(p => !double.IsNaN(p.X) && !double.IsNaN(p.Y) && !double.IsInfinity(p.X) && !double.IsInfinity(p.Y)).ToList();
            int n = list.Count;
            if (n < 2) return null;

            double sumX = 0.0, sumY = 0.0, sumX2 = 0.0, sumY2 = 0.0, sumXY = 0.0;
            double minX = double.MaxValue, maxX = double.MinValue;

            for (int i = 0; i < n; i++)
            {
                var pt = list[i];
                sumX += pt.X;
                sumY += pt.Y;
                sumX2 += pt.X * pt.X;
                sumY2 += pt.Y * pt.Y;
                sumXY += pt.X * pt.Y;

                if (pt.X < minX) minX = pt.X;
                if (pt.X > maxX) maxX = pt.X;
            }

            double denominator = (n * sumX2) - (sumX * sumX);
            if (Math.Abs(denominator) < 1e-12)
            {
                return new RegressionLineResult(0.0, sumY / n, 0.0, minX, maxX);
            }

            double slope = ((n * sumXY) - (sumX * sumY)) / denominator;
            double intercept = (sumY - (slope * sumX)) / n;

            // R-Squared computation
            double meanX = sumX / n;
            double meanY = sumY / n;
            double ssTot = 0.0, ssRes = 0.0;

            for (int i = 0; i < n; i++)
            {
                var pt = list[i];
                double yHat = slope * pt.X + intercept;
                double residual = pt.Y - yHat;
                ssRes += residual * residual;
                double diffY = pt.Y - meanY;
                ssTot += diffY * diffY;
            }

            double rSquared = ssTot > 1e-12 ? Math.Max(0.0, 1.0 - (ssRes / ssTot)) : 1.0;

            return new RegressionLineResult(slope, intercept, rSquared, minX, maxX);
        }

        /// <summary>
        /// Computes bounding domain and range across all series with optimal aesthetic padding.
        /// </summary>
        public static ScatterPlotBounds ComputeBounds(IEnumerable<ScatterSeries> seriesList, double paddingRatio = 0.05)
        {
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            double minSize = double.MaxValue, maxSize = double.MinValue;
            bool hasPoints = false;

            if (seriesList != null)
            {
                foreach (var s in seriesList)
                {
                    if (s?.Points == null) continue;
                    foreach (var p in s.Points)
                    {
                        if (double.IsNaN(p.X) || double.IsNaN(p.Y) || double.IsInfinity(p.X) || double.IsInfinity(p.Y)) continue;
                        hasPoints = true;
                        if (p.X < minX) minX = p.X;
                        if (p.X > maxX) maxX = p.X;
                        if (p.Y < minY) minY = p.Y;
                        if (p.Y > maxY) maxY = p.Y;

                        if (p.Size < minSize) minSize = p.Size;
                        if (p.Size > maxSize) maxSize = p.Size;
                    }
                }
            }

            if (!hasPoints)
            {
                return new ScatterPlotBounds(0, 100, 0, 100, 10, 10);
            }

            // Adjust single-point or zero-range cases
            if (Math.Abs(maxX - minX) < 1e-12)
            {
                minX -= 1.0;
                maxX += 1.0;
            }
            if (Math.Abs(maxY - minY) < 1e-12)
            {
                minY -= 1.0;
                maxY += 1.0;
            }
            if (Math.Abs(maxSize - minSize) < 1e-12)
            {
                minSize = Math.Max(1.0, minSize - 1.0);
                maxSize = minSize + 2.0;
            }

            double spanX = maxX - minX;
            double spanY = maxY - minY;

            return new ScatterPlotBounds(
                minX - spanX * paddingRatio,
                maxX + spanX * paddingRatio,
                minY - spanY * paddingRatio,
                maxY + spanY * paddingRatio,
                minSize,
                maxSize
            );
        }
    }
}
