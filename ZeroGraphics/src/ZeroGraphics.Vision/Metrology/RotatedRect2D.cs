using System;
using ZeroGraphics.Vision.Matching;

namespace ZeroGraphics.Vision.Metrology
{
    /// <summary>
    /// Oriented 2D Bounding Box (OBB) defined by center, dimensions, and rotation angle.
    /// Primarily used for robotic pick-and-place, workpiece pose alignment, and oriented feature inspection.
    /// </summary>
    public readonly struct RotatedRect2D
    {
        public double CenterX { get; }
        public double CenterY { get; }
        public double Width { get; }
        public double Height { get; }

        /// <summary>
        /// Rotation angle in degrees relative to the positive X-axis [-90.0, 90.0].
        /// </summary>
        public double AngleDegrees { get; }

        public double Area => Width * Height;

        public RotatedRect2D(double centerX, double centerY, double width, double height, double angleDegrees)
        {
            CenterX = centerX;
            CenterY = centerY;
            Width = Math.Max(0.0, width);
            Height = Math.Max(0.0, height);
            AngleDegrees = angleDegrees;
        }

        /// <summary>
        /// Computes the 4 corners of this oriented rectangle in clockwise order starting from top-left.
        /// </summary>
        public void GetVertices(Span<VisionPoint2D> destination)
        {
            if (destination.Length < 4)
                throw new ArgumentException("Destination span must have at least 4 elements.", nameof(destination));

            double rad = AngleDegrees * (Math.PI / 180.0);
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            double hw = Width * 0.5;
            double hh = Height * 0.5;

            // Local corner offsets: (-hw, -hh), (hw, -hh), (hw, hh), (-hw, hh)
            destination[0] = new VisionPoint2D(CenterX + (-hw * cos - -hh * sin), CenterY + (-hw * sin + -hh * cos));
            destination[1] = new VisionPoint2D(CenterX + (hw * cos - -hh * sin), CenterY + (hw * sin + -hh * cos));
            destination[2] = new VisionPoint2D(CenterX + (hw * cos - hh * sin), CenterY + (hw * sin + hh * cos));
            destination[3] = new VisionPoint2D(CenterX + (-hw * cos - hh * sin), CenterY + (-hw * sin + hh * cos));
        }

        /// <summary>
        /// Allocates and returns an array containing the 4 vertices of the oriented rectangle.
        /// </summary>
        public VisionPoint2D[] GetVertices()
        {
            var vertices = new VisionPoint2D[4];
            GetVertices(vertices.AsSpan());
            return vertices;
        }

        /// <summary>
        /// Checks if an arbitrary 2D point lies within this oriented rectangle.
        /// </summary>
        public bool Contains(double px, double py)
        {
            // Transform point into unrotated local coordinate frame
            double dx = px - CenterX;
            double dy = py - CenterY;

            double rad = -AngleDegrees * (Math.PI / 180.0);
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            double localX = dx * cos - dy * sin;
            double localY = dx * sin + dy * cos;

            double hw = Width * 0.5;
            double hh = Height * 0.5;

            return Math.Abs(localX) <= hw && Math.Abs(localY) <= hh;
        }

        public override string ToString()
            => $"[RotatedRect] Center=({CenterX:F2}, {CenterY:F2}), Size={Width:F2}x{Height:F2}, Angle={AngleDegrees:F2}°";
    }
}
