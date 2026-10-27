using System;
using ZeroTwin3D.Engine;

namespace ZeroTwin3D.Camera
{
    /// <summary>
    /// Projection mode for the 3D camera.
    /// </summary>
    public enum ProjectionMode
    {
        Perspective,
        Orthographic
    }

    /// <summary>
    /// 3D Spatial Camera for Digital Twin viewport, matrix calculations, frustum culling, and ray casting.
    /// </summary>
    public class Camera3D
    {
        public Vec3 Position { get; set; } = new Vec3(0f, 500f, 1000f);
        public Vec3 Target { get; set; } = new Vec3(0f, 0f, 0f);
        public Vec3 Up { get; set; } = Vec3.UnitY;

        public float FovYDegrees { get; set; } = 45f;
        public float AspectRatio { get; set; } = 16f / 9f;
        public float NearPlane { get; set; } = 1f;
        public float FarPlane { get; set; } = 10000f;
        public float OrthographicSize { get; set; } = 600f;
        public ProjectionMode Projection { get; set; } = ProjectionMode.Perspective;

        public float FovYRadians
        {
            get => FovYDegrees * (float)(Math.PI / 180.0);
            set => FovYDegrees = value * (float)(180.0 / Math.PI);
        }

        public Vec3 Forward => (Target - Position).Normalize();
        public Vec3 Right => Vec3.Cross(Forward, Up).Normalize();

        public Mat4 GetViewMatrix()
        {
            return Mat4.CreateLookAt(Position, Target, Up);
        }

        public Mat4 GetProjectionMatrix()
        {
            if (Projection == ProjectionMode.Perspective)
            {
                return Mat4.CreatePerspectiveFieldOfView(FovYRadians, AspectRatio, NearPlane, FarPlane);
            }
            else
            {
                float width = OrthographicSize * AspectRatio;
                return Mat4.CreateOrthographic(width, OrthographicSize, NearPlane, FarPlane);
            }
        }

        public Mat4 GetViewProjectionMatrix()
        {
            return GetViewMatrix() * GetProjectionMatrix();
        }

        /// <summary>
        /// Extracts the 6 view-frustum planes in world space (Left, Right, Bottom, Top, Near, Far).
        /// </summary>
        public Plane3D[] GetFrustumPlanes()
        {
            var vp = GetViewProjectionMatrix();
            var planes = new Plane3D[6];

            // In row-vector convention: [x y z 1] * M
            // Left:   col4 + col1
            planes[0] = new Plane3D(vp.M14 + vp.M11, vp.M24 + vp.M21, vp.M34 + vp.M31, vp.M44 + vp.M41).Normalize();
            // Right:  col4 - col1
            planes[1] = new Plane3D(vp.M14 - vp.M11, vp.M24 - vp.M21, vp.M34 - vp.M31, vp.M44 - vp.M41).Normalize();
            // Bottom: col4 + col2
            planes[2] = new Plane3D(vp.M14 + vp.M12, vp.M24 + vp.M22, vp.M34 + vp.M32, vp.M44 + vp.M42).Normalize();
            // Top:    col4 - col2
            planes[3] = new Plane3D(vp.M14 - vp.M12, vp.M24 - vp.M22, vp.M34 - vp.M32, vp.M44 - vp.M42).Normalize();
            // Near:   col4 + col3
            planes[4] = new Plane3D(vp.M14 + vp.M13, vp.M24 + vp.M23, vp.M34 + vp.M33, vp.M44 + vp.M43).Normalize();
            // Far:    col4 - col3
            planes[5] = new Plane3D(vp.M14 - vp.M13, vp.M24 - vp.M23, vp.M34 - vp.M33, vp.M44 - vp.M43).Normalize();

            return planes;
        }

        /// <summary>
        /// Tests if a bounding box (AABB) intersects or is inside the camera view frustum.
        /// </summary>
        public bool IsInFrustum(Aabb3D box)
        {
            var planes = GetFrustumPlanes();
            for (int i = 0; i < 6; i++)
            {
                var p = planes[i];
                // Find positive corner along plane normal
                var px = p.Normal.X >= 0 ? box.Max.X : box.Min.X;
                var py = p.Normal.Y >= 0 ? box.Max.Y : box.Min.Y;
                var pz = p.Normal.Z >= 0 ? box.Max.Z : box.Min.Z;

                if (p.DistanceToPoint(new Vec3(px, py, pz)) < 0)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
