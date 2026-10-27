using System;

namespace ZeroTwin3D.Lighting
{
    /// <summary>
    /// Material surface definition specifying visual shading, specular reflection, and industrial presets.
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

        #region Industrial Material Presets

        public static Material3D Default => new Material3D
        {
            Name = "Default",
            Albedo = new ColorRgb(0.75f, 0.75f, 0.75f),
            Roughness = 0.6f,
            Metallic = 0.0f
        };

        public static Material3D IndustrialSteel => new Material3D
        {
            Name = "IndustrialSteel",
            Albedo = new ColorRgb(0.6f, 0.62f, 0.65f),
            Roughness = 0.25f,
            Metallic = 0.85f,
            SpecularIntensity = 0.9f,
            SpecularShininess = 64f
        };

        public static Material3D RobotOrange => new Material3D
        {
            Name = "RobotOrange",
            Albedo = new ColorRgb(0.95f, 0.45f, 0.05f),
            Roughness = 0.35f,
            Metallic = 0.1f,
            SpecularIntensity = 0.6f,
            SpecularShininess = 40f
        };

        public static Material3D SafetyYellow => new Material3D
        {
            Name = "SafetyYellow",
            Albedo = new ColorRgb(0.98f, 0.82f, 0.08f),
            Roughness = 0.4f,
            Metallic = 0.0f
        };

        public static Material3D SafetyRed => new Material3D
        {
            Name = "SafetyRed",
            Albedo = new ColorRgb(0.9f, 0.1f, 0.1f),
            Roughness = 0.3f,
            Metallic = 0.0f
        };

        public static Material3D ConveyorBelt => new Material3D
        {
            Name = "ConveyorBelt",
            Albedo = new ColorRgb(0.12f, 0.12f, 0.12f),
            Roughness = 0.85f,
            Metallic = 0.0f
        };

        public static Material3D Glass => new Material3D
        {
            Name = "Glass",
            Albedo = new ColorRgb(0.6f, 0.85f, 0.95f),
            Alpha = 0.35f,
            Roughness = 0.1f,
            Metallic = 0.1f,
            SpecularIntensity = 1.0f,
            SpecularShininess = 90f
        };

        #endregion
    }
}
