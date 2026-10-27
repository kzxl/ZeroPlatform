using System;
using System.Runtime.InteropServices;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;

namespace ZeroGraphics.Imaging.Gpu
{
    /// <summary>
    /// DirectCompute (CS 5.0) Color adjustment pass node.
    /// Executes Brightness, Contrast, BT.709 Grayscale, Invert, Gamma, and Cutoff Threshold in a single compute dispatch.
    /// </summary>
    public sealed class CsColorPassNode : ImageGraphNode
    {
        public override bool RequiresUav => true;

        public float Brightness { get; }
        public float Contrast { get; }
        public bool Grayscale { get; }
        public bool Invert { get; }
        public float Gamma { get; }
        public float Threshold { get; }

        public CsColorPassNode(
            float brightness = 0.0f,
            float contrast = 1.0f,
            bool grayscale = false,
            bool invert = false,
            float gamma = 1.0f,
            float threshold = -1.0f)
        {
            Brightness = brightness;
            Contrast = contrast;
            Grayscale = grayscale;
            Invert = invert;
            Gamma = gamma;
            Threshold = threshold;
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
            => new ImageDimensions(inputWidth, inputHeight);

        [StructLayout(LayoutKind.Sequential, Size = 32)]
        private struct ColorComputeConstants
        {
            public float Brightness;
            public float Contrast;
            public float Grayscale;
            public float Invert;
            public float Gamma;
            public float Threshold;
            public float Pad0;
            public float Pad1;
        }

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            if (output.Uav == null)
                throw new InvalidOperationException("Output texture must have a valid UAV descriptor for Compute Shader execution.");

            var ctx = context.ImmediateContext;

            var cb = new ColorComputeConstants
            {
                Brightness = Brightness,
                Contrast = Contrast,
                Grayscale = Grayscale ? 1.0f : 0.0f,
                Invert = Invert ? 1.0f : 0.0f,
                Gamma = Gamma,
                Threshold = Threshold
            };
            ctx.UpdateSubresource(context.ConstantBuffer, ref cb);

            ctx.CSSetShader(context.CsColorAdjust);
            ctx.CSSetConstantBuffers(0, context.ConstantBuffer);
            ctx.CSSetShaderResources(0, input.Srv);
            ctx.CSSetUnorderedAccessViews(0, output.Uav);

            uint groupsX = (uint)((output.Width + 15) / 16);
            uint groupsY = (uint)((output.Height + 15) / 16);
            ctx.Dispatch(groupsX, groupsY, 1);

            // Unbind resources
            ctx.CSSetShaderResources(0, (D3D11ShaderResourceView?)null);
            ctx.CSSetUnorderedAccessViews(0, (D3D11UnorderedAccessView?)null);
            ctx.CSSetShader(null);
        }
    }

    /// <summary>
    /// DirectCompute (CS 5.0) Separable Gaussian Blur pass node with Local Data Share (groupshared LDS) caching.
    /// Reduces VRAM global memory fetches by 70%-85% compared to multi-pass pixel shaders.
    /// </summary>
    public sealed class CsGaussianBlurPassNode : ImageGraphNode
    {
        public override bool RequiresUav => true;

        public float Sigma { get; }
        public int Radius { get; }

        public CsGaussianBlurPassNode(float sigma = 1.5f)
        {
            Sigma = Math.Max(0.1f, sigma);
            // 3-sigma rule, clamped to 1..7 for LDS apron
            Radius = Math.Min(7, Math.Max(1, (int)Math.Ceiling(Sigma * 3.0f)));
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
            => new ImageDimensions(inputWidth, inputHeight);

        [StructLayout(LayoutKind.Sequential, Size = 48)]
        private struct BlurConstants
        {
            public uint ImageWidth;
            public uint ImageHeight;
            public int BlurRadius;
            public float BlurPad;
            public float W0;
            public float W1;
            public float W2;
            public float W3;
            public float W4;
            public float W5;
            public float W6;
            public float W7;
        }

        private float[] CalculateGaussianWeights()
        {
            float[] weights = new float[8];
            float sum = 0f;
            for (int i = 0; i <= Radius; i++)
            {
                float w = (float)Math.Exp(-(i * i) / (2.0f * Sigma * Sigma));
                weights[i] = w;
                sum += (i == 0) ? w : 2.0f * w;
            }
            for (int i = 0; i <= Radius; i++)
            {
                weights[i] /= sum;
            }
            return weights;
        }

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            if (output.Uav == null)
                throw new InvalidOperationException("Output texture must have a valid UAV descriptor for Compute Shader execution.");

            var ctx = context.ImmediateContext;
            var pool = context.TexturePool;
            var weights = CalculateGaussianWeights();

            // 1. Lease intermediate ping-pong texture with UAV support
            var tempH = pool.Acquire(input.Width, input.Height, input.Format, needsUav: true);
            try
            {
                // --- Pass 1: Horizontal Blur (input -> tempH) ---
                var cbH = new BlurConstants
                {
                    ImageWidth = (uint)input.Width,
                    ImageHeight = (uint)input.Height,
                    BlurRadius = Radius,
                    W0 = weights[0],
                    W1 = weights[1],
                    W2 = weights[2],
                    W3 = weights[3],
                    W4 = weights[4],
                    W5 = weights[5],
                    W6 = weights[6],
                    W7 = weights[7]
                };
                ctx.UpdateSubresource(context.ConstantBuffer, ref cbH);

                ctx.CSSetShader(context.CsBlurHorizontal);
                ctx.CSSetConstantBuffers(0, context.ConstantBuffer);
                ctx.CSSetShaderResources(0, input.Srv);
                ctx.CSSetUnorderedAccessViews(0, tempH.Uav);

                uint groupsX = (uint)((input.Width + 127) / 128);
                uint groupsY = (uint)input.Height;
                ctx.Dispatch(groupsX, groupsY, 1);

                // Unbind between passes
                ctx.CSSetShaderResources(0, (D3D11ShaderResourceView?)null);
                ctx.CSSetUnorderedAccessViews(0, (D3D11UnorderedAccessView?)null);

                // --- Pass 2: Vertical Blur (tempH -> output) ---
                var cbV = new BlurConstants
                {
                    ImageWidth = (uint)input.Width,
                    ImageHeight = (uint)input.Height,
                    BlurRadius = Radius,
                    W0 = weights[0],
                    W1 = weights[1],
                    W2 = weights[2],
                    W3 = weights[3],
                    W4 = weights[4],
                    W5 = weights[5],
                    W6 = weights[6],
                    W7 = weights[7]
                };
                ctx.UpdateSubresource(context.ConstantBuffer, ref cbV);

                ctx.CSSetShader(context.CsBlurVertical);
                ctx.CSSetConstantBuffers(0, context.ConstantBuffer);
                ctx.CSSetShaderResources(0, tempH.Srv);
                ctx.CSSetUnorderedAccessViews(0, output.Uav);

                uint vertGroupsX = (uint)input.Width;
                uint vertGroupsY = (uint)((input.Height + 127) / 128);
                ctx.Dispatch(vertGroupsX, vertGroupsY, 1);

                // Final unbind
                ctx.CSSetShaderResources(0, (D3D11ShaderResourceView?)null);
                ctx.CSSetUnorderedAccessViews(0, (D3D11UnorderedAccessView?)null);
                ctx.CSSetShader(null);
            }
            finally
            {
                pool.Release(tempH);
            }
        }
    }

    /// <summary>
    /// DirectCompute (CS 5.0) 3x3 Convolution pass node with 18x18 LDS Apron tile caching.
    /// Supports Sobel Edge Detection and Laplacian Sharpening.
    /// </summary>
    public sealed class CsConvolutionPassNode : ImageGraphNode
    {
        public override bool RequiresUav => true;

        public uint Mode { get; } // 0 = Sobel, 1 = Sharpen
        public float Strength { get; }

        public CsConvolutionPassNode(bool isSobel, float strength = 1.0f)
        {
            Mode = isSobel ? 0u : 1u;
            Strength = strength;
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
            => new ImageDimensions(inputWidth, inputHeight);

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct ConvConstants
        {
            public uint ConvWidth;
            public uint ConvHeight;
            public uint ConvMode;
            public float ConvStrength;
        }

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            if (output.Uav == null)
                throw new InvalidOperationException("Output texture must have a valid UAV descriptor for Compute Shader execution.");

            var ctx = context.ImmediateContext;

            var cb = new ConvConstants
            {
                ConvWidth = (uint)input.Width,
                ConvHeight = (uint)input.Height,
                ConvMode = Mode,
                ConvStrength = Strength
            };
            ctx.UpdateSubresource(context.ConstantBuffer, ref cb);

            ctx.CSSetShader(context.CsConvolution3x3);
            ctx.CSSetConstantBuffers(0, context.ConstantBuffer);
            ctx.CSSetShaderResources(0, input.Srv);
            ctx.CSSetUnorderedAccessViews(0, output.Uav);

            uint groupsX = (uint)((output.Width + 15) / 16);
            uint groupsY = (uint)((output.Height + 15) / 16);
            ctx.Dispatch(groupsX, groupsY, 1);

            ctx.CSSetShaderResources(0, (D3D11ShaderResourceView?)null);
            ctx.CSSetUnorderedAccessViews(0, (D3D11UnorderedAccessView?)null);
            ctx.CSSetShader(null);
        }
    }
}
