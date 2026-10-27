using System;
using ZeroGeometry.Core.Spatial;

namespace ZeroScan3D.Core.Camera
{
    /// <summary>
    /// Pinhole camera intrinsic parameters for 3D projection and unprojection.
    /// </summary>
    public sealed class CameraIntrinsics
    {
        public double Fx { get; }
        public double Fy { get; }
        public double Cx { get; }
        public double Cy { get; }
        public int Width { get; }
        public int Height { get; }
        public double DepthScale { get; }

        public CameraIntrinsics(
            double fx,
            double fy,
            double cx,
            double cy,
            int width,
            int height,
            double depthScale = 0.001)
        {
            if (fx <= 0) throw new ArgumentOutOfRangeException(nameof(fx), "Focal length Fx must be positive.");
            if (fy <= 0) throw new ArgumentOutOfRangeException(nameof(fy), "Focal length Fy must be positive.");
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
            if (depthScale <= 0) throw new ArgumentOutOfRangeException(nameof(depthScale), "DepthScale must be positive.");

            Fx = fx;
            Fy = fy;
            Cx = cx;
            Cy = cy;
            Width = width;
            Height = height;
            DepthScale = depthScale;
        }

        /// <summary>
        /// Unprojects a 2D pixel coordinate (u, v) and raw depth value to a 3D point in camera space (meters).
        /// </summary>
        public Point3D Unproject(double u, double v, double rawDepth)
        {
            double z = rawDepth * DepthScale;
            if (z <= 1e-6)
                return default;

            double x = (u - Cx) * z / Fx;
            double y = (v - Cy) * z / Fy;
            return new Point3D(x, y, z);
        }

        /// <summary>
        /// Projects a 3D point in camera space (meters) onto the 2D image plane (u, v).
        /// Returns false if the point is behind the camera or outside image bounds.
        /// </summary>
        public bool Project(Point3D point, out double u, out double v)
        {
            if (point.Z <= 1e-6)
            {
                u = 0;
                v = 0;
                return false;
            }

            u = (point.X * Fx / point.Z) + Cx;
            v = (point.Y * Fy / point.Z) + Cy;

            return u >= 0 && u < Width && v >= 0 && v < Height;
        }

        /// <summary>
        /// Factory for typical RealSense D435 RGB-D depth camera profile (640x480).
        /// </summary>
        public static CameraIntrinsics RealSenseD435_640x480() =>
            new CameraIntrinsics(fx: 382.5, fy: 382.5, cx: 320.0, cy: 240.0, width: 640, height: 480, depthScale: 0.001);

        /// <summary>
        /// Factory for typical Microsoft Azure Kinect 1080p profile.
        /// </summary>
        public static CameraIntrinsics AzureKinect_1080p() =>
            new CameraIntrinsics(fx: 912.0, fy: 912.0, cx: 960.0, cy: 540.0, width: 1920, height: 1080, depthScale: 0.001);

        /// <summary>
        /// Factory for synthetic test sensor (e.g. 100x100 for fast testing).
        /// </summary>
        public static CameraIntrinsics SyntheticTest(int width = 100, int height = 100, double fovDegrees = 60.0)
        {
            double fovRad = fovDegrees * Math.PI / 180.0;
            double focal = (width / 2.0) / Math.Tan(fovRad / 2.0);
            return new CameraIntrinsics(
                fx: focal,
                fy: focal,
                cx: width / 2.0,
                cy: height / 2.0,
                width: width,
                height: height,
                depthScale: 1.0);
        }
    }
}
