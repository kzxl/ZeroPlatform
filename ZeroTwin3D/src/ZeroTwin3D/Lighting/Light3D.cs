using System;
using ZeroTwin3D.Engine;

namespace ZeroTwin3D.Lighting
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
    /// Linear RGB color representation for 3D lighting and material shading.
    /// </summary>
    public struct ColorRgb
    {
        public float R;
        public float G;
        public float B;

        public ColorRgb(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        public static ColorRgb Black => new ColorRgb(0f, 0f, 0f);
        public static ColorRgb White => new ColorRgb(1f, 1f, 1f);
        public static ColorRgb Red => new ColorRgb(1f, 0f, 0f);
        public static ColorRgb Green => new ColorRgb(0f, 1f, 0f);
        public static ColorRgb Blue => new ColorRgb(0f, 0f, 1f);
        public static ColorRgb Yellow => new ColorRgb(1f, 1f, 0f);

        public static ColorRgb operator +(ColorRgb a, ColorRgb b) => new ColorRgb(a.R + b.R, a.G + b.G, a.B + b.B);
        public static ColorRgb operator *(ColorRgb a, float s) => new ColorRgb(a.R * s, a.G * s, a.B * s);
        public static ColorRgb operator *(ColorRgb a, ColorRgb b) => new ColorRgb(a.R * b.R, a.G * b.G, a.B * b.B);

        public ColorRgb Clamp()
        {
            return new ColorRgb(
                Math.Max(0f, Math.Min(1f, R)),
                Math.Max(0f, Math.Min(1f, G)),
                Math.Max(0f, Math.Min(1f, B))
            );
        }
    }

    /// <summary>
    /// 3D Light source entity for illuminated Digital Twin scene presentation.
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
    }
}
