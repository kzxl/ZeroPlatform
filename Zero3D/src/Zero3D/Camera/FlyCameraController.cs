using System;
using Zero3D.Math;

namespace Zero3D.Camera
{
    /// <summary>
    /// Free-flight / First-Person Camera Controller (WASD + Mouse look) suitable for
    /// architectural walkthroughs, BIM flythrough, drone simulations, and immersive navigation.
    /// </summary>
    public class FlyCameraController
    {
        public Vec3 Position { get; set; } = new Vec3(0f, 100f, 200f);
        public float YawDegrees { get; set; } = 0f;
        public float PitchDegrees { get; set; } = 0f;
        public float MoveSpeed { get; set; } = 300f; // units per second
        public float TurnSensitivity { get; set; } = 0.2f;

        public FlyCameraController() { }

        public FlyCameraController(Vec3 startPosition, float yawDeg = 0f, float pitchDeg = 0f)
        {
            Position = startPosition;
            YawDegrees = yawDeg;
            PitchDegrees = pitchDeg;
        }

        public Vec3 Forward
        {
            get
            {
                double yawRad = YawDegrees * (System.Math.PI / 180.0);
                double pitchRad = PitchDegrees * (System.Math.PI / 180.0);

                float x = (float)(System.Math.Cos(pitchRad) * System.Math.Sin(yawRad));
                float y = (float)System.Math.Sin(pitchRad);
                float z = -(float)(System.Math.Cos(pitchRad) * System.Math.Cos(yawRad));
                return new Vec3(x, y, z).Normalize();
            }
        }

        public Vec3 Right => Vec3.Cross(Forward, Vec3.UnitY).Normalize();
        public Vec3 Up => Vec3.Cross(Right, Forward).Normalize();

        public void Look(float deltaX, float deltaY)
        {
            YawDegrees = (YawDegrees + deltaX * TurnSensitivity) % 360f;
            PitchDegrees = System.Math.Max(-89f, System.Math.Min(89f, PitchDegrees - deltaY * TurnSensitivity));
        }

        public void Move(float forwardDelta, float rightDelta, float upDelta, float deltaTimeSeconds)
        {
            float speed = MoveSpeed * deltaTimeSeconds;
            Position = Position + (Forward * (forwardDelta * speed)) + (Right * (rightDelta * speed)) + (Vec3.UnitY * (upDelta * speed));
        }

        public void UpdateCamera(Camera3D camera)
        {
            camera.Position = Position;
            camera.Target = Position + Forward;
            camera.Up = Vec3.UnitY;
        }
    }
}
