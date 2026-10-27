using System;
using System.Collections.Generic;
using ZeroGraphics.Vision.Matching;

namespace ZeroGraphics.Vision.Metrology
{
    /// <summary>
    /// Fitted 2D straight line represented in normalized form: A*x + B*y + C = 0, where A^2 + B^2 = 1.
    /// </summary>
    public readonly struct FittedLine2D
    {
        public double A { get; }
        public double B { get; }
        public double C { get; }
        public double CenterX { get; }
        public double CenterY { get; }
        public double AngleDegrees { get; }
        public double RmsError { get; }

        public FittedLine2D(double a, double b, double c, double cx, double cy, double angleDeg, double rms)
        {
            A = a;
            B = b;
            C = c;
            CenterX = cx;
            CenterY = cy;
            AngleDegrees = angleDeg;
            RmsError = rms;
        }

        /// <summary>
        /// Computes perpendicular orthogonal distance from a point to this line.
        /// </summary>
        public double DistanceTo(double px, double py)
            => Math.Abs(A * px + B * py + C);

        public override string ToString()
            => $"[Line] {A:F4}x + {B:F4}y + {C:F2} = 0, Angle={AngleDegrees:F2}°, RMS={RmsError:F4}";
    }

    /// <summary>
    /// Fitted 2D circle with center, radius, and fitting accuracy.
    /// </summary>
    public readonly struct FittedCircle2D
    {
        public double CenterX { get; }
        public double CenterY { get; }
        public double Radius { get; }
        public double Diameter => Radius * 2.0;
        public double RmsError { get; }

        public FittedCircle2D(double cx, double cy, double r, double rms)
        {
            CenterX = cx;
            CenterY = cy;
            Radius = r;
            RmsError = rms;
        }

        /// <summary>
        /// Radial deviation of a point from the circle boundary (positive = outside, negative = inside).
        /// </summary>
        public double RadialDistanceTo(double px, double py)
        {
            double dx = px - CenterX;
            double dy = py - CenterY;
            return Math.Sqrt(dx * dx + dy * dy) - Radius;
        }

        public override string ToString()
            => $"[Circle] Center=({CenterX:F3}, {CenterY:F3}), R={Radius:F3}, Dia={Diameter:F3}, RMS={RmsError:F4}";
    }

    /// <summary>
    /// Fitted 2D ellipse with center, semi-axes, orientation, and eccentricity.
    /// Used for metrology of tilted circular parts, bevels, and elliptical holes.
    /// </summary>
    public readonly struct FittedEllipse2D
    {
        public double CenterX { get; }
        public double CenterY { get; }
        public double SemiMajorAxis { get; }
        public double SemiMinorAxis { get; }

        /// <summary>
        /// Major diameter of the ellipse.
        /// </summary>
        public double MajorDiameter => SemiMajorAxis * 2.0;

        /// <summary>
        /// Minor diameter of the ellipse.
        /// </summary>
        public double MinorDiameter => SemiMinorAxis * 2.0;

        /// <summary>
        /// Orientation angle of the semi-major axis in degrees relative to X-axis [-90.0, 90.0].
        /// </summary>
        public double AngleDegrees { get; }

        /// <summary>
        /// Ellipse eccentricity e = sqrt(1 - b^2 / a^2), ranging from 0.0 (circle) to 1.0 (flat).
        /// </summary>
        public double Eccentricity { get; }

        public double RmsError { get; }

        public FittedEllipse2D(double cx, double cy, double a, double b, double angleDeg, double eccentricity, double rms)
        {
            CenterX = cx;
            CenterY = cy;
            SemiMajorAxis = a;
            SemiMinorAxis = b;
            AngleDegrees = angleDeg;
            Eccentricity = eccentricity;
            RmsError = rms;
        }

        public override string ToString()
            => $"[Ellipse] Center=({CenterX:F3}, {CenterY:F3}), a={SemiMajorAxis:F3}, b={SemiMinorAxis:F3}, Angle={AngleDegrees:F2}°, Ecc={Eccentricity:F3}, RMS={RmsError:F4}";
    }

    /// <summary>
    /// Robust orthogonal geometric fitters for machine vision metrology (TLS Line Fit & Taubin Circle Fit).
    /// </summary>
    public static class GeometryFitters
    {
        /// <summary>
        /// Fits a line to a set of 2D points using Total Least Squares (Orthogonal Distance Regression).
        /// </summary>
        public static FittedLine2D FitLine(IReadOnlyList<VisionPoint2D> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Count < 2) throw new ArgumentException("At least 2 points are required to fit a line.");

            int n = points.Count;
            double sumX = 0.0;
            double sumY = 0.0;

            for (int i = 0; i < n; i++)
            {
                sumX += points[i].X;
                sumY += points[i].Y;
            }

            double meanX = sumX / n;
            double meanY = sumY / n;

            // Covariance moments
            double sxx = 0.0;
            double syy = 0.0;
            double sxy = 0.0;

            for (int i = 0; i < n; i++)
            {
                double dx = points[i].X - meanX;
                double dy = points[i].Y - meanY;
                sxx += dx * dx;
                syy += dy * dy;
                sxy += dx * dy;
            }

            // Normal vector angle via eigendecomposition of covariance matrix
            double theta = 0.5 * Math.Atan2(-2.0 * sxy, syy - sxx);
            double a = Math.Cos(theta);
            double b = Math.Sin(theta);
            double c = -(a * meanX + b * meanY);

            // Calculate RMS error
            double sqErrorSum = 0.0;
            for (int i = 0; i < n; i++)
            {
                double dist = a * points[i].X + b * points[i].Y + c;
                sqErrorSum += dist * dist;
            }
            double rms = Math.Sqrt(sqErrorSum / n);

            // Direction angle of the line (perpendicular to normal)
            double lineAngleRad = Math.Atan2(-a, b);
            double lineAngleDeg = lineAngleRad * (180.0 / Math.PI);
            if (lineAngleDeg < 0.0) lineAngleDeg += 180.0;

            return new FittedLine2D(a, b, c, meanX, meanY, lineAngleDeg, rms);
        }

        /// <summary>
        /// Fits a line to detected caliper edge points.
        /// </summary>
        public static FittedLine2D FitLine(IReadOnlyList<CaliperEdge> edges)
        {
            if (edges == null) throw new ArgumentNullException(nameof(edges));
            var points = new VisionPoint2D[edges.Count];
            for (int i = 0; i < edges.Count; i++)
                points[i] = new VisionPoint2D(edges[i].X, edges[i].Y);
            return FitLine(points);
        }

        /// <summary>
        /// Fits a circle to a set of 2D points using the Taubin Algebraic Circle Fitting algorithm.
        /// Unbiased and robust even for partial arc segments.
        /// </summary>
        public static FittedCircle2D FitCircle(IReadOnlyList<VisionPoint2D> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Count < 3) throw new ArgumentException("At least 3 points are required to fit a circle.");

            int n = points.Count;
            double sumX = 0.0;
            double sumY = 0.0;

            for (int i = 0; i < n; i++)
            {
                sumX += points[i].X;
                sumY += points[i].Y;
            }

            double meanX = sumX / n;
            double meanY = sumY / n;

            // Moments relative to centroid
            double mxx = 0.0, myy = 0.0, mxy = 0.0;
            double mxz = 0.0, myz = 0.0, mzz = 0.0;

            for (int i = 0; i < n; i++)
            {
                double xi = points[i].X - meanX;
                double yi = points[i].Y - meanY;
                double zi = xi * xi + yi * yi;

                mxx += xi * xi;
                myy += yi * yi;
                mxy += xi * yi;
                mxz += xi * zi;
                myz += yi * zi;
                mzz += zi * zi;
            }

            mxx /= n;
            myy /= n;
            mxy /= n;
            mxz /= n;
            myz /= n;
            mzz /= n;

            // Characteristic polynomial coefficients
            double mz = mxx + myy;
            double covXy = mxx * myy - mxy * mxy;
            double a3 = 4.0 * mz;
            double a2 = -3.0 * mz * mz - mzz;
            double a1 = mzz * mz + 4.0 * covXy * mz - mxz * mxz - myz * myz - mz * mz * mz;
            double a0 = mxz * mxz * myy + myz * myz * mxx - 2.0 * mxz * myz * mxy - covXy * mzz;

            // Solve root near zero using Newton-Raphson iteration
            double eta = 0.0;
            for (int iter = 0; iter < 20; iter++)
            {
                double f = a0 + eta * (a1 + eta * (a2 + eta * (a3 + eta)));
                double fPrime = a1 + eta * (2.0 * a2 + eta * (3.0 * a3 + 4.0 * eta));
                if (Math.Abs(fPrime) < 1e-12) break;
                double step = f / fPrime;
                eta -= step;
                if (Math.Abs(step) < 1e-10) break;
            }

            // Determine center and radius
            double det = (mxx - eta) * (myy - eta) - mxy * mxy;
            if (Math.Abs(det) < 1e-12) det = 1e-12;

            double uc = (mxz * (myy - eta) - myz * mxy) / (2.0 * det);
            double vc = (myz * (mxx - eta) - mxz * mxy) / (2.0 * det);

            double cx = uc + meanX;
            double cy = vc + meanY;
            double r = Math.Sqrt(uc * uc + vc * vc + mz);

            // Compute RMS error
            double sqErrSum = 0.0;
            for (int i = 0; i < n; i++)
            {
                double d = Math.Sqrt((points[i].X - cx) * (points[i].X - cx) + (points[i].Y - cy) * (points[i].Y - cy)) - r;
                sqErrSum += d * d;
            }
            double rms = Math.Sqrt(sqErrSum / n);

            return new FittedCircle2D(cx, cy, r, rms);
        }

        /// <summary>
        /// Fits a circle to detected caliper edge points.
        /// </summary>
        public static FittedCircle2D FitCircle(IReadOnlyList<CaliperEdge> edges)
        {
            if (edges == null) throw new ArgumentNullException(nameof(edges));
            var points = new VisionPoint2D[edges.Count];
            for (int i = 0; i < edges.Count; i++)
                points[i] = new VisionPoint2D(edges[i].X, edges[i].Y);
            return FitCircle(points);
        }

        /// <summary>
        /// Fits an ellipse to a set of 2D points using the Direct Least Squares Ellipse Fitting algorithm (Fitzgibbon, 1999).
        /// Mathematically guaranteed to return a valid ellipse regardless of noise or partial arc occlusion.
        /// </summary>
        public static FittedEllipse2D FitEllipse(IReadOnlyList<VisionPoint2D> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            int n = points.Count;
            if (n < 5) throw new ArgumentException("At least 5 points are required to fit an ellipse.", nameof(points));

            // 1. Centroid normalization
            double meanX = 0, meanY = 0;
            for (int i = 0; i < n; i++)
            {
                meanX += points[i].X;
                meanY += points[i].Y;
            }
            meanX /= n;
            meanY /= n;

            double maxDev = 0;
            for (int i = 0; i < n; i++)
            {
                double dx = Math.Abs(points[i].X - meanX);
                double dy = Math.Abs(points[i].Y - meanY);
                if (dx > maxDev) maxDev = dx;
                if (dy > maxDev) maxDev = dy;
            }
            double scale = maxDev > 1e-6 ? maxDev : 1.0;
            double invScale = 1.0 / scale;

            // 2. Build Scatter matrices S1, S2, S3
            // D1 = [u^2, u*v, v^2], D2 = [u, v, 1]
            double s1_00 = 0, s1_01 = 0, s1_02 = 0, s1_11 = 0, s1_12 = 0, s1_22 = 0;
            double s2_00 = 0, s2_01 = 0, s2_02 = 0;
            double s2_10 = 0, s2_11 = 0, s2_12 = 0;
            double s2_20 = 0, s2_21 = 0, s2_22 = 0;
            double s3_00 = 0, s3_01 = 0, s3_02 = 0, s3_11 = 0, s3_12 = 0, s3_22 = n;

            for (int i = 0; i < n; i++)
            {
                double u = (points[i].X - meanX) * invScale;
                double v = (points[i].Y - meanY) * invScale;

                double u2 = u * u;
                double uv = u * v;
                double v2 = v * v;

                s1_00 += u2 * u2;
                s1_01 += u2 * uv;
                s1_02 += u2 * v2;
                s1_11 += uv * uv;
                s1_12 += uv * v2;
                s1_22 += v2 * v2;

                s2_00 += u2 * u;
                s2_01 += u2 * v;
                s2_02 += u2;
                s2_10 += uv * u;
                s2_11 += uv * v;
                s2_12 += uv;
                s2_20 += v2 * u;
                s2_21 += v2 * v;
                s2_22 += v2;

                s3_00 += u * u;
                s3_01 += u * v;
                s3_02 += u;
                s3_11 += v * v;
                s3_12 += v;
            }

            // Invert S3 (3x3 symmetric matrix)
            double det3 = s3_00 * (s3_11 * s3_22 - s3_12 * s3_12)
                        - s3_01 * (s3_01 * s3_22 - s3_12 * s3_02)
                        + s3_02 * (s3_01 * s3_12 - s3_11 * s3_02);

            if (Math.Abs(det3) < 1e-12) det3 = 1e-12;
            double invDet3 = 1.0 / det3;

            double invS3_00 = (s3_11 * s3_22 - s3_12 * s3_12) * invDet3;
            double invS3_01 = (s3_02 * s3_12 - s3_01 * s3_22) * invDet3;
            double invS3_02 = (s3_01 * s3_12 - s3_02 * s3_11) * invDet3;
            double invS3_11 = (s3_00 * s3_22 - s3_02 * s3_02) * invDet3;
            double invS3_12 = (s3_02 * s3_01 - s3_00 * s3_12) * invDet3;
            double invS3_22 = (s3_00 * s3_11 - s3_01 * s3_01) * invDet3;

            // T = -inv(S3) * S2^T  (3x3)
            // Let K = inv(S3) * S2^T
            double k00 = invS3_00 * s2_00 + invS3_01 * s2_01 + invS3_02 * s2_02;
            double k01 = invS3_00 * s2_10 + invS3_01 * s2_11 + invS3_02 * s2_12;
            double k02 = invS3_00 * s2_20 + invS3_01 * s2_21 + invS3_02 * s2_22;

            double k10 = invS3_01 * s2_00 + invS3_11 * s2_01 + invS3_12 * s2_02;
            double k11 = invS3_01 * s2_10 + invS3_11 * s2_11 + invS3_12 * s2_12;
            double k12 = invS3_01 * s2_20 + invS3_11 * s2_21 + invS3_12 * s2_22;

            double k20 = invS3_02 * s2_00 + invS3_12 * s2_01 + invS3_22 * s2_02;
            double k21 = invS3_02 * s2_10 + invS3_12 * s2_11 + invS3_22 * s2_12;
            double k22 = invS3_02 * s2_20 + invS3_12 * s2_21 + invS3_22 * s2_22;

            // R = S1 - S2 * K  (3x3)
            double r00 = s1_00 - (s2_00 * k00 + s2_01 * k10 + s2_02 * k20);
            double r01 = s1_01 - (s2_00 * k01 + s2_01 * k11 + s2_02 * k21);
            double r02 = s1_02 - (s2_00 * k02 + s2_01 * k12 + s2_02 * k22);

            double r10 = s1_01 - (s2_10 * k00 + s2_11 * k10 + s2_12 * k20);
            double r11 = s1_11 - (s2_10 * k01 + s2_11 * k11 + s2_12 * k21);
            double r12 = s1_12 - (s2_10 * k02 + s2_11 * k12 + s2_12 * k22);

            double r20 = s1_02 - (s2_20 * k00 + s2_21 * k10 + s2_22 * k20);
            double r21 = s1_12 - (s2_20 * k01 + s2_21 * k11 + s2_22 * k21);
            double r22 = s1_22 - (s2_20 * k02 + s2_21 * k12 + s2_22 * k22);

            // M = inv(C1) * R, where inv(C1) = [0 0 0.5; 0 -1 0; 0.5 0 0]
            double m00 = 0.5 * r20, m01 = 0.5 * r21, m02 = 0.5 * r22;
            double m10 = -r10,     m11 = -r11,     m12 = -r12;
            double m20 = 0.5 * r00, m21 = 0.5 * r01, m22 = 0.5 * r02;

            // Eigenvalues of 3x3 matrix M via characteristic polynomial det(M - lambda*I) = 0
            // lambda^3 - c2*lambda^2 + c1*lambda - c0 = 0
            double c2 = m00 + m11 + m22; // Trace
            double c1 = (m00 * m11 - m01 * m10) + (m00 * m22 - m02 * m20) + (m11 * m22 - m12 * m21);
            double c0 = m00 * (m11 * m22 - m12 * m21) - m01 * (m10 * m22 - m12 * m20) + m02 * (m10 * m21 - m11 * m20);

            // Depressed cubic t^3 + p*t + q = 0, where lambda = t + c2/3
            double p = c1 - (c2 * c2) / 3.0;
            double q = (-2.0 * c2 * c2 * c2) / 27.0 + (c2 * c1) / 3.0 - c0;

            double[] eigenValues = new double[3];
            int numRealRoots = 3;

            double r_val = Math.Sqrt(Math.Max(0.0, -p * p * p / 27.0));
            if (r_val < 1e-12) r_val = 1e-12;
            double phi = Math.Acos(Math.Max(-1.0, Math.Min(1.0, -q / (2.0 * r_val))));
            double t0 = 2.0 * Math.Sqrt(Math.Max(0.0, -p / 3.0));

            eigenValues[0] = t0 * Math.Cos(phi / 3.0) + c2 / 3.0;
            eigenValues[1] = t0 * Math.Cos((phi + 2.0 * Math.PI) / 3.0) + c2 / 3.0;
            eigenValues[2] = t0 * Math.Cos((phi + 4.0 * Math.PI) / 3.0) + c2 / 3.0;

            // Find eigenvector satisfying 4*a*c - b^2 > 0
            double bestA = 0, bestB = 0, bestC = 0;
            bool found = false;

            for (int eIdx = 0; eIdx < numRealRoots; eIdx++)
            {
                double val = eigenValues[eIdx];

                // (M - val*I) * v = 0
                double a00 = m00 - val, a01 = m01, a02 = m02;
                double a10 = m10, a11 = m11 - val, a12 = m12;
                double a20 = m20, a21 = m21, a22 = m22 - val;

                // Cross product of rows to find null vector: test all 3 row pairs
                double vx1 = a01 * a12 - a02 * a11;
                double vy1 = a02 * a10 - a00 * a12;
                double vz1 = a00 * a11 - a01 * a10;
                double len1 = vx1 * vx1 + vy1 * vy1 + vz1 * vz1;

                double vx2 = a11 * a22 - a12 * a21;
                double vy2 = a12 * a20 - a10 * a22;
                double vz2 = a10 * a21 - a11 * a20;
                double len2 = vx2 * vx2 + vy2 * vy2 + vz2 * vz2;

                double vx3 = a01 * a22 - a02 * a21;
                double vy3 = a02 * a20 - a00 * a22;
                double vz3 = a00 * a21 - a01 * a20;
                double len3 = vx3 * vx3 + vy3 * vy3 + vz3 * vz3;

                double vx = vx1, vy = vy1, vz = vz1, maxLen = len1;
                if (len2 > maxLen) { vx = vx2; vy = vy2; vz = vz2; maxLen = len2; }
                if (len3 > maxLen) { vx = vx3; vy = vy3; vz = vz3; maxLen = len3; }

                double vLen = Math.Sqrt(maxLen);

                if (vLen > 1e-9)
                {
                    vx /= vLen;
                    vy /= vLen;
                    vz /= vLen;

                    // Constraint check 4*vx*vz - vy^2 > 0
                    double cond = 4.0 * vx * vz - vy * vy;
                    if (cond > 0)
                    {
                        double normFactor = Math.Sqrt(1.0 / cond);
                        bestA = vx * normFactor;
                        bestB = vy * normFactor;
                        bestC = vz * normFactor;
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                // Fallback circle if fitting degrades to a circle
                var fc = FitCircle(points);
                return new FittedEllipse2D(fc.CenterX, fc.CenterY, fc.Radius, fc.Radius, 0.0, 0.0, fc.RmsError);
            }

            // Compute a2 = -K * a1
            double bestD = -(k00 * bestA + k01 * bestB + k02 * bestC);
            double bestE = -(k10 * bestA + k11 * bestB + k12 * bestC);
            double bestF = -(k20 * bestA + k21 * bestB + k22 * bestC);

            // Unnormalize back to world coordinates
            double realA = bestA * (invScale * invScale);
            double realB = bestB * (invScale * invScale);
            double realC = bestC * (invScale * invScale);
            double realD = bestD * invScale - 2.0 * realA * meanX - realB * meanY;
            double realE = bestE * invScale - realB * meanX - 2.0 * realC * meanY;
            double realF = bestF - bestD * invScale * meanX - bestE * invScale * meanY
                         + realA * meanX * meanX + realB * meanX * meanY + realC * meanY * meanY;

            // Algebraic to Geometric parameters
            double denom = realB * realB - 4.0 * realA * realC;
            if (denom >= -1e-12) denom = -1e-12;

            double cx = (2.0 * realC * realD - realB * realE) / denom;
            double cy = (2.0 * realA * realE - realB * realD) / denom;

            // Shift to center: A*(x-cx)^2 + B*(x-cx)*(y-cy) + C*(y-cy)^2 = -f0
            double f0 = realA * cx * cx + realB * cx * cy + realC * cy * cy + realD * cx + realE * cy + realF;
            if (Math.Abs(f0) < 1e-12) f0 = -1.0;

            // Normalized quadratic matrix: [aNorm, bNorm/2; bNorm/2, cNorm]
            double aNorm = -realA / f0;
            double bNorm = -realB / f0;
            double cNorm = -realC / f0;

            double disc = Math.Sqrt((aNorm - cNorm) * (aNorm - cNorm) + bNorm * bNorm);
            double l1 = 0.5 * (aNorm + cNorm - disc);
            double l2 = 0.5 * (aNorm + cNorm + disc);

            double r1 = l1 > 1e-12 ? 1.0 / Math.Sqrt(l1) : 0.0;
            double r2 = l2 > 1e-12 ? 1.0 / Math.Sqrt(l2) : 0.0;

            double semiMajor = Math.Max(r1, r2);
            double semiMinor = Math.Min(r1, r2);

            // Eigenvector direction for semi-major axis (smallest eigenvalue l1)
            double evX = l1 - cNorm;
            double evY = bNorm * 0.5;
            if (Math.Abs(evX) < 1e-9 && Math.Abs(evY) < 1e-9)
            {
                evX = bNorm * 0.5;
                evY = l1 - aNorm;
            }

            double angleDeg = Math.Atan2(evY, evX) * (180.0 / Math.PI);
            while (angleDeg > 90.0) angleDeg -= 180.0;
            while (angleDeg < -90.0) angleDeg += 180.0;

            double ecc = semiMajor > 1e-9 ? Math.Sqrt(Math.Max(0.0, 1.0 - (semiMinor * semiMinor) / (semiMajor * semiMajor))) : 0.0;

            // RMS Error
            double sqSum = 0;
            for (int i = 0; i < n; i++)
            {
                // Geometric distance approximation: algebraic error divided by gradient norm
                double xi = points[i].X;
                double yi = points[i].Y;
                double fVal = realA * xi * xi + realB * xi * yi + realC * yi * yi + realD * xi + realE * yi + realF;
                double gradX = 2.0 * realA * xi + realB * yi + realD;
                double gradY = realB * xi + 2.0 * realC * yi + realE;
                double gradNorm = Math.Sqrt(gradX * gradX + gradY * gradY);
                double d = gradNorm > 1e-6 ? Math.Abs(fVal) / gradNorm : 0.0;
                sqSum += d * d;
            }
            double rms = Math.Sqrt(sqSum / n);

            return new FittedEllipse2D(cx, cy, semiMajor, semiMinor, angleDeg, ecc, rms);
        }

        /// <summary>
        /// Fits an ellipse to detected caliper edge points.
        /// </summary>
        public static FittedEllipse2D FitEllipse(IReadOnlyList<CaliperEdge> edges)
        {
            if (edges == null) throw new ArgumentNullException(nameof(edges));
            var points = new VisionPoint2D[edges.Count];
            for (int i = 0; i < edges.Count; i++)
                points[i] = new VisionPoint2D(edges[i].X, edges[i].Y);
            return FitEllipse(points);
        }
    }

    /// <summary>
    /// Industrial Metrology Measurements & Tolerance Verification.
    /// </summary>
    public static class MetrologyMeasurements
    {
        /// <summary>
        /// Euclidean distance between two 2D points.
        /// </summary>
        public static double Distance(VisionPoint2D p1, VisionPoint2D p2)
        {
            double dx = p1.X - p2.X;
            double dy = p1.Y - p2.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Distance from a point to a fitted line.
        /// </summary>
        public static double Distance(VisionPoint2D p, FittedLine2D line)
            => line.DistanceTo(p.X, p.Y);

        /// <summary>
        /// Average perpendicular distance between two roughly parallel lines.
        /// </summary>
        public static double DistanceBetweenLines(FittedLine2D line1, FittedLine2D line2)
        {
            double d1 = line2.DistanceTo(line1.CenterX, line1.CenterY);
            double d2 = line1.DistanceTo(line2.CenterX, line2.CenterY);
            return (d1 + d2) * 0.5;
        }

        /// <summary>
        /// Angular difference between two lines in degrees [0, 90].
        /// </summary>
        public static double AngleBetweenLines(FittedLine2D line1, FittedLine2D line2)
        {
            double diff = Math.Abs(line1.AngleDegrees - line2.AngleDegrees);
            if (diff > 90.0) diff = 180.0 - diff;
            return diff;
        }

        /// <summary>
        /// Concentricity (center-to-center offset error) between two circles.
        /// 0.0 indicates perfect concentric alignment.
        /// </summary>
        public static double Concentricity(FittedCircle2D c1, FittedCircle2D c2)
        {
            double dx = c1.CenterX - c2.CenterX;
            double dy = c1.CenterY - c2.CenterY;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }

    /// <summary>
    /// Geometric Dimensioning and Tolerancing (GD&T) metric evaluator compliant with ISO 1101.
    /// Evaluates Straightness, Roundness (Circularity), Perpendicularity, Parallelism, and line intersections.
    /// </summary>
    public static class GdtEvaluator
    {
        /// <summary>
        /// Calculates peak-to-valley Straightness error of sampled edge points relative to a fitted line.
        /// Defined as the maximum signed orthogonal deviation minus the minimum signed deviation.
        /// </summary>
        public static double CalculateStraightness(IReadOnlyList<VisionPoint2D> points, FittedLine2D line)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Count == 0) return 0.0;

            double maxDev = double.MinValue;
            double minDev = double.MaxValue;

            for (int i = 0; i < points.Count; i++)
            {
                // Signed orthogonal distance: A*x + B*y + C
                double signedDist = line.A * points[i].X + line.B * points[i].Y + line.C;
                if (signedDist > maxDev) maxDev = signedDist;
                if (signedDist < minDev) minDev = signedDist;
            }

            return Math.Max(0.0, maxDev - minDev);
        }

        /// <summary>
        /// Calculates peak-to-valley Roundness (Circularity) error of sampled edge points relative to a fitted circle.
        /// Defined as the radial distance between the maximum and minimum circumscribing profile zones: max(R_i) - min(R_i).
        /// </summary>
        public static double CalculateRoundness(IReadOnlyList<VisionPoint2D> points, FittedCircle2D circle)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Count == 0) return 0.0;

            double maxR = double.MinValue;
            double minR = double.MaxValue;

            for (int i = 0; i < points.Count; i++)
            {
                double dx = points[i].X - circle.CenterX;
                double dy = points[i].Y - circle.CenterY;
                double r = Math.Sqrt(dx * dx + dy * dy);

                if (r > maxR) maxR = r;
                if (r < minR) minR = r;
            }

            return Math.Max(0.0, maxR - minR);
        }

        /// <summary>
        /// Computes the intersection point between two fitted straight lines.
        /// Returns false if lines are parallel or coincident within tolerance.
        /// </summary>
        public static bool IntersectLines(FittedLine2D line1, FittedLine2D line2, out VisionPoint2D intersection)
        {
            // Solve 2x2 system:
            // A1*x + B1*y = -C1
            // A2*x + B2*y = -C2
            double det = line1.A * line2.B - line2.A * line1.B;
            if (Math.Abs(det) < 1e-9)
            {
                intersection = default;
                return false;
            }

            double x = (-line1.C * line2.B - -line2.C * line1.B) / det;
            double y = (line1.A * -line2.C - line2.A * -line1.C) / det;

            intersection = new VisionPoint2D(x, y);
            return true;
        }

        /// <summary>
        /// Projects an arbitrary 2D point perpendicularly onto a fitted line.
        /// </summary>
        public static VisionPoint2D ProjectPointToLine(VisionPoint2D p, FittedLine2D line)
        {
            // Signed distance d = A*x + B*y + C
            double d = line.A * p.X + line.B * p.Y + line.C;
            // Orthogonal projection: p' = p - d * (A, B)
            return new VisionPoint2D(p.X - d * line.A, p.Y - d * line.B);
        }

        /// <summary>
        /// Angular perpendicularity deviation error in degrees relative to 90.0°.
        /// 0.0° indicates perfect orthogonality.
        /// </summary>
        public static double PerpendicularityError(FittedLine2D line1, FittedLine2D line2)
        {
            double angle = MetrologyMeasurements.AngleBetweenLines(line1, line2);
            return Math.Abs(90.0 - angle);
        }

        /// <summary>
        /// Angular parallelism deviation error in degrees relative to 0.0°.
        /// 0.0° indicates perfect parallel alignment.
        /// </summary>
        public static double ParallelismError(FittedLine2D line1, FittedLine2D line2)
        {
            return MetrologyMeasurements.AngleBetweenLines(line1, line2);
        }
    }
}
