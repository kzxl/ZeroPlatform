using System;
using System.Collections.Generic;
using ZeroGeometry.Core.PointCloud;
using ZeroGeometry.Core.Spatial;

namespace ZeroScan3D.Core.Camera
{
    /// <summary>
    /// Unprojects 2D depth buffers into dense 3D point clouds with optional color binding.
    /// </summary>
    public static class DepthUnprojector
    {
        /// <summary>
        /// Unprojects an RGB-D frame into a geometry-compatible PointCloud3D.
        /// </summary>
        public static PointCloud3D Unproject(
            RgbdFrame frame,
            CameraIntrinsics intrinsics,
            double minDepthMeters = 0.1,
            double maxDepthMeters = 10.0,
            int stride = 1)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (intrinsics == null) throw new ArgumentNullException(nameof(intrinsics));
            if (stride < 1) stride = 1;

            int width = frame.Width;
            int height = frame.Height;
            var points = new List<Point3D>((width / stride) * (height / stride));

            double depthScale = intrinsics.DepthScale;
            double fx = intrinsics.Fx;
            double fy = intrinsics.Fy;
            double cx = intrinsics.Cx;
            double cy = intrinsics.Cy;

            for (int v = 0; v < height; v += stride)
            {
                for (int u = 0; u < width; u += stride)
                {
                    float raw = frame.GetDepth(u, v);
                    double z = raw * depthScale;

                    if (z >= minDepthMeters && z <= maxDepthMeters && !double.IsNaN(z) && !double.IsInfinity(z))
                    {
                        double x = (u - cx) * z / fx;
                        double y = (v - cy) * z / fy;
                        points.Add(new Point3D(x, y, z));
                    }
                }
            }

            return new PointCloud3D(points);
        }

        /// <summary>
        /// Unprojects an RGB-D frame into a list of ColoredPoint3D instances.
        /// </summary>
        public static List<ColoredPoint3D> UnprojectColored(
            RgbdFrame frame,
            CameraIntrinsics intrinsics,
            double minDepthMeters = 0.1,
            double maxDepthMeters = 10.0,
            int stride = 1)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (intrinsics == null) throw new ArgumentNullException(nameof(intrinsics));
            if (stride < 1) stride = 1;

            int width = frame.Width;
            int height = frame.Height;
            var points = new List<ColoredPoint3D>((width / stride) * (height / stride));

            double depthScale = intrinsics.DepthScale;
            double fx = intrinsics.Fx;
            double fy = intrinsics.Fy;
            double cx = intrinsics.Cx;
            double cy = intrinsics.Cy;
            bool hasColor = frame.HasColor;

            for (int v = 0; v < height; v += stride)
            {
                for (int u = 0; u < width; u += stride)
                {
                    float raw = frame.GetDepth(u, v);
                    double z = raw * depthScale;

                    if (z >= minDepthMeters && z <= maxDepthMeters && !double.IsNaN(z) && !double.IsInfinity(z))
                    {
                        double x = (u - cx) * z / fx;
                        double y = (v - cy) * z / fy;
                        var pos = new Point3D(x, y, z);
                        var col = hasColor ? frame.GetColor(u, v) : RgbColor.White;
                        points.Add(new ColoredPoint3D(pos, col));
                    }
                }
            }

            return points;
        }
    }
}
