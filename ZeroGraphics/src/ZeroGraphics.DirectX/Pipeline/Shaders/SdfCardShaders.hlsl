cbuffer SdfCardBuffer : register(b0)
{
    float4 CardRect;       // x, y, width, height (in viewport pixels)
    float4 CardColor;      // r, g, b, a (fill)
    float4 BorderColor;    // r, g, b, a (border)
    float4 ShadowColor;    // r, g, b, a (shadow)
    float4 GlowColor;      // r, g, b, a (glow)
    float4 Params1;        // x: CornerRadius, y: BorderWidth, z: BlurRadius, w: Elevation
    float4 Params2;        // x: GlowIntensity, y: ViewportWidth, z: ViewportHeight, w: Padding
};

struct VS_INPUT
{
    float2 Position : POSITION; // Screen position in NDC [-1, 1]
    float2 TexCoord : TEXCOORD0; // UV [0, 1]
};

struct PS_INPUT
{
    float4 Position : SV_POSITION;
    float2 PixelPos : TEXCOORD0; // Pixel coordinate in viewport space
};

PS_INPUT VS_Main(VS_INPUT input)
{
    PS_INPUT output;
    output.Position = float4(input.Position, 0.0f, 1.0f);
    // Convert NDC [-1, 1] to Viewport pixel coordinates [0, Width] x [0, Height]
    output.PixelPos.x = (input.Position.x * 0.5f + 0.5f) * Params2.y;
    output.PixelPos.y = (0.5f - input.Position.y * 0.5f) * Params2.z;
    return output;
}

// Analytical Signed Distance Field for Rounded Rectangle
float EvaluateBoxSdf(float2 p, float2 halfSize, float radius)
{
    float2 b = halfSize - float2(radius, radius);
    float2 q = abs(p) - b;
    return length(max(q, 0.0f)) + min(max(q.x, q.y), 0.0f) - radius;
}

float4 PS_Main(PS_INPUT input) : SV_TARGET
{
    float cornerRadius = Params1.x;
    float borderWidth  = Params1.y;
    float blurRadius   = Params1.z;
    float elevation    = Params1.w;
    float glowIntensity = Params2.x;

    float2 cardPos   = CardRect.xy;
    float2 cardSize  = CardRect.zw;
    float2 halfSize  = cardSize * 0.5f;
    float2 cardCenter = cardPos + halfSize;

    // 1. Distance for Card Body & Border
    float2 pCard = input.PixelPos - cardCenter;
    float dCard = EvaluateBoxSdf(pCard, halfSize, cornerRadius);

    // 2. Anti-aliased Card Fill
    float fillAlpha = saturate(0.5f - dCard);

    // 3. Anti-aliased Card Border
    float borderDist = abs(dCard + borderWidth * 0.5f) - borderWidth * 0.5f;
    float borderAlpha = saturate(0.5f - borderDist) * (borderWidth > 0.0f ? 1.0f : 0.0f);

    // 4. Analytical Shadow (Offset by Elevation along Y axis)
    float2 shadowCenter = cardCenter + float2(0.0f, elevation);
    float2 pShadow = input.PixelPos - shadowCenter;
    float dShadow = EvaluateBoxSdf(pShadow, halfSize, cornerRadius);
    float shadowAlpha = 0.0f;
    if (blurRadius > 0.001f)
    {
        shadowAlpha = saturate(0.5f - (dShadow / blurRadius)) * ShadowColor.a;
    }

    // 5. Radial Neon Glow Falloff
    float glowAlpha = 0.0f;
    if (glowIntensity > 0.001f && blurRadius > 0.001f)
    {
        float normDist = max(0.0f, dCard) / (blurRadius * 2.0f);
        glowAlpha = exp(-pow(abs(normDist), 1.35f)) * glowIntensity * GlowColor.a;
    }

    // 6. Composition
    float4 shadowRgba = float4(ShadowColor.rgb, shadowAlpha);
    float4 glowRgba = float4(GlowColor.rgb, glowAlpha);
    float4 bodyRgba = lerp(shadowRgba, glowRgba, glowAlpha);

    // Blend card background over shadow/glow
    float4 cardFillRgba = float4(CardColor.rgb, CardColor.a * fillAlpha);
    float3 blendedRgb = lerp(bodyRgba.rgb, cardFillRgba.rgb, fillAlpha * CardColor.a);
    float blendedAlpha = max(bodyRgba.a, fillAlpha * CardColor.a);

    // Blend border over card
    if (borderWidth > 0.0f)
    {
        blendedRgb = lerp(blendedRgb, BorderColor.rgb, borderAlpha * BorderColor.a);
        blendedAlpha = max(blendedAlpha, borderAlpha * BorderColor.a);
    }

    return float4(blendedRgb, blendedAlpha);
}
