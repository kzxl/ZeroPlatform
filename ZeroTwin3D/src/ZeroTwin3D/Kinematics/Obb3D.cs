using System;
using Zero3D.Math;

namespace ZeroTwin3D.Kinematics
{
    /// <summary>
    /// Oriented Bounding Box (OBB) supporting arbitrary spatial orientation.
    /// Uses Separating Axis Theorem (SAT) for high-precision collision detection in robotic arm envelopes.
    /// </summary>
    public struct Obb3D
    {
        public Vec3 Center;
        public Vec3 Extents; // Half-widths along local axes
        public Vec3 AxisX;
        public Vec3 AxisY;
        public Vec3 AxisZ;

        public Obb3D(Vec3 center, Vec3 extents, Vec3 axisX, Vec3 axisY, Vec3 axisZ)
        {
            Center = center;
            Extents = extents;
            AxisX = axisX.Normalize();
            AxisY = axisY.Normalize();
            AxisZ = axisZ.Normalize();
        }

        public static Obb3D FromAabb(Aabb3D aabb, Mat4 transform)
        {
            var center = transform.TransformPoint(aabb.Center);
            var extents = aabb.Size * 0.5f;

            var ax = transform.TransformVector(Vec3.UnitX).Normalize();
            var ay = transform.TransformVector(Vec3.UnitY).Normalize();
            var az = transform.TransformVector(Vec3.UnitZ).Normalize();

            return new Obb3D(center, extents, ax, ay, az);
        }

        /// <summary>
        /// Tests intersection between two OBBs using the Separating Axis Theorem (15 candidate axes).
        /// </summary>
        public bool Intersects(Obb3D other)
        {
            var aAxes = new[] { AxisX, AxisY, AxisZ };
            var bAxes = new[] { other.AxisX, other.AxisY, other.AxisZ };

            var d = other.Center - Center;

            // 1. Check A's local axes (3 tests)
            for (int i = 0; i < 3; i++)
            {
                if (GetSeparationOnAxis(aAxes[i], this, other, d)) return false;
            }

            // 2. Check B's local axes (3 tests)
            for (int i = 0; i < 3; i++)
            {
                if (GetSeparationOnAxis(bAxes[i], this, other, d)) return false;
            }

            // 3. Check cross products of pairs of axes (9 tests)
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    var axis = Vec3.Cross(aAxes[i], bAxes[j]);
                    if (axis.LengthSquared() > 1e-6f)
                    {
                        if (GetSeparationOnAxis(axis.Normalize(), this, other, d)) return false;
                    }
                }
            }

            return true;
        }

        public bool Intersects(Aabb3D aabb)
        {
            var obbB = new Obb3D(aabb.Center, aabb.Size * 0.5f, Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ);
            return Intersects(obbB);
        }

        private static bool GetSeparationOnAxis(Vec3 axis, Obb3D a, Obb3D b, Vec3 d)
        {
            float rA = a.Extents.X * System.Math.Abs(Vec3.Dot(a.AxisX, axis)) +
                       a.Extents.Y * System.Math.Abs(Vec3.Dot(a.AxisY, axis)) +
                       a.Extents.Z * System.Math.Abs(Vec3.Dot(a.AxisZ, axis));

            float rB = b.Extents.X * System.Math.Abs(Vec3.Dot(b.AxisX, axis)) +
                       b.Extents.Y * System.Math.Abs(Vec3.Dot(b.AxisY, axis)) +
                       b.Extents.Z * System.Math.Abs(Vec3.Dot(b.AxisZ, axis));

            float dist = System.Math.Abs(Vec3.Dot(d, axis));
            return dist > (rA + rB);
        }
    }
}
