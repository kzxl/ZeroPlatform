using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Imaging.Gpu
{
    /// <summary>
    /// Base class for a Render Graph pass node.
    /// </summary>
    public readonly struct ImageDimensions
    {
        public int Width { get; }
        public int Height { get; }
        public ImageDimensions(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    public abstract class ImageGraphNode
    {
        public virtual bool RequiresUav => false;
        public abstract ImageDimensions GetOutputSize(int inputWidth, int inputHeight);
        public abstract void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output);
    }

    /// <summary>
    /// Resizing pass node using bilinear or point interpolation.
    /// </summary>
    public sealed class ResizePassNode : ImageGraphNode
    {
        public int TargetWidth { get; }
        public int TargetHeight { get; }
        public bool Bilinear { get; }

        public ResizePassNode(int targetWidth, int targetHeight, bool bilinear = true)
        {
            if (targetWidth <= 0) throw new ArgumentOutOfRangeException(nameof(targetWidth));
            if (targetHeight <= 0) throw new ArgumentOutOfRangeException(nameof(targetHeight));
            TargetWidth = targetWidth;
            TargetHeight = targetHeight;
            Bilinear = bilinear;
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
            => new ImageDimensions(TargetWidth, TargetHeight);

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            var ctx = context.ImmediateContext;
            ctx.OMSetRenderTargets(output.Rtv);
            ctx.RSSetViewports(new D3D11_VIEWPORT(0, 0, output.Width, output.Height));
            ctx.PSSetShader(context.PsResize);
            ctx.PSSetSamplers(0, Bilinear ? context.LinearSampler : context.PointSampler);
            ctx.PSSetShaderResources(0, input.Srv);
            ctx.Draw(3, 0);
            ctx.PSSetShaderResources(0, null!);
        }
    }

    /// <summary>
    /// Color adjustment pass node (Brightness, Contrast, BT.709 Grayscale, Invert).
    /// </summary>
    public sealed class ColorPassNode : ImageGraphNode
    {
        public float Brightness { get; }
        public float Contrast { get; }
        public bool Grayscale { get; }
        public bool Invert { get; }

        public ColorPassNode(float brightness = 0.0f, float contrast = 1.0f, bool grayscale = false, bool invert = false)
        {
            Brightness = brightness;
            Contrast = contrast;
            Grayscale = grayscale;
            Invert = invert;
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
            => new ImageDimensions(inputWidth, inputHeight);

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct ColorConstants
        {
            public float Brightness;
            public float Contrast;
            public float Grayscale;
            public float Invert;
        }

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            var ctx = context.ImmediateContext;
            var cbData = new ColorConstants
            {
                Brightness = Brightness,
                Contrast = Contrast,
                Grayscale = Grayscale ? 1.0f : 0.0f,
                Invert = Invert ? 1.0f : 0.0f
            };
            ctx.UpdateSubresource(context.ConstantBuffer, ref cbData);

            ctx.OMSetRenderTargets(output.Rtv);
            ctx.RSSetViewports(new D3D11_VIEWPORT(0, 0, output.Width, output.Height));
            ctx.PSSetShader(context.PsColorAdjust);
            ctx.PSSetSamplers(0, context.LinearSampler);
            ctx.PSSetConstantBuffers(0, context.ConstantBuffer);
            ctx.PSSetShaderResources(0, input.Srv);
            ctx.Draw(3, 0);
            ctx.PSSetShaderResources(0, null!);
        }
    }

    /// <summary>
    /// Separable 1D Gaussian Blur pass node.
    /// Executes Horizontal pass then Vertical pass using a ping-pong intermediate texture.
    /// </summary>
    public sealed class GaussianBlurPassNode : ImageGraphNode
    {
        public float Sigma { get; }

        public GaussianBlurPassNode(float sigma = 1.5f)
        {
            Sigma = Math.Max(0.1f, sigma);
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
            => new ImageDimensions(inputWidth, inputHeight);

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct BlurConstants
        {
            public float TexelSizeX;
            public float TexelSizeY;
            public float DirX;
            public float DirY;
        }

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            var ctx = context.ImmediateContext;
            var pool = context.TexturePool;

            // 1. Lease temporary intermediate texture for horizontal pass
            var tempH = pool.Acquire(input.Width, input.Height, input.Format);
            try
            {
                // Pass 1: Horizontal Blur (input -> tempH)
                var cbH = new BlurConstants
                {
                    TexelSizeX = 1.0f / input.Width,
                    TexelSizeY = 1.0f / input.Height,
                    DirX = 1.0f,
                    DirY = 0.0f
                };
                ctx.UpdateSubresource(context.ConstantBuffer, ref cbH);
                ctx.OMSetRenderTargets(tempH.Rtv);
                ctx.RSSetViewports(new D3D11_VIEWPORT(0, 0, tempH.Width, tempH.Height));
                ctx.PSSetShader(context.PsGaussianBlur);
                ctx.PSSetSamplers(0, context.LinearSampler);
                ctx.PSSetConstantBuffers(0, context.ConstantBuffer);
                ctx.PSSetShaderResources(0, input.Srv);
                ctx.Draw(3, 0);
                ctx.PSSetShaderResources(0, null!);

                // Pass 2: Vertical Blur (tempH -> output)
                var cbV = new BlurConstants
                {
                    TexelSizeX = 1.0f / input.Width,
                    TexelSizeY = 1.0f / input.Height,
                    DirX = 0.0f,
                    DirY = 1.0f
                };
                ctx.UpdateSubresource(context.ConstantBuffer, ref cbV);
                ctx.OMSetRenderTargets(output.Rtv);
                ctx.RSSetViewports(new D3D11_VIEWPORT(0, 0, output.Width, output.Height));
                ctx.PSSetShader(context.PsGaussianBlur);
                ctx.PSSetShaderResources(0, tempH.Srv);
                ctx.Draw(3, 0);
                ctx.PSSetShaderResources(0, null!);
            }
            finally
            {
                pool.Release(tempH);
            }
        }
    }

    /// <summary>
    /// Laplacian 3x3 Sharpening pass node.
    /// </summary>
    public sealed class SharpenPassNode : ImageGraphNode
    {
        public float Strength { get; }

        public SharpenPassNode(float strength = 1.0f)
        {
            Strength = strength;
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
            => new ImageDimensions(inputWidth, inputHeight);

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct SharpenConstants
        {
            public float TexelSizeX;
            public float TexelSizeY;
            public float Strength;
            public float Padding;
        }

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            var ctx = context.ImmediateContext;
            var cbData = new SharpenConstants
            {
                TexelSizeX = 1.0f / input.Width,
                TexelSizeY = 1.0f / input.Height,
                Strength = Strength,
                Padding = 0.0f
            };
            ctx.UpdateSubresource(context.ConstantBuffer, ref cbData);

            ctx.OMSetRenderTargets(output.Rtv);
            ctx.RSSetViewports(new D3D11_VIEWPORT(0, 0, output.Width, output.Height));
            ctx.PSSetShader(context.PsSharpen);
            ctx.PSSetSamplers(0, context.LinearSampler);
            ctx.PSSetConstantBuffers(0, context.ConstantBuffer);
            ctx.PSSetShaderResources(0, input.Srv);
            ctx.Draw(3, 0);
            ctx.PSSetShaderResources(0, null!);
        }
    }

    /// <summary>
    /// Sobel 3x3 Gradient edge detection pass node.
    /// </summary>
    public sealed class SobelPassNode : ImageGraphNode
    {
        public float Multiplier { get; }
        public float Threshold { get; }

        public SobelPassNode(float multiplier = 1.0f, float threshold = 0.0f)
        {
            Multiplier = multiplier;
            Threshold = threshold;
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
            => new ImageDimensions(inputWidth, inputHeight);

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct SobelConstants
        {
            public float TexelSizeX;
            public float TexelSizeY;
            public float Multiplier;
            public float Threshold;
        }

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            var ctx = context.ImmediateContext;
            var cbData = new SobelConstants
            {
                TexelSizeX = 1.0f / input.Width,
                TexelSizeY = 1.0f / input.Height,
                Multiplier = Multiplier,
                Threshold = Threshold
            };
            ctx.UpdateSubresource(context.ConstantBuffer, ref cbData);

            ctx.OMSetRenderTargets(output.Rtv);
            ctx.RSSetViewports(new D3D11_VIEWPORT(0, 0, output.Width, output.Height));
            ctx.PSSetShader(context.PsSobel);
            ctx.PSSetSamplers(0, context.LinearSampler);
            ctx.PSSetConstantBuffers(0, context.ConstantBuffer);
            ctx.PSSetShaderResources(0, input.Srv);
            ctx.Draw(3, 0);
            ctx.PSSetShaderResources(0, null!);
        }
    }

    /// <summary>
    /// Binary Thresholding pass node.
    /// </summary>
    public sealed class ThresholdPassNode : ImageGraphNode
    {
        public float Cutoff { get; }
        public bool Invert { get; }

        public ThresholdPassNode(float cutoff = 0.5f, bool invert = false)
        {
            Cutoff = cutoff;
            Invert = invert;
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
            => new ImageDimensions(inputWidth, inputHeight);

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct ThresholdConstants
        {
            public float Cutoff;
            public float Invert;
            public float Pad1;
            public float Pad2;
        }

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            var ctx = context.ImmediateContext;
            var cbData = new ThresholdConstants
            {
                Cutoff = Cutoff,
                Invert = Invert ? 1.0f : 0.0f,
                Pad1 = 0.0f,
                Pad2 = 0.0f
            };
            ctx.UpdateSubresource(context.ConstantBuffer, ref cbData);

            ctx.OMSetRenderTargets(output.Rtv);
            ctx.RSSetViewports(new D3D11_VIEWPORT(0, 0, output.Width, output.Height));
            ctx.PSSetShader(context.PsThreshold);
            ctx.PSSetSamplers(0, context.LinearSampler);
            ctx.PSSetConstantBuffers(0, context.ConstantBuffer);
            ctx.PSSetShaderResources(0, input.Srv);
            ctx.Draw(3, 0);
            ctx.PSSetShaderResources(0, null!);
        }
    }

    /// <summary>
    /// Operation Fusion pass node.
    /// Fuses Resize/Sampling + Color/Grayscale + Sharpen + Threshold into a SINGLE GPU draw pass.
    /// </summary>
    public sealed class FusedPassNode : ImageGraphNode
    {
        public int TargetWidth { get; }
        public int TargetHeight { get; }
        public float Brightness { get; }
        public float Contrast { get; }
        public bool Grayscale { get; }
        public bool Invert { get; }
        public float SharpenStrength { get; }
        public float ThresholdCutoff { get; }

        public FusedPassNode(
            int targetWidth,
            int targetHeight,
            float brightness = 0.0f,
            float contrast = 1.0f,
            bool grayscale = false,
            bool invert = false,
            float sharpenStrength = 0.0f,
            float thresholdCutoff = -1.0f)
        {
            TargetWidth = targetWidth;
            TargetHeight = targetHeight;
            Brightness = brightness;
            Contrast = contrast;
            Grayscale = grayscale;
            Invert = invert;
            SharpenStrength = sharpenStrength;
            ThresholdCutoff = thresholdCutoff;
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
        {
            int w = TargetWidth > 0 ? TargetWidth : inputWidth;
            int h = TargetHeight > 0 ? TargetHeight : inputHeight;
            return new ImageDimensions(w, h);
        }

        [StructLayout(LayoutKind.Sequential, Size = 32)]
        private struct FusedConstants
        {
            public float TexelSizeX;
            public float TexelSizeY;
            public float Brightness;
            public float Contrast;

            public float Grayscale;
            public float Invert;
            public float SharpenStrength;
            public float ThresholdCutoff;
        }

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            var ctx = context.ImmediateContext;
            var cbData = new FusedConstants
            {
                TexelSizeX = 1.0f / input.Width,
                TexelSizeY = 1.0f / input.Height,
                Brightness = Brightness,
                Contrast = Contrast,
                Grayscale = Grayscale ? 1.0f : 0.0f,
                Invert = Invert ? 1.0f : 0.0f,
                SharpenStrength = SharpenStrength,
                ThresholdCutoff = ThresholdCutoff
            };
            ctx.UpdateSubresource(context.ConstantBuffer, ref cbData);

            ctx.OMSetRenderTargets(output.Rtv);
            ctx.RSSetViewports(new D3D11_VIEWPORT(0, 0, output.Width, output.Height));
            ctx.PSSetShader(context.PsFused);
            ctx.PSSetSamplers(0, context.LinearSampler);
            ctx.PSSetConstantBuffers(0, context.ConstantBuffer);
            ctx.PSSetShaderResources(0, input.Srv);
            ctx.Draw(3, 0);
            ctx.PSSetShaderResources(0, null!);
        }
    }

    /// <summary>
    /// GPU Mathematical Morphology: Dilation pass node (finds maximum in local neighborhood).
    /// Expands bright regions and bridges thin cracks.
    /// </summary>
    public sealed class DilatePassNode : ImageGraphNode
    {
        public int Radius { get; }

        public DilatePassNode(int radius = 1)
        {
            if (radius < 1) throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be at least 1.");
            Radius = radius;
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
            => new ImageDimensions(inputWidth, inputHeight);

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct MorphologyConstants
        {
            public float TexelSizeX;
            public float TexelSizeY;
            public int Radius;
            public float Pad;
        }

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            var ctx = context.ImmediateContext;
            var cbData = new MorphologyConstants
            {
                TexelSizeX = 1.0f / input.Width,
                TexelSizeY = 1.0f / input.Height,
                Radius = Radius,
                Pad = 0f
            };
            ctx.UpdateSubresource(context.ConstantBuffer, ref cbData);

            ctx.OMSetRenderTargets(output.Rtv);
            ctx.RSSetViewports(new D3D11_VIEWPORT(0, 0, output.Width, output.Height));
            ctx.PSSetShader(context.PsDilate);
            ctx.PSSetSamplers(0, context.PointSampler);
            ctx.PSSetConstantBuffers(0, context.ConstantBuffer);
            ctx.PSSetShaderResources(0, input.Srv);
            ctx.Draw(3, 0);
            ctx.PSSetShaderResources(0, null!);
        }
    }

    /// <summary>
    /// GPU Mathematical Morphology: Erosion pass node (finds minimum in local neighborhood).
    /// Shrinks bright regions and removes salt noise.
    /// </summary>
    public sealed class ErodePassNode : ImageGraphNode
    {
        public int Radius { get; }

        public ErodePassNode(int radius = 1)
        {
            if (radius < 1) throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be at least 1.");
            Radius = radius;
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
            => new ImageDimensions(inputWidth, inputHeight);

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct MorphologyConstants
        {
            public float TexelSizeX;
            public float TexelSizeY;
            public int Radius;
            public float Pad;
        }

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            var ctx = context.ImmediateContext;
            var cbData = new MorphologyConstants
            {
                TexelSizeX = 1.0f / input.Width,
                TexelSizeY = 1.0f / input.Height,
                Radius = Radius,
                Pad = 0f
            };
            ctx.UpdateSubresource(context.ConstantBuffer, ref cbData);

            ctx.OMSetRenderTargets(output.Rtv);
            ctx.RSSetViewports(new D3D11_VIEWPORT(0, 0, output.Width, output.Height));
            ctx.PSSetShader(context.PsErode);
            ctx.PSSetSamplers(0, context.PointSampler);
            ctx.PSSetConstantBuffers(0, context.ConstantBuffer);
            ctx.PSSetShaderResources(0, input.Srv);
            ctx.Draw(3, 0);
            ctx.PSSetShaderResources(0, null!);
        }
    }

    /// <summary>
    /// GPU 2D Affine Alignment Transformation pass node (Rotation, Scaling, Translation around center).
    /// Essential for automated part/PCB alignment and orientation correction.
    /// </summary>
    public sealed class AffineTransformPassNode : ImageGraphNode
    {
        public float AngleDegrees { get; }
        public float ScaleX { get; }
        public float ScaleY { get; }
        public float TranslationX { get; }
        public float TranslationY { get; }
        public float BorderR { get; }
        public float BorderG { get; }
        public float BorderB { get; }
        public float BorderA { get; }
        public bool ClampToBorder { get; }

        public AffineTransformPassNode(
            float angleDegrees,
            float scaleX = 1.0f,
            float scaleY = 1.0f,
            float translationX = 0.0f,
            float translationY = 0.0f,
            float borderR = 0.0f,
            float borderG = 0.0f,
            float borderB = 0.0f,
            float borderA = 1.0f,
            bool clampToBorder = true)
        {
            AngleDegrees = angleDegrees;
            ScaleX = Math.Abs(scaleX) < 1e-6f ? 1.0f : scaleX;
            ScaleY = Math.Abs(scaleY) < 1e-6f ? 1.0f : scaleY;
            TranslationX = translationX;
            TranslationY = translationY;
            BorderR = borderR;
            BorderG = borderG;
            BorderB = borderB;
            BorderA = borderA;
            ClampToBorder = clampToBorder;
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
            => new ImageDimensions(inputWidth, inputHeight);

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        private struct AffineConstants
        {
            public float CenterX;
            public float CenterY;
            public float TransX;
            public float TransY;

            public float CosTheta;
            public float SinTheta;
            public float InvScaleX;
            public float InvScaleY;

            public float BorderR;
            public float BorderG;
            public float BorderB;
            public float BorderA;

            public float ClampToBorder;
            public float Pad1;
            public float Pad2;
            public float Pad3;
        }

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            var ctx = context.ImmediateContext;
            double radians = -AngleDegrees * (Math.PI / 180.0);
            var cbData = new AffineConstants
            {
                CenterX = 0.5f,
                CenterY = 0.5f,
                TransX = TranslationX / input.Width,
                TransY = TranslationY / input.Height,
                CosTheta = (float)Math.Cos(radians),
                SinTheta = (float)Math.Sin(radians),
                InvScaleX = 1.0f / ScaleX,
                InvScaleY = 1.0f / ScaleY,
                BorderR = BorderR,
                BorderG = BorderG,
                BorderB = BorderB,
                BorderA = BorderA,
                ClampToBorder = ClampToBorder ? 1.0f : 0.0f,
                Pad1 = 0f,
                Pad2 = 0f,
                Pad3 = 0f
            };
            ctx.UpdateSubresource(context.ConstantBuffer, ref cbData);

            ctx.OMSetRenderTargets(output.Rtv);
            ctx.RSSetViewports(new D3D11_VIEWPORT(0, 0, output.Width, output.Height));
            ctx.PSSetShader(context.PsAffineTransform);
            ctx.PSSetSamplers(0, context.LinearSampler);
            ctx.PSSetConstantBuffers(0, context.ConstantBuffer);
            ctx.PSSetShaderResources(0, input.Srv);
            ctx.Draw(3, 0);
            ctx.PSSetShaderResources(0, null!);
        }
    }

    /// <summary>
    /// GPU Canny Non-Maximum Suppression pass node.
    /// Evaluates discrete gradient directions and suppresses non-ridge pixels, producing thin 1-pixel edges.
    /// </summary>
    public sealed class CannyNmsPassNode : ImageGraphNode
    {
        public float Multiplier { get; }
        public float Threshold { get; }

        public CannyNmsPassNode(float multiplier = 1.0f, float threshold = 0.1f)
        {
            Multiplier = multiplier;
            Threshold = threshold;
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
            => new ImageDimensions(inputWidth, inputHeight);

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct CannyConstants
        {
            public float TexelSizeX;
            public float TexelSizeY;
            public float Threshold;
            public float Multiplier;
        }

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            var ctx = context.ImmediateContext;
            var cbData = new CannyConstants
            {
                TexelSizeX = 1.0f / input.Width,
                TexelSizeY = 1.0f / input.Height,
                Threshold = Threshold,
                Multiplier = Multiplier
            };
            ctx.UpdateSubresource(context.ConstantBuffer, ref cbData);

            ctx.OMSetRenderTargets(output.Rtv);
            ctx.RSSetViewports(new D3D11_VIEWPORT(0, 0, output.Width, output.Height));
            ctx.PSSetShader(context.PsCannyNms);
            ctx.PSSetSamplers(0, context.LinearSampler);
            ctx.PSSetConstantBuffers(0, context.ConstantBuffer);
            ctx.PSSetShaderResources(0, input.Srv);
            ctx.Draw(3, 0);
            ctx.PSSetShaderResources(0, null!);
        }
    }

    /// <summary>
    /// GPU Non-Linear Gamma Correction pass node.
    /// Adjusts dynamic range: C_out = saturate(C_in ^ Gamma).
    /// </summary>
    public sealed class GammaPassNode : ImageGraphNode
    {
        public float Gamma { get; }

        public GammaPassNode(float gamma = 1.0f)
        {
            if (gamma <= 0.0f) throw new ArgumentOutOfRangeException(nameof(gamma), "Gamma must be greater than zero.");
            Gamma = gamma;
        }

        public override ImageDimensions GetOutputSize(int inputWidth, int inputHeight)
            => new ImageDimensions(inputWidth, inputHeight);

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        private struct GammaConstants
        {
            public float GammaValue;
            public float Pad1;
            public float Pad2;
            public float Pad3;
        }

        public override void Execute(GpuImageContext context, PooledGpuTexture input, PooledGpuTexture output)
        {
            var ctx = context.ImmediateContext;
            var cbData = new GammaConstants
            {
                GammaValue = Gamma,
                Pad1 = 0f,
                Pad2 = 0f,
                Pad3 = 0f
            };
            ctx.UpdateSubresource(context.ConstantBuffer, ref cbData);

            ctx.OMSetRenderTargets(output.Rtv);
            ctx.RSSetViewports(new D3D11_VIEWPORT(0, 0, output.Width, output.Height));
            ctx.PSSetShader(context.PsGamma);
            ctx.PSSetSamplers(0, context.LinearSampler);
            ctx.PSSetConstantBuffers(0, context.ConstantBuffer);
            ctx.PSSetShaderResources(0, input.Srv);
            ctx.Draw(3, 0);
            ctx.PSSetShaderResources(0, null!);
        }
    }

    /// <summary>
    /// Compiled and optimized GPU Image Pipeline ready for execution.
    /// Guarantees zero-allocation steady state and automated ping-pong buffer management.
    /// </summary>
    public sealed class CompiledImagePipeline
    {
        private readonly GpuImageContext _context;
        private readonly IReadOnlyList<ImageGraphNode> _passes;

        public int OriginalPassCount { get; }
        public int OptimizedPassCount => _passes.Count;
        public bool WasOperationFused => OptimizedPassCount < OriginalPassCount;
        public GpuImageContext Context => _context;

        public CompiledImagePipeline(GpuImageContext context, IReadOnlyList<ImageGraphNode> passes, int originalPassCount)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _passes = passes ?? throw new ArgumentNullException(nameof(passes));
            OriginalPassCount = originalPassCount;
        }

        /// <summary>
        /// Executes the pipeline: uploads CPU ImageBuffer -> executes all GPU passes in VRAM -> downloads back to CPU.
        /// </summary>
        public ImageBuffer Execute(ImageBuffer source, ImageBuffer? destination = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            // Determine final output dimensions
            int curW = source.Width;
            int curH = source.Height;
            for (int i = 0; i < _passes.Count; i++)
            {
                var nextSize = _passes[i].GetOutputSize(curW, curH);
                curW = nextSize.Width;
                curH = nextSize.Height;
            }

            destination ??= new ImageBuffer(curW, curH, source.Format);

            // Execute on GPU and download
            using (var finalGpuTexture = ExecuteToGpu(source))
            {
                _context.Transfer.Download(finalGpuTexture.Texture, destination);
            }

            return destination;
        }

        /// <summary>
        /// Executes the entire pipeline 100% on GPU and returns the final VRAM texture.
        /// Zero CPU download overhead; optimal for Direct2D or SwapChain on-screen rendering.
        /// Caller is responsible for releasing or disposing the returned PooledGpuTexture.
        /// </summary>
        public PooledGpuTexture ExecuteToGpu(ImageBuffer source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var pool = _context.TexturePool;
            var currentGpu = pool.Acquire(source.Width, source.Height);
            _context.Transfer.Upload(source, currentGpu.Texture);

            return ExecuteToGpu(currentGpu, retainSource: false);
        }

        /// <summary>
        /// Executes passes directly on an existing device-resident GPU texture without host-to-device upload.
        /// Retains full data residency in VRAM across chained graph executions.
        /// </summary>
        public PooledGpuTexture ExecuteToGpu(PooledGpuTexture sourceTexture, bool retainSource = true)
        {
            if (sourceTexture == null) throw new ArgumentNullException(nameof(sourceTexture));

            var pool = _context.TexturePool;
            var ctx = _context.ImmediateContext;

            // 1. Common Pipeline State
            ctx.IASetInputLayout(null!);
            ctx.IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY.D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
            ctx.VSSetShader(_context.VsFullscreen);
            ctx.RSSetState(_context.RasterizerState);
            ctx.OMSetBlendState(null);

            var currentGpu = sourceTexture;

            if (_passes.Count == 0)
            {
                return currentGpu;
            }

            int currentW = currentGpu.Width;
            int currentH = currentGpu.Height;

            for (int i = 0; i < _passes.Count; i++)
            {
                var pass = _passes[i];
                var nextSize = pass.GetOutputSize(currentW, currentH);
                var nextGpu = pool.Acquire(nextSize.Width, nextSize.Height, currentGpu.Format, needsUav: pass.RequiresUav);

                pass.Execute(_context, currentGpu, nextGpu);

                // Release previous texture back to pool if not the initial retained source
                if (i > 0 || !retainSource)
                {
                    pool.Release(currentGpu);
                }

                currentGpu = nextGpu;
                currentW = nextSize.Width;
                currentH = nextSize.Height;
            }

            return currentGpu;
        }
    }

    /// <summary>
    /// Fluent builder for constructing and optimizing GPU Image Processing Graphs.
    /// </summary>
    public sealed class ImagePipelineBuilder
    {
        private readonly GpuImageContext _context;
        private readonly List<ImageGraphNode> _nodes = new List<ImageGraphNode>();

        public ImagePipelineBuilder(GpuImageContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public ImagePipelineBuilder AddResize(int targetWidth, int targetHeight, bool bilinear = true)
        {
            _nodes.Add(new ResizePassNode(targetWidth, targetHeight, bilinear));
            return this;
        }

        public ImagePipelineBuilder AddColorAdjust(float brightness = 0.0f, float contrast = 1.0f, bool grayscale = false, bool invert = false)
        {
            _nodes.Add(new ColorPassNode(brightness, contrast, grayscale, invert));
            return this;
        }

        public ImagePipelineBuilder AddGaussianBlur(float sigma = 1.5f)
        {
            _nodes.Add(new GaussianBlurPassNode(sigma));
            return this;
        }

        public ImagePipelineBuilder AddSharpen(float strength = 1.0f)
        {
            _nodes.Add(new SharpenPassNode(strength));
            return this;
        }

        public ImagePipelineBuilder AddSobel(float multiplier = 1.0f, float threshold = 0.0f)
        {
            _nodes.Add(new SobelPassNode(multiplier, threshold));
            return this;
        }

        public ImagePipelineBuilder AddThreshold(float cutoff = 0.5f, bool invert = false)
        {
            _nodes.Add(new ThresholdPassNode(cutoff, invert));
            return this;
        }

        public ImagePipelineBuilder AddDilate(int radius = 1)
        {
            _nodes.Add(new DilatePassNode(radius));
            return this;
        }

        public ImagePipelineBuilder AddErode(int radius = 1)
        {
            _nodes.Add(new ErodePassNode(radius));
            return this;
        }

        public ImagePipelineBuilder AddOpening(int radius = 1)
        {
            return AddErode(radius).AddDilate(radius);
        }

        public ImagePipelineBuilder AddClosing(int radius = 1)
        {
            return AddDilate(radius).AddErode(radius);
        }

        public ImagePipelineBuilder AddAffineTransform(
            float angleDegrees,
            float scaleX = 1.0f,
            float scaleY = 1.0f,
            float translationX = 0.0f,
            float translationY = 0.0f,
            float borderR = 0.0f,
            float borderG = 0.0f,
            float borderB = 0.0f,
            float borderA = 1.0f,
            bool clampToBorder = true)
        {
            _nodes.Add(new AffineTransformPassNode(angleDegrees, scaleX, scaleY, translationX, translationY, borderR, borderG, borderB, borderA, clampToBorder));
            return this;
        }

        public ImagePipelineBuilder AddCannyNms(float multiplier = 1.0f, float threshold = 0.1f)
        {
            _nodes.Add(new CannyNmsPassNode(multiplier, threshold));
            return this;
        }

        public ImagePipelineBuilder AddGamma(float gamma = 1.0f)
        {
            _nodes.Add(new GammaPassNode(gamma));
            return this;
        }

        public ImagePipelineBuilder AddCsColorAdjust(float brightness = 0.0f, float contrast = 1.0f, bool grayscale = false, bool invert = false, float gamma = 1.0f, float threshold = -1.0f)
        {
            _nodes.Add(new CsColorPassNode(brightness, contrast, grayscale, invert, gamma, threshold));
            return this;
        }

        public ImagePipelineBuilder AddCsGaussianBlur(float sigma = 1.5f)
        {
            _nodes.Add(new CsGaussianBlurPassNode(sigma));
            return this;
        }

        public ImagePipelineBuilder AddCsSobel(float strength = 1.0f)
        {
            _nodes.Add(new CsConvolutionPassNode(isSobel: true, strength));
            return this;
        }

        public ImagePipelineBuilder AddCsSharpen(float strength = 1.0f)
        {
            _nodes.Add(new CsConvolutionPassNode(isSobel: false, strength));
            return this;
        }

        public ImagePipelineBuilder AddCustomNode(ImageGraphNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            _nodes.Add(node);
            return this;
        }

        /// <summary>
        /// Compiles the graph and executes Operation Fusion optimization.
        /// Merges compatible consecutive passes into single-pass fused kernels.
        /// </summary>
        public CompiledImagePipeline Compile()
        {
            int originalCount = _nodes.Count;
            var optimized = OptimizeNodes(_nodes);
            return new CompiledImagePipeline(_context, optimized, originalCount);
        }

        private static List<ImageGraphNode> OptimizeNodes(List<ImageGraphNode> nodes)
        {
            var result = new List<ImageGraphNode>();
            int i = 0;

            while (i < nodes.Count)
            {
                var current = nodes[i];

                // Check if we can fuse a sequence: Resize? -> ColorAdjust? -> Sharpen? -> Threshold?
                int targetW = -1;
                int targetH = -1;
                float brightness = 0.0f;
                float contrast = 1.0f;
                bool grayscale = false;
                bool invert = false;
                float sharpenStrength = 0.0f;
                float thresholdCutoff = -1.0f;

                int fusedNodesCount = 0;
                int j = i;

                if (j < nodes.Count && nodes[j] is ResizePassNode resize)
                {
                    targetW = resize.TargetWidth;
                    targetH = resize.TargetHeight;
                    j++;
                    fusedNodesCount++;
                }

                if (j < nodes.Count && nodes[j] is ColorPassNode color)
                {
                    brightness = color.Brightness;
                    contrast = color.Contrast;
                    grayscale = color.Grayscale;
                    invert = color.Invert;
                    j++;
                    fusedNodesCount++;
                }

                if (j < nodes.Count && nodes[j] is SharpenPassNode sharpen)
                {
                    sharpenStrength = sharpen.Strength;
                    j++;
                    fusedNodesCount++;
                }

                if (j < nodes.Count && nodes[j] is ThresholdPassNode threshold)
                {
                    thresholdCutoff = threshold.Cutoff;
                    if (threshold.Invert) invert = !invert;
                    j++;
                    fusedNodesCount++;
                }

                // If 2 or more nodes were combined, create a FusedPassNode
                if (fusedNodesCount >= 2)
                {
                    result.Add(new FusedPassNode(
                        targetW, targetH,
                        brightness, contrast, grayscale, invert,
                        sharpenStrength, thresholdCutoff));
                    i = j;
                }
                else
                {
                    result.Add(current);
                    i++;
                }
            }

            return result;
        }
    }
}
