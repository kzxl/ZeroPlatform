using System;

namespace ZeroGraphics.Vision.Calibration
{
    /// <summary>
    /// Represents the intrinsic parameters and lens distortion coefficients of an optical camera.
    /// Uses the standard Brown-Conrady lens distortion model (radial k1, k2, k3 and tangential p1, p2).
    /// </summary>
    public sealed class CameraIntrinsics
    {
        /// <summary>Focal length along X axis in pixels.</summary>
        public double Fx { get; set; }

        /// <summary>Focal length along Y axis in pixels.</summary>
        public double Fy { get; set; }

        /// <summary>Optical center (principal point) X in pixels.</summary>
        public double Cx { get; set; }

        /// <summary>Optical center (principal point) Y in pixels.</summary>
        public double Cy { get; set; }

        /// <summary>First radial distortion coefficient (barrel/pincushion).</summary>
        public double K1 { get; set; }

        /// <summary>Second radial distortion coefficient.</summary>
        public double K2 { get; set; }

        /// <summary>Third radial distortion coefficient.</summary>
        public double K3 { get; set; }

        /// <summary>First tangential distortion coefficient (lens tilt/decentering).</summary>
        public double P1 { get; set; }

        /// <summary>Second tangential distortion coefficient.</summary>
        public double P2 { get; set; }

        public CameraIntrinsics(
            double fx, double fy,
            double cx, double cy,
            double k1 = 0.0, double k2 = 0.0,
            double p1 = 0.0, double p2 = 0.0,
            double k3 = 0.0)
        {
            Fx = fx;
            Fy = fy;
            Cx = cx;
            Cy = cy;
            K1 = k1;
            K2 = k2;
            P1 = p1;
            P2 = p2;
            K3 = k3;
        }

        /// <summary>
        /// Applies the forward lens distortion model to ideal undistorted pixel coordinates (u, v).
        /// </summary>
        public void DistortPoint(double u, double v, out double uDistorted, out double vDistorted)
        {
            // Convert to normalized sensor coordinates
            double x = (u - Cx) / Fx;
            double y = (v - Cy) / Fy;

            double r2 = x * x + y * y;
            double r4 = r2 * r2;
            double r6 = r4 * r2;

            // Radial distortion multiplier
            double radial = 1.0 + K1 * r2 + K2 * r4 + K3 * r6;

            // Tangential distortion terms
            double dx = 2.0 * P1 * x * y + P2 * (r2 + 2.0 * x * x);
            double dy = P1 * (r2 + 2.0 * y * y) + 2.0 * P2 * x * y;

            double xd = x * radial + dx;
            double yd = y * radial + dy;

            // Project back to pixel coordinates
            uDistorted = xd * Fx + Cx;
            vDistorted = yd * Fy + Cy;
        }

        /// <summary>
        /// Inverts the lens distortion using iterative Newton-Raphson refinement,
        /// mapping distorted pixel coordinates (uDist, vDist) back to the true linear perspective (u, v).
        /// </summary>
        public void UndistortPoint(double uDistorted, double vDistorted, out double uUndistorted, out double vUndistorted, int maxIterations = 8)
        {
            // If distortion is zero, return directly
            if (Math.Abs(K1) < 1e-9 && Math.Abs(K2) < 1e-9 && Math.Abs(P1) < 1e-9 && Math.Abs(P2) < 1e-9)
            {
                uUndistorted = uDistorted;
                vUndistorted = vDistorted;
                return;
            }

            // Initial estimate in normalized coordinates
            double xd = (uDistorted - Cx) / Fx;
            double yd = (vDistorted - Cy) / Fy;

            double x = xd;
            double y = yd;

            // Iteratively solve for ideal (x, y)
            for (int iter = 0; iter < maxIterations; iter++)
            {
                double r2 = x * x + y * y;
                double r4 = r2 * r2;
                double r6 = r4 * r2;

                double radial = 1.0 + K1 * r2 + K2 * r4 + K3 * r6;
                double dx = 2.0 * P1 * x * y + P2 * (r2 + 2.0 * x * x);
                double dy = P1 * (r2 + 2.0 * y * y) + 2.0 * P2 * x * y;

                double xEstimated = x * radial + dx;
                double yEstimated = y * radial + dy;

                double errX = xd - xEstimated;
                double errY = yd - yEstimated;

                x += errX;
                y += errY;

                if (Math.Abs(errX) < 1e-7 && Math.Abs(errY) < 1e-7)
                    break;
            }

            uUndistorted = x * Fx + Cx;
            vUndistorted = y * Fy + Cy;
        }
    }
}
