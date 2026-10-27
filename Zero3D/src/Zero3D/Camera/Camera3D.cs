using System;
using Zero3D.Math;

namespace Zero3D.Camera
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
    /// General-purpose 3D Spatial Camera supporting perspective &amp; orthographic projection,
    /// view-frustum culling, and ray unprojection for picking/raycasting.
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
            get => FovYDegrees * (float)(System.Math.PI / 180.0);
            set => FovYDegrees = value * (float)(180.0 / System.Math.PI);
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
        /// Unprojects a 2D screen coordinate into a 3D world-space Ray for mouse picking and interaction.
        /// </summary>
        public Ray3D ScreenPointToRay(float screenX, float screenY, float viewportWidth, float viewportHeight)
        {
            if (viewportWidth <= 0 || viewportHeight <= 0) return new Ray3D(Position, Forward);

            // Normalized device coordinates [-1, 1]
            float ndcX = (2f * screenX / viewportWidth) - 1f;
            float ndcY = 1f - (2f * screenY / viewportHeight); // Invert Y for screen space

            if (Projection == ProjectionMode.Perspective)
            {
                float tanHalfFov = (float)System.Math.Tan(FovYRadians * 0.5f);
                float rayX = ndcX * tanHalfFov * AspectRatio;
                float rayY = ndcY * tanHalfFov;
                Vec3 rayDirLocal = (Right * rayX + Up * rayY + Forward).Normalize();
                return new Ray3D(Position, rayDirLocal);
            }
            else
            {
                float halfHeight = OrthographicSize * 0.5f;
                float halfWidth = halfHeight * AspectRatio;
                Vec3 origin = Position + (Right * (ndcX * halfWidth)) + (Up * (ndcY * halfHeight));
                return new Ray3D(origin, Forward);
            }
        }

        /// <summary>
        /// Extracts the 6 view-frustum planes in world space (Left, Right, Bottom, Top, Near, Far).
        /// </summary>
        public Plane3D[] GetFrustumPlanes()
        {
            var vp = GetViewProjectionMatrix();
            var planes = new Plane3D[6];

            planes[0] = new Plane3D(vp.M14 + vp.M11, vp.M24 + vp.M21, vp.M34 + vp.M31, vp.M44 + vp.M41).Normalize();
            planes[1] = new Plane3D(vp.M14 - vp.M11, vp.M24 - vp.M21, vp.M34 - vp.M31, vp.M44 - vp.M41).Normalize();
            planes[2] = new Plane3D(vp.M14 + vp.M12, vp.M24 + vp.M22, vp.M34 + vp.M32, vp.M44 + vp.M42).Normalize();
            planes[3] = new Plane3D(vp.M14 - vp.M12, vp.M24 - vp.M22, vp.M34 - vp.M32, vp.M44 - vp.M42).Normalize();
            planes[4] = new Plane3D(vp.M14 + vp.M13, vp.M24 + vp.M23, vp.M34 + vp.M33, vp.M44 + vp.M43).Normalize();
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
