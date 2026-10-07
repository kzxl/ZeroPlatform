using System;
using ZeroGeometry.Core.Spatial;
using ZeroScan3D.Core.Camera;

namespace ZeroScan3D.Core.Mapping
{
    /// <summary>
    /// Represents a single fused spatial voxel cell in the 3D map.
    /// </summary>
    public sealed class SpatialVoxel
    {
        public Point3D Centroid { get; private set; }
        public RgbColor Color { get; private set; }
        public int Weight { get; private set; }

        public SpatialVoxel(Point3D point, RgbColor color, int weight = 1)
        {
            Centroid = point;
            Color = color;
            Weight = weight;
        }

        /// <summary>
        /// Fuses a new observed point and color into this voxel using running average.
        /// </summary>
        public void Fuse(Point3D point, RgbColor color)
        {
            int oldWeight = Weight;
            int newWeight = oldWeight + 1;

            double cx = (Centroid.X * oldWeight + point.X) / newWeight;
            double cy = (Centroid.Y * oldWeight + point.Y) / newWeight;
            double cz = (Centroid.Z * oldWeight + point.Z) / newWeight;
            Centroid = new Point3D(cx, cy, cz);

            byte cr = (byte)Math.Min(255, (Color.R * oldWeight + color.R) / newWeight);
            byte cg = (byte)Math.Min(255, (Color.G * oldWeight + color.G) / newWeight);
            byte cb = (byte)Math.Min(255, (Color.B * oldWeight + color.B) / newWeight);
            Color = new RgbColor(cr, cg, cb);

            Weight = newWeight;
        }
    }
}
