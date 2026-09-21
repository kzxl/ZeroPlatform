using System;
using Zero3D.Math;

namespace Zero3D.Camera
{
    /// <summary>
    /// Arcball / Orbit Camera Controller for intuitive 3D model examination, CAD/BIM inspection,
    /// and simulation tracking. Supports smooth orbit rotation, distance zooming, and screen-aligned panning.
    /// </summary>
    public class OrbitCameraController
    {
        public Vec3 Target { get; set; } = Vec3.Zero;
        public float Distance { get; set; } = 1000f;
        public float MinDistance { get; set; } = 10f;
        public float MaxDistance { get; set; } = 50000f;

        public float YawDegrees { get; set; } = 45f;
        public float PitchDegrees { get; set; } = 30f;
        public float MinPitch { get; set; } = -89f;
        public float MaxPitch { get; set; } = 89f;

        public OrbitCameraController() { }

        public OrbitCameraController(Vec3 target, float distance, float yawDegrees = 45f, float pitchDegrees = 30f)
        {
            Target = target;
            Distance = distance;
            YawDegrees = yawDegrees;
            PitchDegrees = pitchDegrees;
        }

        /// <summary>
        /// Rotates the orbit angles by specified degrees. Pitch is clamped to prevent gimbal inversion.
        /// </summary>
        public void Rotate(float deltaYawDeg, float deltaPitchDeg)
        {
            YawDegrees = (YawDegrees + deltaYawDeg) % 360f;
            PitchDegrees = System.Math.Max(MinPitch, System.Math.Min(MaxPitch, PitchDegrees + deltaPitchDeg));
        }

        /// <summary>
        /// Zooms in or out by adjusting distance from target.
        /// </summary>
        public void Zoom(float deltaDistance)
        {
            Distance = System.Math.Max(MinDistance, System.Math.Min(MaxDistance, Distance + deltaDistance));
        }

        /// <summary>
        /// Pans the camera target along the camera's local viewport coordinate plane.
        /// </summary>
        public void Pan(float deltaScreenX, float deltaScreenY, Camera3D camera)
        {
            var right = camera.Right;
            var up = camera.Up;

            float panFactor = Distance * 0.0015f;
            Target = Target + (right * (deltaScreenX * panFactor)) + (up * (deltaScreenY * panFactor));
        }

        /// <summary>
        /// Computes the 3D position in spherical coordinates relative to the Target.
        /// </summary>
        public Vec3 CalculatePosition()
        {
            double yawRad = YawDegrees * (System.Math.PI / 180.0);
            double pitchRad = PitchDegrees * (System.Math.PI / 180.0);

            float x = Target.X + Distance * (float)(System.Math.Cos(pitchRad) * System.Math.Sin(yawRad));
            float y = Target.Y + Distance * (float)System.Math.Sin(pitchRad);
            float z = Target.Z + Distance * (float)(System.Math.Cos(pitchRad) * System.Math.Cos(yawRad));

            return new Vec3(x, y, z);
        }

        /// <summary>
        /// Applies current orbit state to the target Camera3D.
        /// </summary>
        public void UpdateCamera(Camera3D camera)
        {
            camera.Target = Target;
            camera.Position = CalculatePosition();
            camera.Up = Vec3.UnitY;
        }
    }
}
