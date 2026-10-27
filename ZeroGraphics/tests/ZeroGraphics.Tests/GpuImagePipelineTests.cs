using System;
using Xunit;
using ZeroGraphics.DirectX.Core;
using ZeroGraphics.DirectX.Native;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Gpu;

namespace ZeroGraphics.Tests
{
    public class GpuImagePipelineTests
    {
        [Fact]
        public void GpuImageContext_InitializationAndResources_Valid()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported)
            {
                return; // Gracefully skip on platforms without D3D11 hardware or WARP
            }

            using (var context = GpuImageContext.CreateDefault())
            {
                Assert.NotNull(context.Device);
                Assert.True(context.Device.IsValid);
                Assert.NotNull(context.ImmediateContext);
                Assert.True(context.ImmediateContext.IsValid);
                Assert.NotNull(context.TexturePool);
                Assert.NotNull(context.Transfer);

                // Verify compiled shaders
                Assert.NotNull(context.VsFullscreen);
                Assert.NotNull(context.PsResize);
                Assert.NotNull(context.PsColorAdjust);
                Assert.NotNull(context.PsGaussianBlur);
                Assert.NotNull(context.PsSobel);
                Assert.NotNull(context.PsSharpen);
                Assert.NotNull(context.PsThreshold);
                Assert.NotNull(context.PsFused);

                // Verify samplers & pipeline state
                Assert.NotNull(context.LinearSampler);
                Assert.NotNull(context.PointSampler);
                Assert.NotNull(context.RasterizerState);
                Assert.NotNull(context.ConstantBuffer);
            }
        }

        [Fact]
        public void GpuTexturePool_RecyclesIntermediateTexturesWithoutNewAllocations()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            {
                var pool = context.TexturePool;
                Assert.Equal(0, pool.TotalAllocatedCount);
                Assert.Equal(0, pool.ActiveLeasedCount);

                // 1. Lease first texture (100x100)
                var tex1 = pool.Acquire(100, 100);
                Assert.Equal(1, pool.TotalAllocatedCount);
                Assert.Equal(1, pool.ActiveLeasedCount);
                Assert.Equal(100, tex1.Width);
                Assert.Equal(100, tex1.Height);

                // 2. Release texture
                pool.Release(tex1);
                Assert.Equal(1, pool.TotalAllocatedCount);
                Assert.Equal(0, pool.ActiveLeasedCount);

                // 3. Lease again with same dimensions -> must recycle tex1 without new VRAM allocation
                var tex2 = pool.Acquire(100, 100);
                Assert.Same(tex1, tex2);
                Assert.Equal(1, pool.TotalAllocatedCount);
                Assert.Equal(1, pool.ActiveLeasedCount);

                // 4. Lease different dimension (200x200) -> allocates new entry
                var tex3 = pool.Acquire(200, 200);
                Assert.NotSame(tex2, tex3);
                Assert.Equal(2, pool.TotalAllocatedCount);
                Assert.Equal(2, pool.ActiveLeasedCount);

                pool.Release(tex2);
                pool.Release(tex3);
                Assert.Equal(2, pool.TotalAllocatedCount);
                Assert.Equal(0, pool.ActiveLeasedCount);
            }
        }

        [Fact]
        public void GpuTextureTransfer_ZeroCopyRoundtrip_PreservesPixels()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            using (var src = ImageBuffer.CreateBgra32(64, 64))
            using (var dst = ImageBuffer.CreateBgra32(64, 64))
            {
                // Clear and write landmark pixels
                src.Clear(12, 34, 56, 255);
                unsafe
                {
                    byte* p = src.GetRowPointer(20);
                    // Pixel at (10, 20): B=64, G=128, R=250, A=255
                    p[10 * 4 + 0] = 64;
                    p[10 * 4 + 1] = 128;
                    p[10 * 4 + 2] = 250;
                    p[10 * 4 + 3] = 255;

                    byte* p2 = src.GetRowPointer(40);
                    // Pixel at (30, 40): B=210, G=99, R=5, A=255
                    p2[30 * 4 + 0] = 210;
                    p2[30 * 4 + 1] = 99;
                    p2[30 * 4 + 2] = 5;
                    p2[30 * 4 + 3] = 255;
                }

                // DMA Upload to GPU Texture
                var pooled = context.TexturePool.Acquire(64, 64);
                try
                {
                    context.Transfer.Upload(src, pooled.Texture);

                    // DMA Download from GPU Texture
                    context.Transfer.Download(pooled.Texture, dst);

                    // Validate roundtrip pixel values
                    unsafe
                    {
                        byte* p = dst.GetRowPointer(20);
                        Assert.Equal(64, p[10 * 4 + 0]);
                        Assert.Equal(128, p[10 * 4 + 1]);
                        Assert.Equal(250, p[10 * 4 + 2]);
                        Assert.Equal(255, p[10 * 4 + 3]);

                        byte* p2 = dst.GetRowPointer(40);
                        Assert.Equal(210, p2[30 * 4 + 0]);
                        Assert.Equal(99, p2[30 * 4 + 1]);
                        Assert.Equal(5, p2[30 * 4 + 2]);
                        Assert.Equal(255, p2[30 * 4 + 3]);

                        // Also verify default clear pixel at (0, 0)
                        byte* p0 = dst.GetRowPointer(0);
                        Assert.Equal(56, p0[0]); // B
                        Assert.Equal(34, p0[1]); // G
                        Assert.Equal(12, p0[2]); // R
                        Assert.Equal(255, p0[3]); // A
                    }
                }
                finally
                {
                    context.TexturePool.Release(pooled);
                }
            }
        }

        [Fact]
        public void ImagePipelineBuilder_OperationFusion_FusesCompatiblePasses()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            {
                // Sequence: Resize -> ColorAdjust -> Sharpen -> Threshold
                // All 4 compatible operations can fuse into a single FusedPassNode
                var builder = new ImagePipelineBuilder(context)
                    .AddResize(128, 128)
                    .AddColorAdjust(brightness: 0.1f, contrast: 1.2f)
                    .AddSharpen(0.8f)
                    .AddThreshold(0.5f);

                var pipeline = builder.Compile();

                Assert.Equal(4, pipeline.OriginalPassCount);
                Assert.Equal(1, pipeline.OptimizedPassCount);
                Assert.True(pipeline.WasOperationFused);
            }
        }

        [Fact]
        public void ImagePipeline_EndToEndExecution_TransformsImageCorrectly()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            using (var src = ImageBuffer.CreateBgra32(64, 64))
            {
                // Fill source with mid-gray (128, 128, 128)
                src.Clear(128, 128, 128, 255);

                // Build a multi-pass pipeline:
                // 1. Resize to 32x32 + Brightness +0.2 -> Fused Pass 1 (32x32)
                // 2. Gaussian Blur (non-fused kernel) -> Pass 2 (32x32)
                var pipeline = new ImagePipelineBuilder(context)
                    .AddResize(32, 32)
                    .AddColorAdjust(brightness: 0.2f)
                    .AddGaussianBlur(1.0f)
                    .Compile();

                Assert.Equal(3, pipeline.OriginalPassCount);
                Assert.Equal(2, pipeline.OptimizedPassCount); // (Resize+Color) + GaussianBlur
                Assert.True(pipeline.WasOperationFused);

                // Execute end-to-end to CPU destination buffer
                using (var dst = pipeline.Execute(src))
                {
                    Assert.Equal(32, dst.Width);
                    Assert.Equal(32, dst.Height);

                    unsafe
                    {
                        byte* p = dst.GetRowPointer(16);
                        // Due to brightness +0.2 and blur, pixels should be brighter than 128
                        int b = p[16 * 4 + 0];
                        int g = p[16 * 4 + 1];
                        int r = p[16 * 4 + 2];
                        Assert.True(r > 130, $"Expected R > 130, got {r}");
                        Assert.True(g > 130, $"Expected G > 130, got {g}");
                        Assert.True(b > 130, $"Expected B > 130, got {b}");
                    }
                }

                // Execute directly to GPU texture (100% VRAM resident)
                using (var gpuResult = pipeline.ExecuteToGpu(src))
                {
                    Assert.NotNull(gpuResult);
                    Assert.Equal(32, gpuResult.Width);
                    Assert.Equal(32, gpuResult.Height);
                    Assert.NotNull(gpuResult.Texture);
                    Assert.NotNull(gpuResult.Srv);
                    Assert.NotNull(gpuResult.Rtv);
                }
            }
        }

        [Fact]
        public void ImagePipeline_MorphologyDilateAndErode_ExpandsAndShrinksFeatures()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            using (var src = ImageBuffer.CreateBgra32(32, 32))
            {
                // Black background with single isolated white point at (16, 16)
                src.Clear(0, 0, 0, 255);
                unsafe
                {
                    byte* p = src.GetRowPointer(16);
                    p[16 * 4 + 0] = 255;
                    p[16 * 4 + 1] = 255;
                    p[16 * 4 + 2] = 255;
                    p[16 * 4 + 3] = 255;
                }

                // 1. Dilate pass (radius: 1) -> Expands 1x1 point into 3x3 square
                var dilatePipeline = new ImagePipelineBuilder(context)
                    .AddDilate(radius: 1)
                    .Compile();

                using (var dilated = dilatePipeline.Execute(src))
                {
                    unsafe
                    {
                        // Adjacent pixels should now be 255
                        byte* pCenter = dilated.GetRowPointer(16);
                        byte* pTop = dilated.GetRowPointer(15);
                        byte* pBottom = dilated.GetRowPointer(17);

                        Assert.Equal(255, pCenter[16 * 4 + 0]); // Center
                        Assert.Equal(255, pCenter[15 * 4 + 0]); // Left
                        Assert.Equal(255, pCenter[17 * 4 + 0]); // Right
                        Assert.Equal(255, pTop[16 * 4 + 0]);    // Top
                        Assert.Equal(255, pBottom[16 * 4 + 0]); // Bottom

                        // Far pixel should still be black 0
                        byte* pFar = dilated.GetRowPointer(5);
                        Assert.Equal(0, pFar[5 * 4 + 0]);
                    }

                    // 2. Erode pass (radius: 1) -> Shrinks 3x3 square back to isolated point
                    var erodePipeline = new ImagePipelineBuilder(context)
                        .AddErode(radius: 1)
                        .Compile();

                    using (var eroded = erodePipeline.Execute(dilated))
                    {
                        unsafe
                        {
                            byte* pCenter = eroded.GetRowPointer(16);
                            byte* pLeft = eroded.GetRowPointer(16);

                            Assert.Equal(255, pCenter[16 * 4 + 0]); // Center stays
                            Assert.Equal(0, pLeft[15 * 4 + 0]);     // Left eroded away
                        }
                    }
                }

                // 3. Opening & Closing shortcuts compile and execute cleanly
                var openClosePipeline = new ImagePipelineBuilder(context)
                    .AddOpening(radius: 1)
                    .AddClosing(radius: 1)
                    .Compile();

                Assert.Equal(4, openClosePipeline.OriginalPassCount);
                using (var res = openClosePipeline.Execute(src))
                {
                    Assert.Equal(32, res.Width);
                    Assert.Equal(32, res.Height);
                }
            }
        }

        [Fact]
        public void ImagePipeline_AffineTransform_RotatesImageAccurately()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            using (var src = ImageBuffer.CreateBgra32(64, 64))
            {
                // Top half white (y < 32), bottom half black (y >= 32)
                src.Clear(0, 0, 0, 255);
                unsafe
                {
                    for (int y = 0; y < 32; y++)
                    {
                        byte* row = src.GetRowPointer(y);
                        for (int x = 0; x < 64; x++)
                        {
                            row[x * 4 + 0] = 255;
                            row[x * 4 + 1] = 255;
                            row[x * 4 + 2] = 255;
                        }
                    }
                }

                // Rotate 90 degrees around center
                var pipeline = new ImagePipelineBuilder(context)
                    .AddAffineTransform(angleDegrees: 90.0f, scaleX: 1.0f, scaleY: 1.0f)
                    .Compile();

                using (var dst = pipeline.Execute(src))
                {
                    Assert.Equal(64, dst.Width);
                    Assert.Equal(64, dst.Height);

                    unsafe
                    {
                        // After 90-degree rotation, the horizontal division becomes vertical
                        // Pixel at (10, 32) vs (54, 32) should exhibit the boundary flip
                        byte* rowMid = dst.GetRowPointer(32);
                        int valLeft = rowMid[10 * 4 + 0];
                        int valRight = rowMid[54 * 4 + 0];

                        // One side should be bright (>200) and the opposite side should be dark (<50)
                        Assert.True((valLeft > 200 && valRight < 50) || (valLeft < 50 && valRight > 200),
                            $"Expected distinct brightness split, got valLeft={valLeft}, valRight={valRight}");
                    }
                }
            }
        }

        [Fact]
        public void ImagePipeline_CannyNms_ThinsEdges()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            using (var src = ImageBuffer.CreateBgra32(64, 64))
            {
                // Create high-contrast step edge in center: left black, right white
                src.Clear(0, 0, 0, 255);
                unsafe
                {
                    for (int y = 0; y < 64; y++)
                    {
                        byte* row = src.GetRowPointer(y);
                        for (int x = 32; x < 64; x++)
                        {
                            row[x * 4 + 0] = 255;
                            row[x * 4 + 1] = 255;
                            row[x * 4 + 2] = 255;
                        }
                    }
                }

                // Canny NMS pass directly computes gradients and suppresses non-maxima
                var pipeline = new ImagePipelineBuilder(context)
                    .AddCannyNms(multiplier: 1.0f, threshold: 0.1f)
                    .Compile();

                using (var dst = pipeline.Execute(src))
                {
                    Assert.Equal(64, dst.Width);
                    Assert.Equal(64, dst.Height);

                    unsafe
                    {
                        // Ridge edge should peak near x=31..32
                        byte* row = dst.GetRowPointer(32);
                        int edgeVal = row[31 * 4 + 0];
                        int flatLeft = row[10 * 4 + 0];
                        int flatRight = row[50 * 4 + 0];

                        Assert.True(edgeVal > 100, $"Expected edge peak > 100, got {edgeVal}");
                        Assert.Equal(0, flatLeft);  // Flat region suppressed
                        Assert.Equal(0, flatRight); // Flat region suppressed
                    }
                }
            }
        }

        [Fact]
        public void ImagePipeline_GammaCorrection_TransformsDynamicRange()
        {
            D3D11DeviceManager.EnsureInitialized();
            if (!D3D11DeviceManager.IsSupported) return;

            using (var context = GpuImageContext.CreateDefault())
            using (var src = ImageBuffer.CreateBgra32(32, 32))
            {
                // Mid-gray (128, 128, 128) ~ 0.502
                src.Clear(128, 128, 128, 255);

                // 1. Darken with Gamma = 2.0 (0.502 ^ 2.0 = ~0.252 -> ~64)
                var darkenPipeline = new ImagePipelineBuilder(context)
                    .AddGamma(2.0f)
                    .Compile();

                using (var darkDst = darkenPipeline.Execute(src))
                {
                    unsafe
                    {
                        byte* p = darkDst.GetRowPointer(16);
                        int val = p[16 * 4 + 0];
                        Assert.InRange(val, 55, 75);
                    }
                }

                // 2. Brighten with Gamma = 0.5 (sqrt(0.502) = ~0.708 -> ~180)
                var brightenPipeline = new ImagePipelineBuilder(context)
                    .AddGamma(0.5f)
                    .Compile();

                using (var brightDst = brightenPipeline.Execute(src))
                {
                    unsafe
                    {
                        byte* p = brightDst.GetRowPointer(16);
                        int val = p[16 * 4 + 0];
                        Assert.InRange(val, 170, 190);
                    }
                }
            }
        }
    }
}

