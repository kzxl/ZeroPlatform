using System;

namespace ZeroGraphics.Vision.Blob
{
    /// <summary>
    /// Geometric metrics and morphological properties of an extracted connected component (Blob).
    /// </summary>
    public sealed class BlobInfo
    {
        public int Id { get; }
        public int Area { get; set; }
        public int MinX { get; set; }
        public int MinY { get; set; }
        public int MaxX { get; set; }
        public int MaxY { get; set; }

        public int Width => MaxX - MinX + 1;
        public int Height => MaxY - MinY + 1;

        public double CentroidX { get; set; }
        public double CentroidY { get; set; }

        public double Perimeter { get; set; }

        /// <summary>
        /// Shape circularity / compactness metric: 4 * PI * Area / (Perimeter^2).
        /// A perfect circle has Circularity = 1.0. Elongated or complex shapes have values closer to 0.0.
        /// </summary>
        public double Circularity { get; set; }

        /// <summary>
        /// Aspect ratio (Width / Height).
        /// </summary>
        public double AspectRatio => Height > 0 ? (double)Width / Height : 1.0;

        /// <summary>
        /// Minimum Area Oriented Bounding Box (OBB) with true orientation angle.
        /// Essential for robotic pick-and-place gripper alignment.
        /// </summary>
        public ZeroGraphics.Vision.Metrology.RotatedRect2D OrientedBox { get; set; }

        /// <summary>
        /// Primary orientation angle in degrees [-90.0, 90.0].
        /// </summary>
        public double OrientationAngle => OrientedBox.AngleDegrees;

        public BlobInfo(int id)
        {
            Id = id;
            Area = 0;
            MinX = int.MaxValue;
            MinY = int.MaxValue;
            MaxX = int.MinValue;
            MaxY = int.MinValue;
            CentroidX = 0.0;
            CentroidY = 0.0;
            Perimeter = 0.0;
            Circularity = 0.0;
        }

        public override string ToString()
            => $"[Blob #{Id}] Area={Area}, Center=({CentroidX:F1}, {CentroidY:F1}), Box=[{MinX},{MinY} {Width}x{Height}], Circ={Circularity:F3}";
    }
}
