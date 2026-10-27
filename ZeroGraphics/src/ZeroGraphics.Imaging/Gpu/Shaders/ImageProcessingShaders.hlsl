// =========================================================================
// ZeroGraphics GPU Image Pipeline Shaders (Direct3D 11 / Shader Model 4.0)
// High-performance image kernels & Operation Fusion execution
// =========================================================================

struct VS_OUTPUT {
    float4 Pos : SV_POSITION;
    float2 Tex : TEXCOORD0;
};

// -------------------------------------------------------------------------
// 1. Full-screen Triangle (No Vertex Buffer Required)
// -------------------------------------------------------------------------
VS_OUTPUT VS_Fullscreen(uint id : SV_VertexID) {
    VS_OUTPUT output;
    output.Tex = float2((id << 1) & 2, id & 2);
    output.Pos = float4(output.Tex * float2(2.0f, -2.0f) + float2(-1.0f, 1.0f), 0.0f, 1.0f);
    return output;
}

Texture2D inputTexture : register(t0);
SamplerState defaultSampler : register(s0);

// -------------------------------------------------------------------------
// 2. Texture Copy / Resampling (Bilinear/Point Resize)
// -------------------------------------------------------------------------
float4 PS_Resize(VS_OUTPUT input) : SV_TARGET {
    return inputTexture.Sample(defaultSampler, input.Tex);
}

// -------------------------------------------------------------------------
// 3. Color Transformation (Brightness, Contrast, BT.709 Grayscale, Invert)
// -------------------------------------------------------------------------
cbuffer ColorBuffer : register(b0) {
    float Brightness;   // -1.0 to 1.0 (default 0.0)
    float Contrast;     // 0.0 to 3.0 (default 1.0)
    float Grayscale;    // 1.0 = true, 0.0 = false
    float Invert;       // 1.0 = true, 0.0 = false
};

float4 PS_ColorAdjust(VS_OUTPUT input) : SV_TARGET {
    float4 c = inputTexture.Sample(defaultSampler, input.Tex);
    if (Grayscale > 0.5f) {
        float gray = dot(c.rgb, float3(0.2126f, 0.7152f, 0.0722f));
        c.rgb = float3(gray, gray, gray);
    }
    c.rgb = (c.rgb - 0.5f) * Contrast + 0.5f + Brightness;
    if (Invert > 0.5f) {
        c.rgb = 1.0f - c.rgb;
    }
    return saturate(c);
}

// -------------------------------------------------------------------------
// 4. Separable Gaussian Blur (1D Horizontal / Vertical 9-Tap)
// -------------------------------------------------------------------------
cbuffer BlurBuffer : register(b0) {
    float2 TexelSize;       // (1.0 / Width, 1.0 / Height)
    float2 BlurDirection;   // (1, 0) for H, (0, 1) for V
};

static const float blurWeights[5] = { 0.227027f, 0.1945946f, 0.1216216f, 0.054054f, 0.016216f };

float4 PS_GaussianBlur(VS_OUTPUT input) : SV_TARGET {
    float2 offset = TexelSize * BlurDirection;
    float4 result = inputTexture.Sample(defaultSampler, input.Tex) * blurWeights[0];
    [unroll]
    for (int i = 1; i < 5; i++) {
        result += inputTexture.Sample(defaultSampler, input.Tex + offset * (float)i) * blurWeights[i];
        result += inputTexture.Sample(defaultSampler, input.Tex - offset * (float)i) * blurWeights[i];
    }
    return result;
}

// -------------------------------------------------------------------------
// 5. Sobel 3x3 Gradient Edge Detection
// -------------------------------------------------------------------------
cbuffer SobelBuffer : register(b0) {
    float2 SobelTexelSize;
    float Multiplier;       // default 1.0f
    float SobelThreshold;   // 0.0 = grayscale edges, >0.0 = binarized edges
};

float4 PS_Sobel(VS_OUTPUT input) : SV_TARGET {
    float2 uv = input.Tex;
    float dx = SobelTexelSize.x;
    float dy = SobelTexelSize.y;

    float tl = dot(inputTexture.Sample(defaultSampler, uv + float2(-dx, -dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float tc = dot(inputTexture.Sample(defaultSampler, uv + float2(  0, -dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float tr = dot(inputTexture.Sample(defaultSampler, uv + float2( dx, -dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float ml = dot(inputTexture.Sample(defaultSampler, uv + float2(-dx,   0)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float mr = dot(inputTexture.Sample(defaultSampler, uv + float2( dx,   0)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float bl = dot(inputTexture.Sample(defaultSampler, uv + float2(-dx,  dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float bc = dot(inputTexture.Sample(defaultSampler, uv + float2(  0,  dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float br = dot(inputTexture.Sample(defaultSampler, uv + float2( dx,  dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));

    float gx = (-1.0f * tl + 1.0f * tr) + (-2.0f * ml + 2.0f * mr) + (-1.0f * bl + 1.0f * br);
    float gy = (-1.0f * tl - 2.0f * tc - 1.0f * tr) + (1.0f * bl + 2.0f * bc + 1.0f * br);

    float mag = (abs(gx) + abs(gy)) * Multiplier;
    if (SobelThreshold > 0.0f) {
        mag = (mag >= SobelThreshold) ? 1.0f : 0.0f;
    }
    mag = saturate(mag);
    return float4(mag, mag, mag, 1.0f);
}

// -------------------------------------------------------------------------
// 6. Laplacian 3x3 Sharpening
// -------------------------------------------------------------------------
cbuffer SharpenBuffer : register(b0) {
    float2 SharpenTexelSize;
    float SharpenStrength;  // default 1.0f
    float SharpenPadding;
};

float4 PS_Sharpen(VS_OUTPUT input) : SV_TARGET {
    float2 uv = input.Tex;
    float dx = SharpenTexelSize.x;
    float dy = SharpenTexelSize.y;

    float4 center = inputTexture.Sample(defaultSampler, uv);
    float4 top    = inputTexture.Sample(defaultSampler, uv + float2(0, -dy));
    float4 bottom = inputTexture.Sample(defaultSampler, uv + float2(0, dy));
    float4 left   = inputTexture.Sample(defaultSampler, uv + float2(-dx, 0));
    float4 right  = inputTexture.Sample(defaultSampler, uv + float2(dx, 0));

    float4 result = center * (1.0f + 4.0f * SharpenStrength) - (top + bottom + left + right) * SharpenStrength;
    result.a = center.a;
    return saturate(result);
}

// -------------------------------------------------------------------------
// 7. Binary Thresholding
// -------------------------------------------------------------------------
cbuffer ThresholdBuffer : register(b0) {
    float Cutoff;           // 0.0 to 1.0
    float ThresholdInvert;  // 1.0 = true, 0.0 = false
    float2 ThresholdPadding;
};

float4 PS_Threshold(VS_OUTPUT input) : SV_TARGET {
    float4 c = inputTexture.Sample(defaultSampler, input.Tex);
    float lum = dot(c.rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float val = (lum >= Cutoff) ? 1.0f : 0.0f;
    if (ThresholdInvert > 0.5f) {
        val = 1.0f - val;
    }
    return float4(val, val, val, 1.0f);
}

// -------------------------------------------------------------------------
// 8. Operation Fusion (Multi-step single-pass pipeline)
// -------------------------------------------------------------------------
cbuffer FusedBuffer : register(b0) {
    float2 FusedTexelSize;
    float FusedBrightness;
    float FusedContrast;

    float FusedGrayscale;
    float FusedInvert;
    float FusedSharpenStrength;
    float FusedThresholdCutoff; // < 0 means threshold disabled
};

float4 PS_Fused(VS_OUTPUT input) : SV_TARGET {
    float2 uv = input.Tex;
    float4 c;

    if (FusedSharpenStrength > 0.0f) {
        float dx = FusedTexelSize.x;
        float dy = FusedTexelSize.y;
        float4 center = inputTexture.Sample(defaultSampler, uv);
        float4 top    = inputTexture.Sample(defaultSampler, uv + float2(0, -dy));
        float4 bottom = inputTexture.Sample(defaultSampler, uv + float2(0, dy));
        float4 left   = inputTexture.Sample(defaultSampler, uv + float2(-dx, 0));
        float4 right  = inputTexture.Sample(defaultSampler, uv + float2(dx, 0));
        c = center * (1.0f + 4.0f * FusedSharpenStrength) - (top + bottom + left + right) * FusedSharpenStrength;
        c.a = center.a;
    } else {
        c = inputTexture.Sample(defaultSampler, uv);
    }

    if (FusedGrayscale > 0.5f) {
        float gray = dot(c.rgb, float3(0.2126f, 0.7152f, 0.0722f));
        c.rgb = float3(gray, gray, gray);
    }

    c.rgb = (c.rgb - 0.5f) * FusedContrast + 0.5f + FusedBrightness;

    if (FusedInvert > 0.5f) {
        c.rgb = 1.0f - c.rgb;
    }

    c = saturate(c);

    if (FusedThresholdCutoff >= 0.0f) {
        float lum = dot(c.rgb, float3(0.2126f, 0.7152f, 0.0722f));
        float val = (lum >= FusedThresholdCutoff) ? 1.0f : 0.0f;
        c = float4(val, val, val, 1.0f);
    }

    return c;
}

// -------------------------------------------------------------------------
// 9. GPU Mathematical Morphology: Dilation (Local Neighborhood Maximum)
// -------------------------------------------------------------------------
cbuffer MorphologyBuffer : register(b0) {
    float2 MorphTexelSize;
    int MorphRadius;        // 1 = 3x3, 2 = 5x5
    float MorphPadding;
};

float4 PS_Dilate(VS_OUTPUT input) : SV_TARGET {
    float2 uv = input.Tex;
    float4 maxVal = float4(0.0f, 0.0f, 0.0f, 0.0f);
    [loop]
    for (int y = -MorphRadius; y <= MorphRadius; y++) {
        [loop]
        for (int x = -MorphRadius; x <= MorphRadius; x++) {
            float2 sampleUv = uv + float2(x, y) * MorphTexelSize;
            float4 c = inputTexture.Sample(defaultSampler, sampleUv);
            maxVal = max(maxVal, c);
        }
    }
    return maxVal;
}

// -------------------------------------------------------------------------
// 10. GPU Mathematical Morphology: Erosion (Local Neighborhood Minimum)
// -------------------------------------------------------------------------
float4 PS_Erode(VS_OUTPUT input) : SV_TARGET {
    float2 uv = input.Tex;
    float4 minVal = float4(1.0f, 1.0f, 1.0f, 1.0f);
    [loop]
    for (int y = -MorphRadius; y <= MorphRadius; y++) {
        [loop]
        for (int x = -MorphRadius; x <= MorphRadius; x++) {
            float2 sampleUv = uv + float2(x, y) * MorphTexelSize;
            float4 c = inputTexture.Sample(defaultSampler, sampleUv);
            minVal = min(minVal, c);
        }
    }
    return minVal;
}

// -------------------------------------------------------------------------
// 11. GPU 2D Affine Alignment Transformation (Rotate, Scale, Translate)
// -------------------------------------------------------------------------
cbuffer AffineBuffer : register(b0) {
    float2 AffineCenter;      // (0.5, 0.5) in UV coordinates
    float2 AffineTranslation; // (tx, ty) in UV coordinates
    float AffineCosTheta;     // cos(-angle)
    float AffineSinTheta;     // sin(-angle)
    float AffineInvScaleX;    // 1.0 / scaleX
    float AffineInvScaleY;    // 1.0 / scaleY
    float4 AffineBorderColor; // RGBA to fill if UV out of bounds
    float AffineClampToBorder;// 1.0 = fill BorderColor, 0.0 = clamp to edge
    float3 AffinePadding;
};

float4 PS_AffineTransform(VS_OUTPUT input) : SV_TARGET {
    float2 p = input.Tex - AffineCenter - AffineTranslation;
    float rx = (AffineCosTheta * p.x - AffineSinTheta * p.y) * AffineInvScaleX;
    float ry = (AffineSinTheta * p.x + AffineCosTheta * p.y) * AffineInvScaleY;
    float2 srcUv = AffineCenter + float2(rx, ry);

    if (AffineClampToBorder > 0.5f) {
        if (srcUv.x < 0.0f || srcUv.x > 1.0f || srcUv.y < 0.0f || srcUv.y > 1.0f) {
            return AffineBorderColor;
        }
    }
    return inputTexture.Sample(defaultSampler, srcUv);
}

// -------------------------------------------------------------------------
// 12. GPU Canny Non-Maximum Suppression (Edge Thinning)
// -------------------------------------------------------------------------
cbuffer CannyBuffer : register(b0) {
    float2 CannyTexelSize;
    float CannyThreshold;   // Minimum threshold cutoff
    float CannyMultiplier;  // Intensity multiplier
};

float4 PS_CannyNms(VS_OUTPUT input) : SV_TARGET {
    float2 uv = input.Tex;
    float dx = CannyTexelSize.x;
    float dy = CannyTexelSize.y;

    float tl = dot(inputTexture.Sample(defaultSampler, uv + float2(-dx, -dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float tc = dot(inputTexture.Sample(defaultSampler, uv + float2(  0, -dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float tr = dot(inputTexture.Sample(defaultSampler, uv + float2( dx, -dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float ml = dot(inputTexture.Sample(defaultSampler, uv + float2(-dx,   0)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float mr = dot(inputTexture.Sample(defaultSampler, uv + float2( dx,   0)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float bl = dot(inputTexture.Sample(defaultSampler, uv + float2(-dx,  dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float bc = dot(inputTexture.Sample(defaultSampler, uv + float2(  0,  dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
    float br = dot(inputTexture.Sample(defaultSampler, uv + float2( dx,  dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));

    float gx = (-1.0f * tl + 1.0f * tr) + (-2.0f * ml + 2.0f * mr) + (-1.0f * bl + 1.0f * br);
    float gy = (-1.0f * tl - 2.0f * tc - 1.0f * tr) + (1.0f * bl + 2.0f * bc + 1.0f * br);

    float mag = sqrt(gx * gx + gy * gy) * CannyMultiplier;
    if (mag < CannyThreshold) {
        return float4(0.0f, 0.0f, 0.0f, 1.0f);
    }

    // Determine discrete gradient direction: 0, 45, 90, 135 degrees
    // Using tangent ratios to avoid expensive atan2
    float absGx = abs(gx);
    float absGy = abs(gy);
    float mag1 = 0.0f;
    float mag2 = 0.0f;

    if (absGy < absGx * 0.41421356f) {
        // Horizontal direction (0 deg): compare with left and right
        float left_gx = dot(inputTexture.Sample(defaultSampler, uv + float2(-dx, 0)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
        float right_gx = dot(inputTexture.Sample(defaultSampler, uv + float2(dx, 0)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
        mag1 = abs(left_gx);
        mag2 = abs(right_gx);
    } else if (absGx < absGy * 0.41421356f) {
        // Vertical direction (90 deg): compare with top and bottom
        float top_gy = dot(inputTexture.Sample(defaultSampler, uv + float2(0, -dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
        float bot_gy = dot(inputTexture.Sample(defaultSampler, uv + float2(0, dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
        mag1 = abs(top_gy);
        mag2 = abs(bot_gy);
    } else if ((gx * gy) > 0.0f) {
        // Diagonal 45 deg (top-right & bottom-left)
        float tr_g = dot(inputTexture.Sample(defaultSampler, uv + float2(dx, -dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
        float bl_g = dot(inputTexture.Sample(defaultSampler, uv + float2(-dx, dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
        mag1 = abs(tr_g);
        mag2 = abs(bl_g);
    } else {
        // Diagonal 135 deg (top-left & bottom-right)
        float tl_g = dot(inputTexture.Sample(defaultSampler, uv + float2(-dx, -dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
        float br_g = dot(inputTexture.Sample(defaultSampler, uv + float2(dx, dy)).rgb, float3(0.2126f, 0.7152f, 0.0722f));
        mag1 = abs(tl_g);
        mag2 = abs(br_g);
    }

    // Suppress if not local maximum along gradient vector
    if (mag < mag1 || mag < mag2) {
        return float4(0.0f, 0.0f, 0.0f, 1.0f);
    }

    float val = saturate(mag);
    return float4(val, val, val, 1.0f);
}

// -------------------------------------------------------------------------
// 13. GPU Non-Linear Gamma Correction
// -------------------------------------------------------------------------
cbuffer GammaBuffer : register(b0) {
    float GammaValue;       // Standard values: 2.2 (sRGB decode), 0.4545 (encode), etc.
    float3 GammaPadding;
};

float4 PS_Gamma(VS_OUTPUT input) : SV_TARGET {
    float4 c = inputTexture.Sample(defaultSampler, input.Tex);
    c.rgb = pow(max(c.rgb, 0.00001f), GammaValue);
    return saturate(c);
}

