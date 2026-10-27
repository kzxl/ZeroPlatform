using System;

namespace ZeroUI.Core.Rendering.Optimizer
{
    /// <summary>
    /// Describes the geometric and visual parameters of a render operation to be evaluated by <see cref="ZeroRenderAnalyzer"/>.
    /// </summary>
    public readonly struct RenderOperationProfile : IEquatable<RenderOperationProfile>
    {
        public readonly int Width;
        public readonly int Height;
        public readonly float CornerRadius;
        public readonly float Elevation;
        public readonly float BlurRadius;
        public readonly float GlowIntensity;
        public readonly RenderEffectKind Effects;
        public readonly bool IsAnimated;
        public readonly int BatchCount;

        public int Area => Width * Height;

        public RenderOperationProfile(
            int width,
            int height,
            RenderEffectKind effects,
            float cornerRadius = 0f,
            float elevation = 0f,
            float blurRadius = 0f,
            float glowIntensity = 0f,
            bool isAnimated = false,
            int batchCount = 1)
        {
            Width = Math.Max(0, width);
            Height = Math.Max(0, height);
            Effects = effects;
            CornerRadius = Math.Max(0f, cornerRadius);
            Elevation = Math.Max(0f, elevation);
            BlurRadius = Math.Max(0f, blurRadius);
            GlowIntensity = Math.Max(0f, glowIntensity);
            IsAnimated = isAnimated;
            BatchCount = Math.Max(1, batchCount);
        }

        public static RenderOperationProfile ForText(int width, int height) =>
            new RenderOperationProfile(width, height, RenderEffectKind.Text);

        public static RenderOperationProfile ForFlatButton(int width, int height, float cornerRadius = 4f) =>
            new RenderOperationProfile(width, height, RenderEffectKind.FlatFill | RenderEffectKind.Border | RenderEffectKind.Text, cornerRadius: cornerRadius);

        public static RenderOperationProfile ForCard(
            int width,
            int height,
            float elevation,
            float cornerRadius = 8f,
            float blurRadius = 0f,
            float glowIntensity = 0f,
            bool hasText = true,
            bool isAnimated = false,
            int batchCount = 1)
        {
            var effects = RenderEffectKind.FlatFill | RenderEffectKind.Border;
            if (elevation > 0f) effects |= RenderEffectKind.DropShadow;
            if (blurRadius > 0f) effects |= RenderEffectKind.GaussianBlur;
            if (glowIntensity > 0f) effects |= RenderEffectKind.NeonGlow;
            if (hasText) effects |= RenderEffectKind.Text;

            return new RenderOperationProfile(
                width,
                height,
                effects,
                cornerRadius: cornerRadius,
                elevation: elevation,
                blurRadius: blurRadius,
                glowIntensity: glowIntensity,
                isAnimated: isAnimated,
                batchCount: batchCount);
        }

        public bool Equals(RenderOperationProfile other)
        {
            return Width == other.Width &&
                   Height == other.Height &&
                   CornerRadius.Equals(other.CornerRadius) &&
                   Elevation.Equals(other.Elevation) &&
                   BlurRadius.Equals(other.BlurRadius) &&
                   GlowIntensity.Equals(other.GlowIntensity) &&
                   Effects == other.Effects &&
                   IsAnimated == other.IsAnimated &&
                   BatchCount == other.BatchCount;
        }

        public override bool Equals(object? obj) => obj is RenderOperationProfile other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Width;
                hash = (hash * 397) ^ Height;
                hash = (hash * 397) ^ CornerRadius.GetHashCode();
                hash = (hash * 397) ^ Elevation.GetHashCode();
                hash = (hash * 397) ^ BlurRadius.GetHashCode();
                hash = (hash * 397) ^ GlowIntensity.GetHashCode();
                hash = (hash * 397) ^ (int)Effects;
                hash = (hash * 397) ^ IsAnimated.GetHashCode();
                hash = (hash * 397) ^ BatchCount;
                return hash;
            }
        }

        public static bool operator ==(RenderOperationProfile left, RenderOperationProfile right) => left.Equals(right);
        public static bool operator !=(RenderOperationProfile left, RenderOperationProfile right) => !left.Equals(right);
    }
}
