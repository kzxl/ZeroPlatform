using System;
using Zero3D.Math;
using Zero3D.Materials;

namespace Zero3D.Lighting
{
    /// <summary>
    /// Type of 3D light source.
    /// </summary>
    public enum LightType
    {
        Directional,
        Point,
        Ambient,
        Spot
    }

    /// <summary>
    /// 3D Light source entity supporting Directional, Point, Spot, and Ambient lighting.
    /// </summary>
    public class Light3D
    {
        public string Name { get; set; } = "Light";
        public LightType Type { get; set; } = LightType.Directional;
        public Vec3 Position { get; set; } = Vec3.Zero;
        public Vec3 Direction { get; set; } = new Vec3(-0.5f, -1f, -0.5f).Normalize();
        public ColorRgb Color { get; set; } = ColorRgb.White;
        public float Intensity { get; set; } = 1.0f;
        public float Range { get; set; } = 2000f;
        public float SpotAngleDegrees { get; set; } = 45f;
        public bool IsEnabled { get; set; } = true;

        public static Light3D CreateDirectional(Vec3 direction, ColorRgb color, float intensity = 1.0f)
        {
            return new Light3D
            {
                Type = LightType.Directional,
                Direction = direction.Normalize(),
                Color = color,
                Intensity = intensity
            };
        }

        public static Light3D CreatePoint(Vec3 position, ColorRgb color, float range = 2000f, float intensity = 1.0f)
        {
            return new Light3D
            {
                Type = LightType.Point,
                Position = position,
                Color = color,
                Range = range,
                Intensity = intensity
            };
        }

        public static Light3D CreateAmbient(ColorRgb color, float intensity = 0.2f)
        {
            return new Light3D
            {
                Type = LightType.Ambient,
                Color = color,
                Intensity = intensity
            };
        }

        public static Light3D CreateSpot(Vec3 position, Vec3 direction, ColorRgb color, float spotAngleDeg = 45f, float range = 2000f, float intensity = 1.0f)
        {
            return new Light3D
            {
                Type = LightType.Spot,
                Position = position,
                Direction = direction.Normalize(),
                Color = color,
                SpotAngleDegrees = spotAngleDeg,
                Range = range,
                Intensity = intensity
            };
        }
    }
}
