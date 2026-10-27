// =========================================================================
// ZeroGraphics DirectCompute 5.0 Image Kernels
// High-performance compute shaders with LDS shared memory caching
// =========================================================================

// -------------------------------------------------------------------------
// 1. Color Adjust & Fused Point Operations (CS_ColorAdjust)
// -------------------------------------------------------------------------
Texture2D<float4> ColorInput : register(t0);
RWTexture2D<float4> ColorOutput : register(u0);

cbuffer ColorComputeBuffer : register(b0)
{
    float Brightness;   // -1.0 to 1.0 (default 0.0)
    float Contrast;     // 0.0 to 3.0 (default 1.0)
    float Grayscale;    // 1.0 = true, 0.0 = false
    float Invert;       // 1.0 = true, 0.0 = false
    float Gamma;        // default 1.0
    float Threshold;    // < 0 = disabled, >= 0 = binary cutoff
    float2 Padding;
};

[numthreads(16, 16, 1)]
void CS_ColorAdjust(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    uint width, height;
    ColorOutput.GetDimensions(width, height);
    if (dispatchThreadId.x >= width || dispatchThreadId.y >= height)
        return;

    float4 color = ColorInput.Load(int3(dispatchThreadId.xy, 0));

    if (Grayscale > 0.5f)
    {
        float gray = dot(color.rgb, float3(0.2126f, 0.7152f, 0.0722f));
        color.rgb = float3(gray, gray, gray);
    }

    color.rgb = (color.rgb - 0.5f) * Contrast + 0.5f + Brightness;

    if (Invert > 0.5f)
    {
        color.rgb = 1.0f - color.rgb;
    }

    if (Gamma > 0.01f && abs(Gamma - 1.0f) > 0.001f)
    {
        color.rgb = pow(max(color.rgb, 0.00001f), 1.0f / Gamma);
    }

    if (Threshold >= 0.0f)
    {
        float luma = dot(color.rgb, float3(0.2126f, 0.7152f, 0.0722f));
        float bin = luma >= Threshold ? 1.0f : 0.0f;
        color.rgb = float3(bin, bin, bin);
    }

    ColorOutput[dispatchThreadId.xy] = saturate(color);
}

// -------------------------------------------------------------------------
// 2. Separable Gaussian Blur (Horizontal Pass with LDS)
// -------------------------------------------------------------------------
Texture2D<float4> BlurInputH : register(t0);
RWTexture2D<float4> BlurOutputH : register(u0);

cbuffer BlurParamsH : register(b0)
{
    uint ImageWidthH;
    uint ImageHeightH;
    int BlurRadiusH;     // 1 to 7
    float BlurPadH;
    float4 BlurWeightsH[2]; // Packed weights: [0]=w0,w1,w2,w3, [1]=w4,w5,w6,w7
};

#define BLUR_BLOCK_X 128
#define MAX_RADIUS 7
#define LDS_SIZE_X (BLUR_BLOCK_X + 2 * MAX_RADIUS)

groupshared float4 s_Row[LDS_SIZE_X];

[numthreads(BLUR_BLOCK_X, 1, 1)]
void CS_BlurHorizontal(uint3 groupThreadId : SV_GroupThreadID,
                       uint3 groupId : SV_GroupID)
{
    int tid = (int)groupThreadId.x;
    int baseX = (int)groupId.x * BLUR_BLOCK_X;
    int y = (int)groupId.y;

    if (y >= (int)ImageHeightH) return;

    int radius = clamp(BlurRadiusH, 1, MAX_RADIUS);

    // 1. Center element
    int globalX = baseX + tid;
    s_Row[tid + radius] = BlurInputH.Load(int3(clamp(globalX, 0, (int)ImageWidthH - 1), y, 0));

    // 2. Left apron
    if (tid < radius)
    {
        int leftX = baseX - radius + tid;
        s_Row[tid] = BlurInputH.Load(int3(clamp(leftX, 0, (int)ImageWidthH - 1), y, 0));
    }

    // 3. Right apron
    if (tid < radius)
    {
        int rightX = baseX + BLUR_BLOCK_X + tid;
        s_Row[BLUR_BLOCK_X + radius + tid] = BlurInputH.Load(int3(clamp(rightX, 0, (int)ImageWidthH - 1), y, 0));
    }

    GroupMemoryBarrierWithGroupSync();

    if (globalX < (int)ImageWidthH)
    {
        float w[8];
        w[0] = BlurWeightsH[0].x;
        w[1] = BlurWeightsH[0].y;
        w[2] = BlurWeightsH[0].z;
        w[3] = BlurWeightsH[0].w;
        w[4] = BlurWeightsH[1].x;
        w[5] = BlurWeightsH[1].y;
        w[6] = BlurWeightsH[1].z;
        w[7] = BlurWeightsH[1].w;

        int centerIdx = tid + radius;
        float4 sum = s_Row[centerIdx] * w[0];
        [unroll]
        for (int k = 1; k <= MAX_RADIUS; k++)
        {
            if (k <= radius)
            {
                sum += (s_Row[centerIdx - k] + s_Row[centerIdx + k]) * w[k];
            }
        }
        BlurOutputH[int2(globalX, y)] = sum;
    }
}

// -------------------------------------------------------------------------
// 3. Separable Gaussian Blur (Vertical Pass with LDS)
// -------------------------------------------------------------------------
Texture2D<float4> BlurInputV : register(t0);
RWTexture2D<float4> BlurOutputV : register(u0);

cbuffer BlurParamsV : register(b0)
{
    uint ImageWidthV;
    uint ImageHeightV;
    int BlurRadiusV;     // 1 to 7
    float BlurPadV;
    float4 BlurWeightsV[2]; // Packed weights: [0]=w0,w1,w2,w3, [1]=w4,w5,w6,w7
};

#define BLUR_BLOCK_Y 128
#define LDS_SIZE_Y (BLUR_BLOCK_Y + 2 * MAX_RADIUS)

groupshared float4 s_Col[LDS_SIZE_Y];

[numthreads(1, BLUR_BLOCK_Y, 1)]
void CS_BlurVertical(uint3 groupThreadId : SV_GroupThreadID,
                     uint3 groupId : SV_GroupID)
{
    int tid = (int)groupThreadId.y;
    int x = (int)groupId.x;
    int baseY = (int)groupId.y * BLUR_BLOCK_Y;

    if (x >= (int)ImageWidthV) return;

    int radius = clamp(BlurRadiusV, 1, MAX_RADIUS);

    // 1. Center element
    int globalY = baseY + tid;
    s_Col[tid + radius] = BlurInputV.Load(int3(x, clamp(globalY, 0, (int)ImageHeightV - 1), 0));

    // 2. Top apron
    if (tid < radius)
    {
        int topY = baseY - radius + tid;
        s_Col[tid] = BlurInputV.Load(int3(x, clamp(topY, 0, (int)ImageHeightV - 1), 0));
    }

    // 3. Bottom apron
    if (tid < radius)
    {
        int bottomY = baseY + BLUR_BLOCK_Y + tid;
        s_Col[BLUR_BLOCK_Y + radius + tid] = BlurInputV.Load(int3(x, clamp(bottomY, 0, (int)ImageHeightV - 1), 0));
    }

    GroupMemoryBarrierWithGroupSync();

    if (globalY < (int)ImageHeightV)
    {
        float w[8];
        w[0] = BlurWeightsV[0].x;
        w[1] = BlurWeightsV[0].y;
        w[2] = BlurWeightsV[0].z;
        w[3] = BlurWeightsV[0].w;
        w[4] = BlurWeightsV[1].x;
        w[5] = BlurWeightsV[1].y;
        w[6] = BlurWeightsV[1].z;
        w[7] = BlurWeightsV[1].w;

        int centerIdx = tid + radius;
        float4 sum = s_Col[centerIdx] * w[0];
        [unroll]
        for (int k = 1; k <= MAX_RADIUS; k++)
        {
            if (k <= radius)
            {
                sum += (s_Col[centerIdx - k] + s_Col[centerIdx + k]) * w[k];
            }
        }
        BlurOutputV[int2(x, globalY)] = sum;
    }
}

// -------------------------------------------------------------------------
// 4. Sobel Gradient & 3x3 Sharpening with 18x18 LDS Apron
// -------------------------------------------------------------------------
Texture2D<float4> ConvInput : register(t0);
RWTexture2D<float4> ConvOutput : register(u0);

cbuffer ConvParams : register(b0)
{
    uint ConvWidth;
    uint ConvHeight;
    uint ConvMode;       // 0 = Sobel Magnitude, 1 = Laplacian Sharpen
    float ConvStrength;  // Multiplier / Sharpen strength
};

groupshared float4 s_Tile[18][18];

[numthreads(16, 16, 1)]
void CS_Convolution3x3(uint3 groupThreadId : SV_GroupThreadID,
                       uint3 groupId : SV_GroupID)
{
    int tx = (int)groupThreadId.x;
    int ty = (int)groupThreadId.y;
    int gx = (int)groupId.x * 16 + tx;
    int gy = (int)groupId.y * 16 + ty;

    int maxW = (int)ConvWidth - 1;
    int maxH = (int)ConvHeight - 1;

    // Center pixel
    s_Tile[ty + 1][tx + 1] = ConvInput.Load(int3(clamp(gx, 0, maxW), clamp(gy, 0, maxH), 0));

    // Apron borders
    if (tx == 0)
        s_Tile[ty + 1][0] = ConvInput.Load(int3(clamp(gx - 1, 0, maxW), clamp(gy, 0, maxH), 0));
    if (tx == 15)
        s_Tile[ty + 1][17] = ConvInput.Load(int3(clamp(gx + 1, 0, maxW), clamp(gy, 0, maxH), 0));
    if (ty == 0)
        s_Tile[0][tx + 1] = ConvInput.Load(int3(clamp(gx, 0, maxW), clamp(gy - 1, 0, maxH), 0));
    if (ty == 15)
        s_Tile[17][tx + 1] = ConvInput.Load(int3(clamp(gx, 0, maxW), clamp(gy + 1, 0, maxH), 0));

    // Apron corners
    if (tx == 0 && ty == 0)
        s_Tile[0][0] = ConvInput.Load(int3(clamp(gx - 1, 0, maxW), clamp(gy - 1, 0, maxH), 0));
    if (tx == 15 && ty == 0)
        s_Tile[0][17] = ConvInput.Load(int3(clamp(gx + 1, 0, maxW), clamp(gy - 1, 0, maxH), 0));
    if (tx == 0 && ty == 15)
        s_Tile[17][0] = ConvInput.Load(int3(clamp(gx - 1, 0, maxW), clamp(gy + 1, 0, maxH), 0));
    if (tx == 15 && ty == 15)
        s_Tile[17][17] = ConvInput.Load(int3(clamp(gx + 1, 0, maxW), clamp(gy + 1, 0, maxH), 0));

    GroupMemoryBarrierWithGroupSync();

    if (gx < (int)ConvWidth && gy < (int)ConvHeight)
    {
        int py = ty + 1;
        int px = tx + 1;

        float4 c = s_Tile[py][px];

        if (ConvMode == 0) // Sobel
        {
            float tl = dot(s_Tile[py - 1][px - 1].rgb, float3(0.2126f, 0.7152f, 0.0722f));
            float tc = dot(s_Tile[py - 1][px    ].rgb, float3(0.2126f, 0.7152f, 0.0722f));
            float tr = dot(s_Tile[py - 1][px + 1].rgb, float3(0.2126f, 0.7152f, 0.0722f));
            float ml = dot(s_Tile[py    ][px - 1].rgb, float3(0.2126f, 0.7152f, 0.0722f));
            float mr = dot(s_Tile[py    ][px + 1].rgb, float3(0.2126f, 0.7152f, 0.0722f));
            float bl = dot(s_Tile[py + 1][px - 1].rgb, float3(0.2126f, 0.7152f, 0.0722f));
            float bc = dot(s_Tile[py + 1][px    ].rgb, float3(0.2126f, 0.7152f, 0.0722f));
            float br = dot(s_Tile[py + 1][px + 1].rgb, float3(0.2126f, 0.7152f, 0.0722f));

            float gx_val = (-tl + tr) + 2.0f * (-ml + mr) + (-bl + br);
            float gy_val = (-tl - 2.0f * tc - tr) + (bl + 2.0f * bc + br);
            float mag = sqrt(gx_val * gx_val + gy_val * gy_val) * ConvStrength;
            float val = saturate(mag);
            ConvOutput[int2(gx, gy)] = float4(val, val, val, c.a);
        }
        else // Sharpen
        {
            float4 t = s_Tile[py - 1][px];
            float4 b = s_Tile[py + 1][px];
            float4 l = s_Tile[py][px - 1];
            float4 r = s_Tile[py][px + 1];

            float4 laplacian = 5.0f * c - (t + b + l + r);
            float4 sharpened = lerp(c, laplacian, ConvStrength);
            ConvOutput[int2(gx, gy)] = saturate(sharpened);
        }
    }
}

// -------------------------------------------------------------------------
// 5. Image Pyramid Downscale (CS_PyramidDown: 2x 5x5 Binomial Gaussian)
// -------------------------------------------------------------------------
Texture2D<float4> PyramidDownInput : register(t0);
RWTexture2D<float4> PyramidDownOutput : register(u0);

cbuffer PyramidDownParams : register(b0)
{
    uint DstWidth;
    uint DstHeight;
    uint SrcWidth;
    uint SrcHeight;
};

[numthreads(16, 16, 1)]
void CS_PyramidDown(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    if (dispatchThreadId.x >= DstWidth || dispatchThreadId.y >= DstHeight) return;

    int2 srcBase = int2(dispatchThreadId.xy) * 2;
    int maxW = (int)SrcWidth - 1;
    int maxH = (int)SrcHeight - 1;

    static const float weights[5] = { 0.0625f, 0.25f, 0.375f, 0.25f, 0.0625f };

    float4 color = float4(0, 0, 0, 0);
    [unroll]
    for (int dy = -2; dy <= 2; dy++)
    {
        int y = clamp(srcBase.y + dy, 0, maxH);
        float wy = weights[dy + 2];
        [unroll]
        for (int dx = -2; dx <= 2; dx++)
        {
            int x = clamp(srcBase.x + dx, 0, maxW);
            float wx = weights[dx + 2];
            color += PyramidDownInput.Load(int3(x, y, 0)) * (wx * wy);
        }
    }

    PyramidDownOutput[dispatchThreadId.xy] = color;
}

// -------------------------------------------------------------------------
// 6. Image Pyramid Upscale (CS_PyramidUp: 2x Bilinear Interpolation)
// -------------------------------------------------------------------------
Texture2D<float4> PyramidUpInput : register(t0);
RWTexture2D<float4> PyramidUpOutput : register(u0);

cbuffer PyramidUpParams : register(b0)
{
    uint UpDstWidth;
    uint UpDstHeight;
    uint UpSrcWidth;
    uint UpSrcHeight;
};

[numthreads(16, 16, 1)]
void CS_PyramidUp(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    if (dispatchThreadId.x >= UpDstWidth || dispatchThreadId.y >= UpDstHeight) return;

    float u = ((float)dispatchThreadId.x + 0.5f) / (float)UpDstWidth;
    float v = ((float)dispatchThreadId.y + 0.5f) / (float)UpDstHeight;

    float srcX = u * (float)UpSrcWidth - 0.5f;
    float srcY = v * (float)UpSrcHeight - 0.5f;

    int x0 = (int)floor(srcX);
    int y0 = (int)floor(srcY);
    int x1 = x0 + 1;
    int y1 = y0 + 1;

    float fx = srcX - (float)x0;
    float fy = srcY - (float)y0;

    int maxW = (int)UpSrcWidth - 1;
    int maxH = (int)UpSrcHeight - 1;

    float4 c00 = PyramidUpInput.Load(int3(clamp(x0, 0, maxW), clamp(y0, 0, maxH), 0));
    float4 c10 = PyramidUpInput.Load(int3(clamp(x1, 0, maxW), clamp(y0, 0, maxH), 0));
    float4 c01 = PyramidUpInput.Load(int3(clamp(x0, 0, maxW), clamp(y1, 0, maxH), 0));
    float4 c11 = PyramidUpInput.Load(int3(clamp(x1, 0, maxW), clamp(y1, 0, maxH), 0));

    float4 top = lerp(c00, c10, fx);
    float4 bot = lerp(c01, c11, fx);
    PyramidUpOutput[dispatchThreadId.xy] = lerp(top, bot, fy);
}

// -------------------------------------------------------------------------
// 7. Focus Stacking Measure (CS_FocusMeasure: Modified Laplacian Energy)
// -------------------------------------------------------------------------
Texture2D<float4> FocusInput : register(t0);
RWTexture2D<float> FocusEnergyOutput : register(u0);

cbuffer FocusParams : register(b0)
{
    uint FocusWidth;
    uint FocusHeight;
    uint FocusRadius;
    float FocusPad;
};

[numthreads(16, 16, 1)]
void CS_FocusMeasure(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    if (dispatchThreadId.x >= FocusWidth || dispatchThreadId.y >= FocusHeight) return;

    int2 coord = int2(dispatchThreadId.xy);
    int maxW = (int)FocusWidth - 1;
    int maxH = (int)FocusHeight - 1;

    float center = dot(FocusInput.Load(int3(coord, 0)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float left   = dot(FocusInput.Load(int3(clamp(coord.x - 1, 0, maxW), coord.y, 0)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float right  = dot(FocusInput.Load(int3(clamp(coord.x + 1, 0, maxW), coord.y, 0)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float top    = dot(FocusInput.Load(int3(coord.x, clamp(coord.y - 1, 0, maxH), 0)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float bottom = dot(FocusInput.Load(int3(coord.x, clamp(coord.y + 1, 0, maxH), 0)).rgb, float3(0.2126f, 0.7152f, 0.0722f));

    float mlx = abs(2.0f * center - left - right);
    float mly = abs(2.0f * center - top - bottom);
    float energy = mlx + mly;

    FocusEnergyOutput[dispatchThreadId.xy] = energy;
}

// -------------------------------------------------------------------------
// 8. Focus Stacking Blending (CS_FocusBlend: Pixel-wise maximum energy selection)
// -------------------------------------------------------------------------
Texture2D<float4> SliceColor : register(t0);
Texture2D<float> SliceEnergy : register(t1);
RWTexture2D<float4> BestColor : register(u0);
RWTexture2D<float> BestEnergy : register(u1);

cbuffer FocusBlendParams : register(b0)
{
    uint BlendWidth;
    uint BlendHeight;
    uint IsFirstSlice;
    float BlendPad;
};

[numthreads(16, 16, 1)]
void CS_FocusBlend(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    if (dispatchThreadId.x >= BlendWidth || dispatchThreadId.y >= BlendHeight) return;

    int2 coord = int2(dispatchThreadId.xy);
    float4 newColor = SliceColor.Load(int3(coord, 0));
    float newEnergy = SliceEnergy.Load(int3(coord, 0));

    if (IsFirstSlice != 0)
    {
        BestColor[coord] = newColor;
        BestEnergy[coord] = newEnergy;
    }
    else
    {
        float curBestEnergy = BestEnergy[coord];
        if (newEnergy > curBestEnergy)
        {
            BestColor[coord] = newColor;
            BestEnergy[coord] = newEnergy;
        }
    }
}

// -------------------------------------------------------------------------
// 9. HDR Tone Mapping (CS_HdrToneMapping: Reinhard, ACES Filmic, Exposure)
// -------------------------------------------------------------------------
Texture2D<float4> HdrInput : register(t0);
RWTexture2D<float4> SdrOutput : register(u0);

cbuffer HdrParams : register(b0)
{
    uint HdrWidth;
    uint HdrHeight;
    uint ToneMappingMode;
    float Exposure;
    float HdrGamma;
    float3 HdrPad;
};

float3 ReinhardToneMap(float3 c)
{
    return c / (1.0f + c);
}

float3 AcesFilmicToneMap(float3 x)
{
    float a = 2.51f;
    float b = 0.03f;
    float c = 2.43f;
    float d = 0.59f;
    float e = 0.14f;
    return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
}

[numthreads(16, 16, 1)]
void CS_HdrToneMapping(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    if (dispatchThreadId.x >= HdrWidth || dispatchThreadId.y >= HdrHeight) return;

    int2 coord = int2(dispatchThreadId.xy);
    float4 hdr = HdrInput.Load(int3(coord, 0));

    float exposureMult = exp2(Exposure);
    float3 color = hdr.rgb * exposureMult;

    if (ToneMappingMode == 0)
    {
        color = ReinhardToneMap(color);
    }
    else if (ToneMappingMode == 1)
    {
        color = AcesFilmicToneMap(color);
    }
    else
    {
        color = saturate(color);
    }

    float invGamma = 1.0f / max(HdrGamma, 0.001f);
    color = pow(max(color, 0.0f), invGamma);

    SdrOutput[coord] = float4(saturate(color), hdr.a);
}

// -------------------------------------------------------------------------
// 10. AI Tensor Preprocessing (CS_TensorPreprocessNCHW)
// Converts arbitrary resolution texture to Planar NCHW FP32 StructuredBuffer
// with bilinear interpolation, channel segregation, and Mean/Std normalization.
// -------------------------------------------------------------------------
Texture2D<float4> TensorInputImage : register(t0);
RWStructuredBuffer<float> TensorOutputBuffer : register(u0);

cbuffer TensorParams : register(b0)
{
    uint TensorSrcWidth;
    uint TensorSrcHeight;
    uint TensorDstWidth;
    uint TensorDstHeight;
    float3 TensorMean;
    float TensorPad0;
    float3 TensorStd;
    float TensorPad1;
};

[numthreads(16, 16, 1)]
void CS_TensorPreprocessNCHW(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    if (dispatchThreadId.x >= TensorDstWidth || dispatchThreadId.y >= TensorDstHeight) return;

    uint x = dispatchThreadId.x;
    uint y = dispatchThreadId.y;

    // Manual bilinear sampling from TensorInputImage
    float u = (x + 0.5f) / (float)TensorDstWidth;
    float v = (y + 0.5f) / (float)TensorDstHeight;
    float px = u * TensorSrcWidth - 0.5f;
    float py = v * TensorSrcHeight - 0.5f;

    int x0 = clamp((int)floor(px), 0, (int)TensorSrcWidth - 1);
    int y0 = clamp((int)floor(py), 0, (int)TensorSrcHeight - 1);
    int x1 = clamp(x0 + 1, 0, (int)TensorSrcWidth - 1);
    int y1 = clamp(y0 + 1, 0, (int)TensorSrcHeight - 1);

    float fx = px - floor(px);
    float fy = py - floor(py);

    float4 p00 = TensorInputImage.Load(int3(x0, y0, 0));
    float4 p10 = TensorInputImage.Load(int3(x1, y0, 0));
    float4 p01 = TensorInputImage.Load(int3(x0, y1, 0));
    float4 p11 = TensorInputImage.Load(int3(x1, y1, 0));

    float4 c = lerp(lerp(p00, p10, fx), lerp(p01, p11, fx), fy);

    // Normalize channels (assuming standard RGB ordering in float4.rgb)
    float normR = (c.r - TensorMean.x) / max(TensorStd.x, 0.0001f);
    float normG = (c.g - TensorMean.y) / max(TensorStd.y, 0.0001f);
    float normB = (c.b - TensorMean.z) / max(TensorStd.z, 0.0001f);

    uint planeSize = TensorDstWidth * TensorDstHeight;
    uint pixelIdx = y * TensorDstWidth + x;

    // Planar NCHW layout: [Channel 0: Red][Channel 1: Green][Channel 2: Blue]
    TensorOutputBuffer[0 * planeSize + pixelIdx] = normR;
    TensorOutputBuffer[1 * planeSize + pixelIdx] = normG;
    TensorOutputBuffer[2 * planeSize + pixelIdx] = normB;
}

// -------------------------------------------------------------------------
// 11. Heatmap Overlay (CS_HeatmapOverlay)
// Blends AI confidence/anomaly mask with background image using hot-iron ramp
// -------------------------------------------------------------------------
Texture2D<float4> OverlayBaseImage : register(t0);
Texture2D<float> OverlayMaskImage : register(t1);
RWTexture2D<float4> OverlayOutputImage : register(u0);

cbuffer OverlayParams : register(b0)
{
    uint OverlayWidth;
    uint OverlayHeight;
    float OverlayAlpha;
    float OverlayThreshold;
};

[numthreads(16, 16, 1)]
void CS_HeatmapOverlay(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    if (dispatchThreadId.x >= OverlayWidth || dispatchThreadId.y >= OverlayHeight) return;

    int2 coord = int2(dispatchThreadId.xy);
    float4 baseColor = OverlayBaseImage.Load(int3(coord, 0));
    float confidence = OverlayMaskImage.Load(int3(coord, 0));

    float4 finalColor = baseColor;
    if (confidence >= OverlayThreshold)
    {
        // High anomaly: vibrant warning color ramp (Yellow -> Red)
        float t = saturate((confidence - OverlayThreshold) / max(1.0f - OverlayThreshold, 0.001f));
        float3 alertColor = float3(1.0f, 1.0f - t * 0.8f, 0.0f); // Yellow to deep red/orange
        float blendFactor = OverlayAlpha * saturate(confidence);
        finalColor.rgb = lerp(baseColor.rgb, alertColor, blendFactor);
    }

    OverlayOutputImage[coord] = finalColor;
}



