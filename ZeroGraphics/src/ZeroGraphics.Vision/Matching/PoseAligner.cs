using System;

namespace ZeroGraphics.Vision.Matching
{
    /// <summary>
    /// Represents 2D position coordinates.
    /// </summary>
    public readonly struct VisionPoint2D
    {
        public double X { get; }
        public double Y { get; }

        public VisionPoint2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"({X:F3}, {Y:F3})";
    }

    /// <summary>
    /// Result of 2D workpiece / PCB alignment pose estimation.
    /// </summary>
    public readonly struct PoseAlignmentResult
    {
        /// <summary>
        /// Rotation angle in degrees (clockwise positive).
        /// </summary>
        public double AngleDegrees { get; }

        /// <summary>
        /// Rotation angle in radians.
        /// </summary>
        public double AngleRadians { get; }

        /// <summary>
        /// Translation shift along X axis.
        /// </summary>
        public double TranslationX { get; }

        /// <summary>
        /// Translation shift along Y axis.
        /// </summary>
        public double TranslationY { get; }

        /// <summary>
        /// Dimensional scaling factor (measured distance / nominal distance).
        /// Expected ~1.0 for rigid workpieces.
        /// </summary>
        public double Scale { get; }

        public PoseAlignmentResult(double angleRadians, double transX, double transY, double scale)
        {
            AngleRadians = angleRadians;
            AngleDegrees = angleRadians * (180.0 / Math.PI);
            TranslationX = transX;
            TranslationY = transY;
            Scale = scale;
        }

        public override string ToString()
            => $"[Pose] Angle={AngleDegrees:F3}°, Offset=({TranslationX:F3}, {TranslationY:F3}), Scale={Scale:F4}";
    }

    /// <summary>
    /// Automated Workpiece / PCB 2-Point Pose Alignment Engine.
    /// Computes rotation, translation offset, and scale from 2 nominal vs measured fiducial marks.
    /// </summary>
    public static class PoseAligner
    {
        /// <summary>
        /// Aligns workpiece pose based on two fiducial marks.
        /// </summary>
        /// <param name="nominal1">Nominal (CAD) position of mark 1.</param>
        /// <param name="nominal2">Nominal (CAD) position of mark 2.</param>
        /// <param name="measured1">Measured (Camera) position of mark 1.</param>
        /// <param name="measured2">Measured (Camera) position of mark 2.</param>
        /// <returns>Calculated rigid transformation pose.</returns>
        public static PoseAlignmentResult CalculatePose(
            VisionPoint2D nominal1,
            VisionPoint2D nominal2,
            VisionPoint2D measured1,
            VisionPoint2D measured2)
        {
            double dNomX = nominal2.X - nominal1.X;
            double dNomY = nominal2.Y - nominal1.Y;
            double nomDist = Math.Sqrt(dNomX * dNomX + dNomY * dNomY);

            double dMeasX = measured2.X - measured1.X;
            double dMeasY = measured2.Y - measured1.Y;
            double measDist = Math.Sqrt(dMeasX * dMeasX + dMeasY * dMeasY);

            if (nomDist < 1e-6)
                throw new ArgumentException("Nominal fiducial points cannot be identical.");

            // 1. Scale
            double scale = measDist / nomDist;

            // 2. Angle difference
            double angleNom = Math.Atan2(dNomY, dNomX);
            double angleMeas = Math.Atan2(dMeasY, dMeasX);
            double angleDiff = angleMeas - angleNom;

            // Normalize angle to [-PI, PI]
            while (angleDiff > Math.PI) angleDiff -= 2.0 * Math.PI;
            while (angleDiff < -Math.PI) angleDiff += 2.0 * Math.PI;

            // 3. Center offset
            double nomCenterX = (nominal1.X + nominal2.X) * 0.5;
            double nomCenterY = (nominal1.Y + nominal2.Y) * 0.5;

            double measCenterX = (measured1.X + measured2.X) * 0.5;
            double measCenterY = (measured1.Y + measured2.Y) * 0.5;

            double transX = measCenterX - nomCenterX;
            double transY = measCenterY - nomCenterY;

            return new PoseAlignmentResult(angleDiff, transX, transY, scale);
        }
    }
}
