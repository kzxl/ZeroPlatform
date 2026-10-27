using System;
using System.Drawing;
using System.Linq;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Vision.Calibration;

namespace ZeroGraphics.Vision.Metrology
{
    /// <summary>
    /// Result of a physical distance measurement in metric units.
    /// </summary>
    public sealed class MetricDistanceResult
    {
        public double PixelDistance { get; }
        public double MetricDistance { get; }
        public MetricUnit Unit { get; }
        public string Text { get; }

        public MetricDistanceResult(double pixelDistance, double metricDistance, MetricUnit unit, string text)
        {
            PixelDistance = pixelDistance;
            MetricDistance = metricDistance;
            Unit = unit;
            Text = text;
        }

        public override string ToString() => Text;
    }

    /// <summary>
    /// Result of a physical circle/bore hole measurement.
    /// </summary>
    public sealed class MetricCircleResult
    {
        public PointF PixelCenter { get; }
        public double PixelRadius { get; }
        public double MetricRadius { get; }
        public double MetricDiameter { get; }
        public MetricUnit Unit { get; }
        public string Text { get; }

        public MetricCircleResult(PointF pixelCenter, double pixelRadius, double metricRadius, double metricDiameter, MetricUnit unit, string text)
        {
            PixelCenter = pixelCenter;
            PixelRadius = pixelRadius;
            MetricRadius = metricRadius;
            MetricDiameter = metricDiameter;
            Unit = unit;
            Text = text;
        }

        public override string ToString() => Text;
    }

    /// <summary>
    /// High-level metrology calculation engine combining sub-pixel edge detection with 2D metric calibration.
    /// </summary>
    public static class MetricMeasurement
    {
        /// <summary>
        /// Measures the Euclidean distance between two points in calibrated metric units.
        /// </summary>
        public static MetricDistanceResult MeasureDistance(PointF p1, PointF p2, MetricCalibration2D calibration)
        {
            if (calibration == null) throw new ArgumentNullException(nameof(calibration));

            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double pxDist = Math.Sqrt(dx * dx + dy * dy);
            double metricDist = calibration.MeasureDistance(p1, p2);

            return new MetricDistanceResult(
                pxDist,
                metricDist,
                calibration.Unit,
                calibration.Format(metricDist));
        }

        /// <summary>
        /// Measures the perpendicular distance from a point to a line segment defined by (lineA, lineB).
        /// </summary>
        public static MetricDistanceResult MeasurePointToLine(
            PointF point,
            PointF lineA,
            PointF lineB,
            MetricCalibration2D calibration)
        {
            if (calibration == null) throw new ArgumentNullException(nameof(calibration));

            double dx = lineB.X - lineA.X;
            double dy = lineB.Y - lineA.Y;
            double lineLen = Math.Sqrt(dx * dx + dy * dy);

            if (lineLen < 1e-6)
                return MeasureDistance(point, lineA, calibration);

            // Perpendicular distance in pixels
            double num = Math.Abs(dy * point.X - dx * point.Y + lineB.X * lineA.Y - lineB.Y * lineA.X);
            double pxDist = num / lineLen;

            // Project point onto line to find closest point for true metric transformation
            double u = ((point.X - lineA.X) * dx + (point.Y - lineA.Y) * dy) / (lineLen * lineLen);
            PointF proj = new PointF((float)(lineA.X + u * dx), (float)(lineA.Y + u * dy));

            double metricDist = calibration.MeasureDistance(point, proj);

            return new MetricDistanceResult(
                pxDist,
                metricDist,
                calibration.Unit,
                calibration.Format(metricDist));
        }

        /// <summary>
        /// Measures the physical radius and diameter of a circle/bore.
        /// </summary>
        public static MetricCircleResult MeasureCircle(PointF center, double pixelRadius, MetricCalibration2D calibration)
        {
            if (calibration == null) throw new ArgumentNullException(nameof(calibration));

            PointF rimPoint = new PointF((float)(center.X + pixelRadius), center.Y);
            double metricRadius = calibration.MeasureDistance(center, rimPoint);
            double metricDiameter = metricRadius * 2.0;

            string text = $"Ø {calibration.Format(metricDiameter)} (R: {calibration.Format(metricRadius)})";

            return new MetricCircleResult(
                center,
                pixelRadius,
                metricRadius,
                metricDiameter,
                calibration.Unit,
                text);
        }

        /// <summary>
        /// Scans an image along a caliper rake line, detects the first and last sub-pixel edges,
        /// and computes the physical thickness/width of the object in metric units.
        /// </summary>
        public static MetricDistanceResult? MeasureWidthWithCaliper(
            ImageBuffer image,
            PointF start,
            PointF end,
            MetricCalibration2D calibration,
            double minMagnitude = 15.0)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (calibration == null) throw new ArgumentNullException(nameof(calibration));

            var edges = EdgeCaliper1D.FindEdges(
                image,
                start.X, start.Y,
                end.X, end.Y,
                minMagnitude: minMagnitude);

            if (edges.Count < 2) return null;

            var first = edges.First();
            var last = edges.Last();

            PointF p1 = new PointF((float)first.X, (float)first.Y);
            PointF p2 = new PointF((float)last.X, (float)last.Y);

            return MeasureDistance(p1, p2, calibration);
        }
    }
}
