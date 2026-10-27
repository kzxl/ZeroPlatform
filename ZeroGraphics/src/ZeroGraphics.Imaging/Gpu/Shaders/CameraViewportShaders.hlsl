// =========================================================================
// ZeroGraphics Camera Viewport Pipeline Shaders (Direct3D 11 / Shader Model 4.0)
// High-performance camera video stream rendering, R8_UNORM sampling, and inspection overlays
// =========================================================================

cbuffer ViewportBuffer : register(b0) {
    float2 ViewportSize;
    float2 Padding;
};

struct VS_INPUT {
    float2 Pos : POSITION;
    float2 Tex : TEXCOORD0;
};

struct VS_OUTPUT {
    float4 Pos : SV_POSITION;
    float2 Tex : TEXCOORD0;
};

VS_OUTPUT VS_Main(VS_INPUT input) {
    VS_OUTPUT output;
    float ndcX = (input.Pos.x / ViewportSize.x) * 2.0f - 1.0f;
    float ndcY = 1.0f - (input.Pos.y / ViewportSize.y) * 2.0f;
    output.Pos = float4(ndcX, ndcY, 0.0f, 1.0f);
    output.Tex = input.Tex;
    return output;
}

Texture2D camTexture : register(t0);
SamplerState defaultSampler : register(s0);

// Monochrome / Gray8 hardware sampling (R8_UNORM -> RGB Grayscale)
float4 PS_R8(VS_OUTPUT input) : SV_TARGET {
    float g = camTexture.Sample(defaultSampler, input.Tex).r;
    return float4(g, g, g, 1.0f);
}

// BGRA32 / Color hardware sampling
float4 PS_Color(VS_OUTPUT input) : SV_TARGET {
    return camTexture.Sample(defaultSampler, input.Tex);
}

// Overlay Bounding Box / ROI wireframe flat color
cbuffer ColorBuffer : register(b1) {
    float4 BoxColor;
};

float4 PS_FlatColor(VS_OUTPUT input) : SV_TARGET {
    return BoxColor;
}
