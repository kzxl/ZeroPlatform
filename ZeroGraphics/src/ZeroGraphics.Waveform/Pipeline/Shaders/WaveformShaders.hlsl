cbuffer WaveformConstants : register(b0)
{
    float4 Transform;   // ScaleX, ScaleY, OffsetX, OffsetY
    float4 TraceColor;  // R, G, B, A
    float4 GridColor;   // R, G, B, A
    float4 Viewport;    // Width, Height, PointCount, Padding
};

struct VS_INPUT
{
    float2 Position : POSITION;
};

struct PS_INPUT
{
    float4 Position : SV_POSITION;
    float4 Color    : COLOR0;
};

PS_INPUT VS_Main(VS_INPUT input)
{
    PS_INPUT output;

    // Transform from data coordinates to NDC space [-1, 1]
    float ndcX = input.Position.x * Transform.x + Transform.z;
    float ndcY = input.Position.y * Transform.y + Transform.w;

    output.Position = float4(ndcX, ndcY, 0.0f, 1.0f);
    output.Color = TraceColor;
    return output;
}

float4 PS_Main(PS_INPUT input) : SV_TARGET
{
    return input.Color;
}
