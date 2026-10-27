using System;
using System.Drawing;

namespace ZeroGraphics.Vision.Calibration
{
    /// <summary>
    /// Supported real-world metrology distance units.
    /// </summary>
    public enum MetricUnit
    {
        Millimeters,
        Microns,
        Inches
    }

    /// <summary>
    /// High-precision 2D metric calibration model.
    /// Transforms image pixel coordinates and distances into real-world physical units (mm / um / in).
    /// </summary>
    public sealed class MetricCalibration2D
    {
        /// <summary>Millimeters (or chosen metric units) per pixel along the X-axis.</summary>
        public double ScaleX { get; set; }

        /// <summary>Millimeters (or chosen metric units) per pixel along the Y-axis.</summary>
        public double ScaleY { get; set; }

        /// <summary>The unit of measurement represented by this calibration.</summary>
        public MetricUnit Unit { get; set; }

        /// <summary>Optional 3x3 planar perspective homography matrix (row-major).</summary>
        public double[]? Homography { get; set; }

        /// <summary>Optional 3x3 inverse homography matrix.</summary>
        public double[]? InverseHomography { get; set; }

        public MetricCalibration2D(double scaleX, double scaleY, MetricUnit unit = MetricUnit.Millimeters)
        {
            if (scaleX <= 0 || scaleY <= 0)
                throw new ArgumentException("Calibration scale factors must be positive.");

            ScaleX = scaleX;
            ScaleY = scaleY;
            Unit = unit;
        }

        /// <summary>
        /// Creates an isotropic calibration where 1 pixel represents an identical distance in X and Y.
        /// </summary>
        public static MetricCalibration2D FromScale(double metricPerPixel, MetricUnit unit = MetricUnit.Millimeters)
        {
            return new MetricCalibration2D(metricPerPixel, metricPerPixel, unit);
        }

        /// <summary>
        /// Calibrates scale from two reference image points with a known real-world physical distance.
        /// </summary>
        public static MetricCalibration2D FromTwoPoints(
            PointF p1, PointF p2,
            double knownRealWorldDistance,
            MetricUnit unit = MetricUnit.Millimeters)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double pixelDist = Math.Sqrt(dx * dx + dy * dy);

            if (pixelDist < 1.0)
                throw new ArgumentException("Reference points are too close together.");

            double scale = knownRealWorldDistance / pixelDist;
            return new MetricCalibration2D(scale, scale, unit);
        }

        /// <summary>
        /// Transforms image pixel coordinates (px, py) into metric coordinates (mx, my).
        /// </summary>
        public void PixelToMetric(double px, double py, out double mx, out double my)
        {
            if (Homography != null)
            {
                double z = Homography[6] * px + Homography[7] * py + Homography[8];
                if (Math.Abs(z) > 1e-9)
                {
                    mx = (Homography[0] * px + Homography[1] * py + Homography[2]) / z;
                    my = (Homography[3] * px + Homography[4] * py + Homography[5]) / z;
                    return;
                }
            }

            mx = px * ScaleX;
            my = py * ScaleY;
        }

        /// <summary>
        /// Transforms metric coordinates (mx, my) back into image pixel coordinates (px, py).
        /// </summary>
        public void MetricToPixel(double mx, double my, out double px, out double py)
        {
            if (InverseHomography != null)
            {
                double z = InverseHomography[6] * mx + InverseHomography[7] * my + InverseHomography[8];
                if (Math.Abs(z) > 1e-9)
                {
                    px = (InverseHomography[0] * mx + InverseHomography[1] * my + InverseHomography[2]) / z;
                    py = (InverseHomography[3] * mx + InverseHomography[4] * my + InverseHomography[5]) / z;
                    return;
                }
            }

            px = mx / ScaleX;
            py = my / ScaleY;
        }

        /// <summary>
        /// Converts a scalar pixel length into physical metric units.
        /// </summary>
        public double PixelsToMetricDistance(double pixelDistance)
        {
            double avgScale = (ScaleX + ScaleY) * 0.5;
            return pixelDistance * avgScale;
        }

        /// <summary>
        /// Calculates the exact euclidean distance between two pixel points in physical metric units.
        /// </summary>
        public double MeasureDistance(PointF p1, PointF p2)
        {
            PixelToMetric(p1.X, p1.Y, out double m1x, out double m1y);
            PixelToMetric(p2.X, p2.Y, out double m2x, out double m2y);

            double dx = m2x - m1x;
            double dy = m2y - m1y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Formats a measured value into human-readable industrial string with appropriate unit suffix.
        /// </summary>
        public string Format(double metricValue, int decimalPlaces = 3)
        {
            string suffix = Unit switch
            {
                MetricUnit.Millimeters => "mm",
                MetricUnit.Microns => "\u03bcm",
                MetricUnit.Inches => "in",
                _ => "mm"
            };

            return $"{metricValue.ToString($"F{decimalPlaces}")} {suffix}";
        }
    }
}
