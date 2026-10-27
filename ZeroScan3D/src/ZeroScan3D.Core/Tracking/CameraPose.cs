using System;
using ZeroGeometry.Core.Spatial;

namespace ZeroScan3D.Core.Tracking
{
    /// <summary>
    /// Represents a 6-DOF rigid transformation pose [R | t] in 3D Euclidean space.
    /// </summary>
    public sealed class CameraPose : IEquatable<CameraPose>
    {
        public double[,] Rotation { get; } // 3x3
        public Point3D Translation { get; }

        public static readonly CameraPose Identity = new CameraPose(
            new double[3, 3] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } },
            new Point3D(0, 0, 0));

        public CameraPose(double[,] rotation, Point3D translation)
        {
            if (rotation == null) throw new ArgumentNullException(nameof(rotation));
            if (rotation.GetLength(0) != 3 || rotation.GetLength(1) != 3)
                throw new ArgumentException("Rotation matrix must be 3x3.");

            Rotation = (double[,])rotation.Clone();
            Translation = translation;
        }

        public CameraPose(Point3D translation)
            : this(new double[3, 3] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } }, translation)
        {
        }

        /// <summary>
        /// Transforms a point from local frame to world space: P_w = R * P_l + t.
        /// </summary>
        public Point3D TransformPoint(Point3D p)
        {
            double x = Rotation[0, 0] * p.X + Rotation[0, 1] * p.Y + Rotation[0, 2] * p.Z + Translation.X;
            double y = Rotation[1, 0] * p.X + Rotation[1, 1] * p.Y + Rotation[1, 2] * p.Z + Translation.Y;
            double z = Rotation[2, 0] * p.X + Rotation[2, 1] * p.Y + Rotation[2, 2] * p.Z + Translation.Z;
            return new Point3D(x, y, z);
        }

        /// <summary>
        /// Transforms a point from world space back to local frame: P_l = R^T * (P_w - t).
        /// </summary>
        public Point3D InverseTransformPoint(Point3D p)
        {
            double dx = p.X - Translation.X;
            double dy = p.Y - Translation.Y;
            double dz = p.Z - Translation.Z;

            double x = Rotation[0, 0] * dx + Rotation[1, 0] * dy + Rotation[2, 0] * dz;
            double y = Rotation[0, 1] * dx + Rotation[1, 1] * dy + Rotation[2, 1] * dz;
            double z = Rotation[0, 2] * dx + Rotation[1, 2] * dy + Rotation[2, 2] * dz;
            return new Point3D(x, y, z);
        }

        /// <summary>
        /// Composes this pose with a relative subsequent transformation: T_combined = T_this * T_relative.
        /// </summary>
        public CameraPose Compose(CameraPose relative)
        {
            if (relative == null) throw new ArgumentNullException(nameof(relative));

            double[,] rNew = new double[3, 3];
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    rNew[r, c] = Rotation[r, 0] * relative.Rotation[0, c] +
                                 Rotation[r, 1] * relative.Rotation[1, c] +
                                 Rotation[r, 2] * relative.Rotation[2, c];
                }
            }

            double tx = Rotation[0, 0] * relative.Translation.X + Rotation[0, 1] * relative.Translation.Y + Rotation[0, 2] * relative.Translation.Z + Translation.X;
            double ty = Rotation[1, 0] * relative.Translation.X + Rotation[1, 1] * relative.Translation.Y + Rotation[1, 2] * relative.Translation.Z + Translation.Y;
            double tz = Rotation[2, 0] * relative.Translation.X + Rotation[2, 1] * relative.Translation.Y + Rotation[2, 2] * relative.Translation.Z + Translation.Z;

            return new CameraPose(rNew, new Point3D(tx, ty, tz));
        }

        /// <summary>
        /// Computes the inverse rigid transformation T^-1 = [R^T | -R^T * t].
        /// </summary>
        public CameraPose Inverse()
        {
            double[,] rInv = new double[3, 3];
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    rInv[r, c] = Rotation[c, r]; // Transpose

            double tx = -(rInv[0, 0] * Translation.X + rInv[0, 1] * Translation.Y + rInv[0, 2] * Translation.Z);
            double ty = -(rInv[1, 0] * Translation.X + rInv[1, 1] * Translation.Y + rInv[1, 2] * Translation.Z);
            double tz = -(rInv[2, 0] * Translation.X + rInv[2, 1] * Translation.Y + rInv[2, 2] * Translation.Z);

            return new CameraPose(rInv, new Point3D(tx, ty, tz));
        }

        /// <summary>
        /// Creates a CameraPose from roll (X), pitch (Y), yaw (Z) in radians and translation vector.
        /// </summary>
        public static CameraPose FromEuler(double rollRad, double pitchRad, double yawRad, Point3D translation)
        {
            double cr = Math.Cos(rollRad), sr = Math.Sin(rollRad);
            double cp = Math.Cos(pitchRad), sp = Math.Sin(pitchRad);
            double cy = Math.Cos(yawRad), sy = Math.Sin(yawRad);

            // R = Rz * Ry * Rx
            double[,] r = new double[3, 3];
            r[0, 0] = cy * cp;
            r[0, 1] = cy * sp * sr - sy * cr;
            r[0, 2] = cy * sp * cr + sy * sr;

            r[1, 0] = sy * cp;
            r[1, 1] = sy * sp * sr + cy * cr;
            r[1, 2] = sy * sp * cr - cy * sr;

            r[2, 0] = -sp;
            r[2, 1] = cp * sr;
            r[2, 2] = cp * cr;

            return new CameraPose(r, translation);
        }

        public double[,] ToMatrix4x4()
        {
            return new double[4, 4]
            {
                { Rotation[0, 0], Rotation[0, 1], Rotation[0, 2], Translation.X },
                { Rotation[1, 0], Rotation[1, 1], Rotation[1, 2], Translation.Y },
                { Rotation[2, 0], Rotation[2, 1], Rotation[2, 2], Translation.Z },
                { 0, 0, 0, 1 }
            };
        }

        public bool Equals(CameraPose? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            if (!Translation.Equals(other.Translation)) return false;
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    if (Math.Abs(Rotation[r, c] - other.Rotation[r, c]) > 1e-7)
                        return false;

            return true;
        }

        public override bool Equals(object? obj) => obj is CameraPose other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Translation.GetHashCode();
                hash = (hash * 397) ^ Rotation[0, 0].GetHashCode();
                hash = (hash * 397) ^ Rotation[1, 1].GetHashCode();
                hash = (hash * 397) ^ Rotation[2, 2].GetHashCode();
                return hash;
            }
        }

        public override string ToString() => $"Pos={Translation}";
    }
}
