using System;

namespace Zero3D.Materials
{
    /// <summary>
    /// Linear RGB color representation for 3D lighting, textures, and shading.
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
        public static ColorRgb Cyan => new ColorRgb(0f, 1f, 1f);
        public static ColorRgb Magenta => new ColorRgb(1f, 0f, 1f);
        public static ColorRgb Gray => new ColorRgb(0.5f, 0.5f, 0.5f);

        public static ColorRgb operator +(ColorRgb a, ColorRgb b) => new ColorRgb(a.R + b.R, a.G + b.G, a.B + b.B);
        public static ColorRgb operator *(ColorRgb a, float s) => new ColorRgb(a.R * s, a.G * s, a.B * s);
        public static ColorRgb operator *(ColorRgb a, ColorRgb b) => new ColorRgb(a.R * b.R, a.G * b.G, a.B * b.B);

        public ColorRgb Clamp()
        {
            return new ColorRgb(
                System.Math.Max(0f, System.Math.Min(1f, R)),
                System.Math.Max(0f, System.Math.Min(1f, G)),
                System.Math.Max(0f, System.Math.Min(1f, B))
            );
        }

        public override string ToString() => $"RGB({R:F2}, {G:F2}, {B:F2})";
    }

    /// <summary>
    /// Standard 3D surface material definition specifying visual shading, specular reflection, and standard presets.
    /// </summary>
    public class Material3D
    {
        public string Name { get; set; } = "DefaultMaterial";
        public ColorRgb Albedo { get; set; } = new ColorRgb(0.75f, 0.75f, 0.75f);
        public float Alpha { get; set; } = 1.0f;
        public float Roughness { get; set; } = 0.5f;
        public float Metallic { get; set; } = 0.0f;
        public ColorRgb Emissive { get; set; } = ColorRgb.Black;
        public float SpecularIntensity { get; set; } = 0.5f;
        public float SpecularShininess { get; set; } = 32f;
        public bool Wireframe { get; set; } = false;
        public bool DoubleSided { get; set; } = false;

        public Material3D() { }

        public Material3D(ColorRgb albedo, float roughness = 0.5f, float metallic = 0.0f)
        {
            Albedo = albedo;
            Roughness = roughness;
            Metallic = metallic;
        }

        #region Standard Material Presets

        public static Material3D Default => new Material3D
        {
            Name = "Default",
            Albedo = new ColorRgb(0.75f, 0.75f, 0.75f),
            Roughness = 0.6f,
            Metallic = 0.0f
        };

        public static Material3D Steel => new Material3D
        {
            Name = "Steel",
            Albedo = new ColorRgb(0.6f, 0.62f, 0.65f),
            Roughness = 0.25f,
            Metallic = 0.85f,
            SpecularIntensity = 0.9f,
            SpecularShininess = 64f
        };

        public static Material3D Gold => new Material3D
        {
            Name = "Gold",
            Albedo = new ColorRgb(1.0f, 0.76f, 0.33f),
            Roughness = 0.2f,
            Metallic = 0.95f,
            SpecularIntensity = 1.0f,
            SpecularShininess = 80f
        };

        public static Material3D PlasticRed => new Material3D
        {
            Name = "PlasticRed",
            Albedo = new ColorRgb(0.9f, 0.1f, 0.1f),
            Roughness = 0.3f,
            Metallic = 0.0f,
            SpecularIntensity = 0.5f
        };

        public static Material3D PlasticBlue => new Material3D
        {
            Name = "PlasticBlue",
            Albedo = new ColorRgb(0.1f, 0.3f, 0.9f),
            Roughness = 0.3f,
            Metallic = 0.0f,
            SpecularIntensity = 0.5f
        };

        public static Material3D Glass => new Material3D
        {
            Name = "Glass",
            Albedo = new ColorRgb(0.7f, 0.85f, 0.95f),
            Alpha = 0.35f,
            Roughness = 0.1f,
            Metallic = 0.1f,
            SpecularIntensity = 1.0f,
            SpecularShininess = 90f
        };

        #endregion
    }

    /// <summary>
    /// Physically-Based Rendering (PBR) Metallic-Roughness material definition.
    /// Compatible with glTF 2.0 PBR workflow.
    /// </summary>
    public class PbrMaterial : Material3D
    {
        public ColorRgb BaseColorFactor { get => Albedo; set => Albedo = value; }
        public float MetallicFactor { get => Metallic; set => Metallic = value; }
        public float RoughnessFactor { get => Roughness; set => Roughness = value; }
        public string? BaseColorTextureUri { get; set; }
        public string? MetallicRoughnessTextureUri { get; set; }
        public string? NormalTextureUri { get; set; }
        public string? OcclusionTextureUri { get; set; }
        public string? EmissiveTextureUri { get; set; }

        public PbrMaterial()
        {
            Name = "PbrMaterial";
        }
    }
}
